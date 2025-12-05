using System.Threading.Tasks;

namespace RpaAruodas.Services;

public interface IAruodasNotificationService
{
    void ShowAutomationResult(AutomationCompletedEventArgs args);
    void RegisterHostWindow(MainWindow window);
}

public class AutomationCompletedEventArgs : System.EventArgs
{
    public AutomationCompletedEventArgs(bool success, int found, int collected, int inserted, int skipped, string? errorMessage)
    {
        Success = success;
        Found = found;
        Collected = collected;
        Inserted = inserted;
        Skipped = skipped;
        ErrorMessage = errorMessage;
    }

    public bool Success { get; }
    public int Found { get; }
    public int Collected { get; }
    public int Inserted { get; }
    public int Skipped { get; }
    public string? ErrorMessage { get; }
}
