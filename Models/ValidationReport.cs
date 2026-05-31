namespace FacturXValidatorSaas.Models;

public sealed class ValidationReport
{
    public required string FileName { get; init; }

    public DateTimeOffset UploadedAt { get; init; }

    public bool IsValid => Errors.Count == 0 && Warnings.Count == 0;

    public string Level
    {
        get
        {
            if (Errors.Count > 0)
            {
                return "Non conforme";
            }

            return Warnings.Count > 0 ? "Avertissements" : "Conforme";
        }
    }

    public List<ValidationIssue> Errors { get; } = [];

    public List<ValidationIssue> Warnings { get; } = [];

    public List<ValidationIssue> Information { get; } = [];

    public ExtractedInvoiceMetadata Metadata { get; } = new();
}
