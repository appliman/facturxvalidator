namespace FacturXValidatorSaas.Services;

public sealed class InvalidInvoiceUploadException(string message) : Exception(message);
