Screen Bugs {VERSION} for 64-bit Windows 10 (1607) or later.

Download `ScreenBugs-Setup-{VERSION}.exe` and run it. There is no .NET prerequisite — the runtime is bundled. The installer asks which bug you want, how many, whether to run at sign-in, and whether to add a desktop shortcut. Those choices apply the first time each user runs Screen Bugs; if you have used it before, your saved settings are kept.

The installer is not code-signed, so SmartScreen will warn on first download. Check `ScreenBugs-Setup-{VERSION}.exe.sha256` against the file if you want to verify what you got:

```powershell
Get-FileHash ScreenBugs-Setup-{VERSION}.exe -Algorithm SHA256
```

### Silent install

```
ScreenBugs-Setup-{VERSION}.exe /S /CURRENTUSER /BUGTYPE=HouseSpider /BUGCOUNT=12 /STARTUP=1 /DESKTOP=0
```

`/ALLUSERS` or `/CURRENTUSER` chooses the scope. `/BUGTYPE` takes `Random` or a species name — `HissingCockroach`, `BlackGardenAnt`, `RedFireAnt`, `PrayingMantis`, `SevenSpotLadybug`, `StagBeetle`, `HouseSpider`, `Centipede`, `StinkBug`. `/BUGCOUNT` is 1–50. `/D=<path>` sets the install directory and must come last, unquoted.

Uninstall accepts `/S`, and `/DELETEDATA=1` to remove your settings as well.
