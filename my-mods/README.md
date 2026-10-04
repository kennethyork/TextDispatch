# My GTA V / LSPDFR mods — the full list

Every mod on this machine, where it lives, and what it needs. Built from a scan of the folders and the plugins' own
metadata, not from memory of what was downloaded.

| | |
|---|---|
| Game | `D:\Grand Theft Auto V Legacy` — GTA V Legacy, build 1.0.3889.0 |
| RAGE Plugin Hook | 1.131.1424.17745 ✓ |
| LSPDFR | 0.4.9695 (0.4.9) ✓ |
| .NET Framework | 4.8 ✓ |
| RAGENativeUI | 1.9.3.0 ✓ (was wrongly 1.6.3.0 — fixed, see §1) |
| ScriptHookV | ✗ **not installed** — the one thing ELS needs |
| LSPDFR plugins | **17** DLLs in `Plugins\LSPDFR`, all load |
| ASI mods | 3 installed, **2 load** |
| Scanner audio packs | 7 mods' audio installed |
| Vehicles | 12 ELS police models in `patchday25ng\dlc.rpf` |
| Local model | Ollama `llama3.2:latest` ✓ answering |

Legend: **✓** present and working · **✗** missing or broken · *optional* = the mod works without it.

---

## 1. What is still outstanding

### 1.1 ScriptHookV is missing, so ELS does not run ✗

`asiloader.log` says it plainly, in every session:

```
ASI: Loading "D:\Grand Theft Auto V Legacy\ELS.asi"
     "ELS.asi" failed to load
ASI: Loading "D:\Grand Theft Auto V Legacy\openCameraV.asi"     <- loads
ASI: Loading "D:\Grand Theft Auto V Legacy\OpenIV.asi"          <- loads
```

Not a guess: ELS.asi imports `ScriptHookV.dll`, and that file is not on the machine. Everything else ELS needs is
present — `dinput8.dll` (ASI loader), `AdvancedHookV.dll`, `EasyHook.dll` / `EasyHook64.dll` / `EasyLoad64.dll`,
`LMS.Common.dll`, `LMS.PortableExecutable.dll`.

**Fix:** download ScriptHookV from <https://www.dev-c.com/gtav/scripthookv/> and put **`ScriptHookV.dll`** in the game
folder. Take the newest build. Do not replace `dinput8.dll`. It goes in the game *root*, which currently needs an
admin prompt to write to (§1.2).

**What it gets you:** every ELS-enabled car lights up properly, with the six VCF configs below.

### 1.2 The ELS configs are in a folder ELS never reads ✗

- `ELS.ini` says `VcfContainerFolder = pack_default`
- `ELS\pack_default\` **does not exist**
- the six XMLs sit loose in `ELS\`, and are byte-identical (SHA-256 compared) to `VCF FILES/pack_NB-DSQ/POLICE.xml`
  from `[ELS] Los Santos Police Vanilla Pack.7z`

**Fix, either:**

```
A) create  ELS\pack_default\   and move POLICE.xml … POLICE6.xml into it      (matches the ini as-is)
B) create  ELS\pack_NB-DSQ\    and move them there, then set
   VcfContainerFolder = pack_NB-DSQ                                           (what the pack author intends)
```

B is better if you add the County Pack VCFs later — they use `pack_NB-DSQ` too.

### 1.3 The game root folder is locked to Administrators ⚠

`D:\Grand Theft Auto V Legacy` is owned by `BUILTIN\Administrators` and gives your account only **Read & Execute**:

```
BUILTIN\Users                      ReadAndExecute, Synchronize   Allow
NT AUTHORITY\Authenticated Users   ReadAndExecute, Synchronize   Allow
BUILTIN\Administrators             FullControl                   Allow
```

So nothing can be written into the root without an admin prompt — not a DLL, not a config. Changing that needs admin
too. This is the likeliest reason mods keep ending up half-installed: a file destined for the root quietly fails to be
replaced, leaving the old copy in place while everything else looks right. `Plugins\LSPDFR` is **not** affected (it
grants `Users: FullControl`), which is why TextDispatch and the plugin installs work normally.

**Fix, one elevated command:**

```
icacls "D:\Grand Theft Auto V Legacy" /grant "kenne:(OI)(CI)M"
```

### 1.4 Policing Redefined, StopThePed, Ultimate Backup and CompuLite conflict ⚠

From Policing Redefined's own download page:

> This is a replacement for StopThePed and Ultimate Backup. They cannot be used **with** this mod. A lot of plugins
> require STP/UB and cause crashes without them. They will need to update. **Compulite does not work with this.**

You load all four. It is running today, but it is an unsupported combination. The catch: **ManiacCallouts needs
StopThePed** (hard reference — that was the 14:43 crash). So:

| Option | Keep | Remove |
|---|---|---|
| **A — keep StopThePed** | StopThePed, CompuLite, Ultimate Backup, ManiacCallouts, Traffic Policer | `PolicingRedefined.dll` + `Plugins\LSPDFR\PolicingRedefined\` (keep CommonDataFramework — EPC and Riskier Traffic Stops need it) |
| **B — keep Policing Redefined** | PolicingRedefined, CommonDataFramework, ExternalPoliceComputer, Riskier Traffic Stops | StopThePed, CompuLite, Ultimate Backup, ManiacCallouts |

### 1.5 Optional integrations still missing (nothing is broken without them)

| Missing | What it would add | Who wants it |
|---|---|---|
| **VocalDispatch** | Voice commands ("10-97", request backup) | StopThePed, Ultimate Backup |
| **PoliceSearch** | Ped/vehicle search menus | StopThePed, Ultimate Backup |
| **British Policing Script** | Court, insurance, prisoner processing | Traffic Policer, PoliceSmartRadio |
| **RagePROletariat** | BetterEMS's menu library | BetterEMS (if you put it back) |
| **ImmersiveAmbientEvents** | Ambient events | Riskier Traffic Stops |
| **GrammarPolice** | Voice control | Callout Interface (if you put it back) |

---

## 2. Every mod installed

### 2.1 Platform — the things everything else sits on

| Mod | Where | Needs | Status |
|---|---|---|---|
| **RAGE Plugin Hook** 1.131.1424.17745 | `RAGEPluginHook.exe` | — | ✓ F4 console, 10 s plugin timeout |
| **LSPDFR** 0.4.9695 (0.4.9) | `Plugins\LSPD First Response.dll` | RPH | ✓ |
| **ScriptHookV's ASI loader** | `dinput8.dll` | — | ✓ loads ASI mods (build May 2 2015) |
| **ScriptHookV** | `ScriptHookV.dll` | — | ✗ **absent — ELS needs it (§1.1)** |
| **AdvancedHookV** + EasyHook/EasyLoad64 + LMS libraries | game root | — | ✓ (ELS's hook; works, ELS itself does not) |
| **OpenIV** | `OpenIV.asi` + `mods\` folder | — | ✓ loaded, mods folder active |
| **openCameraV** | `openCameraV.asi` | — | ✓ loaded |
| **ScriptHookVDotNet** | `scripts\` + `ScriptHookVDotNet.asi` | — | ✗ not installed — so no .NET script mods |

RAGE Plugin Hook and LSPDFR also drop these support libraries in the root; none of them is a mod you installed:
`SlimDX.dll`, `Gwen.dll`, `Gwen.UnitTest.dll`, `FW1FontWrapper.dll`, `Mono.Cecil.dll` + `.Mdb`/`.Pdb`/`.Rocks`,
`System.ValueTuple.dll`, `Newtonsoft.Json.dll`, `discord-rpc.dll`, `DiscordRpcNet.dll`, `cursor_32_2.png`,
`DefaultSkin.png`, `Microsoft.Expression.Drawing.dll`, `Microsoft.VisualStudio.QualityTools.UnitTestFramework.dll`,
`DdsConvert.dll`, `NvPmApi.Core.win64.dll`, `XInput1_4.dll`, `RPH_Readme.txt`, `RAGEPluginHook - Shortcut.lnk`.

### 2.2 ASI mods

| Mod | Files | Needs | Status |
|---|---|---|---|
| **Emergency Lighting System 1.05** | `ELS.asi`, `ELS.ini`, `ELS\` | **ScriptHookV ✗**, AdvancedHookV ✓, a VCF folder ELS can find ✗ | **does not load (§1.1), and its configs are in the wrong folder (§1.2)** |
| **openCameraV** | `openCameraV.asi` | — | ✓ |
| **OpenIV** | `OpenIV.asi` | — | ✓ |

### 2.3 RagePluginHook-level plugin

| Plugin | Version | Where | Why it is here | Status |
|---|---|---|---|---|
| **Damage Tracker Framework** | 2.0.2 | `Plugins\DamageTrackingFramework.dll` + `DamageTrackerLib.dll` (root) | **required by Policing Redefined** | ✓ RPH loads it at startup (16:43:49) |

### 2.4 LSPDFR plugins — `Plugins\LSPDFR` (17)

| Plugin | Version | Needs | Status |
|---|---|---|---|
| **686 Callouts** | 2.1.2.0 | RAGENativeUI ✓ · *optional*: StopThePed ✓, ExternalPoliceComputer ✓, Riskier Traffic Stops ✓, Callout Interface (removed) | loads |
| **Common Data Framework** (CDF) | 1.0.0.10 | a record API other plugins use | loads |
| **CompuLite** | 1.5.2.8 | RAGENativeUI ✓, LiteDB (embedded ✓) · *optional*: Traffic Policer ✓ | loads · **conflicts with PR §1.4** |
| **External Police Computer** | 2.0.1.0 | Newtonsoft.Json ✓, CDF ✓, IPT.Common ✓, CalloutInterfaceAPI ✓ · *optional*: Callout Interface (removed) | loads |
| **ManiacCallouts** | 1.3.0.0 | **StopThePed ✓ (required)** | loads |
| **MizCallouts** | 1.0.1.0 | RAGENativeUI ✓ | loads |
| **Plain Sight** | 1.1.3.0 | LSPDFR only | loads |
| **PoliceSmartRadio** | 2.0.0.0 | Albo1125.Common ✓, RAGENativeUI ✓, its data folder ✓ **installed** · *optional*: British Policing Script ✗ | loads (game-version warning, §4) |
| **Policing Redefined** | 1.0.0.5 | RAGENativeUI ✓, CDF ✓, Damage Tracker ✓ | loads · **conflicts with StopThePed/UB/CompuLite §1.4** |
| **Riskier Traffic Stops** | 3.4.0.0 | CDF ✓, RAGENativeUI ✓, Newtonsoft.Json ✓ · *optional*: ImmersiveAmbientEvents ✗ | loads |
| **SSStuart Callouts** | 0.1.2.0 | CalloutInterfaceAPI ✓ | loads |
| **StopThePed** | 4.9.5.4 | RAGENativeUI ✓ · *optional*: Ultimate Backup ✓, PoliceSmartRadio ✓, VocalDispatch ✗, PoliceSearch ✗ | loads · **conflicts with PR §1.4** |
| **Story Callouts** | 0.1.2.0 | CalloutInterfaceAPI ✓ | loads |
| **TextDispatch** | 1.0.12.0 | LSPDFR ✓ · *optional*: Ollama ✓, LM Studio | loads, box drawn |
| **Traffic Policer** | 7.0.0.0 | Albo1125.Common ✓, RAGENativeUI ✓, scanner audio ✓ (163 files) · *optional*: British Policing Script ✗ | loads (2019 mod — §4) |
| **Ultimate Backup** | 1.8.7.1 | RAGENativeUI ✓ · *optional*: VocalDispatch ✗, PoliceSearch ✗ | loads · **part of the PR conflict §1.4** |
| **UnitedCallouts** | 1.5.8.2 | RAGENativeUI ✓ | loads |

Each plugin's own data folder is installed alongside it: `686Callouts\`, `CompuLite\`, `MizCallouts\`,
`PolicingRedefined\`, `RiskierTrafficStops\`, `StopThePed\`, `UltimateBackup\`, `UnitedCallouts\`,
`CommonDataFramework\`, `PoliceSmartRadio\`, `VocalDispatch\`.

### 2.5 Mod content inside `lspdfr\`

Scanner audio that came with mods — all present:

| Pack | Files |
|---|---|
| `Traffic Policer Audio` | 163 |
| `PolicingRedefinedAudio` | 156 |
| `686Callouts Audio` | 80 |
| `EMSAUDIO` (BetterEMS — plugin removed, §2.7) | 43 |
| `Maniac Callouts` | 22 |
| `UnitedCallouts Audio` | 21 |
| `Arrest Manager Audio` (plugin removed, §2.7) | 4 |

Plus LSPDFR's own scanner set and cop voice sets (`AREAS`, `CRIMES`, `CAR_MODEL`, `STREETS`, `s_m_y_cop_01_*`, …) and
LSPDFR's own data (`lspdfr\data\`: agency, backup, stations, regions, outfits, duty selection, `ai\`, `custom\`).

### 2.6 Vehicles, liveries and ELS configs

**Installed**

- `mods\update\x64\dlcpacks\patchday25ng\dlc.rpf` (129 MB) — **12 ELS police models**: `police.yft`, `police2.yft` …
  `police6.yft` plus their `_hi` versions, from `[ELS] Los Santos Police Vanilla Pack.7z`. Models ✓, but they only
  light up once §1.1 and §1.2 are done.
- `mods\update\update.rpf` (1.9 GB) — the OpenIV mods folder, in use ✓.
- `ELS\POLICE.xml` … `POLICE6.xml` — their configs, in the wrong folder (§1.2).

**Downloaded, not installed**

| Archive | What it is | To install |
|---|---|---|
| `County Pack.7z` | ELS Vanilla Police Pack — COUNTY: county models + meta + VCFs (`SHERIFF`, `SHERIFF2-4`, `PRANGER`, red/blue variants) | models → an OpenIV dlcpack (`patchday13ng` or `mpchristmas2` per its readme); VCFs → `ELS\pack_NB-DSQ\` + `VcfContainerFolder = pack_NB-DSQ` |
| `Patch 1.7z` | VCFs for `POLICEINSURGENT`, `PREDATOR`, `RIOT` | same ELS pack folder as §1.2 |
| `0bb177-AFProj_Yankton.rar` | **AFNYSP** — dlc of NYSP/Yankton police vehicles + uniforms | `AFNYSP` → `mods\update\x64\dlcpacks\`, add `<Item>dlcpacks:\AFNYSP\</Item>` to `dlclist.xml` in `mods\update\update.rpf\common\data\`; its readme also requires **SSLA V2** ✗ and a **300-car gameconfig** ✗. Do it last, and back up `update.rpf` first. |
| `bddb84-NYSP_Liveries.rar` | 6 PNG livery **templates** | not installable as-is — artwork for making `.ytd` textures |

### 2.7 Leftovers from mods you removed — safe to delete, or keep if you reinstall

| Leftover | Belongs to | Note |
|---|---|---|
| `Plugins\LSPDFR\EMSmod\` + `lspdfr\audio\scanner\EMSAUDIO\` | BetterEMS | its DLL is gone |
| `lspdfr\audio\scanner\Arrest Manager Audio\` | Arrest Manager (or LSPDFR+) | DLL gone |
| `Plugins\ParksCommon\`, `ParksTools.dll`, `ParksTools.pdb` | BetterEMS / PNWParksFan tools | |
| `IPT.Common.dll`, `RawCanvasUI.dll`, `CalloutInterfaceAPI.dll` | Callout Interface | orphans at the root |
| `Plugins\LSPDFR\VocalDispatch\`, `lspdfr\VocalDispatch\` | VocalDispatch | plugin never installed |
| `READ ME.txt`, `RNUI LICENSE.md` | Riskier Traffic Stops | its docs |
| `Installation + Details.html` | 686 Callouts | its docs |
| `LICENSE.md`, `NOTICE.md` | RAGENativeUI | its package docs |
| `startup.rphs.bak`, `lspdfr_uinst.exe`, `index.bin` | LSPDFR installer | |
| `My-Mods-README.md` | this file | the copy in the game folder is stuck at the 15:41 version — §1.3 |

### 2.8 Not installed — removed, or never had

| Mod | State |
|---|---|
| **Callout Interface 1.4.1** | **removed at ~16:45** while hunting the crash. It was never the problem — its archive ships the *good* RAGENativeUI 1.9.3.0. Worth putting back: 686 Callouts, SSStuart, Story Callouts and External Police Computer all reference it. |
| **LSPDFR+ 1.9.0.0** | removed. One of the two that shipped the bad RAGENativeUI 1.6.3.0 — reinstall only if you skip that file. |
| **BetterEMS 4.1b** | removed, same reason as LSPDFR+. |
| **Arrest Manager 7.11.0.0** | downloaded 16:09, DLL not present now. |
| **VocalDispatch**, **PoliceSearch**, **British Policing Script**, **GrammarPolice**, **ImmersiveAmbientEvents**, **RagePROletariat** | never installed — see §1.5 |
| **ScriptHookVDotNet** | never installed |
| **Riskier Traffic Stops** dependencies | CDF ✓ already installed |

---

## 3. Check it yourself

| What | Where |
|---|---|
| Live plugin list, and what each one is missing | `/plugins` in the chat box, or `tdplugins` in the F4 console |
| The same thing, written automatically | `Plugins\LSPDFR\textdispatch.log` — the `inventory:` block, a few seconds after going on duty |
| What LSPDFR loaded, and when | `RagePluginHook.log` and `Logs\` — `Creating plugin:` lines appear **only after you go on duty (E at a police station)** |
| What ASI mods loaded | `asiloader.log` |
| Crashes | `MiniCrashReports\` (8 reports: 14:43, plus 7 between 15:38 and 16:44 — all the RAGENativeUI error, now fixed) |

---

## 4. Rules that keep this working

- **Go on duty.** LSPDFR loads `Plugins\LSPDFR` when you press E at a police station, and not before. Waiting in story
  mode loads nothing, however long you wait.
- **`Plugins\LSPDFR`, never `Plugins\` or `lspdfr\`.** Plugins in `Plugins\` get their own AppDomain from RPH and
  cannot see LSPDFR; plugins in `lspdfr\` are never opened at all. `/plugins` reports anything in the wrong place.
- **Never copy `RAGENativeUI.dll` out of `Arrest Manager`, `LSPDFR+` or `BetterEMS`.** All three ship the 2016
  1.6.3.0 build and overwrite the 1.9.x that everything else needs. That one mistake caused every crash from 15:38 to
  16:44. Copy the plugin DLL, its ini and its folder — nothing else.
- **The root folder needs admin** (§1.3), so root-level files need a UAC prompt until that is changed.
- **Traffic Policer 7.0.0.0, LSPDFR+ 1.9.0.0 and PoliceSmartRadio 2.0.0.0 are 2019-era**, kept alive by community
  patches. They log a *compatibility warning* because your build is newer than the 1.0.3521.x they were written for —
  expected, not a fault.
- **LiteDB and random-string "missing" warnings are false alarms** — embedded inside `CompuLite.dll` via Costura, and
  ConfuserEx-obfuscated names. TextDispatch filters both.

### Fixed today, for reference

| Was | Now |
|---|---|
| RAGENativeUI **1.6.3.0** at the root → `TypeLoadException: Could not load type 'RAGENativeUI.InstructionalKeyExtensions'` → `[FATAL] Forced termination`, seven sessions in a row | **1.9.3.0** (291,328 bytes), the only copy in the tree. All six plugins that wanted 1.9.2/1.9.3 now match: Policing Redefined, Riskier Traffic Stops, StopThePed, Callout Interface, Traffic Policer, Ultimate Backup |
| `Plugins\LSPDFR\PoliceSmartRadio\` empty → `Couldn't find the required audio file … ButtonSelect.wav`, `… Config\GeneralConfig.ini` | 151 files: `Audio\` (48 wavs), `Config\` (5 inis), `Display\` |
| `DamageTrackerFramework.dll` still in the zip; not running | in `Plugins\` and loaded by RPH via `startup.rphs` |
| `UltimateBackup.dll` in `lspdfr\` → never loaded | in `Plugins\LSPDFR\`, loading (`UltimateBackup 1.8.7.1 - loaded`) |

No session has run since the RAGENativeUI fix, so it is verified by reading the files back, not yet in game.

---

## 5. Sources

- LSPDFR — <https://www.lcpdfr.com/downloads/gta5mods/g17media/7792-lspd-first-response/>
- RAGE Plugin Hook — <https://ragepluginhook.net/>
- ScriptHookV — <https://www.dev-c.com/gtav/scripthookv/>
- Damage Tracker Framework — <https://www.lcpdfr.com/downloads/gta5mods/scripts/42767-damage-tracker-framework/>
- Riskier Traffic Stops — <https://www.lcpdfr.com/downloads/gta5mods/scripts/44036-riskier-traffic-stops/>
- 686 Callouts — <https://www.lcpdfr.com/downloads/gta5mods/scripts/37390-686-callouts/>
- Callout Interface — <https://www.lcpdfr.com/downloads/gta5mods/scripts/37828-callout-interface/>
- ExternalPoliceComputer — <https://www.lcpdfr.com/downloads/gta5mods/scripts/45400-externalpolicecomputer/>
- Policing Redefined — <https://www.lcpdfr.com/downloads/gta5mods/scripts/52191-policing-redefined/> · docs <https://policing-redefined.netlify.app/>
- Common Data Framework — <https://github.com/Policing-Redefined/CommonDataFramework>
- IPT.Common — <https://github.com/Immersive-Plugins-Team/IPT.Common>
- TextDispatch — <https://github.com/kennethyork/TextDispatch>
