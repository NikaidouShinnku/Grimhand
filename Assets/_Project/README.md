# Grimhand Battle Demo

## 快速开始

1. 在 Unity 中打开项目，等待 **Console 无红色报错**。
2. **一键准备测试场景（推荐）**  
   菜单 **`Grimhand` → `Open Battle Test Scene`**  
   - 自动生成 Demo 数据、角色立绘目录、战斗 UI，并保存 **`Assets/_Project/Scenes/BattleSandbox.unity`**
3. **直接 Play**  
   在 Project 窗口双击 **`BattleSandbox.unity`**，点击顶部 **▶ Play** 即可开始游戏。  
   - 无需再点其它 Setup 菜单  
   - 我方立绘：**战士 / 法老 / 恶魔**（对应前排/中排/后排角色）

若 Project 里已有 `BattleSandbox.unity`，也可跳过菜单，直接打开该场景 Play。

## 战斗 UI（uGUI）

场景内已包含 Canvas、手牌、战场槽位（含角色立绘）、远征遮罩。可选：

- 每张 **`CardDefinitionSO`** 的 **Card Art / Card Frame** 字段  
- 或 **`Assets/_Project/Data/CardVisualCatalog_Demo.asset`** 统一配置卡图  
- 角色立绘：**`Assets/_Project/Data/CharacterVisualCatalog_Demo.asset`**

卡图/立绘未绑定时显示占位色块，不影响测试逻辑。

## 远征 Demo（三场连战）

Play 后即为 **三场连战远征**；胜利后三选一路线，**HP 跨场不恢复**。

## 操作说明

- **点选手牌** → 攻击/减益需 **选目标** → 确认出牌  
- **空过** → 本回合不出牌  
- **悬停手牌** → 关键词说明  
- **重开远征/战斗** → 从头再来  

## 程序集

| 程序集 | 说明 |
|--------|------|
| Grimhand.Core | RNG 等通用工具 |
| Grimhand.Battle | 纯逻辑战斗内核（无 Unity 引用） |
| Grimhand.Expedition | 远征流程（路线选择、跨场 HP 继承） |
| Grimhand.Content | ScriptableObject 定义 |
| Grimhand.Presentation | uGUI 战斗界面 + IMGUI 调试 Demo |
| Grimhand.Battle.Tests | EditMode 单元测试 |

## 文档

- [ScriptableObject 入门](Docs/ScriptableObject_入门.md) — SO 是什么、菜单怎么用  
- [卡牌 SO 指南](Docs/CardSO_Guide.md) — 如何新建/修改卡牌  

## 测试

Window → General → Test Runner → EditMode → 运行 `Grimhand.Battle.Tests`。
