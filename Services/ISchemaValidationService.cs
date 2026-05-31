using System.Xml.Linq;
using FacturXValidatorSaas.Models;

namespace FacturXValidatorSaas.Services;

public interface ISchemaValidationService
{
    Task<IReadOnlyList<ValidationIssue>> ValidateAsync(XDocument document, CancellationToken cancellationToken);
}
