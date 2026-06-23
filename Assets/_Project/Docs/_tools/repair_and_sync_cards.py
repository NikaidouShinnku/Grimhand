#!/usr/bin/env python3
"""修复损坏的 Card YAML、从 Excel 同步数值/关键词，并创建缺失卡牌资产。"""
from __future__ import annotations

import json
import re
import sys
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
JSON_PATH = ROOT / "Docs" / "_excel_authoritative.json"
CARDS_DIR = ROOT / "Data" / "Cards"
DESC_CS = ROOT / "Scripts" / "Content" / "CardDescriptionCatalog.cs"
SCRIPT_GUID = "d1e8e1f9605a6fb4fa4e4fa5586de5ee"

# Excel 名称 → 已有资产 CardId（避免重复 DisplayName 或迁移别名写错文件）
CARD_ID_OVERRIDE: dict[str, str] = {
    "日光审判": "p_solar_judgment",
    "终焉魂缚": "m_final_bind",
    "终焉缚魂": "m_final_bind",
}

# 已废弃的迁移别名（勿再指向错误 DisplayName）
NAME_ALIASES = {
    "终焉魂缚": "终焉缚魂",
    "利爪劈击": "利爪斩击",
    "破甲俯冲": "破甲冲锋",
}

CARD_TYPE_OVERRIDE = {"沙矛重塑": 2}

FULL_REBUILD_NAMES = {"沙矛重塑", "呼唤鼠群", "回气", "致命缠杀"}

CARD_OWNER = {
    "沙矛重塑": "char_mage",
    "呼唤鼠群": "char_rat",
    "回气": "char_chain_wraith",
    "致命缠杀": "char_spider_lady",
}

PLAYER_OWNER = {"战士": "char_knight", "法老": "char_mage", "恶魔": "char_ranger"}
MONSTER_CHAR = {
    "鼠人": "char_rat",
    "锁链怨灵": "char_chain_wraith",
    "石像鬼": "char_gargoyle",
    "蜘蛛贵妇": "char_spider_lady",
    "石傀儡": "char_stone_golem",
    "哥布林": "char_goblin",
    "史莱姆": "char_slime",
    "骷髅兵": "char_skeleton",
    "骷髅精英": "char_skeleton_elite",
    "幽灵": "char_wraith",
    "幽灵精英": "char_wraith_elite",
    "绿皮巨魔": "char_ogre",
    "巨翼蝙蝠": "char_bat",
    "骷髅王": "char_skeleton_king",
    "共享骷髅王": "char_skeleton_king",
    "易爆骷髅头": "char_explosive_skull",
    "幽灵女王": "char_ghost_queen",
    "踏潮守卫": "char_seahorse_guard",
    "水母海巫": "char_jellyfish_caster",
    "人鱼战士": "char_mermaid_warrior",
    "腐化蟹": "char_corrupted_crab",
    "深渊怪物": "char_abyss_creature",
    "幽灵海盗船长": "char_phantom_captain",
}

MONSTER_CHAR_ALIASES = {
    "腐蚀蟹": "腐化蟹",
    "鬼灵海盗船长": "幽灵海盗船长",
    "精英幽灵": "幽灵精英",
}


def resolve_monster_owner(display: str, monster_char: dict[str, str]) -> str:
    if display in monster_char:
        return monster_char[display]
    alias = MONSTER_CHAR_ALIASES.get(display)
    if alias and alias in monster_char:
        return monster_char[alias]
    if display == "共享骷髅王":
        return "char_explosive_skull"
    return monster_char.get(display, "char_rat")


CARD_ID_BY_OWNER: dict[tuple[str, str], str] = {
    ("char_skeleton", "举盾"): "m_bone_shield",
    ("char_mermaid_warrior", "举盾"): "m_mermaid_shield",
    ("char_bat", "偷袭"): "m_bat_ambush",
    ("char_rat", "偷袭"): "m_rat_ambush",
    ("char_skeleton_elite", "投掷骨矛"): "m_bone_spear",
    ("char_skeleton_king", "投掷骨矛"): "m_king_bone_spear",
    ("char_wraith", "隐身"): "m_wraith_phase",
    ("char_wraith_elite", "隐身"): "m_phase",
    ("char_wraith", "灵魂打击"): "m_wraith_soul_strike",
    ("char_wraith_elite", "灵魂打击"): "m_soul_strike",
    ("char_abyss_creature", "深渊凝视"): "m_abyss_creature_gaze",
    ("char_corrupted_crab", "深渊凝视"): "m_abyss_gaze",
}

CARD_TYPE = {"攻击": 0, "防御": 1, "状态": 2}
RARITY = {"白": 0, "绿": 1, "蓝": 2, "紫": 3, "橙": 4, "橙/金": 4}  # 蓝=SuperRare(2), 紫=Epic(3), 数值越大越稀有

KW_ID_MAP = {
    "消耗": "exhaust",
    "献祭": "sacrifice",
    "应对攻击": "parry",
    "应对防御": "respond_defense",
    "应对状态": "respond_status",
    "AOE": "aoe",
    "中毒": "poison",
    "灼烧": "burn",
    "减速": "slow",
    "污染": "polluted",
    "召唤": "summon",
    "自毁": "self_destruct",
    "额外手牌": "bonus_hand",
    "虚化": "ethereal",
    "X": "x_cost",
}

RE_BRACKET = re.compile(r"【([^】]+)】")
RE_DAMAGE = re.compile(r"造成\s*(\d+)\s*点?伤害")
RE_BLOCK = re.compile(r"(?:获得|添加|为.*?添加|各获得)\s*(\d+)\s*(?:点)?护甲")
RE_HEAL = re.compile(r"(?:治疗|回复)\s*(?:一名队友|自己|等量)?\s*(\d+)\s*HP", re.I)
RE_HEAL_PCT = re.compile(r"回复\s*(\d+)%\s*HP")
RE_SACRIFICE_HP = re.compile(r"献祭\s*(\d+)\s*HP|扣除自己\s*(\d+)%\s*HP", re.I)
RE_DRAW = re.compile(r"抽\s*(\d+)\s*张")
RE_ATTACK_CARD_DMG = re.compile(r"攻击牌[+＋](\d+)\s*伤害")
RE_ALLY_BUFF_PAIR = re.compile(r"攻击牌伤害[+＋](\d+).*防御牌护甲[+＋](\d+)")
RE_ALLY_PICK = re.compile(r"指定一名队友|为一名队友|给一名队友|治疗一名队友")

# EffectTarget 枚举值（与 EffectEnums.cs 一致）
T_SELF = 1
T_FRONT_ALLY = 2
T_DEFAULT_ENEMY = 0
T_ALL_ENEMIES = 12
T_RANDOM_ENEMY = 11
T_ALLY_SLOTS = (9, 10, 11)  # AllyFrontSlot, AllyMiddleSlot, AllyBackSlot
REACH_ANY = 0
REACH_FRONT_MID = 1
T_LAST_ACTOR = 4
CONDITION_ATTACK = 1  # LastActionAttackOnSelf

RE_RESPOND_REDUCE = re.compile(r"获得(\d+)%减伤")
RE_RESPOND_REFLECT = re.compile(r"反射(\d+)%伤害")
RE_RESPOND_COUNTER = re.compile(r"对攻击者造成(\d+)(?:点?)?(?:反击)?伤害")

CARD_ACTION_OVERRIDES: dict[str, list[dict]] = {
    "铁壁弹反": [
        {"Type": 9, "Target": T_SELF, "Value": 30, "Condition": CONDITION_ATTACK},
        {"Type": 8, "Target": T_LAST_ACTOR, "Value": 100, "Condition": CONDITION_ATTACK},
    ],
    "防御架势": [
        {"Type": 9, "Target": T_SELF, "Value": 50, "Condition": CONDITION_ATTACK},
    ],
    "法老权令": [
        {"Type": 7, "Target": T_SELF, "Value": 2},
        {"Type": 3, "Target": T_FRONT_ALLY, "StatusId": "attack_up", "Stacks": 3, "Duration": 1, "Reach": REACH_ANY},
        {"Type": 3, "Target": T_FRONT_ALLY, "StatusId": "defense_up", "Stacks": 3, "Duration": 1, "Reach": REACH_ANY},
    ],
    "圣甲虫护盾": [
        {"Type": 1, "Target": T_FRONT_ALLY, "Value": 8, "Reach": REACH_ANY},
    ],
    "复活祝福": [
        {"Type": 3, "Target": T_FRONT_ALLY, "StatusId": "revive_blessing", "Stacks": 1, "Duration": -1, "Reach": REACH_ANY},
    ],
    "祈祷祝福": [
        {"Type": 2, "Target": T_FRONT_ALLY, "Value": 10, "Reach": REACH_ANY},
    ],
    "沙尘结界": [
        {"Type": 1, "Target": 9, "Value": 6, "Reach": REACH_ANY},
        {"Type": 1, "Target": 10, "Value": 6, "Reach": REACH_ANY},
        {"Type": 1, "Target": 11, "Value": 6, "Reach": REACH_ANY},
    ],
    "战吼鼓舞": [
        {"Type": 3, "Target": 9, "StatusId": "attack_up", "Stacks": 3, "Duration": 1},
        {"Type": 3, "Target": 10, "StatusId": "attack_up", "Stacks": 3, "Duration": 1},
        {"Type": 3, "Target": 11, "StatusId": "attack_up", "Stacks": 3, "Duration": 1},
    ],
    "太阳神的庇佑": [
        {"Type": 1, "Target": 9, "Value": 3, "Reach": REACH_ANY},
        {"Type": 1, "Target": 10, "Value": 3, "Reach": REACH_ANY},
        {"Type": 1, "Target": 11, "Value": 3, "Reach": REACH_ANY},
    ],
    "血尾贯穿": [
        {
            "Type": 0, "Target": T_DEFAULT_ENEMY, "Value": 14, "Reach": REACH_FRONT_MID,
            "SplashBehindTarget": 1, "SplashPowerPercent": 80,
        },
    ],
    "以水为盾": [
        {"Type": 9, "Target": T_SELF, "Value": 60, "Condition": CONDITION_ATTACK},
        {
            "Type": 3, "Target": T_LAST_ACTOR, "StatusId": "slow", "Stacks": 3,
            "Duration": -1, "Condition": CONDITION_ATTACK,
        },
    ],
    "钻地逃遁": [
        {"Type": 9, "Target": T_SELF, "Value": 70, "Condition": CONDITION_ATTACK},
        {
            "Type": 3, "Target": T_LAST_ACTOR, "StatusId": "slow", "Stacks": 1,
            "Duration": 2, "Condition": CONDITION_ATTACK,
        },
    ],
    "终焉魂缚": [
        {
            "Type": 3, "Target": T_DEFAULT_ENEMY, "StatusId": "poison", "Stacks": 15,
            "Duration": -1, "Reach": REACH_FRONT_MID,
        },
    ],
    "剑柄猛击": [
        {"Type": 0, "Target": T_DEFAULT_ENEMY, "Value": 6, "Reach": REACH_FRONT_MID},
    ],
    "怨链投掷": [
        {
            "Type": 0, "Target": T_DEFAULT_ENEMY, "Value": 20, "Reach": REACH_ANY,
            "BonusIfTargetHasStatusId": "slow", "BonusIfTargetHasStatusFlat": 6,
        },
    ],
    "太阳神之怒": [],
    "太阳神的庇佑": [],
}

# 选手动选敌时的 Reach（0=【前/中/后】，1=【前/中】）
CARD_REACH_OVERRIDE: dict[str, int] = {
    "亡灵诅咒": REACH_ANY,
    "日光审判": REACH_ANY,
    "太阳审判": REACH_ANY,
    "灵魂撕裂": REACH_ANY,
    "诅咒之链": REACH_ANY,
    "魔王降临": REACH_ANY,
    "无尽血刃": REACH_ANY,
    "血尾贯穿": REACH_FRONT_MID,
}


def apply_reach_overrides(name: str, actions: list[dict]) -> None:
    reach = CARD_REACH_OVERRIDE.get(name)
    if reach is None:
        return
    for act in actions:
        if act.get("Target") in (T_DEFAULT_ENEMY, T_RANDOM_ENEMY) and act.get("Type") in (0, 3):
            act["Reach"] = reach

RE_IGNORE_BLOCK = re.compile(r"无视(?:目标)?\s*(\d+)%?\s*护甲")
RE_HIT_COUNT = re.compile(r"重复\s*(\d+)\s*次")
RE_LIFESTEAL = re.compile(r"回复(?:造成)?伤害\s*(\d+)%")
RE_POISON = re.compile(r"中毒\s*[×x]\s*(\d+)|(\d+)\s*层中毒")
RE_SLOW = re.compile(r"减速\s*[×x]\s*(\d+)|(\d+)\s*层减速")
RE_SPLASH_PCT = re.compile(r"(?:身后|身后位置).*?(\d+)%|(\d+)%.*?(?:身后|身后位置)")
RE_APPLY_SLOW = re.compile(r"(?:获得|施加|附加).*减速|减速\s*[×x]\s*(\d+)|(\d+)\s*层减速")
RE_SLOW_BONUS = re.compile(r"若目标(?:同时)?处于减速(?:状态)?[,，]?\s*额外造成\s*(\d+)")

ACTION_TAIL = """    ScaleWithAttack: 0
    ScaleWithDefense: 0
    AttackScalePercent: 100
    DefenseScalePercent: 100
    Condition: 0
    Reach: 1
    SplashBehindTarget: 0
    SplashPowerPercent: 100
    BackRowPowerPercent: 100
    IgnoreDefPercent: 0
    BonusIfTargetHpBelowPercent: 0
    BonusIfTargetHpBelowFlat: 0
    BonusIfTargetHitThisTurnPercent: 0
    BonusIfTargetHasStatusId: 
    BonusIfTargetHasStatusFlat: 0
    LifestealPercent: 0
    HealMaxHpPercent: 0
    OnKillHealAmount: 0
    HitCount: 1
    AlternateAttackScalePercent: 0
    AlternateValue: 0
    UseAlternateIfTargetHasDebuff: 0
    AlternateAttackScaleIfActorUsedAttack: 0
    AlternateValueIfActorUsedAttack: 0
    DamageMultiplierPercentIfRespondArmed: 100
    SelfDamageFlat: 0
    RepeatPerEnemyAttackCardThisTurn: 0
    FallbackBlockDefenseScalePercent: 100
    FallbackBlockValue: 0
    SummonCharacterId: 
    GrantInvulnerableOnRespondArm: 0
    LifestealUnblockedOnly: 0"""


def safe_int(val, default=1) -> int:
    try:
        return int(float(val))
    except (TypeError, ValueError):
        return default


def decode_display(raw: str) -> str:
    if "\\u" in raw:
        try:
            return raw.encode("utf-8").decode("unicode_escape")
        except UnicodeDecodeError:
            pass
    return raw


def encode_display(name: str) -> str:
    if all(ord(c) < 128 for c in name):
        return name
    return "".join(f"\\u{ord(c):04X}" for c in name)


def slugify(name: str, prefix: str) -> str:
    manual = {
        "沙矛重塑": "p_sand_spear_reforge",
        "呼唤鼠群": "m_rat_swarm_call",
        "回气": "m_chain_recharge",
        "致命缠杀": "m_spider_fatal_bind",
    }
    if name in manual:
        return manual[name]
    base = re.sub(r"[^\w]", "_", name.lower())
    return f"{prefix}_{base}" if base else f"{prefix}_card"


def extract_keywords(desc: str) -> list[str]:
    ids: list[str] = []
    seen: set[str] = set()
    for raw in RE_BRACKET.findall(desc or ""):
        label = raw.strip()
        if re.match(r"^[前中后/、\\]+$", label):
            continue
        kid = KW_ID_MAP.get(label)
        if not kid and "献祭" in label:
            kid = "sacrifice"
        if not kid and "应对状态" in label:
            kid = "respond_status"
        if not kid and "应对防御" in label:
            kid = "respond_defense"
        if kid and kid not in seen:
            seen.add(kid)
            ids.append(kid)
    return ids


def format_keywords(keywords: list[str]) -> str:
    if not keywords:
        return "  Keywords: []"
    lines = ["  Keywords:"]
    lines.extend(f"  - {k}" for k in keywords)
    return "\n".join(lines)


def repair_keywords_section(text: str, keywords: list[str]) -> str:
    """移除损坏/重复的 Keywords 段，写入正确 YAML。"""
    text = re.sub(r"  Keywords:\s*\n(?:  - .+\n|- .+\n)*", "", text)
    text = re.sub(r"  Keywords: \[\]\n", "", text)
    text = re.sub(r"^- .+\n(?=  Rarity:)", "", text, flags=re.MULTILINE)
    kw_block = format_keywords(keywords) + "\n"
    if re.search(r"  Rarity:", text):
        return re.sub(r"(  Rarity: \d+\n)", kw_block + r"\1", text, count=1)
    if re.search(r"  CardType:", text):
        return re.sub(r"(  CardType: \d+\n)", r"\1" + kw_block, text, count=1)
    return text


def infer_respond_actions(desc: str) -> list[dict]:
    """解析【应对攻击】减伤 / 反射 / 反击伤害。"""
    if "应对攻击" not in (desc or ""):
        return []

    actions: list[dict] = []
    m = RE_RESPOND_REDUCE.search(desc)
    if m:
        actions.append({
            "Type": 9, "Target": T_SELF, "Value": int(m.group(1)), "Condition": CONDITION_ATTACK,
        })
    m = RE_RESPOND_REFLECT.search(desc)
    if m:
        actions.append({
            "Type": 8, "Target": T_LAST_ACTOR, "Value": int(m.group(1)), "Condition": CONDITION_ATTACK,
        })
    m = RE_RESPOND_COUNTER.search(desc)
    if m and not any(a.get("Type") == 8 for a in actions):
        actions.append({
            "Type": 0, "Target": T_LAST_ACTOR, "Value": int(m.group(1)), "Condition": CONDITION_ATTACK,
        })
    return actions


def infer_actions(name: str, desc: str, card_type: int) -> list[dict]:
    if name in CARD_ACTION_OVERRIDES:
        return [dict(a) for a in CARD_ACTION_OVERRIDES[name]]

    actions: list[dict] = []
    d = desc or ""
    ally_pick = bool(RE_ALLY_PICK.search(d))

    respond_actions = infer_respond_actions(d)
    if respond_actions:
        actions.extend(respond_actions)
        m = RE_BLOCK.search(d)
        if m:
            val = int(m.group(1))
            actions.append({"Type": 1, "Target": T_SELF, "Value": val})
        if re.search(r"攻击者.*减速|对攻击者施加减速", d):
            m = RE_SLOW.search(d)
            stacks = int(m.group(1) or m.group(2)) if m else 1
            dur = -1 if "永久" in d else 2
            actions.append({
                "Type": 3, "Target": T_LAST_ACTOR, "StatusId": "slow",
                "Stacks": stacks, "Duration": dur, "Condition": CONDITION_ATTACK,
            })
        if "对自身施加中毒" in d or ("自身" in d and "中毒" in d and "应对" in d):
            m = RE_POISON.search(d)
            stacks = int(m.group(1) or m.group(2)) if m else 5
            dur = -1 if "永久" in d else 2
            actions.append({
                "Type": 3, "Target": T_SELF, "StatusId": "poison",
                "Stacks": stacks, "Duration": dur, "Condition": CONDITION_ATTACK,
            })
        return actions

    if "本场远征" in d and "消耗牌" in d:
        return [{
            "Type": 3, "Target": T_SELF, "Value": 0,
            "StatusId": "sand_spear_reforge", "Stacks": 4, "Duration": -1,
        }]

    m = RE_SACRIFICE_HP.search(d)
    if m and m.group(1):
        actions.append({"Type": 0, "Target": T_SELF, "Value": int(m.group(1))})
    elif m and m.group(2):
        actions.append({"Type": 0, "Target": T_SELF, "Value": 0, "HealMaxHpPercent": int(m.group(2))})

    m = RE_DRAW.search(d)
    if m:
        actions.append({"Type": 7, "Target": T_SELF, "Value": int(m.group(1))})

    m = RE_ALLY_BUFF_PAIR.search(d)
    if m:
        actions.append({
            "Type": 3, "Target": T_FRONT_ALLY, "Value": 0,
            "StatusId": "attack_up", "Stacks": int(m.group(1)), "Duration": 1, "Reach": REACH_ANY,
        })
        actions.append({
            "Type": 3, "Target": T_FRONT_ALLY, "Value": 0,
            "StatusId": "defense_up", "Stacks": int(m.group(2)), "Duration": 1, "Reach": REACH_ANY,
        })
    elif not ally_pick:
        m = RE_ATTACK_CARD_DMG.search(d)
        if m:
            actions.append({
                "Type": 3, "Target": T_SELF, "Value": 0,
                "StatusId": "attack_up", "Stacks": int(m.group(1)), "Duration": 1,
            })

    if "复活" in d and "队友" in d:
        actions.append({
            "Type": 3, "Target": T_FRONT_ALLY, "Value": 0,
            "StatusId": "revive_blessing", "Stacks": 1, "Duration": -1, "Reach": REACH_ANY,
        })

    m = RE_DAMAGE.search(d)
    if m:
        reach = 0 if "后" in d and "前" not in d else 1
        if "全体" in d or "AOE" in d or "所有敌人" in d:
            target, reach = 12, 0
        elif "随机" in d:
            target = 11
        else:
            target = 0
        act = {"Type": 0, "Target": target, "Value": int(m.group(1)), "Reach": reach}
        pct = RE_SACRIFICE_HP.search(d)
        if pct and pct.group(2) and not any(a.get("HealMaxHpPercent") for a in actions):
            act["HealMaxHpPercent"] = int(pct.group(2))
        if "身后" in d or "身后位置" in d:
            sm = RE_SPLASH_PCT.search(d)
            act["SplashBehindTarget"] = 1
            act["SplashPowerPercent"] = int(sm.group(1) or sm.group(2)) if sm else 100
        sm = RE_SLOW_BONUS.search(d)
        if sm:
            act["BonusIfTargetHasStatusId"] = "slow"
            act["BonusIfTargetHasStatusFlat"] = int(sm.group(1))
        actions.append(act)

    m = RE_BLOCK.search(d)
    if m and card_type == 1:
        val = int(m.group(1))
        if "全队" in d or "各获得" in d or "所有队友" in d or "三名队友" in d or "我方所有" in d or "所有角色" in d:
            for slot in T_ALLY_SLOTS:
                actions.append({"Type": 1, "Target": slot, "Value": val, "Reach": REACH_ANY})
        elif "队友" in d:
            actions.append({"Type": 1, "Target": T_FRONT_ALLY, "Value": val, "Reach": REACH_ANY})
        else:
            actions.append({"Type": 1, "Target": T_SELF, "Value": val})

    m = RE_HEAL.search(d)
    if m:
        target = T_FRONT_ALLY if "队友" in d else T_SELF
        actions.append({"Type": 2, "Target": target, "Value": int(m.group(1)), "Reach": REACH_ANY if target == T_FRONT_ALLY else REACH_FRONT_MID})

    m = RE_HEAL_PCT.search(d)
    if m:
        actions.append({"Type": 2, "Target": 1, "HealMaxHpPercent": int(m.group(1))})

    if "中毒" in d and ("自身" in d or "对自己" in d):
        m = RE_POISON.search(d)
        if m:
            stacks = int(m.group(1) or m.group(2))
            dur = -1 if "永久" in d else 2
            actions.append({
                "Type": 3, "Target": 1, "Value": 0,
                "StatusId": "poison", "Stacks": stacks, "Duration": dur,
            })
    elif "中毒" in d:
        m = RE_POISON.search(d)
        if m:
            stacks = int(m.group(1) or m.group(2))
            dur = -1 if "永久" in d else 2
            actions.append({
                "Type": 3, "Target": 0, "Value": 0,
                "StatusId": "poison", "Stacks": stacks, "Duration": dur,
                "Reach": 1 if "前" in d else 0,
            })

    if "身后" in d and ("相同" in d or "身后位置" in d):
        splash_pct = 100
        sm = RE_SPLASH_PCT.search(d)
        if sm:
            splash_pct = int(sm.group(1) or sm.group(2))
        for act in actions:
            if act.get("Type") == 3:
                act["SplashBehindTarget"] = 1
                act["SplashPowerPercent"] = splash_pct

    if "减速" in d and RE_APPLY_SLOW.search(d):
        m = RE_SLOW.search(d)
        if m:
            stacks = int(m.group(1) or m.group(2))
            dur = -1 if "永久" in d else (1 if "1回合" in d else 2)
            if "自身" in d or "自己" in d:
                target = T_SELF
            elif "攻击者" in d:
                target = T_LAST_ACTOR
            else:
                target = 0
            actions.append({
                "Type": 3, "Target": target, "Value": 0,
                "StatusId": "slow", "Stacks": stacks, "Duration": dur,
                "Reach": 1 if target == 0 and "前" in d else 0,
            })

    if "鼠群呼唤" in d:
        actions.append({
            "Type": 3, "Target": 1, "Value": 0,
            "StatusId": "rat_swarm_call", "Stacks": 1, "Duration": -1,
        })

    if "沙矛重塑" in d or "sand_spear" in d:
        actions.append({
            "Type": 3, "Target": 1, "Value": 0,
            "StatusId": "sand_spear_reforge", "Stacks": 1, "Duration": -1,
        })

    if not actions and card_type == 2:
        actions.append({"Type": 3, "Target": 1, "Value": 0, "StatusId": "", "Stacks": 1, "Duration": -1})

    if not actions and card_type == 0:
        actions.append({"Type": 0, "Target": 0, "Value": 8})

    return actions


def format_action(act: dict) -> str:
    lines = [
        "  - Type: {}".format(act.get("Type", 0)),
        "    Target: {}".format(act.get("Target", 0)),
        "    Value: {}".format(act.get("Value", 0)),
        "    StatusId: {}".format(act.get("StatusId", "")),
        "    Stacks: {}".format(act.get("Stacks", 1)),
        "    Duration: {}".format(act.get("Duration", -1)),
    ]
    tail = ACTION_TAIL
    if act.get("BonusIfTargetHasStatusId"):
        tail = tail.replace(
            "BonusIfTargetHasStatusId: ",
            f'BonusIfTargetHasStatusId: {act["BonusIfTargetHasStatusId"]}',
            1,
        )
    for key in ("Reach", "IgnoreDefPercent", "HealMaxHpPercent", "SelfDamageFlat", "LifestealPercent", "HitCount", "Condition", "SplashBehindTarget", "SplashPowerPercent", "BonusIfTargetHasStatusFlat"):
        if key in act:
            if isinstance(act[key], bool):
                tail = re.sub(rf"({key}: )[^\n]+", rf"\g<1>{1 if act[key] else 0}", tail)
            else:
                tail = re.sub(rf"({key}: )[^\n]+", rf"\g<1>{act[key]}", tail)
    lines.append(tail)
    return "\n".join(lines)


def build_card_asset(
    card_id: str,
    display_name: str,
    owner: str,
    cost: int,
    card_type: int,
    rarity: int,
    keywords: list[str],
    actions: list[dict],
) -> str:
    action_yaml = "\n".join(format_action(a) for a in actions)
    return f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {SCRIPT_GUID}, type: 3}}
  m_Name: Card_{card_id}
  m_EditorClassIdentifier: Grimhand.Content::Grimhand.Content.CardDefinitionSO
  CardId: {card_id}
  DisplayName: "{encode_display(display_name)}"
  OwnerCharacterId: {owner}
  Cost: {cost}
  CardType: {card_type}
  Rarity: {rarity}
{format_keywords(keywords)}
  Actions:
{action_yaml}
  CardArt: {{fileID: 0}}
  CardFrame: {{fileID: 0}}
  CardIcon: {{fileID: 0}}
"""


def write_meta(asset_path: Path) -> None:
    meta = asset_path.with_suffix(asset_path.suffix + ".meta")
    if meta.exists():
        return
    guid = uuid.uuid4().hex
    meta.write_text(
        f"fileFormatVersion: 2\n"
        f"guid: {guid}\n"
        f"NativeFormatImporter:\n"
        f"  externalObjects: {{}}\n"
        f"  mainObjectFileID: 11400000\n"
        f"  userData: \n"
        f"  assetBundleName: \n"
        f"  assetBundleVariant: \n",
        encoding="utf-8",
    )


def build_monster_char_map() -> dict[str, str]:
    """Excel 怪物名 → CharacterId；优先读 Character_*.asset，再合并静态表。"""
    mapping = dict(MONSTER_CHAR)
    char_dir = ROOT / "Data" / "Characters"
    for path in char_dir.glob("Character_*.asset"):
        text = path.read_text(encoding="utf-8")
        if re.search(r"Team:\s*0\b", text):
            continue
        m_id = re.search(r"CharacterId:\s*(\S+)", text)
        m_dn = re.search(r'DisplayName:\s*"([^"]*)"', text)
        if m_id and m_dn:
            mapping[decode_display(m_dn.group(1))] = m_id.group(1)
    return mapping


def cell_str(row: list, index: int) -> str:
    if index >= len(row) or row[index] is None:
        return ""
    return str(row[index]).strip()


def is_monster_marker_row(row: list) -> bool:
    first = cell_str(row, 0)
    if not first:
        return False
    if first in {"角色名", "卡牌名称", "地牢层", "海渊层", "共享骷髅王"}:
        return False
    if "层" in first:
        return False
    return all(not cell_str(row, i) for i in range(1, len(row)))


def index_assets() -> tuple[dict[str, Path], dict[str, Path], dict[tuple[str, str], Path]]:
    by_name: dict[str, Path] = {}
    by_id: dict[str, Path] = {}
    by_owner_name: dict[tuple[str, str], Path] = {}
    for path in CARDS_DIR.glob("Card_*.asset"):
        text = path.read_text(encoding="utf-8")
        m = re.search(r'DisplayName:\s*"([^"]*)"', text)
        cid = re.search(r"CardId:\s*(\S+)", text)
        owner = re.search(r"OwnerCharacterId:\s*(\S+)", text)
        if m:
            by_name[decode_display(m.group(1))] = path
        if cid:
            by_id[cid.group(1)] = path
        if m and owner:
            by_owner_name[(owner.group(1), decode_display(m.group(1)))] = path
    return by_name, by_id, by_owner_name


def parse_player_rows(data: dict) -> list[dict]:
    rows = []
    for row in data["卡牌"][1:]:
        if not row or len(row) < 6:
            continue
        role = (row[0] or "").strip()
        name = (row[1] or "").strip()
        desc = (row[5] or "").strip()
        if not name or not desc or name == "卡牌名称":
            continue
        if role not in PLAYER_OWNER and role not in ("", "角色"):
            continue
        cost_raw = row[2]
        is_x_cost = str(cost_raw).strip().upper() == "X"
        rows.append({
            "name": name,
            "owner": CARD_OWNER.get(name, PLAYER_OWNER.get(role, "char_knight")),
            "cost": 0 if is_x_cost else safe_int(cost_raw, 1),
            "is_x_cost": is_x_cost,
            "card_type": CARD_TYPE_OVERRIDE.get(name, CARD_TYPE.get((row[3] or "攻击").strip(), 0)),
            "rarity": RARITY.get((row[6] or "白").strip(), 0),
            "desc": desc,
            "prefix": {"战士": "w", "法老": "p", "恶魔": "d"}.get(role, "w"),
        })
    return rows


def parse_monster_rows(data: dict) -> list[dict]:
    rows = []
    current_monster = ""
    in_cards = False
    awaiting_stats = False
    monster_char = build_monster_char_map()
    for row in data["小怪设计"]:
        if not isinstance(row, list) or not row:
            continue
        first = cell_str(row, 0)
        if first == "卡牌名称":
            in_cards = True
            awaiting_stats = False
            continue
        if first == "角色名":
            in_cards = False
            awaiting_stats = True
            continue
        if awaiting_stats and first:
            try:
                hp = int(float(row[1])) if len(row) > 1 and row[1] is not None else -1
            except (TypeError, ValueError):
                hp = -1
            if hp >= 0:
                current_monster = first
                awaiting_stats = False
            continue
        if not in_cards:
            continue
        name = first
        desc = cell_str(row, 7)
        if not name or not desc or name == "卡牌名称":
            continue
        owner = CARD_OWNER.get(name, resolve_monster_owner(current_monster, monster_char))
        rows.append({
            "name": name,
            "owner": owner,
            "cost": safe_int(row[1], 1),
            "card_type": CARD_TYPE_OVERRIDE.get(name, CARD_TYPE.get(cell_str(row, 3) or "攻击", 0)),
            "rarity": RARITY.get(cell_str(row, 5) or "白", 0),
            "desc": desc,
            "prefix": "m",
            "quantity": safe_int(row[2], 1),
        })
    return rows


def parse_boss_rows(data: dict) -> list[dict]:
    rows = []
    in_cards = False
    monster_char = build_monster_char_map()
    for row in data.get("Boss设计", []):
        if not isinstance(row, list) or not row:
            continue
        first = cell_str(row, 0)
        if first == "Boss卡牌":
            in_cards = True
            continue
        if not in_cards:
            continue
        if cell_str(row, 1) == "卡牌名称":
            continue
        boss = first
        name = cell_str(row, 1)
        desc = cell_str(row, 7)
        if not boss or not name or not desc:
            continue
        owner = CARD_OWNER.get(name, resolve_monster_owner(boss, monster_char))
        rows.append({
            "name": name,
            "owner": owner,
            "cost": safe_int(row[2], 1),
            "card_type": CARD_TYPE_OVERRIDE.get(name, CARD_TYPE.get(cell_str(row, 3) or "攻击", 0)),
            "rarity": RARITY.get(cell_str(row, 5) or "白", 0),
            "desc": desc,
            "prefix": "m",
            "quantity": safe_int(row[6], 1),
        })
    return rows


def sync_card(info: dict, assets: dict[str, Path], assets_by_id: dict[str, Path], assets_by_owner: dict[tuple[str, str], Path], label: str) -> tuple[str, str]:
    name = info["name"]
    lookup = NAME_ALIASES.get(name, name)
    owner = info["owner"]
    path = assets_by_owner.get((owner, lookup)) or assets_by_owner.get((owner, name))
    owner_card_id = CARD_ID_OVERRIDE.get(name) or CARD_ID_BY_OWNER.get((owner, name))
    if owner_card_id and owner_card_id in assets_by_id:
        path = assets_by_id[owner_card_id]
    elif path is None and owner_card_id:
        path = CARDS_DIR / f"Card_{owner_card_id}.asset"
    keywords = extract_keywords(info["desc"])
    if info.get("is_x_cost") and "x_cost" not in keywords:
        keywords.append("x_cost")
    actions = infer_actions(name, info["desc"], info["card_type"])
    apply_reach_overrides(name, actions)

    card_id = owner_card_id or slugify(name, info["prefix"])
    if path is not None and path.exists():
        m = re.search(r"CardId:\s*(\S+)", path.read_text(encoding="utf-8"))
        if m and not owner_card_id:
            card_id = m.group(1)
    elif path is None:
        path = CARDS_DIR / f"Card_{card_id}.asset"

    was_new = not path.exists()
    yaml = build_card_asset(
        card_id, name, info["owner"], info["cost"],
        info["card_type"], info["rarity"], keywords, actions,
    )
    path.write_text(yaml, encoding="utf-8")
    write_meta(path)
    assets[name] = path
    assets_by_id[card_id] = path
    assets_by_owner[(info["owner"], name)] = path
    verb = "created" if was_new else "rebuilt"
    return f"{verb} [{label}] {name} -> {path.name}", card_id


def cs_escape(text: str) -> str:
    return (text or "").replace("\\", "\\\\").replace('"', '\\"')


def emit_description_catalog(entries: list[dict]) -> None:
    """entries: {name, card_id, desc}"""
    by_name: dict[str, str] = {}
    by_id: dict[str, str] = {}
    for e in entries:
        desc = (e.get("desc") or "").strip()
        if not desc:
            continue
        by_name[e["name"]] = desc
        if e.get("card_id"):
            by_id[e["card_id"]] = desc

    lines = [
        "using System.Collections.Generic;",
        "",
        "namespace Grimhand.Content",
        "{",
        "    /// <summary>卡牌描述（对照 Grimhand实际内容总览表.xlsx，UI 唯一文案来源）。</summary>",
        "    public static class CardDescriptionCatalog",
        "    {",
        "        static readonly Dictionary<string, string> ByDisplayName = BuildByName();",
        "        static readonly Dictionary<string, string> ByCardId = BuildById();",
        "",
        "        public static bool TryGetByDisplayName(string displayName, out string description)",
        "        {",
        "            description = null;",
        "            if (string.IsNullOrEmpty(displayName))",
        "                return false;",
        "            return ByDisplayName.TryGetValue(displayName, out description);",
        "        }",
        "",
        "        public static bool TryGetByCardId(string cardId, out string description)",
        "        {",
        "            description = null;",
        "            if (string.IsNullOrEmpty(cardId))",
        "                return false;",
        "            return ByCardId.TryGetValue(cardId, out description);",
        "        }",
        "",
        "        static Dictionary<string, string> BuildByName() => new()",
        "        {",
    ]
    for name in sorted(by_name.keys()):
        lines.append(f'            ["{cs_escape(name)}"] = "{cs_escape(by_name[name])}",')
    lines.extend([
        "        };",
        "",
        "        static Dictionary<string, string> BuildById() => new()",
        "        {",
    ])
    for cid in sorted(by_id.keys()):
        lines.append(f'            ["{cs_escape(cid)}"] = "{cs_escape(by_id[cid])}",')
    lines.extend([
        "        };",
        "    }",
        "}",
        "",
    ])
    DESC_CS.write_text("\n".join(lines), encoding="utf-8")
    print(f"\n=== Wrote {DESC_CS.relative_to(ROOT)} ({len(by_name)} names, {len(by_id)} ids) ===")


def repair_all_assets(assets: dict[str, Path]) -> int:
    n = 0
    for path in set(assets.values()):
        text = path.read_text(encoding="utf-8")
        if re.search(r"^- ", text, re.MULTILINE) or re.search(r"Keywords:\s*\n-", text):
            m = re.search(r'DisplayName:\s*"([^"]*)"', text)
            desc_name = decode_display(m.group(1)) if m else ""
            keywords = extract_keywords("")  # empty - will fix structure only
            # try to salvage keyword from broken line
            broken = re.findall(r"^- (\S+)", text, re.MULTILINE)
            for b in broken:
                kid = KW_ID_MAP.get(b) or (b if b in KW_ID_MAP.values() else None)
                if kid and kid not in keywords:
                    keywords.append(kid)
            fixed = repair_keywords_section(text, keywords)
            if fixed != text:
                path.write_text(fixed, encoding="utf-8")
                n += 1
                print(f"  repaired YAML: {path.name}")
    return n


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    data = json.loads(JSON_PATH.read_text(encoding="utf-8"))["data"]
    assets_by_name, assets_by_id, assets_by_owner = index_assets()

    print("=== Phase 1: repair broken YAML ===")
    repaired = repair_all_assets(assets_by_name)

    print(f"\n=== Phase 2: sync from Excel (repaired {repaired} files) ===")
    player = parse_player_rows(data)
    monster = parse_monster_rows(data) + parse_boss_rows(data)
    created = updated = 0
    missing = []
    catalog_entries: list[dict] = []

    for info in player:
        if info["name"] == "作者境的一击":
            continue
        msg, card_id = sync_card(info, assets_by_name, assets_by_id, assets_by_owner, "玩家")
        catalog_entries.append({"name": info["name"], "card_id": card_id, "desc": info["desc"]})
        if msg.startswith("created"):
            created += 1
        else:
            updated += 1
        print(f"  {msg}")

    for info in monster:
        msg, card_id = sync_card(info, assets_by_name, assets_by_id, assets_by_owner, "怪物")
        catalog_entries.append({"name": info["name"], "card_id": card_id, "desc": info["desc"]})
        if msg.startswith("created"):
            created += 1
        elif "rebuilt" in msg:
            updated += 1
        else:
            missing.append(info["name"])
        print(f"  {msg}")

    emit_description_catalog(catalog_entries)

    print(f"\nDone: created={created} updated={updated} assets={len(set(assets_by_name.values()))}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
