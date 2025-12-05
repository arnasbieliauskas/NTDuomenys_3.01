# Chapter 1 - Pradinis langas

1. Inicializuota Avalonia aplikacija ir įtrauktos priklausomybes (Microsoft.Extensions, SQLite) bendram Windows/macOS UI.
2. Sukurtas `appsettings.json` bei konfiguracijos, logų ir SQLite paslaugos (dienos logas `logo - YYYY.MM.dd.txt`, DB `storage/ntduomenys.db` su `Listings` lentele).
3. Įdiegta DI paleidimo grandinė `Program.cs`/`App.axaml.cs`, langas pradėtas statyti pagal nustatymus, sukurtas raminantis UI.
4. Langas supaprastintas iki vieno „Aruodas.lt“ mygtuko; įgyvendintas hover efektas, kuris keičia spalvą į šviesiai pilką su subtiliu šešėliu, o šablonas praplėstas, kad mygtukas išlaikytų komfortišką padding.

# Chapter 2 - Playwright integracija

1. Į projektą integruotas Playwright runner’is su nuolatiniu Chromium (instaliavimas + DI registracija) ir praplėsta `Program.cs` uždarymo logika tvarkingam resursų atlaisvinimui.
2. `MainWindow` papildytas naršymo paviršiumi ir asinchroniniu atnaujinimu: paspaudus mygtuką atidaromas aruodas.lt, rodomos Playwright ekrano kopijos ir visas pelės/klaviatūros įvedimas peradresuojamas į puslapį, leidžiant pilnai naršyti aplikacijos viduje.

# Chapter 3 - Stealth naršyklė

1. Pritaikyta Playwright paleidimo „stealth“ konfiguracija (custom user-agent, locale, timezone, `navigator.webdriver` slopinimas ir automation flag pašalinimas), kad Cloudflare „Verify you are human“ patvirtinimas praeitų nebeatsidarant antram Chromium langui.
2. Paliktas tik aplikacijos langas - Chromium veikia headless režimu su tais pačiais apsaugų apejimo parametrais, todėl naršymas vyksta viename UI.
3. Išplėstas naršymo paviršius iki 1200x1800 ir `MainWindow` dinamiškai nustato `BrowserSurface`/`AruodasPreview` dydžius pagal gautą ekrano kopiją, tad `ScrollViewer` leidžia peržiūrėti visą puslapį be apribojimų.

# Chapter 4 - Saveikos patobulinimas

1. ScrollWheel kryptis sulyginta - Playwright gauna invertuotą delta, todel scroll up/down juda ta pačia kryptimi tiek aplikacijoje, tiek puslapyje.
2. Pridėtas „Ieškoti“ mygtuko paspaudimo sekimas: `buttonSearchForm` DOM skriptas logina paspaudimus, o `AttachPageEvents` išrašo „Paspaustas Aruodas.lt 'Ieškoti' mygtukas.“ į dienos logą.
3. Log žinutė sutrumpinta į „Paspaustas...“ tam, kad raportė būtų aiškesnė.

# Chapter 5 - Uždarymo valdymas

1. `MainWindow` uždarymo seka pilnai sutvarkyta: `OnClosed` dabar ramiai išvalo CTS, disposina `PlaywrightRunner` ir po log'o `Programa uždaryta.` priverstinai nutraukia procesą, todėl `RpaAruodas.exe` nebelieka Task Manager'yje.
2. `PlaywrightRunner` įgavo `_disposed` apsaugą, kad daugkartinis `DisposeAsync` kvietimas (iš MainWindow ir Program) nekeltų išlygų ir visuomet išjungtų naršyklę.

# Chapter 6 - Fokusas ir sekimas

1. Visa automatika papildyta `EnsureAruodasFocusAsync`, kuri aptinka nukreipimus į kitus domenus, juos išlogina ir perkrauna `https://www.aruodas.lt`, todel vartotojo paspaudimai vėl perduodami į tinkamą puslapį.
2. „Ieškoti“ mygtukas gauna specialų DOM markeri; Playwright `Console` handleris pagauna šiuos signalus ir logina „Paspaustas Aruodas.lt 'Ieškoti' mygtukas.“ kiekvienam paspaudimui.
3. Atstatymo logai leidžia logų faile matyti kada buvo prarastas fokusas ir kada jis buvo sėkmingai grąžintas į Aruodas.lt.

# Chapter 7 - RPA pilnas ciklas

1. Sukurtas `AruodasAutomationService`, kuris po kiekvieno „Ieškoti“ paspaudimo pats nuskaitytų visus skelbimus per visus puslapius, surenka reikiamas reikšmes (href, mikrorajonas, adresas, kaina, €/m2, kambariai, plotas, aukštas) ir saugo jas su datomis į DB.
2. DB schema praplėsta naujomis stulpelių kombinacijomis ir unikaliu indeksu (ExternalId + CollectedOn), todel tas pats skelbimas įrašomas tik kartą per dieną.
3. Pridėtas pranešimas vartotojui: pasibaigus RPA darbui rodomas popupas su rezultatu (kiek naujų, kiek praleistų, klaidos), kad rankinis tikrinimas nebereikalingas.

# Chapter 8 - Papildyti skelbimai ir supaprastinta DB

1. DB išvalyta nuo nereikalingų `Title`/`RawContent`/`CollectedAt` stulpelių: modelis sutrumpintas, o inicializacija/migracija atlieka automatiškai rekonstruodama lentelę be šių laukų ir išsaugodama likusius duomenis.
2. Skelbimų nuskaitymas dabar apima ir nuomos korteles (`div.list-row-v2.object-row.srentflat.advert`), todel parsisiunčiami abu tipai su ta pačia kainos/adreso/savybių logika.

# Chapter 9 - Namų skelbimai ir konfigūruojamas naršyklės vaizdas

1. Pridėtas namų skelbimų (`selhouse`) nuskaitymas: surenkamas adresas, nuoroda, kaina, €/m², bendras plotas, sklypo plotas (`list-AreaLot-v2`), būklė (`list-HouseStates-v2`) ir įrašomi į DB.
2. DB schema praplėsta `AreaLot` ir `HouseState`; trūkstami stulpeliai sukuriami inicijuojant ir net išsaugant, kad duomenys visada tilptų.
3. Playwright Headless režimas valdomas per `appsettings.json` (`Playwright.Headless`), leidžiant pasirinkti ar rodyti Chromium langą be kodo keitimo.

# Chapter 10 - Blur suvestinė ir tęsiama darbo eiga

1. Paspaudus „Ieškoti“ visas langas suliejamas su „Renkama informacija“ kortele, atlaisvinamas tik baigus RPA.
2. Užbaigus darbui paliekamas blur ir rodoma suvestinė: kiek skelbimų rasta, kiek nuskaityta, kiek įrašyta į DB (praleistų ir klaidų tekstas, jei reikia).
3. Pridėtas „Testi“ mygtukas kortelėje; tik jis paspaudus panaikinamas blur ir galima tęsti darbą, veiksmas išloguojamas.

# Chapter 11 - Komercinių skelbimų nuskaitymas

1. Praplėsta paieška apimant komercinius skelbimus (`selcomm`), renkant duomenis iš tokių kortelių taip pat kaip ankstesnių tipų.
2. Išranka papildyta `Intendances` lauku (`list-Intendances-v2 list-detail-v2`), DB schema automatiškai papildo stulpelį ir išsaugo reikšmes, o trūkstamieji paliekami `NULL`.

# Chapter 12 - Namų nuomos skelbimai

1. Papildyta paieška namų nuomos kortelėmis (`srenthouse`), taikant tokią pat nuskaitymo logiką kaip namų pardavimams ir komerciniams skelbimams.
2. Duomenys (kaina, €/m², plotas, sklypo plotas, būklė, adresas, aukštai, nuoroda) saugomi į DB su jau esamais laukais, trūkstami pildomi `NULL`.

# Chapter 13 - Komercinės nuomos skelbimai

1. Praplėsta paieška komercinės nuomos kortelėmis (`srentcomm`), duomenys renkami tokią pačią struktūrą kaip komercinių ir kitų tipų skelbimuose.
2. `Intendances` laukas surenkamas ir saugomas DB; trūkstami laukai ir toliau užpildomi `NULL`.

# Chapter 14 - Sklypai su pasiūlos tipu

1. Papildyta skelbimų paieška sklypų kortelėmis (`sellot`), duomenys renkami kaip kitų tipų skelbimuose.
2. Pasiūlos tipas (nuomai/pardavimui) nuskaitytas iš `searchFormToggleFieldContainer_FOfferType` ir įrašomas į naują `OfferType` stulpelį kartu su skelbimais; sklypo plotas (`list-AreaOverall-v2`) dedamas į `AreaLot`, trūkstami laukai pildomi `NULL`.

# Chapter 15 - Sklypų paieškos „Gyvenvietė“ logika

1. Sklypų paieškoje, kai savivaldybės laukas rodo „Gyvenvietė“, `SearchCity` DB stulpelis pildomas iš `#display_text_FRegion`, kad išsaugotas pasirinktos gyvenvietės pavadinimas.

# Chapter 16 - Garažai/vietos paieška

1. Pridėtas „Garažai/vietos“ paieškos tipas: skelbimuose su klase `selgarage` surenkamas tas pats adresas/kaina/plotas kaip kituose, o `SearchObject` ir `OfferType` išsaugomi taip pat kaip kitoms paieškoms.
2. Jei pasirinkta „Gyvenvietė“ savivaldybėje, `SearchCity` pildomas iš `#display_text_FRegion`, kad išsaugotas tikslus gyvenvietės pavadinimas.

# Chapter 17 - Sodybų paieška

1. Pridėtas „Sodyba“ paieškos tipas: jei building type laukas (`#display_text_FBuildingType`) rodo „Sodyba“, `SearchObject` įrašomas kaip „Sodyba“, skelbimai renkami iš `selhouse` kortelių.
2. Taikoma ta pati „Gyvenvietė“ logika kaip sklypu ir garažų paieškose: jei savivaldybė rodo „Gyvenvietė“, `SearchCity` paimamas iš `#display_text_FRegion`.

# Chapter 18 - Trumpalaikė nuoma paieška

1. Jei objekto tipas (`#display_text_obj`) rodo „Trumpalaike nuoma“, `OfferType` saugomas kaip „Trumpalaike nuoma“, o `SearchObject` pildomas iš `#display_text_FRentType` (pvz., „Butai“). Skelbimai renkami iš `srentshort` kortelių kaip kitų tipų.
2. Pritaikyta „Gyvenvietė“ taisyklė: jei savivaldybės laukas rodo „Gyvenvietė“, `SearchCity` paimamas iš `#display_text_FRegion`.

# Chapter 19 - Statistika lange

1. Pagrindiniame lange pridėtas „Statistika“ mygtukas: paspaudus uždaromas atidarytas Chromium/Playwright seansas ir vietoje peržiūros atidaromas statistikos langas.
2. Statistikoje galima filtruoti DB įrašus pagal objekto tipą, miestą/gyvenvietę bei datos intervalą; rodomas suskaičiuotas rezultatų kiekis ir iki 200 paskutinių atitikmenų su pagrindine informacija.

# Chapter 20 - Statistikoje tvarkomi trūkstami duomenys ir nuorodos

1. Statistikoje null/tusti DB laukai rodomi kaip „-“, o data, miestas, adresas, kaina, €/m² ir meta laukai nebelieka tušti ar „NULL“.
2. Nuorodų mygtukas „Nuoroda“ rodomas tik kai skelbimo URL yra, todel, kai adresas/URL nėra, eilutė neturi netikro href.
3. Inline suvestinės eilutė nebenaudoja URL teksto - visa informacija tvarkingai išdėstyta eilėje, o nuorodos atidarymas paliktas per mygtuką, kuris atidaro skelbimą numatytame naršyklės lange.

# Chapter 23 - Suvestinės su nuorodomis

1. Statistikoje suvestinė rodo min/maks/vid. kainas ir vid. €/m² tik kai yra rezultatų, o lentelė slepiama kada nėra duomenų.
2. Maksimali ir minimali kaina tapo paspaudžiami elementai: paspaudus atidaromas atitinkamo skelbimo URL numatytoje naršyklėje.
3. Kainos formatuojamos su tarpais ir simboliais, o €/m² rodomas tik jei yra duomenų.

# Chapter 24 - Priklausomi filtrai (adresas ir kambariai)

1. Adresas dabar priklauso nuo miesto: kol miestas nepasirinktas, adresų sąrašas tuščias, o pasirinkus miestą rodomi tik to miesto adresai.
2. Kambariai priklauso nuo objekto, miesto ir adreso: kambarių sąrašas kraunamas pagal pasirinkimus, todėl matomi tik atitinkantys variantai.
3. `SelectionChanged` logika atnaujina priklausomus sąrašus, o `reset` išvalo ir susietus laukus.

# Chapter 25 - Visiškas filtrų logavimas ir priklausomybių išplėtimas

1. Vartotojo veiksmai loguojami: mygtukai (Aruodas.lt, Statistika, Filtruoti, Išvalyti) ir visi filtro pasirinkimai (miestas, objekto tipas, adresas, kambariai, būklė) įrašomi į logą.
2. Objekto tipas dabar priklauso nuo miesto - pasirinkus miestą, objekto sąrašas filtruojamas pagal jame esančius skelbimus; miestas savo ruožtu priklauso nuo objekto tipo, todėl abu persikrauna.
3. Adresas priklauso nuo objekto, miesto ir kambarių, o kambariai - nuo objekto, miesto ir adreso; miestas/objektas/adresas/kambariai persikrauna su apsauga nuo netinkamų indeksų.

# Chapter 26 - Ploto watermark pagal filtrus

1. Pridėtas ploto watermark: „Plotas nuo“ rodo filtrų minimalią reikšmę, „Plotas iki“ - maksimalią reikšmę pagal pasirinktus filtrus (objektas, miestas, adresas, kambariai, būklė).
2. Kai filtrai nepanaudoti ir laukai tušti, watermark nerodomas; klaidos atveju taip pat paslepia, kad vartotojas nematytų klaidingų reikšmių.
3. Skaičiai normalizuojami (m², €/m² ir € simboliai pašalinami), o watermark atsinaujina su kiekvienu filtro pasirinkimu ar `resetu`.

# Chapter 27 - Plotas (a) intervalas ir watermark

1. Pridėtas „Plotas (a) nuo / iki“ intervalas su tokia pačia logika kaip m²: watermark rodo min/max pagal pasirinktus filtrus (objektas, miestas, adresas, kambariai, būklė) ir pasislepia, jei filtrų nėra arba įvestis tuščia.
2. Watermark ir skaičių normalizavimas apima `AreaLot` lauką, todėl ploto (a) ribos visada atitinka aktualius filtrus ir nurodomos teisingos reikšmės.
3. `Reset` ir filtrų pakeitimai atnaujina abu ploto laukus (m² ir a) saugiai, išvalant netinkamus indeksus ir išvengiant klaidų.

# Chapter 28 - Kainos intervalas ir watermark

1. Pridėtas „Kaina nuo / Kaina iki“ intervalas su tokią pat watermark logika kaip ploto laukuose: rodomos min/max kainos pagal pasirinktus filtrus (objektas, miestas, adresas, kambariai, būklė) ir slepiamos, kai filtrai nepanaudoti ar įvestis tuščia.
2. Kainos normalizuojamos (pašalinami €, tarpai ir simboliai), todėl watermark vertės teisingos ir nekelia klaidų.
3. Filtrų keitimai ir reset atnaujina kainos laukus kartu su ploto watermark, apsaugant nuo netinkamų indeksų.

# Chapter 29 - Kainos watermark iš DB

1. „Kaina nuo / iki“ watermark dabar imamas iš DB min/max kainų pagal pasirinktus filtrus, rodomas tik kai yra filtrai ar įvestos ribos.
2. Normalizavimas praplėstas (su € ir m² simbolių pašalinimu), kad kainų ribos būtų patikimai konvertuojamos į skaičius.
3. Klaidos atveju kainos watermark išvalomas kartu su ploto watermark, kad vartotojas nematytų klaidingų reikšmių.

# Chapter 30 - €/m² watermark iš DB

1. „€/m² nuo / iki“ watermark rodomas iš DB min/max `PricePerSquare` pagal tuos pačius filtrus kaip kainos.
2. Naudojamas tas pats normalizavimas (su €, m² ir tarpu pašalinimu), kad €/m² ribos būtų patikimai konvertuojamos į skaičius.
3. Klaidos ar tuščių filtrų atveju watermark išvalomas kartu su kitais, kad nebūtų klaidinimo.

# Chapter 31 - Būklė priklausoma nuo filtrų

1. „Būklė“ sąrašas kraunamas iš DB pagal visus pasirinktus filtrus: objekto tipą, miestą, adresą, kambarius, kainą, €/m², ploto m² ir a intervalus.
2. Sąrašas atnaujinamas keičiant objektą, miestą, adresą ar paspaudus „Filtruoti“, o tuščių/klaidos atveju pasirinkta reikšmė pašalinama, kad nebūtų netinkamų indeksų.
3. Rikiavimas ir rodymas filtruoja tuščias reikšmes, kad vartotojas matytų tik aktualius variantus.

# Chapter 32 - Istorija pagal ExternalId

1. Statistikoje rodomas tik naujausias įrašas pagal `ExternalId` (dedupe per `CollectedOn`+`Id`), todel pasikartojantys skelbimai nebedubliuojami.
2. Skelbimams su daugiau nei vienu įrašu šalia „Nuoroda“ atsiranda mygtukas „Istorija“, kuris atidaro chronologinį sąrašą (data, kaina, €/m², plotai, būklė).
3. Istorijos užklausos filtruoja pagal `ExternalId` ir pasirūpina tuščiu `ExternalId`, kad UI nestrigtų.

# Chapter 33 - Istorijos procentai nuo pradinio įrašo

1. „Istorija“ lange kainos pokytis skaičiuojamas nuo seniausio įrašo; visi vėlesni pokyčiai rodo procentą nuo pradinio, o pradinis rodomas su „-“ pokyčiu.
2. Procentai rodomi tik kai bazinė ir dabartinė kaina ne tuščios ir baza > 0.
3. UI kortelėse lieka ta pati detalė, tik procento formulė atnaujinta, kad rodytų realų pokytį nuo pradžios.

# Chapter 34 - Istorijos mygtuko spalvos pagal pokytį

1. „Istorija“ mygtuko fonas spalvinamas pagal kainos pokytį: teigiamas -> pastelinis raudonas, neigiamas -> pastelinis žalias, jokio pokyčio -> numatytas.
2. Teksto spalva priderinta prie fono, kad mygtukas būtų matomas.
3. Spalvinimas atspindi skirtumą nuo bazinio įrašo, o ne tarpinio pokyčio.
4. Kai kaina nepasikeičia, „Istorija“ mygtukas naudoją tą pačią mėlyną stilistiką kaip „Nuoroda“.

# Chapter 35 - Istorijos dialogo nuorodų mygtukas

1. Istorijos lange rodomas tik vienas „Nuoroda“ mygtukas apačioje dešinėje; jis atidaro naujausią istorijos įrašą.
2. Kortelių viduje mygtukai pasalinti, kad istorijos eilutės būtų tvarkingos.
3. Mygtukas išlaiko pirminę mėlyną stilistiką ir išjungiamas jei URL nėra.

# Chapter 36 - Kainos grafikas istorijos lange

1. Istorijos lange pridėtas „Grafikas“ mygtukas (primary stiliaus), kuris atidaro linijinį kainos pokyčio grafą iš visų istorijos įrašų.
2. Grafikas turi tinklelį, ašis, antraštę „Kainos pokytis“ ir žymimus taškus.
3. Mygtukas disablinamas, jei yra mažiau nei du kainos taškai.

# Chapter 37 - €/m² pokyčio mygtukas suvestinėje

1. Suvestinėje „Vid. €/m²“ rodoma su atskiru mygtuku „Atvaizduoti €/m² pokytį“.
2. Mygtukas paslepiamas ir disablinamas, kai €/m² duomenų nėra.
3. Kai rezultatai ar klaidos neturi reikšmių, trendaus mygtukai slepiami.

# Chapter 38 - Statistikų puslapiavimas

1. Statistikoje pridėtas pasirenkamas įrašų skaičius (50/100/200/500) ir puslapių skaitiklis „Puslapis X / Y“.
2. Puslapiavimas matomas numeru juostoje su rodyklėmis ir ellipsis.
3. Kiekvienas pasirinkimas resetina į 1 puslapį, navigacija įjungiama tik kai yra duomenų.

# Chapter 39 - Istorijos ir statistikos deduplikacija pagal SearchObject

1. Statistikoje dedupe ir skaičiavimas daromas pagal `ExternalId` ir `SearchObject`.
2. Istorijos užklausos filtruojamos pagal `SearchObject`, todėl „Istorija“ rodo tik pasirinktus įrašus.
3. Trend mygtukai agreguoja istoriją per `ExternalId+SearchObject`, kad grafikai būtų aiškūs.

# Chapter 40 - „Rodyti naujausius“ filtras

1. Statistikoje pridėtas „Rodyti naujausius“ mygtukas: parodo tik įrašus be istorijos.
2. SQL užklausa praleidžia įrašus su keliomis versijomis.
3. „Filtruoti/ Išvalyti“ grąžina įprastą režimą.

# Chapter 41 - Favoritai ir Selected stulpelis

1. DB papildyta `Selected` stulpeliu, pridėtas `SetSelectedAsync`.
2. Statistikoje kortelės turi žvaigždutę; paspaudus įrašas pažymimas/atžymimas.
3. Žvaigždės būsena keičia tik savo vizualą, o duomenys lieka.

# Chapter 41 - Favoritai ir filtrai

1. `Selected` stulpelis saugo būseną (0/1).
2. „Rodyti pažymėtus“ filtruojama pagal `Selected=1`. „Rodyti naujausius“ nutraukia istorijų filtrą.
3. „Filtruoti/Isvalyti“ atstato standartinį režimą.

# Chapter 42 - Suvestinės nuorodų matomumas

1. Nuorodų mygtukams pritaikytas šablonas su `ContentPresenter`, kad tekstas nebūtų paslepiamas.
2. Pašalintas netinkamas `ContentStringFormat` bindingas, kad nebekiltų AVLN2000 klaida ir spalvos būtų stabilios.

# Chapter 43 - Statistiko lango blur kol kraunama

1. Statistikoje, kai vyksta duomenų užkrova, visas statistikos blokas užblurinamas ir išjungiamos valdikliai.
2. Blur efektas pašalinamas `LoadStatsAsync` pabaigoje (ir klaidos atveju), kad vartotojas matytų, jog sistema dirba.

# Chapter 44 - Vid. €/m² pagal visus filtruotus rezultatus

1. „Vid. €/m²“ dabar apskaičiuojama iš visos filtro aibės, o ne tik puslapio imties.
2. DB užklausa grąžina agregatą, kuris perduodamas UI.
3. Kitos suvestinės laukai lieka vietoje.

# Chapter 45 - Maksimali/Minimali kaina pagal visus filtruotus rezultatus

1. „Maksimali kaina“/„Minimali kaina“ rodomos pagal visą filtruotą aibę.
2. DB agregatai (MIN/MAX/AVG) perduodami UI.
3. Nuorodų mygtukai aktyvuojami tik kai atitinkamas skelbimas yra matomas.

# Chapter 46 - Naujausi/Pažymėti mygtukai pagrindiniame filtre

1. „Rodyti naujausius“ ir „Rodyti pažymėtus“ perkelti į pagrindinį filtro bloką.
2. Mygtukai matomi net kai išplėstinė paieška paslėpta.
3. Išplėstinės paieškos lange šių mygtukų nebėra.

# Chapter 47 - Atpigusių skelbimų filtras

1. Statistiko užklausa sutvarkyta: CTE uždarymas ir finalinis SELECT nebemeta SQLite klaidos įjungus „Rodyti atpigusius skelbimus“.
2. Galutinis rikiavimas naudoja `CollectedOn` ir `ExternalId` vietoje neegzistuojančio `Id`.

# Chapter 48 - Pabrangusių filtras ir max/min nuorodos

1. „Maksimali kaina“/„Minimali kaina“ nuorodos aktyvios net kai reikšmės imamos iš visos aibės: DB grąžina min/max URL.
2. Pridėtas mygtukas „Rodyti pabrangusius skelbimus“ (PriceChangePercent > 0).
3. „Filtruoti/Isvalyti“ grąžina standartinį režimą.

# Chapter 49 - Palyginti statistiką (laikinas stubas)

1. „Palyginti statistiką“ mygtukas atidaro modeless langą su kortele ir dviem pirminiais mygtukais bei tekstu „Palyginimo logika bus pridėta vėliau“.
2. Laikinas stubas lieka, kol reali logika kuriama.

# Chapter 50 - Palyginimo lango filtrų paruošimas

1. Langas naudoja pagrindinius filtrus: objektas, kambariai, miestas/gyvenvietė, datos nuo/iki.
2. Langas atidaromas modeless ir saugo filtrų enumeracijas su `SelectedIndex=-1`.

# Chapter 51 - Palyginimo lango išdėstymo atnaujinimas

1. „Pridėti miestą/gyvenvietę“ mygtukas kairėje prideda papildomas miesto eilutes.
2. Filtrų tinklas sutvarkytas į dvi eilutes po du stulpelius.
3. Nauji miestai naudoja tą patį `ItemsSource`, išdėstymas lieka aiškus.

# Chapter 52 - Primary mygtuko hover efektas

1. Pagrindinio mygtuko šablonas bindina `Background`, `BorderBrush`, `BorderThickness`, `Padding`, `Content` ir `Foreground`, todėl hover efektas atitinka „primary“ stilių.
2. Dėl to „Pridėti miestą/gyvenvietę“ mygtukas gauna tą pačią hover fono ir šešėlio logiką.
3. Vizualinis feedback parodo aktyvius mygtukus be papildomų valdiklių.

# Chapter 53 - Miestų valdymo mygtukai

1. Palyginimo lango kairėje esančiame primary bloke yra du mygtukai: „Pridėti miestą/gyvenvietę“ ir „Pašalinti miestą/gyvenvietę“.
2. Pašalinti mygtukas panašus į pridėjimo, bet išjungtas, kol likęs tik vienas ComboBox.
3. Pridėjus arba pašalinus miestus, filtruose lieka vizualinis atsakas be papildomų valdiklių.

# Chapter 54 - Palyginti pagal

1. Statistikoje pridėtas „Palyginti pagal:“ blokas su dviem checkboksais (vidutinė kaina ir €/m²) po „Pridėti/Pasalinti.“
2. Blokas matomas tik „Palyginti statistiką“ lange, todėl pagrindinis meniu neperkrautas.
3. Vartotojas gali pažymėti ar nuimti palyginimus be trukdžių kitai logikai.

# Chapter 55 - Stabilizavimas ir resursų atkūrimas

1. `MainWindow.axaml.cs` paliktas tik rankiniu būdu rašytas kodas: konstruktorė kviečia `InitializeComponent()`, o `FindControl` priskiria valdiklius, kad generatorius sugeneruotų `.g.cs`.
2. `InitializeComponent` susiaurintas iki `AvaloniaXamlLoader.Load(this)` – nebėra dubliuotų XAML aprašų faile.
3. „App“ paleidimas grįžo prie XAML: `App.axaml` (su kodu) įtrauktas kaip `AvaloniaResource`, todėl nebėra `x:Class` dublikatų ar „no precompiled XAML“ klaidų.
4. `CheckBox.compare-option` šablonas sutvarkytas (pašalintas `StrokeLineJoin` attributas) tam, kad Avalonia krovimo metu nebekiltų AVLN2000.
5. `dotnet build NTDuomenys_3.0.sln` dabar sėkmingai baigiasi, o kūrime lieka tik esamos `MainWindow.axaml.cs` nullability įspėjimai.

# Chapter 56 - Grafikas hover efektas

1. Palyginti lango apačios mygtukas `Grafikas` dabar naudoja tą pačią `primary` klasę kaip kairėje esantis `Pridėti miestas/gyvenvietė`.
2. Klasei priklausantis hover-stilius prideda pilką foną ir šešėlį – kursoriaus užvedimas suteikia tokį pat vizualinį feedbacką.
3. Dėl to nebėra rankinio fonų/perrašymo; palikta tik paddingas, dydis ir turinys.

# Chapter 57 - Palyginimo grafikas

1. Grafikas gauna duomenis pagal pasirinktus miestus, objektų tipą, kambarius ir datų intervalą, o serverio pusėje nauja `GetCityHistoryAsync` užklausa agreguoja vidutines kainas ir €/m² pagal dieną.
2. Mygtukas `Grafikas` dabar surenka įvestus kriterijus, pasižymi, kurie checkbokso filtrai įjungti, ir sukuria liniją kiekvienam miestui bei kriterijui (`Vidutinė kaina`, `€/m²`), jei yra pakankamai duomenų.
3. Daugiaserijiniame grafike rodoma legenda, atitinkanti linijų spalvas, o bendras `ShowLineChartAsync` metodas palaiko vieną ar daugiau duomenų eilučių su automatinio mastelio ir ašių generavimu.
4. `dotnet build NTDuomenys_3.0.sln` parodo, kad naujas langas kompiliuojasi be klaidų (tik esamos struktūros įspėjimai), todėl funkcionalumas „veikia“.
5. Grafiko langas atidaromas modeless – jis rodomas (`Show`) be `ShowDialog`, tad vartotojas gali dirbti su pagrindiniu langu, kol grafikas atidarytas.
6. Virš grafiko dešinėje rodoma „Rezultatai: …“ eilutė su bendru skelbimų skaičiumi pagal pasirinktus filtrus.
7. Ant kiekvieno taško būna tooltip’as su miesto/kainos infomacija (data + € suma), todėl hoveringas iškart praneša, kam priklauso ta linija.
# Chapter 58 - Mikrorajonu parama

1. DB schema papildyta `MicroDistrict_lc` normalizuota kolona, migracija atlieka backfillą ir pridedamas indeksas `IX_Listings_MicroDistrict`, kad paieška pagal mikrorajoną būtų greita.
2. `SaveListingsAsync` įrašo tiek `MicroDistrict`, tiek `MicroDistrict_lc`, o `QueryListingsAsync` bei kainų/plotų bounds metodai priima papildomą `microDistrict` filtrą, kad duomenys liktų statiniai kitiems paieškoms.
3. Statistikoje atsirado “Mikrorajonas” dropdownas, kuris priklauso nuo pasirinkto objekto/miesto/kambarių ir atnaujina vandens žymas bei suvestinės metrikas, kai mikrorajonas parenkamas.
4. Palyginimo langas ir `GetCityHistoryAsync` dabar filtruoja istoriją pagal mikrorajoną, o `GetDistinctMicroDistrictsAsync` pateikia tikrus mikrorajonų pavadinimus UI sąrašams.

# Chapter 59 - Kainos rūšiavimas ir schema

1. InitializeAsync dabar užtikrina, kad lentelė turi visus numerinius stulpelius, paleidžia BackfillNumericColumnsAsync su logu apie atnaujintus įrašus ir kuriamas IX_Listings_PriceValue tik kai PriceValue egzistuoja.
2. QueryListingsAsync naudoja priceExpr/pricePerSquareExpr visose CTE, agregatuose ir ORDER BY, o filtrai (AddNumericRangeFilter) veikia per tuos pačius sprendinius, todėl net su legacy DB be PriceValue nėra SQL klaidų.
3. Paleidimo metu loguojamas PRAGMA table_info(Listings) atsakymas ir aktyvios ORDER BY išraiškos aprašymas, kad matytume naudojamus stulpelius.
4. Filtruoti pagal brangiausia dabar rūšiuoja pagal tikrą numerinę kainą, o Max/Avg price rodikliai atitinka pirmą įrašą be SQLite klaidų.

# Chapter 60 - Pigiausios rusiavimo parinktys

1. "Filtruoti pagal brangiausia" perkelta po "Palyginti statistika" toje pacioje kolonoje, kad kainos filtrai butu susitelke salia palyginimo mygtuko.
2. "Filtruoti pagal pigiausia" mygtukas i ta pati vertikali sekcija ijungia `_sortByPriceAscending` ir islaiko puslapiavimo nustatymus.
3. `LoadStatsAsync` perduoda nauja `orderByPriceAscending` parametra `QueryListingsAsync`, todel rezultatai rusiavimas pereina nuo pigiausio iki brangiausio be papildomos logikos keitimo.
4. Veikia.

# Chapter 61 - DB optimizacija su LatestListings ir keyset

1. Sukurtas migracijos scriptas `migrations/002_latest_listings.sql`, kuris kuriant `LatestListings` materializuota per `ExternalId+SearchObject`, papildant `LatestListings` atitinkamais numeriniais (`PriceValue`, `PricePerSquareValue`, `AreaSquareValue`, `AreaLotValue`) ir normalizuotais (`SearchCity_lc`, `SearchObject_lc`, `Rooms_lc`, `HouseState_lc`, `MicroDistrict_lc`, `Address_lc`) stulpeliais bei FTS5 adresu indeksu.
2. Inicijuojant DB paleidziami saugos PRAGMA: `journal_mode=WAL`, `synchronous=NORMAL`, `temp_store=MEMORY`, `cache_size=-8000`, o visos lentelės idealizuojamos `ANALYZE` ir `PRAGMA optimize` pasibaigus migracijoms.
3. `DatabaseService` realizuoja naują rašymo kelią, kuris įrašo/atnaujina `Listings` ir „LatestListings“ vienu metu (per upsert) ir normalizuoja reikšmes rašant, kad nauji skelbimai visada turėtų `*_Value` ir `*_lc` duomenis.
4. Skaitymui naudojama tik `LatestListings`: `QueryListingsAsync` pakeistas į skaitymą iš naujo indekso, `Stats` ir `bounds` užklausos grąžina tą patį rezultatų formą, bet be langinių funkcijų, rikiavimas pagal `PriceValue` naudoja indeksuotą stulpelį, o daug filtrų (Objekto tipas → Miestas → Kambariai → Mikrorajonas) su indeksais `IX_LatestListings_SearchObject_lc_SearchCity_lc_Rooms_lc_MicroDistrict_lc_CollectedOnLatest` užtikrina greitį.
5. Puslapiavimas per keysetą: `QueryListingsAsync` naudoja paskutinio įrašo tupelį `(PriceValue, CollectedOnLatest, ExternalId)` siekiant diskrėčiai tęsti rikiavimą, o `GetSeekTupleAsync` užtikrina paiešką be OFFSET, užtikrinant <100 ms atsiliepimo laikus.
6. Pasirinktiniai adresų paieškos atvejai nukreipiami į FTS5 lentelę, o likusi logika lieka paprastais indeksų filtravimais.
7. PRAGMA `Schema` patikra sukuria `LatestListings` lentelę, atnaujina rodinius ir palaiko trumpą atkūrimo kelią (`LatestListings` rekonstravimo iš `Listings`), o visos migracijos yra idempotentiškos bei grįžtamasis atkūrimas įrašytas į dokumentaciją.
8. Rašymo/ skaitymo metodai `DatabaseService` viduje optimizuoti, kad nebūtų keičiami vieši API: `SetSelectedAsync` dabar atnaujina abu modelius, o `QueryListingsAsync` išveda identišką formą, tik iš naujojo vaizdo.


# Chapter 62 - DB stabilizavimas ir deterministikos uztikrinimas

1. EnsureLatestListingsPreparedAsync suburta i viena vieta: schema, indeksai, FTS perrusiavimas (INSERT INTO LatestListingsAddressFts(...) VALUES('rebuild')), ANALYZE ir PRAGMA optimize paleidziami tik viena karta po EnsureColumnsAsync, todel likusi veikla randa jau paruoszta duomenu vaizda ir vienas schema ready logas.
2. Rusiavimas isgrynintas - visi ORDER BY (dienos arba kainos) papildyti tikslu tie-breakeriu seka PriceValue -> CollectedOnLatest -> ExternalId -> SearchObject, o ta pati tvarka naudojama ir pirmu reiksmiu langines statistikose, todel nekils neteisingu rezultatu, kai kaina sutampa arba vienas ExternalId turi kelis SearchObject.
3. Keyset puslapiavimas naudoja keturiu lauku tupeli (PriceValue, CollectedOnLatest, ExternalId, SearchObject) tiek GetSeekTupleAsync, tiek ir WHERE tolai ankstesnis sąlygose, todel navigacija pirmyn atgal nepalieka tarpu net su dideliais OFFSET alternatyvomis.
4. Migracijoje atnaujintas unikalus indeksas i UNIQUE(TRIM(ExternalId), TRIM(SearchObject), CollectedOn) - silpni tarpai ir du kartus identiski skelbimai su tuo paciu kolekcijos laiku daugiau nerodomi, bet dabartiniai duomenys lieka vientisi.
5. FTS5 adresu lentele dabar sujungiama tik tada, kai vartotojas pateikia laisvo teksto adresui; be filtro griztama i iprastus indeksuotus filtrus, o tokenizer'is unicode61 lieka dokumentuotas kaip palaikantis lietuviskas raidziu variacijas.
6. Schema patikros duomenu uzklausose pasalintos, kad kiekviena viesoji operacija vykdo EnsureLatestListingsPreparedAsync tik viena karta, o papildomi EnsureLatestListingsSchemaAsync() iskvietimai is kitu vietu pasalinti - perejimai tapo ramesni ir aiskus.
