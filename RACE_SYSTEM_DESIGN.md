# 考验实体种族体系

## 目标

所有可生成的考验实体均使用本模组定义的独立 `ThingDef`（Race）和 `PawnKindDef`。它们不继承原版虫族或机械族的 Race、PawnKind，也不直接引用 `Megascarab`、`Megaspider`、`Mech_Scyther`、`Mech_Centipede*` 作为种族。

考验实体仍可复用 RimWorld 的通用设施：`BodyDef`、`ThinkTreeDef`、伤口与命中效果、音效和当前占位贴图。这些引用只决定身体结构、AI 或表现，不构成种族继承。

## Def 层级

`LCOrdeal_RaceBase` 只继承通用 `BasePawn`，统一定义考验显现体的非有机血肉类型、需求、性别、食物、基础抗性和 AI。其下按身体与战斗轮廓分为四个抽象模板：

- `LCOrdeal_RaceSmall`：小型显现体。
- `LCOrdeal_RaceLarge`：大型显现体。
- `LCOrdeal_RaceWalker`：高速近战显现体。
- `LCOrdeal_RaceHeavy`：重型显现体。

每一个具体 `PawnKindDef` 都指向唯一的 `LCOrdeal_Race_*`。共享抽象模板只用于减少重复属性，不会让不同考验实体共享同一个具体 RaceDef。

`LCOrdeal_ManifestationFlesh` 是考验实体专用 `FleshTypeDef`，负责非有机判定、机械伤口图层、命中特效和尸体分类。`LCOrdeal_KindBase` 是考验实体专用的抽象 PawnKind 父项，负责默认敌对阵营和装备清理规则。

## 兼容边界

- `Faction.OfMechanoids` 仍作为当前敌对阵营载体；这是阵营归属，不是种族继承。
- 当前图形路径仍指向原版虫族与机械体素材；替换原创素材时只需改动图形路径，不需更改 RaceDef 或 PawnKindDef 名称。
- 新生成的考验实体使用独立种族。旧存档中已经生成并保存的实体不会被强制换种，以避免破坏存档对象。
- 仅维护 RimWorld 1.6；`1.5/` 与 `Source/1.5/` 不参与本体系。

## 文件位置

- `1.6/Defs/FleshTypeDefs/OrdealFleshTypes.xml`
- `1.6/Defs/ThingDefs_Races/OrdealRaces.xml`
- `1.6/Defs/PawnKinds/DawnOrdealPawnKinds.xml`
- `1.6/Languages/ChineseSimplified (简体中文)/DefInjected/ThingDef/OrdealRaces.xml`
