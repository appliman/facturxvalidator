namespace FacturXValidator.Models;

public sealed class ValidationIssue
{
    public required ValidationIssueSeverity Severity { get; init; }

    public required string Message { get; init; }
}
