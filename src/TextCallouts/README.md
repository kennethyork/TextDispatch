# TextCallouts

**LSPDFR callouts with no dependencies.** Eight callouts in the style of the big packs, written from
scratch, needing nothing but LSPDFR and RAGE Plugin Hook — no StopThePed, no Ultimate Backup, no
CompuLite, no Callout Interface, no ExternalPoliceComputer, no Common Data Framework, no
RAGENativeUI.

That last part is the whole point. Most callout packs drag in an interaction framework, a records
framework, a UI framework or all three, and then the pack breaks when one of them is missing, out of
date, or fighting with another. This one cannot: the complete list of assemblies it references is

```
LSPD First Response      the callout API, already in your game
RagePluginHook           the host, already in your game
mscorlib, System, System.Core, System.Drawing, Microsoft.CSharp   the .NET framework
```

Nothing else — verified by reading the built DLL's reference table, not by hoping.

> It is **inspired by** packs like 686 Callouts, not a copy of it. All eight callouts, their names and
> their dialogue are original work, and no file, string or asset from 686 Callouts is used. Keep 686
> installed alongside — they do not conflict.

---

## The callouts

| Callout | What happens | How it ends |
|---|---|---|
| **Occupied Stolen Vehicle** | A stolen car with a driver in it. Stop it if you can; when you get close he runs and LSPDFR's own pursuit AI drives him | the driver is arrested or down |
| **Reckless Driver** | Someone driving badly. Nothing criminal until they see you — then they run | the driver is arrested or down |
| **Domestic Disturbance** | Two people behind a front door. About half are just arguing; the rest end with somebody squaring up to you | the aggressor is in custody |
| **Shoplifting In Progress** | On foot from a store, and usually running. A foot pursuit you have to win | arrest, or he clears the block |
| **Armed Robbery In Progress** | Armed from the start. The line dispatch gives you tells you to wait for backup, and it means it | the armed suspect is down or in custody |
| **Two-Car Collision** | Nobody to arrest. One injured driver and an ambulance to call — the callout about helping rather than catching | EMS takes over |
| **Suspicious Person** | May be nothing at all, may be a burglary suspect. The record comes back on the radio when you get close, and what he does about it is decided by that | arrest, or you let him go |
| **Officer Needs Assistance** | Shots fired, two armed suspects, backup rolling | both suspects down or in custody |

Each one is built around LSPDFR's own systems wherever possible: pursuits are LSPDFR pursuits, the
ambulance and backup come from LSPDFR's backup system, arrest detection is LSPDFR's, and the callout
blip and acceptance flow are LSPDFR's. The pack supplies the situation, not a parallel universe.

### Two rules the code follows

- **What dispatch says matches what the ped does.** Hostility is decided *first*, then the line is
  chosen to fit it — so a suspect who is about to fight you is never described as calming down.
- **A native can never crash the callout.** RAGE Plugin Hook resolves GTA natives by name at runtime,
  so a wrong name is a runtime failure; every native here is wrapped, some are tried in more than one
  shape (the hands-up native has changed argument count between game builds), and a failure costs one
  line of speech and a log entry, never the game.

---

## Install

1. Copy `TextCallouts.dll` into your GTA V **`Plugins\LSPDFR\`** folder.
   Or run `tools\install-textcallouts.ps1`, which builds it and puts it in the right place.
2. Launch **RagePluginHook.exe** and load story mode.
3. **Go on duty** — press **E** at a police station. The callouts are registered at that moment;
   until then nothing happens, which is normal, and it is not a fault in the pack.
4. Dispatch starts offering them like any other callout. Accept with LSPDFR's own control, or with
   TextDispatch's `/accept` if you have it.

`Plugins\LSPDFR`, not `Plugins` — LSPDFR loads that folder into its own AppDomain, and a plugin
loaded by RPH instead cannot see LSPDFR's types at all.

---

## With TextDispatch

If [TextDispatch](../TextDispatch/) is installed, this pack finds it and mirrors every line it says
into the chat box: the ambulance request, the pursuit call-in, the suspect descriptions, all as text.
That link is reflection at runtime, so:

- there is no dependency in either direction — this pack still works, and still needs nothing, if
  TextDispatch is not installed;
- if TextDispatch is not there, the same lines simply appear as on-screen notifications instead.

TextDispatch also picks these callouts up on its own, because it reads the callout catalogue out of
the assemblies it finds. So with both installed: `/calls` lists all eight, and `/callout <name>`
starts one on demand — the quickest way to see a callout without waiting for dispatch to offer it.

---

## Checking it, and diagnosing it

| What | Where |
|---|---|
| Log | `Plugins\LSPDFR\textcallouts.log` — what registered, what was accepted, every line said, and any native that did not resolve |
| `tcstatus` (F4) | how many callouts are in the pack, whether they are registered, and the log path |
| `tccallouts` (F4) | the eight, by their dispatch names |
| `/plugins` (TextDispatch) | confirms it loaded, and that it has no missing dependencies |

Every callout also logs when it opens and closes, with the reason — so "dispatch never called me" and
"dispatch called me and it went wrong" are different lines in a file, not the same silence.

---

## Requirements

GTA V (Legacy), RAGE Plugin Hook, LSPDFR 0.4.9 or newer, .NET Framework 4.8. Nothing else.

**RAGE Plugin Hook and LSPDFR are not included**, here or in any download — they are separate works
under their own licences.

## Building

```powershell
dotnet build src\TextCallouts\TextCallouts.csproj -c Release
```

Two references are needed and neither is committed to this repository:

- `tools\rph-sdk\RagePluginHook.dll` — ships inside the official RAGE Plugin Hook download as
  `SDK\RagePluginHook.dll`. RPH's terms forbid redistributing it.
- `plugins\LSPD First Response.dll` from your game folder — a callout pack cannot avoid referencing
  it, because the entry point must derive from its `Plugin` class and every callout from its
  `Callout` class. The build never copies it into the output.

## Status

**1.0.0, first release.** It compiles against the real LSPDFR and RAGE Plugin Hook APIs, and the
built DLL has been checked against LSPDFR's contract — the `Main` type exists, is public, and derives
from `LSPD_First_Response.Mod.API.Plugin`; all eight callouts are public, concrete, have a
parameterless constructor and a `[CalloutInfo]` name; and the reference table contains no mod
assembly (see the check results in the release notes).

What has **not** happened yet is a patrol with it. Callout behaviour depends on the live game — spawn
positions, pursuit AI, whether a native resolves — and that can only be proven in game. So treat 1.0.0
as "correct by construction, unproven in play", and if something misbehaves the log says which
callout, which step, and which native.

## Licence

MIT — see [LICENSE](../../LICENSE). It covers TextCallouts only; RAGE Plugin Hook and LSPDFR are
separate works under their own licences and are not covered by it.
