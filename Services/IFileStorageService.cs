using FacturXValidatorSaas.Models;
using Microsoft.AspNetCore.Components.Forms;

namespace FacturXValidatorSaas.Services;

public interface IFileStorageService
{
    Task<StoredFile> StoreAsync(IBrowserFile file, CancellationToken cancellationToken);
}
