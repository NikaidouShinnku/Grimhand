#!/usr/bin/env python3
"""v0.8 全量卡牌同步：玩家(卡牌 sheet) + 怪物(小怪设计 sheet) → Card_*.asset"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
JSON_PATH = ROOT / "Docs" / "_excel_authoritative.json"
CARDS_DIR = ROOT / "Data" / "Cards"

# ---------- regex ----------
RE_DAMAGE = re.compile(r"造成\s*(\d+)\s*点?伤害")
RE_BLOCK = re.compile(r"(?:获得|添加|为.*?添加)\s*(\d+)\s*(?:点)?护甲")
RE_HEAL = re.compile(r"(?:治疗|回复)\s*(?:一名队友|自己|等量)?\s*(\d+)\s*HP", re.I)
RE_SACRIFICE_HP = re.compile(r"献祭\s*(\d+)\s*HP")
RE_SACRIFICE_PCT = re.compile(r"献祭\s*(\d+)%")
RE_BONUS_HP = re.compile(r"额外\s*[+＋]\s*(\d+)")
RE_BONUS_DMG = re.compile(r"伤害[+＋]\s*(\d+)")
RE_IGNORE_BLOCK = re.compile(r"无视(?:目标)?\s*(\d+)%?\s*护甲")
RE_IGNORE_ARMOR_FULL = re.compile(r"无视护甲")
RE_HIT_COUNT = re.compile(r"重复\s*(\d+)\s*次")
RE_RESPOND_MIT = re.compile(r"获得\s*(\d+)%\s*减伤")
RE_SPLASH = re.compile(r"(\d+)%\s*的?伤害")
RE_LIFESTEAL = re.compile(r"回复(?:造成)?伤害\s*(\d+)%")
RE_LIFESTEAL_FULL = re.compile(r"回复等量HP")
RE_POISON = re.compile(r"(\d+)\s*层(?:中毒|减速)")
RE_SLOW = re.compile(r"减速\s*[×x]\s*(\d+)")
RE_DAMAGE_UP = re.compile(r"攻击牌(?:伤害)?[+＋]\s*(\d+)")
RE_VULNERABLE = re.compile(r"受到的伤害[+＋]\s*(\d+)")
RE_HIT_BONUS_PCT = re.compile(r"伤害[+＋]\s*(\d+)%")
RE_ON_KILL_HEAL = re.compile(r"击杀(?:回复|恢复)\s*(\d+)\s*HP")

# Excel 名称与资产 DisplayName 不一致时的映射
NAME_ALIASES = {
    "日光审判": "太阳审判",
}

ACTION_BLOCK = re.compile(
    r"(- Type: \d+\n(?:    .+\n)*?)(?=  - Type: |  CardArt:)",
    re.MULTILINE,
)


def decode_display(raw: str) -> str:
    if "\\u" in raw:
        try:
            return raw.encode("utf-8").decode("unicode_escape")
        except UnicodeDecodeError:
            pass
    return raw


def load_json() -> dict:
    raw = json.loads(JSON_PATH.read_text(encoding="utf-8"))
    return raw.get("data", raw)


def index_assets() -> dict[str, Path]:
    by_name: dict[str, Path] = {}
    by_id: dict[str, Path] = {}
    for path in CARDS_DIR.glob("Card_*.asset"):
        text = path.read_text(encoding="utf-8")
        m_id = re.search(r"CardId:\s*(\S+)", text)
        m_name = re.search(r'DisplayName:\s*"([^"]*)"', text)
        if m_id:
            by_id[m_id.group(1)] = path
        if m_name:
            by_name[decode_display(m_name.group(1))] = path
    return {"name": by_name, "id": by_id}


def parse_player_cards(data: dict) -> dict[str, dict]:
    rows = {}
    for row in data["卡牌"][1:]:
        if not row or len(row) < 6:
            continue
        name = (row[1] or "").strip()
        desc = (row[5] or "").strip()
        if name and desc:
            kw_label = (row[12] or "").strip() if len(row) > 12 else ""
            rows[name] = {"desc": desc, "keyword": kw_label}
    return rows


def extract_keywords_from_desc(desc: str) -> list[str]:
    ids: list[str] = []
    seen: set[str] = set()
    for raw in re.findall(r"【([^】]+)】", desc or ""):
        label = raw.strip()
        if re.match(r"^[前中后/、\\]+$", label):
            continue
        kid = keyword_id(label)
        if kid and kid not in seen:
            seen.add(kid)
            ids.append(kid)
    return ids


def keyword_id(label: str) -> str | None:
    label = re.sub(r"[【】\[\]]", "", (label or "").strip())
    mapping = {
        "消耗": "exhaust", "献祭": "sacrifice", "应对攻击": "parry",
        "应对防御": "respond_defense", "应对状态": "respond_status",
        "AOE": "aoe", "中毒": "poison", "灼烧": "burn",
        "减速": "slow", "污染": "polluted", "召唤": "summon",
        "自毁": "self_destruct", "额外手牌": "bonus_hand", "虚化": "ethereal",
    }
    return mapping.get(label)


def patch_keywords(text: str, desc: str) -> str:
    keywords = extract_keywords_from_desc(desc)
    kw_block = "  Keywords: []" if not keywords else "  Keywords:\n" + "\n".join(f"  - {k}" for k in keywords)
    text = re.sub(r"  Keywords:\s*\n(?:  - .+\n|- .+\n)*", "", text)
    text = re.sub(r"  Keywords: \[\]\n", "", text)
    text = re.sub(r"^- .+\n(?=  Rarity:)", "", text, flags=re.MULTILINE)
    if re.search(r"  Rarity:", text):
        return re.sub(r"(  Rarity: \d+\n)", kw_block + "\n\\1", text, count=1)
    return re.sub(r"(  CardType: \d+\n)", "\\1" + kw_block + "\n", text, count=1)


def parse_monster_cards(data: dict) -> dict[str, str]:
    rows = {}
    in_cards = False
    for row in data["小怪设计"]:
        if not isinstance(row, list) or not row:
            continue
        if row[0] == "卡牌名称":
            in_cards = True
            continue
        if row[0] == "角色名":
            in_cards = False
            continue
        if not in_cards:
            continue
        name = (row[0] or "").strip()
        desc = (row[7] if len(row) > 7 else "") or ""
        desc = desc.strip()
        if name and desc and name != "卡牌名称":
            rows[name] = desc
    return rows


def infer_patches(desc: str) -> dict[str, int | str | bool]:
    p: dict[str, int | str | bool] = {
        "ScaleWithAttack": 0,
        "ScaleWithDefense": 0,
    }

    m = RE_DAMAGE.search(desc)
    if m:
        p["Value"] = int(m.group(1))

    m = RE_BLOCK.search(desc)
    if m:
        p["Value"] = int(m.group(1))

    m = RE_HEAL.search(desc)
    if m:
        p["Value"] = int(m.group(1))

    m = RE_SACRIFICE_HP.search(desc)
    if m:
        p["SelfDamageFlat"] = int(m.group(1))

    m = RE_SACRIFICE_PCT.search(desc)
    if m:
        p["HealMaxHpPercent"] = int(m.group(1))

    m = RE_BONUS_HP.search(desc)
    if m:
        p["BonusIfTargetHpBelowFlat"] = int(m.group(1))
        if "HP<" in desc or "HP＜" in desc:
            p["BonusIfTargetHpBelowPercent"] = 50

    m = RE_BONUS_DMG.search(desc)
    if m and "额外" in desc:
        p["BonusIfTargetHpBelowFlat"] = int(m.group(1))

    m = RE_IGNORE_BLOCK.search(desc)
    if m:
        p["IgnoreDefPercent"] = int(m.group(1))
    elif RE_IGNORE_ARMOR_FULL.search(desc):
        p["IgnoreDefPercent"] = 100

    m = RE_HIT_COUNT.search(desc)
    if m:
        p["HitCount"] = int(m.group(1))

    m = RE_RESPOND_MIT.search(desc)
    if m:
        p["Value"] = int(m.group(1))

    m = RE_SPLASH.search(desc)
    if m and ("身后" in desc or "后方" in desc or "贯通" in desc):
        p["SplashBehindTarget"] = 1
        p["SplashPowerPercent"] = int(m.group(1))

    m = RE_LIFESTEAL.search(desc)
    if m:
        p["LifestealPercent"] = int(m.group(1))
    elif RE_LIFESTEAL_FULL.search(desc):
        p["LifestealPercent"] = 100

    m = RE_ON_KILL_HEAL.search(desc)
    if m:
        p["OnKillHealAmount"] = int(m.group(1))

    m = RE_HIT_BONUS_PCT.search(desc)
    if m and ("已被攻击" in desc or "已攻击" in desc):
        p["BonusIfTargetHitThisTurnPercent"] = int(m.group(1))

    if RE_POISON.search(desc) and "中毒" in desc:
        m = re.search(r"(\d+)\s*层中毒", desc)
        if m:
            p["StatusId"] = "poison"
            p["Stacks"] = int(m.group(1))

    if "减速" in desc:
        m = RE_SLOW.search(desc) or re.search(r"(\d+)\s*层减速", desc)
        if m:
            p["StatusId"] = "slow"
            p["Stacks"] = int(m.group(1))

    if RE_DAMAGE_UP.search(desc) or "攻击牌伤害" in desc:
        m = RE_DAMAGE_UP.search(desc)
        if m:
            p["StatusId"] = "damage_up"
            p["Stacks"] = int(m.group(1))

    if RE_VULNERABLE.search(desc):
        m = RE_VULNERABLE.search(desc)
        p["StatusId"] = "vulnerable"
        p["Stacks"] = int(m.group(1))

    return p


def patch_field(text: str, field: str, value) -> str:
    if isinstance(value, bool):
        value = 1 if value else 0
    if isinstance(value, str):
        pattern = rf"({re.escape(field)}:\s*)([^\n]*)"
        if not re.search(pattern, text):
            return text
        return re.sub(pattern, rf"\g<1>{value}", text, count=1)
    pattern = rf"({re.escape(field)}:\s*)(-?\d+)"
    if not re.search(pattern, text):
        return text
    return re.sub(pattern, rf"\g<1>{value}", text, count=1)


def clear_all_scaling(text: str) -> str:
    text = re.sub(r"ScaleWithAttack:\s*1", "ScaleWithAttack: 0", text)
    text = re.sub(r"ScaleWithDefense:\s*1", "ScaleWithDefense: 0", text)
    return text


def apply_patches_to_asset(path: Path, patches: dict, desc: str) -> bool:
    text = path.read_text(encoding="utf-8")
    original = text
    text = clear_all_scaling(text)
    for field, value in patches.items():
        text = patch_field(text, field, value)
    if text != original:
        path.write_text(text, encoding="utf-8")
        return True
    return False


def sync_from_excel(excel_map: dict[str, dict], assets: dict, label: str) -> tuple[int, int, list]:
    updated = 0
    missing = []
    for name, info in excel_map.items():
        lookup = NAME_ALIASES.get(name, name)
        path = assets["name"].get(lookup)
        if not path:
            missing.append(name)
            continue
        desc = info["desc"] if isinstance(info, dict) else info
        patches = infer_patches(desc)
        changed = apply_patches_to_asset(path, patches, desc)
        if isinstance(info, dict) and info.get("desc"):
            text = path.read_text(encoding="utf-8")
            new_text = patch_keywords(text, info["desc"])
            if new_text != text:
                path.write_text(new_text, encoding="utf-8")
                changed = True
        if changed:
            updated += 1
            print(f"  [{label}] {name}: {patches}")
    return updated, len(excel_map), missing


def global_clear_scaling(assets: dict) -> int:
    n = 0
    for path in assets["id"].values():
        text = path.read_text(encoding="utf-8")
        new = clear_all_scaling(text)
        if new != text:
            path.write_text(new, encoding="utf-8")
            n += 1
    return n


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    data = load_json()
    assets = index_assets()
    player = parse_player_cards(data)
    monster = parse_monster_cards(data)

    print(f"Assets: {len(assets['id'])}  Player excel: {len(player)}  Monster excel: {len(monster)}")

    pu, pt, pm = sync_from_excel(player, assets, "玩家")
    mu, mt, mm = sync_from_excel(monster, assets, "怪物")
    cleared = global_clear_scaling(assets)

    print(f"\n玩家卡牌: 更新 {pu}/{pt}，未匹配 {len(pm)}")
    if pm:
        print("  未找到资产:", ", ".join(pm[:20]), "..." if len(pm) > 20 else "")
    print(f"怪物卡牌: 更新 {mu}/{mt}，未匹配 {len(mm)}")
    if mm:
        print("  未找到资产:", ", ".join(mm[:20]), "..." if len(mm) > 20 else "")
    print(f"全局清除 ScaleWith*: {cleared} 个资产")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
