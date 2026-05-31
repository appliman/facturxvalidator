using System.Xml.Linq;
using FacturXValidator.Models;

namespace FacturXValidator.Services;

public interface ISchemaValidationService
{
    Task<IReadOnlyList<ValidationIssue>> ValidateAsync(XDocument document, CancellationToken cancellationToken);
}
