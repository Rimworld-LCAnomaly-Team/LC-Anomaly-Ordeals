# LC Anomaly Story 考验 Submod 接入指南

本文面向为 RimWorld 1.6 开发“考验（Ordeal）”内容包的开发者或编码助手。它描述 `LC Anomaly Story` 提供的考验扩展 API、生命周期、存档约束和推荐实现方式。

## 1. 依赖与职责边界

推荐依赖方向：

```text
LC Anomaly Core
        ↓
LC Anomaly Story
        ↓
你的考验 Submod
```

`LC Anomaly Story` 负责：

- 公司阶段、发展节点和“是否通过考验”的权威状态；
- 同一时间只允许一个活动考验；
- 开始、Tick、通过、失败、取消、冷却和加载恢复；
- 基础统计目标、考验 UI、信件和存档；
- Worker 与 Effect 的异常隔离。

考验 Submod 负责：

- 敌人、波次、Incident、AI、地图目标、音乐、贴图和奖励内容；
- 复杂考验的开始条件、生成、胜负判断和清理；
- 需要高频维护或复杂对象引用时，自有的 `GameComponent` / `MapComponent`；
- 将最终结果提交给 Story，而不是维护第二套公司阶段状态。

只支持 RimWorld 1.6。不要修改或引用本仓库的 `1.5/`、`Source/1.5/`。

## 2. API 版本和程序集引用

当前考验框架 API 版本为：

```csharp
CompanyExaminationDef.FrameworkApiVersion == 1
GameComponent_CompanyDevelopment.ExaminationFrameworkApiVersion == 1
```

Submod 的 C# 项目至少应引用：

- RimWorld 1.6 `Assembly-CSharp.dll`；
- `LCAnomalyStory.dll`；
- 如果直接使用异想体或 PE-Box 类型，再引用 `LCAnomalyCore.dll`；
- 需要 Harmony Patch 时引用 RimWorld 1.6 当前使用的 `0Harmony.dll`。

这些游戏与前置程序集应设置 `Private=False`，不要复制进 Submod 的 Assemblies。

## 3. 数据模型

### 3.1 `CompanyExaminationDef`

主要 XML 字段：

| 字段 | 含义 |
| --- | --- |
| `durationTicks` | 持续时间。`-1` 表示没有框架截止时间，由 Worker 决定结果；其他值至少为 60 |
| `retryCooldownTicks` | 失败后的重试冷却，必须大于等于 0 |
| `workerTickInterval` | Worker Tick 间隔，最小为 1；普通考验建议 30–60 |
| `workerClass` | 自定义 Worker 类型；省略时使用默认 Worker |
| `objectives` | 数据驱动目标；默认 Worker 在全部完成时通过 |
| `effects` | 可组合效果列表，由默认 Worker 分发生命周期和数值修改 |

默认 Worker 的规则：

1. `objectives` 非空且全部满足时通过；
2. 到达截止 Tick 且仍未满足时失败；
3. 默认 Worker 至少需要一个 objective；
4. 自定义 Worker 可以不声明 objectives，并自行调用 `context.Pass()` / `context.Fail()`。

### 3.2 `ExaminationRuntime`

框架保存以下公共状态：

- `status`、`startTick`、`deadlineTick`、`retryAvailableTick`；
- `nextWorkerTick`、`attempts`、`lastOutcomeReason`；
- `statisticBaselines`；
- `longState`、`floatState`、`stringState`。

三种 State 字典是“当前尝试”的持久化状态：存档时保存，在下一次 `StartExamination` 前清空。键必须带 Submod 命名空间，例如：

```text
MyOrdealMod.dawn.wave
MyOrdealMod.dawn.remainingTargets
MyOrdealMod.dawn.mapId
```

Worker 实例挂在 Def 上并被复用，因此必须保持无状态。不要把当前地图、Pawn、波次或计时器保存在 Worker 字段中；使用 `ExaminationContext` 的状态方法，或使用 Submod 自有组件。

## 4. 生命周期契约

正常顺序：

```text
CanStart
  ↓
框架创建本次尝试、记录统计基准、清空 attempt state
  ↓
OnStarted
  ↓
Tick → Evaluate → Tick → Evaluate ...
  ↓
OnPassed 或 OnFailed
```

其他路径：

- 载入活动考验：调用 `OnLoaded`，随后恢复 Tick；
- 外部取消或开发者重置：调用 `OnCancelled`，不算失败、不进入冷却；
- `OnStarted` 抛异常：框架尝试调用 `OnCancelled`，并撤销活动状态；
- `Tick` 或 `Evaluate` 抛异常：框架记录错误并安全失败，进入正常冷却；
- `OnPassed`、`OnFailed`、`OnCancelled` 抛异常：框架记录错误，但不会撤销已经确定的终态；
- UI 进度生成抛异常：显示通用错误行，不中断 UI 绘制。

自定义 Worker 覆盖生命周期方法时，如果仍希望 XML `effects` 生效，必须调用对应的 `base` 方法。建议调用次序：

- `OnStarted`、`OnLoaded`、`Tick`：先 `base`，再执行 Submod 逻辑；
- `OnPassed`、`OnFailed`、`OnCancelled`：先清理 Submod 内容，再 `base`；
- `CanStart`：先调用 `base.CanStart` 并尊重拒绝结果；
- `ModifyStudySuccessRate`：通常先 `base`，再追加自定义修正。

## 5. 最小 XML 示例

下面的 Def 使用自定义 Worker 控制无限时考验。`workerClass` 写完整命名空间类型名，不需要程序集后缀。

```xml
<?xml version="1.0" encoding="utf-8"?>
<Defs>
  <LCAnomalyStory.Defs.CompanyExaminationDef>
    <defName>MyOrdeal_Dawn</defName>
    <label>dawn ordeal</label>
    <description>Survive and suppress every spawned ordeal target.</description>
    <durationTicks>-1</durationTicks>
    <retryCooldownTicks>30000</retryCooldownTicks>
    <workerTickInterval>30</workerTickInterval>
    <workerClass>MyOrdealSubmod.DawnOrdealWorker</workerClass>
  </LCAnomalyStory.Defs.CompanyExaminationDef>
</Defs>
```

若考验只需要统计目标和简单效果，不必编写 Worker：

```xml
<LCAnomalyStory.Defs.CompanyExaminationDef>
  <defName>MyOrdeal_ProductionLockdown</defName>
  <label>production lockdown</label>
  <description>Work under reduced success rate.</description>
  <durationTicks>60000</durationTicks>
  <retryCooldownTicks>30000</retryCooldownTicks>
  <effects>
    <li Class="LCAnomalyStory.Examinations.ExaminationEffect_WorkSuccessPenalty">
      <amount>0.15</amount>
    </li>
  </effects>
  <objectives>
    <li Class="LCAnomalyStory.Defs.ExaminationObjective_Statistic">
      <labelKey>MyOrdeal_ValidWorks</labelKey>
      <statistic>validWorks</statistic>
      <target>6</target>
    </li>
  </objectives>
</LCAnomalyStory.Defs.CompanyExaminationDef>
```

## 6. 最小 Worker 示例

此示例只演示 API。实际敌人生成和死亡通知应由 Submod 的 Incident、Pawn、Comp 或 Harmony Patch 完成。

```csharp
using System.Collections.Generic;
using LCAnomalyStory.Examinations;

namespace MyOrdealSubmod
{
    public sealed class DawnOrdealWorker : CompanyExaminationWorker
    {
        public const string RemainingTargetsKey = "MyOrdealMod.dawn.remainingTargets";

        public override bool CanStart(ExaminationContext context, out string rejectionReason)
        {
            if (!base.CanStart(context, out rejectionReason))
            {
                return false;
            }

            // 在这里检查地图、敌对目标或其他开始条件。
            return true;
        }

        public override void OnStarted(ExaminationContext context)
        {
            base.OnStarted(context);

            // 生成第一波后记录实际数量；示例使用 6。
            context.SetLong(RemainingTargetsKey, 6L);
        }

        public override void OnLoaded(ExaminationContext context)
        {
            base.OnLoaded(context);

            // 只重建缓存或重新连接已存在对象，不要重复生成波次。
        }

        public override void Tick(ExaminationContext context)
        {
            base.Tick(context);

            if (context.GetLong(RemainingTargetsKey) <= 0L)
            {
                context.Pass();
            }
        }

        public override void OnPassed(ExaminationContext context)
        {
            CleanupSpawnedContent(context);
            base.OnPassed(context);
        }

        public override void OnFailed(ExaminationContext context)
        {
            CleanupSpawnedContent(context);
            base.OnFailed(context);
        }

        public override void OnCancelled(ExaminationContext context)
        {
            CleanupSpawnedContent(context);
            base.OnCancelled(context);
        }

        public override IEnumerable<string> ProgressLines(ExaminationContext context)
        {
            yield return "剩余考验目标：" + context.GetLong(RemainingTargetsKey);
        }

        private static void CleanupSpawnedContent(ExaminationContext context)
        {
            // 按保存的 ID 或 Submod 组件登记信息清理本次考验内容。
        }
    }
}
```

敌人死亡时，Submod 可以减少活动考验计数：

```csharp
using LCAnomalyStory;
using LCAnomalyStory.Examinations;

public static class OrdealNotifications
{
    public static void NotifyDawnTargetRemoved()
    {
        ExaminationContext context;
        if (StoryComponents.Development != null
            && StoryComponents.Development.TryGetActiveExaminationContext(out context)
            && context.Definition.defName == "MyOrdeal_Dawn")
        {
            context.IncrementLong(DawnOrdealWorker.RemainingTargetsKey, -1L);
        }
    }
}
```

不要仅凭 `defName` 在 Story 主模组中添加分支；此判断属于声明该 Def 的 Submod。

## 7. 自定义 Effect 示例

适合多个考验复用、无需独立状态机的行为可以实现为 Effect：

```csharp
using LCAnomalyStory.Examinations;

namespace MyOrdealSubmod
{
    public sealed class ExaminationEffect_BlockUnsafeStart : ExaminationEffect
    {
        public override bool CanStart(ExaminationContext context, out string rejectionReason)
        {
            bool safe = /* 检查地图状态 */ true;
            rejectionReason = safe ? null : "当前地图状态不允许开始考验。";
            return safe;
        }

        public override void OnStarted(ExaminationContext context)
        {
            // 应用效果。
        }

        public override void OnPassed(ExaminationContext context)
        {
            // 移除效果。
        }

        public override void OnFailed(ExaminationContext context)
        {
            // 移除效果。
        }

        public override void OnCancelled(ExaminationContext context)
        {
            // 移除效果。
        }
    }
}
```

XML：

```xml
<effects>
  <li Class="MyOrdealSubmod.ExaminationEffect_BlockUnsafeStart" />
</effects>
```

## 8. 外部控制 API

入口：

```csharp
GameComponent_CompanyDevelopment component = StoryComponents.Development;
```

主要方法：

```csharp
component.CanStartExamination(def, out rejectionReason);
component.StartExamination(def);
component.PassExamination(def, optionalReason);
component.FailExamination(def, optionalReason);
component.CancelExamination(def, optionalReason);
component.GetExaminationRuntime(def);
component.GetExaminationContext(def);
component.TryGetActiveExaminationContext(out context);
component.GetExaminationProgressLines(def);
```

`PassExamination`、`FailExamination`、`CancelExamination` 只接受当前活动考验；重复提交返回 `false`。成功通过时 Story 自动记录 `examinationsPassed` 并刷新发展节点。

`ExaminationContext` 提供：

```csharp
context.CurrentTick;
context.IsActive;
context.TicksRemaining;       // 无截止时间时为 -1
context.StatisticDelta(key);  // 相对本次尝试开始时
context.GetLong / SetLong / IncrementLong;
context.GetFloat / SetFloat;
context.GetString / SetString;
context.Pass / Fail / Cancel;
```

## 9. 复杂对象和存档规则

State 字典只能保存 `long`、`float`、`string`。不要把 `Pawn`、`Thing`、`Map`、`Lord` 或自定义对象塞入 Worker 字段，也不要依赖静态集合保存本次考验状态。

推荐方式：

- 少量对象：保存 `thingIDNumber`、地图唯一标识或自有逻辑 ID，`OnLoaded` 时重新查找；
- 大量对象、波次队列、Lord 或地图级机制：由 Submod 自有 `GameComponent` / `MapComponent` 使用标准 Scribe 保存；在 Story Runtime 中只保存关联 ID；
- `OnLoaded` 只能重新连接和修复状态，不得无条件重新生成敌人；
- `OnFailed`、`OnPassed`、`OnCancelled` 都必须幂等清理；对象可能已经死亡、离图或被其他 Mod 删除；
- 清理逻辑不得假设当前地图仍然存在。

## 10. 与发展节点连接

若新考验应控制公司发展，在 Submod XML 中向发展节点添加或替换 `DevelopmentCondition_ExaminationPassed`：

```xml
<li Class="LCAnomalyStory.Conditions.DevelopmentCondition_ExaminationPassed">
  <labelKey>LCStory_Condition_Examination</labelKey>
  <examination>MyOrdeal_Dawn</examination>
</li>
```

可使用 XML Patch 修改 Story 节点，但必须遵守：

- Submod 缺失时，Story 主线仍应存在基础考验或明确的替代路径；
- 不要让 Story 主程序集引用 Submod 类型；
- 不要通过完成 Core 研究反推考验通过；
- 一个考验 Def 只应由一个内容包负责生成和清理；
- 避免用随机不到的特定异想体永久阻塞主线。

## 11. 建议测试矩阵

每个考验至少验证：

1. 满足/不满足开始条件；
2. 正常通过；
3. 超时或主动失败；
4. 冷却结束后重试；
5. 活动期间保存并读档；
6. 每个波次节点保存并读档；
7. 目标已被其他 Mod 删除；
8. 地图被放弃或目标离图；
9. 开发者重置触发 `OnCancelled`；
10. Worker 故意抛异常时能够安全失败；
11. 中文和英文缺失键检查；
12. 仅构建和验证 RimWorld 1.6。

主项目验证命令：

```powershell
./Tools/Validate.ps1
./Tools/RuntimeSmokeTest.ps1
```

第二条需要本机 RimWorld、Harmony、Core 和 Story 已按脚本说明部署。

## 12. 当前限制

- 同一时间只支持一个活动考验；
- Story 只提供异想体工作成功率这一项内置数值修改入口，其他战斗或地图效果应由 Submod 自己实现；
- Worker Tick 是定期间隔，不代替 Pawn、Lord 或 MapComponent 的逐 Tick AI；
- State 字典是当前尝试级，不是跨多次尝试的永久数据；永久数据应放入 Submod 自有组件；
- 卸载声明活动考验的 Submod 后无法执行其专属清理逻辑，因此应把该 Submod 声明为存档所需依赖。

按照以上边界实现后，Story 继续作为发展进度的唯一权威来源，考验 Submod 可以独立增加敌人、波次和演出，而不需要复制或绕过公司阶段系统。
