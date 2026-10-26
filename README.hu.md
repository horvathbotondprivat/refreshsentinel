# RefreshSentinel 1.0 — képfrissítés-felügyelet Windows 11-re

Magyar nyelvű, tálcán futó alkalmazás, amely monitoronként figyeli a képfrissítést, és eltérés esetén visszaállítja a mentett célértéket.

Letöltés: a GitHub-tároló **Releases** oldalán található `RefreshSentinel-Windows.zip`.

## Indítás

1. Bontsd ki a `RefreshSentinel-Windows.zip` tartalmát egy állandó mappába, például a Dokumentumok alá. A ZIP-ből közvetlenül ne indítsd.
2. Indítsd el a `RefreshSentinel.exe` fájlt. Nincs telepítő, internetkapcsolat vagy rendszergazdai jogosultság.
3. Monitoronként pipáld be a **Monitor felügyelete** jelölőt, és válaszd ki a kívánt Hz-értéket.
4. Pipáld be az **Automatikus ellenőrzés** lehetőséget. Állítsd be az időközt 5 és 3600 másodperc között; az alapérték 30 másodperc.
5. Kattints a **Mentés** gombra. Bekapcsolt felügyeletnél az első ellenőrzés azonnal elindul.

Az első indításkor a felügyelet kikapcsolt állapotban van: a program a választásaid mentéséig nem módosít kijelzőbeállítást.

## Háttérben futás

- Az ablak bezárása vagy kis méretre állítása elrejti az ablakot, az alkalmazás tovább fut az értesítési területen.
- Az ikon a Windows beállításától függően a tálcán vagy a felfelé mutató nyíl mögötti rejtett ikonok között jelenik meg. A helyét a Windowsban tudod kiválasztani.
- Az ikonra duplán kattintva megnyílnak a beállítások. A jobb kattintásos menüben visszaállíthatod az értékeket, szüneteltetheted a felügyeletet vagy kiléphetsz.
- Az **Induljon a Windowszal, a tálcára rejtve** jelölőt a **Mentés** gombbal lehet érvényesíteni. Ez a saját Windows-fiókod bejelentkezésekor indítja az alkalmazást. A Windows Feladatkezelő / Indítási alkalmazások beállítása külön letilthatja az indulást.
- A **Visszaállítás most** gomb menti az ablak beállításait, és egyszer visszaállítja a felügyelt monitorok célértékeit akkor is, ha az automatikus felügyelet szünetel. A tálcamenü az utoljára mentett beállításokat használja.
- A **Lista frissítése** csak lekérdez, önmagában nem változtat képfrissítést.
- Kijelző csatlakoztatása után 5, alvásból ébresztés után 10 másodpercet vár a lekérdezéssel. Zárolás és alvás alatt szünetel a felügyelet.

## Működés és korlátok

- Az alkalmazás a Windows által felsorolt, a monitorral kompatibilis módokat kínálja fel a jelenlegi felbontáshoz, színmélységhez, tájoláshoz és sorváltási módhoz. Egyéni vagy túlhajtott módot nem hoz létre.
- A Windows hagyományos kijelző-API-ja egész számú névleges Hz-értékeket ad. Például egy körülbelül 59,94 Hz-es jel 59 vagy 60 Hz-ként szerepelhet, a meghajtótól függően. Ez a verzió a Windows által jelentett egész értéket őrzi, pontos törtszámú időzítést nem állít be.
- A kijelző névleges módját felügyeli, nem a játék képkockaszámát vagy a FreeSync/G-SYNC pillanatnyi panelképfrissítését. A Windows dinamikus képfrissítését (DRR) fix célérték használatához a Windows Speciális kijelző beállításaiban érdemes kikapcsolni.
- A váltás rövid képernyővillanással járhat. Mentéskor, bekapcsolt felügyelet mellett azonnal alkalmazhatja a célértéket.
- A felbontást, tájolást és a monitor asztali pozícióját megtartó módot kér. Előbb a Windows módellenőrzését futtatja, majd a váltás eredményét visszaolvassa. A grafikus meghajtó dönt a mód tényleges alkalmazásáról.
- Ha a célérték az aktuális felbontáson nem támogatott, a program kihagyja a váltást. A célérték megmarad, és újra használható lesz, amint támogatott módra váltasz.
- Leválasztott monitort nem kapcsol vissza. A hozzá tartozó szabály megmarad; újracsatlakoztatáskor folytatható a felügyelet. Az azonosítás a Windows monitor-eszközútvonalán alapul, így az azonos típusú kijelzők külön szabályt kapnak. Másik csatlakozóra kötés vagy meghajtócsere esetén újra ki kellhet választani a monitort.
- Tükrözött kijelzőket külön nem állít: a Windowsban válaszd a **Kiterjesztés** módot az önálló szabályozáshoz.
- Sikertelen váltás után legalább 60 másodpercig vár az újabb próbálkozással; sikeres váltás után legalább 15 másodperces védelmi időt tart. Kizárólagos teljes képernyős alkalmazás vagy más módváltó program felülírhatja a beállítást.
- Csak egy példány fut. Az újraindított példány a már futó ablakát nyitja meg.
- A program nem használ hálózatot, és nem küld adatokat. Windows 11 és .NET Framework 4.8 vagy 4.8.1 szükséges; ez a Windows 11 része.
- A saját használatra készült futtatható fájl nincs digitálisan aláírva, ezért a Windows figyelmeztetést jeleníthet meg.

## Beállítások és napló

Az adatok a `%LOCALAPPDATA%\HzGuard` mappába kerülnek:

A belső mappanév és az indítási bejegyzés megtartja az első, HzGuard nevű változat azonosítóját, hogy a korábbi beállítások megmaradjanak. Ha még fut a régi változat, az új indítása előtt lépj ki belőle a tálcamenüből.

- `settings.json`: a mentett beállítások; a mentés atomikus fájlcserével történik.
- `settings.json.bak`: az előző mentés biztonsági másolata.
- `events.log`: változások, visszaállítások és hibák; 1 MB után egy előző naplót tart meg.

Sérült beállításfájl esetén a felügyelet kikapcsolva marad, és az ablak figyelmeztetést mutat. A fájlt nem írja felül az új beállítások mentéséig.

Az automatikus indítás helye: `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`, `HzGuard` érték. Ezt csak az indítási jelölő megváltoztatása és mentése módosítja. Ha máshová mozgatod az alkalmazást, kapcsold ki, mentsd, majd az új helyről indítva kapcsold vissza az automatikus indítást.

Eltávolítás: kapcsold ki és mentsd az automatikus indítást, lépj ki a tálcamenüből, majd töröld a program mappáját. A saját beállításokat tartalmazó fenti mappa külön törölhető.

## Fordítás és ellenőrzés

A teljes forrás a `src` mappában található. Külső csomag, letöltés vagy .NET SDK nem kell. A `build.ps1` a Windows .NET Framework fordítójával készíti el az alkalmazást, lefuttatja az ellenőrzéseket, és létrehozza a ZIP-csomagot.

PowerShellből: `powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1`

A program a `dist` mappába, a ZIP-csomag a projekt gyökerébe kerül. Aktív Windows-asztal nélküli környezetben a `-SkipLiveChecks` kapcsoló kihagyja a valódi monitorok lekérdezését és a meghajtó módellenőrzését; a szimulált tesztek továbbra is lefutnak. A `verification` mappa kizárólag helyi diagnosztika, nem része a GitHubra feltöltött forrásnak vagy a kiadási csomagnak.

A tesztek szimulált kijelzőkkel ellenőrzik a visszaállítást és a hibakezelést. A valódi monitorokat csak lekérdezik; a `CDS_TEST` kizárólag a jelenlegi mód elfogadhatóságát vizsgálja, módváltás nélkül. Az eredmény a `verification` mappába kerül. Az éles automatikus visszaállítás kézi kipróbálása: mentsd a kívánt Hz-et, állíts másik támogatott értéket a Windowsban, és várd meg a beállított időközt. Ezt teljes képernyős játék nélkül végezd.

Technikai források (Microsoft):

- https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumdisplaysettingsexw
- https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-changedisplaysettingsexw
- https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-querydisplayconfig
- https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-displayconfig_target_device_name
