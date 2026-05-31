using FacturXValidator.Models;

namespace FacturXValidator.Services;

public interface IFacturXValidationService
{
    Task<ValidationReport> ValidateAsync(StoredFile file, CancellationToken cancellationToken);
}
