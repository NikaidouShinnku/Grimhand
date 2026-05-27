# Grimhand Battle Demo

## 快速开始

1. 在 Unity 中打开项目，等待 **Console 无红色报错**（若有报错需先修复才能继续）。
2. **方式 A（推荐）**：顶部菜单栏点击 **`Grimhand` → `Setup Battle Sandbox Scene`**
   - 这不是 Project 里的某个资源文件，而是 **Editor 菜单项**（脚本在 `Scripts/Editor/BattleSandboxSetup.cs`）。
   - 执行后会自动创建场景 `Assets/_Project/Scenes/BattleSandbox.unity` 并挂上 Demo 组件。
3. **方式 B（手动）**：若菜单里没有 `Grimhand` 项：
   - `File → New Scene` → 保存为 `Assets/_Project/Scenes/BattleSandbox.unity`
   - 在 Hierarchy 创建空物体 `BattleDemo`
   - `Add Component` → 搜索 **`Battle Demo Controller`** 并添加
4. 打开 `BattleSandbox` 场景，进入 **Play Mode**。

## 操作说明

- **点选手牌**：预选并立即扣除能量；再次点击取消并返还能量。
- **能量不足**时无法新选牌。
- 点击 **「确认出牌」** 提交计划（不再二次扣费），进入速度结算。
- **「重开战斗」** 重置 3v1 演示。

## 程序集

| 程序集 | 说明 |
|--------|------|
| Grimhand.Core | RNG 等通用工具 |
| Grimhand.Battle | 纯逻辑战斗内核（无 Unity 引用） |
| Grimhand.Content | ScriptableObject（后续扩展） |
| Grimhand.Presentation | IMGUI Demo 控制器 |
| Grimhand.Battle.Tests | EditMode 单元测试 |

## 测试

Window → General → Test Runner → EditMode → 运行 `Grimhand.Battle.Tests`。
