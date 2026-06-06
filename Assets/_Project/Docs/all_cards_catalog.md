# Grimhand 全卡牌效果清单

共 **64** 张 `.asset`（正式玩家牌 30 张 + 旧 Demo 牌 6 张 + 敌人技能 26 张）

**修改方式**
- 玩家 30 张：`Scripts/Content/Editor/BalanceV2ContentGenerator.cs`
- 敌人技能：`Scripts/Content/Editor/MonsterContentGenerator.cs`
- 改完后 Unity：**Grimhand → Content → Generate Demo ScriptableObjects**
- 或直接编辑 `Assets/_Project/Data/Cards/Card_*.asset`

**威力公式**：攻击牌 = 攻击×倍率%+固定值；护甲牌 = 防御×倍率%+固定值（见 `balance_reference.md`）

## 一、玩家正式牌（30 张）

| # | ID | 名称 | 费 | 类型 | 关键词 | 效果（动作序列） | 资源文件 |
|---|-----|------|-----|------|--------|------------------|----------|
| 1 | `w_unyielding` | 不屈意志 | 0 | Status | exhaust | 对自身施加状态「unyielding」×1，永久 | `Card_w_unyielding.asset` |
| 2 | `w_basic_slash` | 基础斩击 | 1 | Attack | — | 对默认敌人造成攻击×80%+3伤害 | `Card_w_basic_slash.asset` |
| 3 | `w_defensive_stance` | 防御架势 | 1 | Defense | parry | 【应对攻击】 获得所受伤害50%的护甲（应对减伤） | `Card_w_defensive_stance.asset` |
| 4 | `w_shield_block` | 举盾格挡 | 1 | Defense | — | 自身获得防御×80%+2护甲 | `Card_w_shield_block.asset` |
| 5 | `w_war_cry` | 战吼鼓舞 | 1 | Status | — | 对友前排施加状态「attack_up」×3，1回合 → 对友中排施加状态「attack_up」×3，1回合 → 对友后排施加状态「attack_up」×3，1回合 | `Card_w_war_cry.asset` |
| 6 | `w_guardian` | 誓死守护 | 2 | Defense | — | 对自身施加状态「guard」×1，1回合 | `Card_w_guardian.asset` |
| 7 | `w_iron_parry` | 铁壁弹反 | 2 | Defense | parry | 【应对攻击】 获得所受伤害30%的护甲（应对减伤） → 【应对攻击】 反射100%所受伤害给攻击者 | `Card_w_iron_parry.asset` |
| 8 | `w_power_cleave` | 猛力劈砍 | 2 | Attack | — | 对默认敌人造成攻击×120%+5伤害；目标HP<50%时额外+10威力 | `Card_w_power_cleave.asset` |
| 9 | `w_taunt` | 嘲讽挑衅 | 2 | Defense | — | 对自身施加状态「taunt」×1，1回合 → 自身获得防御×120%+0护甲 | `Card_w_taunt.asset` |
| 10 | `w_charge` | 战士冲锋 | 3 | Attack | — | 对默认敌人造成攻击×160%+8伤害；无视DEF50% | `Card_w_charge.asset` |
| 11 | `w_fatal_strike` | 致命打击 | 3 | Attack | — | 对默认敌人造成攻击×180%+6伤害；本回合已受击+50%伤害 | `Card_w_fatal_strike.asset` |
| 12 | `p_bless` | 祈祷祝福 | 1 | Status | — | 对前排队友(需点选)恢复攻击×100%+2HP | `Card_p_bless.asset` |
| 13 | `p_sand_ray` | 沙暴射线 | 1 | Attack | — | 对默认敌人造成攻击×80%+3伤害 | `Card_p_sand_ray.asset` |
| 14 | `p_scarab_shield` | 圣甲虫护盾 | 1 | Defense | — | 前排队友(需点选)获得防御×120%+0护甲 | `Card_p_scarab_shield.asset` |
| 15 | `p_decree` | 法老权令 | 2 | Status | — | 下回合抽2张 → 对前排队友(需点选)施加状态「attack_up」×3，1回合 → 对前排队友(需点选)施加状态「defense_up」×2，1回合 | `Card_p_decree.asset` |
| 16 | `p_lifesteal` | 生命汲取 | 2 | Attack | — | 对默认敌人造成攻击×100%+4伤害；吸血50% | `Card_p_lifesteal.asset` |
| 17 | `p_sand_barrier` | 沙尘结界 | 2 | Defense | — | 友前排获得防御×100%+0护甲 → 友中排获得防御×100%+0护甲 → 友后排获得防御×100%+0护甲 | `Card_p_sand_barrier.asset` |
| 18 | `p_solar_wrath` | 太阳之怒 | 2 | Attack | aoe | 对全体敌人造成攻击×70%+3伤害；任意站位 | `Card_p_solar_wrath.asset` |
| 19 | `p_revive_bless` | 复活祝福 | 3 | Status | exhaust | 对前排队友(需点选)施加状态「revive_blessing」×1，永久 | `Card_p_revive_bless.asset` |
| 20 | `p_undead_curse` | 亡灵诅咒 | 3 | Attack | poison | 对默认敌人造成攻击×120%+6伤害；任意站位 → 对默认敌人施加状态「necrotic_poison」×1，3回合 | `Card_p_undead_curse.asset` |
| 21 | `p_solar_judgment` | 太阳审判 | 4 | Attack | — | 对默认敌人造成攻击×200%+10伤害；任意站位 | `Card_p_solar_judgment.asset` |
| 22 | `d_devil_touch` | 恶魔之触 | 1 | Attack | — | 对默认敌人造成攻击×50%+2伤害；吸血100% | `Card_d_devil_touch.asset` |
| 23 | `d_shadow_claw` | 暗影爪击 | 1 | Attack | — | 对默认敌人造成攻击×80%+3伤害 | `Card_d_shadow_claw.asset` |
| 24 | `d_vamp_aura` | 吸血光环 | 1 | Status | — | 对自身施加状态「vamp_aura」×30，1回合 | `Card_d_vamp_aura.asset` |
| 25 | `d_blood_flame` | 血焰爆发 | 2 | Attack | sacrifice | 对自身造成8点伤害 → 对默认敌人造成攻击×130%+8伤害 | `Card_d_blood_flame.asset` |
| 26 | `d_blood_tail` | 血尾贯穿 | 2 | Attack | — | 对默认敌人造成攻击×100%+3伤害；后方溅射80% | `Card_d_blood_tail.asset` |
| 27 | `d_curse_chain` | 诅咒之链 | 2 | Attack | — | 对默认敌人造成攻击×100%+3伤害；任意站位 → 对默认敌人施加状态「attack_down」×3，2回合 | `Card_d_curse_chain.asset` |
| 28 | `d_demon_pact` | 恶魔契约 | 2 | Status | sacrifice | 对自身造成5点伤害 → 下回合抽2张 → 对自身施加状态「attack_up」×3，1回合 | `Card_d_demon_pact.asset` |
| 29 | `d_soul_rip` | 灵魂撕裂 | 2 | Attack | — | 对默认敌人造成攻击×80%+4伤害；无视DEF100%，任意站位 | `Card_d_soul_rip.asset` |
| 30 | `d_dark_sacrifice` | 暗黑献祭 | 3 | Attack | sacrifice | 对自身造成15点伤害 → 对默认敌人造成攻击×170%+12伤害 | `Card_d_dark_sacrifice.asset` |
| 31 | `d_hell_fire` | 地狱烈焰 | 3 | Attack | aoe、sacrifice | 对自身造成8点伤害 → 对全体敌人造成攻击×100%+5伤害；任意站位 | `Card_d_hell_fire.asset` |
| 32 | `d_demon_lord` | 魔王降临 | 4 | Attack | sacrifice | 对自身造成20点伤害 → 对默认敌人造成攻击×200%+15伤害；击杀回复30HP，任意站位 | `Card_d_demon_lord.asset` |

## 二、旧 Demo 牌（未进正式牌组，可删）

| # | ID | 名称 | 费 | 类型 | 关键词 | 效果（动作序列） | 资源文件 |
|---|-----|------|-----|------|--------|------------------|----------|
| 1 | `k_strike` | 重击 | 1 | Attack | melee | 对默认敌人造成攻击×100%+8伤害 | `Card_k_strike.asset` |
| 2 | `k_parry` | 弹反 | 2 | Defense | parry | 【应对攻击】 反射50%所受伤害给攻击者 → 【应对攻击】 下回合抽200张 | `Card_k_parry.asset` |
| 3 | `k_slash` | 斩击 | 2 | Attack | melee | 对默认敌人造成攻击×100%+14伤害 | `Card_k_slash.asset` |
| 4 | `r_slow` | 缚足 | 1 | Status | slow、slot | 对敌后排施加状态「slow」×1，2回合 | `Card_r_slow.asset` |
| 5 | `r_far_shot` | 远射 | 2 | Attack | far_shot | 对默认敌人造成攻击×100%+10伤害；任意站位，后排威力70% | `Card_r_far_shot.asset` |
| 6 | `r_pierce` | 贯射 | 2 | Attack | pierce、melee | 对默认敌人造成攻击×100%+11伤害；后方溅射80% | `Card_r_pierce.asset` |
| 7 | `r_snipe` | 狙击 | 2 | Attack | snipe | 对默认敌人造成攻击×100%+15伤害；任意站位 | `Card_r_snipe.asset` |

## 三、敌人技能牌（26 张）

| # | ID | 名称 | 费 | 类型 | 关键词 | 效果（动作序列） | 资源文件 |
|---|-----|------|-----|------|--------|------------------|----------|
| 1 | `g_scratch` | 抓挠 | 1 | Attack | melee | 对默认敌人造成攻击×100%+5伤害 | `Card_g_scratch.asset` |
| 2 | `g_throw` | 投石 | 1 | Attack | far_shot | 对敌后排造成攻击×100%+4伤害；任意站位 | `Card_g_throw.asset` |
| 3 | `g_lunge` | 猛扑 | 2 | Attack | melee | 对默认敌人造成攻击×100%+10伤害 | `Card_g_lunge.asset` |
| 4 | `m_bolt` | 魔弹 | 1 | Attack | melee | 对默认敌人造成攻击×100%+7伤害 | `Card_m_bolt.asset` |
| 5 | `m_poison` | 毒云 | 2 | Status | poison | 对默认敌人施加状态「poison」×10，永久 | `Card_m_poison.asset` |
| 6 | `g_wither` | 虚弱 | 1 | Status | slow | 对默认敌人施加状态「slow」×1，2回合 | `Card_g_wither.asset` |
| 7 | `m_bone_shield` | 举盾 | 1 | Defense | guard | 自身获得8点护甲 | `Card_m_bone_shield.asset` |
| 8 | `m_bone_slash` | 骨剑斩 | 1 | Attack | melee | 对默认敌人造成攻击×100%+6伤害 | `Card_m_bone_slash.asset` |
| 9 | `m_bone_toss` | 投骨 | 2 | Attack | far_shot | 对默认敌人造成攻击×100%+8伤害；任意站位 | `Card_m_bone_toss.asset` |
| 10 | `g_bite` | 撕咬 | 1 | Attack | melee | 对默认敌人造成攻击×100%+7伤害 | `Card_g_bite.asset` |
| 11 | `m_bone_crush` | 骨碎斩 | 2 | Attack | melee | 对默认敌人造成攻击×100%+12伤害 | `Card_m_bone_crush.asset` |
| 12 | `m_bone_wall` | 骨墙 | 2 | Defense | guard | 自身获得15点护甲 | `Card_m_bone_wall.asset` |
| 13 | `m_raise_bones` | 唤骨 | 3 | Status | summon | 对自身施加状态「slow」×1，2回合 | `Card_m_raise_bones.asset` |
| 14 | `m_slime_shield` | 凝胶护盾 | 1 | Defense | guard | 自身获得防御×100%+6护甲 | `Card_m_slime_shield.asset` |
| 15 | `m_slime_slam` | 黏糊撞击 | 1 | Attack | melee | 对默认敌人造成攻击×100%+4伤害 | `Card_m_slime_slam.asset` |
| 16 | `m_slime_absorb` | 吸收 | 2 | Attack | melee | 对默认敌人造成攻击×100%+5伤害 → 对自身恢复4HP | `Card_m_slime_absorb.asset` |
| 17 | `m_slime_split` | 分裂 | 2 | Status | summon | 对自身施加状态「slow」×1，1回合 | `Card_m_slime_split.asset` |
| 18 | `g_arrow` | 箭矢 | 1 | Attack | far_shot | 对默认敌人造成攻击×100%+8伤害；任意站位，后排威力80% | `Card_g_arrow.asset` |
| 19 | `m_phase` | 隐身 | 1 | Defense | guard | 自身获得5点护甲 | `Card_m_phase.asset` |
| 20 | `m_soul_strike` | 灵魂打击 | 1 | Attack | melee | 对默认敌人造成攻击×100%+7伤害 | `Card_m_soul_strike.asset` |
| 21 | `g_hex` | 邪咒 | 2 | Status | poison | 对默认敌人施加状态「poison」×5，永久 | `Card_g_hex.asset` |
| 22 | `g_aim` | 瞄准 | 2 | Attack | snipe | 对默认敌人造成攻击×100%+14伤害；任意站位 | `Card_g_aim.asset` |
| 23 | `m_curse` | 诅咒 | 2 | Status | slow | 对默认敌人施加状态「slow」×2，2回合 | `Card_m_curse.asset` |
| 24 | `m_void` | 虚无 | 2 | Defense | guard | 自身获得12点护甲 | `Card_m_void.asset` |
| 25 | `m_soul_storm` | 灵魂风暴 | 3 | Attack | aoe | 对全体敌人造成攻击×100%+10伤害 | `Card_m_soul_storm.asset` |
