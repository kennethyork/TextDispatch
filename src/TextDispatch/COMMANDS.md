# Every text command

The complete command surface of TextDispatch 1.0.17, taken out of the router rather than remembered —
**123 command names and aliases**, plus the words that work without a slash. Every case label in the
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
| `accept` or `/accept` or `/yes` | take the call dispatch announced |
| `/decline` `/no` | decline it |
| `/endcall` | close the call you are on |
| `/available on\|off` | go on or off the call list |

---

## Traffic stops, and everything on the scene

| | |
|---|---|
| `/stop` `/pullover` | start a traffic stop on the car in front |
| `/endstop` | end it |
| `/id` `/licence` `/license` | the driver's details |
| `/frisk` `/search` | search them |
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

`/mdt` (`/terminal`) · `/person [name]` (`/name`) · `/plate [plate]` · `/warrant [name]` (`/wants`) · `/bolo` · `/arrest` · `/cite <name> <offence>` (`/ticket`)

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
