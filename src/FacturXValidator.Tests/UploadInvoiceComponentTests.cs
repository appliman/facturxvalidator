using Bunit;
using FacturXValidator.Components.Shared;
using FacturXValidator.Models;
using FacturXValidator.Services;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FacturXValidator.Tests;

[TestClass]
public sealed class UploadInvoiceComponentTests
{
    [TestMethod]
    public void UploadAndAnalyze_DisplaysValidAndInvalidInvoiceReports()
    {
        using var context = new BunitContext();
        var fileStorage = new CapturingFileStorageService();
        var validationService = new FakeInvoiceValidationService();

        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var uploadDropZoneModule = context.JSInterop.SetupModule("./js/uploadDropZone.js");
        uploadDropZoneModule.SetupVoid("initializeUploadDropZone", _ => true);
        uploadDropZoneModule.SetupVoid("disposeUploadDropZone", _ => true);

        context.Services.AddLogging();
        context.Services.AddSingleton<IFileStorageService>(fileStorage);
        context.Services.AddSingleton<IFacturXValidationService>(validationService);
        context.Services.AddSingleton(Options.Create(new InvoiceUploadOptions
        {
            MaxFilesPerBatch = 10,
            MaxFileSizeMb = 20
        }));

        var component = context.Render<UploadInvoiceComponent>();
        var validInvoice = CreateInvoiceUpload("facture-conforme.pdf");
        var invalidInvoice = CreateInvoiceUpload("facture-non-conforme.pdf");

        component.FindComponent<InputFile>().UploadFiles(validInvoice, invalidInvoice);
        component.Find("button.btn-primary").Click();

        component.WaitForAssertion(() =>
        {
            Assert.HasCount(2, fileStorage.StoredFiles);
            Assert.HasCount(2, validationService.ValidatedFiles);
            Assert.Contains("facture-conforme.pdf", component.Markup);
            Assert.Contains("facture-non-conforme.pdf", component.Markup);
            Assert.Contains("Conforme", component.Markup);
            Assert.Contains("Non conforme", component.Markup);
            Assert.Contains("EN16931", component.Markup);
            Assert.Contains("Champ obligatoire manquant : vendeur.", component.Markup);
        });
    }

    private static InputFileContent CreateInvoiceUpload(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "Invoices", fileName);
        return InputFileContent.CreateFromBinary(File.ReadAllBytes(path), fileName, contentType: "application/pdf");
    }
}
