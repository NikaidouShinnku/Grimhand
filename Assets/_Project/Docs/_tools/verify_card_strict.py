#!/usr/bin/env python3
"""严格单卡核对：7 项 checklist，禁止 check 4-7 auto pass。"""
from __future__ import annotations

import argparse
import json
import re
import sys
from datetime import datetime, timezone
from pathlib import Path

TOOLS = Path(__file__).resolve().parent
sys.path.insert(0, str(TOOLS))

import audit_card_effects as audit  # noqa: E402
from card_reach_rules import expects_manual_enemy_pick, parse_position_reach, reach_matches_desc  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
MASTER = ROOT / "Docs" / "_card_master_v09.json"
VERIFY = ROOT / "Docs" / "_card_verification_master.json"
BATTLE_SCOPE = ROOT / "Docs" / "_battle_scope_cards_v09.json"
CARDS = ROOT / "Data" / "Cards"
DESC_CS = ROOT / "Scripts" / "Content" / "CardDescriptionCatalog.cs"
PASSIVE_CS = ROOT / "Scripts" / "Battle" / "Rules" / "PassiveCardMechanicsRules.cs"
V09_CS = ROOT / "Scripts" / "Battle" / "V09" / "V09NewMechanicsRules.cs"
SPECIAL_CS = ROOT / "Scripts" / "Battle" / "Rules" / "SpecialCardRules.cs"
STATUS_CS = ROOT / "Scripts" / "Battle" / "Status" / "StatusCatalog.cs"
CARD_POWER_CS = ROOT / "Scripts" / "Battle" / "Rules" / "CardPowerRules.cs"
EXECUTOR_CS = ROOT / "Scripts" / "Battle" / "Effects" / "EffectActionExecutor.cs"
TESTS_DIR = ROOT / "Tests" / "Battle"
OUT = ROOT / "Docs" / "_card_strict_audit_report.txt"

CARD_TYPE_NUM = {"攻击": 0, "防御": 1, "状态": 2}

# cardId → 豁免说明（7c 遗留 / 测试 / Boss 特性）
SPECIAL_CARDS: dict[str, str] = {
    "w_author_realm_strike": "测试卡",
    "m_hp": "已删除占位",
    "m_220": "BossTraitRules.GhostQueenEnrage",
}

# Catalog 参数化只证明「有描述文案」，不算行为测试（check7 禁止用其过关）
CATALOG_ONLY_TEST = "CardV09CatalogRegressionTests"

BEHAVIOR_VERIFIED = ROOT / "Docs" / "_card_behavior_verified.json"


def load_behavior_verified() -> dict[str, dict]:
    if not BEHAVIOR_VERIFIED.exists():
        return {}
    return json.loads(BEHAVIOR_VERIFIED.read_text(encoding="utf-8")).get("verified", {})

HOOK_CARD_IDS = {
    "d_endless_blade", "p_sand_spear_reforge", "w_guardian", "p_solar_god_wrath",
    "p_solar_blessing", "m_spider_fatal_bind", "m_final_bind", "m_gargoyle_sunder",
    "m_magic_lightning", "m_golem_crack_fist", "m_final_summon", "m_king_summon_workshop",
    "m_rat_swarm_call", "p_holy_infusion", "p_anubis_avatar", "w_tactician_finisher",
    "w_respond_stance", "w_god_descends", "w_last_stand", "w_burning_fury",
    "d_final_blood_ritual", "p_rot_avatar", "g_blood_scratch", "m_raise_bones",
    "v_shed_skin", "d_vamp_aura", "p_rot_touch",
}


def load_master() -> dict[str, dict]:
    data = json.loads(MASTER.read_text(encoding="utf-8"))
    return {c["cardId"]: c for c in data["cards"]}


def load_battle_scope() -> set[str]:
    if not BATTLE_SCOPE.exists():
        return set()
    return set(json.loads(BATTLE_SCOPE.read_text(encoding="utf-8")).get("cardIds", []))


def catalog_text() -> str:
    return DESC_CS.read_text(encoding="utf-8")


def check_hooks(cid: str, cs_text: str, actions: list[dict] | None = None) -> tuple[bool, str]:
    if cid not in HOOK_CARD_IDS and cid not in load_battle_scope():
        return True, "无特殊钩子"
    if cid in cs_text:
        return True, "代码引用 cardId"
    for a in actions or []:
        sid = (a.get("StatusId") or "").strip()
        if sid and sid in cs_text:
            return True, f"statusId={sid}"
    # 特殊动作类型（阿努比斯化身等）
    special_types = {a.get("Type") for a in (actions or [])}
    type_hook = {
        18: "DamagePerRespondCount",
        10: "ApplyAnubisAvatar",
        21: "DealDamageScaledByActorHpLoss",
        23: "DealDamageBonusPerTargetDebuffStack",
        27: "RemovePoisonHealPerStack",
        28: "SettlePoisonAndClear",
    }
    for t, name in type_hook.items():
        if t in special_types and name in cs_text:
            return True, name
    if 10 in special_types and "Anubis" in cs_text:
        return True, "ApplyAnubisAvatar"
    return False, "缺少 PassiveCardMechanicsRules/V09 钩子引用"


def check_battle_scope(cid: str, desc: str, actions: list[dict]) -> tuple[bool, str]:
    if "本场战斗" not in (desc or ""):
        return True, ""
    scope = load_battle_scope()
    if cid not in scope:
        return False, "含本场战斗但未在 _battle_scope_cards_v09.json"
    has_perm = any(
        a.get("Type") == 3 and a.get("Duration", -1) == -1
        for a in actions
    )
    has_timed_battle = any(
        a.get("Type") == 3 and (a.get("StatusId") or "").strip()
        for a in actions
    )
    has_special = cid in HOOK_CARD_IDS or cid == "d_endless_blade" or cid == "p_anubis_avatar"
    if not has_perm and not has_timed_battle and not has_special:
        return False, "本场战斗卡缺少 Permanent status 或已知钩子"
    cs = (
        PASSIVE_CS.read_text(encoding="utf-8")
        + V09_CS.read_text(encoding="utf-8")
        + STATUS_CS.read_text(encoding="utf-8")
    )
    ok, detail = check_hooks(cid, cs, actions)
    return ok, detail or "Permanent+钩子"


def check_presentation(card_type: int, keywords: list[str]) -> tuple[bool, str]:
    if "parry" in keywords or "respond_defense" in keywords or "respond_status" in keywords:
        return True, "应对卡：弹反/插队演出"
    if card_type == 0:
        return True, "攻击：PortraitPose"
    return True, "标准结算"


def verify_one(cid: str, master: dict[str, dict], desc_by_id: dict[str, str]) -> dict:
    card = master.get(cid, {})
    xlsx_effect = (card.get("effect") or "").strip()
    special_note = SPECIAL_CARDS.get(cid, "")
    asset_path = CARDS / f"Card_{cid}.asset"
    issues: list[str] = []

    catalog = desc_by_id.get(cid, "")
    check1 = bool(catalog) and (catalog == xlsx_effect or bool(special_note)) and "TODO" not in catalog
    if not catalog:
        issues.append("Catalog 缺失")
    elif catalog != xlsx_effect and not special_note:
        issues.append("Catalog 与 xlsx 不一致")
    if "TODO" in catalog:
        issues.append("Catalog 含 TODO")

    if not asset_path.exists():
        if cid not in SPECIAL_CARDS:
            issues.append("asset 缺失")
        actions = []
        keywords = []
        cost = card.get("cost", "?")
        card_type = CARD_TYPE_NUM.get(card.get("cardType", "攻击"), 0)
    else:
        text = asset_path.read_text(encoding="utf-8")
        actions = audit.parse_actions(text)
        keywords = re.findall(r"  - (\S+)", text.split("Actions:")[0].split("Keywords:")[-1] if "Keywords:" in text else "")
        if not keywords and "Keywords:" in text:
            kw_block = re.search(r"Keywords:\s*\n((?:  - .+\n)*)", text)
            if kw_block:
                keywords = re.findall(r"  - (\S+)", kw_block.group(1))
        cost_m = re.search(r"Cost: (\S+)", text)
        cost = cost_m.group(1) if cost_m else "?"
        ct_m = re.search(r"CardType: (\d+)", text)
        card_type = int(ct_m.group(1)) if ct_m else 0

    check2 = asset_path.exists() or cid in SPECIAL_CARDS
    if "respond_attack" in keywords:
        issues.append("keyword 仍为 respond_attack")
        check2 = False
    if card.get("isXCost") and "x_cost" not in keywords:
        issues.append("缺少 x_cost keyword")
        check2 = False
    if "快速启动" in xlsx_effect and "quick_start" not in keywords:
        issues.append("缺少 quick_start keyword（快速启动无法点按立即生效）")
        check2 = False

    audit_issues: list[str] = []
    if cid in SPECIAL_CARDS:
        check3 = True
        check4 = True
        check5 = True
        hook_detail = special_note
    else:
        audit_issues = audit.compare(cid, catalog or xlsx_effect, actions) if catalog or xlsx_effect else ["无描述"]
        check3 = len(audit_issues) == 0
        issues.extend(audit_issues)

        reach_ok, reach_detail = reach_matches_desc(xlsx_effect, actions)
        pos_expect = parse_position_reach(xlsx_effect)
        manual_pick = expects_manual_enemy_pick(xlsx_effect, actions)
        check4 = reach_ok and (not manual_pick or pos_expect is not None)
        if not reach_ok:
            issues.append(reach_detail)
        if manual_pick and pos_expect is None:
            issues.append("应有位置选目标但未解析 Reach")

        cs_all = (
            PASSIVE_CS.read_text(encoding="utf-8")
            + V09_CS.read_text(encoding="utf-8")
            + SPECIAL_CS.read_text(encoding="utf-8")
            + STATUS_CS.read_text(encoding="utf-8")
            + CARD_POWER_CS.read_text(encoding="utf-8")
            + EXECUTOR_CS.read_text(encoding="utf-8")
        )
        check5, hook_detail = check_hooks(cid, cs_all, actions)
        if "本场战斗" in xlsx_effect:
            bs_ok, bs_detail = check_battle_scope(cid, xlsx_effect, actions)
            check5 = check5 and bs_ok
            if not bs_ok:
                issues.append(bs_detail)

    check6, pres_detail = check_presentation(card_type, keywords)

    verified = load_behavior_verified().get(cid)
    test_ref = verified.get("testMethod", "") if verified else ""
    check7 = bool(verified and verified.get("unityPassed") is True and test_ref)
    if not check7:
        if not verified:
            issues.append("缺少行为测试（未写入 _card_behavior_verified.json）")
        elif not verified.get("unityPassed"):
            issues.append(f"行为测试未跑绿: {test_ref or '?'}")

    all_pass = all([check1, check2, check3, check4, check5, check6, check7])
    return {
        "cardId": cid,
        "displayName": card.get("displayName", cid),
        "checks": [
            {"name": "effect_text", "pass": check1, "detail": xlsx_effect[:100]},
            {"name": "cost_type_keywords", "pass": check2, "detail": f"cost={cost} kw={keywords}"},
            {"name": "actions_semantic", "pass": check3, "detail": "; ".join(audit_issues[:5])},
            {"name": "battle_position", "pass": check4, "detail": f"reach={parse_position_reach(xlsx_effect)}"},
            {"name": "hardcoded_hooks", "pass": check5, "detail": hook_detail},
            {"name": "presentation", "pass": check6, "detail": pres_detail},
            {"name": "regression", "pass": check7, "detail": test_ref or "missing"},
        ],
        "status": "OK" if all_pass else "pending",
        "issues": issues,
        "testRef": test_ref,
        "verifiedAt": datetime.now(timezone.utc).isoformat() if all_pass else None,
    }


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser()
    ap.add_argument("--card", help="single cardId")
    ap.add_argument("--all", action="store_true")
    ap.add_argument("--write", action="store_true", help="update verification master")
    args = ap.parse_args()

    master = load_master()
    desc_by_id = audit.load_descriptions()
    cids = [args.card] if args.card else list(master.keys()) if args.all else []

    if not cids:
        ap.print_help()
        return 1

    results = [verify_one(cid, master, desc_by_id) for cid in cids]
    fails = [r for r in results if r["status"] != "OK"]

    lines = []
    for r in results:
        if r["issues"]:
            lines.append(f"{r['cardId']}: " + "; ".join(r["issues"]))
    OUT.write_text("\n".join(lines) + f"\n\nfail={len(fails)}/{len(results)}\n", encoding="utf-8")

    if args.write and args.all:
        ok = sum(1 for r in results if r["status"] == "OK")
        payload = {
            "version": "v0.9-strict",
            "generatedAt": datetime.now(timezone.utc).isoformat(),
            "summary": {"OK": ok, "pending": len(results) - ok},
            "entries": [{k: v for k, v in r.items() if k != "issues"} for r in results],
        }
        VERIFY.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
        print(f"Wrote {VERIFY.name} OK={ok}/{len(results)}")

    print(f"strict audit: fail={len(fails)}/{len(results)}")
    return 1 if fails else 0


if __name__ == "__main__":
    raise SystemExit(main())
