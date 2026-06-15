# PushBoxz

> 完整使用说明请查看：[Docs/UserGuide.md](Docs/UserGuide.md)

## 项目设计架构

PushBoxz 按照一个小型完整 Unity 产品样例来组织：核心玩法、关卡数据、场景表现、编辑器工具、运行时 UI 流程彼此分层，尽量让每一部分都能独立理解、验证和扩展。

### 1. 数据驱动的关卡格式

关卡以 `LevelDataAsset` ScriptableObject 的形式存放在 `Assets/PushBoxz/Levels` 下。每个关卡资产只保存初始数据：

- 地图尺寸和稳定的 `levelId`。
- 静态地形格子：空白、地板、墙。
- 目的地覆盖层。
- 玩家初始位置和箱子初始位置。
- 可选的自动生成解法步骤，用于回放和参考。

运行时状态，例如“箱子当前是否在目的地上”、撤销栈、玩家当前位置、是否通关等，不会写回原始关卡资产。这样可以保证关卡资产干净稳定，也让重开、撤销、编辑器预览和运行时加载都更可控。

### 2. 纯玩法规则与运行时编排分离

玩法层负责推箱子的基础规则：移动、墙体阻挡、箱子推动、目标点判定、重开和撤销。`PushGameplayController` 负责这些规则本身，不依赖具体场景层级；`GameSession` 则作为运行时编排层，把规则与 Unity 对象、输入、动画、音效、UI 和通关流程连接起来。

这种拆分能体现技术策划项目中比较重要的思路：规则可以作为数据逻辑单独推导和验证，表现层则可以从简单 cube 切换到正式预制体，而不需要重写谜题规则。

### 3. 表现层与场景生成

`LevelSceneBuilder` 负责把一个 `LevelDataAsset` 构建成可玩的场景对象，包括地形、箱子、目的地和玩家，并读取注册表中配置的预制体、音频和字体资源。`GridWorldMapper` 统一网格坐标到世界坐标的换算，保证场景搭建、移动表现和碰撞判断使用同一套坐标规则。

运行时菜单由 `LevelMenuController` 管理，覆盖：

- 封面 UI。
- 官方关卡选择。
- 创造模式关卡选择。
- 游戏内 HUD。
- 通关弹窗。
- 游戏内自定义关卡编辑器。

### 4. 编辑器工具链

自定义关卡编辑器是本项目的主要生产工具。`PushBoxzLevelEditorWindow` 支持手动绘制、合法性校验、保存/加载、场景资产配置、正推/倒拉设计模式，以及自动倒推生成关卡。自动生成器从“箱子已经在目的地上”的完成状态出发，通过合法倒拉生成初始局面，同时记录解法路径，并用短窗口回退和行动评分减少过早卡死与低价值循环。

`LevelSceneBuilderRegistry` 是运行时官方关卡列表和共享场景资源的注册表。`PushBoxzLevelSceneBuilderRegistryWindow` 提供可拖拽排序、启用和解锁配置；`PushBoxzLevelRegistryBuilder` 可以从有效关卡资产中重建注册表，并尽量保留手动调整过的顺序和状态。

### 5. 官方关卡与玩家自定义关卡分离

官方关卡是 Unity 工程资产，由编辑器和关卡列表工具管理。玩家在游戏内创建的自定义关卡则通过 `CustomLevelStorage` 以 JSON 形式保存到 `Application.persistentDataPath`，不依赖 `AssetDatabase`，因此可以在正式运行环境中创建、游玩和删除。

### 6. 交付思路

本项目不只展示单个推箱子机制，而是包含完整游玩流程、编辑器流程、自动生成实验、关卡列表管理、游戏内创造模式、文档和脚本注释。整体架构优先保证职责清晰、数据边界稳定、工具能被设计者实际使用，而不是停留在一次性 Demo 代码。

## Overview

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
