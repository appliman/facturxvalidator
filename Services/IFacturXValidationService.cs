using FacturXValidatorSaas.Models;

namespace FacturXValidatorSaas.Services;

public interface IFacturXValidationService
{
    Task<ValidationReport> ValidateAsync(StoredFile file, CancellationToken cancellationToken);
}
