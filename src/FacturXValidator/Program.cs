using FacturXValidator.Components;
using FacturXValidator.Models;
using FacturXValidator.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<InvoiceUploadOptions>(builder.Configuration.GetSection("Upload"));
builder.Services.Configure<TemporaryFileOptions>(builder.Configuration.GetSection("TemporaryFiles"));
builder.Services.Configure<FacturXOptions>(builder.Configuration.GetSection("FacturX"));
builder.Services.Configure<AdsOptions>(builder.Configuration.GetSection("Ads"));

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<IFileStorageService, LocalTemporaryFileStorageService>();
builder.Services.AddScoped<IFacturXValidationService, FacturXValidationService>();
builder.Services.AddSingleton<ISchemaValidationService, BasicSchemaValidationService>();
builder.Services.AddHostedService<TemporaryFileCleanupService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
