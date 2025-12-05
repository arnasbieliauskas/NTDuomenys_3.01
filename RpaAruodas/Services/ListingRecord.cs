using System;
using System.Collections.Generic;

namespace RpaAruodas.Services;

public class ListingRecord
{
    public string ExternalId { get; init; } = string.Empty;
    public string AdvertisementUrl { get; init; } = string.Empty;
    public string SearchCity { get; init; } = string.Empty;
    public string SearchObject { get; init; } = string.Empty;
    public string MicroDistrict { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string Price { get; init; } = string.Empty;
    public string PricePerSquare { get; init; } = string.Empty;
    public string Rooms { get; init; } = string.Empty;
    public string AreaSquare { get; init; } = string.Empty;
    public string AreaLot { get; init; } = string.Empty;
    public string HouseState { get; init; } = string.Empty;
    public string OfferType { get; init; } = string.Empty;
    public string Intendances { get; init; } = string.Empty;
    public string Floors { get; init; } = string.Empty;
    public bool Selected { get; init; }
}

public class ListingSaveResult
{
    public ListingSaveResult(int inserted, int skipped)
    {
        Inserted = inserted;
        Skipped = skipped;
    }

    public int Inserted { get; }
    public int Skipped { get; }
}

public class StatsListing
{
    public string CollectedOn { get; init; } = string.Empty;
    public string SearchObject { get; init; } = string.Empty;
    public string SearchCity { get; init; } = string.Empty;
    public string MicroDistrict { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string Price { get; init; } = string.Empty;
    public string PricePerSquare { get; init; } = string.Empty;
    public string Rooms { get; init; } = string.Empty;
    public string AreaSquare { get; init; } = string.Empty;
    public string AreaLot { get; init; } = string.Empty;
    public string HouseState { get; init; } = string.Empty;
    public string OfferType { get; init; } = string.Empty;
    public string Intendances { get; init; } = string.Empty;
    public string Floors { get; init; } = string.Empty;
    public string AdvertisementUrl { get; init; } = string.Empty;
    public string ExternalId { get; init; } = string.Empty;
    public bool Selected { get; init; }
    public int VersionCount { get; init; }
    public double? PriceChangePercent { get; init; }
}

public sealed record StatsQueryResult(
    IReadOnlyList<StatsListing> Listings,
    int TotalCount,
    double? AveragePricePerSquare,
    double? AveragePrice,
    double? MinPrice,
    double? MaxPrice,
    string? MaxPriceUrl,
    string? MinPriceUrl);
