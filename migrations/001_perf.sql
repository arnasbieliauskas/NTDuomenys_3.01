BEGIN TRANSACTION;

ALTER TABLE Listings ADD COLUMN IF NOT EXISTS SearchCity_lc TEXT;
ALTER TABLE Listings ADD COLUMN IF NOT EXISTS SearchObject_lc TEXT;
ALTER TABLE Listings ADD COLUMN IF NOT EXISTS Address_lc TEXT;
ALTER TABLE Listings ADD COLUMN IF NOT EXISTS Rooms_lc TEXT;
ALTER TABLE Listings ADD COLUMN IF NOT EXISTS HouseState_lc TEXT;
ALTER TABLE Listings ADD COLUMN IF NOT EXISTS PriceValue REAL;
ALTER TABLE Listings ADD COLUMN IF NOT EXISTS PricePerSquareValue REAL;
ALTER TABLE Listings ADD COLUMN IF NOT EXISTS AreaSquareValue REAL;
ALTER TABLE Listings ADD COLUMN IF NOT EXISTS AreaLotValue REAL;

UPDATE Listings
SET
    SearchCity_lc = NULLIF(LOWER(TRIM(IFNULL(SearchCity, ''))), ''),
    SearchObject_lc = NULLIF(LOWER(TRIM(IFNULL(SearchObject, ''))), ''),
    Address_lc = NULLIF(LOWER(TRIM(IFNULL(Address, ''))), ''),
    Rooms_lc = NULLIF(LOWER(TRIM(IFNULL(Rooms, ''))), ''),
    HouseState_lc = NULLIF(LOWER(TRIM(IFNULL(HouseState, ''))), ''),
    PriceValue = CAST(
        NULLIF(
            REPLACE(
                REPLACE(
                    REPLACE(
                        REPLACE(
                            REPLACE(
                                REPLACE(
                                    REPLACE(
                                        REPLACE(
                                            REPLACE(
                                                LOWER(IFNULL(Price, '')),
                                                '?', ''
                                            ),
                                            '€', ''
                                        ),
                                        '/m2', ''
                                    ),
                                    'm2', ''
                                ),
                                'kv.m', ''
                            ),
                            'kv. m', ''
                        ),
                        CHAR(160), ''
                    ),
                    ' ', ''
                ),
                ',', '.'
            ),
            ''
        ) AS REAL
    ),
    PricePerSquareValue = CAST(
        NULLIF(
            REPLACE(
                REPLACE(
                    REPLACE(
                        REPLACE(
                            REPLACE(
                                REPLACE(
                                    REPLACE(
                                        REPLACE(
                                            REPLACE(
                                                LOWER(IFNULL(PricePerSquare, '')),
                                                '?', ''
                                            ),
                                            '€', ''
                                        ),
                                        '/m2', ''
                                    ),
                                    'm2', ''
                                ),
                                'kv.m', ''
                            ),
                            'kv. m', ''
                        ),
                        CHAR(160), ''
                    ),
                    ' ', ''
                ),
                ',', '.'
            ),
            ''
        ) AS REAL
    ),
    AreaSquareValue = CAST(
        NULLIF(
            REPLACE(
                REPLACE(
                    REPLACE(
                        REPLACE(
                            REPLACE(
                                REPLACE(
                                    REPLACE(
                                        REPLACE(
                                            REPLACE(
                                                LOWER(IFNULL(AreaSquare, '')),
                                                '?', ''
                                            ),
                                            '€', ''
                                        ),
                                        '/m2', ''
                                    ),
                                    'm2', ''
                                ),
                                'kv.m', ''
                            ),
                            'kv. m', ''
                        ),
                        CHAR(160), ''
                    ),
                    ' ', ''
                ),
                ',', '.'
            ),
            ''
        ) AS REAL
    ),
    AreaLotValue = CAST(
        NULLIF(
            REPLACE(
                REPLACE(
                    REPLACE(
                        REPLACE(
                            REPLACE(
                                REPLACE(
                                    REPLACE(
                                        REPLACE(
                                            REPLACE(
                                                LOWER(IFNULL(AreaLot, '')),
                                                '?', ''
                                            ),
                                            '€', ''
                                        ),
                                        '/m2', ''
                                    ),
                                    'm2', ''
                                ),
                                'kv.m', ''
                            ),
                            'kv. m', ''
                        ),
                        CHAR(160), ''
                    ),
                    ' ', ''
                ),
                ',', '.'
            ),
            ''
        ) AS REAL;

CREATE UNIQUE INDEX IF NOT EXISTS IX_Listings_ExternalIdDate ON Listings(ExternalId, CollectedOn);
CREATE INDEX IF NOT EXISTS IX_Listings_CollectedOn_Selected ON Listings(CollectedOn, Selected);
CREATE INDEX IF NOT EXISTS IX_Listings_TextFilters ON Listings(SearchObject_lc, SearchCity_lc, Rooms_lc, HouseState_lc, CollectedOn);
CREATE INDEX IF NOT EXISTS IX_Listings_AddressLc ON Listings(Address_lc);
CREATE INDEX IF NOT EXISTS IX_Listings_PriceValue ON Listings(PriceValue);
CREATE INDEX IF NOT EXISTS IX_Listings_PricePerSquareValue ON Listings(PricePerSquareValue);
CREATE INDEX IF NOT EXISTS IX_Listings_AreaSquareValue ON Listings(AreaSquareValue);
CREATE INDEX IF NOT EXISTS IX_Listings_AreaLotValue ON Listings(AreaLotValue);

COMMIT;
