using System;
using Avalonia.Controls;
using Avalonia.Threading;

namespace RpaAruodas.Services;

public class AvaloniaNotificationService : IAruodasNotificationService
{
    private Window? _host;

    public void RegisterHostWindow(MainWindow window)
    {
        _host = window;
    }

    public void ShowAutomationResult(AutomationCompletedEventArgs args)
    {
        // UI atnaujinima daro pagrindinis langas per blur overlay, papildomo dialogo neberodome.
    }
}
