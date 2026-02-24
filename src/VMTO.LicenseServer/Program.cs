using VMTO.Infrastructure;
using VMTO.LicenseServer.Endpoints;
using VMTO.LicenseServer.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "VMTO License Server", Version = "v1" });
});

builder.Services.AddSingleton<LicenseGenerationService>();
builder.Services.AddSingleton<LicenseValidationService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapLicenseServerEndpoints();
app.MapHealthChecks("/health");

app.Run();
