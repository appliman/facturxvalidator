namespace FacturXValidatorSaas.Models;

public sealed class FacturXOptions
{
    public string SchemasPath { get; set; } = "/app/data/schemas";

    public decimal AmountTolerance { get; set; } = 0.02m;
}
