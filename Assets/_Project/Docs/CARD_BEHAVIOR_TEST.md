# 卡牌行为测试说明（v0.9）

## 目的

保证 **xlsx 描述 = Catalog 文案 = Card_*.asset = 出牌后实际效果**。

新增或修改卡牌后，必须跑本测试；`check7`（行为回归）只认 `_card_behavior_verified.json` 里 Unity 跑绿记录。

## 一键运行（推荐）

**先关闭 Unity Editor**，然后在项目根目录执行：

```powershell
.\Assets\_Project\Docs\_tools\run_card_tests.ps1
```

或：

```powershell
python Assets/_Project/Docs/_tools/run_card_behavior_batch.py
```

成功时终端显示 `行为批量测试: 238/238 通过`，并更新：

| 文件 | 含义 |
|------|------|
| `_card_behavior_verified.json` | 跑绿的卡（check7 权威） |
| `_card_behavior_last_run.json` | 最近一次逐卡结果 |
| `_card_fix_report_v09.md` | 人类可读修复报告 |
| `_card_verification_master.json` | 7 项 strict 总表 |

## 新增卡牌流程

1. 在 xlsx / `_card_master_v09.json` 增加条目  
2. 创建 `Assets/_Project/Data/Cards/Card_{cardId}.asset`  
3. 更新 `CardDescriptionCatalog.cs` 描述  
4. 运行 `python Assets/_Project/Docs/_tools/repair_cards_by_master.py --card {cardId}`（可选，从描述推断 Actions）  
5. **若描述含【快速启动】，asset 必须有 `quick_start` keyword**（否则规划阶段点按不会立即生效）  
6. **关闭 Unity**，运行 `run_card_tests.ps1`  
7. 若失败：看 `_card_fix_report_v09.md` → 修 asset/引擎 → 再跑直到通过  

## 【快速启动】卡（11 张）

规划阶段**单击**手牌立即结算（`BattleEngine.TryResolveQuickStartCard`），不进入速度队列。

| cardId | 名称 |
|--------|------|
| `w_war_cry` | 战吼鼓舞 |
| `w_first_strike` | 先发制人 |
| `p_memory_fragment` | 记忆残片 |
| `p_revive_bless` | 复活祝福 |
| `d_curse_chain` | 诅咒之链 |
| `v_digest_venom` | 消化剧毒 |
| `l_gather_energy` | 聚能 |
| `l_dread_whisper` | 恐惧低语 |
| `l_summon_chaos_spirit` | 召唤混乱之灵 |
| `l_realm_descent` | 灵界降临 |

重建后检查：`Card_*.asset` 的 `Keywords` 含 `quick_start`。

单卡重建 Actions：

```powershell
python Assets/_Project/Docs/_tools/repair_cards_by_master.py --card w_new_card
```

单卡 strict 核对：

```powershell
python Assets/_Project/Docs/_tools/verify_card_strict.py --card w_new_card
```

## Unity 菜单（Editor 开着时）

`Grimhand → Cards → Run All V09 Behavior Tests (238)`

## 技术说明

- 测试入口：`Grimhand.Editor.CardV09BehaviorBatchRunner`（batchmode `-executeMethod`）  
- 每张卡：加载 asset → 构造最小战场 → `EffectActionExecutor` / `RespondEffectExecutor` → 断言 HP/护甲/状态  
- 钩子牌（如 `p_solar_god_wrath`）Actions 为空，由 `PassiveCardMechanicsRules` 承担，测试白名单豁免  

## 常见问题

| 现象 | 处理 |
|------|------|
| `No tests were executed`（NUnit） | 正常；请用本 batch 脚本，不用 Test Runner 计数 |
| Unity 已打开 | 关掉 Editor 再跑 |
| 大量 `StatusId` 为空 | 运行 `repair_cards_by_master.py --failures-from-log <log>` |
