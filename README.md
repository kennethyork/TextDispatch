# TextDispatch

**LSPDFR in text.** Callouts, dispatch, and the people in them, through an in-game chat box instead of
RAGE UI menus — driven by an optional local language model that runs on your own machine.

Press **T**, and dispatch talks to you in text. Type back, and it answers. Type at a suspect, and they
answer too, in character, from what is actually happening to them.

```
[DISPATCH] All units, we have Armed Robbery. 2A19, do you copy? Reply 'accept' or 'decline'.
> accept
[RADIO]    You: 2A19, I'm taking it.
[DISPATCH] Copy 2A19, you are assigned Armed Robbery. Advise 10-97 when you are on scene.
> 10-97
[DISPATCH] Copy 2A19, 10-97 at 21:14. Handle your call.

> /r I've got three males matching the description heading north on the beach
[DISPATCH] Copy 2A19, I have you on the beach. Keep them in sight.

> why did you stop me
[LOCAL]    You say: why did you stop me
[LOCAL]    Marcus Reyes: I wasn't doing anything wrong.
> hands where I can see them
[LOCAL]    Marcus Reyes: Fine. Fine!
```

## Why this exists

Every LSPDFR callout pack — UnitedCallouts, YobbinCallouts, 686 Callouts, DynamicLSPDFR — is a RAGE
Native UI plugin: fixed menu trees you drive with keys. None of them are text, and none of them let you
*talk*. The chat box is the part of the roleplay-server experience LSPDFR structurally does not have.

TextDispatch works **with** those packs, not instead of them. It reads your installed callouts from the
assembly, so every callout you have is announced in text and can be started by name.

## Install

1. Copy `TextDispatch.dll` into your Grand Theft Auto V **`Plugins\LSPDFR\`** folder.
2. Launch **RagePluginHook.exe** and load story mode.
3. **Go on duty** - press **E** at a police station.
4. Press **T**.

> **Step 3 is the one that catches people.** LSPDFR loads everything in `Plugins\LSPDFR` when you go
> on duty, not when the game starts. Until then there is no chat box, no callout pack, no
> StopThePed - and waiting in story mode changes nothing, however long you wait. If RPH's log has no
> `Creating plugin:` lines in it, that is why.

> **`Plugins\LSPDFR`, not `Plugins`.** RAGE Plugin Hook gives every plugin it loads its own AppDomain,
> and a plugin in RPH's AppDomain cannot see LSPDFR's types at all — it runs, it draws, and every call
> into LSPDFR fails silently. Plugins in `Plugins\LSPDFR` are loaded by LSPDFR itself, into LSPDFR's
> AppDomain, which is what makes the API reachable. The plugin's first log line says which happened:
> `detected, LSPDFR 0.4.9` or `not installed`.

## Features

- **Callouts arrive as radio traffic.** Dispatch announces them in text; you reply `accept` or
  `decline`.
- **You can talk to dispatch.** `/r I've got three males heading north on the beach` gets a real
  answer, in character — not just a status code acknowledgement.
- **You can talk to people.** Type at a suspect and they answer in text, from their actual situation:
  stopped, searched, cuffed, caught with something, or running. Each person keeps a name and a
  temperament across the session, so they stay the same character.
- **Full conversations, not one-liners.** Replies run to a sentence or two, twenty lines of history go
  to the model so it knows what it just said, and everyone has an age, a job, a district and a history
  drawn from the same records the terminal reads.
- **You can give them orders.** `hands up`, `step out of the vehicle`, `look at me`, `don't move`,
  `you can go` - recognised by the plugin rather than the model, and actually carried out. Whether
  they obey follows their temperament, so the reply and the action agree.
- **Traffic stops work both ways** — LSPDFR's own control, or `/stop` from the keyboard. `/id`,
  `/frisk`, `/cuff`, `/record` and `/owner` all act on the driver you stopped.
- **Every installed callout pack is supported automatically.** `/calls` lists them with the pack each
  came from; `/callout <name>` starts any of them.
- **An optional local model** (Ollama or LM Studio) behind both the dispatcher and the pedestrians.
  No API key, no internet, nothing leaves your machine.
- **Your civilian jobs are in the box too.** With DriverJobs V installed, `/jobs` lists all 37 of its
  jobs with what each pays, and `/job <name>` describes one and marks where it starts on your map — read
  from the mod's own job file, so a job you edit or add shows up as well.
- **Your other mods keep out of the way while you type.** A sentence is nothing but keystrokes, and
  every plugin is watching for them - so while the box is open the keys are hidden from everything
  else in the game, and read from the hook instead. Type `10-97` and your own menus stay shut. `/typing`
  says how it went, and turns it off.
- **It runs with no model at all**, on a built-in script, so it works on a machine with nothing
  installed.
- **Nothing it says can block the game.** Every model turn has a deadline and a scripted fallback.

## Words and actions are kept apart

This is the part that matters:

- **Working out what you asked for is ordinary code**, and the resulting call goes to LSPDFR directly.
  "Send me another unit" really does request one.
- **The model only writes the line.** Nothing it returns is parsed as a command, so it has no authority.
- **It cannot lie about what happened.** If a reply claims responders are en route and none were
  dispatched, that reply is thrown away and the scripted line is used instead.

A dispatcher that invents backup is worse than one that says nothing.

## Model setup (optional)

```ini
; Plugins\LSPDFR\TextDispatch.ini
AiMode=auto          ; auto | llm | scripted
AiProvider=ollama    ; ollama | lmstudio
AiModel=             ; blank = the first model the server reports
```

`auto` is the default: it probes once at startup and uses a model if one answers. For Ollama, that is
`ollama serve` and any pulled model — the model is loaded at startup so the first thing you say gets a
real answer rather than a scripted one.

## Commands

Plain text is speech. A leading `/` is a command. A bare status code is radio traffic.

| | |
|---|---|
| Talk | plain text · `/s` · `/w` whisper · `/shout` · `/me` · `/do` · `/who` · `/talk <n>` |
| Radio | `/r <text>` · `10-8` `10-7` `10-97` `10-98` `10-13` `code 3` `code 4` |
| Calls | `/accept` · `/decline` · `/calls [filter]` · `/callout <name>` · `/endcall` |
| Stops | `/stop` · `/endstop` · `/tow` · `/id` · `/frisk` · `/cuff` · `/detain` · `/release` · `/record` · `/owner` |
| Services | `/ems` · `/fire` · `/backup [swat\|air\|state\|ems\|fire\|transport\|code2]` · `/transport` · `/panic` · `/911 <details>` |
| Car | `/lock` · `/unlock` · `/engine [off]` · `/trunk` · `/hood` · `/doors` · `/repair` · `/veh <model>` |
| Records | `/mdt` · `/person [name]` · `/plate [plate]` · `/warrant [name]` · `/bolo` · `/arrest` · `/cite <name> <offence>` |
| Pursuit | `/pursuit` · `/calledin` · `/endpursuit` |
| The box | `/pos <corner>` · `/margin <px>` · `/ui` · `/font` · `/fontsize` · `/lines` · `/clear` |
| The install | `/plugins` — what LSPDFR actually loaded, what it did not, and what is sitting in a folder LSPDFR never looks in |
| Typing | `/typing [on\|off]` — whether the other plugins can see what you type, and which way this install turned out |
| Civilian jobs | `/jobs [filter]` — DriverJobs V's jobs, what each pays · `/job <name or number>` — the details, and a marker on where it starts |

Everything is also drivable from RAGE Plugin Hook's **F4** console — `tdsay`, `tdwho`, `tdstatus`,
`tdmodels`, `tdplugins`, `tdkey`, `tdrender`, `tdfont` — so a chat box that will not open is never a
dead end.

## Requirements

- Grand Theft Auto V (Legacy or Enhanced)
- RAGE Plugin Hook, installed and working
- LSPDFR
- .NET Framework 4.8

**RAGE Plugin Hook is not included**, here or in any download — its licence forbids redistribution.
You need your own working install.

## Building

```powershell
dotnet build src\TextDispatch\TextDispatch.csproj -c Release
```

The RAGE Plugin Hook SDK is required to build and is deliberately **not** in this repository:
put `RagePluginHook.dll` in `tools\rph-sdk\`. It ships inside the official RAGE Plugin Hook download as
`SDK\RagePluginHook.dll`. The build never copies it into the output.

## Documentation

[`src/TextDispatch/README.md`](src/TextDispatch/README.md) is the full manual — commands, the AI
design, traffic stops, callouts, troubleshooting, and the console fallback.

## Also in this repository: TextJobs

[`src/TextJobs`](src/TextJobs) is DriverJobs V's jobs in a box like this one — and it is **not** an
LSPDFR plugin. TextDispatch cannot exist without LSPDFR (its entry point derives from LSPDFR's Plugin
base, and LSPDFR is what loads it) and it comes up when you go on duty. TextJobs is a
ScriptHookVDotNet script instead, the same framework DriverJobs itself runs on, so it works with or
without LSPDFR, on duty or off.

```
/jobs              every civilian job DriverJobs V has, with what each pays
/job <name>        what the work is, what you pay, what you drive, and a blip + route to its start
```

It reads the mod's own job file rather than a copy, hides what you type from the other plugins while
the box is open, and needs nothing installed but the game, ScriptHookV, ScriptHookVDotNet and the jobs
mod. The keyboard handling and the job reader are the *same source files* as TextDispatch's, compiled
into both, so a fix to either lands in both.

Two boxes on one key would both open and both read the same keystrokes, so TextJobs defaults to **F8**
rather than TextDispatch's left arrow.

## Also in this repository: TextCallouts

[`src/TextCallouts/`](src/TextCallouts/) is a companion **callout pack** — three hundred and forty-four callouts, written
from scratch, that need **nothing but LSPDFR and RAGE Plugin Hook**: no StopThePed, no Ultimate
Backup, no CompuLite, no Callout Interface, no Common Data Framework, no RAGENativeUI. Its whole
reference table is `LSPD First Response`, `RagePluginHook` and the .NET framework, verified by
reading the built DLL.

If TextDispatch is installed, each callout's lines are mirrored into the chat box automatically, and
TextDispatch lists them in `/calls` and can start any of them with `/callout <name>` — so the two
work as one thing while depending on nothing.

**Every callout in it has a script.** Three hundred and twenty-six recipes, nine hundred and eighty-one lines of dialogue between them, three or four each: open the box, type at the suspect in an armed robbery, and they answer in character out of that callout's own lines - and TextDispatch asks for those lines *before* it asks its own script or a model, so the answer is instant and needs no LLM. In a medical callout it is the patient who answers rather than the bystander beside them. The lines live in `tools/callout-dialogue.psd1`, and `tools/make-callout-library.ps1` will not build a library in which a callout is missing one.

Eighteen of its callouts are written in C#, and three hundred and twenty-six more ship as **XML recipes** in its Library folder, sorted by the duty they belong to; **your own are written the same way** - drop a file in
`Plugins\LSPDFR\TextCallouts\Custom\`, go on duty, and it is offered like any other callout. A file
it cannot make sense of is refused with the reason in the log, never ignored.

Install the DLL into `Plugins\LSPDFR\` and go on duty, exactly like TextDispatch:
[`src/TextCallouts/README.md`](src/TextCallouts/README.md).

### Installing all three at once

```powershell
powershell -ExecutionPolicy Bypass -File tools\install-bundle.ps1
```

builds and installs all three — the two LSPDFR plugins into `Plugins\LSPDFR`, [TextJobs](src/TextJobs)
into `scripts\` where DriverJobs V lives — and `tools\package-bundle.ps1 -Version 1.4.0` packages them
into one zip with [`BUNDLE.txt`](BUNDLE.txt): one download, one install, all three. They are
deliberately three assemblies rather than one: each works without the others, the link between
the two police plugins is reflection at runtime, and TextJobs shares nothing with them at run time at
all — it is a ScriptHookVDotNet script and does not need LSPDFR, or RAGE Plugin Hook, or the game to be
on duty.

What "working together" means, concretely — with both in `Plugins\LSPDFR`:

- callouts arrive as radio traffic in the chat box, accepted or declined by typing;
- every line a callout says is mirrored into the box instead of a notification popup;
- `/calls` lists all of them - your agency's work, whichever duty you went on as - and `/callout <name>` starts one on demand;
- the people in those callouts answer in text when you talk to them.

That contract is verified, not assumed: the release check reads both DLLs and confirms
`TextDispatch.Plugin.Chat` is a public static property and the chat box it returns has a public
`Notice(string)` — the two names the bridge looks up by reflection.

The bundle since `bundle 1.4.0` also carries the civilian half: `/jobs` lists every job DriverJobs V
has with what each pays, and `/job <name>` describes one and marks where it starts. TextJobs reads the
mod's own job file rather than a copy, so a job edited by hand is in the list too.

## Status

Early. **1.0.4 is the first release that loads.** Earlier versions were built as RAGE Plugin Hook
plugins, and LSPDFR does not use RPH's plugin attribute — it looks for a class named `Main` deriving
from `LSPD_First_Response.Mod.API.Plugin`, so it found the DLL and quietly declined it.

1.0.4 is confirmed loading as an LSPDFR plugin, with LSPDFR's API reachable (138 methods found) and
the chat box rendering once per frame rather than once per game tick. The integration is young and is
the part most likely to have rough edges. Issues and log excerpts are welcome.

**TextCallouts 1.1.0** is the pack described above, in [`src/TextCallouts/`](src/TextCallouts/) —
three hundred and forty-four callouts, plus **your own callouts written as XML recipes**. It is released separately from
TextDispatch, which is at **1.0.13** (the inventory wording fix) and does not depend on it.

**1.0.12 answers the question that costs the most evenings.** A plugin that is installed in the wrong
folder, or short of something it depends on, does not fail loudly - it is simply absent, and nothing
anywhere says so. The plugin now reads every DLL in `Plugins\LSPDFR`, asks LSPDFR which of them it
loaded, checks what each one refers to, and reports the difference: in the box a few seconds after you
go on duty, in full in the log, and on demand with `/plugins`. It also finds plugins left in `Plugins\`
or in `lspdfr\`, where LSPDFR never looks for them at all.

## Licence

MIT — see [LICENSE](LICENSE).

That covers TextDispatch only. RAGE Plugin Hook and LSPDFR are separate works under their own licences,
are not included in this project or in any download of it, and this licence does not apply to them.
