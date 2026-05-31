using System.Xml.Linq;
using FacturXValidator.Models;
using Microsoft.Extensions.Options;

namespace FacturXValidator.Services;

public sealed class BasicSchemaValidationService(
    IOptions<FacturXOptions> options,
    ILogger<BasicSchemaValidationService> logger) : ISchemaValidationService
{
    public Task<IReadOnlyList<ValidationIssue>> ValidateAsync(XDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        var issues = new List<ValidationIssue>();
        var schemasPath = options.Value.SchemasPath;

        if (!Directory.Exists(schemasPath))
        {
            logger.LogInformation("Factur-X schema directory is not available at configured path.");
            issues.Add(new ValidationIssue
            {
                Severity = ValidationIssueSeverity.Information,
                Message = "Validation XSD/Schematron complète non exécutée : aucun schéma officiel n'est configuré."
            });
        }
        else
        {
            issues.Add(new ValidationIssue
            {
                Severity = ValidationIssueSeverity.Information,
                Message = "Point d'extension prêt pour les schémas XSD/Schematron officiels Factur-X."
            });
        }

        return Task.FromResult<IReadOnlyList<ValidationIssue>>(issues);
    }
}
