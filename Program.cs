using AsteriskAMIStream.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddMvc();
builder.Services.AddControllers();  // Ensure controllers are added before building the app

var corsPolicyName = "AllowAa5jc";

builder.Services.AddCors(options =>
{
    options.AddPolicy(name: corsPolicyName, policy =>
    {
        policy.WithOrigins("https://aa5jc.com")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Load configuration from appsettings.json or prompt user if not found or invalid
string configFilePath = Path.Combine("/home/repeater", "amiSettings.json"); /// TODO: Make this configurable
IConfiguration configuration;

var rebuildConfigFile = false;
// Check if config file exists
if (File.Exists(configFilePath))
{
    // Reload the configuration with the updated file
    configuration = builder.Configuration.AddJsonFile(configFilePath, optional: false, reloadOnChange: true)
        .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
        .Build();

    // Validate if AMISettings section exists and is valid
    var amiSettings = builder.Configuration.GetSection("AMISettings").Get<List<AMISettings>>();

    if (amiSettings == null || !amiSettings.Any())
    {
        rebuildConfigFile = true;
    }
}
else
{
    rebuildConfigFile = true;
}

if (rebuildConfigFile)
{
    AMISettings.PromptForConfiguration(configFilePath);
}

// Reload the configuration with the updated file
builder.Configuration.AddJsonFile(configFilePath, optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .Build();

// Register services
builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

builder.WebHost.ConfigureKestrel((context, options) =>
{
    options.Configure(context.Configuration.GetSection("Kestrel"));
});

var app = builder.Build();

// Load node metadata
await AsteriskAMIStream.Services.MetadataService.DownloadAndCacheMetadata();

app.UseCors(corsPolicyName);

app.MapControllers();  // Now the controllers are mapped after the services are configured

app.Run();

