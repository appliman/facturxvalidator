namespace FacturXValidator.Models;

public sealed class ExtractedInvoiceMetadata
{
    public FacturXProfile? Profile { get; set; }

    public bool HasEmbeddedXml { get; set; }

    public string? EmbeddedXmlFileName { get; set; }

    public string? FacturXVersion { get; set; }

    public string? InvoiceNumber { get; set; }

    public DateOnly? InvoiceDate { get; set; }

    public string? Seller { get; set; }

    public string? Buyer { get; set; }

    public decimal? TotalWithoutTax { get; set; }

    public decimal? TotalTax { get; set; }

    public decimal? TotalWithTax { get; set; }

    public string? Currency { get; set; }
}
