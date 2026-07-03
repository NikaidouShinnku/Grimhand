# -*- coding: utf-8 -*-
"""v0.9 sections for 战斗逻辑及机制参考.docx"""
from __future__ import annotations

import json
from pathlib import Path

CARDS_JSON = Path(__file__).resolve().parent / "_v09_cards_review.json"

MECHANISM_NOTES = {
    "铁壁弹反": "应对成功：30%减伤+100%反射。演出：敌攻完整段结束后→独立弹反段(应对者Attack→反击伤害→归位)。",
    "见招拆招": "ParryImmuneAndSlowAttacker：100%免疫+攻击者2层减速；应对步无立绘，敌攻时Defense受击。",
    "防御架势": "50%减伤登记；应对步无立绘，敌攻时Defense姿势。",
    "黑暗撕裂": "50%减伤+对攻击者永久20%破损；同上。",
    "毒鳞": "50%减伤+对攻击者3层永久中毒。",
    "女王的预防": "60%减伤；同上。",
    "终焉守护": "逗号段：50%减伤+清空能量/禁回能；句号段：8甲无条件。",
    "怨气护体": "逗号段：90%减伤；失败段：自身5层中毒。",
    "不动如山": "逗号段：90%减伤；句号段：5层强固。",
    "叹息之墙": "80%减伤+RandomAlly虚化1回合。",
    "剑柄猛击": "respond_status：抑制敌状态牌+6伤；全句须应对成功。",
    "蛇信感知": "respond_status：监视目标打状态牌时，对攻击者上3层毒。",
    "战术大师的终结技": "伤害=远征累计应对成功次数×5（RunModifier，跨场不清零）。",
    "剑刃风暴": "Target=RandomEnemy(13)；HitCount=5；CardRules 不要求选手动目标。",
    "嘲讽挑衅": "ApplyStatus taunt(1回合)+DEF×120%护甲；施加后 RefreshEnemyResolutionTargetsForTaunt 覆盖敌预掷目标。",
    "应对姿态": "被动 respond_stance：应对成功 +8 甲；BlockGained 走 snapshot，演出结束 SyncBlockFromLive 对齐 UI。",
    "致命打击": "目标本速度阶段 HitThisTurn → +50%伤。",
    "怒火焚身": "10底伤+每损失5%MaxHP+1伤。",
    "背水一战": "LastStand 2回合：HP保底1+20%增伤。",
    "神圣灌注": "下张友方牌ExecuteAll再执行一遍；X费=该牌费+1。",
    "阿努比斯化身": "本场临时+50%MaxHP且当前HP同比填充(80/80→120/120)；+50%增伤/强固；2回合不能出牌。",
    "灵能体": "非战斗时段伤害+20%：见§1.3（速度结算结束→下次确认出牌，含下回合开始中毒/灼烧tick）。",
    "灵界封印": "敌下一张牌：进弃牌堆、不耗敌能量、效果不生效；X费=该牌费+1。",
    "灵能炮": "延迟伤害：下回合开始tick时结算（属非战斗时段，吃灵能体加成）。",
    "灵魂风暴": "AOE延迟10伤，下回合开始tick。",
    "蛛网包裹": "下回合攻击者无法使用攻击牌（非全锁）。",
    "无尽血刃": "每次使用伤害×2；出牌前献祭25%HP。",
    "超级·无敌·灵能·巨炮": "【继承】100伤；回合结束留手。",
    "灵魂挽歌": "8 AOE + 远征虚化进入次数×1。",
}


def load_cards():
    if not CARDS_JSON.exists():
        return []
    return json.loads(CARDS_JSON.read_text(encoding="utf-8"))


def add_turn_loop_sections(doc, add_title, add_para, add_bullets, add_table):
    add_title(doc, "1.2 单场战斗的完整回合循环", 2)
    add_para(doc, "一场战斗 = Initialize → StartBattle → 重复「回合 N」直到 EvaluateOutcome 非 Ongoing。")
    add_para(doc, "每个「回合 N」对玩家而言 = 一次「抽牌 → 规划 → 确认 → 看演出 → （逻辑上）回合末 → 下一回合抽牌」。")

    add_title(doc, "1.2.1 六阶段总览（逻辑顺序）", 3)
    add_table(doc, ["阶段", "Phase", "何时触发", "玩家可操作"], [
        ["① 回合末", "EndOfTurn", "上次 Commit 的演出全部播完后 FlushPendingEndOfTurn", "否"],
        ["② 抽牌", "Draw", "回合末逻辑末尾自动进入", "否"],
        ["③ 规划", "Planning", "抽牌逻辑结束 BeginPlanning", "是（选牌/目标/quick_start）"],
        ["④ 速度结算", "SpeedResolve", "CommitPlan / SkipTurn 后一次性跑完", "否（PresentationLocked）"],
        ["⑤ 演出缓冲", "SpeedResolve→Pending", "④逻辑结束 EndOfTurnPending=true，动画未播完", "否"],
        ["⑥ 非战斗时段", "—", "④⑤完成后到下次 Commit 之前（含①②③）", "③内可操作"],
    ])

    add_title(doc, "1.2.2 ① 回合末（ProcessEndOfTurn）", 3)
    add_bullets(doc, [
        "触发：BattleSession.OnPresentationComplete → FlushPendingEndOfTurn（须 EndOfTurnPending）。",
        "存档上回合攻击牌（消耗品用）；小怪特质保留护甲；最终壁垒保留50%护甲。",
        "双方手牌全弃（bonus_hand 标记清除不进弃牌堆）。",
        "ProcessTurnEndStatuses：仅仍配置为 TurnEnd tick 的状态（策划：灼烧已改回合开始，见②）。",
        "ProcessEndOfTurnDurations：持续回合 -1、到期移除；TurnNumber++。",
        "遗物/天赋回合末钩子 → SetPhase(Draw) → ProcessDrawPhase → BeginPlanning。",
    ])
    add_para(doc, "演出：回合末逻辑通常 **无立绘段**；若产生 StatusTickDamage 则各成独立段（HandleStatusTick，不播 Attack/Defense 出牌）。")

    add_title(doc, "1.2.3 ② 抽牌阶段（ProcessDrawPhase）", 3)
    add_bullets(doc, [
        "EnergyRules.ApplyTurnStartRegen：首回合回满，其后 +4 封顶。",
        "StatusRules.ProcessTurnStartStatuses：中毒、灼烧（v0.9 策划：灼烧改 **回合开始** tick，每层2伤）、缠绕等。",
        "遗物/Boss/小怪/腐朽化身/延迟伤害(V09)/天赋 等 ProcessTurnStart。",
        "双方抽牌（玩家：配置抽牌数+遗物；敌人：EnemyCardsDrawnPerTurn 或同玩家）。",
        "事件：StatusTickDamage、CardDrawn、EnergyChanged、PhaseChanged。",
    ])
    add_para(doc, "演出：StatusTickDamage → 独立段（overlay + PlayHitReaction useHitPose=false + 飘字 + snapshot）；**不**走 PortraitPoseChanged。抽牌/回能 mainly UI Refresh。")

    add_title(doc, "1.2.4 ③ 规划阶段（Planning）", 3)
    add_bullets(doc, [
        "EnemyTurnPlanner.PrepareEnemyTurn：小怪/Boss 贪心选牌 → EnemyPlan.PlayQueue + EnemyIntentSlot 列表。",
        "隐藏意图：≥3 张时 index1 必藏；≥4 张时 index2 50% 藏；轮到该牌 RevealIntentIfHidden。",
        "PrerollEnemyAutoTargets：Reach 随机与意图预览一致。",
        "玩家：PlanningDraft 选牌入队、扣费、选目标；取消选牌退款。",
        "quick_start：TryResolveQuickStartCard → ResolveCardImmediately（**有** PortraitPoseChanged，当场演出，不进速度队列）。",
        "CommitPlan：Capture PresentationSnapshot → ResolveTurn → DrainEvents → 演出队列。",
    ])
    add_para(doc, "演出：规划期 BeginPlanningIdleLoops（立绘 idle GIF）；选牌 UI 即时反馈；quick_start 走标准出牌段（§12.2）。")

    add_title(doc, "1.2.5 ④ 速度结算（ResolveTurn + schedule 遍历）", 3)
    add_para(doc, "baseline = SpeedResolver 轮询；schedule = RespondResolutionPlanner 插入应对。见 1.2.6–1.2.8。")

    add_title(doc, "1.2.6 速度轮询 baseline（SpeedResolver）", 3)
    add_bullets(doc, [
        "双方 PlayQueue 按 card→owner 拆 FIFO 子队列。",
        "round=0,1,2…：每 round 所有「队列非空且存活」角色各出 1 张。",
        "同 round 按有效速度降序；同速随机。",
        "输出 ResolutionStep(actorId, cardInstanceId, roundIndex) 有序列表。",
    ])

    add_title(doc, "1.2.7 应对配对 schedule（RespondResolutionPlanner）", 3)
    add_bullets(doc, [
        "顺序扫描 baseline。遇 **敌方步** 且可触发应对 → 按 PlayerPlan 顺序插入所有 **未消耗** 的匹配应对步（RespondContext, ApplyConditionalEffects=true）。",
        "插入后 **仍执行该敌步**（除非死亡 / respond_status 压制 / 跳过）。",
        "baseline 中 **未配对** 的玩家应对步 → ApplyConditionalEffects=false（句号段 + 失败段）。",
        "respond_status 成功：SuppressedEnemyCardInstanceIds → 敌步 ResolveStep 只弃牌不 ExecuteAll。",
    ])

    add_title(doc, "1.2.8 单步类型 × 逻辑 × 演出对照表", 3)
    add_table(doc, ["步类型", "Resolve 入口", "立绘段", "效果时机", "典型演出"], [
        ["玩家攻击/状态/防御", "ResolveStep", "有 Pose", "ExecuteAll", "移中→Attack/Defense→伤害/甲/状态→归位"],
        ["玩家 quick_start", "ResolveCardImmediately", "有 Pose", "规划阶段当场", "同上，不进入 schedule"],
        ["玩家应对（配对成功）", "ResolveRespondStep", "无 Pose", "条件段→句号段", "减伤/反制嵌在 **后续敌步** 的 DamageApplied"],
        ["玩家应对（无配对）", "ResolveRespondStep", "无 Pose", "句号段+失败段", "通常仅状态/护甲事件，无出牌立绘"],
        ["敌人攻击", "ResolveStep", "有 Pose", "ExecuteAll→PendingParry?", "敌 Attack→玩家 Hit 或 Defense(应对)→弹反段(若有)"],
        ["敌人防御/状态", "ResolveStep", "有 Pose", "ExecuteAll", "敌 Defense/Attack pose→效果→归位"],
        ["敌人被 respond_status 压", "ResolveStep", "无 ExecuteAll", "弃牌", "ReactionTriggered 文本；无 pose 段则仅 UI"],
        ["见招拆招等100%免疫", "Respond 条件段", "无 Pose", "RegisterMitigation 100%", "敌攻时 HP 伤=0，Defense 姿势"],
        ["铁壁弹反反射", "Respond+ResolvePendingParries", "弹反段有 Pose", "敌攻后", "敌段结束→应对者 Attack 段→反击伤害"],
    ])

    add_title(doc, "1.2.9 敌人回合在流程中的位置", 3)
    add_para(doc, "敌人 **没有独立「敌人回合」阶段**；与玩家共用同一 speed schedule：")
    add_bullets(doc, [
        "规划期：AI 已选好 EnemyPlan，意图展示给玩家（隐藏位问号）。",
        "速度期：敌步与玩家步 **交错** 按 round+速度 轮出；不是「玩家全部出完再敌人」。",
        "同屏多怪：各怪 card 在各自 owner 队列；小怪 **共用能量池** 选牌（EnemyTurnPlanner）。",
        "Boss：通常每回合回满能量；意图排序 SortIntentsByResolutionSpeed 与 SpeedResolver 一致。",
        "轮到隐藏意图：RevealIntentIfHidden → EnemyIntentPrepared。",
        "敌攻玩家：HandleDamage 中 enemyAttackingPlayer 路径；应对成功 → Defense 非 Hit。",
        "敌步结束：ResolvePendingParriesForEnemyCard（弹反）、DefenderRespondArmRules 等。",
    ])

    add_title(doc, "1.3 非战斗时段（灵能体、延迟伤害）", 2)
    add_bullets(doc, [
        "定义：从 **本回合速度结算逻辑结束**（EndOfTurnPending 前后）到 **下次玩家 CommitPlan 之前** 的所有时段。",
        "包含：回合末弃牌、下一回合 Draw 阶段的 **回合开始状态 tick**（中毒、灼烧等）、延迟伤害结算、规划 idle。",
        " **不包含** 速度 schedule 内任意敌我卡牌 ExecuteAll / Respond 造成的效果。",
        "灵能体：此期间造成的伤害 +20%（含中毒/灼烧 tick、灵能炮/灵魂风暴等下回合开始伤害）。",
    ])

    add_title(doc, "1.4 应对文案标点（v0.9 正式）", 2)
    add_table(doc, ["区间", "触发条件", "数据层"], [
        ["第一个句号之前", "必须应对成功", "Condition ≠ None → RespondEffectExecutor"],
        ["句号之后", "本卡本回合生效即触发", "Condition = None → ExecuteUnconditionalActions"],
        ["「若应对失败…」", "无匹配敌步", "RespondArmFailed"],
    ])


def add_presentation_sections(doc, add_title, add_para, add_bullets, add_table):
    add_title(doc, "十二、战斗演出与 UI", 1)

    add_title(doc, "12.0 逻辑与演出分离", 2)
    add_bullets(doc, [
        "CommitPlan 后 ResolveTurn **一次性** 跑完；事件 batch 入队；BattlePortraitDirector 按 SplitIntoSegments 协程播放。",
        "PresentationLocked=true：禁选牌/确认；手牌/HP/护甲/脚边状态走 PresentationSnapshot 逐步 Apply。",
        "OnPresentationComplete → TryFlushPendingEndOfTurn → 可能触发 **下一回合** Draw/Planning（通常无 pose 或仅 tick 段）。",
    ])

    add_title(doc, "12.1 按阶段的演出触发", 2)
    add_table(doc, ["阶段", "产生演出的事件", "Director 行为", "UI"], [
        ["规划", "quick_start 的 Pose 段", "PlaySegment 标准出牌", "可选牌；ShowActiveCard"],
        ["规划", "无 Pose 的 Phase/Energy/Intent", "通常不播或仅 Refresh", "意图槽、手牌、能量"],
        ["速度结算", "每步 ResolveStep 的 Pose 段", "逐段 PlaySegment", "PresentationLocked；ActiveCard banner"],
        ["速度结算", "应对步（无 Pose）", "效果挂在后续敌步段内", "同上"],
        ["速度结算", "弹反 PendingParry", "敌段后追加 Attack 段", "同上"],
        ["回合末/抽牌", "StatusTickDamage", "独立段 HandleStatusTick", "Refresh snapshot"],
        ["抽牌后", "CardDrawn 等", "多数无 pose", "手牌飞入/Refresh"],
        ["规划开始", "—", "BeginPlanningIdleLoops", "idle 循环"],
    ])

    add_title(doc, "12.2 事件分段规则（BattleEventPlayback）", 2)
    add_bullets(doc, [
        "PortraitPoseChanged **开段**；PortraitIdleRestored **收段**。",
        "段内顺序消费：BlockGained、DamageApplied(batch)、StatusApplied、HealApplied、IronWallConverted…",
        "无 pose 的独立段：StatusTickDamage、HealApplied（无前置 Pose）、CharacterDied、CharacterRevived。",
        "CardResolvedStarted/Ended、PhaseChanged、EnemyIntentPrepared **不** 开 pose 段。",
    ])

    add_title(doc, "12.3 标准出牌段（玩家/敌人 ResolveStep & quick_start）", 2)
    add_table(doc, ["序", "事件", "演出"], [
        ["1", "PortraitPoseChanged", "ShowActiveCard → MoveToCenter(X) → ShowPose → AttackWindUp(攻击/状态)"],
        ["2", "效果事件", "Block overlay / Damage overlay+受击 / Status overlay+脚标延迟 / Heal"],
        ["3", "CardResolvedEnded", "逻辑"],
        ["4", "PortraitIdleRestored", "HoldPose → ReturnHome → SyncSlotLayout"],
        ["5*", "PendingParry（仅敌攻后）", "应对者 Attack 段 → 反击 Damage → IdleRestored"],
    ])

    add_title(doc, "12.4 玩家应对步（ResolveRespondStep）", 2)
    add_bullets(doc, [
        "**无** PortraitPoseChanged；ShowActiveCard 不出现应对者出牌 banner（仅 CardResolvedStarted 逻辑事件）。",
        "GainBlockFromLastDamagePercent / ApplyStatus：事件可能在应对步内产生，但 **无移中**；UI snapshot 仍更新。",
        "与敌攻配对时：玩家在敌步 DamageApplied 上看到 Defense 姿势 + 减伤飘字（非 Hit）。",
        "ReflectLastDamage：不在应对步内播反击；等敌步 ResolveStep 完整段结束后播 §12.3 序5*。",
        "ParryImmuneAndSlowAttacker：敌攻 HP 伤归零 + 攻击者上减速；敌段内 Defense 表现。",
        "respond_status 成功：敌状态步被压制 → 敌 **无** ExecuteAll pose 效果（或极短 Reaction 文本）。",
        "无配对失败：ExecuteUnconditionalActions + ExecuteFailedRespondActions；通常无 pose，仅 buff/毒等事件。",
    ])

    add_title(doc, "12.5 敌人攻击段 × 玩家应对组合", 2)
    add_table(doc, ["情况", "玩家表现", "敌人表现", "后续"], [
        ["无应对", "Hit 立绘+受击", "Attack 段完整", "—"],
        ["减伤应对(防御架势等)", "Defense+飘字，非 Hit", "Attack 段完整", "—"],
        ["见招拆招100%免疫", "Defense，HP伤0", "Attack 段完整", "攻击者 Slow 状态图标延迟"],
        ["铁壁弹反", "Defense 受击表现", "Attack 段完整", "**弹反段**：战士 Attack→敌 Hit"],
        ["剑柄猛击 respond_status", "—（应对无 pose）", "状态步被 skip", "若另有伤害段则另说"],
        ["终焉守护等句号段甲", "—", "Attack 后", "句号段甲可能在应对步或轮到该卡时"],
    ])

    add_title(doc, "12.6 状态 tick 与延迟伤害演出", 2)
    add_bullets(doc, [
        "ProcessTurnStartStatuses / ProcessTurnEndStatuses → StatusTickDamage。",
        "HandleStatusTick：status overlay → PlayHitReaction(useHitPose=false) → snapshot；**不出牌立绘**。",
        "中毒/灼烧（回合开始）：在 **下一回合 Draw 后、Planning 前** 的演出 batch 中播放。",
        "灵能炮/缠绕等延迟伤害：同样在 TurnStart tick 段；属 **非战斗时段** 伤害（灵能体加成）。",
        "StatusApplied（卡牌施加）：在 pose 段内，脚边图标 **动画结束后** ApplyFootStatusApplied。",
    ])

    add_title(doc, "12.7 AOE 与同 actor 多段伤害", 2)
    add_bullets(doc, [
        "CollectActorDamageWave：同 actor 连续 DamageApplied → 并行 overlay → 并行受击反应。",
        "Damage wave gap：Death/Block/Heal/Status 可插在多段伤害中间。",
    ])

    add_title(doc, "12.8 规划期 idle 与死亡", 2)
    add_bullets(doc, [
        "Planning 且 !PresentationLocked → BeginPlanningIdleLoops。",
        "CharacterDied：PlayDeathSequence → snapshot MarkDead；段可插在 pose 段内或独立。",
        "演出结束 ForceSettleHome + SyncBlockFromLive（非 ClearAllBlock）+ Refresh → 若 Planning 再 idle。",
        "护甲 chip：UnitStatsRowView 读 PresentationSnapshot._block；脚边 CombatantFootStatusIconsView 仅 status，不含 Block。",
    ])


def add_card_catalog(doc, add_title, add_para, add_bullets, add_table):
    cards = load_cards()
    add_title(doc, "十七、全卡牌机制速查（v0.9 · 118 张）", 1)
    add_para(doc, "效果原文以总览表为准；下列为逻辑/演出备注。")
    by_char = {}
    for c in cards:
        by_char.setdefault(c["char"], []).append(c)
    for idx, char in enumerate(["战士", "法老", "恶魔", "毒蛇女王", "巫妖女王"], start=1):
        subset = by_char.get(char, [])
        if not subset:
            continue
        add_title(doc, f"17.{idx} {char}（{len(subset)} 张）", 2)
        rows = []
        for c in subset:
            note = MECHANISM_NOTES.get(c["name"], infer_simple_mechanism(c))
            rows.append((c["name"], c["cost"], c["type"], c["effect"], note))
        add_table(doc, ["名称", "费", "类型", "效果（原文）", "逻辑/演出备注"], rows)


def infer_simple_mechanism(card):
    eff = card.get("effect", "")
    typ = card.get("type", "")
    if "【应对攻击】" in eff:
        return "parry；句号前须成功；通常无应对立绘，嵌敌攻段 Defense/弹反。"
    if "【应对状态】" in eff:
        return "respond_status；监视目标；成功抑制敌状态步。"
    if "【应对防御】" in eff:
        return "respond_defense；监视目标打防。"
    if "【快速启动】" in eff:
        return "规划阶段立即结算，有 Pose，不进 schedule。"
    if "【AOE】" in eff:
        return "AllEnemies；多目标 Damage wave 并行受击。"
    if "【消耗】" in eff:
        return "exhaust。"
    if "【献祭" in eff:
        return "SelfDamageFlat。"
    if typ == "攻击":
        return "DealDamage + Reach。"
    if typ == "防御":
        return "GainBlock / 应对。"
    return "EffectActionExecutor 标准管线。"


def add_confirmed_rulings(doc, add_title, add_para, add_bullets, add_table):
    add_title(doc, "十八、v0.9 已定稿机制（策划裁决）", 1)
    add_table(doc, ["主题", "规则"], [
        ["非战斗时段", "速度结算结束 → 下次 Commit 之前；含回合开始中毒/灼烧 tick"],
        ["灵能体", "非战斗时段造成的伤害 +20%"],
        ["灼烧", "回合 **开始** tick，每层 2 伤（与中毒同窗口）"],
        ["战术大师终结技", "伤害 = **远征累计** 应对成功次数 ×5，跨战斗场不清零"],
        ["灵界封印", "敌下一张：进弃牌、不耗能量、效果不生效；X费 = 该牌费用 +1"],
        ["阿努比斯化身", "临时 +50% MaxHP，当前 HP 同比上升（80/80→120/120；40/80→80/120）；战斗结束消失"],
        ["铁壁弹反演出", "敌攻动画完整播完后，应对者 Attack 段反击"],
        ["蛛网包裹", "下回合无法使用 **攻击牌**  only"],
    ])
    add_para(doc, "代码待对齐项见 §十四。")


def patch_keyword_burn(keywords):
    """策划裁决：灼烧改回合开始。"""
    out = []
    for k, d in keywords:
        if "灼烧" in k:
            d = "回合开始每层造成2伤害"
        out.append((k, d))
    return out
