# 卡牌行为测试与修复报告（v0.9）

生成时间：2026-07-03 12:26 UTC
测试批次：2026-07-03T10:29:42.8107200Z

## 摘要

| 指标 | 结果 |
|------|------|
| **行为测试（check7 权威）** | **238 / 238** |
| 行为失败 | 0 |
| strict 7 项总表 | 215 / 238 OK |

> 行为失败 = asset 出牌效果与描述不一致。strict pending = Catalog/描述/asset 静态项或 check7 未同步。

## 如何复跑

```powershell
# 关闭 Unity Editor 后
.\Assets\_Project\Docs\_tools\run_card_tests.ps1
```

详见 [CARD_BEHAVIOR_TEST.md](./CARD_BEHAVIOR_TEST.md)

## 本轮主要修复

### 测试基础设施
- `CardV09BehaviorBatchRunner.cs`：238 张逐张 Unity batchmode 行为断言（HP/护甲/状态/应对）
- `run_card_tests.ps1` + `run_card_behavior_batch.py`：一键跑测并刷新 `_card_behavior_verified.json`
- `gen_card_fix_report.py`：生成本报告

### 工具链 / asset
- 修复 `repair_and_sync_cards.py` 中 `T_RANDOM_ENEMY=13`、应对状态推断、`CARD_ID_OVERRIDES` 被动/特殊卡
- `repair_cards_by_master.py` 支持 `--card` / `--failures-from-log` 批量重建 Actions
- 修复大量卡 `StatusId` 为空、应对卡 `Condition` 错误、怪物【中/后】Reach 站位

### 测试器改进
- 三排站位（前/中/后）满足 Reach 与 `w_war_cry` 等槽位 buff
- 区分玩家应对 / 怪物条件攻击 / 怪物应对武装
- 可变伤害（随机目标、多段、献祭、条件加成）降低误报

## 行为测试

✅ **全部 238 张通过**（`_card_behavior_verified.json` 已更新）

## strict 仍 pending（23 张，非行为失败）

多为 Catalog 文案、描述 regex 或 check1–6 静态项；行为已绿时可逐张跑：

```powershell
python Assets/_Project/Docs/_tools/verify_card_strict.py --card <cardId>
```

- `w_taunt`
- `w_respond_stance`
- `w_burning_fury`
- `w_god_descends`
- `w_last_stand`
- `p_rot_touch`
- `p_curse_deepen`
- `p_sand_spear_reforge`
- `p_holy_infusion`
- `p_rot_avatar`
- `d_demon_curse`
- `d_vamp_aura`
- `d_demon_lord`
- `d_final_blood_ritual`
- `v_detonate_venom`
- `v_shed_skin`
- `g_blood_scratch`
- `m_raise_bones`
- `m_spider_fatal_bind`
- `m_golem_fist`
- `m_golem_crack_fist`
- `m_pinch_armor`
- `m_fester_claw`

---

## 2026-07-03 机制修复汇总（第四~五轮）

### 战斗核心
| 主题 | 修复 |
|------|------|
| 无视 N% 护甲 | 有效护甲折算后同时扣 Block 与 HP（50% 无视 +10 伤 vs 10 护甲 → 护甲 5、HP -5） |
| 神圣灌注 | 0 费；队列中紧接下一张 +1 费；`PlanningDraft.GetPlayCost` public |
| 沙矛重塑 | 远征累计消耗牌计数；打出时 4 伤×计数、每次随机敌人；**3 费紫框攻击牌** |
| 状态悬停框 | 紧贴主框右侧；增伤等显示「所有攻击牌伤害 +N%」等描述 |
| 快速启动 | 击杀全敌立即 `EvaluateOutcome` 胜利 |
| 血族传承 | 只加 MaxHp，当前 Hp 不变，UI 即时刷新 |
| 血祭坛 | 献祭 -15% 自伤；叠 SacrificeAttackStacks；增伤改 StatusApplied 非护甲动画 |
| 瘟疫蔓延 | 中毒 tick 后 30% 传染相邻敌人半层，继承持续时间 |
| 死亡角色牌 | 不可选、不 fallback 到其他存活同职业 |
| 浪潮冲锋 | `BonusIfActorFasterThanAllEnemiesFlat=8`，修复损坏 asset |

### UI
- 角色悬停：主框旁独立状态描述框（炉石式）
- 手牌：`GetPlayCost` 含神圣灌注 surcharge，修复 CS1061

### 测试
- 238/238 行为测通过；新增无视护甲、血族传承、沙矛、浪潮冲锋单元测试

详见 `战斗逻辑及机制参考.docx` §十四附。

