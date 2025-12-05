using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using RpaAruodas.Services;

namespace RpaAruodas;

public partial class App : Application
{
    private static IServiceProvider? _services;

    public static void ConfigureServices(IServiceProvider services) => _services = services;

    public static T GetRequiredService<T>() where T : notnull
    {
        if (_services is null)
        {
            throw new InvalidOperationException("Services are not initialized.");
        }

        return _services.GetRequiredService<T>();
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            var notificationService = GetRequiredService<IAruodasNotificationService>();
            notificationService.RegisterHostWindow(window);
            desktop.MainWindow = window;
            GetRequiredService<ILogService>().Info("Pagrindinis langas paruotas vartotojui.");
        }

        base.OnFrameworkInitializationCompleted();
    }
}
