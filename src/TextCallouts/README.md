# TextCallouts

**LSPDFR callouts with no dependencies.** A hundred and two callouts in the style of the big packs, written from
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

> It is **inspired by** packs like 686 Callouts, not a copy of it. All eighteen callouts, their names and
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
| **Barricaded Suspect** | Held up inside with a handgun and refusing to come out. Rush him and it becomes a shooting; give him time and he may walk out | arrested, down, or he gives up |
| **Stolen Vehicle, Driver On Foot** | The stolen car is parked up with the driver still in it — and he bails rather than drives | arrested or down |
| **Public Disturbance** | A fight in the street: two people going at each other, a third watching. They usually scatter when you arrive | both in custody |
| **Bicycle Theft** | A bike taken from outside a shop and a suspect who rides rather than runs | arrested or down |
| **Weapons Call, Brandishing** | Armed but not yet hostile. Arrive fast and he panics; hold back and he may put it down | arrested, down, or he surrenders |

Each one is built around LSPDFR's own systems wherever possible: pursuits are LSPDFR pursuits, the
ambulance and backup come from LSPDFR's backup system, arrest detection is LSPDFR's, and the callout
blip and acceptance flow are LSPDFR's. The pack supplies the situation, not a parallel universe.

### The library, and which duty gets what

A hundred and two callouts, and each one says which duty it belongs to in its `<For>` element, so the
work matches the agency you went on duty as:

| Duty | Callouts | What it is |
|---|---|---|
| **LSPD** | 30 | city work: robberies, drugs, shots fired, burglary, assaults, disturbances |
| **Sheriff** (lssd) | 16 | county work: ranch trespass, poaching, livestock theft, rural burglary, the desert, Sandy Shores |
| **Highway Patrol** (sahp) | 12 | traffic: racing, wrong-way drivers, hit and run, insecure loads, road rage |
| **Park Rangers** (sapr) | 9 | the parks: illegal fires, poaching, lost walkers, injured wildlife, off-roading |
| **Prison** (saspa) | 5 | Bolingbroke: escapes, contraband drops, transport incidents, assaults on staff |
| **EMS** (lsfd) | 14 | patients: falls, overdoses, seizures, chest pain, cyclists, exposure |
| **Fire** (lsfd_fire) | 6 | fires: refuse, kitchens, grass, vehicles, a commercial bin fire |

Eighty-four of those live in `Plugins\LSPDFR\TextCallouts\Library` as XML recipes - the same format a
player's own callouts use, so you can read them, copy them and edit them. The other eighteen are written
in C# because their behaviour is particular (the medical ones, and the police ones that need a scene the
recipes cannot describe). A recipe names a duty with `police`, one agency, or several: `<For>lspd,sheriff,sahp</For>`
is offered to all three. An agency the pack has never heard of gets the police work, because a patrol
with no calls at all is a worse failure than a misfiled one.

### The five that are not police work

LSPDFR has no notion of an EMS callout: its registry is police calls, and the agency you go on duty as
changes only your uniform, your vehicle and what dispatch calls you. What it does give a callout is the
means to ask - `Functions.GetCurrentAgencyScriptName()` - so these five offer themselves **only while
the player is working as an emergency-medical agency**, and stay out of the way on a police patrol.

| | |
|---|---|
| **Cardiac Arrest** | somebody collapsed in the street, a stranger doing compressions. Twenty seconds of yours |
| **Overdose** | slumped and unresponsive; airway, naloxone, and a patient who comes round frightened |
| **Collision with Injuries** | one casualty still in the car - the door comes open when you arrive - and one walking wounded |
| **Welfare Check** | somebody sat where a neighbour thinks they should not be. Half of these are nothing at all |
| **Vehicle Fire** | a car alight in a car park, one person who took the smoke, and an engine that is not there yet |

They are matched against the agency's *ScriptName* from `agency.xml`, loosely: `lsfd`, or anything whose
name contains ems, medic, paramedic, ambulance or fire. So a player who adds their own ambulance service
to `agency.xml` does not have to change anything here. Going on duty as **LSFD** - which the LSPDFR duty
menu offers once `duty_selection.xml` lists it - is how you get them.

They use nothing but LSPDFR's own API, like the rest of the pack: the ambulances and the fire engine come
from LSPDFR's backup system, and a patient who dies on scene ends the callout differently from one who
does not. There is no payment in any of them, because LSPDFR callouts do not pay.
### Two rules the code follows

- **What dispatch says matches what the ped does.** Hostility is decided *first*, then the line is
  chosen to fit it — so a suspect who is about to fight you is never described as calming down.
- **A native can never crash the callout.** RAGE Plugin Hook resolves GTA natives by name at runtime,
  so a wrong name is a runtime failure; every native here is wrapped, some are tried in more than one
  shape (the hands-up native has changed argument count between game builds), and a failure costs one
  line of speech and a log entry, never the game.

---

## Your own callouts

Adding a callout should not need a compiler. Drop an XML file in

```
Plugins\LSPDFR\TextCallouts\Custom\
```

go on duty, and it is offered exactly like the built-in eighteen - same dispatch announcements, same callout
blip, same resolution. The folder is created on first run with a `README.txt` describing every field and
a worked example to copy (`Example-ArmedRobbery.xml.example` - rename it to end in `.xml` to use it).

```xml
<Callout>
  <Name>Armed Robbery At The Pier</Name>
  <Probability>Medium</Probability>
  <Message>Armed robbery in progress at the pier.</Message>
  <Advisory>Two males, one of them with a handgun.</Advisory>
  <Resolution>ArrestOrDeath</Resolution>

  <Distance Min="150" Max="320" Radius="40" />

  <Actors>
    <Ped Model="a_m_y_musclbeac_01" Role="Suspect" Armed="true" Weapon="WEAPON_PISTOL"
         Ammo="120" Hostile="true" Armor="40" Accuracy="45" />
    <Ped Model="a_f_y_business_01" Role="Bystander" Cower="true" />
  </Actors>

  <OnApproach At="30" Hostile="true" />

  <Lines>
    <Briefing>He is armed and he is still there.</Briefing>
    <Approach>He has seen you. He is not putting it down.</Approach>
    <Resolved>The scene is secure.</Resolved>
  </Lines>

  <Support Ambulance="false" Backup="false" />
</Callout>
```

That covers what almost every callout actually is: go somewhere, find people, they react when you
arrive, it ends when you have dealt with them. Actors can be armed, hostile, cowering, in a vehicle;
they can flee on foot or in the car (driven by LSPDFR's pursuit AI), give up, or turn on you; and the
text dispatch says is yours, line by line. `<Resolution>` decides what counts as finished -
`ArrestOrDeath`, `AnyArrest`, or `Manual` for the callouts with nothing to catch.

**A wrong file is refused with a reason, never ignored.** A misspelled element, a word where a number
belongs, a flag that is neither true nor false, a `<Ped>` list that is missing entirely - each is named
in `Plugins\LSPDFR\textcallouts.log` along with the file it came from. A typo that silently did nothing
would be indistinguishable from the whole feature not working, which is the worst possible outcome for
something you are meant to edit by hand.

That parser is tested, and the test earned its keep immediately: it caught `ArrestOrDeath` being
compared against the misspelled literal `arrestoredeath`, which would have refused *every* recipe while
telling the author to use the value they had already used. It now checks the shipped example parses
field by field, that eight kinds of malformed file are refused with a useful message, and that ids come
out usable as type names.

One thing to be straight about: the recipes are turned into real callout types at run time, because
that is the only thing LSPDFR accepts - `Functions.RegisterCallout` takes a `Type`. That step cannot be
exercised outside the game, so if it ever fails it will fail loudly in the log, naming the file, and the
built-in eighteen are unaffected either way.

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
the assemblies it finds. So with both installed: `/calls` lists all eighteen, and `/callout <name>`
starts one on demand — the quickest way to see a callout without waiting for dispatch to offer it.

---

## Checking it, and diagnosing it

| What | Where |
|---|---|
| Log | `Plugins\LSPDFR\textcallouts.log` — what registered, what was accepted, every line said, and any native that did not resolve |
| `tcstatus` (F4) | how many callouts are in the pack, whether they are registered, and the log path |
| `tccallouts` (F4) | the eighteen, by their dispatch names |
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

**1.2.2 stops the duty handler being unsubscribed when you go off duty.** LSPDFR calls a plugin's
`Finally()` when the player clocks off, not only at shutdown, and this pack used to unsubscribe there -
so it was never told about the next duty and its callouts were registered once and then never again,
while every other pack re-registered. That is the half of the bug that removing the "register once"
guard could not fix on its own. Nothing is unhooked now, and `InitializeAgain()` re-arms it exactly
once.

**1.2.1 registers the callouts on every duty, not just the first.** LSPDFR rebuilds its callout
registry each time the player goes on duty; this pack registered once, so after an off-duty and back-on
cycle it had no callouts at all while every other pack re-registered. Fixed, and the log now says
`registered for this duty` every time so it is visible rather than assumed.

**1.2.0 adds five callouts** - Barricaded Suspect, Stolen Vehicle (Driver On Foot), Public Disturbance,
Bicycle Theft and Weapons Call (Brandishing) - which brings the pack to thirteen. Three of them have two
endings rather than one, decided by how the player plays them rather than by a dice roll that ignores it:
the barricaded suspect comes out if you give him time, the brawl scatters rather than fights, and the man
brandishing a weapon keeps it if you close on him too fast.

**1.1.0 adds your own callouts** - an XML recipe per callout, loaded at run time, with a parser that
refuses bad files by name instead of ignoring them (see above). **1.0.0** was the first release.

The pack compiles against the real LSPDFR and RAGE Plugin Hook APIs, and the
built DLL has been checked against LSPDFR's contract — the `Main` type exists, is public, and derives
from `LSPD_First_Response.Mod.API.Plugin`; all eighteen callouts are public, concrete, have a
parameterless constructor and a `[CalloutInfo]` name; and the reference table contains no mod
assembly (see the check results in the release notes).

What has **not** happened yet is a patrol with it. Callout behaviour depends on the live game — spawn
positions, pursuit AI, whether a native resolves — and that can only be proven in game. So treat 1.0.0
as "correct by construction, unproven in play", and if something misbehaves the log says which
callout, which step, and which native.

## Licence

MIT — see [LICENSE](../../LICENSE). It covers TextCallouts only; RAGE Plugin Hook and LSPDFR are
separate works under their own licences and are not covered by it.
