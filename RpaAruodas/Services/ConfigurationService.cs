using System;
using Microsoft.Extensions.Configuration;
using RpaAruodas.Configuration;

namespace RpaAruodas.Services;

public interface IConfigurationService
{
    AppConfiguration Current { get; }
}

public class ConfigurationService : IConfigurationService
{
    public AppConfiguration Current { get; }

    public ConfigurationService(ILogService logService)
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);

        var configuration = builder.Build();
        Current = configuration.Get<AppConfiguration>() ?? new AppConfiguration();
        logService.Info("Konfigūracija nuskaityta iš appsettings.json.");
    }
}
