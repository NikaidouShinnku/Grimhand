#!/usr/bin/env python3
"""从 master 提取「本场战斗中」卡清单。"""
from __future__ import annotations

import json
import sys
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MASTER = ROOT / "Docs" / "_card_master_v09.json"
OUT = ROOT / "Docs" / "_battle_scope_cards_v09.json"


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    master = json.loads(MASTER.read_text(encoding="utf-8"))
    cards = [
        {"cardId": c["cardId"], "displayName": c["displayName"], "effect": c["effect"]}
        for c in master["cards"]
        if "本场战斗" in (c.get("effect") or "")
    ]
    OUT.write_text(json.dumps({
        "version": "v0.9",
        "definition": "从生效时刻持续到一方全灭（所有玩家死亡或所有敌人死亡），战斗结束清除",
        "generatedAt": datetime.now(timezone.utc).isoformat(),
        "count": len(cards),
        "cardIds": [c["cardId"] for c in cards],
        "cards": cards,
    }, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Wrote {len(cards)} battle-scope cards to {OUT.name}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
