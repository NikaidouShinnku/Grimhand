#!/usr/bin/env python3
"""Unity batchmode 跑 238 张卡行为测试，并刷新 verification master。"""
from __future__ import annotations

import argparse
import json
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path

TOOLS = Path(__file__).resolve().parent
ASSETS_PROJECT = TOOLS.parents[1]  # Assets/_Project
REPO = TOOLS.parents[3]  # Unity project root (contains Assets/, ProjectSettings/)
UNITY = Path(r"C:\Program Files\Unity\Hub\Editor\6000.4.5f1\Editor\Unity.exe")
BEHAVIOR = ASSETS_PROJECT / "Docs" / "_card_behavior_verified.json"
LAST_RUN = ASSETS_PROJECT / "Docs" / "_card_behavior_last_run.json"
VERIFY = ASSETS_PROJECT / "Docs" / "_card_verification_master.json"
EXECUTE = "Grimhand.Editor.CardV09BehaviorBatchRunner.RunFromCommandLine"


def run_unity_batch(log_path: Path) -> int:
    if not UNITY.exists():
        print(f"找不到 Unity: {UNITY}")
        return 2
    cmd = [
        str(UNITY),
        "-batchmode",
        "-nographics",
        "-projectPath",
        str(REPO),
        "-executeMethod",
        EXECUTE,
        "-logFile",
        str(log_path),
    ]
    print("运行:", " ".join(cmd))
    p = subprocess.run(cmd, capture_output=False)
    return p.returncode


def refresh_verification() -> None:
    sys.path.insert(0, str(TOOLS))
    import verify_card_strict as strict  # noqa: E402
    import audit_card_effects as audit  # noqa: E402

    master = strict.load_master()
    desc = audit.load_descriptions()
    verify = json.loads(VERIFY.read_text(encoding="utf-8"))
    ok = 0
    for entry in verify["entries"]:
        cid = entry["cardId"]
        r = strict.verify_one(cid, master, desc)
        entry["checks"] = r["checks"]
        entry["status"] = r["status"]
        entry["testRef"] = r.get("testRef", "")
        entry["verifiedAt"] = r.get("verifiedAt")
        if r["status"] == "OK":
            ok += 1
    verify["summary"] = {"OK": ok, "pending": len(verify["entries"]) - ok}
    verify["updatedAt"] = datetime.now(timezone.utc).isoformat()
    VERIFY.write_text(json.dumps(verify, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"verification: {ok}/{len(verify['entries'])} OK")


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser()
    ap.add_argument("--skip-unity", action="store_true", help="只刷新 verification（已有 behavior json）")
    args = ap.parse_args()

    log = Path.home() / "AppData" / "Local" / "Temp" / "grimhand_card_batch.log"
    unity_code = 0
    if not args.skip_unity:
        unity_code = run_unity_batch(log)
        print(f"Unity exit={unity_code}, log={log}")
        import time
        for _ in range(60):
            if LAST_RUN.exists():
                break
            time.sleep(0.5)
        if unity_code != 0 and log.exists():
            lines = log.read_text(encoding="utf-8", errors="replace").splitlines()
            for line in lines:
                if "error CS" in line or "FAIL [" in line or "行为批量测试" in line:
                    print(line)
            if "error CS" in log.read_text(encoding="utf-8", errors="replace"):
                return unity_code

    if LAST_RUN.exists():
        subprocess.run([sys.executable, str(TOOLS / "gen_card_fix_report.py")], check=False)
        run = json.loads(LAST_RUN.read_text(encoding="utf-8"))
        n_pass = sum(1 for r in run["results"] if r["passed"])
        print(f"行为批量测试: {n_pass}/{run['total']} 通过")
    elif not BEHAVIOR.exists():
        print("未生成 _card_behavior_last_run.json")
        return 1

    if BEHAVIOR.exists():
        data = json.loads(BEHAVIOR.read_text(encoding="utf-8"))
        n = len(data.get("verified", {}))
        print(f"behavior verified: {n} 张")
    else:
        print("未生成 _card_behavior_verified.json")
        return 1

    refresh_verification()
    if LAST_RUN.exists():
        run = json.loads(LAST_RUN.read_text(encoding="utf-8"))
        n_fail = sum(1 for r in run["results"] if not r["passed"])
        return 1 if n_fail else 0
    return unity_code


if __name__ == "__main__":
    raise SystemExit(main())
