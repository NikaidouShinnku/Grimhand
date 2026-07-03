#!/usr/bin/env python3
"""按 xlsx 描述修正 Card_*.asset 中定向选敌 action 的 Reach 字段。"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

TOOLS = Path(__file__).resolve().parent
sys.path.insert(0, str(TOOLS))

import audit_card_effects as audit  # noqa: E402
from card_reach_rules import parse_position_reach  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
MASTER = ROOT / "Docs" / "_card_master_v09.json"
CARDS = ROOT / "Data" / "Cards"

DIRECTED_TARGETS = {0, 1}  # DefaultEnemy, ManualSelected
PICK_TYPES = {0, 3, 4, 16, 18, 21, 22, 23, 27, 30, 32, 37}


def patch_reach_in_text(text: str, expected: int) -> tuple[str, int]:
    """仅修改 Condition=0 且 Target 为定向敌人的 action Reach。"""
    changed = 0
    parts = re.split(r"(  - Type: \d+\n)", text)
    if len(parts) < 2:
        return text, 0
    out = [parts[0]]
    for i in range(1, len(parts), 2):
        header = parts[i]
        body = parts[i + 1] if i + 1 < len(parts) else ""
        block = header + body
        type_m = re.search(r"Type: (\d+)", header)
        target_m = re.search(r"Target: (\d+)", body)
        cond_m = re.search(r"Condition: (\d+)", body)
        if not type_m or not target_m:
            out.append(block)
            continue
        t_type = int(type_m.group(1))
        t_target = int(target_m.group(1))
        t_cond = int(cond_m.group(1)) if cond_m else 0
        if t_cond == 0 and t_target in DIRECTED_TARGETS and t_type in PICK_TYPES:
            if re.search(r"Reach: \d+", body):
                new_body, n = re.subn(r"Reach: \d+", f"Reach: {expected}", body, count=1)
                if n:
                    changed += 1
                    body = new_body
            else:
                body = re.sub(r"(Condition: \d+\n)", rf"\1    Reach: {expected}\n", body, count=1)
                changed += 1
        out.append(header + body)
    return "".join(out), changed


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    master = {c["cardId"]: c for c in json.loads(MASTER.read_text(encoding="utf-8"))["cards"]}
    total_files = total_actions = 0
    for cid, card in master.items():
        desc = card.get("effect") or ""
        expected = parse_position_reach(desc)
        if expected is None:
            continue
        path = CARDS / f"Card_{cid}.asset"
        if not path.exists():
            continue
        text = path.read_text(encoding="utf-8")
        new_text, n = patch_reach_in_text(text, expected)
        if n:
            path.write_text(new_text, encoding="utf-8")
            total_files += 1
            total_actions += n
            print(f"  {cid}: Reach→{expected} ({n} actions)")
    print(f"Patched {total_actions} actions in {total_files} assets")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
