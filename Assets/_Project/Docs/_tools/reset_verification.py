#!/usr/bin/env python3
"""重置 _card_verification_master.json 全部为 pending（废除旧 auto OK）。"""
from __future__ import annotations

import json
import sys
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MASTER = ROOT / "Docs" / "_card_master_v09.json"
VERIFY = ROOT / "Docs" / "_card_verification_master.json"


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    master = json.loads(MASTER.read_text(encoding="utf-8"))
    entries = []
    for c in master["cards"]:
        cid = c["cardId"]
        entries.append({
            "cardId": cid,
            "displayName": c.get("displayName", cid),
            "status": "pending",
            "checks": [
                {"name": "effect_text", "pass": False, "detail": ""},
                {"name": "cost_type_keywords", "pass": False, "detail": ""},
                {"name": "actions_semantic", "pass": False, "detail": ""},
                {"name": "battle_position", "pass": False, "detail": ""},
                {"name": "hardcoded_hooks", "pass": False, "detail": ""},
                {"name": "presentation", "pass": False, "detail": ""},
                {"name": "regression", "pass": False, "detail": ""},
            ],
            "testRef": "",
            "verifiedAt": None,
        })
    payload = {
        "version": "v0.9-strict-reset",
        "generatedAt": datetime.now(timezone.utc).isoformat(),
        "summary": {"OK": 0, "pending": len(entries)},
        "entries": entries,
    }
    VERIFY.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Reset {len(entries)} entries to pending → {VERIFY.name}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
