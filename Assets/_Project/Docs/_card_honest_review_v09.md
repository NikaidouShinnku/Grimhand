# v0.9 卡牌实装诚实核对稿（讨论用）

> 生成时间：2026-07-03 09:33 UTC  
> **目的**：说明「238/238 strict OK」实际证明了什么、没证明什么，并逐张列出风险与待验证项。  
> **不是**「全部实装完成」的签字页。

---

## 一、为什么我说「问题可能还多着」

`verify_card_strict.py` 的 7 项检查是**静态**的，且有多处「能通过但不代表能玩对」：

| 检查项 | 实际在查什么 | 没查什么 |
|--------|-------------|----------|
| check1 effect_text | Catalog 与 xlsx 逐字 | 运行时 UI 是否刷新、升级牌实例 |
| check2 cost/keywords | asset 存在、无 respond_attack | Cost 与 xlsx 逐字段、Rarity |
| check3 actions_semantic | 简单 regex：伤害/护甲/中毒数值 | 应对 Condition 分段、多段效果、X 费、条件分支 |
| check4 battle_position | YAML Reach 与括号一致 | `ShouldPromptForTarget` 3v3 实测、选后排拒选 |
| check5 hooks | C# 里能搜到 cardId/statusId/动作类型名 | 钩子**逻辑**是否与描述一致 |
| check6 presentation | **几乎恒 true** | §十二 演出、Pose、VFX |
| check7 regression | 235/238 仅 `CardV09CatalogRegressionTests`（有描述且无 TODO） | 出牌、伤害、被动触发 |

- **PASSIVE_HANDLED 白名单**：49 张卡 check3 **整卡跳过**数值对比。
- **SPECIAL 豁免**：3 张（见下文）。
- **批量脚本改 asset**：Reach 90 张量级、被动/钩子若干张，**无逐张 PlayMode 证据**。
- **Unity 测试**：MCP Test Runner 跑 `Grimhand.Battle.Tests` 返回 **0 tests**（环境/程序集问题），`TargetPickRulesTests` / `BattleScopePassiveTests` **未在 CI 里绿过**。

---

## 二、统计摘要

| 维度 | 数量 |
|------|------|
| 总卡数 | 238 |
| strict 标 OK | 238 |
| 高风险（含未实装/批量修复标注） | 1 |
| 中风险 | 48 |
| 含位置括号 | 90 |
| 含「本场战斗」 | 19 |
| 仅 Catalog 回归 / 无行为测试 | 232 |
| PASSIVE_HANDLED 跳过语义 | 49 |

---

## 三、明确有问题的卡（优先手测 / 补代码）

### 188. `m_tide_charge` · 浪潮冲锋
- **xlsx**：【前/中/后】造成12伤害。如果自身速度快于所有敌人，则额外造成8伤害
- **风险**：高
- 数值/语义 compare 整卡跳过
- 曾 Target=全体；已改单目标 Reach=0；**速度快于敌人 +8 伤未实装**（asset 误用 BonusIfTargetHpBelowFlat）

---

## 四、SPECIAL 豁免三张（strict OK 但不算「正常卡」）

### `w_author_realm_strike` · 作者境的一击
- 测试卡：Catalog 与 xlsx 故意不一致，无行为测试
- xlsx：`【AOE】造成9999伤害（无法获取）`

### `m_hp` · HP
- 总览表遗留占位，无 asset，已删除
- xlsx：`Boss特性`

### `m_220` · 220
- Boss 特性卡，逻辑在 BossTraitRules，无独立 asset
- xlsx：`当血量第一次低于120时，获得1回合【虚化】（本回合受到的所有攻击最多造成1点伤害），并将幽灵女王之怒加入下回合手牌`

---

## 五、「本场战斗中」19 张 — 静态 OK ≠ 行为 OK

清单见 `_battle_scope_cards_v09.json`。静态检查只要求：Permanent status 或已知钩子字符串存在。
**以下逐张列出 xlsx、批量修复备注、待验证行为。**

#### `w_respond_stance` · 应对姿态
- xlsx：【消耗】本场战斗中，每次成功触发应对效果时获得8护甲
- ⚠ 数值/语义 compare 整卡跳过

#### `w_battle_will` · 战意觉醒
- xlsx：【消耗】本场战斗中，每次掉血会使自身获得5%增伤
- ⚠ 数值/语义 compare 整卡跳过

#### `w_heavy_armor` · 重甲强化
- xlsx：【消耗】本场战斗中，每次获得护甲时额外+20%护甲
- ⚠ 数值/语义 compare 整卡跳过
- ⚠ 曾 Actions 为空；已补 heavy_armor Permanent，需验证获甲 +20%

#### `w_unyielding` · 不屈意志
- xlsx：【消耗】在本场战斗中，当第一次血量降至25%以下时，恢复20HP
- 静态通过；**缺行为测试**

#### `w_god_descends` · 天神下凡
- xlsx：【消耗】在本场战斗中，当获得护甲时，对所有敌人造成8伤害
- ⚠ 数值/语义 compare 整卡跳过
- ⚠ 曾误写为 AOE 8 伤；已改为 god_descends 被动，需验证获甲触发

#### `w_final_bulwark` · 最终壁垒
- xlsx：【消耗】在本场战斗中，回合开始时仅清除50%护甲，而非全部
- ⚠ 数值/语义 compare 整卡跳过

#### `w_last_stand` · 背水一战
- xlsx：【消耗】本场战斗中，战士HP不会降至1以下（2回合），并获得20%增伤
- ⚠ 数值/语义 compare 整卡跳过

#### `p_plague_spread` · 瘟疫蔓延
- xlsx：【消耗】本场战斗中，当敌人因中毒受伤时，有30%概率将一半层数的该效果传染给相邻敌人（持续时间和原本保持一致）
- ⚠ 数值/语义 compare 整卡跳过

#### `p_rot_avatar` · 腐朽化身
- xlsx：【消耗】本场战斗中，所有敌人在回合开始时获得2层中毒（永久）
- ⚠ 数值/语义 compare 整卡跳过
- ⚠ 曾 Target=敌人；已改 Self + rot_avatar，需验证回合开始中毒

#### `p_anubis_avatar` · 阿努比斯化身
- xlsx：【消耗】在本场战斗中，临时获得额外50%的血量上限，50%增伤和50%强固，但接下来的两个回合法老不能出牌
- ⚠ 数值/语义 compare 整卡跳过
- ⚠ 已改为 Type=10 ApplyAnubisAvatar，需验证禁出牌 2 回合

#### `d_blood_frenzy` · 鲜血狂欢
- xlsx：【消耗】在本场战斗中，每次献祭后获得5%增伤
- ⚠ 数值/语义 compare 整卡跳过

#### `d_bloodline_legacy` · 血族传承
- xlsx：【消耗】在本场战斗中，恶魔可以拥有150%的最大HP
- ⚠ 数值/语义 compare 整卡跳过

#### `d_endless_blade` · 无尽血刃
- xlsx：【前/中/后】【献祭25%HP】造成25伤害，使用后此牌伤害在本场战斗中翻倍
- ⚠ 数值/语义 compare 整卡跳过
- ⚠ Reach 批量修正；翻倍伤害靠 PassiveCardMechanicsRules 非 status
- ⚠ 描述要求先选目标（未跑 Unity ShouldPromptForTarget）

#### `d_blood_sharing` · 分血仪式
- xlsx：【消耗】在本场战斗中，当恶魔回复HP时治疗其他我方角色30%的回复量
- ⚠ 数值/语义 compare 整卡跳过

#### `d_final_blood_ritual` · 最终鲜血仪式
- xlsx：【消耗】在本场战斗中，每当使用者触发【献祭】关键词，抽一张牌并回复5HP
- ⚠ 数值/语义 compare 整卡跳过
- ⚠ 曾误写为 Heal；已改为 final_blood_ritual 被动

#### `v_venom_sac_burst` · 毒囊破裂
- xlsx：【消耗】在本场战斗中，每当自身施加中毒，额外施加1层
- ⚠ 数值/语义 compare 整卡跳过

#### `v_immortal_shed` · 不朽蛇蜕
- xlsx：【消耗】在本场战斗中，每当自身获得中毒状态时（无论是否免疫），获得10%增伤（5回合）
- ⚠ 数值/语义 compare 整卡跳过

#### `l_psionic_body` · 灵能体
- xlsx：【消耗】在本场战斗中，我方在非战斗回合造成的伤害拥有20%增伤
- ⚠ 数值/语义 compare 整卡跳过

#### `m_queen_wrath` · 幽灵女王之怒
- xlsx：在本场战斗中，获得本场攻击牌伤害翻倍
- ⚠ 数值/语义 compare 整卡跳过

---

## 六、批量改过 Reach 的位置卡（约 90 张）

由 `fix_reach_on_assets.py` 按描述括号写入 YAML Reach。
**只保证 YAML 数字与括号一致，不保证：**
- 规划阶段是否弹出选目标
- 结算时是否打在正确站位
- 多 action 卡是否每个 action Reach 都对

| # | cardId | 名称 | Reach期望 | 备注 |
|---|--------|------|-----------|------|
| 2 | `w_basic_slash` | 基础斩击 | 1 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 6 | `w_first_strike` | 先发制人 | 1 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 7 | `w_power_cleave` | 猛力劈砍 | 1 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 8 | `w_shield_slam` | 护盾猛击 | 1 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 17 | `w_charge` | 战士冲锋 | 1 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 19 | `w_fatal_strike` | 致命打击 | 1 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 21 | `w_tactician_finisher` | 战术大师的终结技 | 1 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 22 | `w_burning_fury` | 怒火焚身 | 1 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 27 | `p_sand_ray` | 沙暴射线 | 1 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 29 | `p_sand_arrow` | 沙之箭 | 0 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 32 | `p_pharaoh_curse` | 法老诅咒 | 1 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 35 | `p_rot_touch` | 腐烂之触 | 1 | 数值/语义 compare 整卡跳过; 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 37 | `p_lifesteal` | 生命汲取 | 1 | 数值/语义 compare 整卡跳过; 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 40 | `p_curse_deepen` | 诅咒加深 | 0 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 41 | `p_undead_curse` | 亡灵诅咒 | 0 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 42 | `p_solar_judgment` | 日光审判 | 0 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 51 | `d_shadow_claw` | 暗影爪击 | 1 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 52 | `d_demon_curse` | 恶魔诅咒 | 0 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 53 | `d_devil_touch` | 恶魔之触 | 1 | 数值/语义 compare 整卡跳过; 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 56 | `d_blood_hand` | 掠血之手 | 1 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 57 | `d_blood_bite` | 鲜血撕咬 | 1 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 60 | `d_blood_tail` | 血尾贯穿 | 1 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 61 | `d_life_siphon` | 生命虹吸 | 1 | 数值/语义 compare 整卡跳过; 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 62 | `d_blood_flame` | 血焰爆发 | 1 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 64 | `d_soul_rip` | 灵魂撕裂 | 0 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 65 | `d_curse_chain` | 诅咒之链 | 0 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 67 | `d_dark_sacrifice` | 暗黑献祭 | 1 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 70 | `d_demon_lord` | 魔王降临 | 0 | 数值/语义 compare 整卡跳过; 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 71 | `d_endless_blade` | 无尽血刃 | 0 | 数值/语义 compare 整卡跳过; Reach 批量修正；翻倍伤害靠 PassiveCardMechanicsRules 非 status |
| 74 | `v_snake_bite` | 蛇牙撕咬 | 0 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 77 | `v_poison_touch` | 剧毒之触 | 0 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 79 | `v_tail_strike` | 蛇尾突袭 | 0 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 82 | `v_venom_spit` | 毒液喷吐 | 0 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 83 | `v_detonate_venom` | 引爆毒囊 | 0 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 90 | `v_python_constrict` | 巨蟒绞杀 | 0 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 99 | `l_ghost_claw` | 幽灵爪击 | 0 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 102 | `l_psionic_cannon` | 灵能炮 | 0 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 109 | `l_realm_burst` | 灵界爆发 | 1 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 110 | `l_psionic_focus` | 灵能聚集 | 1 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 116 | `l_despair_soul` | 绝望之魂 | 0 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 118 | `l_super_psionic_cannon` | 超级·无敌·灵能·巨炮 | 0 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 120 | `g_bite` | 撕咬 | 1 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 121 | `g_blood_scratch` | 嗜血抓挠 | 1 | 数值/语义 compare 整卡跳过; 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 122 | `g_throw` | 投石 | 3 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 124 | `m_slime_slam` | 黏糊撞击 | 1 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 125 | `m_slime_absorb` | 吸收 | 1 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 128 | `m_bone_slash` | 骨剑斩 | 1 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 129 | `m_bone_toss` | 投骨 | 3 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 132 | `m_bone_crush` | 骨碎斩 | 1 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 133 | `m_bone_spear` | 投掷骨矛 | 3 | 数值/语义 compare 整卡跳过; 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 134 | `m_shatter_rush` | 碎骨突袭 | 1 | 数值/语义 compare 整卡跳过; 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 136 | `g_arrow` | 箭矢 | 0 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 138 | `m_wraith_soul_strike` | 灵魂打击 | 1 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 140 | `m_soul_strike` | 灵魂打击 | 1 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 145 | `m_ogre_heavy_punch` | 重拳 | 1 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 148 | `m_ogre_combo_smash` | 连环猛击 | 1 | 数值/语义 compare 整卡跳过; 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 150 | `m_bat_claw` | 蝙蝠爪击 | 1 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 151 | `m_bat_dive` | 俯冲撕咬 | 0 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 154 | `m_bat_poison_wing` | 淬毒翼击 | 1 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 156 | `m_rat_punch` | 鼠人拳击 | 1 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 157 | `m_rat_trash` | 投掷垃圾 | 3 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 162 | `m_chain_whip` | 锁链鞭打 | 1 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 164 | `m_chain_throw` | 怨链投掷 | 0 | 数值/语义 compare 整卡跳过; 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 167 | `m_final_bind` | 终焉魂缚 | 1 | 数值/语义 compare 整卡跳过; 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 169 | `m_gargoyle_claw` | 利爪劈击 | 1 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 171 | `m_gargoyle_sunder` | 破甲俯冲 | 0 | 数值/语义 compare 整卡跳过; 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 175 | `m_spider_fang` | 毒牙刺击 | 1 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 180 | `m_spider_fatal_bind` | 致命缠杀 | 3 | 数值/语义 compare 整卡跳过; 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 181 | `m_golem_fist` | 石拳 | 1 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget）; check3 不验证 Condition 句号分段（§3.4） |
| 185 | `m_golem_quake_slam` | 山崩地裂 | 1 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 186 | `m_golem_crack_fist` | 崩裂拳 | 1 | 数值/语义 compare 整卡跳过; 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 187 | `m_tide_lance` | 枪刺 | 1 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 188 | `m_tide_charge` | 浪潮冲锋 | 0 | 数值/语义 compare 整卡跳过; 曾 Target=全体；已改单目标 Reach=0；**速度快于敌人 +8 伤未实装**（asset 误用 BonusIfTargetHpBelowFlat） |
| 193 | `m_jelly_sting` | 电刺击 | 1 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 196 | `m_bounce_sting` | 弹射蛰刺 | 1 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 197 | `m_magic_lightning` | 魔力之电 | 0 | 数值/语义 compare 整卡跳过; 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 199 | `m_paralyze_sting` | 麻痹之电 | 1 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 200 | `m_mermaid_slash` | 劈砍 | 1 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 203 | `m_wave_cleave` | 破浪斩 | 0 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 204 | `m_abyss_lash` | 深渊鞭笞 | 0 | 数值/语义 compare 整卡跳过; 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 208 | `m_piercing_tentacle` | 贯穿之触手 | 0 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 209 | `m_giant_claw` | 巨钳击 | 1 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 212 | `m_pinch_armor` | 夹断护甲 | 0 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 213 | `m_fester_claw` | 溃烂钳击 | 1 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget）; check3 不验证 Condition 句号分段（§3.4） |
| 214 | `m_phantom_slash` | 鬼魅斩击 | 1 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 215 | `m_musket_shot` | 火枪射击 | 2 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 217 | `m_plunder_strike` | 掠夺鬼击 | 0 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 221 | `m_king_bone_slash` | 骨王斩击 | 1 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 223 | `m_king_bone_spear` | 投掷骨矛 | 3 | 数值/语义 compare 整卡跳过; 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 231 | `m_queen_claw` | 幽灵爪击 | 0 | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |

---

## 七、全量 238 张逐卡表（按 xlsx 顺序）

| # | cardId | 名称 | 风险 | 标签 | 测试 | 备注 |
|---|--------|------|------|------|------|------|
| 1 | `w_author_realm_strike` | 作者境的一击 | 低 | SPECIAL豁免, 仅Catalog或无行为测试 | 测试卡 | 测试卡：Catalog 与 xlsx 故意不一致，无行为测试 |
| 2 | `w_basic_slash` | 基础斩击 | 低 | 位置括号 | CardMechanicsV2Tests + TargetPickRulesTe… | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 3 | `w_shield_block` | 举盾格挡 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 4 | `w_defensive_stance` | 防御架势 | 低 | 应对卡, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | check3 不验证 Condition 句号分段（§3.4） |
| 5 | `w_war_cry` | 战吼鼓舞 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 6 | `w_first_strike` | 先发制人 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 7 | `w_power_cleave` | 猛力劈砍 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 8 | `w_shield_slam` | 护盾猛击 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 9 | `w_strategic_retreat` | 以退为进 | 中 | PASSIVE_HANDLED, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 数值/语义 compare 整卡跳过 |
| 10 | `w_pommel_strike` | 剑柄猛击 | 低 | 应对卡, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | check3 不验证 Condition 句号分段（§3.4） |
| 11 | `w_parry_counter` | 见招拆招 | 低 | 应对卡, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | check3 不验证 Condition 句号分段（§3.4） |
| 12 | `w_taunt` | 嘲讽挑衅 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 13 | `w_iron_parry` | 铁壁弹反 | 低 | 应对卡, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | check3 不验证 Condition 句号分段（§3.4） |
| 14 | `w_respond_stance` | 应对姿态 | 中 | PASSIVE_HANDLED, 本场战斗 | BattleScopePassiveTests.RespondStance | 数值/语义 compare 整卡跳过 |
| 15 | `w_battle_will` | 战意觉醒 | 中 | PASSIVE_HANDLED, 本场战斗, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 数值/语义 compare 整卡跳过 |
| 16 | `w_heavy_armor` | 重甲强化 | 中 | PASSIVE_HANDLED, 批量修复, 本场战斗, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 数值/语义 compare 整卡跳过；曾 Actions 为空；已补 heavy_armor Permanent，需验证获甲 +20% |
| 17 | `w_charge` | 战士冲锋 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 18 | `w_guardian` | 誓死守护 | 中 | PASSIVE_HANDLED, 批量修复, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 数值/语义 compare 整卡跳过；曾 Actions 为空；已补 guard 1 回合，需验证伤害转移 |
| 19 | `w_fatal_strike` | 致命打击 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 20 | `w_blade_storm` | 剑刃风暴 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 21 | `w_tactician_finisher` | 战术大师的终结技 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 22 | `w_burning_fury` | 怒火焚身 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 23 | `w_unyielding` | 不屈意志 | 低 | 本场战斗, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 24 | `w_god_descends` | 天神下凡 | 中 | PASSIVE_HANDLED, 批量修复, 本场战斗, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 数值/语义 compare 整卡跳过；曾误写为 AOE 8 伤；已改为 god_descends 被动，需验证获甲触发 |
| 25 | `w_final_bulwark` | 最终壁垒 | 中 | PASSIVE_HANDLED, 本场战斗, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 数值/语义 compare 整卡跳过 |
| 26 | `w_last_stand` | 背水一战 | 中 | PASSIVE_HANDLED, 本场战斗, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 数值/语义 compare 整卡跳过 |
| 27 | `p_sand_ray` | 沙暴射线 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 28 | `p_bless` | 祈祷祝福 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 29 | `p_sand_arrow` | 沙之箭 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 30 | `p_sand_shield` | 沙之盾 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 31 | `p_scarab_shield` | 圣甲虫护盾 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 32 | `p_pharaoh_curse` | 法老诅咒 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 33 | `p_memory_fragment` | 记忆残片 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 34 | `p_solar_wrath` | 太阳之怒 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 35 | `p_rot_touch` | 腐烂之触 | 中 | PASSIVE_HANDLED, 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 数值/语义 compare 整卡跳过；描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 36 | `p_sand_barrier` | 沙尘结界 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 37 | `p_lifesteal` | 生命汲取 | 中 | PASSIVE_HANDLED, 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 数值/语义 compare 整卡跳过；描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 38 | `p_decree` | 法老权令 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 39 | `p_plague_spread` | 瘟疫蔓延 | 中 | PASSIVE_HANDLED, 本场战斗, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 数值/语义 compare 整卡跳过 |
| 40 | `p_curse_deepen` | 诅咒加深 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 41 | `p_undead_curse` | 亡灵诅咒 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 42 | `p_solar_judgment` | 日光审判 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 43 | `p_revive_bless` | 复活祝福 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 44 | `p_sand_spear_reforge` | 沙矛重塑 | 中 | PASSIVE_HANDLED, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 数值/语义 compare 整卡跳过 |
| 45 | `p_holy_infusion` | 神圣灌注 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 46 | `p_solar_god_wrath` | 太阳神之怒 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 47 | `p_solar_blessing` | 太阳神的庇佑 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 48 | `p_rot_avatar` | 腐朽化身 | 中 | PASSIVE_HANDLED, 批量修复, 本场战斗, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 数值/语义 compare 整卡跳过；曾 Target=敌人；已改 Self + rot_avatar，需验证回合开始中毒 |
| 49 | `p_anubis_avatar` | 阿努比斯化身 | 中 | PASSIVE_HANDLED, 批量修复, 本场战斗, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 数值/语义 compare 整卡跳过；已改为 Type=10 ApplyAnubisAvatar，需验证禁出牌 2 回合 |
| 50 | `p_holy_cycle` | 神圣轮回 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 51 | `d_shadow_claw` | 暗影爪击 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 52 | `d_demon_curse` | 恶魔诅咒 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 53 | `d_devil_touch` | 恶魔之触 | 中 | PASSIVE_HANDLED, 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 数值/语义 compare 整卡跳过；描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 54 | `d_blood_armor` | 鲜血铠甲 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 55 | `d_demon_pact` | 恶魔契约 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 56 | `d_blood_hand` | 掠血之手 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 57 | `d_blood_bite` | 鲜血撕咬 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 58 | `d_dark_mist` | 黑暗之雾 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 59 | `d_vamp_aura` | 吸血光环 | 中 | PASSIVE_HANDLED, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 数值/语义 compare 整卡跳过 |
| 60 | `d_blood_tail` | 血尾贯穿 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 61 | `d_life_siphon` | 生命虹吸 | 中 | PASSIVE_HANDLED, 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 数值/语义 compare 整卡跳过；描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 62 | `d_blood_flame` | 血焰爆发 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 63 | `d_dark_tear` | 黑暗撕裂 | 低 | 应对卡, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | check3 不验证 Condition 句号分段（§3.4） |
| 64 | `d_soul_rip` | 灵魂撕裂 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 65 | `d_curse_chain` | 诅咒之链 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 66 | `d_hell_fire` | 地狱烈焰 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 67 | `d_dark_sacrifice` | 暗黑献祭 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 68 | `d_blood_frenzy` | 鲜血狂欢 | 中 | PASSIVE_HANDLED, 本场战斗, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 数值/语义 compare 整卡跳过 |
| 69 | `d_bloodline_legacy` | 血族传承 | 中 | PASSIVE_HANDLED, 本场战斗, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 数值/语义 compare 整卡跳过 |
| 70 | `d_demon_lord` | 魔王降临 | 中 | PASSIVE_HANDLED, 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 数值/语义 compare 整卡跳过；描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 71 | `d_endless_blade` | 无尽血刃 | 中 | PASSIVE_HANDLED, 批量修复, 位置括号, 本场战斗, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 数值/语义 compare 整卡跳过；Reach 批量修正；翻倍伤害靠 PassiveCardMechanicsRules 非 status |
| 72 | `d_blood_sharing` | 分血仪式 | 中 | PASSIVE_HANDLED, 本场战斗, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 数值/语义 compare 整卡跳过 |
| 73 | `d_final_blood_ritual` | 最终鲜血仪式 | 中 | PASSIVE_HANDLED, 批量修复, 本场战斗, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 数值/语义 compare 整卡跳过；曾误写为 Heal；已改为 final_blood_ritual 被动 |
| 74 | `v_snake_bite` | 蛇牙撕咬 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 75 | `v_python_guard` | 蟒蛇守护 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 76 | `v_scale_harden` | 鳞片硬化 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 77 | `v_poison_touch` | 剧毒之触 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 78 | `v_queen_authority` | 女王威信 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 79 | `v_tail_strike` | 蛇尾突袭 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 80 | `v_poison_scale` | 毒鳞 | 低 | 应对卡, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | check3 不验证 Condition 句号分段（§3.4） |
| 81 | `v_snake_king_blessing` | 蛇王护佑 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 82 | `v_venom_spit` | 毒液喷吐 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 83 | `v_detonate_venom` | 引爆毒囊 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 84 | `v_digest_venom` | 消化剧毒 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 85 | `v_shed_skin` | 蜕皮 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 86 | `v_tongue_sense` | 蛇信感知 | 低 | 应对卡, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | check3 不验证 Condition 句号分段（§3.4） |
| 87 | `v_queen_prevention` | 女王的预防 | 低 | 应对卡, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | check3 不验证 Condition 句号分段（§3.4） |
| 88 | `v_venom_sac_burst` | 毒囊破裂 | 中 | PASSIVE_HANDLED, 本场战斗, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 数值/语义 compare 整卡跳过 |
| 89 | `v_venom_feast` | 毒裂盛宴 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 90 | `v_python_constrict` | 巨蟒绞杀 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 91 | `v_immortal_shed` | 不朽蛇蜕 | 中 | PASSIVE_HANDLED, 本场战斗, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 数值/语义 compare 整卡跳过 |
| 92 | `v_poison_feedback` | 剧毒反哺 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 93 | `v_all_snakes_heart` | 万蛇噬心 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 94 | `v_pray_ancient_god` | 祈求远古蛇神 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 95 | `v_snake_god_response` | 蛇神的回应 | 中 | PASSIVE_HANDLED, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 数值/语义 compare 整卡跳过 |
| 96 | `l_ethereal_form` | 虚化形态 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 97 | `l_void_gaze` | 空洞凝视 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 98 | `l_charge` | 蓄能 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 99 | `l_ghost_claw` | 幽灵爪击 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 100 | `l_gather_energy` | 聚能 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 101 | `l_spirit_walk` | 灵体漫步 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 102 | `l_psionic_cannon` | 灵能炮 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 103 | `l_dread_whisper` | 恐惧低语 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 104 | `l_soul_storm` | 灵魂风暴 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 105 | `l_two_realms_walker` | 两界行者 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 106 | `l_soul_devour` | 灵魂吞噬 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 107 | `l_psionic_body` | 灵能体 | 中 | PASSIVE_HANDLED, 本场战斗 | BattleScopePassiveTests.PsionicBody | 数值/语义 compare 整卡跳过 |
| 108 | `l_soul_reinforce` | 灵魂强化 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 109 | `l_realm_burst` | 灵界爆发 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 110 | `l_psionic_focus` | 灵能聚集 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 111 | `l_realm_seal` | 灵界封印 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 112 | `l_soul_elegy` | 灵魂挽歌 | 中 | PASSIVE_HANDLED, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 数值/语义 compare 整卡跳过 |
| 113 | `l_summon_card_spirit` | 召唤卡牌之灵 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 114 | `l_summon_chaos_spirit` | 召唤混乱之灵 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 115 | `l_wall_of_sighs` | 叹息之墙 | 低 | 应对卡, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | check3 不验证 Condition 句号分段（§3.4） |
| 116 | `l_despair_soul` | 绝望之魂 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 117 | `l_realm_descent` | 灵界降临 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 118 | `l_super_psionic_cannon` | 超级·无敌·灵能·巨炮 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 119 | `l_eternal_void` | 永恒虚无 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 120 | `g_bite` | 撕咬 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 121 | `g_blood_scratch` | 嗜血抓挠 | 中 | PASSIVE_HANDLED, 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 数值/语义 compare 整卡跳过；描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 122 | `g_throw` | 投石 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 123 | `m_slime_shield` | 凝胶护盾 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 124 | `m_slime_slam` | 黏糊撞击 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 125 | `m_slime_absorb` | 吸收 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 126 | `m_slime_wrap` | 粘液缠绕 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 127 | `m_bone_shield` | 举盾 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 128 | `m_bone_slash` | 骨剑斩 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 129 | `m_bone_toss` | 投骨 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 130 | `m_maim` | 致残 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 131 | `m_bone_wall` | 骨墙 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 132 | `m_bone_crush` | 骨碎斩 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 133 | `m_bone_spear` | 投掷骨矛 | 中 | PASSIVE_HANDLED, 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 数值/语义 compare 整卡跳过；描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 134 | `m_shatter_rush` | 碎骨突袭 | 中 | PASSIVE_HANDLED, 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 数值/语义 compare 整卡跳过；描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 135 | `m_raise_bones` | 唤骨 | 中 | PASSIVE_HANDLED, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 数值/语义 compare 整卡跳过 |
| 136 | `g_arrow` | 箭矢 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 137 | `m_wraith_phase` | 隐身 | 低 | 应对卡, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | check3 不验证 Condition 句号分段（§3.4） |
| 138 | `m_wraith_soul_strike` | 灵魂打击 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 139 | `g_hex` | 邪咒 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 140 | `m_soul_strike` | 灵魂打击 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 141 | `m_phase` | 隐身 | 低 | 应对卡, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | check3 不验证 Condition 句号分段（§3.4） |
| 142 | `m_advanced_hex` | 高级邪咒 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 143 | `m_soul_storm` | 灵魂风暴 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 144 | `m_soul_bind` | 灵魂束缚 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 145 | `m_ogre_heavy_punch` | 重拳 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 146 | `m_ogre_stomp` | 践踏 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 147 | `m_ogre_war_cry` | 战争怒吼 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 148 | `m_ogre_combo_smash` | 连环猛击 | 中 | PASSIVE_HANDLED, 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 数值/语义 compare 整卡跳过；描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 149 | `m_ogre_thick_hide` | 厚皮护甲 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 150 | `m_bat_claw` | 蝙蝠爪击 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 151 | `m_bat_dive` | 俯冲撕咬 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 152 | `m_bat_ambush` | 偷袭 | 低 | 应对卡, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | check3 不验证 Condition 句号分段（§3.4） |
| 153 | `m_bat_shadow_dodge` | 暗影闪避 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 154 | `m_bat_poison_wing` | 淬毒翼击 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 155 | `m_bat_night_slash` | 夜袭连斩 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 156 | `m_rat_punch` | 鼠人拳击 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 157 | `m_rat_trash` | 投掷垃圾 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 158 | `m_rat_ambush` | 偷袭 | 低 | 应对卡, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | check3 不验证 Condition 句号分段（§3.4） |
| 159 | `m_rat_morale` | 提振士气 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 160 | `m_rat_swarm_call` | 呼唤鼠群 | 中 | PASSIVE_HANDLED, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 数值/语义 compare 整卡跳过 |
| 161 | `m_rat_burrow` | 钻地逃遁 | 低 | 应对卡, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | check3 不验证 Condition 句号分段（§3.4） |
| 162 | `m_chain_whip` | 锁链鞭打 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 163 | `m_chain_grudge` | 怨气缠绕 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 164 | `m_chain_throw` | 怨链投掷 | 中 | PASSIVE_HANDLED, 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 数值/语义 compare 整卡跳过；描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 165 | `m_chain_guard` | 锁链护体 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 166 | `m_grudge_guard` | 怨气护体 | 低 | 应对卡, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | check3 不验证 Condition 句号分段（§3.4） |
| 167 | `m_final_bind` | 终焉魂缚 | 中 | PASSIVE_HANDLED, 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 数值/语义 compare 整卡跳过；描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 168 | `m_chain_recharge` | 回气 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 169 | `m_gargoyle_claw` | 利爪劈击 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 170 | `m_gargoyle_petrify` | 石化形态 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 171 | `m_gargoyle_sunder` | 破甲俯冲 | 中 | PASSIVE_HANDLED, 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 数值/语义 compare 整卡跳过；描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 172 | `m_gargoyle_empower` | 活体强化 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 173 | `m_gargoyle_counter` | 崩石反击 | 低 | 应对卡, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | check3 不验证 Condition 句号分段（§3.4） |
| 174 | `m_gargoyle_sleep_stone` | 沉睡之石 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 175 | `m_spider_fang` | 毒牙刺击 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 176 | `m_spider_silk` | 蛛丝缠绕 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 177 | `m_spider_trap` | 蛛网陷阱 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 178 | `m_spider_spray` | 剧毒喷射 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 179 | `m_spider_wrap` | 蛛网包裹 | 低 | 应对卡 | CardMechanicsV2Tests + TargetPickRulesTe… | check3 不验证 Condition 句号分段（§3.4） |
| 180 | `m_spider_fatal_bind` | 致命缠杀 | 中 | PASSIVE_HANDLED, 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 数值/语义 compare 整卡跳过；描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 181 | `m_golem_fist` | 石拳 | 低 | 位置括号, 应对卡, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget）；check3 不验证 Condition 句号分段（§3.4） |
| 182 | `m_golem_wall` | 石之壁垒 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 183 | `m_golem_quake` | 地震波 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 184 | `m_golem_unmovable` | 不动如山 | 低 | 应对卡, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | check3 不验证 Condition 句号分段（§3.4） |
| 185 | `m_golem_quake_slam` | 山崩地裂 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 186 | `m_golem_crack_fist` | 崩裂拳 | 中 | PASSIVE_HANDLED, 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 数值/语义 compare 整卡跳过；描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 187 | `m_tide_lance` | 枪刺 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 188 | `m_tide_charge` | 浪潮冲锋 | 高 | PASSIVE_HANDLED, 批量修复, 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 数值/语义 compare 整卡跳过；曾 Target=全体；已改单目标 Reach=0；**速度快于敌人 +8 伤未实装**（asset 误用 BonusIfTargetHpBelowFlat） |
| 189 | `m_water_shield` | 以水为盾 | 低 | 应对卡, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | check3 不验证 Condition 句号分段（§3.4） |
| 190 | `m_guard_stance` | 守卫姿态 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 191 | `m_tail_splash` | 扫尾泼水 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 192 | `m_final_guard` | 终焉守护 | 低 | 应对卡, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | check3 不验证 Condition 句号分段（§3.4） |
| 193 | `m_jelly_sting` | 电刺击 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 194 | `m_phase_current` | 相位电流 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 195 | `m_gel_wall` | 凝胶护壁 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 196 | `m_bounce_sting` | 弹射蛰刺 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 197 | `m_magic_lightning` | 魔力之电 | 中 | PASSIVE_HANDLED, 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 数值/语义 compare 整卡跳过；描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 198 | `m_final_summon` | 终焉召唤 | 中 | PASSIVE_HANDLED, 批量修复, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 数值/语义 compare 整卡跳过；补 StatusId=final_summon_pending |
| 199 | `m_paralyze_sting` | 麻痹之电 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 200 | `m_mermaid_slash` | 劈砍 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 201 | `m_tidal_power` | 潮汐之力 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 202 | `m_mermaid_shield` | 举盾 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 203 | `m_wave_cleave` | 破浪斩 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 204 | `m_abyss_lash` | 深渊鞭笞 | 中 | PASSIVE_HANDLED, 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 数值/语义 compare 整卡跳过；描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 205 | `m_abyss_creature_gaze` | 深渊凝视 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 206 | `m_shell_craft` | 制造外壳 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 207 | `m_corrosion_volley` | 腐蚀乱射 | 中 | PASSIVE_HANDLED, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 数值/语义 compare 整卡跳过 |
| 208 | `m_piercing_tentacle` | 贯穿之触手 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 209 | `m_giant_claw` | 巨钳击 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 210 | `m_abyss_gaze` | 深渊凝视 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 211 | `m_reforge_shell` | 重塑外壳 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 212 | `m_pinch_armor` | 夹断护甲 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 213 | `m_fester_claw` | 溃烂钳击 | 低 | 位置括号, 应对卡, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget）；check3 不验证 Condition 句号分段（§3.4） |
| 214 | `m_phantom_slash` | 鬼魅斩击 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 215 | `m_musket_shot` | 火枪射击 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 216 | `m_phantom_armor` | 鬼灵盾甲 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 217 | `m_plunder_strike` | 掠夺鬼击 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 218 | `m_plunder_cannon` | 掠夺火炮 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 219 | `m_plunder` | 劫掠 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 220 | `m_ghost_ship` | 驾驶幽灵船 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 221 | `m_king_bone_slash` | 骨王斩击 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 222 | `m_king_bone_roar` | 骨王怒吼 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 223 | `m_king_bone_spear` | 投掷骨矛 | 中 | PASSIVE_HANDLED, 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 数值/语义 compare 整卡跳过；描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 224 | `m_king_summon_workshop` | 召唤骨之王座 | 中 | PASSIVE_HANDLED, 批量修复, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 数值/语义 compare 整卡跳过；补 StatusId=bone_workshop |
| 225 | `m_king_bone_block` | 骨甲格挡 | 低 | 应对卡, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | check3 不验证 Condition 句号分段（§3.4） |
| 226 | `m_king_bone_shield` | 召唤骨盾 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 227 | `m_king_white_storm` | 白骨风暴 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 228 | `m_skull_explode` | 骷髅自爆 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 229 | `m_hp` | HP | 中 | SPECIAL豁免, PASSIVE_HANDLED | 已删除占位 | 总览表遗留占位，无 asset，已删除；数值/语义 compare 整卡跳过 |
| 230 | `m_220` | 220 | 中 | SPECIAL豁免, PASSIVE_HANDLED, Catalog+部分 | BossTraitRules + CardV09CatalogRegressio… | Boss 特性卡，逻辑在 BossTraitRules，无独立 asset；数值/语义 compare 整卡跳过 |
| 231 | `m_queen_claw` | 幽灵爪击 | 低 | 位置括号, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 描述要求先选目标（未跑 Unity ShouldPromptForTarget） |
| 232 | `m_queen_deterrence` | 女王的威慑 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 233 | `m_queen_soul_drain` | 摄魂 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 234 | `m_queen_curse` | 女王的诅咒 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 235 | `m_queen_command` | 女王的命令 | 低 | 应对卡, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | check3 不验证 Condition 句号分段（§3.4） |
| 236 | `m_queen_spirit_guard` | 灵气护体 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 237 | `m_queen_burst` | 幽灵爆发 | 低 | 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | — |
| 238 | `m_queen_wrath` | 幽灵女王之怒 | 中 | PASSIVE_HANDLED, 本场战斗, 仅Catalog或无行为测试 | CardV09CatalogRegressionTests | 数值/语义 compare 整卡跳过 |

---

## 八、建议下一步（讨论用）

1. **先手测「高 + 中」里你常玩的牌**（尤其位置、应对、本场被动）。
2. **补 check3**：应对卡 Condition 分段 parser；PASSIVE_HANDLED 缩小到「真有钩子」的子集。
3. **check7 升级**：行为测试按角色分批，不能 235 张共用 Catalog 参数化就标 OK。
4. **修 Unity Test Runner**（`UNITY_INCLUDE_TESTS` / asmdef），让 TargetPickRulesTests 真正跑起来。
5. **单卡失败就回到 xlsx 第 2 步**，改 asset/引擎后再 strict + 手测，禁止只跑 batch fix。

复现命令：`python Assets/_Project/Docs/_tools/verify_card_strict.py --card <cardId>`
