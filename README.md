# PushBoxz

PushBoxz is a Unity 3D Sokoban project built as a technical-design test work. The project includes a playable level flow, a runtime level menu, a level registry, and a custom Unity Editor level editor with reverse-design generation tools.

Unity version: `2022.3.51f1c1`

## Current Features

- Grid-based Sokoban gameplay with walls, floors, boxes, goals, player movement, push rules, restart, undo, and completion detection.
- Level data stored as `ScriptableObject` assets.
- Unity Editor level editor for creating, editing, validating, saving, and generating levels.
- Reverse-design mode for pulling boxes backward while designing puzzles.
- Automatic reverse level generation with adjustable box count, seed, wall density, and behavior weights.
- Level registry used by runtime menus to list enabled and unlocked levels.
- Canvas UI flow for cover screen, level select, gameplay HUD, and level-complete popup.
- Shared scene asset settings for optional prefabs, gameplay audio, UI button audio, and TMP font selection.

## Main Editor Entrances

- `Tools/PushBoxz/Level Editor`
- `Tools/PushBoxz/Level Registry`
- `Tools/PushBoxz/Rebuild Level Registry`

Extra asset shortcuts:

- Select a `LevelDataAsset` and click `Open Level Editor` in the Inspector.
- Right-click a `LevelDataAsset` and choose `PushBoxz/Open With Level Editor`.

## Version History

### v0.1.0 - Initial Repository

Commit: `3ee2428`  
Date: 2026-06-11

- Created the GitHub repository baseline.
- Added the initial README.

### v0.2.0 - Initial Unity Project

Commit: `1e611d9`  
Date: 2026-06-11

- Added the Unity project structure, project settings, packages, and scenes.
- Added the first PushBoxz gameplay architecture.
- Added core data and rule scripts, including directions, tile types, level data, runtime player and box state, and push results.
- Added the first `PushGameplayController`, `GameSession`, `PlayerInputController`, `LevelSceneBuilder`, `BoxView`, `GridWorldMapper`, and runtime HUD.
- Added the first custom `PushBoxzLevelEditorWindow`.
- Added early sample levels and prototype documentation.

### v0.3.0 - Level Menu And Reverse Generator

Commit: `69ccfef`  
Date: 2026-06-12

- Added the runtime level menu flow.
- Added a level catalog resource and builder tooling.
- Added reverse-design and automatic reverse-generation support in the level editor.
- Added generated level assets and renamed several working level assets.
- Improved level data to store generated solution steps.
- Improved gameplay flow around undo, restart, continuous movement, and level completion.

### v0.4.0 - Level Editor And Menu Flow Update

Commit: `fa610a2`  
Date: 2026-06-15

- Replaced the older level catalog flow with `LevelSceneBuilderRegistry`.
- Added the Level Registry editor window.
- Updated runtime menus to read enabled and unlocked level entries from the registry.
- Changed runtime level switching to reuse one `LevelSceneBuilder` while changing the selected `LevelDataAsset`.
- Added Canvas UI support for cover, level select, gameplay HUD, and level-complete popup.
- Added next-level behavior after completion.
- Added optional prefabs, gameplay audio settings, and registry-backed scene builder assets.
- Added TextMeshPro resources and additional audio resources.
- Improved level editor layout, auto-generation controls, behavior weights, validation messages, and bottom actions.

### v0.5.0 - Editor Access And UI Assets

Commit: `a7bd00f`  
Date: 2026-06-15

- Added direct `LevelDataAsset` editor access from the Inspector and Project right-click menu.
- Cleaned the `Tools/PushBoxz` menu to keep only Level Editor, Level Registry, and Rebuild Level Registry.
- Renamed the registry menu entry to `Level Registry`.
- Added shared TMP font selection to the registry-backed scene asset settings.
- Added UI button audio settings and runtime click sound playback.
- Converted runtime UI text written by code to English to avoid missing Chinese glyphs.
- Added additional font assets and TMP font settings.
- Updated gameplay scene and UI prefab assets.
- Added a new level asset.

## Build And Verification

The project has been checked with:

```bash
dotnet build PushBoxz.sln --no-restore
```

Latest local result before this README update: `0 warnings, 0 errors`.
