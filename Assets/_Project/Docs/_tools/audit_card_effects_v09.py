#!/usr/bin/env python3
"""扩展 audit：委托 verify_card_strict，禁止 check 4-7 auto pass。"""
from __future__ import annotations

import json
import sys
from datetime import datetime, timezone
from pathlib import Path

TOOLS = Path(__file__).resolve().parent
sys.path.insert(0, str(TOOLS))
import audit_card_effects as audit  # noqa: E402
import verify_card_strict as strict  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
VERIFY = ROOT / "Docs" / "_card_verification_master.json"
MASTER = ROOT / "Docs" / "_card_master_v09.json"


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    master = strict.load_master()
    desc_by_id = audit.load_descriptions()
    results = [strict.verify_one(cid, master, desc_by_id) for cid in master]
    fails = [r for r in results if r["status"] != "OK"]

    audit.OUT.write_text(
        "\n".join(f"{r['cardId']}: " + "; ".join(r["issues"]) for r in fails if r["issues"])
        + f"\n\nstrict fail={len(fails)}/{len(results)}\n",
        encoding="utf-8",
    )

    payload = {
        "version": "v0.9-strict",
        "generatedAt": datetime.now(timezone.utc).isoformat(),
        "summary": {"OK": len(results) - len(fails), "pending": len(fails)},
        "entries": [{k: v for k, v in r.items() if k != "issues"} for r in results],
    }
    VERIFY.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"strict audit fail={len(fails)}/{len(results)} verification OK={payload['summary']['OK']}")
    return 1 if fails else 0


if __name__ == "__main__":
    raise SystemExit(main())
