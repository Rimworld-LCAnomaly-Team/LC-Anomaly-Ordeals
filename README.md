# LC Anomaly Ordeals

面向 RimWorld 1.7 的 `LC Anomaly Story` 考验内容包。当前版本实现原作的黎明、正午、黄昏和午夜考验，Story 的阶段门槛依次接入对应层级的随机考验镇压。

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

## 当前美术说明

所有考验实体均使用本模组独立的 Race、FleshType 与 PawnKind 体系，不继承原版虫族或机械族。当前可运行版本仅复用 RimWorld 原版虫族和机械体图形、身体结构、音效与通用 AI，并以颜色区分，不包含《脑叶公司》的原始美术或音频资源。代码和 Def 已为后续原创资源替换保持稳定命名。
