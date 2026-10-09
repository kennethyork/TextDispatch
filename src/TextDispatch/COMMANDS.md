# Every text command

The complete command surface of TextDispatch 1.0.33, taken out of the router rather than remembered —
**147 command names and aliases**, plus the words that work without a slash. Every case label in the
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
| `/r <text>` `/radio <text>` | say anything to dispatch; it answers in character |
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

`/lock` · `/unlock` · `/engine [off]` · `/trunk` · `/hood` · `/doors` (`/shut`) · `/repair` (`/fix`, `/fixveh`) · `/veh <model>` (`/spawn`)

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
