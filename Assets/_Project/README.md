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
5. **Play Mode** — 绑定 **Expedition Setup** 时为 **三场连战远征**；仅 Battle Setup 时为单场 3v3

## 远征 Demo（三场连战）

1. 菜单 **`Grimhand → Content → Generate Demo ScriptableObjects`**（会生成 `ExpeditionSetup_Demo.asset` 并尝试自动绑定）
2. 选中 **BattleDemo**，确认 **Expedition Setup** 已绑定  
   - 或菜单 **`Grimhand → Content → Assign Demo Expedition Setup to Scene`**
3. **Play** — 流程：
   - 第 1 场战斗 → 胜利后 **三选一路线**（目前均为普通战斗，遭遇可复用）
   - 第 2 场 → 再选路线 → 第 3 场
   - 三场全胜 → **远征完成**；任一场战败 → **远征失败**
4. **血量跨场不恢复**（与杀戮尖塔一致）；每场敌人满血、牌堆重新洗牌
5. 底部 **重开远征** 可从头再来

仅想测单场战斗时，清空 Inspector 中的 **Expedition Setup** 即可。

## 操作说明

- **点选手牌** → 攻击/减益需 **选目标** → 确认出牌  
- **空过** → 本回合不出牌  
- **重开战斗** → 新随机种子 + 重新洗牌  

## 程序集

| 程序集 | 说明 |
|--------|------|
| Grimhand.Core | RNG 等通用工具 |
| Grimhand.Battle | 纯逻辑战斗内核（无 Unity 引用） |
| Grimhand.Expedition | 远征流程（路线选择、跨场 HP 继承） |
| Grimhand.Content | ScriptableObject 定义 |
| Grimhand.Presentation | IMGUI Demo 控制器 |
| Grimhand.Battle.Tests | EditMode 单元测试 |

## 文档

- [ScriptableObject 入门](Docs/ScriptableObject_入门.md) — SO 是什么、菜单怎么用  
- [卡牌 SO 指南](Docs/CardSO_Guide.md) — 如何新建/修改卡牌  

## 测试

Window → General → Test Runner → EditMode → 运行 `Grimhand.Battle.Tests`。
