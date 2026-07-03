#!/usr/bin/env python3
"""列出缺少 Catalog 描述的 Card 资产。"""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CARDS = ROOT / "Data" / "Cards"
DESC = ROOT / "Scripts" / "Content" / "CardDescriptionCatalog.cs"


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    text = DESC.read_text(encoding="utf-8")
    by_id = dict(re.findall(r'\["([^"]+)"\]\s*=\s*"([^"]+)"', text))
    missing = []
    for path in sorted(CARDS.glob("Card_*.asset")):
        t = path.read_text(encoding="utf-8")
        m = re.search(r"CardId:\s*(\S+)", t)
        if not m:
            continue
        cid = m.group(1)
        if cid not in by_id:
            missing.append(cid)
    print("missing", len(missing))
    for cid in missing:
        print(cid)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
