# Grimhand 开发交接总结

**日期：** 2026-06-03  
**项目路径：** `c:\Users\Kelthuzad\Documents\GitHub\Grimhand\Grimhand`  
**当前阶段：** 数值策划表 v2 **首版内容已进游戏**（Lv1 三名玩家 + 30 张牌）；战斗演出与 **护甲 UI** 已打通；等级 / 遗物 / 楼层等待做

**上一篇：** [SessionHandoff_2026-06-02.md](SessionHandoff_2026-06-02.md)（uGUI 第二版、悬停详情、速度序队列）

**策划表导出：** [v2_balance.json](v2_balance.json)（来自 `Grimhand_数值策划表_v2.xlsx`）

---

## 一、本会话完成事项

### 1. 数值策划表 v2 — 玩家 Lv1 + 30 张卡牌

| 角色 | HP | ATK | DEF | SPD | 站位 | CharacterId |
|------|-----|-----|-----|-----|------|-------------|
| 战士 | 50 | 8 | 6 | 4 | 前排 | `char_knight` |
| 法老 | 40 | 6 | 4 | 6 | 中排 | `char_mage` |
| 恶魔 | 30 | 9 | 2 | 8 | 后排 | `char_ranger` |

- 每名角色 **10 张不重复牌**（2 基础 + 4 职业 + 4 自由），Deck 在 `Character_*.asset` 中
- 卡牌 asset：`Assets/_Project/Data/Cards/Card_w_*`、`Card_p_*`、`Card_d_*`（共 30 张）
- 伤害/护甲公式支持 **ATK/DEF 倍率**：`AttackScalePercent` / `DefenseScalePercent`（如 `ATK×0.8+3` → 倍率 80 + 固定 3）
- 新增 `EffectActionType.DrawCards`（出牌时立即抽牌，如法老权令、恶魔契约）

**内容生成管线：**

| 入口 | 说明 |
|------|------|
| `BalanceV2ContentGenerator.cs` | Editor 脚本，按 v2 表生成 30 卡 + 三名玩家 SO |
| `GrimhandContentMenu.GenerateDemoAssetsSilent` | 菜单调用上述生成器 + 更新 `BattleSetup_Demo` |
| `Temp/gen_v2_assets.py` | 一次性 Python 脚本（已用过）；**优先用 Unity 菜单重新生成** |

**菜单：** `Grimhand → Content → Generate Demo ScriptableObjects`

---

### 2. 随机抽牌 / 种子

**问题：** 每局首手相同 → `ExpeditionSetup_Demo.RunSeed = 42` + `BattleSetup_Demo.Seed = 42` 固定序列。

**修复：**

- `Seed = 0` / `RunSeed = 0` 表示 **每次开局随机**
- `ExpeditionSetupSO.ToExpeditionConfig()` 在 `RunSeed <= 0` 时 `Random.Range`
- 单场战斗 `BattleSession.RestartBattle()` 仍会覆盖为随机种子
- Demo asset 已改为 `Seed: 0`、`RunSeed: 0`

---

### 3. 敌人数值（1 层基准，临时 Demo）

| 显示名 | HP | ATK | DEF | SPD |
|--------|-----|-----|-----|-----|
| 哥布林 | 20 | 4 | 1 | 5 |
| 骷髅兵 | 25 | 6 | 3 | 4 |
| 幽灵 | 18 | 7 | 1 | 7 |

内部 ID 仍为 `char_goblin_*`，仅显示名与数值微调。**未做**楼层浮动、多遭遇配置。

---

### 4. 护甲（Block）UI 与演出同步

**问题：**

1. 逻辑层 `ProcessEndOfTurn` 在演出前就 `Block = 0`，UI 读不到护甲  
2. `BlockGained` 未驱动刷新  
3. 图标误用 DEF；竖排布局导致 HP 上推遮挡名字  

**修复：**

| 模块 | 改动 |
|------|------|
| `PresentationSnapshot` | 演出期间单独追踪 `_block`（获得 / 消耗 / 演出结束清零） |
| `BattleEventPlayback.SplitIntoSegments` | 出牌段落内保留 `BlockGained` 等事件 |
| `BattlePortraitDirector` | 处理 `BlockGained`、受击扣护甲、`ShowBlockAbsorbedNumber` 浮动字 |
| `UnitStatsRowView` | **HP 与 ARM 同一行**：`❤ 36/50` + 右侧 `ARM 12`；无护甲时隐藏 ARM 芯片 |
| `BattleUiIconCatalog_Demo` | `ArmorIcon` → `Assets/The Grimhands Asset/icon/ARM.png` |
| `GrimhandUiVisualBootstrap` | 刷新目录时自动绑定 ARM |

**悬停详情：** 仍不显示护甲（此前约定）；脚下只显示 HP + ARM。

---

### 5. 编译修复

`BalanceV2ContentGenerator.cs`：`EffectTarget.AllyFrontSlot` 前缀、`using UnityEngine`、`Merge(AoeDmg)` 与 `params` 混用等问题已修。

---

## 二、卡牌效果 — 已实现 vs 简化

策划表内许多机制 **引擎尚未支持**，当前为 **可玩简化版**：

| 策划效果 | 当前实现 |
|----------|----------|
| 基础伤害/护甲公式 | ✅ 倍率 + 固定值 |
| 铁壁弹反 | ✅ 减伤 50% + 反射 100% |
| 献祭 HP | ✅ `DealDamage` → Self + 对敌伤害 |
| AOE | ✅ 三槽位各一次 `DealDamage` |
| 抽牌 | ✅ `DrawCards` 立即抽 |
| 中毒/减速 | ✅ `poison` / `slow` 状态 |
| 嘲讽 / 伤害转移 | ⚠️ 仅给固定护甲 |
| 战吼 ATK+3 全队 | ⚠️ 简化为全队 +3 护甲 |
| 复活祝福 | ⚠️ 简化为治疗 10 |
| 无视 DEF / 条件加伤 / 击杀回血 | ❌ 未做 |
| 吸血按伤害比例 | ⚠️ 固定治疗量近似 |

**未做（用户明确留明天）：** 等级成长、遗物、装备、20 层关卡、经验系统、多遭遇 SO。

---

## 三、关键文件索引

```
Assets/_Project/
  Docs/
    v2_balance.json              ← 策划表 JSON 导出
    SessionHandoff_2026-06-03.md ← 本文
  Data/
    Cards/Card_{w,p,d}_*.asset   ← 30 张玩家卡
    Characters/Character_*.asset   ← 6 角色（含 Deck）
    Setups/
      BattleSetup_Demo.asset       ← Seed=0
      ExpeditionSetup_Demo.asset   ← RunSeed=0
    BattleUiIconCatalog_Demo.asset ← ArmorIcon=ARM
  Scripts/
    Content/Editor/
      BalanceV2ContentGenerator.cs
      GrimhandContentMenu.cs
    Battle/
      Model/EffectActionSpec.cs    ← AttackScalePercent 等
      Effects/EffectActionExecutor.cs
    Presentation/Battle/
      PresentationSnapshot.cs      ← HP + Block 演出快照
      BattlePortraitDirector.cs
      UnitStatsRowView.cs            ← HP | ARM 横排
      CombatantSlotView.cs
```

---

## 四、如何跑 Demo

1. 打开 `Assets/_Project/Scenes/BattleSandbox.unity` → **Play**
2. 若卡牌/角色数据旧：`Grimhand → Content → Generate Demo ScriptableObjects`
3. 若图标不对：`Grimhand → Content → Refresh UI Visual Catalogs`
4. 验证随机：多开几局，首手应不同；战士打「举盾格挡」后 HP 右侧应出现 **ARM** 图标与数字

---

## 五、已知问题 / 技术债

1. **DEF 未参与伤害公式** — 策划表为 `(ATK×倍率+固定)×位置 - 目标DEF`，引擎目前只扣 Block，未减 DEF  
2. **演出结束后护甲才从快照清零** — 与逻辑层 `ProcessEndOfTurn` 清零存在时间差，靠 `PresentationSnapshot` 补显示；若跳过演出需再测  
3. **旧场景 Stats 节点** — 若 Play 后布局仍竖排，重进 Play 或删 Slot 下 `Stats` 子物体让 `UnitStatsRowView` 重建  
4. **Demo 工厂** `DemoBattleFactory.cs` 仍为旧硬编码，场景走 SO 时无影响；无 SO 时 fallback 仍是旧卡  
5. **Unity 批处理生成** — 项目已打开时无法用 `-executeMethod` 批跑，需切菜单或关 Editor  

---

## 六、下次建议优先级

### P0 — 内容补完（策划表 v2 剩余）

- [ ] 复杂卡牌机制：嘲讽、伤害转移、条件加伤、复活、真实吸血  
- [ ] 伤害公式接入 **目标 DEF**（最低 1）  
- [ ] 敌人完整配置（哥布林×2 等遭遇、±10% 浮动）  
- [ ] 等级成长 / 遗物 / 楼层 — **用户说留到后续会话**

### P1 — 体验

- [ ] 护甲消耗数字与 HP 伤害数字分层（避免同 floater 互抢）  
- [ ] 卡牌图 / 框与 v2 卡名对齐（`CardVisualCatalog` 按 CardId）  
- [ ] 多场不同 `BattleSetupSO` 远征路线  

### P2 — 工程

- [ ] 删除或归档 `Temp/gen_v2_assets.py`  
- [ ] `BalanceV2ContentGenerator` 与 Python 脚本二选一，避免双源  
- [ ] 提交 git（本会话改动未要求 commit）

---

## 七、演出架构速查（勿破坏）

| 模块 | 作用 |
|------|------|
| `PresentationSnapshot` | 演出期间 HP/Block/存活与逻辑解耦 |
| `BattleEventPlayback` | 按出牌段拆分事件 |
| `BattlePortraitDirector` | 立绘队列；`ParryTriggered`；`BlockGained` |
| `BattleSession.OnPresentationComplete` | 演出结束再远征选关 |
| `BattleActiveCardBanner` | 中央上方当前牌 |

弹反：减伤 + 反射后防御者切 Attack 立绘反伤（`ParryTriggered`）。

---

## 八、相关对话

本会话 Cursor 记录（含护甲 UI、v2 数值接入）：见 agent transcript `1ac6b155-6409-4327-8c65-4e34f0ebbcbb`。

---

*文档由 2026-06-03 会话整理，便于下次从「简化卡牌机制 + 敌人遭遇 + 伤害公式」继续。*
