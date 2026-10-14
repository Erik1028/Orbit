# Orbit – SteamVR overlay (csukló-óra, 3 monitor, billentyűzet)

Saját, XSOverlay-szerű SteamVR overlay app. C# / .NET 8, Direct3D11 + Direct2D (Vortice), OpenVR,
Windows Graphics Capture. A felülete angol, ez a leírás magyar.

## Mit tud

- **Csukló-óra** (alapból bal kéz): idő + dátum, fps / reprojekció / GPU ms (a SteamVR compositorból),
  VRAM + GPU-terhelés (WDDM számlálók, mint a Feladatkezelőben), akkuk (HMD, L, R, trackerek közül a legkisebb).
  Akkor jelenik meg, ha a csuklódat magad felé fordítod és ránézel. 4 gomb: billentyűzet, monitorok
  ki/be, recenter, beállítások (a SteamVR dashboardon nyitja meg az Orbit fület).
- **Monitorok VR-ben**: mindegyik monitor külön overlay (nálad a 3 db 1920×1080), alapból ívben eléd
  rakva úgy, ahogy az asztalon állnak (bal Xiaomi, középen az AOC, jobb Xiaomi). Monitoronként: méret,
  átlátszóság, görbület, rögzítés (világ / bal kéz / jobb kéz / fej), egérvezérlés.
- **Billentyűzet**: teljes billentyűzet, a feliratokat a Windows aktív kiosztásából veszi (magyar QWERTZ,
  AltGr-jelek kék színnel). Shift/Ctrl/Alt/AltGr/Win: 1 kattintás = csak a következő billentyűre,
  2 = rögzítve, 3 = ki. Nyomva tartva ismétel (backspace, nyilak).
- **Beállítások**: ugyanaz a felület asztali ablakban (tálcaikon), VR-ben **lebegő Orbit-panelként** (az óra
  fogaskerék-gombja nyitja; lézerrel kezelhető, grippel mozgatható, X-szel zárható, mindig a monitorok
  előtt rajzolódik) és a SteamVR dashboard „Orbit” fülén is.
- **Lézer-simítás** (General → Smoothing: Off / Low / Med / High, alap Med): adaptív One Euro szűrő a
  lézer irányán – nyugodt kéznél erősen simít, gyors mozdulatnál alig késik. Mérve (szimulált remegés,
  1,07 m-es monitor 1,2 m-ről): kikapcsolva ~5 px rezgés, Med ~0,9 px, ~30 ms késés 1 rad/s-nál.
  Kattintáskor a kurzor 0,3 s-ig áll (amíg 0,9°-nál jobban el nem mozdítod), így a ravasz meghúzása
  nem csúsztatja el a klikket.
- **Billentyűhang** (Keyboard fül): egyetlen rövid klikk leütésenként (mint a Virtual Desktop billentyűzetén),
  Click / Muted, hangerő, „Test sound”. XAudio2-vel szól (kis késés), a hangeszközt háttérszálon nyitja.
- **Billentyűzet felső sáv**: Select all / Copy / Paste / Cut / Undo / Redo (Ctrl+A/C/V/X/Z/Y, virtuális
  billentyűkóddal, így QWERTZ-n is a Ctrl+Z a visszavonás). **Chatbox mód**: a gépelés egy saját sorba
  megy (max. 144 karakter, ékezetekkel), Enter / Send → OSC `/chatbox/input` a VRChatnek, közben
  `/chatbox/typing`; Esc törli.
- **Nyelvváltó gomb** (földgömb a szóköz mellett): a Windowsban telepített kiosztások között vált (a
  Windowst is átváltja), és ha az angol nincs telepítve, egy beépített angol (US) kiosztás is van – ez
  a betűket közvetlenül karakterként küldi, a Ctrl/Alt/Win + betű gyorsbillentyűket az angol betű
  virtuális kódjával. A választást megjegyzi (Keyboard.Language).
- **Zene a csuklón**: a Windows médiavezérlőjéből (SMTC) cím, előadó, borító, és előző / lejátszás-szünet /
  következő gombok (Wrist → Now playing).
- **Értesítések**: XSOverlay-formátumú UDP-üzenetek a 42069-es porton (VRCX: Beállítások → Értesítések →
  XSOverlay), a látótér alsó részén kártyaként, max. 3 egymás alatt, halk csengővel; General →
  Notifications (Test gomb). Ha az XSOverlay fut, az foglalja a portot.
- **Animációk**: a panelek be-/kiúsznak (áttűnés + enyhe méretezés), a kapcsolók gombja csúszik, a
  kijelölések csúsznak, a gombok és billentyűk felvillannak / benyomódnak.
- **Gyors ki/be (OVR Toolkit-stílus)**: mindkét grip egyszerre, kétszer gyorsan → a monitorok és a
  billentyűzet eltűnnek; ugyanez újra → visszajön, ami látszott. (General → Double-tap both grips.)
- **Egybe pattintott monitorok**: első induláskor a monitorok egyetlen íves képernyővé állnak össze
  előtted (hézag nélkül). Desktops → *Join into one curved screen* újra összerakja, *Separate* szétszedi.
  Egy monitort egy másik mellé ejtve az automatikusan a sorba pattan; B/Y-t nyomva tartva megfogáskor
  kiveheted a sorból. A sort bármelyik tagjánál fogva egyben viszed.
- **Hajlítás**: Curvature csúszka (kiírja a hajlítási sugarat), *Curve around me* = pont a fejed köré
  hajlít, VR-ben megfogás közben a kar jobbra/balra hajlít. Egy sor mindig közös sugáron marad.
- **Színezés (Look fül)**: 6 kész téma egy kattintással (Classic, Ocean, Sakura, Forest, Ember, Mono),
  kiemelőszín a 10 alapszínből vagy **bármilyen árnyalat** (Custom colour csúszka, szivárvány-sáv alatta),
  panelszín (Charcoal/Midnight/Plum/Forest/Mocha/Black), panel-átlátszóság.
- **Windows 95 stílus** (Look → *Style*): a beállítások, az óra, a billentyűzet és az értesítések igazi
  1995-ös Windowsként – „Orbit Properties” tulajdonságlap, Date/Time ablak a csuklón, MS Sans Serif,
  3D keretek, lenyíló listák –, eredeti pixelméretben rajzolva és simítás nélkül nagyítva. A Modern a régi marad.
- **Lézer** (Look fül): szín, **vastagság** (1,5–8 mm), és hogy **mindig látszódjon** (1 m-es sugár akkor
  is, ha nem mutatsz panelre) vagy csak panelre mutatáskor.
- **Számlapok** (Wrist fül → Watch face): Full (minden), Analog (mutatós óra + akkuk + teljesítmény),
  Minimal (nagy idő + gombok), Time (csak idő, kis kártya), **Custom** (widgetek egy 6×4-es rácson, lásd 1.5.0).
  12/24 órás óra is itt.
  Performance: Numbers (számok) vagy **Graph**: az utolsó 5 mp képkockaideje oszlopokban
  (sárga = reprojektált, piros = eldobott képkocka, vonal = a frissítési keret), a SteamVR `GetFrameTiming`-ből.
- **Egyes ablakok tükrözése** (Windows fül): Add a window → az ablaklistából (mint az Alt+Tab) kiválasztod,
  és külön panelként lebeg VR-ben (max. 6). Követi az ablakot, ha a PC-n mozog vagy átméreteződik,
  bezárás/újraindítás után program + ablakosztály (és utolsó cím) alapján magától újra rácsatlakozik.
  Kattintás előtt a PC-n előtérbe hozza az ablakot, hogy a kattintás biztosan bele menjen
  (a kis-méretre tett ablakot az első kattintás visszaállítja). Méret, átlátszóság, hajlítás, rögzítés
  (világ/kéz/fej), egér ki/be, *Stop mirroring*. A monitorokkal együtt kapcsol (Desktops gomb, dupla grip).
- **Okos képfrissítés** (Desktops → Smart frame rate, alapból be): teljes fps csak arra a monitorra /
  ablakra, amit nézel vagy amire mutatsz; látótér szélén 15 fps, mögötted 4 fps.
- **Szünet levett headsetnél** (General, alapból be): ha a headset 3 mp-ig nincs a fejeden
  (közelségérzékelő), nincs capture és nincs lézer, amíg vissza nem veszed.
- **Lekerekített VR-beállításpanel**: a lebegő beállításpanel sarkai kerekek, vékony világos szegéllyel.
- **Magától feljövő billentyűzet** (Keyboard → *Pop up for text fields*, alapból be): ha egy VR-ből érkező
  kattintás szövegmezőbe teszi a fókuszt, a billentyűzet feljön; ha a fókusz nem-szöveges elemre kerül,
  0,8 mp múlva eltűnik (csak ha magától jött fel, Chatbox módban és gépelés közben nem).
  Felismerés: WinEvent-hook (EVENT_OBJECT_FOCUS) külön szálon; Win32 Edit/RichEdit ablakosztályból, minden
  más (böngésző, Discord, Windows-appok) az akadálymentességi szerepből („editable text”, nem csak olvasható).
  Ez utóbbit csak VR-kattintás után 1,5 mp-ig kérdezi (a Chromium ettől bekapcsolja az a11y-fáját, ami lassít).
- **Éjszakai mód** (csuklón a hold gomb, vagy Look → *Night mode*): minden Orbit-panelt (monitorok, ablakok,
  billentyűzet, óra, beállítások, értesítések, lézer) tompít, áttűnéssel; *Night brightness* 15–90 %.
  A SteamVR overlay-színével (SetOverlayColor) szoroz, a megfogás-színezés megmarad.
- **Elrendezés-profilok** (új *Layouts* fül + a csuklón a profil nevét mutató gomb): Desk / Bed / Couch.
  Minden profil megjegyzi a monitorok, ablakok és a billentyűzet helyét, méretét, hajlítását, és az
  éjszakai módot. Váltáskor a régi profilba elmenti az aktuálisat, az újat visszarakja. Első használatkor
  minden oda jön, ahova nézel (ágyban fekve fölém). A csuklógomb a „On the watch” profilok között lép
  (alapból Desk és Bed).
- **Nyelvgomb javítva**: a billentyűzet nyelvgombja a billentyűzet-*kiosztást* írja ki (HKL felső szó), nem a
  beviteli nyelvet – nálad az angol nyelv + magyar kiosztás eddig „EN” volt, most „HU”.
- **DXOverlay / VRChat avatar-paraméterek (OSC)**: az XSOverlay-jel azonos nevű paramétereket küldi a
  VRChatnek (127.0.0.1:9000): `isOverlayOpen`, `isKeyboardOpen`, `isWristVisible`, `openOverlayCount`,
  `leftControllerBattery`, `rightControllerBattery`, `averageControllerBattery`, `averageTrackerBattery`.
  A DXOverlay ezekből dolgozik, így az Orbittal is működnie kell (VRChatben az OSC-t be kell kapcsolni).

## Kezelés VR-ben

| Mozdulat | Hatás |
|---|---|
| rámutatsz egy panelre | megjelenik a sugár; monitoron az igazi egérkurzor odamegy |
| ravasz (trigger) | kattintás (monitoron bal klikk, billentyűzeten gombnyomás) |
| B / Y | jobb klikk a monitoron |
| kar (stick) fel-le | görgetés |
| markolat (grip) rámutatva | megfogod és viszed a panelt |
| fogás közben kar fel-le | távolabb–közelebb |
| fogás közben kar jobbra-balra | monitor: hajlítás; óra/billentyűzet: méret |
| a másik kéz gripje ugyanazon a panelen | kétkezes átméretezés (széthúzás = nagyobb) |
| B/Y nyomva + grip egy soron lévő monitoron | kiveszed a sorból |
| mindkét grip kétszer | monitorok + billentyűzet ki/be |
| a másik kézzel megfogod az órát | áthelyezed a csuklón (elmenti) |

**Recenter fekve is**: ha egyenesen előre nézel (±15°), a monitorok függőlegesek maradnak; ha jól láthatóan
fel- vagy lefelé nézel (hátradőlve, ágyban), az egész elrendezés a nézésed irányába kerül, feléd fordítva
(fekve föléd). Ugyanígy nyílik a lebegő beállítás-panel és az első elhelyezés is.

A dashboard nyitva tartása alatt a saját sugár kikapcsol (ott a SteamVR sugara működik).

## Indítás

- `dist\Orbit.exe` – asztali parancsikon: `Desktop\Orbit.lnk`. Ha a SteamVR már fut, azonnal
  csatlakozik; ha nem, a tálcán vár, és 3 másodpercenként ellenőrzi. **A SteamVR-t magától nem indítja el.**
- Újraindításkor (második példány) csak előhozza a beállítás-ablakot. Bezárás: tálcaikon → Exit, vagy `Orbit.exe --exit`.
- General → **Start with SteamVR** (alapból be): az Orbit beregisztrálja magát a SteamVR-be
  (`orbit.vrmanifest` → `Steam\config\appconfig.json`, az automatikus indítás:
  `Steam\config\vrappconfig\erik.orbit.vrappconfig`). Ezután a SteamVR indításakor magától elindul
  (tálcán, ablak nélkül, a feladaton át admin joggal), és a SteamVR leállásakor kilép. SteamVR nélkül is
  átkapcsolható, ilyenkor a következő SteamVR-induláskor alkalmazza. A SteamVR saját helye:
  Beállítások → Indítás/Leállítás → Indításkor futó overlay-alkalmazások → Orbit.

## Fájlok

- Beállítások: `%LOCALAPPDATA%\Orbit\config.json` (mentéskor az előző `config.json.bak` lesz;
  hibás fájlt félretesz `.bad-…` néven, nem törli). Log: `orbit.log` (+ `orbit.prev.log`).
- Forrás: `src\` – `OrbitApp.cs` (főciklus, SteamVR-kapcsolat, elhelyezés), `Panels\` (óra, billentyűzet,
  monitor), `Vr\` (overlay, input, lézer), `Ui\` (widgetek, beállítás-képernyő), `Capture\` (monitor- és ablak-capture, ablaklista).
- Input-akciók: `assets\actions.json` + alap bindingok Quest/Touch, Index és Vive kontrollerekre.

## Telepíthető verzió (barátoknak)

`build_release.ps1` → `release\OrbitSetup.exe` (~75 MB, egyetlen fájl, a .NET is benne van – a másik
gépen semmit nem kell előre telepíteni). Futtatva angol telepítőt mutat (asztali parancsikon / Start with
SteamVR / Run as administrator), felhasználói szintre telepít admin jog nélkül:
`%LOCALAPPDATA%\Programs\Orbit` + Start menü + Gépház > Alkalmazások (onnan eltávolítható, az
eltávolító leveszi a SteamVR-regisztrációt és az admin feladatot is, a beállításokat megtartja).
Mellé kerül az angol `Orbit Quick Start.txt`. Nincs digitálisan aláírva, ezért a Windows SmartScreen
először figyelmeztet („További információ” → „Futtatás mindenképp”).
Ugyanaz az exe: ha a neve tartalmazza a „setup” szót, telepítő; telepítve `Orbit.exe` néven maga a program.

## Build

```
dotnet publish -c Release -o dist
```
(Előtte `dist\Orbit.exe --exit`, mert a futó exe zárolja a fájlt.)

## 1.3.0 – GitHub, frissítés, ellenőrző kör (2026-10-13)

- Repó: https://github.com/Erik1028/Orbit (nyilvános, MIT). A felhasználói README angol, ez a fájl a
  magyar fejlesztői jegyzet. A `docs/vr-test-*.png` (az asztal tükrözött képe) nincs a repóban.
- **Frissítés az appon belül** (General → Version sor gombja, `Sys\Updater.cs`): GitHub API
  `releases/latest` → ha újabb, a gomb „Update to x.y.z”: letölti az `OrbitSetup.exe`-t a
  `%LOCALAPPDATA%\Orbit\updates` mappába, ellenőrzi (méret, GitHub SHA-256 `digest`, ProductName = Orbit,
  ProductVersion), és `--update --install-dir <a futó példány mappája>` kapcsolóval elindítja. A telepítő
  leállítja az Orbitot, kicseréli a fájlokat (beállítás, parancsikonok, Alkalmazások-bejegyzés marad –
  utóbbiban csak a verzió frissül, ha ez a telepített példány), majd újraindítja (`--steamvr`, ha a SteamVR
  fut). Fejlesztői `dist` mappában is működik: az egyfájlos exe a régi DLL-ek mellett is rendben fut
  (kipróbálva). `--updatetest`: lekérdezés + letöltés + ellenőrzés telepítés nélkül; a telepítő
  `--no-start` kapcsolója tesztekhez.
- **Ellenőrző kör** (5 szempont, mindegyik találatot külön ellenőr próbálta cáfolni): 30 találatból 27
  megerősítve (25 különböző), mind javítva. A fontosabbak: a fókusz-hook újrabelépése (régebbi esemény
  felülírta az újabbat – most sorszám, `--focustest`-ben látszik a „superseded”), profilváltás két
  menetben (a sor nem szakad szét), a profil által nem ismert panelek eléd kerülnek, első használat
  headset nélkül később pótolva, foglalt 42069-es port: 10 mp-enként próbálja újra (nem képkockánként,
  socket-szivárgás nélkül), elesett monitor-capture újraépül (30 mp-es visszatartással), a médiafigyelő
  később érkező borítót is betölti, a kattintás utáni képkocka mindig újrarajzol, kép-rögzítés
  gyorsulásnál azonnal frissít, 256 px-es kurzor, kiosztás-variánsok (0xFxxx) a registryből, ablakcím-
  változás nem ír lemezre (MarkSoftDirty), sikertelen mentés újrapróbálva, duplikált ablak-azonosító
  kiszűrve, egy hibás overlay nem állítja meg az indulást.

## 1.3.1 – teszt-frissítés (2026-10-13)

- Frissítés után az Orbit első indításkor VR-értesítést mutat („Orbit updated – Now running 1.3.1
  (was 1.3.0)”), és a General fül Version sora alatt „updated from 1.3.0” áll. A régi verziót a telepítő
  olvassa ki a felülírt `Orbit.exe`-ből, és `--updated-from` kapcsolóval adja tovább (az admin-feladaton
  át is); tartalékként a konfig `General.LastVersion` mezője.

## 1.3.2 – zárható óra (2026-10-13)

- Wrist → *Lock position* (`Wrist.Locked`): a grip nem mozdítja és nem méretezi át az órát
  (`WristPanel.Grabbable` = !Locked); zárt panelre griplésnél egy rövid haptikus tick jelzi, hogy zárva van.
  A *Reset position* zárva is működik. Az 1.3.1-es frissítést Erik élesben kipróbálta (19:09, dist-ben): rendben.

## 1.3.3 – DXOverlay csak valódi overlayre (2026-10-13)

- Az OSC `isOverlayOpen` / `openOverlayCount` mostantól csak a látható monitorokat, tükrözött ablakokat
  és a billentyűzetet számolja; az Orbit saját lebegő beállításpanelje nem (Erik: a DXOverlay
  elindult, amikor csak a beállításokat nyitotta meg).

## 1.5.0 – widgetes óra (2026-10-14)

Erik backlogjából (memória: orbit-backlog.md) a „sarok-widgetek” és a „az egész óra egy üres felület
widgetekkel” pont; utána: „több widget, szebb, Apple-szerű animációk, szebb összkép”.

- **Custom számlap** (`Wrist.Face = "custom"`, `Wrist.Widgets` = típus + rács X/Y/W/H, `Wrist.Background`
  panel|cards). 6×4-es rács, minden rés és a keret is pontosan 10 px (Modern; a 34 px-es óralap-sarokkal a 24 px-es
  kártyasarok így koncentrikus), 95-ben 4 px; a cellaszélek egész pixelen (Erik: „minden widget széle egyforma
  távolságban”). `Panels/WatchWidgets.cs`: 24 fajta (méretek, alapméret, tónus: akcent / logó-indigó / a kettő keveréke / zöld), Add (a jelölt cellába bármely
  befér méretben), Free/InGrid (túlcsordulás-biztos), Step, SnapSize, Seed (a régi számlapokból + „Corners”), Clean
  (rossz méret → a dobozán belüli legnagyobb megengedett; ismeretlen típus MARAD - egy újabb Orbit widgetje túléli).
- **Widgetek**: idő, dátum, naptár (havi rács / nagy nap; 95: tépőlapos naptár), analóg óra, **időzítő** (gyűrű,
  ±1 perc, lejártakor értesítés + csengő: `timer.done`), **stopper**, **VR-ben töltött idő** (`PanelData.VrSince`),
  akkuk (gyűrűk; kevés hely → a legüresebbek), most szól, **hangerő** (`Sys/SystemStats.cs` SystemVolume: Core Audio
  IAudioEndpointVolume, 5 mp-enként újra lekérve), teljesítmény-mérők (fps, reprojekció, GPU ms, VRAM), grafikon,
  **CPU** (GetSystemTimes), **memória** (GlobalMemoryStatusEx), és minden óra-gomb külön.
- **Modern = Orbit-saját** (Erik: ne legyen egyértelmű Apple-másolat): a Look fül akcentszíne + a logó indigója
  (téma-váltással együtt változik), az értékek „pályák”: vékony teljes kör, az érték íve, a végén keringő
  „műhold”-pötty (`Orbit`, `Painter.Arc` kerek végekkel), határértéknél az Orbit Warn/Bad színe; analóg óra:
  pötty-jelölések, a másodperc egy a peremen keringő akcentpötty; feliratok szürke nagybetűvel, mint a beállításokban;
  gombok az Orbit panelszínében kerettel, bekapcsolva akcent. Mozgás: `Ui.Spring` (rugó al-lépésekkel; `snap`: a kis
  remegés nem animál, így az fps nem rajzoltat folyamatosan), a widgetek soronként beúsznak, amikor ránézel
  (`Intro`), a szerkesztőben új helyre rugóznak, új widget „beugrik”, az óra percei / az időzítő / a VR-idő
  számjegyei gördülnek (`RollText`). Mozgás közben legfeljebb 45 fps (`_lastDraw`); egy rajz ~2 ms (Debug), a
  beúszás ~1,25 s. A 95-ös stílusban nincs mozgás (a rugó ott azonnal célba ér).
- **Szerkesztő** (`Ui/SettingsWatchEditor.cs`, Wrist → Custom → Edit layout; 95: „Edit layout...”): élő előnézet,
  kattintás = kijelölés (referencia szerint, nem sorszám), húzás = mozgatás, sarok húzása = méret (a megfogáshoz
  képest), üres cella = ide jön a következő; paletta (Modern 5×5 gomb, 95 lista + Add), Smaller/Larger/Remove,
  háttér, „Start over from” (Full/Analog/Minimal/Time only/Corners), Empty.
- **Kinyitható widgetek** (Erik: „ha rálépek… megnagyobbodik a többi előterében és több információ vagy kontroll”;
  `Panels/WristDetail.cs`): a kártyára bökés kinyitja (`CardTap`, a widget saját gombjai előbb kapják a bökést),
  Modernben rugóval az egész rácsra nő egy sötétített óra előtt (`Ui.Inert`: mögötte minden csak kép), mellé bökve
  vagy ×-szel visszaáll; 95-ben a korabeli ablak-„zoom” körvonal után egy 95-ös ablak (X / OK zárja, mellé bökés
  nem). Nézetek: most szól (nagy borító, vezérlők, hangerő-csúszka), akkuk, idő (óra + dátum + hét + VR-idő; 95:
  Date/Time Properties), naptár (hónapok lapozva), időzítő (1/3/5/10/15/30 perc), stopper (köridők), teljesítmény
  (grafikon + átlag/legrosszabb/eldobott), rendszer (CPU-előzmény 60 s), hangerő. Az 1×1-es időzítő/stopper/hangerő
  bökésre most kinyílik (a vezérlés a kinyitott nézetben). Ha elfordítod a csuklód, bezárul.
- Az óra már nem rajzol újra minden lézermozdulatra (`NeedsRedrawForMove`), és a képkocka-mintavétel csak akkor
  fut, ha valami mutatja (`WristPanel.WantsFrames`; a widget-grafikon eddig üres maradt volna).
- Két ellenőrző kör (review workflow + cáfoló ellenőrök) javításai benne vannak.

## 1.4.0 – Windows 95 stílus (2026-10-14)

Kiadva: v1.4.0 (GitHub, OrbitSetup.exe sha256 8eccb33b…d954), és Erik dist-jébe a frissítő úton telepítve
(`OrbitSetup --update --install-dir dist`, config mentés: `config.json.pre-1.4-20261014-140709`).

Erik kérése: választható Win95-ös kinézet „mint a Mixtape-nél”; az első, csak átszínezett változatra:
„ez még nem az igazi, lehetne igazán autentikus, a stílus, a betűtípus, a layoutok, minden”. Mintának
a Regional Settings Properties lapot és egy Date/Time órát küldte.

- **Elv**: Win95 módban minden kis, valódi 1995-ös méretű vásznon készül (`Painter.BeginPixel`: aliasolt
  alakzatok, aliasolt monokróm szöveg, GDI-classic mérés, Microsoft Sans Serif 11 px = 8 pt), utána
  `ImageNearest` egész szorzóval nagyítja. Vásznak: beállítások 750×480 (VR-panel és dashboard ×2 =
  1500×960), óra 320×220 ×2, billentyűzet 740×338 ×2, értesítés 480×320 ×2.
- **Vezérlők** (`Ui/Ui95.cs`): Button95 (alapértelmezett fekete keret, benyomva, „latched” raszterezve),
  Check95, Radio95, Combo95 (+ `Popups95`: a nyitott lista mindenek fölött, alatta minden vezérlő vak; a
  listán kívüli kattintás CSAK bezár), Track95, Tabs95 (a kijelölt fül egybeolvad a lappal), List95
  görgetősávval, Group95, Edit95, Progress95 (blokkok), Pane95, Caption95 + ? / X / _ / □ gombok.
  Gyorsbillentyű-aláhúzás: `Label95("&Size:")`; adat (ablakcím, dal) `Text95`-tel megy, így a `&` nem tűnik el.
- **Beállítások** (`Ui/SettingsView95.cs`): „Orbit Properties” 7 füllel; csoportkeretek, „Címke: [vezérlő]”
  sorok, lenyíló listák (Attached to, Hand, Face, Laser on, Smoothing, Style...). Lent balra Recenter /
  Show keyboard / Show desktops, jobbra **OK / Close / Apply**: minden azonnal érvényes (mint egy 95-ös
  lap PSM_CANCELTOCLOSE után, ezért „Close” és nem visszavonni nem tudó Cancel); az Apply addig aktív,
  amíg a változás nincs lemezen (`ConfigStore.Dirty`), megnyomva azonnal ment. A ? a General fülre visz.
- **Asztali ablak** Win95 módban keret nélküli, maga a 95-ös ablak: a kék címsor húzza
  (WM_NCHITTEST → HTCAPTION, a ? és X fölött nem), szögletes sarok (DWMWCP_DONOTROUND), méret 750×480 × k
  (k = DPI kerekítve: 100–125 % → 1, vagyis 1080p-n valódi 1995-ös méret). Stílusváltáskor átáll oda-vissza.
- **Óra** (`Panels/WristPanel95.cs`): „Date/Time” ablak – nagy idő másodperccel, hosszú dátum a Windows
  területi beállítása szerint, analóg számlap pöttyökkel és domború türkiz mutatókkal (`Painter.Polygon`),
  CD Player-sor (borító, cím, |<< > >>|), Power Meter-akkuk, eszköztár (bekapcsolt gomb raszterezve),
  állapotsor vagy fekete-zöld grafikon. A _ és X elrejti, amíg el nem fordítod a csuklód (mindig látható
  módban 8 mp-re); a □ szürke. Az ablak magassága a bekapcsolt részekhez igazodik.
- **Billentyűzet** (`Panels/KeyboardPanel95.cs`): 95-ös gombok, nyomva benyomódik, rögzített módosító
  raszteres, egyszeri módosító fekete keretes; pixel-nyilak (←, ↵, kurzornyilak); a találati téglalapok
  nem változtak. **Értesítés**: 95-ös ablak navy címsorral és az Info ikonnal.
- **Ellenőrizve**: a Modern az 1.3.3-mal azonos (csatornánkénti snapshot-diff), kivéve a szándékos új
  Style-kapcsolót a Look fül alján és a verziószámot (az óra számjegyei az idő miatt térnek el). CSAPDA: a
  Pillow `getbbox()` RGBA képen csak az alfát nézi, átlátszatlan képeknél színeltérésre is None-t ad –
  csatornánként kell összevetni; `--snapshot`: lista-választás (Smoothing → High) és a listán kívüli kattintás (a Haptic ticks
  nem vált); `--wintest`: keret nélküli ablak, HTCAPTION a címsoron, 750×480 96 DPI-n, váltás oda-vissza.
- **Kiadás előtti átnézés** (4 szempont + 4 cáfoló ellenőr): 12 megerősített, mind javítva – egy 95-ben nyitva
  hagyott lenyíló lista Modernre váltás után is elnyelte a kattintásokat (`BeginPopups95` Modernben eldobja);
  ablakválasztó a sor helyett az ablak handle-jét jegyzi meg; a „Display:” választás nem jelöl változást;
  induláskor nincs Win11-keretes villanás (`OnLoad` → `ApplyStyle`), stílusváltáskor az ablak a képernyőn
  marad; a félkövér dalcím félkövérként mérve; a 95-ös billentyűzet felső gombjai is benyomódnak; a Modern
  szöveg ismét Grayscale (ahogy a `Gpu` beállítja, nem Default); Volume elengedésekor hangminta.

## Fejlesztői kapcsolók

- `--snapshot <mappa>` – VR nélkül PNG-be rajzol minden panelt + egy-egy képet minden monitorról (saját,
  külön config-gal, a valódit nem érinti).
- `--selftest` – futó SteamVR mellett: teszt-overlay, UV-irány ellenőrzés, frame sync, statisztika.
- `--scene [--dash]` – 12 másodpercig „játéknak” adja ki magát (bezárja a dashboardot); `--dash`: kinyitja az Orbit fület.
- `--osctest <port>` – egy-egy minta OSC-üzenet a megadott portra (formátum-ellenőrzéshez).
- `--tasktest` – ideiglenes, NEM admin feladattal kipróbálja a feladatos indítást, majd törli.
- `--filtertest` – a lézer-simítás remegése / késése szintenként.
- `--raytest` – a saját lézer-találatszámítás a futó SteamVR-rel szemben (alpha 0 tesztpanel, pár mp).
- `--leaktest <gpu|media|idle> [mp]` – egy háttér-összetevő privát memóriája egyedül futtatva, a végén GC-vel.
- `--wintest <mappa>` – az asztali beállításablak mindkét stílusban (saját config a mappában): kép, méret,
  és mit felel a 95-ös címsor a WM_NCHITTEST-re.
- `--focustest` – a szövegmező-felismerés egy képernyőn kívüli tesztablakon (szövegmező, csak olvasható mező,
  gomb, RichEdit), közvetlenül és a fókusz-hookon át.

## Teljesítmény-napló

Az `orbit.log` 10 másodpercenként két sort ír:
- `perf:` képkocka/s, munka képkockánként, szakaszonként átlag/max ms (vr, lasers, wrist, kbd, settings,
  desktops, dash+osc);
- `mem:` a fő szál és az összes szál szemét-termelése (KB/s), GC-k száma gen0/1/2 és a szünetük, heap,
  working set, private bytes, handle-ök, GDI / USER objektumok, és szakaszonként a képkockánkénti bájtok.
  Ha volt GC, egy `gc:` sor is jön (generáció, háttér/tömörítő, szünet, előléptetett KB).

Mért és javított (1.2.0, 2026-10-13): a fő szál 300–550 KB/s szemetet termelt, emiatt 10 mp-enként futott
egy teljes GC 3–5 ms szünettel. A legnagyobb forrás a monitorképek átvétele volt a C#/WinRT-vetületen át
(1,3–3,9 KB/képkocka) – most nyers COM-hívásokkal megy (`MonitorCapture.Poll`, frame pool vtable [7],
frame [6]/[8], IClosable), szemét nélkül. Szintén javítva: `PlaceMissing` LINQ-listái minden képkockán
(264 B), a widget-azonosítók összefűzése (`Ui.K` / `Ui.Id` gyorsítótár), a lézer `foreach`-e az interfészen,
a kurzorkép 3×16 KB-os tömbjei. A VR-beállításpanel csak akkor rajzol újra, ha a mutató másik widgetre
ér (654 mozgásból 2–4 újrarajzolás), és induláskor minden fülét egyszer előrajzolja (első megnyitás
akadás nélkül). A fő ciklus szála AboveNormal prioritású.

Reszponzivitás (1.2.0, 2026-10-13 este):
- **Saját lézer-találatszámítás** (`Vr\LocalRaycast.cs`): a SteamVR `ComputeOverlayIntersection` IPC-hívása
  helyett (panelenként, kezenként, képkockánként; ez okozta a mért 5–16 ms-os akadásokat) folyamaton belül.
  `--raytest` ellenőrzi a futó SteamVR-rel szemben: egy alpha 0 tesztpanel 156 alakban, 62 400 sugár →
  **0 eltérés** (max uv 0,00015, távolság 0,04 mm). Közben kimérve: SteamVR a görbült panelt csak ott
  találja el, ahol a sugár KILÉP a hengerből (homorú oldal; hátulról soha), a sík panelt mindkét oldalról;
  rejtett panelre nem ad találatot; panel-változást csak egy későbbi képkockán alkalmaz; textúra nélkül
  uv = 0; 0,0005 alatti görbületnél maga is pontatlan (float) – az Orbit ezt síkra kerekíti
  (`DesktopLayout.MinCurvature`). Mód: `General.Raycast` = `shadow` (SteamVR dönt, a saját is lefut, a
  naplóba `raycast:` sor jön az eltérésekkel és a hívásonkénti µs-mal) | `local` | `steamvr`.
- **Képrögzítés ütemezése**: pontos átlag-ütemezés (`DesktopPanel.CaptureDue`) – a régi „most + 1/fps” a
  ~80–90 Hz-es ciklusban minden második ciklust kihagyta (60 fps-ből ~40 lett). Okos módban a nézett / mutatott
  monitor és ablak a headset frissítésével megy (90 Hz; a monitor saját frissítése és a ciklus a felső határ).
- **GPU-prioritás**: `IDXGIDevice.SetGPUThreadPriority(7)` (admin kell; `General.HighGpuPriority`), és minden
  képkocka után egy esemény-lekérdezés + Flush: a `perf:` sor `gpu late %` értéke mutatja, hány képkockánál
  nem végzett még az Orbit GPU-munkája a következő ciklusra.
- **Energiatakarékos ütemezés kikapcsolva**: `SetProcessInformation(ProcessPowerThrottling)` – EXECUTION_SPEED
  és IGNORE_TIMER_RESOLUTION nélkül (a Windows 11 a háttérben futó, ablak nélküli folyamatokat lassítaná).
- **Médiafigyelő**: másodpercenkénti lekérdezés helyett a Windows változás-eseményeire olvas (legfeljebb 2 mp-enként,
  parancs után azonnal), 20 olvasásonként egy gen0 GC. Mérve (`--leaktest media`): a régi 1 mp-es lekérdezés
  ~14 MB/perc natív memóriát hagyott a WinRT-objektumok mögött (csak GC szabadítja fel), az új stabil.
  `--leaktest gpu`: a GPU-számlálók nem szivárognak.

## Tesztelve / nem tesztelve (2026-10-13)

Tesztelve SteamVR null headsettel (headset nélkül): overlay-ek létrejönnek, a 3 monitor élőben megy
és ívben áll, billentyűzet, dashboard-fül, UV-irány (bal alsó sarok → tükrözni kell), színek.
Szintén null headsettel (2. kör): a görbült overlay geometriája mérve (tengely r-re a panel előtt, a szélesség
= ívhossz), a 3 monitor hézagmentes íves sorba áll; OSC-üzenetek formátuma UDP-n visszaolvasva.
**Kontroller nélkül nem tesztelhető**: lézer, kattintás, megfogás, csukló-óra helye és előtűnése,
dupla-grip kapcsoló, összepattintás ejtéssel, kétkezes átméretezés. DXOverlay élesben nincs kipróbálva.
Ha az óra nem jó helyen van, a másik kézzel megfogva odébb tehető; a Wrist → Reset position visszaállítja.
1.1.0 (2026-10-13): ablak-capture élesben mérve (`--snapshot`): a kép mérete pontosan a DWM látható
ablakkerete (1920×1032 = 1920×1032), erre épül az egér-leképezés. Számlapok, grafikon, Look/Windows fül
és a lekerekített panel snapshotban ellenőrizve. VR-ben (kontrollerrel) az ablakpanel kattintása,
az előtérbe hozás és a headset-levétel szünet még nincs kipróbálva.

## Rendszergazdaként (alapból bekapcsolva)

Admin joggal fut, hogy a VR-egér és -billentyűzet a rendszergazdaként futó ablakokba (Feladatkezelő,
Hexpad stb.) is be tudjon kattintani/gépelni – a Windows (UIPI) egy nem-admin programtól ezt letiltja.
Az **első** indításkor egyszer feljön a UAC; ekkor az Orbit létrehoz egy ütemezett feladatot
(**„Orbit (admin)”**, csak kézi indítású, nincs időzítése). Ezután a parancsikonról és a SteamVR-ből is
kérdés nélkül, ezen a feladaton át indul admin joggal. General → *Run as administrator* kikapcsolva
nem kér admin jogot (admin példányból kikapcsolva a feladatot is törli); `Orbit.exe --no-elevate`
egyszer admin nélkül indít. Ha az exe máshova kerül, a következő indításkor újra kér egy UAC-ot,
és frissíti a feladatot.

## Ismert korlátok

- A UAC-ablakra magára (biztonsági asztal) VR-ből sem lehet kattintani – ez minden overlaynél így van.
- A csuklón nincs GPU-terhelés %: a Windows GPU-számlálói az AMD kártyán erősen alulmérnek
  (a Hexpadnél mérve 2 % valódi 99 % mellett). A VRAM és a SteamVR GPU ms pontos.
- A monitorra kattintás a valódi egérrel kattint, tehát elveszi a fókuszt a VRChat ablakától
  (a VRChat VR-ben ettől fut tovább).
- Tükrözött ablak: amíg csak mutatsz rá (nem kattintasz), a valódi kurzor a PC-n az ablak helyére megy;
  ha azt az ablakot ott egy másik takarja, a hover-effektek a takaró ablakon látszanak. Az első
  kattintás/görgetés előtérbe hozza. Kis méretre tett ablakról nem jön új kép (az utolsó marad).
