#!/usr/bin/env python3
"""为本场战斗被动卡补全 asset StatusId + Duration=-1。"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SCOPE = ROOT / "Docs" / "_battle_scope_cards_v09.json"
CARDS = ROOT / "Data" / "Cards"

# cardId → StatusId（d_endless_blade / p_anubis_avatar 走特殊 action，跳过）
STATUS_BY_CARD: dict[str, str] = {
    "w_respond_stance": "respond_stance",
    "w_battle_will": "battle_will",
    "w_heavy_armor": "heavy_armor",
    "w_unyielding": "unyielding",
    "w_god_descends": "god_descends",
    "w_final_bulwark": "final_bulwark",
    "w_last_stand": "last_stand",
    "p_plague_spread": "plague_spread",
    "p_rot_avatar": "rot_avatar",
    "d_blood_frenzy": "blood_frenzy",
    "d_bloodline_legacy": "bloodline_legacy",
    "d_blood_sharing": "blood_sharing",
    "d_final_blood_ritual": "final_blood_ritual",
    "v_venom_sac_burst": "venom_sac_burst",
    "v_immortal_shed": "immortal_shed",
    "l_psionic_body": "psionic_body",
    "m_queen_wrath": "ghost_queen_wrath",
}

SKIP = {"d_endless_blade", "p_anubis_avatar"}


def patch_status(text: str, status_id: str) -> str:
    if "StatusId:" not in text:
        return text
    text = re.sub(r"StatusId: \S*", f"StatusId: {status_id}", text, count=1)
    text = re.sub(r"Duration: \d+", "Duration: -1", text, count=1)
    return text


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    scope = json.loads(SCOPE.read_text(encoding="utf-8"))
    fixed = 0
    for cid in scope["cardIds"]:
        if cid in SKIP:
            continue
        sid = STATUS_BY_CARD.get(cid)
        if not sid:
            print(f"  skip (no map): {cid}")
            continue
        path = CARDS / f"Card_{cid}.asset"
        if not path.exists():
            print(f"  missing asset: {cid}")
            continue
        text = path.read_text(encoding="utf-8")
        if f"StatusId: {sid}" in text:
            continue
        new_text = patch_status(text, sid)
        path.write_text(new_text, encoding="utf-8")
        fixed += 1
        print(f"  {cid} → {sid}")
    print(f"Fixed {fixed} battle-scope assets")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
