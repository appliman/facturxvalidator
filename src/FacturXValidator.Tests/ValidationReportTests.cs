using FacturXValidator.Models;

namespace FacturXValidator.Tests;

[TestClass]
public sealed class ValidationReportTests
{
    [TestMethod]
    public void IsValid_ReturnsFalse_WhenWarningsArePresent()
    {
        var report = new ValidationReport
        {
            FileName = "invoice.pdf",
            UploadedAt = DateTimeOffset.UtcNow
        };

        report.Warnings.Add(new ValidationIssue
        {
            Severity = ValidationIssueSeverity.Warning,
            Message = "Missing optional metadata."
        });

        Assert.IsFalse(report.IsValid);
        Assert.AreEqual("Avertissements", report.Level);
    }
}
