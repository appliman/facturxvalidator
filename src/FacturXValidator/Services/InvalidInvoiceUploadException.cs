namespace FacturXValidator.Services;

public sealed class InvalidInvoiceUploadException(string message) : Exception(message);
