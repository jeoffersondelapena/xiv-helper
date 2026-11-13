# XIV Helper

Renamed from XIV Doctor on 2026-11-03: it is outgrowing the overlays (a Rotation Solver Reborn state button is next), so the name follows what it does. 
Renamed from Overlay Doctor on 2026-09-29: it now fronts the overlays, the Mac-side watchers' notes and a fix command, so the old name had grown too narrow. The command is `/helper`.

One in-game command that brings the ACT overlay stack back, relays the Mac's notes and reports the demiatma: `/helper`.

```
/helper            how the IINACT parser and the Browsingway renderer are right now
/helper fix        restart whichever layer is unwell, or load a plugin that is missing
/helper button     a window with the RSR hotbar button to drag onto a hotbar slot
/helper demiatma   the six demiatma counts and which kinds BOCCHI is told to skip
/helper horn       re-entry into South Horn after its timer: on, off, or the current state
```

## The RSR hotbar button

Rotation Solver Reborn runs in Manual mode here and is started and stopped by hand, so the plugin adds one
action to the game's hotbars: it shows whether RSR is running (two icons) and a press flips it between Off
and Manual through `/rotation manual` and `/rotation off`. The game never shipped such an action, so the
plugin injects a row the way Wrath Combo does (`XIVHelper/HotbarButton/`, ported under its BSD 3-Clause
licence, icons included; see THIRD-PARTY-NOTICES.md). RSR exposes no IPC getter for its state, so the state
is read from its `DataCenter` statics by reflection; a renamed field shows as "RSR not loaded" until the
plugin is adjusted. The hooks rest on signatures a game patch can move; when they do, the button fails to
set up, the plugin logs it and everything else keeps working.

It is a Dalamud plugin that depends on nothing but Dalamud, so it still exists when IINACT or
Browsingway failed to load. It talks to both over Dalamud's plugin-to-plugin channel (`IINACT.Healthy`,
`IINACT.Restart`, `Browsingway.Healthy`, `Browsingway.Restart`, provided by the macOS forks of those
plugins) and reaches for Dalamud's plugin manager only to load a plugin that is not running.

| File | What it is |
|---|---|
| `XIVHelper/Doctor.cs` | the decision: which steps, in which order (pure, tested) |
| `XIVHelper/PluginControl.cs` | load or reload another plugin through Dalamud's internals |
| `XIVHelper/Plugin.cs` | the command, the IPC calls, the chat lines |
| `XIVHelper/Demiatma/DemiatmaWatch.cs` | the demiatma tables and which ids go on or off BOCCHI's lists (pure, tested) |
| `XIVHelper/Demiatma/DemiatmaWatcher.cs` | the five-second poll in South Horn, the chat lines, the record of the plugin's own skips |
| `XIVHelper/Demiatma/BocchiLists.cs` | reaches BOCCHI's live disabled-id sets through its service container |
| `XIVHelper/Field/ReviveWatch.cs` | which revive dialog to answer when, and the thank-you phrase (pure, tested) |
| `XIVHelper/Field/ReviveWatcher.cs` | the half-second poll while dead in the Crescent, the chat line, the `/say` |
| `XIVHelper/Field/HornWatch.cs` | when a zone change counts as the timer's kick, the menu match, the timeouts (pure, tested) |
| `XIVHelper/Field/HornReentry.cs` | the timer read, the walk to the coordinator, the dialogs, Commence, the BOCCHI start |
| `XIVHelper/DiagLog.cs` | the per-window diagnostic log and its once-a-minute heartbeat |
| `XIVHelper.Tests/` | xunit; runs before every commit once the hook is enabled |

The IPC names above are a contract with the two forks (`iinact-fork`, `browsingway-fork`): change them in all
three places or not at all.

Every five seconds it also writes a heartbeat line to its per-window diagnostic log (`diag/helper-<start>-<pid>.log`
in the config folder). The Mac-side freeze watcher in `xiv-mac-tools` reads that file: a log that stops beating
while the game process lives is a frozen frame loop, and a last line of `unloading` is the plugin switched off on
purpose. When the game itself is closing the line is `unloading; game closing`: a process still alive two minutes
after that has stopped closing, and the watcher ends it. The `heartbeat:` prefix and those two lines are a contract with the watcher. Once a minute a `memory:` line gives the managed heap, what the runtime has committed, how much of the process is in RAM at that moment (its working set, which falls when the system swaps the window out; the `Process` sizes are not used because under Wine on macOS they ask the Wine server for every process and come back 0), the players in view, the territory, and the longest wait between two frames in that minute with the time it ended; the watcher's memory report reads it beside the Mac's own figures for the window, and holds each of its own readings against that longest frame so a reading that is felt in the game shows. A timer thread adds `frame loop stalled Ns; …` lines while the frame loop is not ticking (with a few runtime counters, so a capture shows whether other managed threads still run) and `frame loop resumed` when it ends; the watcher reads those too.

## Demiatma skips for BOCCHI

`/helper demiatma` prints the six demiatma counts and which kinds are being skipped.

In South Horn, every five seconds while BOCCHI is loaded, the plugin counts the six demiatma in the inventory. For every
kind already at three it puts that kind's FATEs and critical encounters on BOCCHI's two disabled-id lists, so Illegal
Mode farms only what the Phantom Weapon quest still needs; a kind that drops under three again (handed in, or another
character) gets exactly the ids the plugin added taken back out, and anything the player disabled by hand is never
touched. A complete kind's activity turned back on by hand goes off again on the next poll. The two pot FATEs
(Persistent Pots, Pleading Pots) are left alone because they also feed BOCCHI's pot-chest farming. Each change is one
chat line; when all six are complete the line says so, and with every demiatma activity off BOCCHI returns to camp and
waits there (its Idle state hands off to Returning). The drop table is BOCCHI's own (`OccultEventDrops.cs`, 4.2.0.19),
copied into `Demiatma/DemiatmaWatch.cs`; the ids are the game's FATE and DynamicEvent ids.

Why the live lists rather than BOCCHI's config file (decided 2026-11-13): BOCCHI has no IPC or command for the lists and
reads its file only when it loads, so steering it through the file would mean a reload in the middle of a run and a
blind `/bocchi illegal on` afterwards. Its autopilot checks the two `HashSet<uint>` on every decision, so adding an id to
the live set takes effect at once. The sets are reached through Dalamud's plugin list and BOCCHI's service container by
reflection (`Demiatma/BocchiLists.cs`; member names as of BOCCHI 4.2.0.19 and Ocelot). When a BOCCHI update renames one,
the plugin prints one chat line per BOCCHI load naming the member and leaves the lists alone; untick by hand until this
file is adjusted. The ids the plugin added are kept in `demiatma-skips.json` in the config folder so they are recognised
as its own after a restart.

## Raises and returns during BOCCHI runs

BOCCHI's own dead state only waits: no answer to the raise prompt, no way back to camp (its Return code refuses while
unconscious). So in the Occult Crescent (South or North Horn), while BOCCHI is loaded, the plugin takes over the revive
dialogs. A raise offer ("Accept Raise…", told apart from the return dialog by the game's own Addon texts, rows 112 and
197/117, so any client language works) is accepted at once. Once back on its feet with Transcendent on (which only a
raise grants, never a return) it sends `/say` with the auto-translate phrase "Thank you." (Completion group 2, row 216)
so the raiser reads it in their own language. After 20 s with nothing going on it summons the revive agent's return
dialog and confirms it, with one line in the local chat log, visible to nobody else. "Nothing going on" means no raise on
its way (nobody casting a raise spell or a Phoenix Down at the character, and the revive agent neither Revivable nor
Reviving) and no FATE or
critical encounter the character is still part of (the game's current FATE, or a dynamic-event id on the character, the
same two tests BOCCHI uses); the 20 s clock restarts whenever one of those was true, so a cancelled raise or a finished
FATE starts a fresh wait. Inside a FATE or CE it keeps waiting for a raise, which is likely there and keeps the credit. BOCCHI
treats the respawn like any revive and carries on from camp. The wait comes from the first line of
`revive-return-seconds.txt` in the config folder when that file exists. A death outside a BOCCHI run is left to the
player. (2026-11-13)

## Back into South Horn after the timer

`/helper horn on` switches it on, `/helper horn off` off, `/helper horn` shows the state; the setting lives in `horn.txt` in
the config folder and survives reloads. Off by default.

While the character is in South Horn the plugin reads the instance timer from the game's public content director every
half second. When the zone changes with that reading at 20 s or less the timer did it; a voluntary leave, a disconnect
or a teleport out shows minutes or hours left and is left alone. BOCCHI must be loaded, and once all six demiatma are
complete it stays out and says so. The way back follows what the game shows: land in Phantom Village (territory 1269 or
1278), walk to the Expedition Coordinator Jeffroy (NPC 1053611) with vnavmesh if he is more than 6 y away, interact, pick
the menu entry that names South Horn (the game's SelectString), confirm the yes/no that names it, press Commence on the
duty window (ContentsFinderConfirm, callback 8), wait for South Horn to load, then after 5 s send `/bocchi illegal on`.
Every step has a timeout (90 s, 10 min for the duty window since it waits on a free instance); on a timeout it stops,
says where, and leaves the rest to the player. The timer reading at each exit goes to the diagnostic log, so the first
real kick shows whether the 20 s threshold holds. (2026-11-13)

Build: `dotnet build XIVHelper -c Release` (needs a Dalamud dev install; on macOS XIV on Mac's).
The first build enables the versioned pre-commit hook (`git config core.hooksPath .githooks`).

A standing note ends with an instruction sentence. The default is neutral; put your own wording on the first line of `instruction.txt` in the plugin's config folder and it is used instead.
