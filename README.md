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
3. Press **T**.

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
- **You can give them orders.** `hands up`, `step out of the vehicle`, `look at me`, `don't move`,
  `you can go` - recognised by the plugin rather than the model, and actually carried out. Whether
  they obey follows their temperament, so the reply and the action agree.
- **Traffic stops work both ways** — LSPDFR's own control, or `/stop` from the keyboard. `/id`,
  `/frisk`, `/cuff`, `/record` and `/owner` all act on the driver you stopped.
- **Every installed callout pack is supported automatically.** `/calls` lists them with the pack each
  came from; `/callout <name>` starts any of them.
- **An optional local model** (Ollama or LM Studio) behind both the dispatcher and the pedestrians.
  No API key, no internet, nothing leaves your machine.
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

Everything is also drivable from RAGE Plugin Hook's **F4** console — `tdsay`, `tdwho`, `tdstatus`,
`tdmodels`, `tdkey`, `tdrender`, `tdfont` — so a chat box that will not open is never a dead end.

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

## Status

Early. **1.0.4 is the first release that loads.** Earlier versions were built as RAGE Plugin Hook
plugins, and LSPDFR does not use RPH's plugin attribute — it looks for a class named `Main` deriving
from `LSPD_First_Response.Mod.API.Plugin`, so it found the DLL and quietly declined it.

1.0.4 is confirmed loading as an LSPDFR plugin, with LSPDFR's API reachable (138 methods found) and
the chat box rendering once per frame rather than once per game tick. The integration is young and is
the part most likely to have rough edges. Issues and log excerpts are welcome.

## Licence

MIT — see [LICENSE](LICENSE).

That covers TextDispatch only. RAGE Plugin Hook and LSPDFR are separate works under their own licences,
are not included in this project or in any download of it, and this licence does not apply to them.
