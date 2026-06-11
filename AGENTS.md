# PushBoxz Agent Orchestration

本文档定义 PushBoxz 项目的多 Agent 协作方式。项目目标是完成一个产品级 Unity 推箱子笔试作品，包含完整关卡体验流程和关卡编辑器。

当前项目路径：`P:\UnityG\PushBoxz`  
Unity 版本：`2022.3.51f1c1`  
协作入口：用户只需要直接与 Agent 1 对话。

## 核心结论

- 使用 6 个 Agents。
- Agent 1 是唯一总调度、架构负责人和最终集成负责人。
- Agent 2-6 是专项 Agent，按任务被 Agent 1 派发、复核、整合。
- 首轮编辑器形态采用 Unity `EditorWindow`，不做玩家侧运行时编辑器。
- 首轮关卡数据采用静态地形层和对象层分离，避免把运行时状态写入关卡原始数据。
- 首批内容目标为 10 个关卡，至少保证 8 个可交付关卡。
- 体验侧必须包含主菜单、关卡选择、游戏 HUD、撤销、重开、胜利结算、编辑器闭环。

## Agent 1: Producer / Tech Lead

负责人：Codex 主会话。

职责：

- 接收用户所有需求。
- 判断需求归属，拆分给对应专项 Agent。
- 冻结架构、目录、数据结构、接口和里程碑。
- 维护实现优先级，控制笔试题范围。
- 集成各 Agent 产出，避免模块冲突。
- 最终决定是否进入下一阶段。
- 保证交付物达到产品级笔试展示标准。

Agent 1 不把关键架构决策完全外包。遇到数据格式、模块边界、运行时流程、编辑器流程、验收标准等问题，由 Agent 1 汇总意见后拍板。

## Agent 2: Core Gameplay

职责：

- 实现推箱子核心规则。
- 维护纯 C# 逻辑层，尽量不绑死 Unity GameObject。
- 支持关卡加载、玩家移动、箱子推动、墙体阻挡、目标点判定。
- 支持步数统计、撤销、重开、胜利检测。
- 向 UI 和表现层暴露事件。

核心接口草案：

```csharp
public enum Direction
{
    Up,
    Down,
    Left,
    Right
}

public interface IGameplayController
{
    void LoadLevel(LevelData level);
    bool Move(Direction direction);
    bool Undo();
    void Restart();
    bool IsCompleted { get; }
    int StepCount { get; }
}
```

首轮目标：

- 完成核心数据模型。
- 完成四向移动和推箱规则。
- 完成胜利检测。
- 完成 Undo / Restart。
- 完成运行时实例化接口。
- 至少用 3 个测试关卡验证基础规则。

## Agent 3: Level Editor

职责：

- 实现 Unity Editor 关卡编辑工具。
- 定义并维护关卡数据结构、版本字段、保存和加载机制。
- 实现关卡合法性校验。
- 支持一键试玩当前关卡。

首轮编辑器形态：

- 使用 Unity `EditorWindow`。
- 支持网格编辑。
- 支持画笔：地板、墙、目标点、箱子、玩家、擦除。
- 支持关卡元信息：ID、名称、章节、顺序、难度、推荐步数、教学文本。
- 支持保存、加载、复制、校验、试玩。

不做事项：

- 首轮不做玩家侧运行时编辑器。
- 不把编辑器 UI 与 Gameplay 规则强耦合。

关卡校验分级：

- Error：阻止发布或正式保存。
- Warning：允许保存，但提示设计风险。

关键 Error：

- 必须且只能有一个玩家出生点。
- 至少一个箱子和一个目标点。
- 箱子数量必须等于目标点数量。
- 玩家、箱子、目标点不能在墙或空白格上。
- 所有坐标必须在地图范围内。
- `levelId` 不能为空且必须唯一。

## Agent 4: Level Design

职责：

- 设计首批关卡和难度曲线。
- 保证教学、练习、进阶、综合挑战完整。
- 输出关卡数据需求、教学文本、推荐步数和验收标准。
- 和 QA 一起保证关卡可解、节奏平滑。

首批 10 关曲线：

| 关卡 | 定位 | 机制 | 尺寸建议 | 箱子/目标 |
|---|---|---|---|---|
| 1 | 基础教学 | 移动、推动、目标点 | 5x5 | 1/1 |
| 2 | 单箱直线 | 箱子只能推不能拉 | 6x5 | 1/1 |
| 3 | 墙体绕行 | 站位和绕路 | 7x6 | 1/1 |
| 4 | 死角认知 | 避免推入墙角 | 7x7 | 1/1 |
| 5 | 双目标入门 | 双箱双目标 | 8x7 | 2/2 |
| 6 | 顺序意识 | 错误顺序会堵路 | 8x8 | 2/2 |
| 7 | 狭窄通道 | 通道和站位限制 | 9x8 | 2/2 |
| 8 | 临时占位 | 目标点可临时经过 | 9x9 | 3/3 |
| 9 | 多箱干扰 | 三箱相互影响 | 10x9 | 3/3 |
| 10 | 综合挑战 | 死角、顺序、通道综合 | 10x10 | 3/3 或 4/4 |

验收要求：

- 每关可人工完整通关。
- 每关至少通关验证 2 次。
- 前 5 关难度克制，不突然跳到复杂多箱谜题。
- 教学文案短，主要靠布局教学。

## Agent 5: UX / UI / Presentation

职责：

- 负责完整产品体验流程。
- 设计 UI 页面、HUD、弹窗、反馈和视觉风格。
- 对齐 Gameplay 和 Editor 事件接口。
- 保证演示时像完整小游戏，而不是临时技术 Demo。

必要页面和界面：

- 主菜单。
- 关卡选择。
- 游戏 HUD。
- 暂停菜单。
- 胜利结算。
- 设置页，至少包含音量控制。
- 关卡编辑器主界面。
- 保存、加载、覆盖确认和错误提示。

游戏 HUD 必须包含：

- 当前关卡名或编号。
- 步数。
- 撤销。
- 重开。
- 暂停。
- 返回选关。

视觉最低标准：

- 地面、墙、箱子、目标点、玩家一眼可区分。
- 箱子到目标点后有明确状态变化。
- 按钮有正常、悬停、按下、禁用状态。
- 文本不溢出、不重叠。
- 主菜单、关卡选择、胜利页使用统一视觉语言。

## Agent 6: QA / Build / Documentation

职责：

- 制定测试矩阵。
- 检查主流程、关卡流程、编辑器流程。
- 验证 Unity Editor 和 Windows Build。
- 维护 README、技术说明、测试报告和已知问题。
- 给出阶段 QA 结论。

三个 QA Gate：

- Gate 1：基础玩法可玩，至少一关可通关。
- Gate 2：编辑器可创建、保存、加载并试玩关卡。
- Gate 3：构建版完整流程可交付。

最终交付清单：

- Unity 工程完整目录。
- Windows 构建包。
- 主菜单、关卡选择、游戏关卡、胜利流程。
- 至少一组内置关卡。
- 完整关卡编辑器。
- 自定义关卡保存和加载能力。
- 基础存档系统。
- `README.md`。
- `Docs/Design.md`。
- `Docs/Technical.md`。
- `Docs/TestReport.md`。
- `Docs/KnownIssues.md`。

## 冻结数据方向

关卡数据使用地形层和对象层分离。

建议字段：

```csharp
public class LevelData
{
    public string Version;
    public string LevelId;
    public string DisplayName;
    public int Chapter;
    public int Order;
    public int Difficulty;
    public int Width;
    public int Height;
    public Vector2Int PlayerStart;
    public List<TileCell> Cells;
    public List<Vector2Int> BoxStarts;
    public int ParMoves;
    public string TutorialText;
    public string Author;
    public string Notes;
}

public class TileCell
{
    public int X;
    public int Y;
    public BaseTileType BaseType;
    public bool HasGoal;
}

public enum BaseTileType
{
    Empty = 0,
    Floor = 1,
    Wall = 2
}
```

规则：

- `BaseTileType` 表达静态地形。
- `HasGoal` 表达目标点覆盖层。
- `PlayerStart` 和 `BoxStarts` 表达初始对象层。
- “箱子在目标点上”属于运行时状态，不写入原始地形。
- `levelId` 必须稳定，不随显示名称变化。

首轮主格式建议使用 `ScriptableObject`，必要时额外导出 JSON 方便展示和调试。

## 推荐目录结构

```text
Assets/
  PushBoxz/
    Art/
    Audio/
    Editor/
    Levels/
    Prefabs/
    Scenes/
    Scripts/
      Core/
      Data/
      EditorBridge/
      Gameplay/
      Presentation/
      UI/
      Utility/
    Settings/
    Tests/
Docs/
Build/
```

开发硬规则：

- 不手动修改 `Library/`、`Temp/`、`Logs/`。
- 只在必要时修改 `Packages/` 和 `ProjectSettings/`。
- 运行时代码不得依赖 Editor-only API。
- Editor-only 代码必须放在 `Editor/` 目录或 Editor asmdef 中。
- 数据结构先于 Gameplay、Editor、UI 冻结。
- Agent 之间不得并行修改同一批文件，除非 Agent 1 明确分配写入范围。

## 调度流程

用户提出需求后，Agent 1 按以下方式处理：

1. 判断需求类型。
2. 如需求简单且不需要并行，Agent 1 直接实现。
3. 如需求涉及专项设计或可并行开发，Agent 1 派发给对应 Agent。
4. Agent 1 在本地推进关键路径，不等待非阻塞分析。
5. 专项 Agent 返回后，Agent 1 复核、整合、必要时修正。
6. Agent 1 运行可用的验证命令或 Unity 检查。
7. Agent 1 向用户报告完成内容、文件路径、测试结果和剩余风险。

任务归属：

| 需求类型 | 主责 Agent |
|---|---|
| 架构、排期、接口冻结、冲突处理 | Agent 1 |
| 移动、推箱、Undo、胜利判定 | Agent 2 |
| 关卡编辑器、保存加载、校验 | Agent 3 |
| 关卡设计、教学节奏、难度曲线 | Agent 4 |
| 主菜单、HUD、结算、视觉反馈 | Agent 5 |
| 测试、构建、文档、交付检查 | Agent 6 |

## 里程碑

M0：项目骨架

- 建立目录结构。
- 冻结命名空间和数据模型。
- 创建主场景和基础资源路径。

M1：核心玩法

- 可加载一关。
- 可移动和推箱。
- 可通关。
- 可重开和撤销。

M2：关卡编辑器

- 可新建、绘制、校验、保存、加载关卡。
- 可一键试玩。

M3：产品流程

- 主菜单。
- 关卡选择。
- 游戏 HUD。
- 胜利结算。
- 设置和返回路径。

M4：内容与打磨

- 8-10 个内置关卡。
- 基础视觉、动画、音效反馈。
- 存档和最佳步数。

M5：交付

- Windows Build。
- README 和 Docs。
- 测试报告。
- 已知问题归档。

## 当前执行原则

优先级从高到低：

1. 冻结关卡数据格式。
2. 实现可测试的核心玩法逻辑。
3. 实现编辑器写入同一数据格式。
4. 接入完整 UI 流程。
5. 生产首批关卡并验证可解。
6. 构建、文档、最终 QA。

任何新需求默认先由 Agent 1 判断是否影响已冻结接口。如果影响，必须先更新本文档或对应设计文档，再进入实现。
