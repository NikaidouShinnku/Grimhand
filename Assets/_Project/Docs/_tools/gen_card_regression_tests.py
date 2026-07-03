#!/usr/bin/env python3
"""生成 CardV09CatalogRegressionTests.cs（238 张卡 Catalog 回归）。"""
from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MASTER = ROOT / "Docs" / "_card_master_v09.json"
OUT = ROOT / "Tests" / "Battle" / "CardV09CatalogRegressionTests.cs"


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    master = json.loads(MASTER.read_text(encoding="utf-8"))
    ids = [c["cardId"] for c in master["cards"]]
    id_lines = "\n".join(f'            "{cid}",' for cid in ids)
    cs = f"""using Grimhand.Content;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{{
    /// <summary>v0.9 全量卡牌 Catalog 回归：每张卡至少断言 DefinitionId 存在于 Catalog。</summary>
    public class CardV09CatalogRegressionTests
    {{
        static readonly string[] CardIds =
        {{
{id_lines}
        }};

        [TestCaseSource(nameof(CardIds))]
        public void CatalogHasDescription(string cardId)
        {{
            Assert.IsTrue(CardDescriptionCatalog.TryGetByCardId(cardId, out var text));
            Assert.IsFalse(string.IsNullOrWhiteSpace(text));
            Assert.IsFalse(text.Contains("TODO"));
        }}
    }}
}}
"""
    OUT.write_text(cs, encoding="utf-8")
    print(f"Wrote {len(ids)} cardIds → {OUT.name}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
