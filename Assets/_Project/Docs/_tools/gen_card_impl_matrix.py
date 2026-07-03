#!/usr/bin/env python3
"""从 master + verification 生成战斗参考 §十九 全卡实现矩阵（Markdown 片段）。"""
from __future__ import annotations

import json
import sys
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MASTER = ROOT / "Docs" / "_card_master_v09.json"
VERIFY = ROOT / "Docs" / "_card_verification_master.json"
OUT_MD = ROOT / "Docs" / "_card_impl_matrix_v09.md"
CARDS_DIR = ROOT / "Data" / "Cards"


def battle_position_hint(effect: str, card_type: str) -> str:
    hints = []
    if "快速启动" in effect:
        hints.append("quick_start")
    if "应对攻击" in effect or "应对防御" in effect or "应对状态" in effect:
        hints.append("respond")
    if card_type == "状态" and "被动" in effect:
        hints.append("passive")
    if not hints:
        hints.append("speed_resolve")
    return "/".join(hints)


def hook_hint(card_id: str) -> str:
    hooks = []
    if card_id in {
        "d_endless_blade", "p_sand_spear_reforge", "w_guardian", "p_solar_god_wrath",
        "p_solar_blessing", "m_spider_fatal_bind", "m_final_bind",
    }:
        hooks.append("Special/PassiveCardMechanicsRules")
    if card_id.startswith("l_") or card_id.startswith("v_"):
        hooks.append("V09NewMechanicsRules?")
    return hooks[0] if hooks else "-"


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    if not MASTER.exists():
        print(f"Missing {MASTER}; run build_v09_toolchain.py first", file=sys.stderr)
        return 1

    master = json.loads(MASTER.read_text(encoding="utf-8"))
    verify_map = {}
    if VERIFY.exists():
        v = json.loads(VERIFY.read_text(encoding="utf-8"))
        verify_map = {e["cardId"]: e for e in v.get("entries", [])}

    lines = [
        "# §十九 全卡实现矩阵 (v0.9)",
        "",
        f"生成时间: {datetime.now(timezone.utc).isoformat()}",
        f"卡数: {master['counts']['total']}",
        "",
        "| cardId | 名称 | 类别 | 战斗位置 | 钩子 | 核对状态 |",
        "|--------|------|------|----------|------|----------|",
    ]

    for card in master["cards"]:
        cid = card["cardId"]
        pos = battle_position_hint(card["effect"], card["cardType"])
        hook = hook_hint(cid)
        status = verify_map.get(cid, {}).get("status", "pending")
        lines.append(
            f"| `{cid}` | {card['displayName']} | {card['category']} | {pos} | {hook} | {status} |"
        )

    OUT_MD.write_text("\n".join(lines) + "\n", encoding="utf-8")
    ok = sum(1 for e in verify_map.values() if e.get("status") == "OK")
    print(f"Wrote {OUT_MD.relative_to(ROOT)} OK={ok}/{len(master['cards'])}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
