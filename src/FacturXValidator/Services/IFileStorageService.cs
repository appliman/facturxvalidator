using FacturXValidator.Models;
using Microsoft.AspNetCore.Components.Forms;

namespace FacturXValidator.Services;

public interface IFileStorageService
{
    Task<StoredFile> StoreAsync(IBrowserFile file, CancellationToken cancellationToken);

    Task DeleteAsync(StoredFile file, CancellationToken cancellationToken);
}
