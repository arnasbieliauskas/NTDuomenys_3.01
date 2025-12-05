using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace RpaAruodas.Services;

public interface IPlaywrightRunner : IAsyncDisposable
{
    event EventHandler? SearchTriggered;
    int ViewportWidth { get; }
    int ViewportHeight { get; }
    Task<byte[]> NavigateAndCaptureAsync(string url, int width, int height, CancellationToken cancellationToken);
    Task<byte[]> CaptureAsync(CancellationToken cancellationToken);
    Task MouseMoveAsync(double x, double y, CancellationToken cancellationToken);
    Task MouseDownAsync(double x, double y, MouseButton button, CancellationToken cancellationToken);
    Task MouseUpAsync(double x, double y, MouseButton button, CancellationToken cancellationToken);
    Task MouseWheelAsync(double deltaX, double deltaY, CancellationToken cancellationToken);
    Task TypeTextAsync(string text, CancellationToken cancellationToken);
    Task PressKeyAsync(string key, CancellationToken cancellationToken);
    Task UsePageAsync(Func<IPage, Task> work, CancellationToken cancellationToken, bool ensureAruodas = true);
    Task<T> UsePageAsync<T>(Func<IPage, Task<T>> work, CancellationToken cancellationToken, bool ensureAruodas = true);
    Task CloseAsync();
}

public enum MouseButton
{
    Left,
    Right,
    Middle
}

public class PlaywrightRunner : IPlaywrightRunner
{
    public event EventHandler? SearchTriggered;
    private static readonly SemaphoreSlim InstallLock = new(1, 1);
    private static bool _isInstalled;
    private static readonly string[] BrowserArgs =
    {
        "--disable-blink-features=AutomationControlled",
        "--disable-dev-shm-usage"
    };
    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36";
    private const string Locale = "lt-LT";
    private const string Timezone = "Europe/Vilnius";
    private const string SearchButtonMarker = "NTD_SEARCH_BUTTON_CLICKED";

    private readonly ILogService _logService;
    private readonly IConfigurationService _configurationService;
    private readonly SemaphoreSlim _sync = new(1, 1);

    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private IPage? _page;
    private int _viewportWidth = 1200;
    private int _viewportHeight = 720;
    private bool _pageEventsAttached;
    private bool _disposed;
    private bool _headless;

    public PlaywrightRunner(ILogService logService, IConfigurationService configurationService)
    {
        _logService = logService;
        _configurationService = configurationService;
        _headless = _configurationService.Current.Playwright.Headless;
        _logService.Info($"Playwright nustatytas headless={_headless} (appsettings.json).");
    }

    public int ViewportWidth => _viewportWidth;
    public int ViewportHeight => _viewportHeight;

    public async Task<byte[]> NavigateAndCaptureAsync(string url, int width, int height, CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async page =>
        {
            await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle
            });
            _logService.Info($"Playwright įkėlė {url}.");
            return await page.ScreenshotAsync(new PageScreenshotOptions());
        }, width, height, cancellationToken, ensureAruodas: false);
    }

    public async Task<byte[]> CaptureAsync(CancellationToken cancellationToken)
    {
        return await ExecuteAsync(page => page.ScreenshotAsync(new PageScreenshotOptions()), null, null, cancellationToken);
    }

    public Task MouseMoveAsync(double x, double y, CancellationToken cancellationToken)
        => ExecuteAsync(page => page.Mouse.MoveAsync((float)x, (float)y), null, null, cancellationToken);

    public Task MouseDownAsync(double x, double y, MouseButton button, CancellationToken cancellationToken)
        => ExecuteAsync(async page =>
        {
            await page.Mouse.MoveAsync((float)x, (float)y);
            await page.Mouse.DownAsync(new MouseDownOptions { Button = ToPlaywrightButton(button) });
        }, null, null, cancellationToken);

    public Task MouseUpAsync(double x, double y, MouseButton button, CancellationToken cancellationToken)
        => ExecuteAsync(async page =>
        {
            await page.Mouse.MoveAsync((float)x, (float)y);
            await page.Mouse.UpAsync(new MouseUpOptions { Button = ToPlaywrightButton(button) });
        }, null, null, cancellationToken);

    public Task MouseWheelAsync(double deltaX, double deltaY, CancellationToken cancellationToken)
        => ExecuteAsync(page => page.Mouse.WheelAsync((float)deltaX, (float)deltaY), null, null, cancellationToken);

    public Task TypeTextAsync(string text, CancellationToken cancellationToken)
        => ExecuteAsync(page => page.Keyboard.TypeAsync(text), null, null, cancellationToken);

    public Task PressKeyAsync(string key, CancellationToken cancellationToken)
        => ExecuteAsync(page => page.Keyboard.PressAsync(key), null, null, cancellationToken);

    public Task UsePageAsync(Func<IPage, Task> work, CancellationToken cancellationToken, bool ensureAruodas = true)
        => ExecuteAsync(async page =>
        {
            await work(page);
            return true;
        }, null, null, cancellationToken, ensureAruodas);

    public Task<T> UsePageAsync<T>(Func<IPage, Task<T>> work, CancellationToken cancellationToken, bool ensureAruodas = true)
        => ExecuteAsync(work, null, null, cancellationToken, ensureAruodas);

    public async Task CloseAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _sync.WaitAsync();
        try
        {
            await CloseBrowserResourcesAsync(false);
        }
        finally
        {
            _sync.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _sync.WaitAsync();
        try
        {
            await CloseBrowserResourcesAsync(true);
        }
        finally
        {
            _sync.Release();
        }
    }

    private Task ExecuteAsync(Func<IPage, Task> work, int? width, int? height, CancellationToken cancellationToken, bool ensureAruodas = true)
        => ExecuteAsync(async page =>
        {
            await work(page);
            return true;
        }, width, height, cancellationToken, ensureAruodas);

    private async Task<T> ExecuteAsync<T>(Func<IPage, Task<T>> work, int? width, int? height, CancellationToken cancellationToken, bool ensureAruodas = true)
    {
        await EnsureBrowsersInstalledAsync(cancellationToken);
        await _sync.WaitAsync(cancellationToken);
        try
        {
            var page = await EnsurePageAsync(width, height, cancellationToken);
            if (ensureAruodas)
            {
                await EnsureAruodasFocusAsync(page);
            }
            return await work(page);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logService.Error("Playwright veiksmas nesėkmingas.", ex);
            throw;
        }
        finally
        {
            _sync.Release();
        }
    }

    private async Task<IPage> EnsurePageAsync(int? width, int? height, CancellationToken cancellationToken)
    {
        _playwright ??= await Microsoft.Playwright.Playwright.CreateAsync();
        _browser ??= await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = _headless,
            Args = BrowserArgs,
            IgnoreDefaultArgs = new[] { "--enable-automation" }
        });

        var targetWidth = width ?? _viewportWidth;
        var targetHeight = height ?? _viewportHeight;

        if (_context is null)
        {
            _context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = targetWidth, Height = targetHeight },
                ScreenSize = new ScreenSize { Width = targetWidth, Height = targetHeight },
                UserAgent = DesktopUserAgent,
                Locale = Locale,
                TimezoneId = Timezone,
                ColorScheme = ColorScheme.Light,
                HasTouch = false,
                IsMobile = false,
                DeviceScaleFactor = 1
            });
            await ApplyStealthScriptsAsync(_context);
        }

        if (_page is null)
        {
            _page = await _context.NewPageAsync();
            AttachPageEvents(_page);
        }

        if (_viewportWidth != targetWidth || _viewportHeight != targetHeight)
        {
            await _page.SetViewportSizeAsync(targetWidth, targetHeight);
        }

        _viewportWidth = targetWidth;
        _viewportHeight = targetHeight;
        return _page;
    }

    private async Task CloseBrowserResourcesAsync(bool disposePlaywright)
    {
        if (_page is not null)
        {
            try
            {
                await _page.CloseAsync();
            }
            catch (Exception ex)
            {
                _logService.Error("Nepavyko uzdaryti puslapio.", ex);
            }
        }

        if (_context is not null)
        {
            try
            {
                await _context.CloseAsync();
            }
            catch (Exception ex)
            {
                _logService.Error("Nepavyko uzdaryti konteksto.", ex);
            }
        }

        if (_browser is not null)
        {
            try
            {
                await _browser.CloseAsync();
            }
            catch (Exception ex)
            {
                _logService.Error("Nepavyko uzdaryti narsykles.", ex);
            }
        }

        _page = null;
        _context = null;
        _browser = null;
        _pageEventsAttached = false;

        if (disposePlaywright && _playwright is not null)
        {
            _playwright.Dispose();
            _playwright = null;
        }
    }

    private async Task EnsureAruodasFocusAsync(IPage page)
    {
        var url = page.Url;
        if (string.IsNullOrWhiteSpace(url) || IsAruodasUrl(url))
        {
            return;
        }

        _logService.Info($"Aruodas.lt fokusas prarastas (dabartinis adresas: {url}). Atkuriame puslapi.");
        await page.GotoAsync("https://www.aruodas.lt", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle
        });
        _logService.Info("Aruodas.lt puslapis perkrautas po nukreipimo.");
    }

    private static bool IsAruodasUrl(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
           uri.Host.EndsWith("aruodas.lt", StringComparison.OrdinalIgnoreCase);

    private static Task ApplyStealthScriptsAsync(IBrowserContext context)
    {
        const string template = """
                                Object.defineProperty(navigator, 'webdriver', { get: () => undefined });
                                window.navigator.chrome = window.navigator.chrome || { runtime: {} };
                                Object.defineProperty(navigator, 'languages', { get: () => ['lt-LT', 'lt', 'en-US'] });
                                Object.defineProperty(navigator, 'plugins', { get: () => [1, 2, 3, 4, 5] });
                                document.addEventListener('click', event => {
                                    const target = event.target?.closest('#buttonSearchForm');
                                    if (target) {
                                        console.log('__SEARCH_MARKER__');
                                    }
                                });
                                """;
        var script = template.Replace("__SEARCH_MARKER__", SearchButtonMarker);
        return context.AddInitScriptAsync(script);
    }

    private void AttachPageEvents(IPage page)
    {
        if (_pageEventsAttached)
        {
            return;
        }

        page.Console += (_, message) =>
        {
            if (string.Equals(message.Text, SearchButtonMarker, StringComparison.Ordinal))
            {
                _logService.Info("Paspaustas Aruodas.lt 'Ieškoti' mygtuka.");
                SearchTriggered?.Invoke(this, EventArgs.Empty);
            }
        };

        _pageEventsAttached = true;
    }

    private static async Task EnsureBrowsersInstalledAsync(CancellationToken cancellationToken)
    {
        if (_isInstalled)
        {
            return;
        }

        await InstallLock.WaitAsync(cancellationToken);
        try
        {
            if (_isInstalled)
            {
                return;
            }

            await Task.Run(() => Microsoft.Playwright.Program.Main(new[] { "install", "chromium" }), cancellationToken);
            _isInstalled = true;
        }
        finally
        {
            InstallLock.Release();
        }
    }

    private static Microsoft.Playwright.MouseButton ToPlaywrightButton(MouseButton button)
        => button switch
        {
            MouseButton.Middle => Microsoft.Playwright.MouseButton.Middle,
            MouseButton.Right => Microsoft.Playwright.MouseButton.Right,
            _ => Microsoft.Playwright.MouseButton.Left
        };
}
