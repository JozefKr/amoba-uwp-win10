AMŐBA UWP JÁTÉK - FUTTATÁSI ÉS FEJLESZTÉSI ÚTMUTATÓ

Ez a dokumentum a szakdolgozathoz ("Amőba játék fejlesztése mobil eszközre") tartozó UWP forráskód futtatásához és a fejlesztői környezet konfigurálásához nyújt segítséget.
A szoftver futtatható Windows 10/11 PC-n és Windows Mobile 10 operációs rendszert futtató okostelefonokon (pl. Nokia Lumia 930) is.

FEJLESZTŐI KÖRNYEZET (IDE) ÉS ESZKÖZÖK
IDE: Microsoft Visual Studio Community 2017 (15.0-s verzió a kompatibilitás miatt!!).

Szükséges Visual Studio Workload-ok:
Universal Windows Platform development (UWP fejlesztés)
.NET asztali fejlesztés (C#)
Célzott Framework: .NET Core, Version=v5.0

WINDOWS SDK VERZIÓK
Target SDK: Windows 10, version 2004 (Build 19041) - 10.0.19041.0
Minimális SDK: Windows 10 Anniversary Update (Build 14393) - 10.0.14393.0 (Nokia Lumia telefonokhoz)

RENDSZERKÖVETELMÉNYEK ÉS FÜGGŐSÉGEK
A célgépen (PC vagy mobil) a következő csomagoknak kell jelen lenniük (általában automatikusan települnek):
VCLibs: Microsoft.VCLibs.140.00.Debug (Min. verzió: 14.0.33519.0)
NET Core Runtime: Microsoft.NET.CoreRuntime.1.1 (Min. verzió: 1.1.27004.0)
Architektúra: A fordítás x64-re lett optimalizálva, de az "Any CPU" beállítás is támogatott.

SZÜKSÉGES KÉPESSÉGEK (CAPABILITIES)
A hálózati játék miatt a Windows tűzfal engedélyt kérhet, melyet el kell fogadni!
internetClientServer (Távoli hálózati kapcsolatokhoz)
privateNetworkClientServer (UDP felderítéshez és LAN játékhoz)

FUTTATÁS ÉS FORDÍTÁS LÉPÉSEI
Nyissa meg az Amoba.sln fájlt a Visual Studio-ban.
Kapcsolja be a "Fejlesztői módot" a Windows 10/11 operációs rendszeren (Gépház -> Frissítés és biztonság -> Fejlesztőknek).
Állítsa be a kívánt architektúrát (pl. x64 asztali PC-hez, ARM mobil teszteléshez).
Válassza ki a céleszközt: "Local Machine" (Helyi gép) asztali teszteléshez, vagy csatlakoztatott mobil eszköz esetén "Device".
Indítsa el a futtatást (F5).

GYAKORI HIBÁK
Csatlakozási probléma: Ellenőrizze, hogy a Windows Tűzfal engedélyezi-e a kapcsolatokat az "Amoba.exe" számára a privát és nyilvános hálózatokon.
"Unloaded" projekt állapot: Frissítse az SDK verziót a projekten jobb gombbal kattintva, vagy telepítse a hiányzó Build 19041-es SDK-t.