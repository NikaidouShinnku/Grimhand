#!/usr/bin/env python3
"""根据 _card_behavior_last_run.json 生成 _card_fix_report_v09.md"""
from __future__ import annotations

import json
import sys
from datetime import datetime, timezone
from pathlib import Path

DOCS = Path(__file__).resolve().parents[1]
LAST_RUN = DOCS / "_card_behavior_last_run.json"
VERIFY = DOCS / "_card_verification_master.json"
MASTER = DOCS / "_card_master_v09.json"
OUT = DOCS / "_card_fix_report_v09.md"


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    if not LAST_RUN.exists():
        print(f"缺少 {LAST_RUN.name}，请先运行 run_card_behavior_batch.py")
        return 1

    run = json.loads(LAST_RUN.read_text(encoding="utf-8"))
    passed = [r for r in run["results"] if r["passed"]]
    failed = [r for r in run["results"] if not r["passed"]]

    strict_ok = strict_pending = 0
    pending_ids: list[str] = []
    if VERIFY.exists():
        verify = json.loads(VERIFY.read_text(encoding="utf-8"))
        for e in verify.get("entries", []):
            if e.get("status") == "OK":
                strict_ok += 1
            else:
                strict_pending += 1
                pending_ids.append(e["cardId"])

    lines = [
        "# 卡牌行为测试与修复报告（v0.9）",
        "",
        f"生成时间：{datetime.now(timezone.utc).strftime('%Y-%m-%d %H:%M UTC')}",
        f"测试批次：{run.get('runAt', '?')}",
        "",
        "## 摘要",
        "",
        f"| 指标 | 结果 |",
        f"|------|------|",
        f"| **行为测试（check7 权威）** | **{len(passed)} / {run['total']}** |",
        f"| 行为失败 | {len(failed)} |",
        f"| strict 7 项总表 | {strict_ok} / {run['total']} OK |",
        "",
        "> 行为失败 = asset 出牌效果与描述不一致。strict pending = Catalog/描述/asset 静态项或 check7 未同步。",
        "",
        "## 如何复跑",
        "",
        "```powershell",
        "# 关闭 Unity Editor 后",
        ".\\Assets\\_Project\\Docs\\_tools\\run_card_tests.ps1",
        "```",
        "",
        "详见 [CARD_BEHAVIOR_TEST.md](./CARD_BEHAVIOR_TEST.md)",
        "",
        "## 本轮主要修复",
        "",
        "### 测试基础设施",
        "- `CardV09BehaviorBatchRunner.cs`：238 张逐张 Unity batchmode 行为断言（HP/护甲/状态/应对）",
        "- `run_card_tests.ps1` + `run_card_behavior_batch.py`：一键跑测并刷新 `_card_behavior_verified.json`",
        "- `gen_card_fix_report.py`：生成本报告",
        "",
        "### 工具链 / asset",
        "- 修复 `repair_and_sync_cards.py` 中 `T_RANDOM_ENEMY=13`、应对状态推断、`CARD_ID_OVERRIDES` 被动/特殊卡",
        "- `repair_cards_by_master.py` 支持 `--card` / `--failures-from-log` 批量重建 Actions",
        "- 修复大量卡 `StatusId` 为空、应对卡 `Condition` 错误、怪物【中/后】Reach 站位",
        "",
        "### 测试器改进",
        "- 三排站位（前/中/后）满足 Reach 与 `w_war_cry` 等槽位 buff",
        "- 区分玩家应对 / 怪物条件攻击 / 怪物应对武装",
        "- 可变伤害（随机目标、多段、献祭、条件加成）降低误报",
        "",
    ]

    if failed:
        lines += ["## 仍失败（需继续修）", ""]
        for r in failed:
            lines.append(f"- `{r['cardId']}`：{r['error']}")
        lines.append("")
    else:
        lines += ["## 行为测试", "", "✅ **全部 238 张通过**（`_card_behavior_verified.json` 已更新）", ""]

    if pending_ids:
        lines += [
            f"## strict 仍 pending（{len(pending_ids)} 张，非行为失败）",
            "",
            "多为 Catalog 文案、描述 regex 或 check1–6 静态项；行为已绿时可逐张跑：",
            "",
            "```powershell",
            "python Assets/_Project/Docs/_tools/verify_card_strict.py --card <cardId>",
            "```",
            "",
        ]
        for cid in pending_ids[:30]:
            lines.append(f"- `{cid}`")
        if len(pending_ids) > 30:
            lines.append(f"- … 另有 {len(pending_ids) - 30} 张")
        lines.append("")

    OUT.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"Wrote {OUT.name}  pass={len(passed)} fail={len(failed)} strict_ok={strict_ok}")
    return 0 if not failed else 1


if __name__ == "__main__":
    raise SystemExit(main())
