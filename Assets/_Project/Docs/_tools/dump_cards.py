import re
from pathlib import Path

cards_dir = Path(__file__).resolve().parents[2] / "Data" / "Cards"
out_path = Path(__file__).resolve().parents[1] / "all_cards_catalog.md"

EFFECT_TYPE = {
    0: "DealDamage", 1: "GainBlock", 2: "Heal", 3: "ApplyStatus", 4: "RemoveStatus",
    5: "SwapPositionWithFrontAlly", 6: "DrawCardsNextTurn", 7: "DrawCards",
    8: "ReflectLastDamageToAttacker", 9: "GainBlockFromLastDamagePercent",
}
TARGET = {
    0: "DefaultEnemy", 1: "Self", 2: "FrontAlly", 3: "BackAlly", 4: "LastActionActor",
    5: "ManualSelected", 6: "EnemyFrontSlot", 7: "EnemyMiddleSlot", 8: "EnemyBackSlot",
    9: "AllyFrontSlot", 10: "AllyMiddleSlot", 11: "AllyBackSlot", 12: "AllEnemies",
}
CONDITION = {0: "None", 1: "LastActionAttackOnSelf"}
CARD_TYPE = {0: "Attack", 1: "Defense", 2: "Status"}
REACH = {0: "Any", 1: "FrontAndMiddle", 2: "BackOnly"}

OWNER_NAMES = {
    "char_knight": "战士",
    "char_mage": "法老",
    "char_ranger": "恶魔",
    "char_goblin": "哥布林",
    "char_slime": "史莱姆",
    "char_skeleton": "骷髅兵",
    "char_skeleton_elite": "骷髅精英",
    "char_wraith": "幽灵",
    "char_wraith_elite": "幽灵精英",
}

OWNER_ORDER = [
    "char_knight", "char_mage", "char_ranger",
    "char_goblin", "char_slime", "char_skeleton", "char_skeleton_elite",
    "char_wraith", "char_wraith_elite",
]


def unescape(s):
    if not s:
        return s
    try:
        return bytes(s, "utf-8").decode("unicode_escape")
    except Exception:
        return s


def parse_card(path):
    text = path.read_text(encoding="utf-8")

    def get(field, default=""):
        m = re.search(rf"^\s*{field}: (.+)$", text, re.M)
        return m.group(1).strip() if m else default

    def get_quoted(field):
        m = re.search(rf'^\s*{field}: "(.+)"', text, re.M)
        if m:
            return unescape(m.group(1))
        return get(field)

    card = {
        "file": path.name,
        "id": get("CardId"),
        "name": get_quoted("DisplayName"),
        "owner": get("OwnerCharacterId"),
        "cost": int(get("Cost", "0") or 0),
        "type": CARD_TYPE.get(int(get("CardType", "0") or 0), get("CardType")),
        "keywords": [],
        "actions": [],
    }

    if "Keywords:" in text:
        kw_block = text.split("Keywords:")[1].split("Actions:")[0]
        card["keywords"] = re.findall(r"^  - (.+)$", kw_block, re.M)

    if "Actions:" not in text:
        return card

    actions_block = text.split("Actions:")[1]
    chunks = re.split(r"\n  - Type:", actions_block)
    for i, chunk in enumerate(chunks):
        if i == 0:
            continue
        chunk = "Type:" + chunk

        def aget(k, default=""):
            m = re.search(rf"^\s*{k}: (.+)$", chunk, re.M)
            return m.group(1).strip() if m else default

        card["actions"].append({
            "type": EFFECT_TYPE.get(int(aget("Type", "0") or 0), aget("Type")),
            "target": TARGET.get(int(aget("Target", "0") or 0), aget("Target")),
            "value": int(aget("Value", "0") or 0),
            "status": aget("StatusId", "").strip(),
            "stacks": int(aget("Stacks", "1") or 1),
            "duration": int(aget("Duration", "-1") or -1),
            "scaleAtk": aget("ScaleWithAttack") == "1",
            "scaleDef": aget("ScaleWithDefense") == "1",
            "atkPct": int(aget("AttackScalePercent", "100") or 100),
            "defPct": int(aget("DefenseScalePercent", "100") or 100),
            "condition": CONDITION.get(int(aget("Condition", "0") or 0), aget("Condition")),
            "reach": REACH.get(int(aget("Reach", "1") or 1), aget("Reach")),
            "splash": aget("SplashBehindTarget") == "1",
            "splashPct": int(aget("SplashPowerPercent", "100") or 100),
            "backRowPct": int(aget("BackRowPowerPercent", "100") or 100),
            "ignoreDefPct": int(aget("IgnoreDefPercent", "0") or 0),
            "bonusHpBelowPct": int(aget("BonusIfTargetHpBelowPercent", "0") or 0),
            "bonusHpBelowFlat": int(aget("BonusIfTargetHpBelowFlat", "0") or 0),
            "bonusHitTurnPct": int(aget("BonusIfTargetHitThisTurnPercent", "0") or 0),
            "lifestealPct": int(aget("LifestealPercent", "0") or 0),
            "onKillHeal": int(aget("OnKillHealAmount", "0") or 0),
        })
    return card


TARGET_CN = {
    "DefaultEnemy": "默认敌人",
    "Self": "自身",
    "FrontAlly": "前排队友(需点选)",
    "BackAlly": "后排队友",
    "LastActionActor": "上一行动者/攻击者",
    "ManualSelected": "手动点选",
    "EnemyFrontSlot": "敌前排",
    "EnemyMiddleSlot": "敌中排",
    "EnemyBackSlot": "敌后排",
    "AllyFrontSlot": "友前排",
    "AllyMiddleSlot": "友中排",
    "AllyBackSlot": "友后排",
    "AllEnemies": "全体敌人",
}


def target_cn(t):
    return TARGET_CN.get(t, t)


def fmt_action(a):
    cond = "【应对攻击】" if a["condition"] != "None" else ""
    if cond:
        cond += " "
    t = target_cn(a["target"])
    typ = a["type"]

    if typ == "DealDamage":
        dmg = f"攻击×{a['atkPct']}%+{a['value']}" if a["scaleAtk"] else f"{a['value']}点"
        extras = []
        if a["ignoreDefPct"]:
            extras.append(f"无视DEF{a['ignoreDefPct']}%")
        if a["bonusHpBelowPct"] or a["bonusHpBelowFlat"]:
            extras.append(f"目标HP<{a['bonusHpBelowPct']}%时额外+{a['bonusHpBelowFlat']}威力")
        if a["bonusHitTurnPct"]:
            extras.append(f"本回合已受击+{a['bonusHitTurnPct']}%伤害")
        if a["lifestealPct"]:
            extras.append(f"吸血{a['lifestealPct']}%")
        if a["onKillHeal"]:
            extras.append(f"击杀回复{a['onKillHeal']}HP")
        if a["reach"] == "BackOnly":
            extras.append("仅后排")
        elif a["reach"] == "Any":
            extras.append("任意站位")
        if a["backRowPct"] != 100:
            extras.append(f"后排威力{a['backRowPct']}%")
        if a["splash"]:
            extras.append(f"后方溅射{a['splashPct']}%")
        extra = ("；" + "，".join(extras)) if extras else ""
        return f"{cond}对{t}造成{dmg}伤害{extra}"

    if typ == "GainBlock":
        blk = f"防御×{a['defPct']}%+{a['value']}" if a["scaleDef"] else f"{a['value']}点"
        return f"{cond}{t}获得{blk}护甲"

    if typ == "Heal":
        h = str(a["value"]) if not a["scaleAtk"] else f"攻击×{a['atkPct']}%+{a['value']}"
        return f"{cond}对{t}恢复{h}HP"

    if typ == "ApplyStatus":
        dur = ""
        if a["duration"] > 0:
            dur = f"，{a['duration']}回合"
        elif a["duration"] < 0:
            dur = "，永久"
        return f"{cond}对{t}施加状态「{a['status']}」×{a['stacks']}{dur}"

    if typ == "DrawCardsNextTurn":
        return f"{cond}下回合额外抽{a['value']}张"

    if typ == "DrawCards":
        return f"{cond}下回合抽{a['value']}张"

    if typ == "GainBlockFromLastDamagePercent":
        return f"{cond}获得所受伤害{a['value']}%的护甲（应对减伤）"

    if typ == "ReflectLastDamageToAttacker":
        return f"{cond}反射{a['value']}%所受伤害给攻击者"

    return f"{cond}{typ} → {t}，数值={a['value']}，状态={a['status'] or '—'}"


def main():
    cards = [parse_card(p) for p in sorted(cards_dir.glob("Card_*.asset"))]
    lines = [
        "# Grimhand 全卡牌效果清单",
        "",
        f"共 **{len(cards)}** 张 `.asset`（正式玩家牌 30 张 + 旧 Demo 牌 6 张 + 敌人技能 26 张）",
        "",
        "**修改方式**",
        "- 玩家 30 张：`Scripts/Content/Editor/BalanceV2ContentGenerator.cs`",
        "- 敌人技能：`Scripts/Content/Editor/MonsterContentGenerator.cs`",
        "- 改完后 Unity：**Grimhand → Content → Generate Demo ScriptableObjects**",
        "- 或直接编辑 `Assets/_Project/Data/Cards/Card_*.asset`",
        "",
        "**威力公式**：攻击牌 = 攻击×倍率%+固定值；护甲牌 = 防御×倍率%+固定值（见 `balance_reference.md`）",
        "",
    ]

    PLAYER_IDS = {"char_knight", "char_mage", "char_ranger"}
    legacy = [c for c in cards if c["id"].startswith(("k_", "r_"))]
    players = [c for c in cards if c["id"][0:2] in ("w_", "p_", "d_") and c["id"][1] == "_"
               or (len(c["id"]) > 2 and c["id"][0] in "wpd" and c["id"][1] == "_")]
    players = [c for c in cards if re.match(r"^[wpd]_", c["id"])]
    enemies = [c for c in cards if c not in players and c not in legacy]

    def render_group(title, group):
        lines.append(f"## {title}")
        lines.append("")
        lines.append("| # | ID | 名称 | 费 | 类型 | 关键词 | 效果（动作序列） | 资源文件 |")
        lines.append("|---|-----|------|-----|------|--------|------------------|----------|")
        for i, c in enumerate(sorted(group, key=lambda x: (x["owner"], x["cost"], x["id"])), 1):
            kw = "、".join(c["keywords"]) if c["keywords"] else "—"
            effects = " → ".join(fmt_action(a) for a in c["actions"]) if c["actions"] else "—"
            effects = effects.replace("|", "\\|")
            owner_tag = OWNER_NAMES.get(c["owner"], c["owner"])
            lines.append(
                f"| {i} | `{c['id']}` | {c['name']} | {c['cost']} | {c['type']} | {kw} | {effects} | `{c['file']}` |"
            )
        lines.append("")

    render_group("一、玩家正式牌（30 张）", players)
    if legacy:
        render_group("二、旧 Demo 牌（未进正式牌组，可删）", legacy)
    render_group("三、敌人技能牌（26 张）", enemies)

    out_path.write_text("\n".join(lines), encoding="utf-8")
    print(f"Wrote {out_path} ({len(cards)} cards)")


if __name__ == "__main__":
    main()
