# Design notes

These notes add detail to the [README](../README.md). They answer the questions a reviewer is most likely to ask about how the game works, with references to the actual classes.

## How is the board represented?

`Board` is a `rows × columns` grid of `Cell`s, addressed by the `BoardPosition(row, column)` struct. A cell holds a `Block`, an `Obstacle` (a Crate), or nothing, never both. A `Block` has a `BlockKind` (Normal, Rocket, Bomb), a `BlockColor` and, for rockets, a `RocketDirection`. None of these types reference `UnityEngine`, so the whole rule set runs in plain EditMode tests.

## Why is row 0 the bottom?

Gravity then means moving toward lower row indices, and a row maps directly to Unity's +Y axis. Logic and screen use the same orientation, so there is no flip that could cause an off-by-one error. `BoardView.BoardPositionToLocalPosition` does only centring and scaling.

## How are connected groups found?

`ConnectedGroupFinder` runs a breadth-first search from the tapped cell. It uses a `Queue` and a `visited` `HashSet` and expands only to orthogonal neighbours that hold a *normal* block of the same colour. Power-ups and crates stop the search. A group counts only if it has at least `GameplayController.MinimumGroupSize` (2) blocks.

## How does gravity stay deterministic?

`GameplayController.SettleBoard` repeats a fixed sequence until nothing changes:

1. **Vertical gravity** (`GravityResolver.ApplyGravity`). Each column is compacted downward within segments; a crate ends one segment and starts the next.
2. **Diagonal slides** (`ApplyDiagonalGravity`). An empty cell with a crate somewhere above it can take a block from the row above in a neighbouring column. The block can only move there if the side cell is not a crate, so blocks never pass through a crate wall. The preferred side alternates on a checkerboard, and the other side is the fixed fallback, so the same board always settles the same way.
3. **Refill** (`RefillResolver`, `topAccessibleOnly`). New blocks enter only cells that can be reached from the top of the board.

Each step returns `BlockMove` or `BlockSpawn` records. `BoardView` collects them into one route per block and animates the whole plan once. A block that falls, then slides, then falls again moves smoothly instead of stopping at each step.

## Why separate logic from presentation?

- **Testing.** The rules are covered by EditMode tests that need no scenes, frames or coroutines.
- **Determinism.** Given the same seed, the resolvers always produce the same board.
- **Timing is not rules.** Animation length can change without touching any rule. `GameplayController` is the only class that connects the two layers: it applies a rule and then waits for the matching animation.

## Why is `System.Random` injected?

`GameBootstrap` owns a single `System.Random` for the session and passes it to refill and shuffle. Tests pass `new System.Random(seed)` to get reproducible layouts. Creating a clock-seeded instance for every attempt could deal the same board twice on a quick retry.

## How do ScriptableObject levels work?

`LevelDefinition` holds authored data only:

- size, move limit and available colours;
- colour goals;
- crate placements and the crate goal.

`Validate()` rejects impossible configurations and names the asset in the error. Examples: non-positive size or move limit, duplicate goals, goals for colours not in the level, crates outside the board or on the same cell, or a crate goal larger than the number of crates. `GameFlowController` holds the ordered level array. `GameBootstrap.StartLevel` builds a new `Board`, places crates, fills and (if needed) shuffles it, and creates a new `GoalTracker`. The asset itself is never modified at runtime.

## Config vs runtime vs save?

| | Examples | Owner | Lifetime |
| --- | --- | --- | --- |
| Config | `LevelDefinition` | Asset | Shared, read-only |
| Runtime | `Board`, `GoalTracker`, `MovesRemaining`, `GameplayState` | `GameplayController` | One attempt |
| Save | `PlayerProgressData` | `SaveService` | Across sessions |

The save stores only the highest unlocked level, the save version and two preference flags. Board state is never saved, which keeps the schema small and easy to validate and version.

## How do Crates change gravity?

Crates are walls that never move.

- **Vertical gravity** treats each column as separate segments split by crates.
- **Diagonal slides** let existing blocks move sideways into gaps under a crate row, but only through an opening.
- **Refill** never creates a block inside a covered pocket. A fully enclosed pocket therefore stays empty until a blast breaks its "roof". The PlayMode test `EnclosedGap_StaysEmptyUntilRoofIsRemoved` covers this.

A group blast breaks crates orthogonally adjacent to any removed cell (`ObstacleResolver`). A power-up breaks crates inside its footprint.

## How do Rocket and Bomb differ from normal blocks?

They are `Block`s with a different `BlockKind`:

- **Not part of colour groups.** The group search skips them.
- **Tapping fires them.** `PowerUpResolver` computes a footprint (the whole row, the whole column, or a plus shape for the Bomb) and clears it.
- **Chain reactions.** Any power-up inside the footprint is queued and fires in turn. A `fired` set ensures each power-up fires once, so two rockets cannot trigger each other forever.

`PowerUpRules` decides creation:

- 5–6 blocks make a Rocket, oriented along the group's longer axis;
- 7 or more make a Bomb.

## Why pool `BlockView`s but not `Block`s?

A `Block` is a small C# object whose identity has meaning:

- goals count the removed instances;
- moves and spawns refer to them;
- tests assert on them.

Recycling blocks would make an old block and a new block look like the same object. A `BlockView` is a GameObject with a renderer, so instantiating it costs more. `BoardView` takes views from an `ObjectPool<BlockView>` sized to the board. On release, `BlockView.PrepareForPool` clears the bound block and resets the scale, rotation, sprite and tint (including alpha). The PlayMode test `CoveredGap_SettlesUsingExistingBlocksAndKeepsPoolInSync` checks that the views and the board stay in step.

## How is re-entrant input prevented?

- **Controller state.** `HandleBlockSelected` accepts a tap only in `GameplayState.WaitingForInput`. It switches to `ResolvingMove` *before* starting the resolution coroutine. A `finally` block returns it to `WaitingForInput`, unless the move ended in Won, Lost or Blocked. Taps during a move are dropped, not queued.
- **Modals and UI.** `BoardInputHandler.InputBlocked` is set while the settings modal is open. A current-position EventSystem raycast stops taps on UI from reaching the board. PlayMode tests cover both.

## How is a deadlocked board handled?

After every move that neither wins nor loses:

1. `BoardShuffleResolver` asks `MoveAvailabilityChecker` whether any valid group exists.
2. If none does, it runs a Fisher–Yates shuffle of the existing blocks, up to 100 attempts, until the board is playable. No move is spent.
3. If every attempt fails, the state becomes `Blocked` and the HUD offers a retry.

## What did profiling show?

Profiling ran in Editor Play Mode (iOS build target) on a seeded, scripted 40-move scenario.

- **CPU:** main-thread time was about 1 ms per frame.
- **GC:** there were no per-frame GC allocations from project scripts.
- **Atlas:** packing the 21 board-piece sprites into one atlas cut idle batches from 5 to 1 and idle draw calls from 28 to 20. CPU time did not change measurably, and texture memory rose by about 1.2 MB.
- **Not measured:** the device. Numbers from a real iPhone would be the next step.

## What would come next with more time?

- Profile on a physical iPhone and keep device captures as evidence.
- Strip or compile out the per-move `Debug.Log` calls in release builds.
- Add a full-flow PlayMode test: menu, then win, next level, and a reload of saved progress. This flow was checked by a scripted smoke run but is not an automated test.
- Add a second obstacle type (for example multi-hit) through the existing `ObstacleType`/`ObstacleResolver` seam.
- Add level select and a small editor tool for authoring levels.
