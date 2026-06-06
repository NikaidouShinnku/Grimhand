#!/usr/bin/env python3
"""Compare player Card_*.asset data against Gramhand xlsx sheet expectations."""
from __future__ import annotations

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from dump_cards import parse_card, cards_dir as CARDS

# Expected from xlsx_dump.txt — mechanics only (not prose descriptions)
EXPECTED = {
    "w_basic_slash": {"cost": 1, "type": "Attack", "actions": [("DealDamage", 3, 80, {"reach": "FrontAndMiddle"})]},
    "w_shield_block": {"cost": 1, "type": "Defense", "actions": [("GainBlock", 2, 80, {"target": "Self", "scaleDef": True})]},
    "w_defensive_stance": {"cost": 1, "type": "Defense", "keywords": ["parry"], "actions": [("GainBlockFromLastDamagePercent", 50, 0, {"condition": "LastActionAttackOnSelf", "target": "Self"})]},
    "w_power_cleave": {"cost": 2, "type": "Attack", "actions": [("DealDamage", 5, 120, {"bonusHpBelowPct": 50, "bonusHpBelowFlat": 10})]},
    "w_taunt": {"cost": 2, "type": "Defense", "actions": [("ApplyStatus", 0, 0, {"status": "taunt", "target": "Self"}), ("GainBlock", 0, 120, {"target": "Self", "scaleDef": True})]},
    "w_iron_parry": {"cost": 2, "type": "Defense", "keywords": ["parry"], "actions": [
        ("GainBlockFromLastDamagePercent", 30, 0, {"condition": "LastActionAttackOnSelf"}),
        ("ReflectLastDamageToAttacker", 100, 0, {"condition": "LastActionAttackOnSelf"}),
    ]},
    "w_charge": {"cost": 3, "type": "Attack", "actions": [("DealDamage", 8, 160, {"ignoreDefPct": 50})]},
    "w_war_cry": {"cost": 1, "type": "Status", "actions": [("ApplyStatus", 3, 0, {"status": "attack_up", "target": "AllyFrontSlot"}), ("ApplyStatus", 3, 0, {"status": "attack_up", "target": "AllyMiddleSlot"}), ("ApplyStatus", 3, 0, {"status": "attack_up", "target": "AllyBackSlot"})]},
    "w_guardian": {"cost": 2, "type": "Defense", "actions": [("ApplyStatus", 0, 0, {"status": "guard", "target": "Self"})]},
    "w_fatal_strike": {"cost": 3, "type": "Attack", "actions": [("DealDamage", 6, 180, {"bonusHitTurnPct": 50})]},
    "w_unyielding": {"cost": 0, "type": "Status", "keywords": ["exhaust"], "actions": [("ApplyStatus", 0, 0, {"status": "unyielding", "target": "Self"})]},
    "p_sand_ray": {"cost": 1, "type": "Attack", "actions": [("DealDamage", 3, 80, {})]},
    "p_bless": {"cost": 1, "type": "Status", "actions": [("Heal", 2, 100, {"target": "FrontAlly", "scaleAtk": True})]},
    "p_solar_wrath": {"cost": 2, "type": "Attack", "keywords": ["aoe"], "actions": [("DealDamage", 3, 70, {"target": "AllEnemies"})]},
    "p_lifesteal": {"cost": 2, "type": "Attack", "actions": [("DealDamage", 4, 100, {"lifestealPct": 50})]},
    "p_decree": {"cost": 2, "type": "Status", "actions": [("DrawCards", 2, 0, {}), ("ApplyStatus", 3, 0, {"status": "attack_up", "target": "FrontAlly"}), ("ApplyStatus", 2, 0, {"status": "defense_up", "target": "FrontAlly"})]},
    "p_undead_curse": {"cost": 3, "type": "Attack", "keywords": ["poison"], "actions": [("DealDamage", 6, 120, {"reach": "Any"}), ("ApplyStatus", 0, 0, {"status": "necrotic_poison", "target": "DefaultEnemy"})]},
    "p_scarab_shield": {"cost": 1, "type": "Defense", "actions": [("GainBlock", 0, 120, {"target": "FrontAlly", "scaleDef": True})]},
    "p_sand_barrier": {"cost": 2, "type": "Defense", "actions": [
        ("GainBlock", 0, 100, {"target": "AllyFrontSlot", "scaleDef": True}),
        ("GainBlock", 0, 100, {"target": "AllyMiddleSlot", "scaleDef": True}),
        ("GainBlock", 0, 100, {"target": "AllyBackSlot", "scaleDef": True}),
    ]},
    "p_revive_bless": {"cost": 3, "type": "Status", "keywords": ["exhaust"], "actions": [("ApplyStatus", 0, 0, {"status": "revive_blessing", "target": "FrontAlly"})]},
    "p_solar_judgment": {"cost": 4, "type": "Attack", "actions": [("DealDamage", 10, 200, {"reach": "Any"})]},
    "d_shadow_claw": {"cost": 1, "type": "Attack", "actions": [("DealDamage", 3, 80, {})]},
    "d_devil_touch": {"cost": 1, "type": "Attack", "actions": [("DealDamage", 2, 50, {"lifestealPct": 100})]},
    "d_blood_tail": {"cost": 2, "type": "Attack", "actions": [("DealDamage", 3, 100, {"splash": True, "splashPct": 80})]},
    "d_blood_flame": {"cost": 2, "type": "Attack", "keywords": ["sacrifice"], "actions": [("DealDamage", 8, 0, {"target": "Self"}), ("DealDamage", 8, 130, {})]},
    "d_soul_rip": {"cost": 2, "type": "Attack", "actions": [("DealDamage", 4, 80, {"ignoreDefPct": 100, "reach": "Any"})]},
    "d_dark_sacrifice": {"cost": 3, "type": "Attack", "keywords": ["sacrifice"], "actions": [("DealDamage", 15, 0, {"target": "Self"}), ("DealDamage", 12, 170, {})]},
    "d_demon_pact": {"cost": 2, "type": "Status", "keywords": ["sacrifice"], "actions": [("DealDamage", 5, 0, {"target": "Self"}), ("DrawCards", 2, 0, {}), ("ApplyStatus", 3, 0, {"status": "attack_up", "target": "Self"})]},
    "d_vamp_aura": {"cost": 1, "type": "Status", "actions": [("ApplyStatus", 30, 0, {"status": "vamp_aura", "target": "Self"})]},
    "d_curse_chain": {"cost": 2, "type": "Attack", "actions": [("DealDamage", 3, 100, {"reach": "Any"}), ("ApplyStatus", 3, 0, {"status": "attack_down", "target": "DefaultEnemy"})]},
    "d_hell_fire": {"cost": 3, "type": "Attack", "keywords": ["aoe", "sacrifice"], "actions": [("DealDamage", 8, 0, {"target": "Self"}), ("DealDamage", 5, 100, {"target": "AllEnemies"})]},
    "d_demon_lord": {"cost": 4, "type": "Attack", "keywords": ["sacrifice"], "actions": [("DealDamage", 20, 0, {"target": "Self"}), ("DealDamage", 15, 200, {"reach": "Any", "onKillHeal": 30})]},
}


def norm_action(card_action: dict) -> dict:
    return {
        "type": card_action["type"],
        "value": card_action["value"],
        "atkPct": card_action["atkPct"] if card_action["scaleAtk"] else 0,
        "target": card_action["target"],
        "reach": card_action["reach"],
        "lifestealPct": card_action["lifestealPct"],
        "onKillHeal": card_action["onKillHeal"],
        "ignoreDefPct": card_action["ignoreDefPct"],
        "splash": card_action["splash"],
        "splashPct": card_action["splashPct"],
        "bonusHpBelowPct": card_action["bonusHpBelowPct"],
        "bonusHpBelowFlat": card_action["bonusHpBelowFlat"],
        "bonusHitTurnPct": card_action["bonusHitTurnPct"],
        "condition": card_action["condition"],
        "status": card_action["status"],
        "stacks": card_action["stacks"],
        "scaleDef": card_action["scaleDef"],
        "defPct": card_action["defPct"] if card_action["scaleDef"] else 0,
    }


def check_action(actual: dict, exp_type: str, exp_val: int, exp_pct: int, opts: dict) -> list[str]:
    issues = []
    if actual["type"] != exp_type:
        issues.append(f"type {actual['type']} != {exp_type}")
    if exp_type in ("DealDamage", "Heal", "GainBlock", "DrawCards", "GainBlockFromLastDamagePercent", "ReflectLastDamageToAttacker"):
        if actual["value"] != exp_val:
            issues.append(f"value {actual['value']} != {exp_val}")
    if exp_type in ("DealDamage", "Heal") and exp_pct:
        if actual["atkPct"] != exp_pct:
            issues.append(f"atkPct {actual['atkPct']} != {exp_pct}")
    if exp_type == "GainBlock" and opts.get("scaleDef"):
        if actual["defPct"] != exp_pct:
            issues.append(f"defPct {actual['defPct']} != {exp_pct}")
    for k, v in opts.items():
        if k == "scaleDef" or k == "scaleAtk":
            continue
        key_map = {
            "target": "target", "reach": "reach", "lifestealPct": "lifestealPct",
            "onKillHeal": "onKillHeal", "ignoreDefPct": "ignoreDefPct",
            "splash": "splash", "splashPct": "splashPct",
            "bonusHpBelowPct": "bonusHpBelowPct", "bonusHpBelowFlat": "bonusHpBelowFlat",
            "bonusHitTurnPct": "bonusHitTurnPct", "condition": "condition",
            "status": "status",
        }
        ak = key_map.get(k, k)
        av = actual.get(ak)
        if k == "splash" and av != v:
            issues.append(f"splash {av} != {v}")
        elif k != "splash" and av != v:
            issues.append(f"{ak} {av} != {v}")
    if exp_type == "ApplyStatus":
        if actual["stacks"] != exp_val and exp_val:
            issues.append(f"stacks {actual['stacks']} != {exp_val}")
        if opts.get("status") and actual["status"] != opts["status"]:
            issues.append(f"status {actual['status']} != {opts['status']}")
    return issues


def main():
    errors = []
    for cid, spec in sorted(EXPECTED.items()):
        path = CARDS / f"Card_{cid}.asset"
        if not path.exists():
            errors.append(f"{cid}: missing asset")
            continue
        card = parse_card(path)
        if card["cost"] != spec["cost"]:
            errors.append(f"{cid}: cost {card['cost']} != {spec['cost']}")
        if card["type"] != spec["type"]:
            errors.append(f"{cid}: type {card['type']} != {spec['type']}")
        exp_kw = sorted(spec.get("keywords", []))
        if sorted(card["keywords"]) != exp_kw:
            errors.append(f"{cid}: keywords {card['keywords']} != {exp_kw}")
        exp_actions = spec["actions"]
        if len(card["actions"]) != len(exp_actions):
            errors.append(f"{cid}: action count {len(card['actions'])} != {len(exp_actions)}")
            continue
        for i, (exp, act) in enumerate(zip(exp_actions, card["actions"])):
            exp_type, exp_val, exp_pct, opts = exp
            na = norm_action(act)
            act_issues = check_action(na, exp_type, exp_val, exp_pct, opts)
            for issue in act_issues:
                errors.append(f"{cid}[{i}]: {issue}")

    if errors:
        print(f"FAIL {len(errors)} issue(s):")
        for e in errors:
            print(" ", e)
        return 1
    print(f"OK all {len(EXPECTED)} player cards match sheet mechanics")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
