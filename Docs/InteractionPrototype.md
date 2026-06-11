# PushBoxz Interaction Prototype 0.1

本文档定义第一版可实现、可测试的交互原型。范围只覆盖 3D 俯视推箱、地图生成和运行时重置，不包含目标点、胜负条件、Undo、关卡选择和完整 UI 流程。

## 目标

- 玩家以静止俯视角查看 3D 场景。
- 玩家使用 WASD 移动，并由最后一次 WASD 输入决定当前朝向。
- 玩家使用 F 尝试推动当前朝向上的箱子。
- 箱子能否推动由目标箱子、箱子属性、箱子后方占用情况决定。
- 成功推动后，箱子在短时间内快速移动到新格子。
- 箱子移动不可被打断。
- 玩家推箱需要冷却，冷却时间略大于箱子移动时间。
- 开发者可以用编辑器工具生成测试地图。
- Runtime 提供按钮，把地图重置到初始状态。

## 第一版非目标

- 不做胜负条件。
- 不做目标点判定。
- 不做 Undo。
- 不做玩家侧运行时关卡编辑器。
- 不做复杂美术、音效和完整产品 UI。
- 不做多个箱子的连锁推动。

## 交互模式

### 摄像机

- 3D 项目。
- 摄像机固定在俯视角。
- 第一版不需要相机旋转、拖拽或缩放。

### 玩家输入

- `W`：玩家向上移动，并朝向 Up。
- `S`：玩家向下移动，并朝向 Down。
- `A`：玩家向左移动，并朝向 Left。
- `D`：玩家向右移动，并朝向 Right。
- `F`：沿当前朝向尝试推动箱子。

方向映射：

```text
Up    -> Grid +Y -> World +Z
Down  -> Grid -Y -> World -Z
Left  -> Grid -X -> World -X
Right -> Grid +X -> World +X
```

玩家朝向使用逻辑方向 `Direction` 记录，视觉朝向由表现层同步。核心判定不直接依赖 `transform.forward`。

## 网格与坐标

关卡以二维网格表达，运行时映射到 3D 世界。

```text
Grid X -> World X
Grid Y -> World Z
World Y -> 高度
```

坐标转换：

```csharp
Vector3 worldPos = new Vector3(gridX * cellSize, 0f, gridY * cellSize);
```

第一版默认 `cellSize = 1f`。

## 地图元素

第一版支持三类格子配置：

- Empty：空地，可通行，可被箱子推入。
- Wall：不可通行，不可推动。
- Box：普通箱子初始位置，可被推动。

另有一个玩家初始位置。

注意：

- Wall 是静态阻挡。
- Box 是运行时对象。
- 玩家初始位置和箱子初始位置必须在可通行地面上。
- 第一版可以把所有非墙格子默认视为地面。

## 推箱判定

玩家按 F 后执行一次 `TryPush()`。

判定流程：

1. 玩家是否可推箱。
   - 如果箱子正在移动或玩家处于推箱冷却，失败。
2. 根据玩家当前朝向计算前方格。
   - `front = player.Position + DirectionOffset(player.Facing)`
3. 前方格是否有普通箱子。
   - 没有箱子，失败。
   - 是墙或不可移动物，失败。
4. 箱子距离是否足够近。
   - 第一版采用格子制：箱子在玩家正前方一格即为足够近。
5. 计算箱子目标格。
   - `target = box.Position + DirectionOffset(player.Facing)`
6. 目标格是否合法。
   - 越界，失败。
   - 有墙，失败。
   - 有其他箱子，失败。
   - 为空地，成功。
7. 成功推动。
   - 逻辑层立即把箱子占用从旧格移动到目标格。
   - 箱子表现层从旧世界坐标快速移动到新世界坐标。
   - 玩家进入推箱过程和冷却流程。

核心原则：

> 规则层立即结算格子占用，表现层负责短动画追上规则结果。

这样可以避免动画期间再次输入导致两个箱子占用同一格，或后续判定读取到旧位置。

## 推箱状态机

推荐状态：

```csharp
public enum PlayerActionState
{
    Idle,
    BoxMoving,
    PushCooldown
}
```

状态说明：

- `Idle`：允许 WASD，允许 F。
- `BoxMoving`：箱子正在移动，不允许再次推箱；第一版也不允许 WASD 改朝向。
- `PushCooldown`：箱子已到位，仍不允许 F；可以允许 WASD 改朝向和移动。

第一版为了实现清晰，可以选择整个推箱冷却期间都锁住 F，只在 `PushCooldown` 期间允许 WASD。

## 时序参数

推荐初始参数：

```csharp
float boxMoveDuration = 0.12f;
float pushCooldown = 0.18f;
```

约束：

```text
pushCooldown > boxMoveDuration
```

时序：

```text
t = 0.00
  玩家按 F
  判定成功
  箱子逻辑位置立即更新
  箱子开始移动动画
  玩家进入 BoxMoving

t = 0.00 ~ 0.12
  箱子移动中
  不接受新的推箱

t = 0.12
  箱子到达目标格
  玩家进入 PushCooldown

t = 0.12 ~ 0.18
  箱子稳定
  玩家仍不能推箱

t = 0.18
  玩家回到 Idle
```

## Runtime 组件划分

推荐第一版组件：

```text
Data
  LevelDataAsset
  TileCell
  BaseTileType

Core
  GridPosition
  Direction
  DirectionUtility
  PushResult
  PushFailReason

Gameplay
  PushGameplayController
  GridOccupancyService
  PlayerInputController
  PlayerRuntimeState
  BoxRuntimeState

Presentation
  GridWorldMapper
  LevelSceneBuilder
  PlayerView
  BoxView
  GameSession
  RuntimeHud
```

职责：

- `PushGameplayController`：维护玩家、箱子、墙体占用和推箱判定。
- `GridOccupancyService`：提供格子是否越界、是否为空、是否有箱子的查询。
- `PlayerInputController`：读取 WASD / F，转发给玩法控制器。
- `LevelSceneBuilder`：根据关卡数据生成 3D 地图对象。
- `BoxView`：播放箱子从旧格到新格的短移动动画。
- `GameSession`：加载当前关卡，协调重置。
- `RuntimeHud`：提供 Runtime 重置按钮。

## 关卡数据

第一版使用 `ScriptableObject` 表达关卡初始状态。

```csharp
[CreateAssetMenu(menuName = "PushBoxz/Level Data")]
public class LevelDataAsset : ScriptableObject
{
    public string version = "0.1";
    public string levelId;
    public string displayName;
    public int width;
    public int height;
    public Vector2Int playerStart;
    public List<TileCell> cells = new();
    public List<Vector2Int> boxStarts = new();
}

[Serializable]
public class TileCell
{
    public int x;
    public int y;
    public BaseTileType baseType;
}

public enum BaseTileType
{
    Empty = 0,
    Floor = 1,
    Wall = 2
}
```

第一版不保存当前玩家位置和当前箱子位置。Runtime 状态由 `PushGameplayController` 维护，重置时回到 `LevelDataAsset`。

## 编辑器地图生成工具

入口：

```text
Tools / PushBoxz / Level Editor
```

最小工作流：

1. 新建关卡。
2. 输入宽度和高度，例如 `5 x 6`。
3. 在 EditorWindow 中显示二维网格。
4. 选择画笔。
5. 点击格子设置类型或对象。
6. 设置玩家初始位置。
7. 保存为 `LevelDataAsset`。
8. 一键生成到当前场景。
9. 进入 Play Mode 测试。

第一版画笔：

- Floor / Empty：可通行格。
- Wall：不可通行格。
- Box：普通箱子初始点。
- Player：玩家初始点。
- Erase：擦除对象层或恢复为空地。

保存路径：

```text
Assets/PushBoxz/Levels/
```

预制体路径：

```text
Assets/PushBoxz/Prefabs/
  Floor.prefab
  Wall.prefab
  Box.prefab
  Player.prefab
```

生成层级：

```text
LevelRoot
  Tiles
  Boxes
  Player
```

## 编辑器校验

保存或生成前至少校验：

- 宽度和高度必须大于 0。
- 必须有且只有一个玩家初始位置。
- 玩家初始位置必须在地图范围内。
- 玩家初始位置不能在墙上。
- 箱子数量可以为 0，但为了测试推箱功能，建议至少 1 个。
- 箱子不能在墙上。
- 箱子不能重叠。
- 所有箱子必须在地图范围内。
- `levelId` 不能为空。

第一版不需要校验可解性，因为暂不做胜负条件。

## Runtime 重置

Runtime HUD 提供 `Restart` 按钮。

点击后调用：

```csharp
GameSession.RestartLevel()
```

推荐 MVP 流程：

```csharp
public void RestartLevel()
{
    levelSceneBuilder.Clear();
    gameplayController.LoadLevel(currentLevel);
    levelSceneBuilder.Build(currentLevel);
}
```

这会直接从 `LevelDataAsset` 重新生成初始状态。

后续如果需要更丝滑，可以改为不销毁场景物体，只把玩家和箱子移动回初始格，但第一版优先保证稳定。

## 边界情况

- F 时前方无箱子：失败，不触发冷却。
- F 时前方是墙：失败，不触发冷却。
- 箱子后方越界：失败。
- 箱子后方有墙：失败。
- 箱子后方有箱子：失败。
- 箱子移动中再次按 F：失败，不追加冷却。
- 玩家冷却未结束按 F：失败，不追加冷却。
- 冷却期间 WASD：第一版允许在 `PushCooldown` 阶段移动和改朝向，不允许在 `BoxMoving` 阶段移动。
- 关卡数据中多个箱子重叠：编辑器校验阻止保存或生成。
- 关卡没有箱子：允许用于移动测试，但提示 Warning。

## 第一版验收

功能验收：

- 可以用编辑器创建 `5 x 6` 测试地图。
- 可以设置墙、箱子、空地和玩家初始位置。
- 可以保存关卡资产。
- 可以生成 3D 场景。
- Runtime 可加载该场景。
- 玩家能用 WASD 在空地移动，并更新朝向。
- 玩家按 F 能推动正前方相邻的普通箱子。
- 箱子后方为空时，箱子快速移动到下一格。
- 箱子后方不是空地时，推动失败。
- 推动动画期间无法再次推动。
- 推动冷却期间无法再次推动。
- Runtime `Restart` 按钮可恢复初始地图。

工程验收：

- 运行时代码不依赖 Editor API。
- 编辑器代码放在 `Editor` 目录。
- 关卡初始数据和 Runtime 状态分离。
- 规则层不依赖 `transform.forward` 做核心判定。
- 逻辑占用在推动成功时立即更新。

## Update 0.2 - Goals And Completion

- Level data now stores goals on `TileCell.hasGoal`.
- The editor includes a `Goal` brush. Goals are a terrain overlay and may share a cell with a box or player start.
- Validation requires at least one box, at least one goal, and equal box/goal counts.
- Runtime scene generation draws goals as green markers on floor tiles.
- Completion is true when every box occupies a goal and the box count equals the goal count.
- Runtime HUD shows `Completed` after success, and movement/pushing is locked until restart.
