# RefreshSentinel – Windows 11 vizuális irány

Ez a csomag ikonválogatás és megjelenési javaslat. Az alkalmazás működését és a GitHubon közzétett kiadást nem módosítja.

## Javasolt alkalmazásikon

**Desktop Sync:** monitor és frissítési nyilak. A három közül ez fejezi ki legpontosabban a mentett képfrissítés visszaállítását. A `RefreshSentinel-Fluent.ico` ebből készített, kék, átlátszó hátterű parancsikonváltozat.

Alternatívák:

- **Desktop Checkmark:** monitor és pipa; a megfelelő beállítás megőrzését hangsúlyozza.
- **Desktop Pulse:** monitor és jelalak; a képfrissítés figyelésére utal, de általános teljesítményfigyelőnek is tűnhet.

Az eredeti SVG-k a Microsoft [Fluent UI System Icons](https://github.com/microsoft/fluentui-system-icons) készletéből származnak, MIT licenccel. A pontos forrásverzió és fájlonkénti hivatkozás a `sources.json` fájlban szerepel. A licenc másolatát a csomag tartalmazza; a származtatott fájlok terjesztésekor is tartsd meg.

Az alkalmazás/parancsikon színes lehet, a tálcára pedig egyszerű, egyszínű, átlátszó hátterű változatot javaslok. A `tray-light.ico` világos tálcára való sötét rajzolat; a `tray-dark.ico` sötét tálcára való világos rajzolat. Az alkalmazásnak a tálca témája alapján kellene választania köztük. Az állapotot a menü szövege és a buboréksúgó is jelezze; ne csak a szín.

A többméretes ICO-fájlok 16, 20, 24, 32, 40, 48, 64, 96, 128 és 256 pixeles képeket tartalmaznak. Ezzel lefedhetők a különböző megjelenítési méretek. [Microsoft: ikonméretezés és átlátszó háttér](https://learn.microsoft.com/en-us/windows/apps/design/iconography/app-icon-construction).

## Gomb- és állapotikonok

| Funkció | Fluent ikon |
| --- | --- |
| Mentés | Save |
| Kézi ellenőrzés / lista frissítése | Arrow Sync |
| Beállítások | Settings |
| Szüneteltetés | Pause |
| Folytatás | Play |
| Napló | Document Text |
| Rendben | Checkmark Circle |
| Nem támogatott mód / hiba | Warning |
| Ellenőrzési időköz | Clock |

Az alkalmazáson belüli parancsokhoz a 20 pixeles Regular változatokat választottam. Mellettük maradjon magyar felirat is. A gombokhoz ne keverj másik ikoncsaládot. Windows 11-en a rendszer [Segoe Fluent Icons](https://learn.microsoft.com/en-us/windows/apps/design/style/segoe-fluent-icons-font) betűkészlete is használható, azt nem kell a programmal együtt terjeszteni.

## Felületi elemek

- **Alap:** a Windows Gépház nyugodt, beállítássorokra épülő elrendezése; monitoronként egy kártya, balra név és aktuális Hz, jobbra a célérték.
- **Betű:** Segoe UI Variable, rendszerbeli Segoe UI tartalékkal. Általános szöveg 14 epx, másodlagos 12 epx, szakaszcím 20 epx, főcím 28 epx. Normál és félkövér helyett elsősorban Regular és Semibold; mondatkezdő nagybetűs feliratok, csupa nagybetűs címkék nélkül. [Microsoft: tipográfia](https://learn.microsoft.com/en-us/windows/apps/design/signature-experiences/typography).
- **Geometria:** 4 pixeles lekerekítés a gombokon és beviteli mezőkön, 8 pixeles a kártyákon és felugró elemeken; 8/12/16/24 pixeles térközök. Az ablakkeret lekerekítését a Windows kezelje. [Microsoft: geometria](https://learn.microsoft.com/en-us/windows/apps/design/signature-experiences/geometry).
- **Színek:** automatikus világos/sötét téma; neutrális háttér, a fő műveleten rendszerkiemelőszín. Zöld pipa a helyes értékhez, borostyán figyelmeztetés az eltéréshez, ikon és szöveg együtt. A `theme-proposal.json` színei tervhez választott mintaszínek, nem egy Windows-téma hivatalosan kiolvasott értékei. [Microsoft: színek](https://learn.microsoft.com/en-us/windows/apps/design/signature-experiences/color).
- **Anyagok:** Mica az ablak alaprétegén, fölötte visszafogott kártyák. Acrylic csak átmeneti menükhöz vagy felugró elemekhez. A jelenlegi WinForms felülethez ezek tényleges beépítése külön fejlesztési lépés; egy háttérszín önmagában nem valódi Mica. [Microsoft: Mica](https://learn.microsoft.com/en-us/windows/apps/design/style/mica), [Acrylic](https://learn.microsoft.com/en-us/windows/apps/design/style/acrylic).
- **Kapcsolók:** váltókapcsolót akkor használjunk, ha az állítás azonnal érvényesül. A jelenlegi, Mentés gombbal érvényesülő beállításokhoz maradjon jelölőnégyzet, amíg ez a viselkedés nem változik. [Microsoft: ToggleSwitch](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/toggles).
- **Állapotvisszajelzés:** egyszerű állapotsor vagy InfoBar; a mentés és az automatikus visszaállítás eredménye ne csak értesítési buborékban legyen elérhető.

## Kész Microsoft-tervezési források

- [Windows UI Kit, Figma-tervezőkészlet és további erőforrások](https://learn.microsoft.com/en-us/windows/apps/design/downloads/): gombok, beviteli mezők, kártyák, navigáció és témák.
- [WinUI 3 Gallery](https://github.com/microsoft/WinUI-Gallery): kipróbálható natív vezérlők és minták; a célmegjelenés jó viszonyítási alapja.

A betűfájlokat és a Microsoft terméklogóit nem másoltam a csomagba. A `.ico`-k a licencelt Fluent rendszerikonok változatai; a főikon kék színezése RefreshSentinel-megjelenési javaslat, nem Microsoft által készített terméklogó.
