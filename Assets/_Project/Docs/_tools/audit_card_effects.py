#!/usr/bin/env python3
"""Audit card .asset actions vs CardDescriptionCatalog descriptions."""
from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CARDS = ROOT / "Data" / "Cards"
DESC_CS = ROOT / "Scripts" / "Content" / "CardDescriptionCatalog.cs"
OUT = ROOT / "Docs" / "_card_effect_audit_report.txt"

# Cards resolved outside standard Actions YAML.
SPECIAL_EMPTY_OK = {
    "p_solar_god_wrath",
    "p_solar_blessing",
    "w_guardian",
    "m_bat_shadow_dodge",
    "m_queen_command",
}

PASSIVE_HANDLED = {
    "d_endless_blade",
    "p_sand_spear_reforge",
    "m_spider_fatal_bind",
    "m_gargoyle_sunder",
    "m_final_bind",
    "m_magic_lightning",
    "m_golem_crack_fist",
    "m_final_summon",
    "m_king_summon_workshop",
    "m_rat_swarm_call",
}

# Status/special summon cards — description mentions 召唤 but effect is status-driven.
STATUS_SUMMON_CARDS = {
    "m_king_summon_workshop",
    "m_rat_swarm_call",
    "m_final_summon",
}


def decode(s: str) -> str:
    if "\\u" in s:
        try:
            return s.encode("utf-8").decode("unicode_escape")
        except UnicodeDecodeError:
            pass
    return s


def load_descriptions() -> dict[str, str]:
    text = DESC_CS.read_text(encoding="utf-8")
    return dict(re.findall(r'\["([^"]+)"\]\s*=\s*"([^"]+)"', text))


def parse_actions(text: str) -> list[dict]:
    actions: list[dict] = []
    for m in re.finditer(r"  - Type: (\d+)\n(.*?)(?=\n  - Type:|\n  CardArt:)", text, re.S):
        block = m.group(2)
        a: dict = {"Type": int(m.group(1))}
        for field in (
            "Target", "Value", "StatusId", "Stacks", "Duration", "HitCount",
            "SelfDamageFlat", "LifestealPercent", "SplashBehindTarget",
            "SplashPowerPercent", "IgnoreDefPercent", "HealMaxHpPercent",
            "OnKillHealAmount", "ScaleWithAttack", "SummonCharacterId",
        ):
            fm = re.search(rf"{field}:\s*(.*)", block)
            if not fm:
                continue
            v = fm.group(1).strip()
            if field in ("StatusId", "SummonCharacterId"):
                a[field] = v
            elif v:
                try:
                    a[field] = int(v)
                except ValueError:
                    pass
        actions.append(a)
    return actions


def expected_from_desc(desc: str) -> dict:
    exp: dict = {}
    hits = list(re.finditer(r"造成\s*(\d+)\s*点?伤害", desc))
    if hits:
        exp["damage"] = int(hits[-1].group(1))
    m = re.search(r"(?:获得|添加|各获得)\s*(\d+)\s*(?:点)?护甲", desc)
    if m:
        exp["block"] = int(m.group(1))
    m = re.search(r"(?:治疗|回复)\s*(?:.*?)?\s*(\d+)\s*HP", desc, re.I)
    if m:
        exp["heal"] = int(m.group(1))
    m = re.search(r"下次攻击[+＋]\s*(\d+)\s*伤害", desc)
    if m:
        exp["next_attack"] = int(m.group(1))
    m = re.search(r"攻击[+＋]\s*(\d+)%", desc)
    if m and "下次攻击" not in desc:
        exp["attack_pct"] = int(m.group(1))
    m = re.search(r"防御[+＋]\s*(\d+)%", desc)
    if m:
        exp["defense_pct"] = int(m.group(1))
    if "中毒" in desc:
        m = re.search(r"(\d+)\s*层中毒", desc) or re.search(r"中毒\s*[×x]\s*(\d+)", desc)
        if m:
            exp["poison"] = int(m.group(1))
    if "减速" in desc:
        m = re.search(r"(\d+)\s*层减速", desc) or re.search(r"减速\s*[×x]\s*(\d+)", desc)
        if m:
            exp["slow"] = int(m.group(1))
    if "灼烧" in desc:
        m = re.search(r"(\d+)\s*层灼烧", desc) or re.search(r"灼烧\s*[×x]\s*(\d+)", desc)
        if m:
            exp["burn"] = int(m.group(1))
    if "易伤" in desc:
        m = re.search(r"(\d+)\s*层易伤", desc) or re.search(r"易伤\s*[×x]\s*(\d+)", desc)
        if m:
            exp["vulnerable"] = int(m.group(1))
    if "虚弱" in desc:
        m = re.search(r"(\d+)\s*层虚弱", desc) or re.search(r"虚弱\s*[×x]\s*(\d+)", desc)
        if m:
            exp["weaken"] = int(m.group(1))
    if "嘲讽" in desc:
        exp["taunt"] = 1
    if "召唤" in desc:
        exp["summon"] = True
    if re.search(r"献祭\s*(\d+)\s*HP", desc):
        exp["sacrifice_hp"] = int(re.search(r"献祭\s*(\d+)\s*HP", desc).group(1))
    if "吸血" in desc or "回复等量HP" in desc or re.search(r"回复(?:造成)?伤害\s*\d+%", desc):
        exp["lifesteal"] = True
    if re.search(r"重复\s*(\d+)\s*次", desc):
        exp["hit_count"] = int(re.search(r"重复\s*(\d+)\s*次", desc).group(1))
    if "击杀" in desc and re.search(r"击杀(?:回复|恢复)\s*(\d+)", desc):
        exp["on_kill_heal"] = int(re.search(r"击杀(?:回复|恢复)\s*(\d+)", desc).group(1))
    return exp


def actual_from_actions(actions: list[dict]) -> dict:
    act: dict = {}
    dmg = [a["Value"] for a in actions if a["Type"] == 0 and a.get("Value")]
    if dmg:
        act["damage"] = dmg[-1]
    blk = [a["Value"] for a in actions if a["Type"] == 1 and a.get("Value")]
    if blk:
        act["block"] = blk[0]
    heal = [a["Value"] for a in actions if a["Type"] == 2 and a.get("Value")]
    if heal:
        act["heal"] = heal[0]
    act["next_attack"] = sum(
        a.get("Stacks", 0)
        for a in actions
        if a["Type"] == 3 and a.get("StatusId") in ("attack_up", "damage_up") and a.get("Target", 1) == 1
    )
    act["attack_pct"] = sum(
        a.get("Stacks", 0)
        for a in actions
        if a["Type"] == 3 and a.get("StatusId") == "attack_up_pct" and a.get("Target", 1) == 1
    )
    act["defense_pct"] = sum(
        a.get("Stacks", 0)
        for a in actions
        if a["Type"] == 3 and a.get("StatusId") == "defense_up_pct" and a.get("Target", 1) == 1
    )
    for st in ("poison", "slow", "burn", "vulnerable", "weaken"):
        act[st] = sum(a.get("Stacks", 0) for a in actions if a["Type"] == 3 and a.get("StatusId") == st)
    act["taunt"] = sum(a.get("Stacks", 0) for a in actions if a["Type"] == 3 and a.get("StatusId") == "taunt")
    act["summon"] = any(a["Type"] == 14 and a.get("SummonCharacterId") for a in actions)
    act["sacrifice_hp"] = max((a.get("SelfDamageFlat", 0) for a in actions), default=0)
    act["lifesteal"] = any(a.get("LifestealPercent", 0) > 0 for a in actions)
    act["hit_count"] = max((a.get("HitCount", 1) for a in actions if a["Type"] == 0), default=1)
    act["on_kill_heal"] = max((a.get("OnKillHealAmount", 0) for a in actions), default=0)
    return act


def compare(cid: str, desc: str, actions: list[dict]) -> list[str]:
    if cid in SPECIAL_EMPTY_OK and ("剩余所有能量" in desc or cid.startswith("p_solar")):
        return []
    if not actions and cid in SPECIAL_EMPTY_OK:
        return []

    exp = expected_from_desc(desc)
    act = actual_from_actions(actions)
    issues: list[str] = []

    for key in ("damage", "heal", "next_attack", "attack_pct", "defense_pct",
                "poison", "slow", "burn", "vulnerable", "weaken", "on_kill_heal"):
        if key not in exp:
            continue
        if key == "burn" and cid == "m_magic_lightning":
            continue
        if key == "poison" and cid == "m_magic_lightning":
            continue
        if key == "damage" and cid in PASSIVE_HANDLED and cid in ("p_sand_spear_reforge",):
            continue
        ev, av = exp[key], act.get(key, 0)
        if av != ev:
            issues.append(f"{key}: 描述={ev} 实现={av}")

    if exp.get("block") and cid in PASSIVE_HANDLED and cid in ("m_golem_crack_fist", "m_raise_bones"):
        pass
    elif exp.get("block") and act.get("block", 0) != exp["block"]:
        blk_exp = exp["block"]
        if act.get("block", 0) != blk_exp and not (act.get("summon") and cid == "m_raise_bones"):
            if not (cid == "m_raise_bones" and any(a["Type"] == 14 for a in actions)):
                issues.append(f"block: 描述={blk_exp} 实现={act.get('block', 0)}")

    if exp.get("taunt") and act.get("taunt", 0) < 1:
        issues.append("taunt: 描述有嘲讽 实现无")
    if exp.get("summon") and not act.get("summon"):
        if cid in STATUS_SUMMON_CARDS:
            pass
        else:
            issues.append("summon: 描述有召唤 实现无")
    if exp.get("sacrifice_hp"):
        self_sac = sum(
            a.get("Value", 0)
            for a in actions
            if a["Type"] == 0 and a.get("Target", 1) == 1
        )
        sac_flat = act.get("sacrifice_hp", 0)
        if self_sac != exp["sacrifice_hp"] and sac_flat != exp["sacrifice_hp"]:
            issues.append(f"sacrifice_hp: 描述={exp['sacrifice_hp']} 实现={max(self_sac, sac_flat)}")
    if exp.get("lifesteal") and not act.get("lifesteal"):
        issues.append("lifesteal: 描述有吸血 实现无")
    if exp.get("hit_count") and act.get("hit_count", 1) != exp["hit_count"]:
        issues.append(f"hit_count: 描述={exp['hit_count']} 实现={act.get('hit_count', 1)}")

    if not actions and exp and cid not in PASSIVE_HANDLED:
        issues.insert(0, "Actions 为空但描述含数值效果")

    return issues


def main() -> int:
    desc_by_id = load_descriptions()
    all_issues: list[tuple[str, str, list[str], str]] = []

    for path in sorted(CARDS.glob("Card_*.asset")):
        text = path.read_text(encoding="utf-8")
        cid_m = re.search(r"CardId:\s*(\S+)", text)
        name_m = re.search(r'DisplayName:\s*"([^"]*)"', text)
        if not cid_m:
            continue
        cid = cid_m.group(1)
        name = decode(name_m.group(1)) if name_m else cid
        desc = desc_by_id.get(cid) or desc_by_id.get(name, "")
        if not desc:
            continue
        actions = parse_actions(text)
        issues = compare(cid, desc, actions)
        if issues:
            all_issues.append((cid, name, issues, desc))

    lines = [f"卡牌效果审计 — 共 {len(all_issues)} 张有问题", "=" * 60, ""]
    for cid, name, issues, desc in all_issues:
        lines.append(f"[{cid}] {name}")
        lines.append(f"  描述: {desc}")
        for i in issues:
            lines.append(f"  - {i}")
        lines.append("")

    OUT.write_text("\n".join(lines), encoding="utf-8")
    print(f"共 {len(all_issues)} 张卡牌有问题")
    print(f"报告: {OUT}")
    for cid, name, issues, _ in all_issues[:40]:
        print(f"  {cid} ({name}): {', '.join(issues)}")
    if len(all_issues) > 40:
        print(f"  ... 另有 {len(all_issues) - 40} 张，见报告")
    return 1 if all_issues else 0


if __name__ == "__main__":
    raise SystemExit(main())
