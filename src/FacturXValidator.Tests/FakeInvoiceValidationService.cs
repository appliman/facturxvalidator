using FacturXValidator.Models;
using FacturXValidator.Services;

namespace FacturXValidator.Tests;

internal sealed class FakeInvoiceValidationService : IFacturXValidationService
{
    public List<StoredFile> ValidatedFiles { get; } = [];

    public Task<ValidationReport> ValidateAsync(StoredFile file, CancellationToken cancellationToken)
    {
        ValidatedFiles.Add(file);

        var report = new ValidationReport
        {
            FileName = file.SafeDisplayName,
            UploadedAt = file.UploadedAt
        };

        if (file.SafeDisplayName.Contains("non-conforme", StringComparison.OrdinalIgnoreCase))
        {
            PopulateInvalidReport(report);
        }
        else
        {
            PopulateValidReport(report);
        }

        return Task.FromResult(report);
    }

    private static void PopulateValidReport(ValidationReport report)
    {
        report.Metadata.HasEmbeddedXml = true;
        report.Metadata.EmbeddedXmlFileName = "factur-x.xml";
        report.Metadata.FacturXVersion = "1.0";
        report.Metadata.Profile = FacturXProfile.En16931;
        report.Metadata.InvoiceNumber = "FV-2026-001";
        report.Metadata.InvoiceDate = new DateOnly(2026, 5, 31);
        report.Metadata.Seller = "Appliman";
        report.Metadata.Buyer = "Client test";
        report.Metadata.TotalWithoutTax = 100m;
        report.Metadata.TotalTax = 20m;
        report.Metadata.TotalWithTax = 120m;
        report.Metadata.Currency = "EUR";
        report.Information.Add(new ValidationIssue
        {
            Severity = ValidationIssueSeverity.Information,
            Message = "Facture valide pour le test d'integration."
        });
    }

    private static void PopulateInvalidReport(ValidationReport report)
    {
        report.Metadata.HasEmbeddedXml = false;
        report.Metadata.InvoiceNumber = "FNC-2026-001";
        report.Metadata.InvoiceDate = new DateOnly(2026, 5, 31);
        report.Metadata.Buyer = "Client test";
        report.Metadata.TotalWithoutTax = 100m;
        report.Metadata.TotalTax = 19m;
        report.Metadata.TotalWithTax = 120m;
        report.Metadata.Currency = "EUR";
        report.Errors.Add(new ValidationIssue
        {
            Severity = ValidationIssueSeverity.Error,
            Message = "Champ obligatoire manquant : vendeur."
        });
        report.Errors.Add(new ValidationIssue
        {
            Severity = ValidationIssueSeverity.Error,
            Message = "Aucun XML embarque compatible Factur-X n'a ete detecte dans le PDF."
        });
    }
}
