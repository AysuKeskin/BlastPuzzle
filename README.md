# BlastPuzzle

A single-player, tap-to-blast puzzle game built with Unity 6000.6.0f1 and C#. Ten ScriptableObject levels share one gameplay scene.

## Run

1. Open this folder in Unity Hub using **6000.6.0f1** (the version in `ProjectSettings/ProjectVersion.txt`).
2. Let Unity restore the packages and import the assets.
3. Press Play, then select Play/Continue in the main menu. Editor Play Mode always starts from `Assets/Scenes/MainMenu.unity`, even when `Gameplay.unity` is open for editing. Builds also start from the main menu.
4. Use a portrait Game view. Check both 1080×1920 and 1080×2400; the orthographic camera fits the board when the screen size changes.

Click with a mouse or tap on a touch screen. Clear orthogonally connected groups of at least two matching blocks to advance the level's goals. Each valid action costs one move; invalid taps give visual feedback without spending a move.

- Groups of 5–6 create a rocket; groups of 7 or more create a bomb.
- Rockets clear a row or column. Bombs clear their cell and four orthogonal neighbours.
- Tap a power-up to activate it. Power-ups hit by another power-up trigger a chain reaction, with an activation effect and sound for each.
- Crates break from adjacent group blasts or direct power-up hits. After vertical gravity, existing upper-side blocks slide diagonally into reachable gaps below crates; open columns refill from above. Completely enclosed pockets retain a fallback refill. Blocks are clipped to the framed board boundary during entry.
- Complete every goal before running out of moves. Use Retry after a loss, Next Level after a win, and Play Again after the final level.
- A deadlocked board is shuffled without spending a move. If the bounded shuffle cannot find a valid arrangement, a separate no-matches retry prompt appears.

## Structure

| Area | Responsibility |
| --- | --- |
| `Core/GameFlowController` | Level selection, retry/next flow, persistent unlocks and save retries |
| `Core/GameBootstrap` | Compose a fresh attempt from a level definition |
| `Levels/LevelDefinition` | Authored dimensions, colours, goals, obstacles and move budget |
| `Boards`, `Blocks`, `Goals`, `Gameplay/*Resolver`, `PowerUps` | Board data and gameplay rules; most are plain C# |
| `Gameplay/GameplayController` | Sequence moves, animations and outcome evaluation |
| `Presentation` | Pooled block views, camera framing, animation, audio and haptics |
| `UI` | Event-driven uGUI/TextMeshPro HUD and result panels |
| `Persistence` | Versioned local JSON progress and file I/O |

Level assets are configuration. Each attempt creates a new board and goal tracker, leaving the assets unchanged. Block views are reused across refills and level transitions.

Progress lives in `player-progress.json` under `Application.persistentDataPath`. The highest unlocked level, save version, sound and vibration preferences are persisted; an in-progress board is not resumed. Saves use a temporary file before replacing the previous file. Failed writes stay pending and retry every five seconds of unscaled time, on level navigation, on wins, and on application pause/focus loss/quit, and before the gameplay scene is disabled. Persistence still requires a writable disk; pending progress cannot survive a forced shutdown if every write fails.

## Tests

In Unity, open **Window → General → Test Runner** and run the EditMode suite in `Assets/Tests/EditMode`. Run the PlayMode suite in `Assets/Tests/Integration` for settings input blocking, UI pointer isolation and chain-reaction feedback.

With Unity CLI installed and the project closed in the Editor:

```sh
unity test . --mode EditMode --output /tmp/blast-editmode.xml
```

Tests cover matching, gravity, refill, goals, power-ups, shuffle, save-file handling and controller rules. Camera framing, pooled views, scene navigation and HUD layout also need manual Play Mode verification; passing the EditMode suite alone does not verify these features.

## Scope and portfolio presentation

This is a local single-player project: there are no accounts, cloud saves or backend services. Level difficulty and win rates are not measured by the unit tests. Mobile input and haptics still need verification on a physical target device.

Code was developed with AI assistance. When presenting the project, explain the systems you implemented or revised and the design decisions you can demonstrate. A gameplay recording is not included yet; a useful demo would show a normal blast, crate removal, a power-up, a win and a retry, alongside the test results.

## Mobile builds

Use **BlastPuzzle → Build** in the Editor:

- **iOS Device Development**: debugging and profiler connection.
- **iOS Device Release**: non-development export for a physical device.
- **iOS Simulator Release**: non-development ARM64 Simulator export; restores Device SDK afterwards.

Exports go to `Builds/`. Open the generated `Unity-iPhone.xcodeproj` in Xcode. Physical devices require your own signing team. Do not commit generated builds.

Mobile rendering targets 60 FPS; this is a policy, not a hardware benchmark. See [Milestone 22 audit](Docs/MILESTONE22.md) for changes, test evidence, remaining device checks and design explanations.
