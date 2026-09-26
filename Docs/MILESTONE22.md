# Milestone 22 — Mobile polish audit

Date: 2026-09-25. Unity 6000.6.0f1. Primary build target: iOS. No physical phone available. This report distinguishes code checks, automated tests, visual checks and hardware verification.

## A. Already satisfied — unchanged

| Requirement | Evidence / decision |
| --- | --- |
| 1–4: portrait, CanvasScaler, anchors | Portrait only; both scenes use Screen Space Overlay, Scale With Screen Size, 1080×1920, match 0.5. Existing anchored HUD/menu/result layouts retained. |
| 7–9: board fitting | Existing orthographic fit intersects the safe area with the gameplay viewport and HUD boundary. Both axes fit uniformly; 6×6 and 8×8 covered by new regression tests. No camera rewrite. |
| 10: button design | Existing shared SettingsPanel prefab and reduced button sizes retained; no separate per-device layouts. Physical touch comfort remains a device check. |
| 11–12: input availability | New Input System pointer bindings support mouse/touch. Controller state remains authoritative during resolution; existing modal input lock preserved. Added only UI raycast filtering. |
| 13–14: lifecycle/audio | Existing pause/focus/quit save retries and scene-owned audio sources retained. No new persistent audio manager or custom animation pause system. Hardware interruption behavior is not certified. |
| 15: haptics | Existing platform guards, Editor exclusion and preference flag retained. Actual vibration intensity/frequency requires a phone. |
| 17–18: quality/rendering | Existing mobile Medium quality, URP 2D, render scale 1 and MSAA 1 retained. No evidence justifying a pipeline/material rewrite or arbitrary quality reduction. |
| 19–21: VFX, text, menu | Existing particles, TMP typography and main menu retained. Safe-area parent added instead of rebuilding the screens. |
| 23: launch | Existing Unity/default launch presentation retained. |
| 24: identity | Product name BlastPuzzle and version 1.0 retained. Company name retained to avoid changing the desktop save path. |
| 25, 27: development/device settings | Existing development profile retained. Device SDK restored after Simulator export. |
| 28–29: platforms | iOS prioritized; Android module and physical phone unavailable. No Android setup expansion. |
| 31–32: save/pooling | Save schema version 1, highest unlocked index, preference storage, atomic write/retry and pooled block views retained. No migration or pool redesign. |
| 33–34: regression scope | Existing tests preserved; added only safe-area, camera-fit and UI-input coverage. |
| 36–37: performance/scope | No M21 profiling report found in the project. No invented baseline, broad optimization, new mechanic or Milestone 23 work. |

## B. Changes required

| Problem | Change | Reason |
| --- | --- | --- |
| UI had no safe-area container | Added SafeAreaFitter and one SafeArea child under each scene Canvas | Normalize Screen.safeArea to anchors; avoid notch/home-indicator areas. Startup application and change-only layout updates. |
| Pointer action could select board beneath UI | BoardInputHandler performs a current-position EventSystem raycast, recognizing GraphicRaycaster hits | Input callbacks can arrive before cached UI pointer state updates; preserves the existing controller state machine. |
| No intentional mobile frame-rate target | MobileRuntimeSettings sets 60 on mobile before the first scene | Explicit budget; not a claim of measured 60 FPS. |
| Missing original app icon | Added BlastPuzzleIcon.png and configured default app icon | Original glossy block matching the game; opaque image. |
| Default mobile bundle/package identifiers | Set iOS and Android to com.aysukeskin.blastpuzzle | Clear developer/game identity. An old mobile install with the previous identifier has a separate save container. |
| Release purpose was not explicit | MobileBuild menu provides device release, device development and Simulator release exports | BuildOptions explicitly determine development flags; temporary Simulator SDK/architecture restored in finally. |
| Main-menu Play override discarded Unity Test Runner scene | OpenEntryScene exempts InitTestScene on Play entry | Allows PlayMode tests while normal Editor Play still begins at Main Menu. |
| New behavior needed regression coverage | Nine layout test cases and one UI-input integration test | Verifies normalized safe area, 6×6/8×8 bounds and actual queued pointer input. |

## C. Verification

- **EditMode: 173 passed, 0 failed.** Includes safe-area changes and 6×6/8×8 fit at 9:16, 9:19.5 and 9:20. These are mathematical camera/layout checks, not device screenshots.
- **PlayMode: 3 passed, 0 failed.** Shared settings modal blocking, actual mouse UI click not consuming a move, rocket/bomb chain activation feedback.
- Unity Simulator export succeeded. The export log includes two CLI main-thread timeout messages while the synchronous build occupied the Editor, plus package/build warnings; do not describe this as a warning-free Console. See local `Logs/Editor.log` and `Logs/M22-build-result.txt`.
- Xcode ARM64 Simulator Release build succeeded (Xcode 26.6, iOS 26.5 SDK). Generated GameAssembly run-script output-dependency warning remains. Simulator install/launch and the full visual matrix were not completed before the subsequent board-animation request. The exported app predates the later diagonal-gravity/board-mask changes and must be rebuilt before validating those on Simulator.

## D. Remaining limitations

No physical iOS/Android test, haptic verification, thermal measurement, hardware FPS guarantee, or Android build. Simulator timing does not represent a phone GPU/CPU. Save persistence tests do not prove mobile OS force-kill behavior. Full manual final-flow and pooling transitions must be distinguished from automated coverage. Unverified items are not acceptance passes.

## Files and settings

Created (with Unity .meta files):

- Assets/Scripts/UI/SafeAreaFitter.cs
- Assets/Scripts/Core/MobileRuntimeSettings.cs
- Assets/Editor/MobileBuild.cs
- Assets/Tests/EditMode/MobileLayoutTests.cs
- Assets/Art/Icons/BlastPuzzleIcon.png
- Docs/MILESTONE22.md

Modified for M22:

- Assets/Scripts/Presentation/BoardInputHandler.cs
- Assets/Editor/OpenEntryScene.cs
- Assets/Tests/Integration/GameplayIntegrationTests.cs
- Assets/Tests/Integration/BlastPuzzle.Tests.Integration.asmdef
- Assets/Scenes/MainMenu.unity and Gameplay.unity: SafeArea wrapper, existing children reparented.
- ProjectSettings/ProjectSettings.asset: mobile identifiers and default icon.
- README.md: build entry points and verification reference.

Existing shared settings prefab and unrelated earlier working-tree changes are not new M22 changes. Build outputs remain local under Builds/; do not commit generated Xcode or DerivedData folders.

Suggested commit: `Add mobile safe areas, UI input blocking and explicit iOS builds`

## Teaching notes

1. **Reference vs device resolution:** 1080×1920 is the design coordinate system. CanvasScaler maps it to the actual screen; it does not force the phone to render at that resolution.
2. **Anchors:** describe where a UI element belongs relative to its parent, so a taller screen does not require another menu.
3. **Screen.safeArea:** a rectangle in screen pixels where important content can avoid system cutouts and gesture regions. The fitter converts it into 0–1 anchors.
4. **Available board region:** safe area minus HUD leaves the actual play region. Fitting against the full screen can hide blocks behind goals or system UI.
5. **Orthographic size:** half the visible world height. Width also depends on aspect ratio; the fit takes the larger size required by either axis.
6. **Simulator limits:** useful for launch, layout and interaction, but runs on the Mac rather than phone hardware and cannot establish mobile thermals or haptics.
7. **Development vs release:** development enables debugging/profiling overhead; a release export explicitly disables those build options. Neither replaces signing for physical distribution.
8. **UI input isolation:** one touch should activate its button without also spending a board move underneath it. EventSystem handles UI hits; gameplay state still handles whether a move is allowed.
9. **Background/resume:** Unity normally suspends player activity with the app. Existing pause/focus callbacks retry pending saves. The OS can kill the process, so RAM alone is not persistence; an active board is intentionally not saved.
10. **Frame-rate policy:** asking for 60 is a target, not proof. Hardware, load and OS scheduling determine achieved frame times.
11. **Polish after profiling:** measure actual bottlenecks before changing quality. Here, no M21 artifact was available, so working renderer/VFX settings were preserved rather than optimized speculatively.
12. **Intentionally preserved:** orientation, scaler, anchors, camera algorithm, pointer bindings, save schema/retries, pool design, audio ownership and rendering pipeline.
13. **Interview explanation:** demonstrate the path from screen touch → UI rejection → board coordinate → controller state → move resolution; explain safe-area anchors, both-axis camera fit, local save guarantees and the difference between a passing test and physical-device evidence.

## Subsequent board presentation request (2026-09-26)

Added a framed board with SpriteMask clipping: incoming blocks reveal only the portion inside the board. Verified visually with a block straddling the top edge. Existing blocks now descend diagonally into reachable covered gaps after vertical settling; open columns refill and continue the flow. Sealed pockets retain the fallback instead of crossing crate walls. This changes obstacle refill behavior and is explicitly requested after M22.

Validation: 178 EditMode tests and 4 PlayMode integration tests passed, including covered-gap identity, impassable walls, one movement per block per wave, top-only refill, view/board synchronization and mask interaction. The first PlayMode attempt failed inside Unity Test Runner before executing the suite; resetting its temporary scene and rerunning succeeded. Simulator export must be rebuilt for these subsequent changes.
