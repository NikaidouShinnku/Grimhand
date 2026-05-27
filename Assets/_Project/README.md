# Grimhand Battle Demo

## 快速开始

1. 在 Unity 中打开项目，等待 **Console 无红色报错**。
2. **生成数据（首次必做）**  
   菜单 **`Grimhand` → `Content` → `Generate Demo ScriptableObjects`**  
   - 会在 Project 里创建 `Assets/_Project/Data/`  
   - 会弹出完成对话框，并高亮 `BattleSetup_Demo.asset`  
   - 不懂 SO？见 [`Docs/ScriptableObject_入门.md`](Docs/ScriptableObject_入门.md)
3. **创建/打开场景**  
   **`Grimhand` → `Setup Battle Sandbox Scene`**  
   - 或打开已有 `Assets/_Project/Scenes/BattleSandbox.unity`
4. 选中 **BattleDemo**，确认 Inspector 中 **Battle Setup** 已绑定（若为空：  
   **`Grimhand` → `Content` → `Assign Demo Battle Setup to Scene`**）
5. **Play Mode** — 当前为 **3v3**（三前排哥布林小队 vs 我方三人）

## 操作说明

- **点选手牌** → 攻击/减益需 **选目标** → 确认出牌  
- **空过** → 本回合不出牌  
- **重开战斗** → 新随机种子 + 重新洗牌  

## 程序集

| 程序集 | 说明 |
|--------|------|
| Grimhand.Core | RNG 等通用工具 |
| Grimhand.Battle | 纯逻辑战斗内核（无 Unity 引用） |
| Grimhand.Content | ScriptableObject 定义 |
| Grimhand.Presentation | IMGUI Demo 控制器 |
| Grimhand.Battle.Tests | EditMode 单元测试 |

## 文档

- [ScriptableObject 入门](Docs/ScriptableObject_入门.md) — SO 是什么、菜单怎么用  
- [卡牌 SO 指南](Docs/CardSO_Guide.md) — 如何新建/修改卡牌  

## 测试

Window → General → Test Runner → EditMode → 运行 `Grimhand.Battle.Tests`。
