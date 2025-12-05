BEGIN TRANSACTION;

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

DROP INDEX IF EXISTS IX_Listings_ExternalIdDate;
CREATE UNIQUE INDEX IF NOT EXISTS IX_Listings_ExternalIdSearchObjectCollectedOn
    ON Listings(
        TRIM(IFNULL(ExternalId, '')),
        COALESCE(NULLIF(TRIM(SearchObject), ''), ''),
        CollectedOn
    );

CREATE INDEX IF NOT EXISTS IX_Listings_FilterCombo ON Listings(SearchObject_lc, SearchCity_lc, Rooms_lc, MicroDistrict_lc, CollectedOn);
CREATE INDEX IF NOT EXISTS IX_LatestListings_FilterCombo ON LatestListings(SearchObject_lc, SearchCity_lc, Rooms_lc, MicroDistrict_lc, CollectedOnLatest DESC);
CREATE INDEX IF NOT EXISTS IX_LatestListings_PriceValue ON LatestListings(PriceValue DESC);
CREATE INDEX IF NOT EXISTS IX_LatestListings_CollectedOnLatest ON LatestListings(CollectedOnLatest DESC);

-- Unicode61 tokenizer preserves Lithuanian diacritics/digraphs for free-text address search.
CREATE VIRTUAL TABLE IF NOT EXISTS LatestListingsAddressFts
    USING fts5(Address_lc, tokenize = 'unicode61', content = 'LatestListings', content_rowid = 'rowid');

CREATE TRIGGER IF NOT EXISTS LatestListings_fts_insert AFTER INSERT ON LatestListings BEGIN
    INSERT INTO LatestListingsAddressFts(rowid, Address_lc)
    VALUES (new.rowid, COALESCE(new.Address_lc, ''));
END;

CREATE TRIGGER IF NOT EXISTS LatestListings_fts_delete AFTER DELETE ON LatestListings BEGIN
    INSERT INTO LatestListingsAddressFts(LatestListingsAddressFts, rowid, Address_lc)
    VALUES('delete', old.rowid, COALESCE(old.Address_lc, ''));
END;

CREATE TRIGGER IF NOT EXISTS LatestListings_fts_update AFTER UPDATE ON LatestListings BEGIN
    INSERT INTO LatestListingsAddressFts(LatestListingsAddressFts, rowid, Address_lc)
    VALUES('delete', old.rowid, COALESCE(old.Address_lc, ''));
    INSERT INTO LatestListingsAddressFts(rowid, Address_lc)
    VALUES (new.rowid, COALESCE(new.Address_lc, ''));
END;

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

INSERT INTO LatestListingsAddressFts(LatestListingsAddressFts) VALUES('rebuild');

COMMIT;
ANALYZE;
PRAGMA optimize;
