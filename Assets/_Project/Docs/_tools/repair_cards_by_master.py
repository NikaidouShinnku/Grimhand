#!/usr/bin/env python3
"""按 _card_master_v09.json 重建指定卡牌的 Actions（修复空 StatusId / 错误 Target）。"""
from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

TOOLS = Path(__file__).resolve().parent
sys.path.insert(0, str(TOOLS))

from repair_and_sync_cards import (  # noqa: E402
    CARDS_DIR,
    apply_reach_overrides,
    build_card_asset,
    extract_keywords,
    infer_actions,
    write_meta,
)

MASTER = TOOLS.parent / "_card_master_v09.json"
CARD_TYPE_MAP = {"攻击": 0, "防御": 1, "状态": 2}


def load_master() -> dict[str, dict]:
    data = json.loads(MASTER.read_text(encoding="utf-8"))
    return {c["cardId"]: c for c in data["cards"]}


def card_type_num(card: dict) -> int:
    if "cardTypeNum" in card:
        return int(card["cardTypeNum"])
    return CARD_TYPE_MAP.get(card.get("cardType", "攻击"), 0)


def rebuild(card_id: str, info: dict) -> bool:
    path = CARDS_DIR / f"Card_{card_id}.asset"
    if not path.exists():
        print(f"  skip (no asset): {card_id}")
        return False

    name = info["displayName"]
    desc = info["effect"]
    ctype = card_type_num(info)
    keywords = extract_keywords(desc)
    if info.get("isXCost") and "x_cost" not in keywords:
        keywords.append("x_cost")
    if "快速启动" in desc and "quick_start" not in keywords:
        keywords.append("quick_start")
    if card_id == "d_demon_lord" and "sacrifice" not in keywords:
        keywords.append("sacrifice")

    actions = infer_actions(name, desc, ctype, card_id=card_id)
    apply_reach_overrides(name, actions)

    rarity_map = {"白": 0, "绿": 1, "蓝": 2, "紫": 3, "橙": 4}
    rarity = rarity_map.get(info.get("rarity", "白"), 0)
    cost_raw = info.get("cost") or 1
    cost = 0 if str(cost_raw).upper() == "X" else int(cost_raw)

    yaml = build_card_asset(
        card_id,
        name,
        info.get("ownerCharacterId", "char_knight"),
        cost,
        ctype,
        rarity,
        keywords,
        actions,
    )
    path.write_text(yaml, encoding="utf-8")
    write_meta(path)
    print(f"  rebuilt: {card_id} ({name}) actions={len(actions)}")
    return True


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser()
    ap.add_argument("--card", action="append", dest="cards", help="cardId，可重复")
    ap.add_argument("--failures-from-log", help="从 batch log 提取 FAIL 列表")
    ap.add_argument("--all-empty-status", action="store_true", help="重建所有 ApplyStatus 空 StatusId 的卡")
    args = ap.parse_args()

    master = load_master()
    targets: set[str] = set(args.cards or [])

    if args.failures_from_log:
        log = Path(args.failures_from_log)
        for line in log.read_text(encoding="utf-8", errors="replace").splitlines():
            m = re.search(r"FAIL \[([^\]]+)\]", line)
            if m:
                targets.add(m.group(1))

    if args.all_empty_status:
        for p in CARDS_DIR.glob("Card_*.asset"):
            t = p.read_text(encoding="utf-8")
            if re.search(r"- Type: 3\n.*?StatusId: \n", t, re.S):
                cid = re.search(r"CardId: (\S+)", t)
                if cid:
                    targets.add(cid.group(1))

    if not targets:
        ap.print_help()
        return 1

    ok = 0
    for cid in sorted(targets):
        if cid not in master:
            print(f"  skip (not in master): {cid}")
            continue
        if rebuild(cid, master[cid]):
            ok += 1

    print(f"\nDone: rebuilt {ok}/{len(targets)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
