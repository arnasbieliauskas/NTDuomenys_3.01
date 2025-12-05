namespace RpaAruodas.Configuration;

public class AppConfiguration
{
    public WindowSettings Window { get; init; } = new();
    public DatabaseSettings Database { get; init; } = new();
    public PlaywrightSettings Playwright { get; init; } = new();
}

public class WindowSettings
{
    public string Title { get; init; } = "NT Duomenys";
    public double Width { get; init; } = 1024;
    public double Height { get; init; } = 768;
    public double MinWidth { get; init; } = 800;
    public double MinHeight { get; init; } = 600;
    public string State { get; init; } = "Normal";
    public string StartupLocation { get; init; } = "CenterScreen";
    public bool CanResize { get; init; } = true;
}

public class DatabaseSettings
{
    public string FilePath { get; init; } = "storage/ntduomenys.db";
}

public class PlaywrightSettings
{
    public bool Headless { get; init; } = true;
}
