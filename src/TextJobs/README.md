# TextJobs — DriverJobs V in a chat box

TextDispatch's box and its keyboard, for DriverJobs V, as a ScriptHookVDotNet script. Install it in
`scripts\` beside `DriverJobs.dll` and press **F8**.

It is a **separate plugin from TextDispatch** and shares nothing with it at run time. TextDispatch is
an LSPDFR plugin: it cannot load without LSPDFR and does not exist off duty. This is a script for
ScriptHookVDotNet — the same framework DriverJobs itself uses — so it works with or without LSPDFR, on
duty or off, and needs nothing installed but the game and the jobs mod.

## What it does

| | |
|---|---|
| `/jobs [filter]` | every job DriverJobs has, with what each pays |
| `/job <name or number>` | the description, the pay, what you drive — and a blip and a sat-nav route on where it starts |
| `/key <key>` | which key opens the box (`/key F8`, `/key Right`) |
| `/pos <corner>` | which corner it sits in |
| `/hide on\|off` | whether what you type is hidden from the other plugins |
| `/hardware on\|off` | whether a hidden key is also released in the hardware state |
| `/clear` | empty the box |

It reads the file the mod itself loads — `scripts\DriverJobsData\Missions\Jobs.xml` — rather than a
copy, so a job you edit or add by hand appears in the list, and the list is re-read when the file
changes. DriverJobs not being installed is one sentence in the box, not an error.

**It cannot take a job.** The mod owns that: a job is started by being at its place, or from its own
menu (`Shift+J`). This adds what belongs in a box — what the work is, what it pays, what you drive, and
where it is.

**Typing is hidden from everything else in the game** while the box is open, exactly as TextDispatch
does it: a system-wide keyboard hook and a hook on the game window's procedure, so typing "San Andreas
Freight" cannot open another mod's menu. Alt with any key, and anything another program synthesises, is
always let through.

## Install

1. **Close the game.**
2. Copy **`TextJobs.dll`** into `<your game folder>\scripts\` — the folder `DriverJobs.dll` is already in.
3. Start the game. There is nothing to go on duty for: a ticker confirms it loaded.
4. Press **F8** and type `/jobs`.

## Requirements

GTA V (Legacy or Enhanced), **ScriptHookV**, **ScriptHookVDotNet** (the same versions DriverJobs V
needs), and **DriverJobs V** itself — without it the box still opens and says the job list is missing.

Not required: LSPDFR, RAGE Plugin Hook, or TextDispatch. If you run TextDispatch too, keep the two on
different keys — TextDispatch uses the left arrow, this uses F8, and two boxes on one key would both
open and both read the same keystrokes.

## What it deliberately is not

No callouts, no traffic stops, no dispatch, no records. Those are LSPDFR's and TextDispatch's, and a
second police plugin would only be a worse copy of the one that exists.

## Files

```
TextJobs.dll     the script        scripts\TextJobs.dll
TextJobs.ini     its settings     written beside the script on first run
TextJobs.log     what it did      written beside the script
```

## Licence

MIT — it covers this plugin only. DriverJobs V is a separate work under its own licence.
