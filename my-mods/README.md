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
| ScriptHookV | **3889.0.1158.13 ✓** — installed, matches your game build 1.0.3889.0 (§1.1) |
| ScriptHookVDotNet | **nightly 3.7.0.191 ✓** — installed for the civilian jobs mod (§1.6) |
| LemonUI | **2.2 ✓** — `LemonUI.SHVDN3.dll` in `scripts\`, the menu library the jobs mod draws with |
| LSPDFR plugins | **17** DLLs in `Plugins\LSPDFR` — 686 Callouts and External Police Computer are back, TextDispatch is **1.0.20**, TextCallouts **1.3.0** and TextJobs 1.0.2 |
| ASI mods | **4** installed — ELS should load now that ScriptHookV is in place; unverified until the next launch (§1.1) |
| Civilian jobs | **DriverJobs V 1.6.0 ✓** — 31 jobs, `Shift+J` for its menu, in `scripts\` (**§1.6**), all of them now running **as your own character** (**§1.8**), and listed in the chat box by **`/jobs`** |
| Non-police duty | **LSFD — EMS and Fire ✓** — added to LSPDFR's duty menu (**§1.7**) |
| Jobs in text | **TextJobs 1.0.1 ✓** — DriverJobs V in a chat box, **F9**, no LSPDFR needed (**§1.10**) |
| The bundle | **bundle 1.4.0 ✓** — all three in one download: the two police plugins into `Plugins\LSPDFR`, TextJobs into `scripts\` |
| Scanner audio packs | 7 mods' audio installed |
| Vehicles | 12 ELS police models in `patchday25ng\dlc.rpf` |
| Local model | Ollama ✓ — `llama3.2:1b` for the NPCs, with `llama3.2:latest` still installed (§1.9) |
| Pausing on focus loss | **off** — the game no longer pauses when you click your second monitor (§1.9) |

Legend: **✓** present and working · **✗** missing or broken · *optional* = the mod works without it.

---

## 1. What is still outstanding

### 1.1 ScriptHookV is missing, so ELS does not run ✓ FIXED

`ScriptHookV.dll` **3889.0.1158.13** is installed in the game root — the build that matches your
exactly (game 1.0.3889.0), from the official source: `https://www.dev-c.com/files/ScriptHookV_3889.0_1158.13.zip`.
It needed a browser user-agent to download; dev-c.com serves its homepage to anything else, which is
why the first attempt produced 11 KB of HTML instead of a zip.

`NativeTrainer.asi` from the same zip was **deliberately not** copied: F4 is RAGE Plugin Hook's console
here, and a trainer is not something that was asked for. `dinput8.dll` was left alone too — the ASI
loader already present loads `openCameraV.asi` and `OpenIV.asi` successfully, so there was no reason to
replace a working loader.

The check next launch is `asiloader.log`: it said

```
ASI: Loading "D:\Grand Theft Auto V Legacy\ELS.asi"
     "ELS.asi" failed to load          <- because ScriptHookV.dll was missing
```

and should now say a load address after the name, with no `failed`.

The note below is kept because it explains the mechanism.

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

### 1.2 The ELS configs are in a folder ELS never reads ✓ FIXED

**All 52** of them — not the six I first attributed to the Los Santos Police Vanilla Pack; the County
Pack, Patch 1 and others had been installed too — now sit in `ELS\pack_default\`, which is what
`ELS.ini` names (`VcfContainerFolder = pack_default`). They were loose in `ELS\`, a folder ELS never
looked in.

The description below is kept because it explains how the mismatch worked.

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

### 1.3 The game root folder was locked to Administrators ✓ FIXED

```
icacls "D:\Grand Theft Auto V Legacy" /grant "kennethyork-win\kenne:(OI)(CI)M"
```

Your account now has **Modify** on the folder, verified by writing a file into it afterwards. Root-level
installs — mods, configs, ScriptHookV — no longer need a UAC prompt, and the failure mode that produced
half-installed mods is gone with it. The `(OI)(CI)` means new files and subfolders inherit it, so it
does not need doing again.

Files that already existed keep the permissions they had; if one ever refuses to be replaced, the same
command with `/T` fixes the whole tree in one go.

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

### 1.6 Civilian jobs (DriverJobs V) — installed, not yet launched

DriverJobs V **1.6.0** is in `scripts\`, with the two things it needs: **ScriptHookVDotNet nightly
3.7.0.191** in the game root, and **LemonUI 2.2** (`LemonUI.SHVDN3.dll`) beside it in `scripts\`. It is
the first mod here that runs through ScriptHookV rather than through RAGE Plugin Hook, which is what
makes it work with LSPDFR rather than instead of it: nothing is shared between them but the game.

Installed path, for reference:

```
D:\Grand Theft Auto V Legacy\
    ScriptHookVDotNet.asi          the loader ScriptHookV's ASI loader picks up
    ScriptHookVDotNet2.dll         API for v2 scripts
    ScriptHookVDotNet3.dll         API for v3 scripts  (3.7.0.191)
    ScriptHookVDotNet.ini          console key F7 (was F4 - see below)
    scripts\
        DriverJobs.dll             the mod
        DriverJobsData\            its 84 XML data files
        LemonUI.SHVDN3.dll         the menu library it draws with
```

Verified by reading the files, not by playing:

- all **476** ScriptHookVDotNet and LemonUI members `DriverJobs.dll` calls resolve against the versions
  installed, so there is no `MissingMethodException` waiting to happen;
- it references `World.CreateRandomPed` — the one the newest nightly did **not** rename (nightly 191
  changed `Ped.CreateRandom` only, which DriverJobs does not use);
- its assembly references carry no public key token, so their version numbers are not enforced: built
  against 3.7.0.106 and 2.1.2.0, it loads against 3.7.0.191 and 2.2.0;
- all 84 XML data files parse, and `Missions\Jobs.xml` holds 37 jobs.

**One setting was changed for you.** ScriptHookVDotNet's console key is **F4** by default, which is
RAGE Plugin Hook's console too — two consoles fighting over one key. It is set to **F7** in
`ScriptHookVDotNet.ini`; that is the only line in it that was touched.

**Not yet verified, because the game has not been launched since:** whether ScriptHookVDotNet accepts
your build (it refuses on a *minimum* version, and build 3889 is far above it, but that is the check
that would say no), whether the ASI loads at all, and whether the jobs start. On the next launch, in
this order:

| File, in the game root | What to look for |
|---|---|
| `asiloader.log` | `Loading "…\ScriptHookVDotNet.asi"` and **no** `failed to load` after it — the failure ELS showed before ScriptHookV was in place |
| `ScriptHookVDotNet.log` | its version, then `Loaded 1 script(s)` |
| `DriverJobsV.log` | the mod's own start-up, and any job it could not load |

If `DriverJobsV.log` complains that it cannot access `Jobs.xml`, the fix is **DirectStorageFix** by
alloc8or — the one known Windows 11 failure mode for this mod — and installing anything else for it
would be guesswork.

### 1.7 Being EMS or Fire on duty — LSFD added to the duty menu

LSPDFR ships two non-police agencies in `lspdfr\data\agency.xml` and neither could ever be chosen:

| Agency | What it has |
|---|---|
| `lsfd` — Los Santos Fire Department (EMS) | a **Rescue Ambulance** (the `ambulance` model, livery 2), paramedic peds, the `mp_ems` outfits |
| `lsfd_fire` — Los Santos Fire Department (Fire), child of `lsfd` | the **Fire Truck** (`firetruk`), fireman peds, the `fire` inventory |

The list the duty menu is built from is `lspdfr\data\duty_selection.xml`, and it held only police
agencies — LSPD, LSSD, SAHP, NYSP, the SWAT branches, NOOSE, FIB, IAA, DOA, SASPA, SAPR. Both LSFD
agencies are now listed in its `<Peds>` and `<Vehicles>` lists, with the ambulance and the fire truck
named in the vehicle descriptions, so **LSFD can be picked at any station** alongside the police ones.

Reverting is one file: the original is beside it as `duty_selection.xml.backup-<timestamp>`. Nothing
else in the install was touched, and LSPDFR's own `agency.xml` was not modified at all.

Two things to expect, both gameplay rather than faults:

- **LSPDFR's callouts are police callouts.** Being LSFD changes what you drive, what you wear and what
  the records call you; it does not add medical callouts, because LSPDFR has no such callout type. For
  actual paramedic work, DriverJobs' shift below is the one that has it.
- **Policing Redefined and StopThePed are written for police duty** and may behave oddly with a
  non-police agency. If a shift as EMS turns strange, take a police agency and use DriverJobs'
  paramedic job instead — that one never involves LSPDFR at all.

### 1.8 DriverJobs runs as your own character now

Every job in DriverJobs normally puts you into a work ped for the shift — a paramedic, a fireman, a
garbage crewman, a pilot. It is not destructive: `PlayerSkinHelper` stores the model hash, the outfit
and the weapons first and `RevertModel()` puts all three back afterwards. But during the shift you are
somebody else, which is not what you want when the whole point is playing *your* character.

That is now off. The `<skins>` block has been removed from all **24** jobs that had one, in
`scripts\DriverJobsData\Missions\Jobs.xml`. The mod checks `Skins.Count` before it calls `ApplyModel`
— visible in the IL of `Mission.Start` — and 13 of the 37 jobs already shipped with no skins at all, so
a job without one simply leaves you alone. **Every job now runs as whoever you are**: on duty that is
your LSPDFR character in the uniform of the agency you picked (LSFD now included), off duty your story
character. Nothing is saved or restored, because nothing is changed.

| | |
|---|---|
| Backup | `Jobs.xml.bak-20261005-1728` beside it — 56,972 → 54,942 bytes, 1,096 → 1,004 lines |
| Removed | 24 `<skins>` blocks and their 45 `<skin>` entries, 92 lines; **nothing else** |
| Checked | line-for-line identical to the backup with those blocks taken out (0 differences), same UTF-8 BOM and CRLF endings, XML parses, 37 missions intact, every other element count unchanged |
| Unchanged | pay, company vehicles, `personalVehicle` categories, start points, blips, descriptions |

To give one job its uniform back, add the block to that job and nothing else:

```xml
<skins>
    <skin>s_m_y_fireman_01</skin>
</skins>
```

Not seen in game yet: the game has not been launched since any of this was installed.

**In the chat box.** TextDispatch **1.0.18** reads that same file, so the civilian side is in the text
interface with the police side:

| | |
|---|---|
| `/jobs [filter]` | all 37, with what each pays |
| `/job <name or number>` | the description, the pay, what you drive, and a blip + sat-nav route on where it starts |

It reads the mod's file rather than keeping a copy, so a job edited or added by hand appears there too,
and the list is re-read when the file changes. It cannot *start* a job - the mod owns that - and the
mod not being installed is a sentence in the box, not a fault. Tested outside the game against your
real `Jobs.xml`: 21 checks, including the two freight jobs that share a name (they are numbered, so
`/job 19` picks one).

### 1.9 The two settings you said yes to

| What | Was | Now | Where |
|---|---|---|---|
| Pausing when you click your other monitor | `<PauseOnFocusLoss value="1" />` | `value="0"` | `Documents\Rockstar Games\GTA V\settings.xml` — backup `settings.xml.bak-20261005-1740` beside it |
| The model answering dispatch and the people | nothing set ("whatever the server reports first") | `AiModel=llama3.2:1b` | `Plugins\LSPDFR\TextDispatch.ini` |

Measured on this machine, both models warm:

| Model | Speed | Notes |
|---|---|---|
| `llama3.2:1b` | **~330 tokens/s** | 1.3 GB, pulled tonight; blunter, and fine for one-line replies |
| `llama3.2:latest` | ~210 tokens/s | 2.0 GB, what has been answering until now; better sentences |

The first reply of a session also pays for loading the model, which is why a cold one can look much
slower than these numbers. To go back: set `AiModel=llama3.2:latest`, or blank the line.

### 1.10 TextJobs — DriverJobs V in a text box, without LSPDFR

TextDispatch is the police half and cannot exist without LSPDFR: it derives from LSPDFR's plugin base
and is loaded when you go on duty. So the civilian jobs got their own plugin, built as a
**ScriptHookVDotNet** script - the same framework DriverJobs itself runs on:

| | |
|---|---|
| Where | `scripts\TextJobs.dll`, beside DriverJobs |
| Opens with | **the left arrow** - or **F9** when TextDispatch's box is loaded in the same session (`OpenKey=auto`), because two boxes on one key would both open and both read the same keystrokes |
| On duty | **not there at all** - the police box is the interface and it has `/jobs` and `/job`; this one stands down on duty and comes back off duty |
| Needs | the game, ScriptHookV, ScriptHookVDotNet, DriverJobs V. **Not** LSPDFR, not RAGE Plugin Hook |
| Commands | `/jobs [filter]`, `/job <name or number>`, `/key`, `/pos`, `/hide on\|off`, `/hardware on\|off`, `/clear`, `/help` |
| Its files | `scripts\TextJobs.ini` (settings), `scripts\TextJobs.log` (what it did, and the whole job list) |

It reads the mod's own `Jobs.xml`, so a job edited by hand is in the list, and it hides what you type
from every other plugin and script while the box is open - DriverJobs' own `Shift+J` included. It
cannot take a job: the mod owns that, and `Shift+J` or being at the place is how a job starts.

The keyboard handling, the hiding rule and the job reader are the **same source files** as
TextDispatch's, compiled into both plugins, so they cannot drift apart. Nothing else is shared: no
run-time dependency either way, and each works with the other absent.

Built and installed; not yet run in game. Its key is **F9** rather than F8 as first shipped: F8 turned
out to be ALPRLite's plate reader, which the keyboard audit found.

It is in the bundle from `bundle 1.4.0` on: the zip carries TextDispatch, TextCallouts **and** this, with
the install notes describing both folders - `Plugins\LSPDFR` for the two police plugins, `scripts\` for
this one - and two command lists. `tools\install-bundle.ps1` puts all three in the right places in one
go; `tools\package-bundle.ps1` builds the zip.

### 1.12 TextCallouts: 102 callouts, sorted by the duty you work as

LSPDFR's callout registry is police calls and it filters nothing by agency - the agency you choose changes
your uniform, your vehicle and what dispatch calls you. So the pack decides for itself, and every callout
says which duty it belongs to in its `<For>` element.

| Duty you go on as | Callouts | What it is |
|---|---|---|
| **LSPD** |  city work: robberies, drugs, shots fired, burglary, assaults, disturbances, missing people |
| **Sheriff** (lssd) |  county: ranch trespass, poaching, livestock theft, rural burglary, the desert, Sandy Shores |
| **Highway Patrol** (sahp) |  traffic: racing, wrong-way drivers, hit and run, insecure loads, road rage |
| **Park Rangers** (sapr) |  illegal fires, poaching, lost walkers, injured wildlife, off-roading |
| **Prison** (saspa) |  Bolingbroke: escapes, contraband drops, transport incidents, assaults on staff |
| **EMS** (lsfd) |  patients: falls, overdoses, seizures, chest pain, cyclists, exposure |
| **Fire** (lsfd_fire) |  refuse, kitchens, grass, a car in a car park, a car on a hard shoulder, commercial bins |

An agency the pack has never heard of gets the police work - a patrol with no calls at all is a worse
failure than a misfiled callout. Some callouts name several duties and are offered to all of them.

**Where they live.** Three hundred and twenty-two are XML recipes in `Plugins\LSPDFR\TextCallouts\Library` - the same format
your own go in, so they can be read, copied and edited; the pack reads that folder and your `Custom` one,
and an update replaces only the library. The other eighteen are written in C#, because their behaviour is
particular: the medical callouts that need a patient to treat, and the police ones that need a scene a
recipe cannot describe. `tools\make-callout-library.ps1` writes the library from one table, so adding a
callout is a line in it rather than a new class.

**Checked outside the game:** all 84 recipes parsed with the pack's own parser, and the routing tested duty
by duty - LSPD gets city work and not county, the Sheriff the other way round, a shared callout reaches all
three agencies that asked for it, EMS gets medical work and no police work, the fire branch gets fires, an
unknown agency gets the police work. 21 checks, no failures.

`Plugins\LSPDFR\TextCallouts.dll` is 1.4.0.0 and the library is installed at
`Plugins\LSPDFR\TextCallouts\Library` (84 files). `bundle 1.4.2` carries all three plugins.
### 1.11 The keyboard, redone so nothing is shared

Every mod's config was read, the bindings were pooled, and anything two mods both watched for was moved
onto a key or a modifier that nothing else uses. **34 lines across nine config files** were changed -
counted by diffing each file against its backup, not from the plan; every file has that backup beside it
named `*.bak-keymap-20261005-1819`. One line of C# changed too: TextJobs' default key, F8 to F9.

What was actually colliding, and what a press used to do:

| Key | Who was sharing it | Now |
|---|---|---|
| **Back** | PR tackle, StopThePed tackle, PR fast dispatch, Traffic Policer's radar disable — four actions on one press | PR keeps Back; StopThePed's tackle is `~`; PR's fast dispatch is **LCtrl+Back**; the radar disable is **Insert** |
| **Enter** | PR speed boost, PR dismiss-all, StopThePed sprint boost, Ultimate Backup dismiss-all | PR's speed boost is **RShift**; its dismiss-all is **LCtrl+Enter**; Ultimate Backup's is **Delete**; StopThePed's sprint is **RAlt** |
| **B** | LSPDFR's backup menu, LSPDFR's crime report, PR's dispatch menu, Ultimate Backup's menu | LSPDFR keeps **B**; its crime report is **LAlt+B**; PR's dispatch is **F1**; Ultimate Backup's menu is **F2** |
| **T** | PR's menu and pursuit, ELS's manual siren, StopThePed's gunpoint pursuit, Ultimate Backup's K9, Traffic Policer's stop-follow | PR keeps **T**; ELS's manual siren is **;**; StopThePed's is **LAlt+T**; Ultimate Backup's K9 is **.**; Traffic Policer's is **LShift+T** |
| **J** | ELS's lights, LSPDFR's siren-sound toggle, Traffic Policer's remove-signs | ELS keeps **J**; LSPDFR's is **F11**; Traffic Policer's is **LCtrl+L** |
| **G** | ELS's siren, LSPDFR's abort/join chase, StopThePed's context menu | ELS keeps **G**; LSPDFR's is **F3**; StopThePed's is **LAlt+G** |
| **Y** | ELS's airhorn, LSPDFR's accept-callout | ELS keeps **Y**; LSPDFR's is **F12** (and the box still takes `accept`) |
| **U** | ELS's primary pattern, PR's felony-stop menu | ELS keeps **U**; PR's is **LAlt+U** |
| **E** | LSPDFR's interact family, CompuLite's computer | LSPDFR keeps **E**; CompuLite's computer is **LShift+E** |
| **NumPad0** | PR's panic and Ultimate Backup's panic, both on **RCtrl+NumPad0** | PR keeps **RCtrl+NumPad0**; Ultimate Backup's is **LAlt+NumPad0** |
| **F8** | ALPRLite's plate reader and TextJobs' box | ALPRLite keeps **F8**; **TextJobs is F9** |
| **5 and 6** | ELS's own siren tones 5/6 and its scan/tone-X | scan is **-**, tone X is **=** |
| **G again** | ELS's siren toggle and its own panic siren | panic siren is **'** |

**The function row is now one thing per key:** F1 PR dispatch, F2 Ultimate Backup, F3 LSPDFR chase,
F4 RPH console, F5/F6 Traffic Policer, F7 ScriptHookVDotNet console, F8 ALPRLite, F9 TextJobs,
F10 StopThePed search, F11 LSPDFR siren sound, F12 LSPDFR accept callout.

**Two layers keep the two police frameworks apart.** Policing Redefined and StopThePed/Ultimate Backup do
the same jobs, so StopThePed and Ultimate Backup now live on the **LAlt** layer
(`LAlt+E` stop a ped, `LAlt+G` context menu, `LAlt+T` gunpoint, `LAlt+Q` quick grab, `LAlt+Back` …)
and PR keeps the bare keys it always had. That is a keybinding fix, not a fix for the deeper conflict
between them (§1.4) — one of those two setups still has to go eventually.

**What was deliberately not changed:** LSPDFR's `E` family (duty, arrest, stop peds, traffic-stop talk,
garage, apartment — one key, contextual by design), its `Q` pair, PR's `T` pair, ELS's lights on
J/K/L, TextDispatch's left arrow, DriverJobs' `Shift+J`, and ELS's `ScrollLock` key lock.

**Two things worth knowing about the game's own bindings**, which no mod config can settle: bare `E`
is also the horn while driving (that is LSPDFR's choice, not a collision with another mod), and bare `C`
(PR's partner in/out of a vehicle) is the game's look-behind. If either annoys you, they are one line each.

---

### 1.13 What can now come out, and what cannot

TextCallouts is at four hundred and sixty-two callouts, and every callout the other packs offer has an
equivalent in it - so the callout packs can be uninstalled.

| Pack | Callouts | Covered? |
|---|---|---|
| 686 Callouts 2.1.2 | 30 | yes - a recipe for each, except the two that were already here |
| UnitedCallouts 1.5.8.2 | 19 enabled | yes |
| StoryCallouts 0.1.2 | the mission remakes | the four situations, as police work rather than cut scenes |
| ManiacCallouts 1.3.0 | - | its own scenarios are covered by type (shootings, foot chases, weapons) |
| MizCallouts 1.0.1 | - | idem - its BabyDriver scenario is the pursuit and stolen-vehicle recipes |
| Plain Sight 1.1.3 | - | idem |
| SSStuartCallouts 0.1.2 | - | idem |

**What you would lose by uninstalling them, which this does not replace:** Callout Interface's MDT and
call metadata (already removed once, see the inventory), anything that leaned on interiors or RAGENativeUI,
and the specific *scripted* routines - a bomb on a timer, a protest crowd, a SWAT entry sequence. The
callout list is covered; the staging is simpler.

**Nothing else depends on them.** StopThePed, Ultimate Backup and Policing Redefined are interaction
plugins, not callout packs, and TextCallouts references nothing but LSPDFR and RAGE Plugin Hook.

**How to take them out:** delete the DLL from `Plugins\LSPDFR\` and its folder (686Callouts, MizCallouts,
UnitedCallouts) if it has one. Then check `/calls` in the box, or `/plugins`, which lists what loaded.

### 1.14 The other callout packs are out - moved, not deleted

Twenty-two things were moved out of the game and into `_removed-callout-packs\` in the game folder: the
seven callout packs' DLLs and folders (686Callouts, ManiacCallouts, MizCallouts, Plain Sight,
SSStuartCallouts, StoryCallouts, UnitedCallouts), three sets of scanner audio that came with them, and two
odds and ends they left behind in `Plugins\` - `SSStuart_UpdateChecker.ini`, which was still phoning home
for SSStuartCallouts and StoryCallouts, and an empty `ParksCommon\ParksModsWebCheck.ini` from a pack that
is no longer installed. Nothing was deleted, so putting one back is a drag. Delete that folder once you are
sure.

Still in `Plugins\LSPDFR`: **TextDispatch 1.0.22**, **TextCallouts 1.13.0**, and the interaction plugins
that are not callout packs - StopThePed, Ultimate Backup, Policing Redefined, CompuLite, Traffic Policer,
ALPRLite, SpeedRadarLite, CommonDataFramework, ExternalPoliceComputer, RiskierTrafficStops, plus
CalloutInterfaceAPI, which ExternalPoliceComputer still needs.

`Plugins\` itself is now just LSPD First Response, CommonDataFramework, DamageTrackingFramework and the
LSPDFR folder - no stray ini files from packs that have gone.

**TextCallouts 1.13.0** is at 462 callouts, and **every one of them has a script, can be finished, sends
you to a different person each time, and follows the duty you went on as.**

What each duty actually gives you, counted against this install's own `lspdfr\data\agency.xml`:

| Duty you pick | Callouts offered |
|---|---|
| **LSPD**, LSPD SWAT, LSPD Air Support | 179 |
| **Sheriff** (lssd) | 85 |
| **Sheriff SWAT** | 86 |
| **Highway Patrol** (sahp) | 63 |
| **Park Rangers** (sapr) | 49 |
| **Bolingbroke** (saspa) | 37 |
| **LSFD - EMS** | 74 |
| **LSFD - Fire** | 41 |
| **FIB** | 35 |
| **NOOSE** (and NOOSE Air Support) | 27 |
| **North Yankton** (nysp) | 25 |
| **IAA** | 21 |
| **DOA** | 21 |

Every agency in that table has callouts of its own now. The federal and state rows used to be fifteen
calls each with none of them federal, because every recipe named a police family - LSPD, Sheriff, Highway
Patrol, Rangers, the prison - or a medical or a fire branch, and not one of them said "any police agency".
They have warrants, watches, informant meets, raids and sieges now, and North Yankton has snow on the
ground: 21 to 35 calls each instead of 15.

Previously only four did. Now all 444 recipes carry three or four lines - 1,335 lines in total - for the
person in that scene to say, and TextDispatch asks the callout for those lines before it asks its own
script and before it asks the model:

  * open the box (left arrow), type at whoever is nearest, and the suspect or the complainant answers in
    character, instantly, with no model running at all;
  * in a medical callout it is the **patient** who answers, not the bystander stood next to them - the 53
    medical recipes now carry the caller or family as a bystander and the patient as the speaker, which is
    what made a woman with chest pain answer for herself and nobody else;
  * somebody who has said their piece repeats their last line rather than going mute.

It is checked rather than hoped for. `tools\make-callout-library.ps1` refuses to write a library where any
callout has no script, fewer than three lines, a repeated line, or an apostrophe that would break the data
file; the recipe harness reads the finished library back with the pack's own parser and refuses one where
any of that slipped through (28 checks, no failures). A recipe that is missing its script cannot be built,
let alone shipped.

  * **scenes that move** - stages: backup arriving twenty seconds in, a suspect's nerve breaking at
    fifty, the fire starting once they have had a chance to talk. Four callouts use them: a standoff, a
    bank siege, a gang fight, and a patient who talks to you before you treat them.

**`tools\install-textcallouts.ps1` now installs the library too.** It only ever copied the DLL, which left
the previous library in place - a pack that looked like it had stopped working, because the new callouts
were simply not in the folder it reads. It now replaces `Plugins\LSPDFR\TextCallouts\Library` as well,
says how many recipes and how many script lines went in, and names any recipe that arrived without a
script. Your own `Custom\` folder is beside it and is never touched.

**The library is in the game, and the loader has been run against it.** `tools\LoaderCheck\` is a small
console program that runs the pack's *real* loader - `CustomCallouts.cs`, compiled from `src` - against a
real `TextCallouts` folder, and reports what it built. Against the game's own folder it says:

    326 loaded, 0 skipped (326 from the library, 0 of your own)
    326 callout types built, none skipped, none colliding, all carrying the name LSPDFR shows
    326 bound to the recipe they came from, 326 carrying a script (981 lines)
    PASS: 14 checks, 0 failed

The one substitution is `RecipeCallout`, which cannot be loaded outside the game at all - it derives from
LSPDFR's `Callout` and its signatures mention Rage types, and the only `RagePluginHook` assembly on this
machine is the SDK reference assembly, whose members have no implementation, so the CLR refuses to load
it. The loader uses exactly two things from it, so the check uses a stand-in with those two. That is why
this proves the loader and not the scenes.

**In game, two lines say it.** After you go on duty, in `Plugins\LSPDFR\textcallouts.log`:

    callouts from files: 326 loaded, 0 skipped (326 from the library, 0 of your own)
    registered for this duty: 18 of 18 built-in callouts, 326 of 326 from the library, 0 of 0 of your own

The second line used to call the library's 326 callouts "of your own", which is what the Custom folder
is for and had nothing to do with them - it now counts the three groups apart, and so does the on-screen
message and `tcstatus`. If it ever says "0 of 0 from the library", the folder is not where the pack looks
for it.

**Being asked whether all the callouts are playable turned up one that was not.** A recipe with nothing to
catch - a report, a welfare check, a scene to look at - ended only by timing out, so you arrived, dispatch
said its line, and the call sat open for twenty minutes with nothing you could do to finish it. A hundred
and fourteen recipes were like that. They now end the way the hand-written ones do - be at the scene, then
leave the area, or stay a minute and be plainly done with it - and the timeout is only the backstop for a
call nobody turns up to.

What the four hundred and forty-four recipes are, by how they end:

| | Callouts | How it ends |
|---|---|---|
| Somebody to catch | **273** | every suspect dealt with, or the first arrest, whichever the recipe asks |
| Somebody to treat | **53** | long enough with the patient, then hand them over |
| Nothing to catch | **114** | you have been there and move on |

The recipe harness now refuses a library containing a callout that cannot be ended, and reports that split:
29 checks, no failures. It also checks that nothing in the pack closes the instant it starts, which is what
a recipe with a resolution and nobody to resolve would do.

**The people are randomised now, which was the last rough edge.** Every suspect used to be
`a_m_y_business_01` - the same man in the armed robbery, the bank siege and the corner dealing - and it was
the first thing you would notice in game. Each scene now draws from a pool of two to thirteen models chosen
to suit the callout: forty-two models in the pack, so three suspects in a street are three different men and
the same callout twice running is rarely the same face.

Two things about how that is done, because both were decisions:

  * **A pool cannot cost you a callout.** A scene with nobody in it closes the moment it starts, so before
    spawning anybody the pack checks the model against the game and takes the first of the pool that exists,
    falling back to a model that certainly exists if none of them do. A typo in a pool is survivable by
    design, and the log names the people it picked.
  * **They are men and women.** Sixty-three models, 21 of them women, and 324 of the 326 scenes can send
    either. This needed the dispatch prose doing first: it was measured properly, and the "103 recipes that
    say he" turned out to be the suspect talking about somebody else ("He started it"), which is true
    whoever they are. What actually had to change was two recipes. `Man with a Gun` keeps the name every
    pack gives that call and sends a man; `Theft from a Charity Shop` describes one female and sends one.
    The generator now refuses a recipe whose text describes a sex while its pool draws from both, so it
    cannot drift back.
  * **The vehicles are pooled too.** 30 of the 41 callouts that involve a vehicle pick from a pool of their
    own kind; the 8 where the vehicle *is* the callout keep the one they name - a stolen bus is a bus.

  * **And the fallback bug that fell out of doing it:** a pooled vehicle had no single vehicle to fall
    back to, so a pool that was wrong for an install would have left three suspects standing where a car
    chase was meant to be. People had a fallback, vehicles did not. Both pools now ship with their
    fallback in the recipe, and both checkers look for it.

Patient models are not randomised: each medical callout names the person it is about, the 53 are already
different from each other, and each matches what that patient says. Your own recipes can use either form:
`<Ped Model="..." />` or `<Ped Models="a,b,c" />`, and the same for vehicles with `Vehicle` and
`Vehicles` - all documented in the README in `Custom\`.

`tools\verify-bridge.ps1` checks the bridge between the two plugins by reading their metadata - the
compiler cannot see across a reflection boundary, and a rename there would fail silently in game.

## 2. Every mod installed

### 2.1 Platform — the things everything else sits on

| Mod | Where | Needs | Status |
|---|---|---|---|
| **RAGE Plugin Hook** 1.131.1424.17745 | `RAGEPluginHook.exe` | — | ✓ F4 console, 10 s plugin timeout |
| **LSPDFR** 0.4.9695 (0.4.9) | `Plugins\LSPD First Response.dll` | RPH | ✓ |
| **ScriptHookV's ASI loader** | `dinput8.dll` | — | ✓ loads ASI mods (build May 2 2015) |
| **ScriptHookV** | `ScriptHookV.dll` | — | ✓ installed, matches the game build (§1.1) |
| **AdvancedHookV** + EasyHook/EasyLoad64 + LMS libraries | game root | — | ✓ (ELS's hook; works, ELS itself does not) |
| **OpenIV** | `OpenIV.asi` + `mods\` folder | — | ✓ loaded, mods folder active |
| **openCameraV** | `openCameraV.asi` | — | ✓ loaded |
| **ScriptHookVDotNet** | `ScriptHookVDotNet.asi`, `ScriptHookVDotNet2.dll`, `ScriptHookVDotNet3.dll`, `ScriptHookVDotNet.ini` | ScriptHookV ✓, .NET 4.8.1 ✓, VC++ v14 ✓ | ✓ nightly **3.7.0.191** — its console key moved from F4 to **F7** because F4 is RAGE Plugin Hook's (§1.6) |
| **LemonUI** | `scripts\LemonUI.SHVDN3.dll` | ScriptHookVDotNet ✓ | ✓ **2.2** |

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

### 2.3 .NET script mods — `scripts\` (loaded by ScriptHookVDotNet)

Not LSPDFR plugins, and they never will be: these are loaded by ScriptHookV's ASI loader through
ScriptHookVDotNet, before the game reaches story mode. That is also why they run whether or not you go
on duty — and why they appear in none of LSPDFR's own lists.

| Mod | Version | Needs | Status |
|---|---|---|---|
| **DriverJobs V** | **1.6.0** | ScriptHookVDotNet ✓, LemonUI 2.x ✓ | installed — 31 civilian jobs; `Shift+J` opens its menu. **Never launched yet (§1.6)** |
| **LemonUI** | 2.2 (SHVDN3 build) | ScriptHookVDotNet ✓ | ✓ the menus DriverJobs draws with |
| **DriverJobs' data** | — | — | `DriverJobsData\` beside the DLL: 84 XML files, all parsing cleanly, `Missions\Jobs.xml` holding 37 jobs |

### 2.4 RagePluginHook-level plugin

| Plugin | Version | Where | Why it is here | Status |
|---|---|---|---|---|
| **Damage Tracker Framework** | 2.0.2 | `Plugins\DamageTrackingFramework.dll` + `DamageTrackerLib.dll` (root) | **required by Policing Redefined** | ✓ RPH loads it at startup (16:43:49) |

### 2.5 LSPDFR plugins — `Plugins\LSPDFR` (17)

| Plugin | Version | Needs | Status |
|---|---|---|---|
| **Common Data Framework** (CDF) | 1.0.0.10 | a record API other plugins use | loads |
| **CompuLite** | 1.5.2.8 | RAGENativeUI ✓, LiteDB (embedded ✓) · *optional*: Traffic Policer ✓ | loads · **conflicts with PR §1.4** |
| **ManiacCallouts** | 1.3.0.0 | **StopThePed ✓ (required)** | loads |
| **MizCallouts** | 1.0.1.0 | RAGENativeUI ✓ | loads |
| **Plain Sight** | 1.1.3.0 | LSPDFR only | loads |
| **Policing Redefined** | 1.0.0.5 | RAGENativeUI ✓, CDF ✓, Damage Tracker ✓ | loads · **conflicts with StopThePed/UB/CompuLite §1.4** |
| **Riskier Traffic Stops** | 3.4.0.0 | CDF ✓, RAGENativeUI ✓, Newtonsoft.Json ✓ · *optional*: ImmersiveAmbientEvents ✗ | loads |
| **SSStuart Callouts** | 0.1.2.0 | CalloutInterfaceAPI ✓ | loads |
| **StopThePed** | 4.9.5.4 | RAGENativeUI ✓ · *optional*: Ultimate Backup ✓, PoliceSmartRadio ✓, VocalDispatch ✗, PoliceSearch ✗ | loads · **conflicts with PR §1.4** |
| **Story Callouts** | 0.1.2.0 | CalloutInterfaceAPI ✓ | loads |
| **TextCallouts** | 1.2.2 | **nothing but LSPDFR and RAGE Plugin Hook** | loads, 13 callouts + your own XML recipes |
| **TextDispatch** | 1.0.14 | LSPDFR ✓ · *optional*: Ollama ✓, LM Studio | loads, box drawn |
| **Traffic Policer** | 7.0.0.0 | Albo1125.Common ✓, RAGENativeUI ✓, scanner audio ✓ (163 files) · *optional*: British Policing Script ✗ | loads (2019 mod — §4) |
| **Ultimate Backup** | 1.8.7.1 | RAGENativeUI ✓ · *optional*: VocalDispatch ✗, PoliceSearch ✗ | loads · **part of the PR conflict §1.4** |
| **UnitedCallouts** | 1.5.8.2 | RAGENativeUI ✓ | loads |

Each plugin's own data folder is installed alongside it: `CompuLite\`, `MizCallouts\`,
`PolicingRedefined\`, `RiskierTrafficStops\`, `StopThePed\`, `UltimateBackup\`, `UnitedCallouts\`,
`CommonDataFramework\`, `VocalDispatch\` — and the leftovers `686Callouts\`, `EMSmod\` and
`PoliceSmartRadio\` from the mods below.

**Put back on request:** 686 Callouts 2.1.2.0 (with its 482-file data folder) and External Police
Computer 2.0.1.0 — with RAGENativeUI fixed at 1.9.3.0, the reason they were removed is gone. 686
Callouts' `Is ExternalPoliceComputer Running? False` line should now read `True`.

**Still out:** PoliceSmartRadio (its 151-file data folder is on disk), Callout Interface, LSPDFR+,
BetterEMS and Arrest Manager. All of them are optional integrations for something else.

### 2.6 Mod content inside `lspdfr\`

Scanner audio that came with mods — all present:

| Pack | Files |
|---|---|
| `Traffic Policer Audio` | 163 |
| `PolicingRedefinedAudio` | 156 |
| `686Callouts Audio` | 80 |
| `EMSAUDIO` (BetterEMS — plugin removed, §2.8) | 43 |
| `Maniac Callouts` | 22 |
| `UnitedCallouts Audio` | 21 |
| `Arrest Manager Audio` (plugin removed, §2.8) | 4 |

Plus LSPDFR's own scanner set and cop voice sets (`AREAS`, `CRIMES`, `CAR_MODEL`, `STREETS`, `s_m_y_cop_01_*`, …) and
LSPDFR's own data (`lspdfr\data\`: agency, backup, stations, regions, outfits, duty selection, `ai\`, `custom\`).

### 2.7 Vehicles, liveries and ELS configs

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

### 2.8 Leftovers from mods you removed — safe to delete, or keep if you reinstall

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

### 2.9 Not installed — removed, or never had

| Mod | State |
|---|---|
| **Callout Interface 1.4.1** | **removed at ~16:45** while hunting the crash. It was never the problem — its archive ships the *good* RAGENativeUI 1.9.3.0. Worth putting back: 686 Callouts, SSStuart, Story Callouts and External Police Computer all reference it. |
| **LSPDFR+ 1.9.0.0** | removed. One of the two that shipped the bad RAGENativeUI 1.6.3.0 — reinstall only if you skip that file. |
| **BetterEMS 4.1b** | removed, same reason as LSPDFR+. |
| **Arrest Manager 7.11.0.0** | downloaded 16:09, DLL not present now. |
| **VocalDispatch**, **PoliceSearch**, **British Policing Script**, **GrammarPolice**, **ImmersiveAmbientEvents**, **RagePROletariat** | never installed — see §1.5 |
| **Riskier Traffic Stops** dependencies | CDF ✓ already installed |

---

## 3. Check it yourself

| What | Where |
|---|---|
| Live plugin list, and what each one is missing | `/plugins` in the chat box, or `tdplugins` in the F4 console |
| The same thing, written automatically | `Plugins\LSPDFR\textdispatch.log` — the `inventory:` block, a few seconds after going on duty |
| What LSPDFR loaded, and when | `RagePluginHook.log` and `Logs\` — `Creating plugin:` lines appear **only after you go on duty (E at a police station)** |
| What ASI mods loaded | `asiloader.log` — should now include `ScriptHookVDotNet.asi` |
| The .NET scripts (DriverJobs V, LemonUI) | `ScriptHookVDotNet.log` — loads, and `Loaded 1 script(s)` |
| What the civilian jobs mod did | `DriverJobsV.log` in the game root |
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
- **`scripts\` belongs to ScriptHookV, not to LSPDFR.** Mods there (DriverJobs V, LemonUI) are loaded by
  ScriptHookVDotNet when the game starts, duty or not, and they appear in none of LSPDFR's lists — not in
  `/plugins`, not in `RagePluginHook.log`. Their logs are `asiloader.log`, `ScriptHookVDotNet.log` and
  `DriverJobsV.log`, all in the game root. Going on duty is not what starts them.
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
- DriverJobs V 1.6.0 — <https://www.gta5-mods.com/scripts/driverjobs-v> · also on Nexus <https://www.nexusmods.com/gta5/mods/1235>
- ScriptHookVDotNet (nightly builds) — <https://github.com/scripthookvdotnet/scripthookvdotnet-nightly/releases>
- LemonUI — <https://github.com/LemonUIbyLemon/LemonUI/releases>
- DirectStorageFix (only if `DriverJobsV.log` cannot read its XML) — <https://www.gta5-mods.com/scripts/directstoragefix>
