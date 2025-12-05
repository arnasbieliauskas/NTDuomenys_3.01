using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace RpaAruodas.Services;

public interface IAruodasAutomationService
{
    event EventHandler<AutomationCompletedEventArgs>? AutomationCompleted;
    Task RunSearchScrapeAsync(CancellationToken cancellationToken);
}

public class AruodasAutomationService : IAruodasAutomationService, IDisposable
{
    public event EventHandler<AutomationCompletedEventArgs>? AutomationCompleted;
    private const string SaleListingSelector = "div.list-row-v2.object-row.selflat.advert";
    private const string RentListingSelector = "div.list-row-v2.object-row.srentflat.advert";
    private const string HouseListingSelector = "div.list-row-v2.object-row.selhouse.advert";
    private const string CommercialListingSelector = "div.list-row-v2.object-row.selcomm.advert";
    private const string RentHouseListingSelector = "div.list-row-v2.object-row.srenthouse.advert";
    private const string RentCommercialListingSelector = "div.list-row-v2.object-row.srentcomm.advert";
    private const string LandListingSelector = "div.list-row-v2.object-row.sellot.advert";
    private const string GarageListingSelector = "div.list-row-v2.object-row.selgarage.advert";
    private const string ShortTermRentListingSelector = "div.list-row-v2.object-row.srentshort.advert";
    private static readonly string ListingSelector = $"{SaleListingSelector}, {RentListingSelector}, {HouseListingSelector}, {CommercialListingSelector}, {RentHouseListingSelector}, {RentCommercialListingSelector}, {LandListingSelector}, {GarageListingSelector}, {ShortTermRentListingSelector}";
    private const string SearchCitySelector = "#display_text_FDistrict";
    private const string SearchRegionSelector = "#display_text_FRegion";
    private const string SearchObjectSelector = "#display_text_obj";
    private const string SearchBuildingTypeSelector = "#display_text_FBuildingType";
    private const string SearchRentTypeSelector = "#display_text_FRentType";

    private readonly IPlaywrightRunner _playwrightRunner;
    private readonly IDatabaseService _databaseService;
    private readonly ILogService _logService;
    private readonly IAruodasNotificationService _notificationService;
    private static readonly TimeSpan SearchCooldown = TimeSpan.FromSeconds(1);
    private readonly CancellationTokenSource _cts = new();
    private readonly object _lock = new();
    private Task? _currentTask;

    public AruodasAutomationService(IPlaywrightRunner playwrightRunner, IDatabaseService databaseService, ILogService logService, IAruodasNotificationService notificationService)
    {
        _playwrightRunner = playwrightRunner;
        _databaseService = databaseService;
        _logService = logService;
        _notificationService = notificationService;
        _playwrightRunner.SearchTriggered += OnSearchTriggered;
    }

    public Task RunSearchScrapeAsync(CancellationToken cancellationToken)
        => RunAutomationAsync(cancellationToken);

    private void OnSearchTriggered(object? sender, EventArgs e)
    {
        lock (_lock)
        {
            if (_currentTask is { IsCompleted: false })
            {
                _logService.Info("Skelbimu nuskaitymas jau vykdomas, praleidziame nauja uzklausa.");
                return;
            }

            _currentTask = Task.Run(async () =>
            {
                await Task.Delay(SearchCooldown, _cts.Token);
                await RunAutomationAsync(_cts.Token);
            }, _cts.Token);
        }
    }

    private async Task RunAutomationAsync(CancellationToken cancellationToken)
    {
        var inserted = 0;
        var skipped = 0;
        var found = 0;
        var collected = 0;
        var success = false;

        try
        {
            await _playwrightRunner.UsePageAsync(async page =>
            {
                await WaitForResultsAsync(page, cancellationToken);
                _logService.Info("Pradedamas aruodas.lt skelbimu nuskaitymas.");
                var searchContext = await ReadSearchContextAsync(page, cancellationToken);
                var allListings = new List<ListingRecord>();
                var pageNumber = 1;

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var listings = await ExtractListingsOnPageAsync(page, searchContext, cancellationToken);
                    _logService.Info($"Puslapis {pageNumber}: rasta {listings.Count} skelbimu.");
                    allListings.AddRange(listings);
                    found += listings.Count;

                    var hasNext = await TryGoToNextPageAsync(page, cancellationToken);
                    if (!hasNext)
                    {
                        break;
                    }

                    pageNumber++;
                    await WaitForResultsAsync(page, cancellationToken);
                }

                _logService.Info($"Surinkta {allListings.Count} skelbimu. Pradedamas issaugojimas.");
                var saveResult = await _databaseService.SaveListingsAsync(allListings, cancellationToken);
                collected = allListings.Count;
                inserted = saveResult.Inserted;
                skipped = saveResult.Skipped;
                success = true;
                _logService.Info($"Skelbimu issaugojimas baigtas. Nauji: {saveResult.Inserted}, praleisti: {saveResult.Skipped}.");
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logService.Info("Skelbimu nuskaitymas nutrauktas.");
            _notificationService.ShowAutomationResult(new AutomationCompletedEventArgs(false, found, collected, inserted, skipped, "Veiksmas nutrauktas"));
            AutomationCompleted?.Invoke(this, new AutomationCompletedEventArgs(false, found, collected, inserted, skipped, "Veiksmas nutrauktas"));
        }
        catch (Exception ex)
        {
            _logService.Error("Skelbimu nuskaitymas nepavyko.", ex);
            _notificationService.ShowAutomationResult(new AutomationCompletedEventArgs(false, found, collected, inserted, skipped, ex.Message));
            AutomationCompleted?.Invoke(this, new AutomationCompletedEventArgs(false, found, collected, inserted, skipped, ex.Message));
            return;
        }

        _notificationService.ShowAutomationResult(new AutomationCompletedEventArgs(success, found, collected, inserted, skipped, null));
        AutomationCompleted?.Invoke(this, new AutomationCompletedEventArgs(success, found, collected, inserted, skipped, null));
    }

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private async Task<SearchContext> ReadSearchContextAsync(IPage page, CancellationToken cancellationToken)
    {
        var city = await GetInnerTextAsync(page, SearchCitySelector, cancellationToken);
        var obj = await GetInnerTextAsync(page, SearchObjectSelector, cancellationToken);
        var buildingType = await GetInnerTextAsync(page, SearchBuildingTypeSelector, cancellationToken);
        var rentType = await GetInnerTextAsync(page, SearchRentTypeSelector, cancellationToken);
        if (string.Equals(buildingType, "Sodyba", StringComparison.OrdinalIgnoreCase))
        {
            obj = "Sodyba";
        }

        var offerType = await GetOfferTypeAsync(page, cancellationToken);
        if (string.Equals(obj, "Trumpalaikė nuoma", StringComparison.OrdinalIgnoreCase))
        {
            offerType = obj;
            if (!string.IsNullOrWhiteSpace(rentType))
            {
                obj = rentType;
            }
        }

        var isLandSearch = string.Equals(obj, "Sklypai", StringComparison.OrdinalIgnoreCase);
        var isGarageSearch = string.Equals(obj, "Garažai/vietos", StringComparison.OrdinalIgnoreCase);
        var isHomesteadSearch = string.Equals(obj, "Sodyba", StringComparison.OrdinalIgnoreCase);
        var isShortTermRentSearch = string.Equals(offerType, "Trumpalaikė nuoma", StringComparison.OrdinalIgnoreCase);
        if ((isLandSearch || isGarageSearch || isHomesteadSearch || isShortTermRentSearch) &&
            string.Equals(city, "Gyvenvietė", StringComparison.OrdinalIgnoreCase))
        {
            city = await GetInnerTextAsync(page, SearchRegionSelector, cancellationToken);
        }

        return new SearchContext(city, obj, offerType);
    }

    private static async Task<string> GetInnerTextAsync(IPage page, string selector, CancellationToken cancellationToken)
    {
        var element = await page.QuerySelectorAsync(selector);
        if (element is null)
        {
            return string.Empty;
        }

        var text = await element.InnerTextAsync();
        await element.DisposeAsync();
        cancellationToken.ThrowIfCancellationRequested();
        return Normalize(text);
    }

    private async Task<IReadOnlyList<ListingRecord>> ExtractListingsOnPageAsync(IPage page, SearchContext context, CancellationToken cancellationToken)
    {
        var listings = new List<ListingRecord>();
        var handles = await page.QuerySelectorAllAsync(ListingSelector);
        if (handles.Count == 0)
        {
            await page.WaitForTimeoutAsync(500);
        }

        foreach (var handle in handles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var listing = await ParseListingAsync(page, handle, context);
                if (listing is not null)
                {
                    listings.Add(listing);
                }
            }
            catch (Exception ex)
            {
                _logService.Error("Nepavyko perskaityti skelbimo.", ex);
            }
            finally
            {
                await handle.DisposeAsync();
            }
        }

        return listings;
    }

    private static async Task<ListingRecord?> ParseListingAsync(IPage page, IElementHandle handle, SearchContext context)
    {
        var classes = await handle.GetAttributeAsync("class") ?? string.Empty;
        var isHouse = classes.Contains("selhouse", StringComparison.OrdinalIgnoreCase);
        var isCommercial = classes.Contains("selcomm", StringComparison.OrdinalIgnoreCase);
        var isRentHouse = classes.Contains("srenthouse", StringComparison.OrdinalIgnoreCase);
        var isRentCommercial = classes.Contains("srentcomm", StringComparison.OrdinalIgnoreCase);
        var isLand = classes.Contains("sellot", StringComparison.OrdinalIgnoreCase);
        var isGarage = classes.Contains("selgarage", StringComparison.OrdinalIgnoreCase);
        var isShortTermRent = classes.Contains("srentshort", StringComparison.OrdinalIgnoreCase);
        if (isHouse)
        {
            return await ParseHouseListingAsync(page, handle, context);
        }

        if (isRentCommercial)
        {
            return await ParseCommercialListingAsync(page, handle, context);
        }

        if (isRentHouse)
        {
            return await ParseHouseListingAsync(page, handle, context);
        }

        if (isLand)
        {
            return await ParseLandListingAsync(page, handle, context);
        }

        if (isGarage)
        {
            return await ParseGarageListingAsync(page, handle, context);
        }

        if (isShortTermRent)
        {
            return await ParseFlatListingAsync(page, handle, context);
        }

        if (isCommercial)
        {
            return await ParseCommercialListingAsync(page, handle, context);
        }

        return await ParseFlatListingAsync(page, handle, context);
    }

    private static async Task<ListingRecord?> ParseFlatListingAsync(IPage page, IElementHandle handle, SearchContext context)
    {
        var linkHandle = await handle.QuerySelectorAsync("div.list-adress-v2 h3 a");
        if (linkHandle is null)
        {
            return null;
        }

        var href = await linkHandle.GetAttributeAsync("href") ?? string.Empty;
        var linkText = await linkHandle.InnerTextAsync();
        await linkHandle.DisposeAsync();

        var absoluteUrl = BuildAbsoluteUrl(href);
        var (microDistrict, address) = SplitAddress(linkText);
        var price = await GetInnerTextAsync(handle, ".list-item-price-v2");
        var pricePm = await GetInnerTextAsync(handle, ".price-pm-v2");
        var rooms = await GetInnerTextAsync(handle, ".list-RoomNum-v2.list-detail-v2");
        var area = await GetInnerTextAsync(handle, ".list-AreaOverall-v2.list-detail-v2");
        var floor = await GetInnerTextAsync(handle, ".list-Floors-v2.list-detail-v2");

        return new ListingRecord
        {
            ExternalId = ExtractListingId(absoluteUrl),
            AdvertisementUrl = absoluteUrl,
            SearchCity = context.City,
            SearchObject = context.ObjectType,
            OfferType = context.OfferType,
            MicroDistrict = microDistrict,
            Address = address,
            Price = price,
            PricePerSquare = pricePm,
            Rooms = rooms,
            AreaSquare = area,
            Floors = floor
        };
    }

    private static async Task<ListingRecord?> ParseGarageListingAsync(IPage page, IElementHandle handle, SearchContext context)
    {
        var linkHandle = await handle.QuerySelectorAsync("div.list-adress-v2 h3 a");
        if (linkHandle is null)
        {
            return null;
        }

        var href = await linkHandle.GetAttributeAsync("href") ?? string.Empty;
        var linkText = await linkHandle.InnerTextAsync();
        await linkHandle.DisposeAsync();

        var absoluteUrl = BuildAbsoluteUrl(href);
        var (microDistrict, address) = SplitAddress(linkText);
        var price = await GetInnerTextAsync(handle, ".list-item-price-v2");
        var pricePm = await GetInnerTextAsync(handle, ".price-pm-v2");
        var area = await GetInnerTextAsync(handle, ".list-AreaOverall-v2.list-detail-v2");
        var floors = await GetInnerTextAsync(handle, ".list-Floors-v2.list-detail-v2");

        return new ListingRecord
        {
            ExternalId = ExtractListingId(absoluteUrl),
            AdvertisementUrl = absoluteUrl,
            SearchCity = context.City,
            SearchObject = context.ObjectType,
            OfferType = context.OfferType,
            MicroDistrict = microDistrict,
            Address = address,
            Price = price,
            PricePerSquare = pricePm,
            Rooms = string.Empty,
            AreaSquare = area,
            AreaLot = string.Empty,
            HouseState = string.Empty,
            Intendances = string.Empty,
            Floors = floors
        };
    }

    private static async Task<ListingRecord?> ParseHouseListingAsync(IPage page, IElementHandle handle, SearchContext context)
    {
        var linkHandle = await handle.QuerySelectorAsync("div.list-adress-v2 h3 a");
        if (linkHandle is null)
        {
            return null;
        }

        var href = await linkHandle.GetAttributeAsync("href") ?? string.Empty;
        var linkText = await linkHandle.InnerTextAsync();
        await linkHandle.DisposeAsync();

        var absoluteUrl = BuildAbsoluteUrl(href);
        var (microDistrict, address) = SplitAddress(linkText);
        var price = await GetInnerTextAsync(handle, ".list-item-price-v2");
        var pricePm = await GetInnerTextAsync(handle, ".price-pm-v2");
        var area = await GetInnerTextAsync(handle, ".list-AreaOverall-v2.list-detail-v2");
        var areaLot = await GetInnerTextAsync(handle, ".list-AreaLot-v2.list-detail-v2");
        var houseState = await GetInnerTextAsync(handle, ".list-HouseStates-v2.list-detail-v2");
        var rooms = await GetInnerTextAsync(handle, ".list-RoomNum-v2.list-detail-v2");
        var floors = await GetInnerTextAsync(handle, ".list-Floors-v2.list-detail-v2");

        return new ListingRecord
        {
            ExternalId = ExtractListingId(absoluteUrl),
            AdvertisementUrl = absoluteUrl,
            SearchCity = context.City,
            SearchObject = context.ObjectType,
            OfferType = context.OfferType,
            MicroDistrict = microDistrict,
            Address = address,
            Price = price,
            PricePerSquare = pricePm,
            Rooms = rooms,
            AreaSquare = area,
            AreaLot = areaLot,
            HouseState = houseState,
            Floors = floors
        };
    }

    private static async Task<ListingRecord?> ParseCommercialListingAsync(IPage page, IElementHandle handle, SearchContext context)
    {
        var linkHandle = await handle.QuerySelectorAsync("div.list-adress-v2 h3 a");
        if (linkHandle is null)
        {
            return null;
        }

        var href = await linkHandle.GetAttributeAsync("href") ?? string.Empty;
        var linkText = await linkHandle.InnerTextAsync();
        await linkHandle.DisposeAsync();

        var absoluteUrl = BuildAbsoluteUrl(href);
        var (microDistrict, address) = SplitAddress(linkText);
        var price = await GetInnerTextAsync(handle, ".list-item-price-v2");
        var pricePm = await GetInnerTextAsync(handle, ".price-pm-v2");
        var area = await GetInnerTextAsync(handle, ".list-AreaOverall-v2.list-detail-v2");
        var floors = await GetInnerTextAsync(handle, ".list-Floors-v2.list-detail-v2");
        var intendances = await GetInnerTextAsync(handle, ".list-Intendances-v2.list-detail-v2");

        return new ListingRecord
        {
            ExternalId = ExtractListingId(absoluteUrl),
            AdvertisementUrl = absoluteUrl,
            SearchCity = context.City,
            SearchObject = context.ObjectType,
            OfferType = context.OfferType,
            MicroDistrict = microDistrict,
            Address = address,
            Price = price,
            PricePerSquare = pricePm,
            Rooms = string.Empty,
            AreaSquare = area,
            AreaLot = string.Empty,
            HouseState = string.Empty,
            Intendances = intendances,
            Floors = floors
        };
    }

    private static async Task<ListingRecord?> ParseLandListingAsync(IPage page, IElementHandle handle, SearchContext context)
    {
        var linkHandle = await handle.QuerySelectorAsync("div.list-adress-v2 h3 a");
        if (linkHandle is null)
        {
            return null;
        }

        var href = await linkHandle.GetAttributeAsync("href") ?? string.Empty;
        var linkText = await linkHandle.InnerTextAsync();
        await linkHandle.DisposeAsync();

        var absoluteUrl = BuildAbsoluteUrl(href);
        var (microDistrict, address) = SplitAddress(linkText);
        var price = await GetInnerTextAsync(handle, ".list-item-price-v2");
        var pricePm = await GetInnerTextAsync(handle, ".price-pm-v2");
        var areaLot = await GetInnerTextAsync(handle, ".list-AreaOverall-v2.list-detail-v2");
        var floors = await GetInnerTextAsync(handle, ".list-Floors-v2.list-detail-v2");
        var intendances = await GetInnerTextAsync(handle, ".list-Intendances-v2.list-detail-v2");

        return new ListingRecord
        {
            ExternalId = ExtractListingId(absoluteUrl),
            AdvertisementUrl = absoluteUrl,
            SearchCity = context.City,
            SearchObject = context.ObjectType,
            OfferType = context.OfferType,
            MicroDistrict = microDistrict,
            Address = address,
            Price = price,
            PricePerSquare = pricePm,
            Rooms = string.Empty,
            AreaSquare = string.Empty,
            AreaLot = areaLot,
            HouseState = string.Empty,
            Intendances = intendances,
            Floors = floors
        };
    }

    private static async Task<string> GetInnerTextAsync(IElementHandle parent, string selector)
    {
        var element = await parent.QuerySelectorAsync(selector);
        if (element is null)
        {
            return string.Empty;
        }

        var text = await element.InnerTextAsync();
        await element.DisposeAsync();
        return Normalize(text);
    }

    private static string BuildAbsoluteUrl(string href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return string.Empty;
        }

        if (!Uri.TryCreate(href, UriKind.Absolute, out var uri))
        {
            uri = new Uri(new Uri("https://www.aruodas.lt"), href);
        }

        return uri.ToString();
    }

    private static (string MicroDistrict, string Address) SplitAddress(string text)
    {
        var parts = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var micro = parts.Length > 0 ? parts[0] : string.Empty;
        var address = parts.Length > 1 ? parts[1] : string.Empty;
        return (Normalize(micro), Normalize(address));
    }

    private static string ExtractListingId(string url)
    {
        var match = Regex.Match(url ?? string.Empty, @"-(\d+)(?:[/?]|$)");
        return match.Success ? match.Groups[1].Value : url ?? string.Empty;
    }

    private static async Task<bool> TryGoToNextPageAsync(IPage page, CancellationToken cancellationToken)
    {
        var pagination = await page.QuerySelectorAsync("div.pagination");
        if (pagination is null)
        {
            return false;
        }

        var links = await pagination.QuerySelectorAllAsync("a");
        var moveNext = false;
        foreach (var link in links)
        {
            var classes = (await link.GetAttributeAsync("class")) ?? string.Empty;
            if (classes.Contains("active-page", StringComparison.OrdinalIgnoreCase))
            {
                moveNext = true;
                continue;
            }

            if (!moveNext)
            {
                continue;
            }

            if (classes.Contains("disabled", StringComparison.OrdinalIgnoreCase))
            {
                await link.DisposeAsync();
                await pagination.DisposeAsync();
                return false;
            }

            await link.ClickAsync(new ElementHandleClickOptions { Delay = 100 });
            try
            {
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 20000 });
            }
            catch (TimeoutException)
            {
                // ignore and continue with best effort
            }
            await link.DisposeAsync();
            await pagination.DisposeAsync();
            await page.WaitForTimeoutAsync(500);
            return true;
        }

        await pagination.DisposeAsync();
        return false;
    }

    private static async Task WaitForResultsAsync(IPage page, CancellationToken cancellationToken)
    {
        try
        {
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 20000 });
        }
        catch (TimeoutException)
        {
            // ignore, continue with what we have
        }

        try
        {
            await page.WaitForSelectorAsync(ListingSelector, new PageWaitForSelectorOptions
            {
                Timeout = 20000
            });
        }
        catch (TimeoutException)
        {
            // listings might still be missing, continue gracefully
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    public void Dispose()
    {
        _playwrightRunner.SearchTriggered -= OnSearchTriggered;
        _cts.Cancel();
        _cts.Dispose();
    }

    private static async Task<string> GetOfferTypeAsync(IPage page, CancellationToken cancellationToken)
    {
        const string containerSelector = "#searchFormToggleFieldContainer_FOfferType .search-form__field-display-type-toggle.search-form__field-display__has-value .search-form__field-display-input-min";
        var text = await GetInnerTextAsync(page, containerSelector, cancellationToken);
        return text;
    }
    private record SearchContext(string City, string ObjectType, string OfferType);
}
