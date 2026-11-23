# LC Anomaly Ordeals

> **⚠️ 极早期不完善版本（Early WIP）**：当前考验系统（含全部 22 种考验原版贴图/多帧 Spine 姿态/音效/四色战斗/实时技能特效）仍处于极早期开发验证阶段，数值平衡、动画衔接、特效表现、AI 节奏与流程体验均未定型，后续将继续大幅打磨与调整，仅供早期体验与反馈。

面向 RimWorld 1.7 的 `LC Anomaly Story` 考验内容包。当前版本实现原作的黎明、正午、黄昏、午夜与白昼考验，Story 的阶段门槛依次接入对应层级的考验镇压。

详细玩法见 [DAWN_ORDEAL_DESIGN.md](DAWN_ORDEAL_DESIGN.md)、[NOON_ORDEAL_DESIGN.md](NOON_ORDEAL_DESIGN.md)、[DUSK_ORDEAL_DESIGN.md](DUSK_ORDEAL_DESIGN.md)、[MIDNIGHT_ORDEAL_DESIGN.md](MIDNIGHT_ORDEAL_DESIGN.md) 和 [WHITE_ORDEAL_DESIGN.md](WHITE_ORDEAL_DESIGN.md)，种族结构见 [RACE_SYSTEM_DESIGN.md](RACE_SYSTEM_DESIGN.md)，接入框架约束见 [ORDEAL_SUBMOD_GUIDE.md](ORDEAL_SUBMOD_GUIDE.md)。

## 依赖与加载顺序

1. Harmony
2. LC Anomaly Core
3. LC Anomaly Story
4. LC Anomaly Ordeals

只支持 RimWorld 1.7，使用 .NET 10；无需 Anomaly DLC。1.5、1.6 内容及项目已从当前发布树移除，历史版本保留在 Git 历史中。

## 构建与验证

```powershell
./Tools/Build.ps1
./Tools/Validate.ps1
./Tools/RuntimeSmokeTest.ps1
```

安装 .NET 10 SDK，并将 Core、Story 和本模组放在同一游戏安装的 `Mods` 目录。项目引用游戏根目录的 1.7 程序集、同级 Core / Story 项目以及 Harmony 的 `1.7/Assemblies`。非默认安装可给构建脚本传入 `-RimWorldRoot` 和 `-HarmonyAssembliesDir`。

运行测试入口复用 Core 的 1.7 系列测试，自动生成独立配置，默认关闭 Anomaly，执行地图、玩法和存档读回检查。可使用 `-DlcProfile BaseGame` 或 `-DlcProfile All` 切换配置。

## 美术、音频与四色机制说明

所有考验实体均使用本模组独立的 Race、FleshType 与 PawnKind 体系，不继承原版虫族或机械族。

- **全部 22 种考验实体已全面原版化**：涵盖黎明（4 种）、正午（4 种）、黄昏（3 种）、午夜（3 种 / 6 类实体）与白昼（4 色收尾人与爪牙），均已升级为原作 Spine 骨骼渲染多方向贴图（`Cont/Textures/Things/Pawn/Ordeals/`）、原作开场/结束与战斗技能音效（`Cont/Sounds/Ordeals/`）、原作四色承伤抗性（`FourColorTargetExtension`）、四色技能伤害（`LC_RedDamage` / `LC_WhiteDamage` / `LC_BlackDamage` / `LC_PaleDamage`）以及考验镇压奖励闭环（统计、分层部门经验与主管战术子弹回满）。
- **全考验多帧动画与签名特效系统**：
  - 原生 RimWorld 1.7 `PawnRenderTreeDef`（`LCOrdeal_Animated`）+ `CompOrdealAnimator` / `PawnRenderNode_OrdealAnimated`：为全部 22 种考验实体补全了基于原作 Spine 骨骼导出的 `_move1` / `_move2` / `_attack` / `_special` 多方向关键帧，并叠加 6 类程序化辅助运动（双足迈步、蠕虫爬行、神龛悬浮、小丑滚冲、工厂脉动、终末螺旋塔蓄力/开壳）。
  - 60 FPS 实时战斗特效渲染引擎（`MapComponent_OrdealVfx` + `OrdealVfxUtility`）与 14 张原作解包特效贴图（`Cont/Textures/FX/Ordeals/`）：完整实装绿午夜“终末螺旋”双轨翠绿旋转激光、紫罗兰午夜四色神龛传送门预警 + 红/白/黑触手 + 苍白之眼、紫罗兰正午天降传送门与裂地冲击、红/白/黑/苍白收尾人激光/白雾/祈祷反伤光环/240° 死亡激光扫射/传送门闪现，以及“爪牙”橙/蓝/绿三色血清冲刺光束、蓝血清锁定准星（`Aim_Default`）与连闪斩击特效。
