# LC Anomaly Ordeals

面向 RimWorld 1.6 的 `LC Anomaly Story` 考验内容包。当前版本实现四种黎明考验：琥珀、深红、翠绿和紫罗兰，并将 Story 的第一阶段基础考验替换为随机黎明镇压。

详细玩法与技术边界见 [DAWN_ORDEAL_DESIGN.md](DAWN_ORDEAL_DESIGN.md)，接入框架约束见 [ORDEAL_SUBMOD_GUIDE.md](ORDEAL_SUBMOD_GUIDE.md)。

## 依赖与加载顺序

1. Harmony
2. LC Anomaly Core
3. LC Anomaly Story
4. LC Anomaly Ordeals

只支持 RimWorld 1.6。

## 构建与验证

```powershell
./Tools/Build.ps1
./Tools/Validate.ps1
```

项目默认从 `Source/1.6/LocalDllPaths.props` 读取本机程序集位置。首次构建时请复制
`Source/1.6/LocalDllPaths.props.example` 为 `Source/1.6/LocalDllPaths.props`，再填写本机路径；本地配置不会提交到 Git。

## 当前美术说明

首个可运行版本复用 RimWorld 原版虫族和机械体图形并进行颜色区分，不包含《脑叶公司》的原始美术或音频资源。代码和 Def 已为后续原创资源替换保持稳定命名。
