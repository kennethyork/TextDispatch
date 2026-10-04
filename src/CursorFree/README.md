# CursorFree

**Lets the mouse leave a borderless-windowed game window and reach your other monitors.**

GTA V in borderless windowed mode keeps the mouse inside its own window while it has focus. On a
single-monitor setup that is invisible; on a four-monitor desktop it means the cursor cannot reach
three quarters of your screen without alt-tabbing out of the game.

This is a small tray program that takes that restraint away on a hotkey, and hands it back when you
want to play again.

```
run CursorFree.exe          a tray icon appears
Ctrl+Alt+F                  the mouse is free - move it to any monitor
Ctrl+Alt+F again            the front window gets it back
```

## Why a toggle rather than "always free"

If the cursor were loose all the time, it would wander off to another monitor mid-aim and the game
would be unusable. Playing needs the mouse captured; looking something up on another screen needs it
released. So the useful thing is a switch, and that is all this is.

Right-clicking the tray icon gives the same toggle plus:

| Menu item | Does |
|---|---|
| **Free the cursor** | Same as Ctrl+Alt+F |
| **Hand it back to the front window** | Confines the cursor to whatever is in front — the game |
| **What is the clip right now?** | Says what the cursor is currently confined to, and writes it to the log |
| **Open the log** | `cursorfree.log`, next to the exe |

## What it does, precisely

Windows has one desktop-wide setting for this: `ClipCursor`, the rectangle the cursor is confined to.
When a borderless game takes focus it calls `ClipCursor` with its own window rectangle, and the mouse
is then trapped. A different process may change it back — that is the whole trick, and this program
does it from outside the game so nothing has to be installed into LSPDFR.

Being free is not a switch that stays thrown: the game re-applies its clip whenever it has reason to,
so while you have the cursor free a 25 ms timer checks and removes it again. **Every removal is
counted in the log**, which is the point — that number is the answer to a question you cannot ask from
inside the game.

## Being straight about whether this is the right fix

Before writing it I measured your desktop, and the honest result is: **it may not be.** The
measurements, which the test below takes from your machine:

```
desktop      : (0,0)-(6144,1080)   6144x1080      <- four monitors
GTA window   : (0,0)-(1920,1080)   1920x1080      <- your borderless window
clip observed: (0,0)-(6784,1080)   6784x1080      <- wider than the desktop, so NOT the game window
```

So while that was measured, the cursor was **not** being held by a `ClipCursor` set to the game
window. That leaves two possibilities, and the tool tells them apart in about a second of playing:

- **The game clips while it has focus.** Press Ctrl+Alt+F in game and watch the log: if removals
  start climbing, this was the problem and it is now solved.
- **The game re-centres the cursor instead** (games often `SetCursorPos` the mouse back to the middle
  of the window, which no clip tool can prevent). Then the log stays at zero removals while the mouse
  still will not leave, and the honest answer is that this program is not the fix — the options are
  the game's own release (Esc, or alt-tabbing), or a bigger tool that takes the cursor over with raw
  input, which I have not built.

Use **What is the clip right now?** while in game, focused, to find out which it is.

## Also worth knowing, from your settings.xml

```
<Windowed value="2" />            borderless windowed, as you said
<PauseOnFocusLoss value="1" />    the game pauses the moment it loses focus
```

That second line matters for how you use this: with it at `1`, clicking onto another monitor pauses
GTA V. If you would rather the game kept running while you look something up, set
`PauseOnFocusLoss` to `0` — then a freed cursor is genuinely useful, rather than a pause button.

## The mechanism is tested, not assumed

`CursorGate` (the part that touches Windows) has a test that runs against the real desktop, because a
mistake here moves your actual mouse pointer:

```
=== CursorGate, against this desktop ===
desktop      : (0,0)-(6144,1080)  6144x1080
clip to start: (0,0)-(6784,1080)  6784x1080
    ok    Free() lets the cursor cover the desktop
    ok    IsConfined() is false when free
    ok    Confine() sets the clip
    ok    IsConfined() is true when confined
    ok    Free() removes it again
    ok    the window rect is sane
restored    : (0,0)-(6784,1080)  6784x1080
RESULT: all checks passed
```

The important line is **`Confine() sets the clip`**: it proves a process that is not the game can
change the desktop cursor clip. The test confines the cursor only to a large rectangle, never a small
one, and always restores what it found — including on the way out of a failure.

## Building

```powershell
dotnet build src\CursorFree\CursorFree.csproj -c Release
```

The exe lands in `src\CursorFree\bin\Release\`. It needs .NET Framework 4.8, which Windows has.

`CursorFree.exe --status` prints the current clip, the desktop bounds and whether the cursor is
confined, without starting the tray icon — useful for scripting the same diagnostic.

## Requirements

Windows, .NET Framework 4.8. No installation, no dependencies, nothing written outside its own folder
apart from the log beside the exe.
