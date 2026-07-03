#!/usr/bin/env python3
"""逐卡闭环：strict 核对 → 提示跑 Unity 测试 → 跑绿后写入 behavior verified + verification OK。"""
from __future__ import annotations

import argparse
import json
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path

TOOLS = Path(__file__).resolve().parent
sys.path.insert(0, str(TOOLS))

import verify_card_strict as strict  # noqa: E402
import audit_card_effects as audit  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
MASTER = ROOT / "Docs" / "_card_master_v09.json"
BEHAVIOR = ROOT / "Docs" / "_card_behavior_verified.json"
VERIFY = ROOT / "Docs" / "_card_verification_master.json"

# cardId → NUnit 方法名（CardV09VerifiedCardsTests 内）
CARD_TESTS: dict[str, str] = {
    "w_basic_slash": "w_basic_slash_Deals8RequiresFrontMidPick",
    "w_shield_block": "w_shield_block_Grants6Block",
    "w_first_strike": "w_first_strike_Deals3WithReach",
}


def load_behavior() -> dict:
    if BEHAVIOR.exists():
        return json.loads(BEHAVIOR.read_text(encoding="utf-8"))
    return {"version": "v0.9-behavior", "definition": "Unity 跑绿后写入", "verified": {}}


def save_behavior(data: dict) -> None:
    BEHAVIOR.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")


def mark_verified(cid: str, test_method: str) -> None:
    data = load_behavior()
    data.setdefault("verified", {})[cid] = {
        "testMethod": f"CardV09VerifiedCardsTests.{test_method}",
        "unityPassed": True,
        "verifiedAt": datetime.now(timezone.utc).isoformat(),
    }
    save_behavior(data)


def refresh_verification_entry(cid: str, result: dict) -> None:
    verify = json.loads(VERIFY.read_text(encoding="utf-8"))
    for entry in verify["entries"]:
        if entry["cardId"] == cid:
            entry["checks"] = result["checks"]
            entry["status"] = result["status"]
            entry["testRef"] = result.get("testRef", "")
            entry["verifiedAt"] = result.get("verifiedAt")
            break
    ok = sum(1 for e in verify["entries"] if e["status"] == "OK")
    verify["summary"] = {"OK": ok, "pending": len(verify["entries"]) - ok}
    verify["updatedAt"] = datetime.now(timezone.utc).isoformat()
    VERIFY.write_text(json.dumps(verify, ensure_ascii=False, indent=2), encoding="utf-8")


def strict_check(cid: str) -> dict:
    master = strict.load_master()
    desc = audit.load_descriptions()
    return strict.verify_one(cid, master, desc)


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser()
    ap.add_argument("--card", required=True)
    ap.add_argument("--mark-green", action="store_true", help="Unity 已跑绿，写入 verified")
    args = ap.parse_args()
    cid = args.card

    if args.mark_green:
        tm = CARD_TESTS.get(cid)
        if not tm:
            print(f"无 CARD_TESTS 映射: {cid}")
            return 1
        mark_verified(cid, tm)

    r = strict_check(cid)
    if cid in load_behavior().get("verified", {}):
        # 注入 behavior 后重跑 strict（check7 读 json）
        r = strict_check(cid)
    refresh_verification_entry(cid, {k: v for k, v in r.items() if k != "issues"})
    print(f"{cid}: {r['status']} issues={r.get('issues', [])}")
    return 0 if r["status"] == "OK" else 1


if __name__ == "__main__":
    raise SystemExit(main())
