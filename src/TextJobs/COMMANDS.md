# TextJobs — every command

The whole command surface of TextJobs 1.0.0, taken out of the command handler rather than remembered.
Anything with a `/` is a command; anything without one is not — there is no speech here, because
talking to people is LSPDFR's half of the idea and lives in TextDispatch.

---

## Jobs

| | |
|---|---|
| `/jobs` | every job DriverJobs V has, with what each pays |
| `/jobs <filter>` | the ones with that word in their name — `/jobs taxi`, `/jobs bus` |
| `/job <name>` | what the work is, what it pays, what you drive, and a blip + route on where it starts |
| `/job <number>` | the same, by the number `/jobs` showed — needed when two jobs share a name |

The list is read from `scripts\DriverJobsData\Missions\Jobs.xml`, the file the mod itself loads, so a
job you edit or add by hand appears here too, and the whole list is written to `TextJobs.log`.

A job is **taken** in the mod, not here: be at its place (the blip and route this marks) or use the
mod's own menu, `Shift+J`. Jobs the file marks `remote` can be started from that menu anywhere, as long
as you are in a suitable vehicle — `/job` says so when it applies.

---

## The box

| | |
|---|---|
| `/key <key>` | which key opens it (`/key F9`, `/key Right`, `/key Numpad0`) — saved to the ini |
| `/pos <corner>` | top-left, top-right, bottom-left, bottom-right — saved to the ini |
| `/hide on\|off` | whether what you type is hidden from the other plugins |
| `/hardware on\|off` | whether a hidden key is also released in the hardware state |
| `/clear` `/cls` | empty the box |
| `/help` | all of the above, in the box |

---

## Keys

| | |
|---|---|
| **F9** | open the box (`OpenKey=F9` in `TextJobs.ini`) |
| **Enter** | run the command · **Esc** close and clear |
| **Up / Down** | command history |
| **Backspace** | delete a character |

While the box is open, and only while the game window is in front, what you type is hidden from every
other plugin and script — including DriverJobs' own `Shift+J`. Alt with any key is always let through.

---

## Settings, in `TextJobs.ini` beside the script

`OpenKey` · `ChatPosition` · `ChatMargin` · `FontScale` · `Lines` · `TranscriptSeconds` ·
`BlockOtherModsKeys` · `HideHardwareKeys`

The two hiding settings are the same idea as TextDispatch's: `BlockOtherModsKeys` stops what you type
reaching the other plugins at all, and `HideHardwareKeys` also releases a hidden key in the hardware
state, which is what covers a plugin that reads the keyboard directly rather than the game's messages.
