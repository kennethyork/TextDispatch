# TextDispatch

**LSPDFR, in text.** Callouts, dispatch *and the people in them* happen through an in-game chat box
instead of LSPDFR's menus — the interface a GTA World / FiveM roleplay server gives you, running in
single player on top of LSPDFR.

Press **T**. Dispatch talks to you in text, answers you when you type back, and the suspects you talk
to answer in character based on what is actually happening to them.

```
[DISPATCH] All units, we have Armed Robbery. 2A19, do you copy? Reply 'accept' or 'decline'.
> accept
[RADIO]    You: 2A19, I'm taking it.
[DISPATCH] Copy 2A19, you are assigned Armed Robbery. Advise 10-97 when you are on scene.
> 10-97
[DISPATCH] Copy 2A19, 10-97 at 21:14. Handle your call.

> /r I've got three males matching the description heading north on the beach
[RADIO]    You: I've got three males matching the description heading north on the beach
[DISPATCH] Copy 2A19, I have you on the beach. Keep them in sight.

> /r send me another unit, I've got a runner
[RADIO]    You: send me another unit, I've got a runner
[DISPATCH] Copy 2A19, backup en route. Stand by.

> why did you stop me
[LOCAL]    You say: why did you stop me
[LOCAL]    Marcus Reyes: I wasn't doing anything wrong.
> hands where I can see them
[LOCAL]    You say: hands where I can see them
[LOCAL]    Marcus Reyes: Fine. Fine!
```

## Two halves of the same idea

**The dispatcher** is a character on the radio. Say what is actually happening — not just a status
code — and it answers like somebody on the other end.

**The people** answer when you talk to them. They answer *from their situation*, because LSPDFR
reports it: stopped, searched, cuffed, caught with contraband, surrendered, or running. A compliant
driver who has been pulled over does not answer like a hostile one who is fleeing. Each person also
keeps a **name and a temperament** derived from their identity, so the same suspect is the same
character every time you meet them.

## Words and action are separated on purpose

This is the part that matters, and it is not a prompt:

- **Working out what you're asking for is deterministic code.** "Send me another unit" is classified
  as a backup request, and it *actually calls* LSPDFR's `RequestBackup(Code3, LocalUnit)`. A model
  never decides whether a unit is dispatched.
- **The model only writes the words.** It receives facts about the situation and returns one line of
  radio traffic. Nothing it sends is ever parsed as a command, so it has no authority.
- **It cannot lie about what happened.** If the model's answer claims units are "en route" when
  nothing was requested, that answer is discarded and the scripted line is used instead
  (`DispatcherBrain.ClaimsUnitsIncoming`). The dispatcher says responders are coming only when they
  really were sent.

## Ollama

`Plugins\LSPDFR\TextDispatch.ini` (it is written next to the DLL, which lives in `Plugins\LSPDFR`):

```ini
AiMode=auto          ; auto | llm | scripted
AiProvider=ollama    ; ollama | lmstudio
AiEndpoint=          ; blank = the provider's default
AiModel=             ; blank = the first model the server reports
```

| Provider | Default endpoint |
|---|---|
| `ollama` | `http://localhost:11434/api/chat` |
| `lmstudio` | `http://localhost:1234/v1/chat/completions` |

Anything else is treated as an OpenAI-compatible chat server.

**`auto` (the default)** probes once at startup. If a model answers, the dispatcher and the NPCs both
use it. If nothing is listening, the built-in script runs and everything still works — so a machine
with no model installed behaves exactly as it did before any of this existed.

To use Ollama:

```powershell
ollama serve
ollama pull llama3.2
```

Then start the game — `auto` will find it. Or force it live from the F4 console with `tdmode llm`,
and check what the server has with `tdmodels`.

> **Verified without a model.** Both request/response shapes — Ollama's native `/api/chat` and the
> OpenAI-compatible form — were tested against a stub server answering exactly like each, driving the
> real `LocalModel` code: model listing, reply parsing, auto-resolution of a blank `AiModel`, and the
> unavailable-server fallback. That test found a genuine bug (JSON arrays arriving as `ArrayList`
> rather than `object[]`, which silently emptied every reply and model list). What is *not* proven is
> how a specific real model behaves in conversation.

## More callouts

**LSPDFR itself ships only three callout classes.** Everything you think of as "callouts" comes from
packs. TextDispatch works with every pack automatically - it finds callouts by walking the assembly,
and reads each pack's own display name - so there is nothing to configure. Install a pack, restart,
and `/calls` shows it.

```
> /calls
[DISPATCH] Callouts: 214  from 5 pack(s)
           Armed Robbery  -  UnitedCallouts
           Bicycle Theft  -  686Callouts
           ... 194 more - narrow it with '/calls <text>'.
> /calls robbery
           Armed Robbery  -  UnitedCallouts
           7-Eleven Robbery  -  686Callouts
```

With hundreds installed the list is capped at 20 lines in the box and written in full to the log.
`tdcallouts` in the F4 console prints the pack names and counts.

### What to install

1. **RAGENativeUI** - nearly every callout pack needs it, and it must be present *before* the packs.
   Without it a pack either does nothing or takes the game down on load.
2. **The packs**, in small batches, restarting and testing between each. Callout packs are the
   single most common cause of LSPDFR crashes; installing twelve at once means never learning which
   one is responsible.
3. **Interaction frameworks** some packs depend on - StopThePed, Ultimate Backup, Policing Redefined.
   Each pack's page lists its own requirements.

Some packs are on GitHub and download directly (`sEbi3/UnitedCallouts`, `YobB1n/YobbinCallouts`,
`SSStuart/SSStuartCallouts`, `Mitsutan/MizCallouts`, `DekoKiyo/DynamicLSPDFR-Dev`). The rest are on
lcpdfr.com, which needs an account to download anything.

> Two packs can define a callout with the same name. TextDispatch shows the pack next to each entry
> so it is obvious which is which; `/callout <name>` picks the first match and logs which it used.

## Traffic stops

Both ways work, and the point is that the stop becomes a conversation.

**LSPDFR's own control** — pull someone over the way you normally do. The plugin notices on its own
and dispatch comes on the air:

```
[DISPATCH] 2A19, I show you on a traffic stop. Run the plate and tell me what you have.
> /record
[LOCAL]    You run the plate.
[LOCAL]    Registered owner: Marisol Vasquez
> /id
[LOCAL]    Marcus Reyes: It's in the car. You want me to get it?
> /frisk
[LOCAL]    Nothing illegal on them.
> /cuff
[LOCAL]    * You cuff them and pat them down.
[DISPATCH] Copy 2A19, one detained. Ask me for transport when you are ready.
> /transport
[DISPATCH] Copy 2A19, transport unit en route.
> /endstop
[DISPATCH] Traffic stop cleared. File it before you close the call.
```

**From the keyboard** — `/stop` asks LSPDFR to pull over the nearest vehicle within 30m; `/endstop`
releases it.

While a stop is running, `/id`, `/frisk`, `/cuff`, `/record`, `/owner` and `/transport` all act on the
**driver you stopped** — not on whoever happens to be nearest, which is usually a passer-by.

> `/stop` calls LSPDFR's `StartPulloverOnParkedVehicle`, which returns nothing. The confirmation is
dispatch announcing the stop a second later — so if dispatch stays quiet, LSPDFR did not take it, and
its own control is the fallback.

## Talking to people

Type anything and it is spoken where you are standing.

| Say it like | Carries |
|---|---|
| plain text, or `/s <text>` | 15m — normal speech |
| `/w <text>` | 3m — a whisper |
| `/shout <text>` (or `/y`) | 35m — a yell |
| `/me <action>` | an action — `* You draw your baton` |
| `/do <text>` | scene description — `(( the door is hanging open ))` |
| `/b <text>` | out of character, grey |
| `/who` | list who is nearby, with names and moods |
| `/talk [n]` | speak to whoever is nearest - or `/talk 3` for a specific person from `/who` |
| `/endtalk` | drop the current target |

Talk to dispatch with `/r <text>`, or just type a status code.

### Conversations, not one-liners

Replies used to be capped at one short line, and the model was shown only the last six exchanges - about
two turns, which is why people seemed to forget what they had just said.

- **Twenty lines of history** go to the model now (`AiHistoryLines`), so it knows what it said a moment
  ago.
- **Replies are one or two sentences, up to about 45 words**, and it is told to answer *and* keep the
  exchange going - to deflect, complain, or ask something back.
- **It is told never to repeat a line it has already used.**
- **Everyone has a life.** Age, job, district and history come from the same records the terminal reads,
  so the person who says "I'm a mechanic from Sandy Shores" is the person `/person` describes. Somebody
  with a warrant knows the police are looking for them, and it colours what they say.
- **`/me` and `/do` are part of the conversation.** Announcing that you have drawn your baton now reaches
  the person you are talking to instead of vanishing.

The reply deadline went up to 15 seconds, and the log complains when a reply takes over six - a slow
machine is exactly how a conversation would quietly fall back to the scripted line again.

> Without a model running there is no conversation to be had: the script answers, and it answers in one
> line. That is the honest limit of the no-setup path.

### Orders they actually carry out

Some sentences are not conversation. They are recognised by the plugin - never by the model - and the
person in front of you is made to do them, if they are willing.

| You type | They |
|---|---|
| `hands up` · `let me see your hands` | raise their hands |
| `step out of the vehicle` · `get out of the car` | get out |
| `look at me` | turn to face you |
| `don't move` · `stay there` | stop and wait (LSPDFR's own stopped state) |
| `you can go` · `move along` | walk off |

Willingness follows temperament, so what they say and what they do agree. A compliant driver complies;
a hostile one refuses, and their reply already reads as a refusal rather than obeying under protest.
Someone already in cuffs always complies.

The log records every order and whether it landed, naming the signature that worked - these are GTA
natives resolved by name at run time, and one or two changed shape between game builds.

## Commands

[`COMMANDS.md`](COMMANDS.md) beside this file lists **every** command and alias — 125 of them, which
is every case label in the router's command switch — with the words that work without a slash: the
status codes, `accept`, and the orders a pedestrian obeys. The summary below is the short version.

Plain text is speech. A leading `/` is a command. A bare status code is radio traffic.

**Status** — bare codes work too
`10-8` `10-7` `10-6` `10-97` `10-98` `10-13` `code 3` `code 4`

**Radio**
`/r <text>` — say anything to dispatch · `/r send me backup` / `/r I need EMS` work as you'd expect

**Records over the radio, too.** `/r run 4ABC123` and `/r person Marcus Reyes` are answered from the same
ledger the terminal reads — procedurally, so the reply is the record rather than a sentence about one,
and it costs no model time. A plate is recognised by its shape (four to eight characters with letters
and digits in it), so `/r what is 4ABC123` works as well as `/r run the plate 4ABC123`. A name only
matches if the records already hold it and the sentence asks about a person, so ordinary conversation
cannot turn into a records check by accident.

**Callouts**
`/accept` · `/yes` (or just type `accept`) · `/decline` · `/no` · `/calls` · `/callout <name>` · `/endcall` · `/available on|off`

A call waiting for an answer understands the bare word `accept` too, because that is the word dispatch
says. `yes` and `no` on their own stay speech - they are the words somebody says to a suspect.

`/calls` lists every callout this install has — LSPDFR's own plus every pack's, read from the
assembly — and `/callout` starts any of them by name.

**Traffic stops**
`/stop` · `/endstop` · `/tow` · `/record` (or `/plate`) · `/owner`

**The person**
`/id` (or `/licence`) · `/frisk` (or `/search`) · `/search car` · `/cuff` · `/detain` · `/release` (or `/uncuff`) ·
`/transport`

**Services**
`/ems` (or `/ambulance`, `/medic`) · `/fire` · `/backup [swat|air|state|ems|fire|transport|code2]` ·
`/zone`

**The car**
`/lock` · `/unlock` · `/engine [off]` · `/trunk` · `/hood` · `/doors` · `/repair` (or `/fix`) ·
`/veh <model>`

### Civilian jobs — DriverJobs V

If DriverJobs V is installed, its work is in the same box as everything else. It is a ScriptHookV
script with no plugin API, so TextDispatch reads the file the mod itself loads —
`scripts\DriverJobsData\Missions\Jobs.xml` — rather than keeping a copy: what `/jobs` shows is what
the mod actually has, including a job edited or added by hand, and a mod that is not installed is one
sentence rather than a fault.

```
/jobs              every civilian job, with what each pays
/jobs taxi         the ones matching a word
/job LS Medical    what the work is, what it pays, what you drive, and marks where it starts
/job 13            the same one by its number, which is handy when two jobs share a name
```

`/job` puts a blip and a sat-nav route on the start point, and replaces the last one it made. It
cannot *take* the job: the mod owns that, and a job is started by being at its place or from the mod's
own menu (`Shift+J`).

### The records terminal

GTA World's MDT is a server database - which is exactly why it is the one part of that server's command
set worth rebuilding here. A database needs no other players.

Thirty-five people and thirty-four vehicles are on file, generated from a **fixed seed**, so the town is
the same town every session and the person the terminal flags as wanted is still that person tomorrow.
Anyone you meet in the street is given a record the first time you deal with them - under the same name
the chat box shows - and keeps it.

| | |
|---|---|
| `/mdt` | what is on file, and what you have filed |
| `/person [name]` | their age, job, district, priors, warrants, fines and vehicles |
| `/plate [plate]` | registration, insurance, stolen flag, and whether the owner is wanted |
| `/warrant [name]` | outstanding warrants only |
| `/bolo` · `/bolo add <plate or name> <reason>` · `/bolo clear <subject>` | file, read and clear BOLOs |
| `/arrest [name] [for <charge>]` | books them; if they are in front of you it also cuffs them |
| `/cite <name> <offence>` | a $250 citation on their record |
| `/report <what happened>` | a written report on the person you last dealt with, for the call you are on |
| `/reports [name]` · `/court` | the reports on file; what is still with the courts |

`/person` and `/warrant` with no name describe whoever is in front of you. `/plate` with no plate runs
the car you have stopped. Name lookups match on part of a name, the way somebody would actually ask -
`/person Vasquez` finds "Marisol Vasquez".

**Records are kept between sessions**, in `Plugins\LSPDFR\TextDispatch.records.json` (with the previous
copy as `.bak`). The town is still seeded the same every time; what is saved is what you have done to it -
the people you met and booked, the cars you searched, BOLOs, reports, cases still with the courts, the
shift history, and your callsign. Delete the file to start the town over.

**What comes back.** An `/arrest` or a `/cite` is answered on the radio three to eight minutes later -
charged, pleaded guilty, bailed or dropped; paid, contested, or a warrant for not paying - and the outcome
goes on the person's record. An arrest with a `/report` behind it is far likelier to be charged, which is
the reason to write one. A case still open when the game closes is delivered next session.

**Vehicle searches.** `/search car` searches the car you stopped, or the one beside you. What is in it is
decided once from the plate and kept, so the same car holds the same thing every time; a stolen car, or a
driver with a warrant or priors, is likelier to be carrying something. The driver knows what was found, so
what they say next is about it.

**The shift.** `10-8` starts one and `10-7` ends it: dispatch reads back the calls, stops, pursuits,
arrests, citations, searches and reports, and files it. `/status` is the one-line answer to "where am I",
`/shift` is the shift so far, `/shifts` the history.

> **`/tow` really takes the vehicle.** LSPDFR has no towing system and there is no tow plugin here to
> borrow one from, so it does what a tow ends with: the car leaves the world and dispatch says the
> recovery is arranged. A command that only printed text would be a service pretending to happen.
> `/detain` uses LSPDFR's own stopped state, and `/release` takes the cuffs off and clears their tasks.

**Pursuit**
`/pursuit` · `/calledin` · `/endpursuit` · `/panic` · `/911 <details>`

**The box itself**
`/pos <corner>` · `/margin <px>` · `/ui <scale>` · `/font <name>` · `/fontsize <n>` · `/lines <n>` · `/clear`
`/key <key>` · `/chatter quiet|brief|full`

**The install**
`/plugins` - what LSPDFR actually loaded, what it did not, and anything installed where LSPDFR never
looks. Also written to the log a few seconds after you go on duty, without being asked.

The box sits in the **top-right** by default. An LSPDFR install is crowded and the top-left is the
most contested patch of screen in the game, so putting a chat box there makes it and whatever else is
drawing unreadable. `/pos bottom-right` moves it; the choice is saved to the ini.

## Controls

| Key | Does |
|---|---|
| **Left arrow** | Open the chat box - configurable, see below |
| **/** | Open the chat box with a slash already typed |
| **Enter** | Send |
| **Esc** | Close and clear |
| **Up / Down** | Command history |
| **PgUp / PgDn** | Scroll the transcript |
| **Ctrl+V** | Paste (yes, really) |

**The key that opens the box is yours to choose.** Left arrow by default, because T is claimed by other
mods on a lot of installs and a chat box whose key opens something else is one you stop using. `/key F6`
changes it while playing and saves it; `OpenKey=Left,F6` in the ini gives it two. `/` opens the box with
a slash already typed - a way in that no other mod can take.

**How much dispatch says is a setting too.** `/chatter quiet | brief | full`: `quiet` is only what asks
you something and the answers to what you said; `brief` (the default) adds the callout cycle; `full` is
everything, including narration about calls that have already finished. It is in the box because the
right answer changes with what you are doing - `quiet` while you are reading your way through a callout,
`full` while you are waiting for something to happen. Lines kept out of the box are still written to the
log, and still remembered as radio traffic, so the dispatcher's side of the conversation stays whole.

While the box is open the game's controls are frozen, so typing `10-97` does not also steer the car.

### Typing, and your other mods

Freezing the game's controls stops the *game* reacting to a sentence. It does nothing about the other
plugins, which read the keyboard themselves — which is why typing `10-97` used to open whatever menu
was watching for those letters.

There is no RAGE Plugin Hook call that says "not while he is typing", so the keystrokes are taken out
of the pipe instead, in the two places one can be caught on its way in:

- a **system-wide low-level keyboard hook**, installed after RAGE Plugin Hook's own, so it is called
  first and the key is dropped before RPH ever sees it;
- a **hook on the game window's own message procedure**, which also catches raw keyboard reports — the
  ones that do not travel through the other hook at all.

Both are installed at startup and either alone would do. While the box is open, and only while the game
window is in front, the keystrokes are hidden and read from the hook instead, so the box keeps working —
and typing into another window on another monitor is left alone, which on a multi-monitor install
matters as much as anything here.

Nothing is taken on trust. The first time you open the box, the plugin hides a key of its own and then
asks the game whether it can still see it. `/typing` reports which of these it found:

> a key hidden from the game was invisible to it, so other plugins do not see what is typed

or, on an install whose plugins read the keyboard hardware directly rather than the game's messages:

> a key hidden from the game was still visible to it …

That second answer is covered as well: a hidden press is followed by a synthetic release, which empties
the hardware state too — a key that went down and up inside one frame is far too fast for a menu to open
on. `HideHardwareKeys` in the ini controls it, and `/typing hardware off` turns it off.

`/typing off` gives all of it up and lets the other plugins see what you type, exactly as they did
before any of this existed. Alt with any key, and anything another program synthesises, is always let
through — Alt+Tab is not something to break in the name of a chat box.

## Install

1. Build:
   ```powershell
   dotnet build src\TextDispatch\TextDispatch.csproj -c Debug
   ```
2. Copy `src\TextDispatch\bin\Debug\TextDispatch.dll` into your GTA V **`Plugins\LSPDFR\`** folder
   (create it if it is not there). Or run `tools\install-textdispatch.ps1`, which builds it, puts it
   in the right folder, and cleans up a copy in `Plugins\` if an earlier version left one there.
3. Launch **RagePluginHook.exe** (not the normal launcher) and load into story mode.
4. **Go on duty** - press **E** at a police station.
5. The box appears saying which key opens it. Press that key - the **left arrow** unless you changed
   it. `T` is claimed by other mods on a lot of installs, which is why it is not the default.

> **LSPDFR loads `Plugins\LSPDFR` when you go on duty - not when the game starts.** Nothing in that
> folder runs before then: no callout pack, no StopThePed, no chat box. Waiting in story mode changes
> nothing, however long you wait, and a plugin folder that seems to do nothing is behaving normally.
> Pressing **E** at a police station is the step that starts everything. RPH's log shows it:
>
> ```
> [2:02:59.847] LSPDFR: Player went on duty.
> [2:02:59.857] LSPDFR: Folder is D:\Grand Theft Auto V Legacy\plugins\lspdfr   <- the scan starts here
> [2:03:00.260] LSPDFR: Creating plugin: _686_Callouts.Main
> [2:03:03.784] LSPDFR: Creating plugin: TextDispatch.Main
> ```
>
> If RPH's log has no `Creating plugin:` lines at all, you have not been on duty yet. That is the
> whole answer, and it is not a fault in the plugin.

> **It must be `Plugins\LSPDFR`, not `Plugins`.** RAGE Plugin Hook gives every plugin it loads its
> own AppDomain, and a plugin in RPH's AppDomain cannot see LSPDFR's types at all. It runs, it draws,
> and every call into LSPDFR fails silently - no callouts, no dispatch, no ped state. Plugins in
> `Plugins\LSPDFR` are loaded by LSPDFR itself, into LSPDFR's AppDomain, which is what makes the
> whole API reachable. The log says which happened on its first line: `detected, LSPDFR 0.4.9` or
> `not installed`.

On first run the plugin writes `Plugins\LSPDFR\TextDispatch.ini` next to itself, and logs to
`Plugins\LSPDFR\textdispatch.log`.

> **Building needs the RPH SDK.** `tools\rph-sdk\RagePluginHook.dll` must exist. It ships inside the
> official RAGE Plugin Hook download as `SDK\RagePluginHook.dll` — copy it there. The build refuses
> to run without it, and never copies it into the output: RPH's terms forbid redistributing it.

## Console fallback

Everything can be driven from RPH's **F4** console, which matters if the box ever draws wrongly:

| Command | Does |
|---|---|
| `tdsay <text>` | Send a line as if typed into the chat box |
| `tdwho` | List the people near you |
| `tdmode auto\|llm\|scripted` | Change AI mode without editing the ini |
| `tdmodels` | Ask the model server what it has |
| `tdstatus` | AI mode, provider, resolved model, box state, log path |
| `tdscale <n>` `tdfontsize <n>` `tdlines <n>` | Fix the box without a rebuild |
| `tdcallouts` | List every callout and write it to the log |
| `tdplugins` | Every DLL in `Plugins\LSPDFR`, which ones loaded, and what is missing |

## If something is wrong

Log: **`Plugins\LSPDFR\textdispatch.log`** - beside the plugin and its ini, not in AppData.
`tdstatus` prints the exact path it is using.

RPH resolves overloads and finds LSPDFR by reflection, so most failures are recorded rather than
thrown. The log says which LSPDFR methods were found, which callouts were discovered, whether the
model answered or timed out, and any call that could not be matched to a signature.

A few seconds after you go on duty, the log also gets an `inventory:` section: every DLL in
`Plugins\LSPDFR`, which ones LSPDFR loaded, which ones it did not, what each one refers to that is not
installed, and anything installed in `Plugins\` or `lspdfr\` where LSPDFR never looks. `/plugins`
prints the same thing in the box.

It keeps two things apart on purpose, because confusing them is worse than saying nothing:

- a **missing dependency**, on a plugin that also failed to load - that is a real problem, and it says so;
- an **optional integration** that is not installed, on a plugin that loaded fine - that is a feature
  switched off, which is the common case and not a fault. Reporting those as faults teaches you to
  ignore the line, so the one time it matters it gets ignored too.

| Symptom | Look at |
|---|---|
| **The box disappears when you go off duty** | By design. Off duty the civilian jobs box is the interface and it has this corner, so this one stops drawing, stops hiding keystrokes and lets go of its key until you go back on duty - one box at a time, one key each. It used to be a *fault* instead: before 1.0.14, LSPDFR's `Finally()` (clocking off, as well as shutdown) shut the engine down, so the box was gone for the rest of the session, with `stopping` as the last line of the log. |
| **Nothing loaded at all - not this plugin, not the callout packs** | You have not been on duty. Press **E** at a police station: LSPDFR scans `Plugins\LSPDFR` at that moment, and not before it. |
| A plugin you installed is not in LSPDFR's list | `/plugins` - or `tdplugins` in F4, or the log. It names every DLL in the folder, which ones loaded, and which are in `Plugins\` or `lspdfr\` where LSPDFR never looks for them. |
| The game crashed after a while on duty | `/plugins` sorts the two cases apart. **Did not load** and **Missing dependency** mean something is actually broken. **Optional integrations not installed** means a feature is switched off and nothing is wrong - a callout pack's StopThePed or Callout Interface link, for instance. |
| It says a plugin "needs" something | Almost always an optional integration with another pack. Only the **Missing dependency** wording, on a plugin that also did not load, means you have to install something. |
| Nothing on screen at all | Is `TextDispatch.dll` in `Plugins\LSPDFR\`? Then read our log and RPH's. |
| **RPH's log never mentions TextDispatch at all** | If it is in `Plugins\`, move it to `Plugins\LSPDFR\` - RPH is not going to load it into the right AppDomain anyway. |
| Log says `starting; not installed` | LSPDFR is installed but invisible: the DLL is in `Plugins\` instead of `Plugins\LSPDFR\`. |
| Box draws but T does nothing | `AlwaysReceiveKeyEvents` needs the game in borderless/windowed. |
| No callouts in `/calls` | LSPDFR not loaded, or it loads after us — `ReloadAllPlugins` in F4. |
| Nobody answers you | `/who` — is anyone within range? Then check the log for the mode. |
| Answers are canned, not conversational | `tdstatus` — it is probably running scripted. `tdmodels` to check Ollama. |
| Ollama installed but not found | Is `ollama serve` running? Does `ollama list` show a model? |
| `/stop` says LSPDFR would not start a stop | Pull them over with LSPDFR's own control; `/stop` is a convenience, not a replacement. |
| Nobody came when you asked for backup | Dispatch requested one through LSPDFR; whether a car rolls up is LSPDFR's backup AI. |
| `/backup` says "requested through the normal channel" | The log names the LSPDFR signatures it found. |

## Licence

MIT — see [LICENSE](../../LICENSE).

That covers TextDispatch and nothing else. RAGE Plugin Hook and LSPDFR are separate works under their
own licences: they are not included in this project or in any download of it, and this licence does
not apply to them. The RAGE Plugin Hook SDK is required to build and its terms forbid redistributing
it, which is why `tools/rph-sdk/` is deliberately not in the repository.

## Design notes

### LSPDFR's lifecycle, and why nothing is torn down

LSPDFR calls `Initialize()` when it creates a plugin, and `Finally()` when the player goes **off duty**
as well as at shutdown. It does not call `Initialize()` again when they go back on duty; if anything it
calls `InitializeAgain()`.

That pairing looks like a subscription and its cleanup, and it is not one: treating `Finally()` as
"unload" means the plugin tears itself down every time the player clocks off, which is what killed the
chat box in 1.0.13 and earlier - the log said `stopping` at the moment of going off duty and nothing
after it. So `Finally()` now only writes a line to the log, `Stop()` exists for a real unload and is
deliberately not wired to it, and the engine keeps running across duty changes: its services read the
player's duty state from LSPDFR every tick and go quiet on their own.

- **Nothing of LSPDFR is bundled, and the API is reached by reflection.** The one compile-time
  reference is the plugin base class LSPDFR instantiates - the same reference every callout pack
  has - and it is marked so it can never be copied into the output. Every *call* into the API goes
  through a reflection bridge that resolves overloads against the runtime signatures, which is how
  two bugs (`RequestBackup`'s shape, and `StartPulloverOnParkedVehicle` taking three arguments) were
  found by reading metadata instead of by crashing.
- **The entry point is an LSPDFR plugin, not an RPH one.** LSPDFR instantiates a class named `Main`
  deriving from `LSPD_First_Response.Mod.API.Plugin`. That is the only way to run in LSPDFR's
  AppDomain, and an RPH-loaded plugin gets its own AppDomain where the API is invisible.
- **Dispatch and dialogue poll, they are not pushed.** A handful of getters a few times a second
  cannot be broken by an API that changed shape between LSPDFR builds, and everything stays on the
  game fiber.
- **One model turn at a time, per conversation.** A slow turn on the radio must not stall the suspect
  standing in front of you, so dialogue and dispatch each own a pump.
- **Nothing the model says can block the game.** A turn has a deadline and a scripted fallback; miss
  it and the conversation simply carries on.
- **Drawing is separate from state.** `Game.FrameRender` is a different thread from the fiber, so the
  transcript is read and written under a lock.
- **Speech is delayed on purpose.** An instant reply reads as a machine. The pause is what makes an
  NPC feel like somebody deciding what to say.
