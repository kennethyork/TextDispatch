# Releases, and how they are named

Three things ship from this repository, each with its own version and its own release.

| Product | Tag | Release name | Asset |
|---|---|---|---|
| TextDispatch | `v1.0.13` | `TextDispatch 1.0.13` | `TextDispatch-1.0.13.zip` |
| TextCallouts | `callouts-v1.2.0` | `TextCallouts 1.2.0` | `TextCallouts-1.2.0.zip` |
| TextJobs | `textjobs-v1.0.0` | `TextJobs 1.0.0` | `TextJobs-1.0.0.zip` |
| The bundle — all three together | `bundle-1.4.0` | `bundle 1.4.0` | `bundle-1.4.0.zip` |

**A release name is the product and its version. Nothing else.**

What changed belongs in the notes, which is where somebody who clicks the release is already looking. A
name carrying a sentence turns the releases page into a paragraph, and a list of paragraphs is hard to
scan — which is the only thing a releases page is for.

1.0.12 and 1.0.13 briefly carried descriptions ("say what loaded, and what did not", "say what it
actually means"). Both were renamed back to `TextDispatch <version>`, as 1.0.11 and earlier already
were. TextCallouts' first two releases did the same and were renamed too.

## Why the versions are independent

The bundle names a *set* — "these versions were tested together" — so it has its own version number
and moves whenever any of the plugins in it does. `bundle 1.4.0` is not TextDispatch 1.4.0.

It has carried two plugins (TextDispatch and TextCallouts) up to `bundle 1.3.5`. From `bundle 1.4.0` it
carries **three**: TextJobs is in it as well, which is why the zip now has two command lists —
`COMMANDS.md` for TextDispatch, `COMMANDS-TextJobs.md` for the other — and why the install notes describe
two folders rather than one, since TextJobs is a ScriptHookVDotNet script and lives in `scripts\`.

## Making one

```
powershell -ExecutionPolicy Bypass -File tools\package-release.ps1      -Version 1.0.14
powershell -ExecutionPolicy Bypass -File tools\package-textcallouts.ps1 -Version 1.2.1
powershell -ExecutionPolicy Bypass -File tools\package-textjobs.ps1     -Version 1.0.0
powershell -ExecutionPolicy Bypass -File tools\package-bundle.ps1       -Version 1.2.1
```

TextJobs is the one product that is not an LSPDFR plugin: it is a ScriptHookVDotNet script, and it
ships into `scripts\` rather than `Plugins\LSPDFR`. It is in the bundle from `bundle 1.4.0` onwards, so
the bundle's notes explain both folders; it also still ships on its own, for players who want the jobs
box without the police side.

Each builds, checks the built DLL's version against the version asked for, and writes the zip. The
bundle also fills its own version into `BUNDLE.txt`, so a download says which three it contains without
anyone having to open it.

None of the zips contain `RagePluginHook.dll` or `LSPD First Response.dll`: both are separate works
under their own licences, and neither may be redistributed.
