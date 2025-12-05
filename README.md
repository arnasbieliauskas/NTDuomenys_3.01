# NT Duomenys Studio (RpaAruodas)

## Apžvalga
`RpaAruodas` yra „Avalonia“ pagrindu sukurta darbalaukio programa, kuri vienoje vietoje paleidžia stealth Playwright naršyklę, leidžia bandyti filtrų kombinacijas ir automatiškai nuskaityti `aruodas.lt` skelbimus. Kiekvienas paieškos paspaudimas įjungia `AruodasAutomationService`, kuris perskaito visus puslapius, paima kainą, plotą, mikrorajoną, pastato būklę ir kitą meta informaciją ir įrašo juos į lokalią SQLite bazę.

## Pagrindinės funkcijos
- Stealth Playwright sesija (`PlaywrightRunner`) su lietuvišku lokalizacijos nustatymu, pasirinktiniu user agentu ir `navigator.webdriver` slopinimu, kad `aruodas.lt` neaptiktų automatizuoto rinkimo.
- Automatinis `Aruodas.lt` paieškos aptikimas (`buttonSearchForm` markeris) ir po kiekvieno „Ieškoti“ nuskaitymo ciklas, kuris surenka naujas eilutes ir dedupinuoja pagal `ExternalId + CollectedOn`.
- SQLite saugykla (`storage/ntduomenys.db`) su `Listings` lentele, papildomomis `PriceValue`, `PricePerSquareValue`, `AreaSquareValue`, `AreaLotValue` ir indeksais filtravimui.
- Statistikos langas, kuriame galima filtruoti pagal objektą, miestą, adresą, kambarių skaičių, kainos/plotų intervalus, pažiūrėti kainų pokyčius/įrašų istoriją ir pažymėtus rezultatus.
- Vaizdavimas: kairėje yra „Aruodas.lt“ ir „Statistika“ mygtukai, dešinėje sritis rodo naršyklės vaizdą arba statistiką, o po automatiniu surinkimu atvaizduojamas blur uždengimas su suvestiniu.
- Žurnalas (`logs/logo - YYYY.MM.DD.txt`) fiksuoja svarbius įvykius: paleidimą, paieškos paspaudimus, automatinius nuskaitymus ir klaidas.

## Priklausomybės
1. [.NET SDK 8.0](https://dotnet.microsoft.com/) – projektas tikslinamas į `net8.0`.
2. `Microsoft.Playwright` (biblioteka automatiškai kviečia `Playwright.Program.Main install chromium`, tad pirmą kartą paleidus reikalingas tinklo ryšys).
3. Veikia Windows/macOS/Linux, bet `app.manifest` ir GUI pritaikyti „WinExe“ tipui.

## Diegimas ir paleidimas
1. Atsisiųskite priklausomybes: `dotnet restore`.
2. Paleiskite aplikaciją komandose: `dotnet run --project RpaAruodas`.
3. Alternatyva pakavimui: `dotnet publish RpaAruodas -c Release -r win-x64 --self-contained false` (arba pasirinktas tikslinis `RID`).
4. Pirmą kartą paleidus Playwright atsisiųs Chromium; jeigu reikia, rankiniu būdu `dotnet tool install --global Microsoft.Playwright.CLI` ir `playwright install chromium`.

## Konfigūravimas
- `appsettings.json` (vykdymo aplanke) kontroliuoja langą, duomenų failą ir naršyklės režimą:
  - `Window` – pavadinimas, dydis, minimali reikšmė ir pozicija.
  - `Database.FilePath` – kelias iki SQLite failo (`storage/ntduomenys.db` pagal nutylėjimą, sukuriamas automatiškai).
  - `Playwright.Headless` – `true` režime nesimatys Chromium lango, `false` leidžia stebėti naršymo eigą GUI viduje.
- Dėl dinaminės DI grandinės `Program.cs` tiesiogiai naudoja `ConfigurationService`, tad pasikeitus `appsettings` reikia iš naujo paleisti aplikaciją.

## Duomenų bazė ir žurnalai
- `DatabaseService` saugo tik naujas kombinacijas ir palaiko `Selected` žymę, `CityHistory`, `StatsQueryResult` ir `ListingHistory`.
- `storage` katalogas yra projektų išvestyje; pasirūpinkite, kad aplankas būtų rašomas ir nesaugomas kitur.
- Žurnalai (`logs/logo - ...txt`) padeda sekti vykdymus ir klaidas.

## Naudojimo pavyzdys
1. Paspauskite „Aruodas.lt“ – `BrowserSurface` prisijungs prie svetainės ir rodys naršyklės vaizdą.
2. Vykdant paiešką, sistema blokuoja UI (blur overlay) ir laukia baigties; baigus pasirodomas duomenų suvestinė (nauji/praleisti/klaidos).
3. Skelbimų istoriją rasite statistikos lange: paspauskite „Statistika“, papildykite filtrus, „Rodyti naujausius/pabrangusius/atpigusius“, „Istorija“ ar „Grafikas“.
4. Priklausomi filtrai (miestas → objektas → adresas → kambariai) ir watermark rodo galimus min/max rubus.

## Papildoma informacija
- `chapters.md` seka projekto funkcijų eiliškumą nuo pradinės sąsajos iki statistikos filtrų (darbuose iki 38 skyriaus).
- `RpaAruodas/Services` kataloge yra atskiros paslaugos: konfigūracija, žurnalas, Playwright, automatika ir DB užklausos.
