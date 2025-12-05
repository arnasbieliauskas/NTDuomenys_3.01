using System;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using RpaAruodas.Services;

namespace RpaAruodas;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var serviceProvider = BuildServices();
        App.ConfigureServices(serviceProvider);

        var logService = serviceProvider.GetRequiredService<ILogService>();
        logService.Info("Programa paleista.");

        var playwrightRunner = serviceProvider.GetRequiredService<IPlaywrightRunner>();
        serviceProvider.GetRequiredService<IAruodasAutomationService>();

        try
        {
            var databaseService = serviceProvider.GetRequiredService<IDatabaseService>();
            databaseService.InitializeAsync().GetAwaiter().GetResult();
            logService.Info("Pradinės paslaugos sėkmingai inicijuotos.");

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            logService.Error("Kritinė klaida startuojant aplikaciją.", ex);
            throw;
        }
        finally
        {
            logService.Info("Programa uždaryta.");
            playwrightRunner.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILogService, LogService>();
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<IDatabaseService, DatabaseService>();
        services.AddSingleton<IAruodasNotificationService, AvaloniaNotificationService>();
        services.AddSingleton<IPlaywrightRunner, PlaywrightRunner>();
        services.AddSingleton<IAruodasAutomationService, AruodasAutomationService>();
        return services.BuildServiceProvider();
    }
}
