using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace RpaAruodas.Services;

public interface IDatabaseService
{
    Task InitializeAsync();
    Task<ListingSaveResult> SaveListingsAsync(IReadOnlyCollection<ListingRecord> listings, CancellationToken cancellationToken);
    Task<StatsQueryResult> QueryListingsAsync(
        string? searchObject,
        string? searchCity,
        string? microDistrict,
        string? address,
        DateTime? fromDate,
        DateTime? toDate,
        double? priceFrom,
        double? priceTo,
        double? pricePerSquareFrom,
        double? pricePerSquareTo,
        double? areaFrom,
        double? areaTo,
        double? areaLotFrom,
        double? areaLotTo,
        string? rooms,
        string? houseState,
        int limit,
        int offset,
        bool onlyWithoutHistory,
        bool onlyFavorites,
        bool onlyPriceDrop,
        bool onlyPriceIncrease,
        bool orderByPriceDescending,
        bool orderByPriceAscending,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetDistinctSearchObjectsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetDistinctSearchObjectsAsync(string? searchCity, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetDistinctSearchCitiesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetDistinctSearchCitiesAsync(string? searchObject, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetDistinctAddressesAsync(string? searchObject, string? searchCity, string? rooms, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetDistinctRoomsAsync(string? searchObject, string? searchCity, string? address, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetDistinctMicroDistrictsAsync(string? searchObject, string? searchCity, string? rooms, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetDistinctHouseStatesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetDistinctHouseStatesAsync(string? searchObject, string? searchCity, string? address, string? rooms, double? priceFrom, double? priceTo, double? pricePerSquareFrom, double? pricePerSquareTo, double? areaFrom, double? areaTo, double? areaLotFrom, double? areaLotTo, CancellationToken cancellationToken);
    Task<(double? Min, double? Max)> GetPriceBoundsAsync(string? searchObject, string? searchCity, string? microDistrict, string? address, string? rooms, string? houseState, CancellationToken cancellationToken);
    Task<(double? Min, double? Max)> GetPricePerSquareBoundsAsync(string? searchObject, string? searchCity, string? microDistrict, string? address, string? rooms, string? houseState, CancellationToken cancellationToken);
    Task<(double? Min, double? Max)> GetAreaBoundsAsync(string? searchObject, string? searchCity, string? microDistrict, string? address, string? rooms, string? houseState, CancellationToken cancellationToken);
    Task<(double? Min, double? Max)> GetAreaLotBoundsAsync(string? searchObject, string? searchCity, string? microDistrict, string? address, string? rooms, string? houseState, CancellationToken cancellationToken);
    Task<IReadOnlyList<StatsListing>> GetListingHistoryAsync(string externalId, string? searchObject, CancellationToken cancellationToken);
    Task<IReadOnlyList<CityHistoryEntry>> GetCityHistoryAsync(IReadOnlyCollection<string> cities, string? searchObject, string? microDistrict, string? rooms, DateTime? fromDate, DateTime? toDate, CancellationToken cancellationToken);
    Task SetSelectedAsync(string externalId, string? searchObject, bool selected, CancellationToken cancellationToken);
}

public sealed record CityHistoryEntry(string City, DateTime Date, double? AveragePrice, double? AveragePricePerSquare);

public class DatabaseService : IDatabaseService
{
    private static readonly (string Name, string Definition)[] RequiredColumns =
    {
        ("ExternalId", "TEXT"),
        ("CollectedOn", "TEXT NOT NULL DEFAULT ''"),
        ("SearchCity", "TEXT"),
        ("SearchObject", "TEXT"),
        ("MicroDistrict", "TEXT"),
        ("Address", "TEXT"),
        ("Price", "TEXT"),
        ("PricePerSquare", "TEXT"),
        ("Rooms", "TEXT"),
        ("AreaSquare", "TEXT"),
        ("AreaLot", "TEXT"),
        ("HouseState", "TEXT"),
        ("OfferType", "TEXT"),
        ("Intendances", "TEXT"),
        ("Floors", "TEXT"),
        ("AdvertisementUrl", "TEXT"),
        ("Selected", "INTEGER NOT NULL DEFAULT 0"),
        ("SearchCity_lc", "TEXT"),
        ("SearchObject_lc", "TEXT"),
        ("MicroDistrict_lc", "TEXT"),
        ("Address_lc", "TEXT"),
        ("Rooms_lc", "TEXT"),
        ("HouseState_lc", "TEXT"),
        ("PriceValue", "REAL"),
        ("PricePerSquareValue", "REAL"),
        ("AreaSquareValue", "REAL"),
        ("AreaLotValue", "REAL")
    };

    private static readonly string[] DeprecatedColumns = { "Title", "RawContent", "CollectedAt" };

    private static readonly string[] ColumnsToPreserve =
    {
        "Id",
        "ExternalId",
        "CollectedOn",
        "SearchCity",
        "SearchObject",
        "MicroDistrict",
        "Address",
        "Price",
        "PricePerSquare",
        "Rooms",
        "AreaSquare",
        "AreaLot",
        "HouseState",
        "OfferType",
        "Intendances",
        "Floors",
        "AdvertisementUrl",
        "Selected",
        "SearchCity_lc",
        "SearchObject_lc",
        "MicroDistrict_lc",
        "Address_lc",
        "Rooms_lc",
        "HouseState_lc",
        "PriceValue",
        "PricePerSquareValue",
        "AreaSquareValue",
        "AreaLotValue"
    };

    private static readonly (string Name, string Value)[] ConnectionPragmas =
    {
        ("journal_mode", "WAL"),
        ("synchronous", "NORMAL"),
        ("temp_store", "MEMORY"),
        ("cache_size", "-8000"),
        ("foreign_keys", "ON")
    };

    private static readonly string[] NumericExpressionTokens =
    {
        " ",
        "\u00A0",
        "€",
        "/m2",
        "/m\u00B2",
        "/m\u0131",
        "m2",
        "m\u00B2",
        "m\u0131",
        "kv.m",
        "kv. m",
        "kvm",
        "/kvm"
    };

    private enum KeysetSortMode
    {
        DateDesc,
        PriceDesc,
        PriceAsc
    }

    private sealed record KeysetSeekTuple(double? PriceValue, string CollectedOnLatest, string ExternalId, int PriceNullFlag);

    private readonly IConfigurationService _configurationService;
    private readonly ILogService _logService;
    private readonly string _databasePath;

    public DatabaseService(IConfigurationService configurationService, ILogService logService)
    {
        _configurationService = configurationService;
        _logService = logService;
        _databasePath = Path.Combine(AppContext.BaseDirectory, _configurationService.Current.Database.FilePath);
    }

    public async Task InitializeAsync()
    {
        try
        {
            _logService.Info("Pradedamas SQLite inicializavimas.");
            var directory = Path.GetDirectoryName(_databasePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync();
            await ApplyConnectionPragmasAsync(connection);

            await CreateListingsTableAsync(connection, "Listings");
            await RemoveDeprecatedColumnsAsync(connection);
            var ensuredColumns = await EnsureColumnsAsync(connection);
            await BackfillNumericColumnsAsync(connection);
            await BackfillMicroDistrictNormalizedAsync(connection);
            await LogTableInfoAsync(connection);
            await EnsureLatestListingsSchemaAsync(connection);
            await BackfillLatestListingsAsync(connection);

            var indexCommand = connection.CreateCommand();
            indexCommand.CommandText =
                """
                CREATE UNIQUE INDEX IF NOT EXISTS IX_Listings_ExternalIdDate
                    ON Listings(ExternalId, CollectedOn);
                """;
            await indexCommand.ExecuteNonQueryAsync();

            var microDistrictIndexCommand = connection.CreateCommand();
            microDistrictIndexCommand.CommandText =
                """
                CREATE INDEX IF NOT EXISTS IX_Listings_MicroDistrict
                    ON Listings(MicroDistrict_lc, SearchCity_lc, SearchObject_lc, CollectedOn);
                """;
            await microDistrictIndexCommand.ExecuteNonQueryAsync();

            if (ensuredColumns.Contains("PriceValue"))
            {
                var priceIndexCommand = connection.CreateCommand();
                priceIndexCommand.CommandText =
                    """
                    CREATE INDEX IF NOT EXISTS IX_Listings_PriceValue
                        ON Listings(PriceValue DESC);
                    """;
                await priceIndexCommand.ExecuteNonQueryAsync();
            }

            await OptimizeDatabaseAsync(connection);

            _logService.Info($"SQLite DB paruosta ({_databasePath}).");
        }
        catch (Exception ex)
        {
            _logService.Error("Nepavyko inicializuoti SQLite DB.", ex);
            throw;
        }
    }

    public async Task<ListingSaveResult> SaveListingsAsync(IReadOnlyCollection<ListingRecord> listings, CancellationToken cancellationToken)
    {
        if (listings.Count == 0)
        {
            return new ListingSaveResult(0, 0);
        }

        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync(cancellationToken);
        await ApplyConnectionPragmasAsync(connection);
        await EnsureColumnsAsync(connection);
        await EnsureLatestListingsSchemaAsync(connection);
        await using var transaction = await connection.BeginTransactionAsync() as SqliteTransaction
            ?? throw new InvalidOperationException("Nepavyko pradeti SQLite transakcijos.");

        var inserted = 0;
        var skipped = 0;
        var collectedOn = DateTime.UtcNow.ToString("yyyy-MM-dd");

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT OR IGNORE INTO Listings
            (
                ExternalId,
                CollectedOn,
                SearchCity,
                SearchObject,
                MicroDistrict,
                Address,
                Price,
                PricePerSquare,
                Rooms,
                AreaSquare,
                AreaLot,
                HouseState,
                OfferType,
                Intendances,
                Floors,
                AdvertisementUrl,
            Selected,
            SearchCity_lc,
            SearchObject_lc,
            MicroDistrict_lc,
            Address_lc,
                Rooms_lc,
                HouseState_lc,
                PriceValue,
                PricePerSquareValue,
                AreaSquareValue,
                AreaLotValue
            )
            VALUES
            (
                @externalId,
                @collectedOn,
                @searchCity,
                @searchObject,
                @microDistrict,
                @address,
                @price,
                @pricePerSquare,
                @rooms,
                @areaSquare,
                @areaLot,
                @houseState,
                @offerType,
                @intendances,
                @floors,
                @url,
            @selected,
            @searchCityLc,
            @searchObjectLc,
            @microDistrictLc,
            @addressLc,
                @roomsLc,
                @houseStateLc,
                @priceValue,
                @pricePerSquareValue,
                @areaSquareValue,
                @areaLotValue
            );
            """;

        var paramExternalId = command.Parameters.Add("@externalId", SqliteType.Text);
        var paramCollectedOn = command.Parameters.Add("@collectedOn", SqliteType.Text);
        var paramSearchCity = command.Parameters.Add("@searchCity", SqliteType.Text);
        var paramSearchObject = command.Parameters.Add("@searchObject", SqliteType.Text);
        var paramMicroDistrict = command.Parameters.Add("@microDistrict", SqliteType.Text);
        var paramAddress = command.Parameters.Add("@address", SqliteType.Text);
        var paramPrice = command.Parameters.Add("@price", SqliteType.Text);
        var paramPricePerSquare = command.Parameters.Add("@pricePerSquare", SqliteType.Text);
        var paramRooms = command.Parameters.Add("@rooms", SqliteType.Text);
        var paramAreaSquare = command.Parameters.Add("@areaSquare", SqliteType.Text);
        var paramAreaLot = command.Parameters.Add("@areaLot", SqliteType.Text);
        var paramHouseState = command.Parameters.Add("@houseState", SqliteType.Text);
        var paramOfferType = command.Parameters.Add("@offerType", SqliteType.Text);
        var paramIntendances = command.Parameters.Add("@intendances", SqliteType.Text);
        var paramFloors = command.Parameters.Add("@floors", SqliteType.Text);
        var paramUrl = command.Parameters.Add("@url", SqliteType.Text);
        var paramSelected = command.Parameters.Add("@selected", SqliteType.Integer);
        var paramSearchCityLc = command.Parameters.Add("@searchCityLc", SqliteType.Text);
        var paramSearchObjectLc = command.Parameters.Add("@searchObjectLc", SqliteType.Text);
        var paramMicroDistrictLc = command.Parameters.Add("@microDistrictLc", SqliteType.Text);
        var paramAddressLc = command.Parameters.Add("@addressLc", SqliteType.Text);
        var paramRoomsLc = command.Parameters.Add("@roomsLc", SqliteType.Text);
        var paramHouseStateLc = command.Parameters.Add("@houseStateLc", SqliteType.Text);
        var paramPriceValue = command.Parameters.Add("@priceValue", SqliteType.Real);
        var paramPricePerSquareValue = command.Parameters.Add("@pricePerSquareValue", SqliteType.Real);
        var paramAreaSquareValue = command.Parameters.Add("@areaSquareValue", SqliteType.Real);
        var paramAreaLotValue = command.Parameters.Add("@areaLotValue", SqliteType.Real);

        await using var latestCommand = connection.CreateCommand();
        latestCommand.Transaction = transaction;
        latestCommand.CommandText =
            """
            INSERT INTO LatestListings
            (
                ExternalId,
                SearchObject,
                CollectedOnLatest,
                SearchCity,
                MicroDistrict,
                Address,
                Price,
                PricePerSquare,
                Rooms,
                AreaSquare,
                AreaLot,
                HouseState,
                OfferType,
                Intendances,
                Floors,
                AdvertisementUrl,
                Selected,
                SearchCity_lc,
                SearchObject_lc,
                MicroDistrict_lc,
                Address_lc,
                Rooms_lc,
                HouseState_lc,
                PriceValue,
                PricePerSquareValue,
                AreaSquareValue,
                AreaLotValue
            )
            VALUES
            (
                @latestExternalId,
                @latestSearchObject,
                @collectedOnLatest,
                @latestSearchCity,
                @latestMicroDistrict,
                @latestAddress,
                @latestPrice,
                @latestPricePerSquare,
                @latestRooms,
                @latestAreaSquare,
                @latestAreaLot,
                @latestHouseState,
                @latestOfferType,
                @latestIntendances,
                @latestFloors,
                @latestAdvertisementUrl,
                @latestSelected,
                @latestSearchCityLc,
                @latestSearchObjectLc,
                @latestMicroDistrictLc,
                @latestAddressLc,
                @latestRoomsLc,
                @latestHouseStateLc,
                @latestPriceValue,
                @latestPricePerSquareValue,
                @latestAreaSquareValue,
                @latestAreaLotValue
            )
            ON CONFLICT(ExternalId, SearchObject) DO UPDATE SET
                CollectedOnLatest = excluded.CollectedOnLatest,
                SearchCity = excluded.SearchCity,
                MicroDistrict = excluded.MicroDistrict,
                Address = excluded.Address,
                Price = excluded.Price,
                PricePerSquare = excluded.PricePerSquare,
                Rooms = excluded.Rooms,
                AreaSquare = excluded.AreaSquare,
                AreaLot = excluded.AreaLot,
                HouseState = excluded.HouseState,
                OfferType = excluded.OfferType,
                Intendances = excluded.Intendances,
                Floors = excluded.Floors,
                AdvertisementUrl = excluded.AdvertisementUrl,
                Selected = excluded.Selected,
                SearchCity_lc = excluded.SearchCity_lc,
                SearchObject_lc = excluded.SearchObject_lc,
                MicroDistrict_lc = excluded.MicroDistrict_lc,
                Address_lc = excluded.Address_lc,
                Rooms_lc = excluded.Rooms_lc,
                HouseState_lc = excluded.HouseState_lc,
                PriceValue = excluded.PriceValue,
                PricePerSquareValue = excluded.PricePerSquareValue,
                AreaSquareValue = excluded.AreaSquareValue,
                AreaLotValue = excluded.AreaLotValue
            WHERE excluded.CollectedOnLatest >= LatestListings.CollectedOnLatest;
            """;

        var latestParamExternalId = latestCommand.Parameters.Add("@latestExternalId", SqliteType.Text);
        var latestParamSearchObject = latestCommand.Parameters.Add("@latestSearchObject", SqliteType.Text);
        var latestParamCollectedOn = latestCommand.Parameters.Add("@collectedOnLatest", SqliteType.Text);
        var latestParamSearchCity = latestCommand.Parameters.Add("@latestSearchCity", SqliteType.Text);
        var latestParamMicroDistrict = latestCommand.Parameters.Add("@latestMicroDistrict", SqliteType.Text);
        var latestParamAddress = latestCommand.Parameters.Add("@latestAddress", SqliteType.Text);
        var latestParamPrice = latestCommand.Parameters.Add("@latestPrice", SqliteType.Text);
        var latestParamPricePerSquare = latestCommand.Parameters.Add("@latestPricePerSquare", SqliteType.Text);
        var latestParamRooms = latestCommand.Parameters.Add("@latestRooms", SqliteType.Text);
        var latestParamAreaSquare = latestCommand.Parameters.Add("@latestAreaSquare", SqliteType.Text);
        var latestParamAreaLot = latestCommand.Parameters.Add("@latestAreaLot", SqliteType.Text);
        var latestParamHouseState = latestCommand.Parameters.Add("@latestHouseState", SqliteType.Text);
        var latestParamOfferType = latestCommand.Parameters.Add("@latestOfferType", SqliteType.Text);
        var latestParamIntendances = latestCommand.Parameters.Add("@latestIntendances", SqliteType.Text);
        var latestParamFloors = latestCommand.Parameters.Add("@latestFloors", SqliteType.Text);
        var latestParamAdvertisementUrl = latestCommand.Parameters.Add("@latestAdvertisementUrl", SqliteType.Text);
        var latestParamSelected = latestCommand.Parameters.Add("@latestSelected", SqliteType.Integer);
        var latestParamSearchCityLc = latestCommand.Parameters.Add("@latestSearchCityLc", SqliteType.Text);
        var latestParamSearchObjectLc = latestCommand.Parameters.Add("@latestSearchObjectLc", SqliteType.Text);
        var latestParamMicroDistrictLc = latestCommand.Parameters.Add("@latestMicroDistrictLc", SqliteType.Text);
        var latestParamAddressLc = latestCommand.Parameters.Add("@latestAddressLc", SqliteType.Text);
        var latestParamRoomsLc = latestCommand.Parameters.Add("@latestRoomsLc", SqliteType.Text);
        var latestParamHouseStateLc = latestCommand.Parameters.Add("@latestHouseStateLc", SqliteType.Text);
        var latestParamPriceValue = latestCommand.Parameters.Add("@latestPriceValue", SqliteType.Real);
        var latestParamPricePerSquareValue = latestCommand.Parameters.Add("@latestPricePerSquareValue", SqliteType.Real);
        var latestParamAreaSquareValue = latestCommand.Parameters.Add("@latestAreaSquareValue", SqliteType.Real);
        var latestParamAreaLotValue = latestCommand.Parameters.Add("@latestAreaLotValue", SqliteType.Real);
        SetParameterValue(paramCollectedOn, collectedOn);
        SetParameterValue(latestParamCollectedOn, collectedOn);

        foreach (var record in listings)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalizedSearchCity = NormalizeText(record.SearchCity);
            var normalizedSearchObject = NormalizeText(record.SearchObject);
            var normalizedMicroDistrict = NormalizeText(record.MicroDistrict);
            var normalizedAddress = NormalizeText(record.Address);
            var normalizedRooms = NormalizeText(record.Rooms);
            var normalizedHouseState = NormalizeText(record.HouseState);
            var priceValue = ParseNumericValue(record.Price);
            var pricePerSquareValue = ParseNumericValue(record.PricePerSquare);
            var areaSquareValue = ParseNumericValue(record.AreaSquare);
            var areaLotValue = ParseNumericValue(record.AreaLot);
            var trimmedExternalId = string.IsNullOrWhiteSpace(record.ExternalId) ? string.Empty : record.ExternalId.Trim();
            var trimmedSearchObject = string.IsNullOrWhiteSpace(record.SearchObject) ? string.Empty : record.SearchObject.Trim();
            var trimmedSearchCity = string.IsNullOrWhiteSpace(record.SearchCity) ? null : record.SearchCity.Trim();
            var trimmedMicroDistrict = string.IsNullOrWhiteSpace(record.MicroDistrict) ? null : record.MicroDistrict.Trim();
            var trimmedAddress = string.IsNullOrWhiteSpace(record.Address) ? null : record.Address.Trim();
            var trimmedPrice = string.IsNullOrWhiteSpace(record.Price) ? null : record.Price.Trim();
            var trimmedPricePerSquare = string.IsNullOrWhiteSpace(record.PricePerSquare) ? null : record.PricePerSquare.Trim();
            var trimmedRooms = string.IsNullOrWhiteSpace(record.Rooms) ? null : record.Rooms.Trim();
            var trimmedAreaSquare = string.IsNullOrWhiteSpace(record.AreaSquare) ? null : record.AreaSquare.Trim();
            var trimmedAreaLot = string.IsNullOrWhiteSpace(record.AreaLot) ? null : record.AreaLot.Trim();
            var trimmedHouseState = string.IsNullOrWhiteSpace(record.HouseState) ? null : record.HouseState.Trim();
            var trimmedOfferType = string.IsNullOrWhiteSpace(record.OfferType) ? null : record.OfferType.Trim();
            var trimmedIntendances = string.IsNullOrWhiteSpace(record.Intendances) ? null : record.Intendances.Trim();
            var trimmedFloors = string.IsNullOrWhiteSpace(record.Floors) ? null : record.Floors.Trim();
            var trimmedAdvertisementUrl = string.IsNullOrWhiteSpace(record.AdvertisementUrl) ? null : record.AdvertisementUrl.Trim();

            SetParameterValue(paramExternalId, record.ExternalId);
            SetParameterValue(paramSearchCity, string.IsNullOrWhiteSpace(record.SearchCity) ? null : record.SearchCity);
            SetParameterValue(paramSearchObject, string.IsNullOrWhiteSpace(record.SearchObject) ? null : record.SearchObject);
            SetParameterValue(paramMicroDistrict, string.IsNullOrWhiteSpace(record.MicroDistrict) ? null : record.MicroDistrict);
            SetParameterValue(paramAddress, string.IsNullOrWhiteSpace(record.Address) ? null : record.Address);
            SetParameterValue(paramPrice, string.IsNullOrWhiteSpace(record.Price) ? null : record.Price);
            SetParameterValue(paramPricePerSquare, string.IsNullOrWhiteSpace(record.PricePerSquare) ? null : record.PricePerSquare);
            SetParameterValue(paramRooms, string.IsNullOrWhiteSpace(record.Rooms) ? null : record.Rooms);
            SetParameterValue(paramAreaSquare, string.IsNullOrWhiteSpace(record.AreaSquare) ? null : record.AreaSquare);
            SetParameterValue(paramAreaLot, string.IsNullOrWhiteSpace(record.AreaLot) ? null : record.AreaLot);
            SetParameterValue(paramHouseState, string.IsNullOrWhiteSpace(record.HouseState) ? null : record.HouseState);
            SetParameterValue(paramOfferType, string.IsNullOrWhiteSpace(record.OfferType) ? null : record.OfferType);
            SetParameterValue(paramIntendances, string.IsNullOrWhiteSpace(record.Intendances) ? null : record.Intendances);
            SetParameterValue(paramFloors, string.IsNullOrWhiteSpace(record.Floors) ? null : record.Floors);
            SetParameterValue(paramUrl, string.IsNullOrWhiteSpace(record.AdvertisementUrl) ? null : record.AdvertisementUrl);
            SetParameterValue(paramSelected, record.Selected ? 1 : 0);

            SetParameterValue(paramSearchCityLc, normalizedSearchCity);
            SetParameterValue(paramSearchObjectLc, normalizedSearchObject);
            SetParameterValue(paramMicroDistrictLc, normalizedMicroDistrict);
            SetParameterValue(paramAddressLc, normalizedAddress);
            SetParameterValue(paramRoomsLc, normalizedRooms);
            SetParameterValue(paramHouseStateLc, normalizedHouseState);

            SetParameterValue(paramPriceValue, priceValue);
            SetParameterValue(paramPricePerSquareValue, pricePerSquareValue);
            SetParameterValue(paramAreaSquareValue, areaSquareValue);
            SetParameterValue(paramAreaLotValue, areaLotValue);

            SetParameterValue(latestParamExternalId, trimmedExternalId);
            SetParameterValue(latestParamSearchObject, trimmedSearchObject);
            SetParameterValue(latestParamSearchCity, trimmedSearchCity);
            SetParameterValue(latestParamMicroDistrict, trimmedMicroDistrict);
            SetParameterValue(latestParamAddress, trimmedAddress);
            SetParameterValue(latestParamPrice, trimmedPrice);
            SetParameterValue(latestParamPricePerSquare, trimmedPricePerSquare);
            SetParameterValue(latestParamRooms, trimmedRooms);
            SetParameterValue(latestParamAreaSquare, trimmedAreaSquare);
            SetParameterValue(latestParamAreaLot, trimmedAreaLot);
            SetParameterValue(latestParamHouseState, trimmedHouseState);
            SetParameterValue(latestParamOfferType, trimmedOfferType);
            SetParameterValue(latestParamIntendances, trimmedIntendances);
            SetParameterValue(latestParamFloors, trimmedFloors);
            SetParameterValue(latestParamAdvertisementUrl, trimmedAdvertisementUrl);
            SetParameterValue(latestParamSelected, record.Selected ? 1 : 0);
            SetParameterValue(latestParamSearchCityLc, normalizedSearchCity);
            SetParameterValue(latestParamSearchObjectLc, normalizedSearchObject);
            SetParameterValue(latestParamMicroDistrictLc, normalizedMicroDistrict);
            SetParameterValue(latestParamAddressLc, normalizedAddress);
            SetParameterValue(latestParamRoomsLc, normalizedRooms);
            SetParameterValue(latestParamHouseStateLc, normalizedHouseState);
            SetParameterValue(latestParamPriceValue, priceValue);
            SetParameterValue(latestParamPricePerSquareValue, pricePerSquareValue);
            SetParameterValue(latestParamAreaSquareValue, areaSquareValue);
            SetParameterValue(latestParamAreaLotValue, areaLotValue);

            var affected = await command.ExecuteNonQueryAsync(cancellationToken);
            await latestCommand.ExecuteNonQueryAsync(cancellationToken);
            if (affected > 0)
            {
                inserted++;
            }
            else
            {
                skipped++;
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return new ListingSaveResult(inserted, skipped);
    }

    public async Task<StatsQueryResult> QueryListingsAsync(
        string? searchObject,
        string? searchCity,
        string? microDistrict,
        string? address,
        DateTime? fromDate,
        DateTime? toDate,
        double? priceFrom,
        double? priceTo,
        double? pricePerSquareFrom,
        double? pricePerSquareTo,
        double? areaFrom,
        double? areaTo,
        double? areaLotFrom,
        double? areaLotTo,
        string? rooms,
        string? houseState,
        int limit,
        int offset,
        bool onlyWithoutHistory,
        bool onlyFavorites,
        bool onlyPriceDrop,
        bool onlyPriceIncrease,
        bool orderByPriceDescending,
        bool orderByPriceAscending,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync(cancellationToken);
        await ApplyConnectionPragmasAsync(connection);
        await EnsureColumnsAsync(connection);
        await EnsureLatestListingsSchemaAsync(connection);

        var filters = new List<string>();
        var parameters = new List<(string Name, object? Value)>();
        var joinClause = string.Empty;

        var normalizedSearchObject = NormalizeText(searchObject);
        if (normalizedSearchObject != null)
        {
            filters.Add("SearchObject_lc = @searchObject");
            parameters.Add(("@searchObject", normalizedSearchObject));
        }

        var normalizedSearchCity = NormalizeText(searchCity);
        if (normalizedSearchCity != null)
        {
            filters.Add("SearchCity_lc = @searchCity");
            parameters.Add(("@searchCity", normalizedSearchCity));
        }

        var normalizedMicroDistrict = NormalizeText(microDistrict);
        if (normalizedMicroDistrict != null)
        {
            filters.Add("MicroDistrict_lc = @microDistrict");
            parameters.Add(("@microDistrict", normalizedMicroDistrict));
        }

        var normalizedAddress = NormalizeText(address);
        if (normalizedAddress != null)
        {
            joinClause = "JOIN LatestListingsAddressFts addressFts ON addressFts.rowid = l.rowid";
            filters.Add("addressFts MATCH @address");
            parameters.Add(("@address", normalizedAddress));
        }

        if (fromDate.HasValue)
        {
            filters.Add("CollectedOnLatest >= @fromDate");
            parameters.Add(("@fromDate", fromDate.Value.ToString("yyyy-MM-dd")));
        }

        if (toDate.HasValue)
        {
            filters.Add("CollectedOnLatest <= @toDate");
            parameters.Add(("@toDate", toDate.Value.ToString("yyyy-MM-dd")));
        }

        AddNumericRangeFilter(filters, parameters, "PriceValue", "@priceFrom", "@priceTo", priceFrom, priceTo);
        AddNumericRangeFilter(filters, parameters, "PricePerSquareValue", "@pricePerSquareFrom", "@pricePerSquareTo", pricePerSquareFrom, pricePerSquareTo);
        AddNumericRangeFilter(filters, parameters, "AreaSquareValue", "@areaFrom", "@areaTo", areaFrom, areaTo);
        AddNumericRangeFilter(filters, parameters, "AreaLotValue", "@areaLotFrom", "@areaLotTo", areaLotFrom, areaLotTo);

        var normalizedRooms = NormalizeText(rooms);
        if (normalizedRooms != null)
        {
            filters.Add("Rooms_lc = @rooms");
            parameters.Add(("@rooms", normalizedRooms));
        }

        var normalizedHouseState = NormalizeText(houseState);
        if (normalizedHouseState != null)
        {
            filters.Add("HouseState_lc = @houseState");
            parameters.Add(("@houseState", normalizedHouseState));
        }

        if (onlyFavorites)
        {
            filters.Add("Selected = 1");
        }

        var whereClause = filters.Count > 0 ? $"WHERE {string.Join(" AND ", filters)}" : string.Empty;
        var statsFromClause = $"LatestListings l{(string.IsNullOrEmpty(joinClause) ? string.Empty : $" {joinClause}")}";

        var priceWindowDescendingOrder = "ORDER BY (PriceValue IS NULL), PriceValue DESC, CollectedOn DESC, ExternalId DESC";
        var priceWindowAscendingOrder = "ORDER BY (PriceValue IS NULL), PriceValue ASC, CollectedOn DESC, ExternalId DESC";

        KeysetSortMode sortMode;
        string orderClause;
        string seekOrderClause;
        if (orderByPriceDescending)
        {
            sortMode = KeysetSortMode.PriceDesc;
            orderClause = "ORDER BY (f.PriceValue IS NULL), f.PriceValue DESC, f.CollectedOn DESC, f.ExternalId DESC";
            seekOrderClause = "ORDER BY (PriceValue IS NULL), PriceValue DESC, CollectedOnLatest DESC, ExternalId DESC";
            _logService.Info($"Sorting stats by price descending using '{priceWindowDescendingOrder}'.");
        }
        else if (orderByPriceAscending)
        {
            sortMode = KeysetSortMode.PriceAsc;
            orderClause = "ORDER BY (f.PriceValue IS NULL), f.PriceValue ASC, f.CollectedOn DESC, f.ExternalId DESC";
            seekOrderClause = "ORDER BY (PriceValue IS NULL), PriceValue ASC, CollectedOnLatest DESC, ExternalId DESC";
            _logService.Info($"Sorting stats by price ascending using '{priceWindowAscendingOrder}'.");
        }
        else
        {
            sortMode = KeysetSortMode.DateDesc;
            orderClause = "ORDER BY f.CollectedOn DESC, f.ExternalId DESC";
            seekOrderClause = "ORDER BY CollectedOnLatest DESC, ExternalId DESC";
        }

        var baseParameters = new List<(string Name, object? Value)>(parameters);
        KeysetSeekTuple? seekTuple = null;
        if (offset > 0)
        {
            seekTuple = await GetSeekTupleAsync(connection, statsFromClause, whereClause, seekOrderClause, baseParameters, offset, cancellationToken);
        }

        var queryParameters = new List<(string Name, object? Value)>(baseParameters);
        var keysetClause = string.Empty;
        if (seekTuple != null)
        {
            switch (sortMode)
            {
                case KeysetSortMode.PriceDesc:
                    keysetClause =
                        """
                        (
                            (f.PriceValue IS NULL) > @seekPriceNull
                            OR ((f.PriceValue IS NULL) = @seekPriceNull AND f.PriceValue < @seekPriceValue)
                            OR ((f.PriceValue IS NULL) = @seekPriceNull AND f.PriceValue = @seekPriceValue AND f.CollectedOn < @seekCollectedOn)
                            OR ((f.PriceValue IS NULL) = @seekPriceNull AND f.PriceValue = @seekPriceValue AND f.CollectedOn = @seekCollectedOn AND f.ExternalId < @seekExternalId)
                        )
                        """;
                    queryParameters.Add(("@seekPriceNull", seekTuple.PriceNullFlag));
                    queryParameters.Add(("@seekPriceValue", seekTuple.PriceValue.HasValue ? (object)seekTuple.PriceValue.Value : DBNull.Value));
                    queryParameters.Add(("@seekCollectedOn", seekTuple.CollectedOnLatest));
                    queryParameters.Add(("@seekExternalId", seekTuple.ExternalId));
                    break;
                case KeysetSortMode.PriceAsc:
                    keysetClause =
                        """
                        (
                            (f.PriceValue IS NULL) > @seekPriceNull
                            OR ((f.PriceValue IS NULL) = @seekPriceNull AND f.PriceValue > @seekPriceValue)
                            OR ((f.PriceValue IS NULL) = @seekPriceNull AND f.PriceValue = @seekPriceValue AND f.CollectedOn < @seekCollectedOn)
                            OR ((f.PriceValue IS NULL) = @seekPriceNull AND f.PriceValue = @seekPriceValue AND f.CollectedOn = @seekCollectedOn AND f.ExternalId < @seekExternalId)
                        )
                        """;
                    queryParameters.Add(("@seekPriceNull", seekTuple.PriceNullFlag));
                    queryParameters.Add(("@seekPriceValue", seekTuple.PriceValue.HasValue ? (object)seekTuple.PriceValue.Value : DBNull.Value));
                    queryParameters.Add(("@seekCollectedOn", seekTuple.CollectedOnLatest));
                    queryParameters.Add(("@seekExternalId", seekTuple.ExternalId));
                    break;
                default:
                    keysetClause =
                        """
                        (
                            f.CollectedOn < @seekCollectedOn
                            OR (f.CollectedOn = @seekCollectedOn AND f.ExternalId < @seekExternalId)
                        )
                        """;
                    queryParameters.Add(("@seekCollectedOn", seekTuple.CollectedOnLatest));
                    queryParameters.Add(("@seekExternalId", seekTuple.ExternalId));
                    break;
            }
        }

        var outerFilters = new List<string>();
        if (onlyWithoutHistory)
        {
            outerFilters.Add("versionCount <= 1");
        }

        if (onlyPriceDrop)
        {
            outerFilters.Add("PriceChangePercent < 0");
        }

        if (onlyPriceIncrease)
        {
            outerFilters.Add("PriceChangePercent > 0");
        }

        var finalWhereClauses = new List<string>(outerFilters);
        if (!string.IsNullOrWhiteSpace(keysetClause))
        {
            finalWhereClauses.Add(keysetClause);
        }

        var finalWhereClause = finalWhereClauses.Count > 0 ? $"WHERE {string.Join(" AND ", finalWhereClauses)}" : string.Empty;

        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            WITH VersionCounts AS (
                SELECT
                    TRIM(IFNULL(ExternalId, '')) AS ExternalKey,
                    COALESCE(NULLIF(TRIM(SearchObject), ''), '') AS SearchObjectKey,
                    COUNT(*) AS versionCount
                FROM Listings
                GROUP BY ExternalKey, SearchObjectKey
            ),
            BasePrices AS (
                SELECT ExternalKey, SearchObjectKey, PriceValue
                FROM (
                    SELECT
                        TRIM(IFNULL(ExternalId, '')) AS ExternalKey,
                        COALESCE(NULLIF(TRIM(SearchObject), ''), '') AS SearchObjectKey,
                        PriceValue,
                        ROW_NUMBER() OVER (
                            PARTITION BY TRIM(IFNULL(ExternalId, '')), COALESCE(NULLIF(TRIM(SearchObject), ''), '')
                            ORDER BY CollectedOn ASC, Id ASC
                        ) AS rn
                    FROM Listings
                )
                WHERE rn = 1
            ),
            Stats AS (
                SELECT
                    l.CollectedOnLatest AS CollectedOn,
                    l.SearchObject,
                    l.SearchCity,
                    l.MicroDistrict,
                    l.Address,
                    l.Price,
                    l.PricePerSquare,
                    l.Rooms,
                    l.AreaSquare,
                    l.AreaLot,
                    l.HouseState,
                    l.OfferType,
                    l.Intendances,
                    l.Floors,
                    l.AdvertisementUrl,
                    l.ExternalId,
                    l.Selected,
                    IFNULL(vc.VersionCount, 0) AS versionCount,
                    CASE
                        WHEN bp.PriceValue IS NOT NULL AND bp.PriceValue > 0 AND l.PriceValue IS NOT NULL
                            THEN ((l.PriceValue - bp.PriceValue) / bp.PriceValue) * 100.0
                        ELSE NULL
                    END AS PriceChangePercent,
                    l.PriceValue,
                    l.PricePerSquareValue
                FROM {statsFromClause}
                LEFT JOIN VersionCounts vc
                    ON vc.ExternalKey = TRIM(IFNULL(l.ExternalId, ''))
                    AND vc.SearchObjectKey = COALESCE(NULLIF(TRIM(l.SearchObject), ''), '')
                LEFT JOIN BasePrices bp
                    ON bp.ExternalKey = TRIM(IFNULL(l.ExternalId, ''))
                    AND bp.SearchObjectKey = COALESCE(NULLIF(TRIM(l.SearchObject), ''), '')
                {whereClause}
            ),
            Final AS (
                SELECT
                    CollectedOn,
                    SearchObject,
                    SearchCity,
                    MicroDistrict,
                    Address,
                    Price,
                    PricePerSquare,
                    Rooms,
                    AreaSquare,
                    AreaLot,
                    HouseState,
                    OfferType,
                    Intendances,
                    Floors,
                    AdvertisementUrl,
                    ExternalId,
                    Selected,
                    versionCount,
                    PriceChangePercent,
                    COUNT(*) OVER() AS TotalCount,
                    AVG(PricePerSquareValue) OVER() AS AvgPricePerSquare,
                    MIN(PriceValue) OVER() AS MinPriceValue,
                    MAX(PriceValue) OVER() AS MaxPriceValue,
                    AVG(PriceValue) OVER() AS AvgPriceValue,
                    FIRST_VALUE(AdvertisementUrl) OVER (
                        {priceWindowDescendingOrder}
                    ) AS MaxPriceUrl,
                    FIRST_VALUE(AdvertisementUrl) OVER (
                        {priceWindowAscendingOrder}
                    ) AS MinPriceUrl,
                    PriceValue,
                    PricePerSquareValue
                FROM Stats
            )
            SELECT *
            FROM Final f
            {finalWhereClause}
            {orderClause}
            LIMIT @limit;
            """;

        foreach (var (name, value) in queryParameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        command.Parameters.AddWithValue("@limit", Math.Max(1, limit));

        var results = new List<StatsListing>();
        var totalCount = 0;
        double? avgPricePerSquare = null;
        double? avgPrice = null;
        double? minPrice = null;
        double? maxPrice = null;
        string? maxPriceUrl = null;
        string? minPriceUrl = null;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (totalCount == 0 && !reader.IsDBNull(19))
            {
                totalCount = reader.GetInt32(19);
            }

            if (!avgPricePerSquare.HasValue && !reader.IsDBNull(20))
            {
                avgPricePerSquare = reader.GetDouble(20);
            }

            if (!minPrice.HasValue && !reader.IsDBNull(21))
            {
                minPrice = reader.GetDouble(21);
            }

            if (!maxPrice.HasValue && !reader.IsDBNull(22))
            {
                maxPrice = reader.GetDouble(22);
            }

            if (!avgPrice.HasValue && !reader.IsDBNull(23))
            {
                avgPrice = reader.GetDouble(23);
            }

            if (maxPriceUrl is null && !reader.IsDBNull(24))
            {
                var url = reader.GetString(24);
                if (!string.IsNullOrWhiteSpace(url))
                {
                    maxPriceUrl = url;
                }
            }

            if (minPriceUrl is null && !reader.IsDBNull(25))
            {
                var url = reader.GetString(25);
                if (!string.IsNullOrWhiteSpace(url))
                {
                    minPriceUrl = url;
                }
            }

            results.Add(new StatsListing
            {
                CollectedOn = reader.GetString(0),
                SearchObject = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                SearchCity = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                MicroDistrict = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                Address = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                Price = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                PricePerSquare = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                Rooms = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                AreaSquare = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                AreaLot = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                HouseState = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                OfferType = reader.IsDBNull(11) ? string.Empty : reader.GetString(11),
                Intendances = reader.IsDBNull(12) ? string.Empty : reader.GetString(12),
                Floors = reader.IsDBNull(13) ? string.Empty : reader.GetString(13),
                AdvertisementUrl = reader.IsDBNull(14) ? string.Empty : reader.GetString(14),
                ExternalId = reader.IsDBNull(15) ? string.Empty : reader.GetString(15),
                Selected = !reader.IsDBNull(16) && reader.GetInt32(16) == 1,
                VersionCount = reader.IsDBNull(17) ? 0 : reader.GetInt32(17),
                PriceChangePercent = reader.IsDBNull(18) ? null : reader.GetDouble(18)
            });
        }

        return new StatsQueryResult(results, totalCount, avgPricePerSquare, avgPrice, minPrice, maxPrice, maxPriceUrl, minPriceUrl);
    }
    public async Task<IReadOnlyList<string>> GetDistinctSearchObjectsAsync(CancellationToken cancellationToken)
    {
        return await GetDistinctSearchObjectsAsync(null, cancellationToken);
    }

    public async Task<IReadOnlyList<StatsListing>> GetListingHistoryAsync(string externalId, string? searchObject, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(externalId))
        {
            return Array.Empty<StatsListing>();
        }

        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync(cancellationToken);
        await EnsureColumnsAsync(connection);
        await EnsureLatestListingsSchemaAsync(connection);
        await EnsureLatestListingsSchemaAsync(connection);
        await EnsureLatestListingsSchemaAsync(connection);
        await EnsureLatestListingsSchemaAsync(connection);
        await EnsureLatestListingsSchemaAsync(connection);
        await EnsureLatestListingsSchemaAsync(connection);
        await EnsureLatestListingsSchemaAsync(connection);
        await EnsureLatestListingsSchemaAsync(connection);
        await EnsureLatestListingsSchemaAsync(connection);
        await EnsureLatestListingsSchemaAsync(connection);
        await EnsureLatestListingsSchemaAsync(connection);

        await using var command = connection.CreateCommand();
        var filters = new List<string> { "TRIM(IFNULL(ExternalId, '')) = TRIM(@externalId)" };
        var normalizedSearchObject = NormalizeText(searchObject);
        if (normalizedSearchObject != null)
        {
            filters.Add("SearchObject_lc = @searchObject");
            command.Parameters.AddWithValue("@searchObject", normalizedSearchObject);
        }

        command.CommandText =
            $"""
            SELECT
                CollectedOn,
                SearchObject,
                SearchCity,
                MicroDistrict,
                Address,
                Price,
                PricePerSquare,
                Rooms,
                AreaSquare,
                AreaLot,
                HouseState,
                OfferType,
                Intendances,
                Floors,
                AdvertisementUrl,
                ExternalId,
                Selected
            FROM Listings
            WHERE {string.Join(" AND ", filters)}
            ORDER BY CollectedOn DESC, Id DESC;
            """;

        command.Parameters.AddWithValue("@externalId", externalId.Trim());

        var results = new List<StatsListing>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new StatsListing
            {
                CollectedOn = reader.GetString(0),
                SearchObject = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                SearchCity = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                MicroDistrict = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                Address = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                Price = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                PricePerSquare = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                Rooms = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                AreaSquare = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                AreaLot = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                HouseState = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                OfferType = reader.IsDBNull(11) ? string.Empty : reader.GetString(11),
                Intendances = reader.IsDBNull(12) ? string.Empty : reader.GetString(12),
                Floors = reader.IsDBNull(13) ? string.Empty : reader.GetString(13),
                AdvertisementUrl = reader.IsDBNull(14) ? string.Empty : reader.GetString(14),
                ExternalId = reader.IsDBNull(15) ? string.Empty : reader.GetString(15),
                Selected = !reader.IsDBNull(16) && reader.GetInt32(16) == 1,
                VersionCount = results.Count + 1
            });
        }

        return results;
    }

    public async Task SetSelectedAsync(string externalId, string? searchObject, bool selected, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(externalId))
        {
            return;
        }

        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync(cancellationToken);
        await EnsureColumnsAsync(connection);

        await using var command = connection.CreateCommand();
        var filters = new List<string> { "TRIM(IFNULL(ExternalId, '')) = TRIM(@externalId)" };
        command.Parameters.AddWithValue("@externalId", externalId.Trim());

        var normalizedSearchObject = NormalizeText(searchObject);
        if (normalizedSearchObject != null)
        {
            filters.Add("SearchObject_lc = @searchObject");
            command.Parameters.AddWithValue("@searchObject", normalizedSearchObject);
        }

        command.CommandText =
            $"""
            UPDATE Listings
            SET Selected = @selected
            WHERE {string.Join(" AND ", filters)};
            """;

        command.Parameters.AddWithValue("@selected", selected ? 1 : 0);

        await command.ExecuteNonQueryAsync(cancellationToken);

        var searchObjectForLatest = string.IsNullOrWhiteSpace(searchObject) ? string.Empty : searchObject.Trim();
        await using var latestCommand = connection.CreateCommand();
        latestCommand.CommandText =
            """
            UPDATE LatestListings
            SET Selected = @selected
            WHERE TRIM(IFNULL(ExternalId, '')) = TRIM(@externalId)
              AND SearchObject = @searchObject;
            """;
        latestCommand.Parameters.AddWithValue("@externalId", externalId.Trim());
        latestCommand.Parameters.AddWithValue("@searchObject", searchObjectForLatest);
        latestCommand.Parameters.AddWithValue("@selected", selected ? 1 : 0);
        await latestCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetDistinctSearchObjectsAsync(string? searchCity, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync(cancellationToken);
        await EnsureColumnsAsync(connection);

        var filters = new List<string> { "SearchObject IS NOT NULL", "SearchObject <> ''" };
        await using var command = connection.CreateCommand();
        var normalizedSearchCity = NormalizeText(searchCity);
        if (normalizedSearchCity != null)
        {
            filters.Add("SearchCity_lc = @city");
            command.Parameters.AddWithValue("@city", normalizedSearchCity);
        }

        command.CommandText =
            $"""
            SELECT DISTINCT SearchObject
            FROM LatestListings
            WHERE {string.Join(" AND ", filters)}
            ORDER BY SearchObject COLLATE NOCASE;
            """;

        var values = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (!reader.IsDBNull(0))
            {
                values.Add(reader.GetString(0));
            }
        }

        return values;
    }

    public async Task<IReadOnlyList<string>> GetDistinctSearchCitiesAsync(CancellationToken cancellationToken)
    {
        return await GetDistinctSearchCitiesAsync(null, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetDistinctSearchCitiesAsync(string? searchObject, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync(cancellationToken);
        await EnsureColumnsAsync(connection);

        var filters = new List<string>
        {
            "SearchCity IS NOT NULL",
            "SearchCity <> ''"
        };

        await using var command = connection.CreateCommand();
        var normalizedSearchObject = NormalizeText(searchObject);
        if (normalizedSearchObject != null)
        {
            filters.Add("SearchObject_lc = @obj");
            command.Parameters.AddWithValue("@obj", normalizedSearchObject);
        }

        command.CommandText =
            $"""
            SELECT DISTINCT SearchCity
            FROM LatestListings
            WHERE {string.Join(" AND ", filters)}
            ORDER BY SearchCity COLLATE NOCASE;
            """;

        var values = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (!reader.IsDBNull(0))
            {
                values.Add(reader.GetString(0));
            }
        }

        return values;
    }

    public async Task<IReadOnlyList<string>> GetDistinctAddressesAsync(string? searchObject, string? searchCity, string? rooms, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(searchCity))
        {
            return Array.Empty<string>();
        }

        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync(cancellationToken);
        await EnsureColumnsAsync(connection);

        await using var command = connection.CreateCommand();
        var filters = new List<string>
        {
            "Address IS NOT NULL",
            "Address <> ''",
            "SearchCity_lc = @city"
        };
        command.Parameters.AddWithValue("@city", NormalizeText(searchCity));

        var normalizedSearchObject = NormalizeText(searchObject);
        if (normalizedSearchObject != null)
        {
            filters.Add("SearchObject_lc = @obj");
            command.Parameters.AddWithValue("@obj", normalizedSearchObject);
        }

        var normalizedRooms = NormalizeText(rooms);
        if (normalizedRooms != null)
        {
            filters.Add("Rooms_lc = @rooms");
            command.Parameters.AddWithValue("@rooms", normalizedRooms);
        }

        command.CommandText =
            $"""
            SELECT DISTINCT Address
            FROM LatestListings
            WHERE {string.Join(" AND ", filters)}
            ORDER BY Address COLLATE NOCASE;
            """;

        var values = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (!reader.IsDBNull(0))
            {
                values.Add(reader.GetString(0));
            }
        }

        return values;
    }

    public async Task<IReadOnlyList<string>> GetDistinctRoomsAsync(string? searchObject, string? searchCity, string? address, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync(cancellationToken);
        await EnsureColumnsAsync(connection);

        var filters = new List<string> { "Rooms IS NOT NULL", "Rooms <> ''" };
        var parameters = new List<(string Name, object? Value)>();

        await using var command = connection.CreateCommand();
        var normalizedObject = NormalizeText(searchObject);
        if (normalizedObject != null)
        {
            filters.Add("SearchObject_lc = @obj");
            parameters.Add(("@obj", normalizedObject));
        }

        var normalizedCity = NormalizeText(searchCity);
        if (normalizedCity != null)
        {
            filters.Add("SearchCity_lc = @city");
            parameters.Add(("@city", normalizedCity));
        }

        var normalizedAddress = NormalizeText(address);
        if (normalizedAddress != null)
        {
            filters.Add("Address_lc = @address");
            parameters.Add(("@address", normalizedAddress));
        }

        command.CommandText =
            $"""
            SELECT DISTINCT Rooms
            FROM LatestListings
            WHERE {string.Join(" AND ", filters)}
            ORDER BY Rooms COLLATE NOCASE;
            """;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        var values = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (!reader.IsDBNull(0))
            {
                values.Add(reader.GetString(0));
            }
        }

        return values;
    }

    public async Task<IReadOnlyList<string>> GetDistinctMicroDistrictsAsync(string? searchObject, string? searchCity, string? rooms, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync(cancellationToken);
        await EnsureColumnsAsync(connection);

        var filters = new List<string>
        {
            "MicroDistrict IS NOT NULL",
            "MicroDistrict <> ''"
        };
        var parameters = new List<(string Name, object? Value)>();

        var normalizedObject = NormalizeText(searchObject);
        if (normalizedObject != null)
        {
            filters.Add("SearchObject_lc = @obj");
            parameters.Add(("@obj", normalizedObject));
        }

        var normalizedCity = NormalizeText(searchCity);
        if (normalizedCity != null)
        {
            filters.Add("SearchCity_lc = @city");
            parameters.Add(("@city", normalizedCity));
        }

        var normalizedRooms = NormalizeText(rooms);
        if (normalizedRooms != null)
        {
            filters.Add("Rooms_lc = @rooms");
            parameters.Add(("@rooms", normalizedRooms));
        }

        var where = filters.Count > 0 ? $"WHERE {string.Join(" AND ", filters)}" : string.Empty;

        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT DISTINCT MicroDistrict
            FROM LatestListings
            {where}
            ORDER BY MicroDistrict COLLATE NOCASE;
            """;

        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        var values = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (!reader.IsDBNull(0))
            {
                values.Add(reader.GetString(0));
            }
        }

        return values;
    }

    public async Task<IReadOnlyList<string>> GetDistinctHouseStatesAsync(CancellationToken cancellationToken)
    {
        return await GetDistinctValuesAsync("HouseState", cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetDistinctHouseStatesAsync(
        string? searchObject,
        string? searchCity,
        string? address,
        string? rooms,
        double? priceFrom,
        double? priceTo,
        double? pricePerSquareFrom,
        double? pricePerSquareTo,
        double? areaFrom,
        double? areaTo,
        double? areaLotFrom,
        double? areaLotTo,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync(cancellationToken);
        await EnsureColumnsAsync(connection);

        var filters = new List<string> { "HouseState IS NOT NULL AND HouseState <> ''" };
        var parameters = new List<(string Name, object? Value)>();

        var normalizedObject = NormalizeText(searchObject);
        if (normalizedObject != null)
        {
            filters.Add("SearchObject_lc = @obj");
            parameters.Add(("@obj", normalizedObject));
        }

        var normalizedCity = NormalizeText(searchCity);
        if (normalizedCity != null)
        {
            filters.Add("SearchCity_lc = @city");
            parameters.Add(("@city", normalizedCity));
        }

        var normalizedAddress = NormalizeText(address);
        if (normalizedAddress != null)
        {
            filters.Add("Address_lc = @address");
            parameters.Add(("@address", normalizedAddress));
        }

        var normalizedRooms = NormalizeText(rooms);
        if (normalizedRooms != null)
        {
            filters.Add("Rooms_lc = @rooms");
            parameters.Add(("@rooms", normalizedRooms));
        }

        AddNumericRangeFilter(filters, parameters, "PriceValue", "@priceFrom", "@priceTo", priceFrom, priceTo);
        AddNumericRangeFilter(filters, parameters, "PricePerSquareValue", "@pricePerFrom", "@pricePerTo", pricePerSquareFrom, pricePerSquareTo);
        AddNumericRangeFilter(filters, parameters, "AreaSquareValue", "@areaFrom", "@areaTo", areaFrom, areaTo);
        var where = filters.Count > 0 ? $"WHERE {string.Join(" AND ", filters)}" : string.Empty;

        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT DISTINCT HouseState
            FROM LatestListings
            {where}
            ORDER BY HouseState COLLATE NOCASE;
            """;

        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        var values = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (!reader.IsDBNull(0))
            {
                values.Add(reader.GetString(0));
            }
        }

        return values;
    }

    public async Task<(double? Min, double? Max)> GetPriceBoundsAsync(string? searchObject, string? searchCity, string? microDistrict, string? address, string? rooms, string? houseState, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync(cancellationToken);
        await EnsureColumnsAsync(connection);

        var filters = new List<string>();
        var parameters = new List<(string Name, object? Value)>();

        var normalizedObject = NormalizeText(searchObject);
        if (normalizedObject != null)
        {
            filters.Add("SearchObject_lc = @obj");
            parameters.Add(("@obj", normalizedObject));
        }

        var normalizedCity = NormalizeText(searchCity);
        if (normalizedCity != null)
        {
            filters.Add("SearchCity_lc = @city");
            parameters.Add(("@city", normalizedCity));
        }

        var normalizedMicroDistrict = NormalizeText(microDistrict);
        if (normalizedMicroDistrict != null)
        {
            filters.Add("MicroDistrict_lc = @microDistrict");
            parameters.Add(("@microDistrict", normalizedMicroDistrict));
        }

        var normalizedAddress = NormalizeText(address);
        if (normalizedAddress != null)
        {
            filters.Add("Address_lc = @address");
            parameters.Add(("@address", normalizedAddress));
        }

        var normalizedRooms = NormalizeText(rooms);
        if (normalizedRooms != null)
        {
            filters.Add("Rooms_lc = @rooms");
            parameters.Add(("@rooms", normalizedRooms));
        }

        var normalizedHouseState = NormalizeText(houseState);
        if (normalizedHouseState != null)
        {
            filters.Add("HouseState_lc = @houseState");
            parameters.Add(("@houseState", normalizedHouseState));
        }

        var where = filters.Count > 0 ? $"WHERE {string.Join(" AND ", filters)}" : string.Empty;

        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT MIN(PriceValue), MAX(PriceValue)
            FROM LatestListings
            {where};
            """;

        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        double? min = null;
        double? max = null;
        if (await reader.ReadAsync(cancellationToken))
        {
            if (!reader.IsDBNull(0))
            {
                min = reader.GetDouble(0);
            }
            if (!reader.IsDBNull(1))
            {
                max = reader.GetDouble(1);
            }
        }

        return (min, max);
    }

    public async Task<(double? Min, double? Max)> GetPricePerSquareBoundsAsync(string? searchObject, string? searchCity, string? microDistrict, string? address, string? rooms, string? houseState, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync(cancellationToken);
        await EnsureColumnsAsync(connection);

        var filters = new List<string>();
        var parameters = new List<(string Name, object? Value)>();

        var normalizedObject = NormalizeText(searchObject);
        if (normalizedObject != null)
        {
            filters.Add("SearchObject_lc = @obj");
            parameters.Add(("@obj", normalizedObject));
        }

        var normalizedCity = NormalizeText(searchCity);
        if (normalizedCity != null)
        {
            filters.Add("SearchCity_lc = @city");
            parameters.Add(("@city", normalizedCity));
        }

        var normalizedMicroDistrict = NormalizeText(microDistrict);
        if (normalizedMicroDistrict != null)
        {
            filters.Add("MicroDistrict_lc = @microDistrict");
            parameters.Add(("@microDistrict", normalizedMicroDistrict));
        }

        var normalizedAddress = NormalizeText(address);
        if (normalizedAddress != null)
        {
            filters.Add("Address_lc = @address");
            parameters.Add(("@address", normalizedAddress));
        }

        var normalizedRooms = NormalizeText(rooms);
        if (normalizedRooms != null)
        {
            filters.Add("Rooms_lc = @rooms");
            parameters.Add(("@rooms", normalizedRooms));
        }

        var normalizedHouseState = NormalizeText(houseState);
        if (normalizedHouseState != null)
        {
            filters.Add("HouseState_lc = @houseState");
            parameters.Add(("@houseState", normalizedHouseState));
        }

        var where = filters.Count > 0 ? $"WHERE {string.Join(" AND ", filters)}" : string.Empty;

        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT MIN(PricePerSquareValue), MAX(PricePerSquareValue)
            FROM LatestListings
            {where};
            """;

        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        double? min = null;
        double? max = null;
        if (await reader.ReadAsync(cancellationToken))
        {
            if (!reader.IsDBNull(0))
            {
                min = reader.GetDouble(0);
            }
            if (!reader.IsDBNull(1))
            {
                max = reader.GetDouble(1);
            }
        }

        return (min, max);
    }

    public async Task<(double? Min, double? Max)> GetAreaBoundsAsync(string? searchObject, string? searchCity, string? microDistrict, string? address, string? rooms, string? houseState, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync(cancellationToken);
        await EnsureColumnsAsync(connection);

        var filters = new List<string>();
        var parameters = new List<(string Name, object? Value)>();

        var normalizedObject = NormalizeText(searchObject);
        if (normalizedObject != null)
        {
            filters.Add("SearchObject_lc = @obj");
            parameters.Add(("@obj", normalizedObject));
        }

        var normalizedCity = NormalizeText(searchCity);
        if (normalizedCity != null)
        {
            filters.Add("SearchCity_lc = @city");
            parameters.Add(("@city", normalizedCity));
        }

        var normalizedMicroDistrict = NormalizeText(microDistrict);
        if (normalizedMicroDistrict != null)
        {
            filters.Add("MicroDistrict_lc = @microDistrict");
            parameters.Add(("@microDistrict", normalizedMicroDistrict));
        }

        var normalizedAddress = NormalizeText(address);
        if (normalizedAddress != null)
        {
            filters.Add("Address_lc = @address");
            parameters.Add(("@address", normalizedAddress));
        }

        var normalizedRooms = NormalizeText(rooms);
        if (normalizedRooms != null)
        {
            filters.Add("Rooms_lc = @rooms");
            parameters.Add(("@rooms", normalizedRooms));
        }

        var normalizedHouseState = NormalizeText(houseState);
        if (normalizedHouseState != null)
        {
            filters.Add("HouseState_lc = @houseState");
            parameters.Add(("@houseState", normalizedHouseState));
        }

        var where = filters.Count > 0 ? $"WHERE {string.Join(" AND ", filters)}" : string.Empty;

        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT MIN(AreaSquareValue), MAX(AreaSquareValue)
            FROM LatestListings
            {where};
            """;

        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        double? min = null;
        double? max = null;
        if (await reader.ReadAsync(cancellationToken))
        {
            if (!reader.IsDBNull(0))
            {
                min = reader.GetDouble(0);
            }
            if (!reader.IsDBNull(1))
            {
                max = reader.GetDouble(1);
            }
        }

        return (min, max);
    }

    public async Task<(double? Min, double? Max)> GetAreaLotBoundsAsync(string? searchObject, string? searchCity, string? microDistrict, string? address, string? rooms, string? houseState, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync(cancellationToken);
        await EnsureColumnsAsync(connection);

        var filters = new List<string>();
        var parameters = new List<(string Name, object? Value)>();

        var normalizedObject = NormalizeText(searchObject);
        if (normalizedObject != null)
        {
            filters.Add("SearchObject_lc = @obj");
            parameters.Add(("@obj", normalizedObject));
        }

        var normalizedCity = NormalizeText(searchCity);
        if (normalizedCity != null)
        {
            filters.Add("SearchCity_lc = @city");
            parameters.Add(("@city", normalizedCity));
        }

        var normalizedMicroDistrict = NormalizeText(microDistrict);
        if (normalizedMicroDistrict != null)
        {
            filters.Add("MicroDistrict_lc = @microDistrict");
            parameters.Add(("@microDistrict", normalizedMicroDistrict));
        }

        var normalizedAddress = NormalizeText(address);
        if (normalizedAddress != null)
        {
            filters.Add("Address_lc = @address");
            parameters.Add(("@address", normalizedAddress));
        }

        var normalizedRooms = NormalizeText(rooms);
        if (normalizedRooms != null)
        {
            filters.Add("Rooms_lc = @rooms");
            parameters.Add(("@rooms", normalizedRooms));
        }

        var normalizedHouseState = NormalizeText(houseState);
        if (normalizedHouseState != null)
        {
            filters.Add("HouseState_lc = @houseState");
            parameters.Add(("@houseState", normalizedHouseState));
        }

        var where = filters.Count > 0 ? $"WHERE {string.Join(" AND ", filters)}" : string.Empty;

        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT MIN(AreaLotValue), MAX(AreaLotValue)
            FROM LatestListings
            {where};
            """;

        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        double? min = null;
        double? max = null;
        if (await reader.ReadAsync(cancellationToken))
        {
            if (!reader.IsDBNull(0))
            {
                min = reader.GetDouble(0);
            }
            if (!reader.IsDBNull(1))
            {
                max = reader.GetDouble(1);
            }
        }

        return (min, max);
    }

    public async Task<IReadOnlyList<CityHistoryEntry>> GetCityHistoryAsync(IReadOnlyCollection<string> cities, string? searchObject, string? microDistrict, string? rooms, DateTime? fromDate, DateTime? toDate, CancellationToken cancellationToken)
    {
        if (cities == null || cities.Count == 0)
        {
            return Array.Empty<CityHistoryEntry>();
        }

        List<string> normalizedCities = cities
            .Select(NormalizeText)
            .Where(value => value != null)
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedCities.Count == 0)
        {
            return Array.Empty<CityHistoryEntry>();
        }

        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync(cancellationToken);
        await EnsureColumnsAsync(connection);

        var filters = new List<string>();
        var parameters = new List<(string Name, object? Value)>();

        var cityPlaceholders = normalizedCities.Select((_, idx) => $"@historyCity{idx}").ToList();
        filters.Add($"SearchCity_lc IN ({string.Join(", ", cityPlaceholders)})");
        for (int i = 0; i < normalizedCities.Count; i++)
        {
            parameters.Add((cityPlaceholders[i], normalizedCities[i]));
        }

        var normalizedSearchObject = NormalizeText(searchObject);
        if (normalizedSearchObject != null)
        {
            filters.Add("SearchObject_lc = @historyObject");
            parameters.Add(("@historyObject", normalizedSearchObject));
        }

        var normalizedMicroDistrict = NormalizeText(microDistrict);
        if (normalizedMicroDistrict != null)
        {
            filters.Add("MicroDistrict_lc = @historyMicroDistrict");
            parameters.Add(("@historyMicroDistrict", normalizedMicroDistrict));
        }

        var normalizedRooms = NormalizeText(rooms);
        if (normalizedRooms != null)
        {
            filters.Add("Rooms_lc = @historyRooms");
            parameters.Add(("@historyRooms", normalizedRooms));
        }

        if (fromDate.HasValue)
        {
            filters.Add("CollectedOn >= @historyFrom");
            parameters.Add(("@historyFrom", fromDate.Value.ToString("yyyy-MM-dd")));
        }

        if (toDate.HasValue)
        {
            filters.Add("CollectedOn <= @historyTo");
            parameters.Add(("@historyTo", toDate.Value.ToString("yyyy-MM-dd")));
        }

        string whereClause = filters.Count > 0 ? $"WHERE {string.Join(" AND ", filters)}" : string.Empty;

        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT SearchCity, CollectedOn,
                   AVG(PriceValue) AS AvgPrice,
                   AVG(PricePerSquareValue) AS AvgPricePerSquare
            FROM Listings
            {whereClause}
            GROUP BY SearchCity, CollectedOn
            ORDER BY SearchCity COLLATE NOCASE ASC, CollectedOn ASC;
            """;

        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        var entries = new List<CityHistoryEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (reader.IsDBNull(0) || reader.IsDBNull(1))
            {
                continue;
            }

            string city = reader.GetString(0);
            string collectedOn = reader.GetString(1);
            if (!DateTime.TryParse(collectedOn, out var date))
            {
                continue;
            }

            double? avgPrice = reader.IsDBNull(2) ? null : reader.GetDouble(2);
            double? avgPerSquare = reader.IsDBNull(3) ? null : reader.GetDouble(3);
            entries.Add(new CityHistoryEntry(city.Trim(), date, avgPrice, avgPerSquare));
        }

        return entries;
    }

    private static async Task EnsureLatestListingsSchemaAsync(SqliteConnection connection)
    {
        await using var createTable = connection.CreateCommand();
        createTable.CommandText =
            """
            CREATE TABLE IF NOT EXISTS LatestListings
            (
                ExternalId TEXT NOT NULL,
                SearchObject TEXT NOT NULL DEFAULT '',
                CollectedOnLatest TEXT NOT NULL DEFAULT '',
                SearchCity TEXT,
                MicroDistrict TEXT,
                Address TEXT,
                Price TEXT,
                PricePerSquare TEXT,
                Rooms TEXT,
                AreaSquare TEXT,
                AreaLot TEXT,
                HouseState TEXT,
                OfferType TEXT,
                Intendances TEXT,
                Floors TEXT,
                AdvertisementUrl TEXT,
                Selected INTEGER NOT NULL DEFAULT 0,
                SearchCity_lc TEXT,
                SearchObject_lc TEXT,
                MicroDistrict_lc TEXT,
                Address_lc TEXT,
                Rooms_lc TEXT,
                HouseState_lc TEXT,
                PriceValue REAL,
                PricePerSquareValue REAL,
                AreaSquareValue REAL,
                AreaLotValue REAL,
                PRIMARY KEY (ExternalId, SearchObject)
            );
            """;
        await createTable.ExecuteNonQueryAsync();

        await using var listingsFilterIndex = connection.CreateCommand();
        listingsFilterIndex.CommandText =
            """
            CREATE INDEX IF NOT EXISTS IX_Listings_FilterCombo
                ON Listings(SearchObject_lc, SearchCity_lc, Rooms_lc, MicroDistrict_lc, CollectedOn);
            """;
        await listingsFilterIndex.ExecuteNonQueryAsync();

        await using var latestFilterIndex = connection.CreateCommand();
        latestFilterIndex.CommandText =
            """
            CREATE INDEX IF NOT EXISTS IX_LatestListings_FilterCombo
                ON LatestListings(SearchObject_lc, SearchCity_lc, Rooms_lc, MicroDistrict_lc, CollectedOnLatest DESC);
            """;
        await latestFilterIndex.ExecuteNonQueryAsync();

        await using var latestPriceIndex = connection.CreateCommand();
        latestPriceIndex.CommandText =
            """
            CREATE INDEX IF NOT EXISTS IX_LatestListings_PriceValue
                ON LatestListings(PriceValue DESC);
            """;
        await latestPriceIndex.ExecuteNonQueryAsync();

        await using var latestCollectedIndex = connection.CreateCommand();
        latestCollectedIndex.CommandText =
            """
            CREATE INDEX IF NOT EXISTS IX_LatestListings_CollectedOnLatest
                ON LatestListings(CollectedOnLatest DESC);
            """;
        await latestCollectedIndex.ExecuteNonQueryAsync();

        await using var ftsCommand = connection.CreateCommand();
        ftsCommand.CommandText =
            """
            CREATE VIRTUAL TABLE IF NOT EXISTS LatestListingsAddressFts
                USING fts5(Address_lc, tokenize = 'unicode61', content = 'LatestListings', content_rowid = 'rowid');
            """;
        await ftsCommand.ExecuteNonQueryAsync();

        await using var insertTrigger = connection.CreateCommand();
        insertTrigger.CommandText =
            """
            CREATE TRIGGER IF NOT EXISTS LatestListings_fts_insert AFTER INSERT ON LatestListings BEGIN
                INSERT INTO LatestListingsAddressFts(rowid, Address_lc)
                VALUES (new.rowid, COALESCE(new.Address_lc, ''));
            END;
            """;
        await insertTrigger.ExecuteNonQueryAsync();

        await using var deleteTrigger = connection.CreateCommand();
        deleteTrigger.CommandText =
            """
            CREATE TRIGGER IF NOT EXISTS LatestListings_fts_delete AFTER DELETE ON LatestListings BEGIN
                INSERT INTO LatestListingsAddressFts(LatestListingsAddressFts, rowid, Address_lc)
                VALUES('delete', old.rowid, COALESCE(old.Address_lc, ''));
            END;
            """;
        await deleteTrigger.ExecuteNonQueryAsync();

        await using var updateTrigger = connection.CreateCommand();
        updateTrigger.CommandText =
            """
            CREATE TRIGGER IF NOT EXISTS LatestListings_fts_update AFTER UPDATE ON LatestListings BEGIN
                INSERT INTO LatestListingsAddressFts(LatestListingsAddressFts, rowid, Address_lc)
                VALUES('delete', old.rowid, COALESCE(old.Address_lc, ''));
                INSERT INTO LatestListingsAddressFts(rowid, Address_lc)
                VALUES (new.rowid, COALESCE(new.Address_lc, ''));
            END;
            """;
        await updateTrigger.ExecuteNonQueryAsync();
    }

    private static async Task OptimizeDatabaseAsync(SqliteConnection connection)
    {
        await using var analyze = connection.CreateCommand();
        analyze.CommandText = "ANALYZE;";
        await analyze.ExecuteNonQueryAsync();

        await using var optimize = connection.CreateCommand();
        optimize.CommandText = "PRAGMA optimize;";
        await optimize.ExecuteNonQueryAsync();
    }

    private async Task BackfillLatestListingsAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            WITH Ranked AS (
                SELECT
                    TRIM(IFNULL(ExternalId, '')) AS ExternalId,
                    COALESCE(NULLIF(TRIM(SearchObject), ''), '') AS SearchObject,
                    CollectedOn,
                    NULLIF(TRIM(IFNULL(SearchCity, '')), '') AS SearchCity,
                    NULLIF(TRIM(IFNULL(MicroDistrict, '')), '') AS MicroDistrict,
                    NULLIF(TRIM(IFNULL(Address, '')), '') AS Address,
                    NULLIF(TRIM(IFNULL(Price, '')), '') AS Price,
                    NULLIF(TRIM(IFNULL(PricePerSquare, '')), '') AS PricePerSquare,
                    NULLIF(TRIM(IFNULL(Rooms, '')), '') AS Rooms,
                    NULLIF(TRIM(IFNULL(AreaSquare, '')), '') AS AreaSquare,
                    NULLIF(TRIM(IFNULL(AreaLot, '')), '') AS AreaLot,
                    NULLIF(TRIM(IFNULL(HouseState, '')), '') AS HouseState,
                    NULLIF(TRIM(IFNULL(OfferType, '')), '') AS OfferType,
                    NULLIF(TRIM(IFNULL(Intendances, '')), '') AS Intendances,
                    NULLIF(TRIM(IFNULL(Floors, '')), '') AS Floors,
                    NULLIF(TRIM(IFNULL(AdvertisementUrl, '')), '') AS AdvertisementUrl,
                    COALESCE(Selected, 0) AS Selected,
                    COALESCE(SearchCity_lc, NULLIF(LOWER(TRIM(IFNULL(SearchCity, ''))), '')) AS SearchCity_lc,
                    COALESCE(SearchObject_lc, NULLIF(LOWER(TRIM(IFNULL(SearchObject, ''))), '')) AS SearchObject_lc,
                    COALESCE(MicroDistrict_lc, NULLIF(LOWER(TRIM(IFNULL(MicroDistrict, ''))), '')) AS MicroDistrict_lc,
                    COALESCE(Address_lc, NULLIF(LOWER(TRIM(IFNULL(Address, ''))), '')) AS Address_lc,
                    COALESCE(Rooms_lc, NULLIF(LOWER(TRIM(IFNULL(Rooms, ''))), '')) AS Rooms_lc,
                    COALESCE(HouseState_lc, NULLIF(LOWER(TRIM(IFNULL(HouseState, ''))), '')) AS HouseState_lc,
                    PriceValue,
                    PricePerSquareValue,
                    AreaSquareValue,
                    AreaLotValue,
                    ROW_NUMBER() OVER (
                        PARTITION BY TRIM(IFNULL(ExternalId, '')), COALESCE(NULLIF(TRIM(SearchObject), ''), '')
                        ORDER BY CollectedOn DESC, Id DESC
                    ) AS rn
                FROM Listings
                WHERE TRIM(IFNULL(ExternalId, '')) <> ''
            )
            INSERT OR REPLACE INTO LatestListings
            (
                ExternalId,
                SearchObject,
                CollectedOnLatest,
                SearchCity,
                MicroDistrict,
                Address,
                Price,
                PricePerSquare,
                Rooms,
                AreaSquare,
                AreaLot,
                HouseState,
                OfferType,
                Intendances,
                Floors,
                AdvertisementUrl,
                Selected,
                SearchCity_lc,
                SearchObject_lc,
                MicroDistrict_lc,
                Address_lc,
                Rooms_lc,
                HouseState_lc,
                PriceValue,
                PricePerSquareValue,
                AreaSquareValue,
                AreaLotValue
            )
            SELECT
                ExternalId,
                SearchObject,
                CollectedOn,
                SearchCity,
                MicroDistrict,
                Address,
                Price,
                PricePerSquare,
                Rooms,
                AreaSquare,
                AreaLot,
                HouseState,
                OfferType,
                Intendances,
                Floors,
                AdvertisementUrl,
                Selected,
                SearchCity_lc,
                SearchObject_lc,
                MicroDistrict_lc,
                Address_lc,
                Rooms_lc,
                HouseState_lc,
                PriceValue,
                PricePerSquareValue,
                AreaSquareValue,
                AreaLotValue
            FROM Ranked
            WHERE rn = 1
            ON CONFLICT(ExternalId, SearchObject) DO UPDATE SET
                CollectedOnLatest = excluded.CollectedOnLatest,
                SearchCity = excluded.SearchCity,
                MicroDistrict = excluded.MicroDistrict,
                Address = excluded.Address,
                Price = excluded.Price,
                PricePerSquare = excluded.PricePerSquare,
                Rooms = excluded.Rooms,
                AreaSquare = excluded.AreaSquare,
                AreaLot = excluded.AreaLot,
                HouseState = excluded.HouseState,
                OfferType = excluded.OfferType,
                Intendances = excluded.Intendances,
                Floors = excluded.Floors,
                AdvertisementUrl = excluded.AdvertisementUrl,
                Selected = excluded.Selected,
                SearchCity_lc = excluded.SearchCity_lc,
                SearchObject_lc = excluded.SearchObject_lc,
                MicroDistrict_lc = excluded.MicroDistrict_lc,
                Address_lc = excluded.Address_lc,
                Rooms_lc = excluded.Rooms_lc,
                HouseState_lc = excluded.HouseState_lc,
                PriceValue = excluded.PriceValue,
                PricePerSquareValue = excluded.PricePerSquareValue,
                AreaSquareValue = excluded.AreaSquareValue,
                AreaLotValue = excluded.AreaLotValue
            WHERE excluded.CollectedOnLatest >= LatestListings.CollectedOnLatest;
            """;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<KeysetSeekTuple?> GetSeekTupleAsync(
        SqliteConnection connection,
        string fromClause,
        string whereClause,
        string orderClause,
        IReadOnlyList<(string Name, object? Value)> parameters,
        int offset,
        CancellationToken cancellationToken)
    {
        if (offset <= 0)
        {
            return null;
        }

        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT
                PriceValue,
                CollectedOnLatest,
                ExternalId
            FROM {fromClause}
            {whereClause}
            {orderClause}
            LIMIT 1 OFFSET @seekOffset;
            """;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        command.Parameters.AddWithValue("@seekOffset", Math.Max(0, offset - 1));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var priceNullFlag = reader.IsDBNull(0) ? 1 : 0;
        double? priceValue = reader.IsDBNull(0) ? (double?)null : reader.GetDouble(0);
        var collectedOn = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
        var externalId = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
        return new KeysetSeekTuple(priceValue, collectedOn, externalId, priceNullFlag);
    }

    private static void AddNumericRangeFilter(List<string> filters, List<(string Name, object? Value)> parameters, string expression, string fromParam, string toParam, double? from, double? to)
    {
        if (from.HasValue)
        {
            filters.Add($"{expression} >= {fromParam}");
            parameters.Add((fromParam, from.Value));
        }

        if (to.HasValue)
        {
            filters.Add($"{expression} <= {toParam}");
            parameters.Add((toParam, to.Value));
        }
    }

    private static void SetParameterValue(SqliteParameter parameter, object? value)
    {
        parameter.Value = value ?? DBNull.Value;
    }

    private static string? NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed.ToLowerInvariant();
    }

    private static double? ParseNumericValue(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var cleaned = rawValue.Trim();
        var tokens = new[]
        {
            "?",
            "€",
            "/m²",
            "/m2",
            "m²",
            "m2",
            "kv.m",
            "kv. m"
        };

        foreach (var token in tokens)
        {
            cleaned = cleaned.Replace(token, string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        cleaned = cleaned.Replace('\u00A0', ' ');
        cleaned = cleaned.Replace(" ", string.Empty, StringComparison.Ordinal);
        cleaned = cleaned.Replace(",", ".", StringComparison.Ordinal);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return null;
        }

        return double.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }

    private static string BuildNumericExpression(string columnReference)
    {
        var expression = $"REPLACE({columnReference}, ',', '.')";
        foreach (var token in NumericExpressionTokens)
        {
            var escaped = token.Replace("'", "''");
            expression = $"REPLACE({expression}, '{escaped}', '')";
        }

        expression = $"TRIM({expression})";
        expression = $"NULLIF({expression}, '')";
        return $"CAST({expression} AS REAL)";
    }

    private static async Task ApplyConnectionPragmasAsync(SqliteConnection connection)
    {
        foreach (var (name, value) in ConnectionPragmas)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA {name} = {value};";
            await command.ExecuteNonQueryAsync();
        }
    }

    private async Task LogTableInfoAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(Listings);";
        var columns = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (reader.IsDBNull(1))
            {
                continue;
            }

            var name = reader.GetString(1);
            var type = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
            columns.Add(string.IsNullOrWhiteSpace(type) ? name : $"{name} ({type})");
        }

        if (columns.Count > 0)
        {
            _logService.Info($"Listings table columns: {string.Join(", ", columns)}");
        }
        else
        {
            _logService.Info("Listings table_info returned no columns.");
        }
    }
    
    private async Task<IReadOnlyList<string>> GetDistinctValuesAsync(string column, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync(cancellationToken);
        await EnsureColumnsAsync(connection);

        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT DISTINCT {column}
            FROM Listings
            WHERE {column} IS NOT NULL AND {column} <> ''
            ORDER BY {column} COLLATE NOCASE;
            """;

        var values = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (!reader.IsDBNull(0))
            {
                values.Add(reader.GetString(0));
            }
        }

        return values;
    }

    private static async Task RemoveDeprecatedColumnsAsync(SqliteConnection connection)
    {
        var existing = await GetExistingColumnsAsync(connection);
        var hasDeprecated = DeprecatedColumns.Any(existing.Contains);
        var missingRequired = RequiredColumns.Any(c => !existing.Contains(c.Name));

        if (!hasDeprecated && !missingRequired)
        {
            return;
        }

        await using var transaction = await connection.BeginTransactionAsync() as SqliteTransaction
            ?? throw new InvalidOperationException("Nepavyko pradeti SQLite migracijos transakcijos.");
        await DropTableIfExistsAsync(connection, transaction, "Listings_new");
        await CreateListingsTableAsync(connection, "Listings_new", transaction);

        var insertColumns = string.Join(", ", ColumnsToPreserve);
        var selectColumns = BuildSelectColumns(existing);

        await using (var copyCommand = connection.CreateCommand())
        {
            copyCommand.Transaction = transaction;
            copyCommand.CommandText = $"INSERT INTO Listings_new ({insertColumns}) SELECT {selectColumns} FROM Listings;";
            await copyCommand.ExecuteNonQueryAsync();
        }

        await using (var dropOld = connection.CreateCommand())
        {
            dropOld.Transaction = transaction;
            dropOld.CommandText = "DROP TABLE Listings;";
            await dropOld.ExecuteNonQueryAsync();
        }

        await using (var rename = connection.CreateCommand())
        {
            rename.Transaction = transaction;
            rename.CommandText = "ALTER TABLE Listings_new RENAME TO Listings;";
            await rename.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    private static string BuildSelectColumns(IReadOnlySet<string> existingColumns)
    {
        var parts = new List<string>(ColumnsToPreserve.Length);
        foreach (var column in ColumnsToPreserve)
        {
            if (existingColumns.Contains(column))
            {
                parts.Add(column);
                continue;
            }

            string fallback = column switch
            {
                var c when c.Equals("Id", StringComparison.OrdinalIgnoreCase) => "NULL",
                var c when c.Equals("Selected", StringComparison.OrdinalIgnoreCase) => "0",
                _ => "''"
            };
            parts.Add($"{fallback} AS {column}");
        }

        return string.Join(", ", parts);
    }

    private static async Task DropTableIfExistsAsync(SqliteConnection connection, SqliteTransaction transaction, string tableName)
    {
        await using var dropCommand = connection.CreateCommand();
        dropCommand.Transaction = transaction;
        dropCommand.CommandText = $"DROP TABLE IF EXISTS {tableName};";
        await dropCommand.ExecuteNonQueryAsync();
    }

    private static async Task CreateListingsTableAsync(SqliteConnection connection, string tableName, SqliteTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"""
            CREATE TABLE IF NOT EXISTS {tableName}
            (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ExternalId TEXT,
                CollectedOn TEXT NOT NULL DEFAULT '',
                SearchCity TEXT,
                SearchObject TEXT,
                MicroDistrict TEXT,
                Address TEXT,
                Price TEXT,
                PricePerSquare TEXT,
                Rooms TEXT,
                AreaSquare TEXT,
                AreaLot TEXT,
                HouseState TEXT,
                OfferType TEXT,
                Intendances TEXT,
                Floors TEXT,
                AdvertisementUrl TEXT,
                Selected INTEGER NOT NULL DEFAULT 0,
                SearchCity_lc TEXT,
                SearchObject_lc TEXT,
                MicroDistrict_lc TEXT,
                Address_lc TEXT,
                Rooms_lc TEXT,
                HouseState_lc TEXT,
                PriceValue REAL,
                PricePerSquareValue REAL,
                AreaSquareValue REAL,
                AreaLotValue REAL
            );
            """;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<HashSet<string>> EnsureColumnsAsync(SqliteConnection connection)
    {
        var existing = await GetExistingColumnsAsync(connection);
        var addedColumn = false;
        foreach (var column in RequiredColumns)
        {
            if (existing.Contains(column.Name))
            {
                continue;
            }

            await using var command = connection.CreateCommand();
            command.CommandText = $"ALTER TABLE Listings ADD COLUMN {column.Name} {column.Definition};";
            await command.ExecuteNonQueryAsync();
            addedColumn = true;
        }

        if (addedColumn)
        {
            existing = await GetExistingColumnsAsync(connection);
        }

        return existing;
    }

    private static async Task BackfillMicroDistrictNormalizedAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE Listings
            SET MicroDistrict_lc = LOWER(TRIM(MicroDistrict))
            WHERE MicroDistrict IS NOT NULL
              AND MicroDistrict <> ''
              AND (MicroDistrict_lc IS NULL OR MicroDistrict_lc = '');
            """;
        await command.ExecuteNonQueryAsync();
    }

    private async Task BackfillNumericColumnsAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                Id,
                Price,
                PricePerSquare,
                AreaSquare,
                AreaLot,
                PriceValue,
                PricePerSquareValue,
                AreaSquareValue,
                AreaLotValue
            FROM Listings
            WHERE PriceValue IS NULL
               OR PricePerSquareValue IS NULL
               OR AreaSquareValue IS NULL
               OR AreaLotValue IS NULL;
            """;

        var pending = new List<(long Id, double? Price, double? PricePerSquare, double? AreaSquare, double? AreaLot)>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var id = reader.GetInt64(0);
            double? priceValue = null;
            double? pricePerSquareValue = null;
            double? areaSquareValue = null;
            double? areaLotValue = null;
            bool needsUpdate = false;

            if (reader.IsDBNull(5))
            {
                priceValue = ParseNumericValue(reader.IsDBNull(1) ? null : reader.GetString(1));
                if (priceValue.HasValue)
                {
                    needsUpdate = true;
                }
            }

            if (reader.IsDBNull(6))
            {
                pricePerSquareValue = ParseNumericValue(reader.IsDBNull(2) ? null : reader.GetString(2));
                if (pricePerSquareValue.HasValue)
                {
                    needsUpdate = true;
                }
            }

            if (reader.IsDBNull(7))
            {
                areaSquareValue = ParseNumericValue(reader.IsDBNull(3) ? null : reader.GetString(3));
                if (areaSquareValue.HasValue)
                {
                    needsUpdate = true;
                }
            }

            if (reader.IsDBNull(8))
            {
                areaLotValue = ParseNumericValue(reader.IsDBNull(4) ? null : reader.GetString(4));
                if (areaLotValue.HasValue)
                {
                    needsUpdate = true;
                }
            }

            if (needsUpdate)
            {
                pending.Add((id, priceValue, pricePerSquareValue, areaSquareValue, areaLotValue));
            }
        }

        if (pending.Count == 0)
        {
            return;
        }

        await using var transaction = await connection.BeginTransactionAsync() as SqliteTransaction
            ?? throw new InvalidOperationException("Nepavyko pradeti SQLite transakcijos.");

        await using var updateCommand = connection.CreateCommand();
        updateCommand.Transaction = transaction;
        updateCommand.CommandText =
            """
            UPDATE Listings
            SET
                PriceValue = CASE WHEN PriceValue IS NULL AND @priceValue IS NOT NULL THEN @priceValue ELSE PriceValue END,
                PricePerSquareValue = CASE WHEN PricePerSquareValue IS NULL AND @pricePerSquareValue IS NOT NULL THEN @pricePerSquareValue ELSE PricePerSquareValue END,
                AreaSquareValue = CASE WHEN AreaSquareValue IS NULL AND @areaSquareValue IS NOT NULL THEN @areaSquareValue ELSE AreaSquareValue END,
                AreaLotValue = CASE WHEN AreaLotValue IS NULL AND @areaLotValue IS NOT NULL THEN @areaLotValue ELSE AreaLotValue END
            WHERE Id = @id;
            """;

        var paramId = updateCommand.Parameters.Add("@id", SqliteType.Integer);
        var paramPrice = updateCommand.Parameters.Add("@priceValue", SqliteType.Real);
        var paramPricePerSquare = updateCommand.Parameters.Add("@pricePerSquareValue", SqliteType.Real);
        var paramAreaSquare = updateCommand.Parameters.Add("@areaSquareValue", SqliteType.Real);
        var paramAreaLot = updateCommand.Parameters.Add("@areaLotValue", SqliteType.Real);
        var updatedRows = 0;

        foreach (var (id, price, pricePerSquare, areaSquare, areaLot) in pending)
        {
            SetParameterValue(paramId, id);
            SetParameterValue(paramPrice, price);
            SetParameterValue(paramPricePerSquare, pricePerSquare);
            SetParameterValue(paramAreaSquare, areaSquare);
            SetParameterValue(paramAreaLot, areaLot);
            updatedRows += await updateCommand.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
        if (updatedRows > 0)
        {
            _logService.Info($"Backfilled numeric columns for {updatedRows} row(s).");
        }
    }

    private static async Task<HashSet<string>> GetExistingColumnsAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(Listings);";

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (!reader.IsDBNull(1))
            {
                columns.Add(reader.GetString(1));
            }
        }

        return columns;
    }

}

