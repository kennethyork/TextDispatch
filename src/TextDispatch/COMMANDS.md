# Every text command

The complete command surface of TextDispatch 1.0.50, taken out of the router rather than remembered —
**218 command names and aliases**, plus the words that work without a slash. Every case label in the
router's command switch is here, and nothing here is not in the router.

Anything with a `/` is a command. Anything without one is speech — except the status codes, the word
`accept`, and the orders below, which are recognised because a police radio *is* mostly codes and orders.

---

## Talking to people

| | |
|---|---|
| *anything you type* | **speak out loud.** People within 15 m answer you in text, in character |
| `/s <text>` `/say <text>` | say it (the same as typing it plainly) |
| `/w <text>` `/whisper <text>` | whisper — 3 m |
| `/shout <text>` `/y <text>` `/yell <text>` | shout — 35 m |
| `/me <action>` | *You do something* — an action, in the roleplay style |
| `/do <text>` | `(( a description of the scene ))` |
| `/b <text>` `/ooc <text>` | out of character |
| `/who` | who is nearby, with names and moods |
| `/talk` | talk to **whoever is nearest** |
| `/talk <n>` | talk to a specific person from `/who` |
| `/endtalk` | stop addressing one person |

### Orders — plain words, no slash, and they *do* it

Recognised by the plugin, not by the model, and carried out with GTA natives. Whether they obey follows
their temperament, so the reply and the action agree.

```
hands up · hands on your head · hands where I can see · show me your hands · let me see your hands
step out of the vehicle · get out of the car · exit the vehicle · get out and …
look at me · face me · eyes on me · look this way
don't move · freeze · stay there · stay put · hold still · wait here · everybody stay where you are
you can go · you're free · free to go · move along · on your way · beat it · clear out
```

---

## Radio and dispatch

| | |
|---|---|
| `/r <text>` `/radio <text>` | say anything to dispatch; it answers in character (`/radio` on its own is the RADIO tab) |
| `/911 <details>` | a 911 call — the details decide the response, so *"man with a gun"* sends units |
| bare codes | `10-8` `10-7` `10-6` `10-97` `10-98` `10-13` `code 3` `code 4` (or `ten eight`…) |
| `/panic` | emergency, all units |
| `/backup` | a local unit, code 3 |

**Records over the radio** — answered from the same ledger the terminal reads, procedurally:

```
/r run 4ABC123          /r what is 4ABC123          /r person Marcus Reyes
```

### `/backup <kind>` — asks another plugin first when one is installed

```
code2  2            code3            swat  noose        localswat
air  helicopter     nooseair         state              female
k9  dog  statek9    pursuit          felony             group
traffic  stop       spikes  spikestrips              roadblock  block
coroner             animal           ems  ambulance  medic
fire  firetruck     tow              transport
```

`/ems` `/ambulance` `/medic` and `/fire` `/firedept` `/lsfd` are shortcuts for two of those.

---

## Callouts

| | |
|---|---|
| `/calls [filter]` | every callout this install has — yours, LSPDFR's, and every pack's |
| `/callout <name>` `/start <name>` | start one by name |
| `/911 <details>` | a 911 call: starts the callout that best fits what the caller said |
| `/auto` `/auto on\|off` | automatic calls: one comes after AutoCalloutSeconds (120 by default) free |
| `/auto <seconds>` | how long a free patrol waits for an automatic call, 30-3600 |
| `/auto accept on\|off` | whether calls are accepted for you (on by default) |
| `accept` or `/accept` or `/yes` | take the call dispatch announced |
| `/decline` `/no` | decline it |
| `/endcall` | close the call you are on |
| `/available on\|off` | go on or off the call list |

---

## Civilian jobs — DriverJobs V

DriverJobs V is a ScriptHookV script with no plugin API to ask, so these read the same file the mod
loads — `scripts\DriverJobsData\Missions\Jobs.xml` — rather than a copy of it. A job you edited or
added by hand shows up here too, and a mod that is not installed is one sentence, not a fault.

| | |
|---|---|
| `/jobs [filter]` | every civilian job the mod has, with what each pays |
| `/job <name or number>` | what the work is, what it pays, what you drive — and marks where it starts on your map |

Starting one is not TextDispatch's to do: the mod owns that, and a job is taken by being at its place,
or from the mod's own menu (`Shift+J`).

---

## Traffic stops, and everything on the scene

| | |
|---|---|
| `/stop` `/pullover` | start a traffic stop on the car in front |
| `/endstop` | end it |
| `/id` `/licence` `/license` | the driver's details |
| `/frisk` `/search` | search them |
| `/search car` `/searchcar` `/searchveh` `/vsearch` | search the vehicle you stopped, or the one beside you - what is in it is kept on file, and the driver knows what you found |
| `/cuff` | cuff them |
| `/detain` | hold them where they are |
| `/release` `/uncuff` | let them go |
| `/record` | their record |
| `/owner` | who the vehicle belongs to |
| `/tow` `/impound` | have it towed |
| `/transport` | transport for a prisoner |
| `/zone` | what area you are in |
| `/pursuit` `/calledin` `/endpursuit` | start a pursuit, call it in, end it |

All of those act on **the driver LSPDFR is holding at your stop**, not on whoever happens to be closest —
and on the nearest person when you are not on a stop.

---

## The car you are in

`/lock` · `/unlock` · `/engine [off]` · `/trunk` · `/hood` · `/doors` (`/shut`) · `/repair` (`/fix`, `/fixveh`) · `/veh <model> [department]` · `/veh <department>` · `/veh off` · `/veh help` (`/spawn`)

| | |
|---|---|
| `/veh police3 sahp` | bring a car in a department's livery - from whichever copy of the car carries it; with no department, the fleet's (below), or else your duty agency's |
| *the fleet* | the police cars start every session in a San Andreas design - one the car came with, or one of the pack's own departments (lspd, bcso, sahp ...), never a real city's. Naming a department with `/veh` puts **every police car** in it instead - parked, on patrol, backup, the one you are in, and every one that turns up later. A car whose own texture has the design is repainted; one that does not is swapped for the copy that does, with its crew, speed, siren and plate. Only cars crewed by cops, medics, firefighters or you are swapped, never the car you have stopped, and a car whose crew is out of it at a scene waits until they are back in. Lasts the session |
| `/veh off` | back to the San Andreas designs: every police car in a real city's livery goes back to one of them |
| `/veh police3 plain` | the car as it came, in its own livery |
| `/veh atlanta` · `/veh atlanta-old` | a department on its own, in the car that suits it: the current design on the Interceptor, the old one on the Stanier, fire on the fire truck. A city, county or state (`fulton`, `dallas county`, `texas`), with `old` for the old style and `fire`, `sheriff` or `state` when a place has more than one. A civilian car with a city's name is `/veh phoenix plain` |
| `/veh help [text] [page]` | every department `/veh` takes on its own, as you would type it, by group - city police, county police, sheriffs, state, fire; a `*` has an old design too. Twenty lines a page; a word narrows it (`/veh help fire`); the whole list goes to the log |
| `/livery` (`/paint`) | the liveries this car has, and which one it is wearing |
| `/livery <department>` | repaint it - `sahp`, `San Andreas Highway Patrol`, or `highway` |
| `/livery <number>` · `/livery next` · `/livery back` | by number, or step through them - on **any** car with one livery or more, named or not |
| `/livery name <number> <name>` | give a livery a name, on any car: `/livery name 3 Atlanta PD`. From then on `/livery atlanta-pd` and `/veh <car> atlanta-pd` find it |
| `/livery unname <number>` | take your name off it again |
| `/liveries <car> [text] [page]` | every livery the car comes in, across all its copies: the department, its full name, and which copy carries it under which number. Twenty a page; a word narrows it; the whole list goes to the log |

`/livery` acts on the car you are in, or the nearest one within 10m when you are on foot. It works with
any livery: the numbered sign textures in a car's `.ytd`, or a mod kit's liveries (how most add-on police
cars carry them - those show the game's own names). Names come from every `TextDispatch.liveries*.ini` in
`Plugins\LSPDFR`, read together: the main one your livery tools write, `TextDispatch.liveries.packs.ini`
from the Patreon livery pack installer, and `TextDispatch.liveries.mine.ini` from `/livery name`. None is
required.

---

## Records terminal

`/mdt` (`/terminal`) · `/person [name]` (`/name`) · `/plate [plate]` · `/warrant [name]` (`/wants`) · `/bolo` · `/arrest [name] [for <charge>]` · `/cite <name> <offence>` (`/ticket`)

Kept between sessions, in `Plugins\LSPDFR\TextDispatch.records.json`.

| | |
|---|---|
| `/report <what happened>` | a report on the person you last dealt with, for the call you are on |
| `/report <name>: <text>` · `/report general: <text>` | on somebody named, or on nobody in particular |
| `/report draft [name]` | a draft written from what you actually did, put in the box to finish and send |
| `/reports [name, call or #number]` | the reports on file, newest first |
| `/court` `/followups` | what is still with the courts, what each case has behind it, and its chance of being charged |
| `/evidence <item>` · `/evidence <name>: <item>` · `/evidence` | log evidence against them, or list what is held |
| `/miranda` `/rights` | read the person in front of you their rights |
| `/interview` `/question` | question them - what they say goes on the case, and counts only after `/miranda` |
| `/cite <name> <offence>` · `/cite <offence>` | the fine comes from the schedule; with no name it is whoever is in front of you; `$400` on the end names your own |
| `/fines [filter]` | the fine schedule |
| `/warrants` · `/warrants serve [name]` | who you know is wanted, and a warrant service to go and get one |

An arrest or a citation comes back on the radio three to eight minutes later: charged, pleaded, bailed or
dropped; paid, contested, or a warrant for not paying. What decides an arrest is what it has behind it - a
report, the evidence (`/search car` and `/frisk` log what they find), and a statement taken after
`/miranda`. A confession without Miranda counts for nothing. `/court` shows each case's chance.

**Warrant service.** Somebody you have dealt with who is wanted now - an unpaid citation does it - is sent
to you as a call of its own now and then, or whenever you ask with `/warrants serve`. They are placed a few
streets away; how they take the knock depends on who they are. `/endcall` stands it down.

**Traffic stops are run for you.** When a stop starts, dispatch runs the plate and the driver and says
so when something comes back - stolen, uninsured, a BOLO, a warrant.

---

## The box's tabs — new in 1.0.50

The box has five tabs, so the radio does not bury a conversation and a text does not get lost in it:

| Tab | What is on it |
|---|---|
| **CHAT** | the scene: what you say and what people answer, `/me`, `/do`, `/ame`, and what your commands answer |
| **RADIO** | dispatch, the other units, `/d` and the other departments, 911 calls, the sergeant, roll call, the radar |
| **MDT** | the computer (below) |
| **PHONE** | texts, calls, the inbox, tips |
| **ALL** | everything mixed, as the box was before it had tabs |

- **Ctrl+Tab** goes to the next tab and **Ctrl+Shift+Tab** back (Tab on its own still completes).
  `/chat`, `/radio`, `/mdt`, `/phone` and `/all` jump straight to one - `/radio <text>` still transmits,
  and `/phone` shows your contacts as well.
- **Urgent lines are on every tab**: a P1 call, a call for help or a 10-13, a panic, shots fired, a warrant,
  a stolen car or a suspended licence on your stop, a radar alert. On the MDT tab the latest one shows over
  the last row for thirty seconds.
- **Every other tab counts what is new** on it - `RADIO (3)  PHONE (1)` - on a strip along the top of the
  box. The strip shows while the box is open, and while it is closed when something is waiting.
- A command's answer goes to the tab you typed it on.

---

## The computer — new in 1.0.48, a tab of the box since 1.0.49

The chat box has two tabs: **CHAT**, the conversation, and **MDT**, the mobile data computer. `/mdt` (or
`/computer`) switches the box to the computer and `/chat` switches back - same corner, same text, a few rows
taller while it shows a record. Every screen is one of the box's own lookups, so the computer and the
commands always agree. Chat that arrives while you are on the MDT tab is counted on the tab strip
(`CHAT (3 new)`) and is all there when you go back.

| | |
|---|---|
| `/mdt` | the MDT tab, on the home screen - your rank and status, the active call, what is on file, and what is waiting on you (arrests to book, reports sent back, warrants). `/mdt` again, or `/chat`, goes back to the chat |
| *a name or a plate* | on the MDT tab, typed on its own in the box: a person or plate search |
| `/mdt person [name]` · `/mdt vehicle [plate]` | the person in front of you or by name · the car you stopped or by plate |
| `/mdt warrants` · `/mdt bolo` · `/mdt cad [number]` · `/mdt units` | warrants · BOLOs · the dispatch screen · the unit board |
| `/mdt reports [name]` · `/mdt cases` · `/mdt flags [subject]` · `/mdt msg [contact]` | reports · investigations · flags and notes · the phone's messages |
| `/mdt next` · `/mdt back` · `/chat` | page through a long screen · back to the chat tab |

Warrants, flags, stolen cars and suspended licences show in red, BOLOs in amber.

---

## LSPDFR in text — new in 1.0.47

Built on what LSPDFR itself does: its pullovers, its people, its callouts, its stations.

### The stop, in your own words

Ask the driver anything in plain words and they answer from what is true about them - and lie on purpose
when they have a reason to:

| Ask | What decides the answer |
|---|---|
| *where are you headed?* | their mood |
| *have you been drinking?* | how much they have actually had - the same number `/fst`, `/pbt` and `/examine` use |
| *whose car is this?* | the registration: their own, a named friend's, or a stolen car they claim was lent to them |
| *anything in the car?* | what a search would find - they know before you do, and it shows |
| *mind if I search the car?* | consent or a refusal, put on the record for your report |
| *licence and registration* | LSPDFR's licence status - a suspended or missing licence is admitted, or not |

**People are who LSPDFR says they are.** Names in the box are the names on LSPDFR's own ID card, and
`/person`, `/id`, the stop run and the records take LSPDFR's age, licence status (valid, expired,
suspended, unlicensed), warrants and times stopped. A suspended licence is read out when the stop starts.

### Commands

| | |
|---|---|
| `/checks` `/runall` | the plate, the driver's licence and warrants, BOLOs and flags, and the insurance and registration checks of whichever plugin owns them - in one go |
| `/passengers` · `/id passenger` | who else is in the car · ask them for ID |
| `/fst [hgn\|walk\|stand]` | the field sobriety tests - all three, or one - with the clues written out |
| `/pbt` `/breathtest` | a preliminary breath test. The reading goes into evidence; over 0.08 is a DUI (`/charge dui`) |
| `/book [name]` | at any station: booking number, charges, prints and photographs, property bagged, a cell. The desk reminds you when you arrive with somebody to book |
| `/cad` · `/cad <number>` | the dispatch screen: every call now has an incident number and a priority (P1-P3) and a log - received, assigned, on scene, closed. Reports carry the number |
| `/board` | the units on the street within 2.5 km: callsign, code 3 / 10-8 / 10-6, distance and area. Units sent to you are marked |
| `/collision` `/crash` | at a crash: the damaged vehicles, each driver's statement, who was at fault and why - drafted into the report box |

---

## GTA World on LSPDFR — new in 1.0.44

The roleplay commands, paperwork that becomes forum posts, a world that remembers you, a career, a phone
and a radar. Everything here is text — no voice, no extra UI.

### Roleplay

| | |
|---|---|
| `/low <text>` `/quiet` | say it quietly — about 5 m |
| `/m <text>` `/megaphone` `/mp` | through the car's PA — twice a shout's range |
| `/ame <action>` | an action shown **over your head** for a few seconds, and in the box as `> You …` |
| `/attempt <action>` | GTA World's 50/50: *You attempt to force the door open and fail.* The result is binding |
| `/roll [sides]` `/dice` | a die, out loud — d6 unless told otherwise |
| `/examine` `/ex` `/look` | what you see: build, clothes, marks, manner, a smell of drink — or `/examine car` for its condition |
| `/examine me` | your own description |
| `/my <how you look>` | set it. The people you talk to see it and can remark on it. `/my clear` removes it |
| `/d <text>` `/dept` | the inter-department channel — LSSD, SAHP, LSFD answer |

### Paperwork as forum posts

| | |
|---|---|
| `/log` | how many lines this session has, and the options |
| `/log export` | the roleplay (speech, `/me`, `/do`, radio) as a chat log **and** as forum BBCode in the box's colours |
| `/log export all` · `/log export new` | everything, notices included · only what is new since the last export |
| `/report export [n]` | a report as a GTA World arrest/incident report form in BBCode — officer, subject, charges by section, evidence, statement, narrative — with a plain copy under it |
| `/report fix <n> <more>` | add to a report the sergeant sent back, and send it again |

Files go to `Plugins\LSPDFR\TextDispatch Logs\` and `Plugins\LSPDFR\TextDispatch Reports\`.
`/report draft` now writes in the first person, and puts the charge sheet in.

### The penal code

| | |
|---|---|
| `/penal [word]` `/pc` | the San Andreas penal code: section, title, class, fine, days. `/penal felony`, `/penal theft`, `/penal 4-` |
| `/charge <charge or section>` | put it on the charge sheet of whoever is in front of you — `/charge robbery`, `/charge 4-03` |
| `/charge <name>: <charge>` | on somebody by name |
| `/charges [name]` `/sheet` | the sheet and its totals · `/charges clear` starts again |
| `/arrest` | now books **everything on the sheet**. `/arrest for <charge>` adds that charge first; a warrant arrest is PC 4-08 |

A guilty plea is sentenced from the sheet (a third off what the code says), and the exported report lists
the charges by section.

### The world that remembers

| | |
|---|---|
| `/flag <name or plate>: <warning>` | a warning on file — *known to carry a knife*. Dispatch reads it out when they or the plate are stopped |
| `/note <name or plate>: <text>` | a note for the record. Both work on whoever is in front of you without the name |
| `/notes [name or plate]` `/flags` | what is on file · `/flag clear <subject>` |
| `/case open <title>` | open an investigation. Reports you file go under it |
| `/case` · `/case <n>` | the list · one case: people, reports, notes |
| `/case use <n>` · `/case add <n> [name]` · `/case note <n> <text>` | work it · name somebody in it · note on it |
| `/case close <n> <outcome>` · `/case export <n>` | close it · write the whole case as a forum post |

**People come back.** Now and then somebody you have arrested, ticketed or written up turns up again as a
new face in the street — rarely, at most once in eight minutes, never the same person twice a session.
When you are face to face: *(( You recognise Marcus Reyes - you have arrested them before, last for assault. ))*
They remember you too, and a stop on them says so.

### Department life

| | |
|---|---|
| `/rollcall` `/briefing` | the watch sergeant's briefing — assignment, BOLOs, who is wanted, open cases, the DA, reports sent back. Read by itself a few seconds into each shift |
| `/rank` `/career` | your rank, points and what they came from |
| `/certs` | Traffic, K9, Air support, Detective, FTO — and what each still needs |

**The sergeant reviews every report** a minute or two after it is filed: first person, long enough, the
charge said, the evidence listed, the rights mentioned when there is a statement. Approved, approved with
a note, or **sent back** — and a report sent back does not count for the DA until `/report fix` puts it right.
Promotions are announced at the end of a shift. `RequireCertifications=1` in the ini makes `/k9` and air
support wait for their certification.

### Phone

| | |
|---|---|
| `/phone` `/contacts` | your contacts: Sgt. Kowalski, Det. Ramirez, the DA's Office, the Tow Yard, Lucky the informant, and people you saved |
| `/text <contact> <message>` `/sms` | a text. The department answers from your records; people you met answer as themselves (with the model if it is running) |
| `/call <contact> <what you say>` | the same, as a call |
| `/inbox [contact]` `/messages` | the last ten messages |
| `/phone add <name>` | save somebody you have met |

On a shift some of them text first: the DA chasing a missing report, the sergeant with one sent back,
the detective on your case, Lucky with a name — or a plate, which becomes a BOLO. `PhoneTips=0` stops it.

### Radar

| | |
|---|---|
| `/radar` `/speed` | read both antennas once: patrol speed, and the strongest target front and rear — speed, model, plate, distance, closing or going away |
| `/radar front` · `/radar rear` | one antenna's reading on its own (`back` works for rear) |
| `/radar lock [front\|rear]` | hold the reading - the front target, then the rear, unless an antenna is named. `/cite speeding` puts the locked speed on the ticket and into evidence |
| `/radar on [front\|rear]` · `/radar off [front\|rear]` | both antennas watch (or just the one named), beep and fast-lock anything over the alert speed |
| `/radar alert <mph>` · `/radar clear` | set the alert speed (75 by default) · drop the lock |

---

## Status and the shift

| | |
|---|---|
| `/status` | your unit, status, call, time on scene, the next automatic call, and the shift so far |
| `/shift` | this shift in full |
| `/shifts` | past shifts, and the career totals |
| `/units` | the units sent to you: what, how far out, and whether they are on scene |
| `/chatter traffic on\|off` | whether the other units are heard on the radio (on; never at `quiet`) |

**Tab** completes a command, and then its argument - a callout's name, a person on file, a corner of the
screen. Tab again goes to the next match, Shift+Tab back; the first press lists what matches.

`10-8` starts a shift and `10-7` ends it - dispatch reads the summary back and files it. Going off duty does
the same.

---


## Features other plugins own — new in 1.0.16

Reached by reflection, so a missing plugin costs only its own commands. `/bridges` says which are running.

| | |
|---|---|
| `/bridges` `/frameworks` | what can be driven, and which plugins are installed |
| `/k9` `/dog` | a K9 unit |
| `/spikes` `/spikestrips` `/stingers` | spike strips ahead |
| `/roadblock` `/block` | a road block |
| `/pit` | a PIT manoeuvre |
| `/felony` `/felonystop` | a felony stop |
| `/coroner` | the coroner |
| `/animal` `/animalcontrol` | animal control |
| `/group` | a group of units |
| `/dismiss` `/standdown` | stand every unit down |
| `/insurance` | the vehicle's insurance, from StopThePed's records |
| `/reg` `/registration` | its registration |
| `/breath` `/breathalyzer` `/dui` | is the person in front of you over the limit |
| `/drugs` | are they using |
| `/platecheck` `/runplate` | ask dispatch to run the plate on the radio |
| `/pedcheck` `/runped` | the same for a person |

---

## Characters

| | |
|---|---|
| `/chars [text]` | the preset characters, numbered, with the agency and ped model each one uses |
| `/rename <number> <new name>` | names one - the name the character menu shows |
| `/rename <current name> = <new name>` | the same, when you know the name but not the number |

The characters you pick from at a police station are LSPDFR's preset characters, and each one's name is
in `lspdfr\data\cop_presets.xml`. `/rename` writes that file for you: a `.bak-names-<date>` copy is kept
beside it before anything is written, and the file is parsed again afterwards - if it no longer parses,
the copy goes back and nothing has changed. The new name shows the next time the character menu opens,
because that is when LSPDFR reads the file. A custom character is named in the game's own character
creator instead.

---

## The box, and the install

| | |
|---|---|
| `/help` | everything, printed in the box |
| `/plugins` `/plugin` | what LSPDFR loaded, and what it did not |
| `/key <key>` | which key opens the box (`/key Left,F6` for two) — saved to the ini |
| `/chatter quiet\|brief\|full` (`/spam`) | how much dispatch traffic you see |
| `/pos <corner>` `/corner [corner]` | which corner the box sits in |
| `/margin <px>` | distance from the screen edge |
| `/ui <scale>` `/font <name>` `/fontsize <n>` `/lines <n>` | how it looks |
| `/typing [on\|off]` | whether what you type is hidden from the other plugins — and what this install found |
| `/typing hardware [on\|off]` | whether a hidden key is also released in the hardware state (see below) |
| `/clear` `/cls` | empty the box |

---

## Keys

| | |
|---|---|
| **Left arrow** | open the box (`OpenKey=Left` in the ini; change it with `/key`) |
| **/** | open the box with a slash already typed |
| **Enter** | send · **Esc** close and clear |
| **Up / Down** | command history · **PgUp / PgDn** scroll · **Ctrl+V** paste |

While the box is open and the game window is in front, what you type is hidden from everything else in
the game — the other plugins never see the letters, so none of their menus open on the word `10-97`.
`/typing` says how that went, and turns it off if you would rather have the old behaviour. Alt with any
key, and anything another program sends, is always let through.

---

## F4 console — insurance if the box will not open

`tdsay <text>` · `tdstatus` · `tdplugins` · `tdcallouts` · `tdwho` · `tdmode auto|llm|scripted` ·
`tdmodels` · `tdkey <key>` · `tdpos <corner>` · `tdscale <n>` · `tdfont <name>` · `tdfontsize <n>` ·
`tdlines <n>` · `tdrender`
