using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using FacturXValidator.Models;
using Microsoft.Extensions.Options;
using PdfSharp.Pdf.IO;

namespace FacturXValidator.Services;

public sealed partial class FacturXValidationService(
    ISchemaValidationService schemaValidationService,
    IOptions<FacturXOptions> options,
    ILogger<FacturXValidationService> logger) : IFacturXValidationService
{
    private static readonly string[] FacturXNames =
    [
        "factur-x.xml",
        "zugferd-invoice.xml",
        "xrechnung.xml"
    ];

    public async Task<ValidationReport> ValidateAsync(StoredFile file, CancellationToken cancellationToken)
    {
        var report = new ValidationReport
        {
            FileName = file.SafeDisplayName,
            UploadedAt = file.UploadedAt
        };

        byte[] pdfBytes;
        try
        {
            pdfBytes = await File.ReadAllBytesAsync(file.FullPath, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to read uploaded file for validation.");
            AddError(report, "Impossible de lire le fichier stocké temporairement.");
            return report;
        }

        ValidatePdfStructure(file.FullPath, pdfBytes, report);

        var xmlCandidate = ExtractXmlCandidate(pdfBytes, report);
        if (xmlCandidate is null)
        {
            AddError(report, "Aucun XML embarqué compatible Factur-X n'a été détecté dans le PDF.");
            AddInfo(report, "Limite connue : les pièces jointes PDF très spécifiques peuvent nécessiter un extracteur PDF avancé ou une validation XSD/Schematron officielle.");
            return report;
        }

        report.Metadata.HasEmbeddedXml = true;
        report.Metadata.EmbeddedXmlFileName = xmlCandidate.FileName;

        if (string.IsNullOrWhiteSpace(xmlCandidate.FileName))
        {
            AddWarning(report, "Le XML embarqué est détecté, mais son nom de fichier n'a pas pu être confirmé.");
        }
        else if (!FacturXNames.Contains(xmlCandidate.FileName, StringComparer.OrdinalIgnoreCase))
        {
            AddWarning(report, $"Le XML embarqué s'appelle \"{xmlCandidate.FileName}\" au lieu de factur-x.xml.");
        }

        XDocument document;
        try
        {
            document = LoadXmlSecurely(xmlCandidate.Xml);
            AddInfo(report, "Le XML embarqué est bien formé.");
        }
        catch (XmlException ex)
        {
            logger.LogInformation(ex, "Embedded XML is not well formed.");
            AddError(report, "Le XML embarqué n'est pas bien formé.");
            return report;
        }

        ValidateCiiSyntax(document, report);
        ExtractMetadata(document, report);
        ValidateRequiredFields(document, report);
        ValidateAmounts(report);

        var schemaIssues = await schemaValidationService.ValidateAsync(document, cancellationToken);
        AddIssues(report, schemaIssues);

        return report;
    }

    private static void ValidatePdfStructure(string path, byte[] pdfBytes, ValidationReport report)
    {
        if (pdfBytes.Length < 5 || Encoding.ASCII.GetString(pdfBytes, 0, 5) != "%PDF-")
        {
            AddError(report, "La signature du fichier ne correspond pas à un PDF valide.");
            return;
        }

        AddInfo(report, "La signature PDF %PDF- est présente.");

        try
        {
            using var document = PdfReader.Open(path, PdfDocumentOpenMode.Import);
            AddInfo(report, $"PDF lisible : {document.PageCount} page(s).");
        }
        catch
        {
            AddError(report, "Le PDF ne peut pas être ouvert par le moteur de lecture PDF.");
        }

        var pdfText = DecodeLatin1(pdfBytes);
        if (pdfText.Contains("pdfaid:part>3", StringComparison.OrdinalIgnoreCase) ||
            pdfText.Contains("<pdfaid:part>3", StringComparison.OrdinalIgnoreCase) ||
            pdfText.Contains("/pdfaid:part", StringComparison.OrdinalIgnoreCase))
        {
            AddInfo(report, "Des métadonnées PDF/A-3 semblent présentes.");
        }
        else
        {
            AddWarning(report, "Compatibilité PDF/A-3 non confirmée. Une validation PDF/A complète nécessite un validateur spécialisé.");
        }
    }

    private static EmbeddedXmlCandidate? ExtractXmlCandidate(byte[] pdfBytes, ValidationReport report)
    {
        var rawText = DecodeLatin1(pdfBytes);
        var fileName = DetectEmbeddedFileName(rawText);

        if (fileName is not null)
        {
            AddInfo(report, $"Nom de pièce jointe XML détecté : {fileName}.");
        }

        var rawCandidate = TryFindInvoiceXml(rawText, fileName);
        if (rawCandidate is not null)
        {
            return rawCandidate;
        }

        foreach (var streamBytes in EnumeratePdfStreams(pdfBytes))
        {
            var streamText = DecodeUtf8OrLatin1(streamBytes);
            var candidate = TryFindInvoiceXml(streamText, fileName);
            if (candidate is not null)
            {
                return candidate;
            }
        }

        return null;
    }

    private static EmbeddedXmlCandidate? TryFindInvoiceXml(string text, string? fileName)
    {
        var starts = new[]
        {
            text.IndexOf("<rsm:CrossIndustryInvoice", StringComparison.OrdinalIgnoreCase),
            text.IndexOf("<CrossIndustryInvoice", StringComparison.OrdinalIgnoreCase),
            text.IndexOf("<?xml", StringComparison.OrdinalIgnoreCase)
        }.Where(index => index >= 0).Order().ToArray();

        foreach (var start in starts)
        {
            var xml = SliceLikelyXml(text[start..]);
            if (xml is null)
            {
                continue;
            }

            if (xml.Contains("CrossIndustryInvoice", StringComparison.OrdinalIgnoreCase) ||
                xml.Contains("urn:un:unece:uncefact:data:standard:CrossIndustryInvoice", StringComparison.OrdinalIgnoreCase))
            {
                return new EmbeddedXmlCandidate(fileName, xml);
            }
        }

        return null;
    }

    private static string? SliceLikelyXml(string text)
    {
        var end = text.IndexOf("</rsm:CrossIndustryInvoice>", StringComparison.OrdinalIgnoreCase);
        var closeLength = "</rsm:CrossIndustryInvoice>".Length;

        if (end < 0)
        {
            end = text.IndexOf("</CrossIndustryInvoice>", StringComparison.OrdinalIgnoreCase);
            closeLength = "</CrossIndustryInvoice>".Length;
        }

        if (end < 0)
        {
            return null;
        }

        return text[..(end + closeLength)];
    }

    private static IEnumerable<byte[]> EnumeratePdfStreams(byte[] pdfBytes)
    {
        var marker = Encoding.ASCII.GetBytes("stream");
        var endMarker = Encoding.ASCII.GetBytes("endstream");
        var index = 0;

        while ((index = IndexOf(pdfBytes, marker, index)) >= 0)
        {
            var dataStart = index + marker.Length;
            if (dataStart < pdfBytes.Length && pdfBytes[dataStart] == '\r')
            {
                dataStart++;
            }

            if (dataStart < pdfBytes.Length && pdfBytes[dataStart] == '\n')
            {
                dataStart++;
            }

            var dataEnd = IndexOf(pdfBytes, endMarker, dataStart);
            if (dataEnd < 0)
            {
                yield break;
            }

            var stream = pdfBytes[dataStart..TrimStreamEnd(pdfBytes, dataEnd)];
            var dictionaryStart = Math.Max(0, index - 500);
            var dictionary = DecodeLatin1(pdfBytes[dictionaryStart..index]);

            if (dictionary.Contains("/FlateDecode", StringComparison.OrdinalIgnoreCase))
            {
                var inflated = TryInflate(stream);
                if (inflated is not null)
                {
                    yield return inflated;
                }
            }

            yield return stream;
            index = dataEnd + endMarker.Length;
        }
    }

    private static int TrimStreamEnd(byte[] bytes, int end)
    {
        while (end > 0 && (bytes[end - 1] == '\r' || bytes[end - 1] == '\n'))
        {
            end--;
        }

        return end;
    }

    private static byte[]? TryInflate(byte[] bytes)
    {
        try
        {
            using var input = new MemoryStream(bytes);
            using var deflate = new ZLibStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            deflate.CopyTo(output);
            return output.ToArray();
        }
        catch
        {
            try
            {
                using var input = new MemoryStream(bytes);
                using var deflate = new DeflateStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                deflate.CopyTo(output);
                return output.ToArray();
            }
            catch
            {
                return null;
            }
        }
    }

    private static int IndexOf(byte[] haystack, byte[] needle, int start)
    {
        for (var i = start; i <= haystack.Length - needle.Length; i++)
        {
            var matched = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
            {
                return i;
            }
        }

        return -1;
    }

    private static string? DetectEmbeddedFileName(string pdfText)
    {
        foreach (var name in FacturXNames)
        {
            if (pdfText.Contains(name, StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }
        }

        var match = EmbeddedXmlNameRegex().Match(pdfText);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static XDocument LoadXmlSecurely(string xml)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersFromEntities = 0,
            MaxCharactersInDocument = 10_000_000
        };

        using var stringReader = new StringReader(xml);
        using var xmlReader = XmlReader.Create(stringReader, settings);
        return XDocument.Load(xmlReader, LoadOptions.None);
    }

    private static void ValidateCiiSyntax(XDocument document, ValidationReport report)
    {
        var rootName = document.Root?.Name.LocalName;
        var namespaceName = document.Root?.Name.NamespaceName ?? string.Empty;

        if (rootName != "CrossIndustryInvoice")
        {
            AddError(report, "Le document XML n'a pas pour racine CrossIndustryInvoice.");
        }

        if (!namespaceName.Contains("CrossIndustryInvoice", StringComparison.OrdinalIgnoreCase))
        {
            AddError(report, "L'espace de noms XML ne correspond pas à la syntaxe UN/CEFACT CII attendue.");
        }
        else
        {
            AddInfo(report, "Syntaxe UN/CEFACT CII détectée.");
        }
    }

    private static void ExtractMetadata(XDocument document, ValidationReport report)
    {
        report.Metadata.InvoiceNumber = FirstValue(document, "ExchangedDocument", "ID");
        report.Metadata.InvoiceDate = ParseFacturXDate(FirstValue(document, "IssueDateTime", "DateTimeString"));
        report.Metadata.Seller = FirstValue(document, "SellerTradeParty", "Name");
        report.Metadata.Buyer = FirstValue(document, "BuyerTradeParty", "Name");
        report.Metadata.TotalWithoutTax = ParseDecimal(FirstValue(document, "TaxBasisTotalAmount"));
        report.Metadata.TotalTax = ParseDecimal(FirstValue(document, "TaxTotalAmount"));
        report.Metadata.TotalWithTax = ParseDecimal(FirstValue(document, "GrandTotalAmount"));
        report.Metadata.Currency = FirstValue(document, "InvoiceCurrencyCode");

        var guidelineId = FirstValue(document, "GuidelineSpecifiedDocumentContextParameter", "ID");
        report.Metadata.Profile = DetectProfile(guidelineId);
        report.Metadata.FacturXVersion = DetectVersion(guidelineId);

        if (report.Metadata.Profile is not null)
        {
            AddInfo(report, $"Profil Factur-X détecté : {FormatProfile(report.Metadata.Profile.Value)}.");
        }
        else
        {
            AddWarning(report, "Profil Factur-X non détecté dans le contexte documentaire.");
        }
    }

    private static void ValidateRequiredFields(XDocument document, ValidationReport report)
    {
        Require(report, report.Metadata.InvoiceNumber, "numéro de facture");
        Require(report, report.Metadata.InvoiceDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), "date d'émission");
        Require(report, report.Metadata.Seller, "vendeur");
        Require(report, report.Metadata.Buyer, "acheteur");
        Require(report, report.Metadata.TotalWithoutTax?.ToString(CultureInfo.InvariantCulture), "total HT");
        Require(report, report.Metadata.TotalTax?.ToString(CultureInfo.InvariantCulture), "total TVA");
        Require(report, report.Metadata.TotalWithTax?.ToString(CultureInfo.InvariantCulture), "total TTC");
        Require(report, report.Metadata.Currency, "devise");

        var lineCount = document.Descendants().Count(e => e.Name.LocalName == "IncludedSupplyChainTradeLineItem");
        if (report.Metadata.Profile is FacturXProfile.En16931 or FacturXProfile.Extended or FacturXProfile.Basic && lineCount == 0)
        {
            AddError(report, "Aucune ligne de facture n'a été détectée alors que le profil l'exige généralement.");
        }
        else if (lineCount > 0)
        {
            AddInfo(report, $"{lineCount} ligne(s) de facture détectée(s).");
        }
    }

    private void ValidateAmounts(ValidationReport report)
    {
        var totalWithoutTax = report.Metadata.TotalWithoutTax;
        var totalTax = report.Metadata.TotalTax;
        var totalWithTax = report.Metadata.TotalWithTax;

        if (totalWithoutTax is null || totalTax is null || totalWithTax is null)
        {
            return;
        }

        var expectedTotal = totalWithoutTax.Value + totalTax.Value;
        var difference = Math.Abs(totalWithTax.Value - expectedTotal);
        if (difference > options.Value.AmountTolerance)
        {
            AddError(report, $"Incohérence de montants : total TTC ({totalWithTax}) différent de HT + TVA ({expectedTotal}) au-delà de la tolérance configurée.");
        }
        else
        {
            AddInfo(report, "Cohérence simple des montants HT + TVA = TTC validée.");
        }
    }

    private static void Require(ValidationReport report, string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            AddError(report, $"Champ obligatoire manquant : {label}.");
        }
    }

    private static string? FirstValue(XDocument document, params string[] localNamePath)
    {
        IEnumerable<XElement> current = document.Root is null ? [] : [document.Root];
        foreach (var localName in localNamePath)
        {
            current = current.Descendants().Where(e => e.Name.LocalName == localName);
            var first = current.FirstOrDefault();
            if (first is null)
            {
                return null;
            }

            current = [first];
        }

        return current.FirstOrDefault()?.Value.Trim();
    }

    private static DateOnly? ParseFacturXDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var formats = new[] { "yyyyMMdd", "yyyy-MM-dd" };
        return DateOnly.TryParseExact(value.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
    }

    private static decimal? ParseDecimal(string? value)
    {
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }

    private static FacturXProfile? DetectProfile(string? guidelineId)
    {
        if (string.IsNullOrWhiteSpace(guidelineId))
        {
            return null;
        }

        if (guidelineId.Contains("minimum", StringComparison.OrdinalIgnoreCase))
        {
            return FacturXProfile.Minimum;
        }

        if (guidelineId.Contains("basicwl", StringComparison.OrdinalIgnoreCase) ||
            guidelineId.Contains("basic wl", StringComparison.OrdinalIgnoreCase))
        {
            return FacturXProfile.BasicWl;
        }

        if (guidelineId.Contains("basic", StringComparison.OrdinalIgnoreCase))
        {
            return FacturXProfile.Basic;
        }

        if (guidelineId.Contains("en16931", StringComparison.OrdinalIgnoreCase))
        {
            return FacturXProfile.En16931;
        }

        if (guidelineId.Contains("extended", StringComparison.OrdinalIgnoreCase))
        {
            return FacturXProfile.Extended;
        }

        return null;
    }

    private static string? DetectVersion(string? guidelineId)
    {
        if (string.IsNullOrWhiteSpace(guidelineId))
        {
            return null;
        }

        var match = VersionRegex().Match(guidelineId);
        return match.Success ? match.Groups[1].Value.Replace('p', '.') : null;
    }

    private static string FormatProfile(FacturXProfile profile)
    {
        return profile switch
        {
            FacturXProfile.Minimum => "MINIMUM",
            FacturXProfile.BasicWl => "BASIC WL",
            FacturXProfile.Basic => "BASIC",
            FacturXProfile.En16931 => "EN16931",
            FacturXProfile.Extended => "EXTENDED",
            _ => profile.ToString()
        };
    }

    private static string DecodeLatin1(byte[] bytes)
    {
        return Encoding.Latin1.GetString(bytes);
    }

    private static string DecodeUtf8OrLatin1(byte[] bytes)
    {
        try
        {
            return new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return DecodeLatin1(bytes);
        }
    }

    private static void AddIssues(ValidationReport report, IEnumerable<ValidationIssue> issues)
    {
        foreach (var issue in issues)
        {
            switch (issue.Severity)
            {
                case ValidationIssueSeverity.Error:
                    report.Errors.Add(issue);
                    break;
                case ValidationIssueSeverity.Warning:
                    report.Warnings.Add(issue);
                    break;
                default:
                    report.Information.Add(issue);
                    break;
            }
        }
    }

    private static void AddError(ValidationReport report, string message)
    {
        report.Errors.Add(new ValidationIssue { Severity = ValidationIssueSeverity.Error, Message = message });
    }

    private static void AddWarning(ValidationReport report, string message)
    {
        report.Warnings.Add(new ValidationIssue { Severity = ValidationIssueSeverity.Warning, Message = message });
    }

    private static void AddInfo(ValidationReport report, string message)
    {
        report.Information.Add(new ValidationIssue { Severity = ValidationIssueSeverity.Information, Message = message });
    }

    [GeneratedRegex(@"/(?:F|UF)\s*\(([^)]*\.xml)\)", RegexOptions.IgnoreCase)]
    private static partial Regex EmbeddedXmlNameRegex();

    [GeneratedRegex(@"(\d+p\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex VersionRegex();
}
