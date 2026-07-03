#!/usr/bin/env python3
"""为无 xlsx 行的遗留 Card 资产补全 Catalog 条目（7c 别名清理辅助）。"""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CARDS = ROOT / "Data" / "Cards"
DESC_CS = ROOT / "Scripts" / "Content" / "CardDescriptionCatalog.cs"

LEGACY_DESC: dict[str, str] = {
    "curse_chaos_touch": "诅咒：混沌之触（远征附加）",
    "g_aim": "哥布林：瞄准",
    "g_lunge": "哥布林：猛扑",
    "g_scratch": "哥布林：抓挠",
    "g_wither": "哥布林：枯萎",
    "m_bolt": "怪物：闪电",
    "m_curse": "怪物：诅咒",
    "m_poison": "怪物：中毒",
    "m_slime_split": "史莱姆：分裂",
    "m_void": "怪物：虚空",
    "m_利爪劈击": "（遗留别名，待合并）",
    "m_破甲俯冲": "（遗留别名，待合并）",
    "m_终焉魂缚": "（遗留别名 → m_final_bind）",
    "w_author_realm_strike": "测试卡：作者境的一击（非正式）",
}


def decode(s: str) -> str:
    if "\\u" in s:
        try:
            return s.encode("utf-8").decode("unicode_escape")
        except UnicodeDecodeError:
            pass
    return s


def cs_escape(text: str) -> str:
    return (text or "").replace("\\", "\\\\").replace('"', '\\"')


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    text = DESC_CS.read_text(encoding="utf-8")
    by_name = dict(re.findall(r'\["([^"]+)"\]\s*=\s*"([^"]+)"', text.split("BuildById")[0]))
    by_id = dict(re.findall(r'\["([^"]+)"\]\s*=\s*"([^"]+)"', text.split("BuildById")[1]))

    added = 0
    for path in CARDS.glob("Card_*.asset"):
        t = path.read_text(encoding="utf-8")
        cid_m = re.search(r"CardId:\s*(\S+)", t)
        name_m = re.search(r'DisplayName:\s*"([^"]*)"', t)
        if not cid_m:
            continue
        cid = cid_m.group(1)
        name = decode(name_m.group(1)) if name_m else cid
        if cid in by_id:
            continue
        desc = LEGACY_DESC.get(cid) or LEGACY_DESC.get(name) or f"（遗留资产 {name}）"
        by_id[cid] = desc
        if name not in by_name:
            by_name[name] = desc
        added += 1

    lines = [
        "using System.Collections.Generic;",
        "",
        "namespace Grimhand.Content",
        "{",
        "    /// <summary>卡牌描述（对照 Grimhand实际内容总览表.xlsx，UI 唯一文案来源）。</summary>",
        "    public static class CardDescriptionCatalog",
        "    {",
        "        static readonly Dictionary<string, string> ByDisplayName = BuildByName();",
        "        static readonly Dictionary<string, string> ByCardId = BuildById();",
        "",
        "        public static bool TryGetByDisplayName(string displayName, out string description)",
        "        {",
        "            description = null;",
        "            if (string.IsNullOrEmpty(displayName))",
        "                return false;",
        "            return ByDisplayName.TryGetValue(displayName, out description);",
        "        }",
        "",
        "        public static bool TryGetByCardId(string cardId, out string description)",
        "        {",
        "            description = null;",
        "            if (string.IsNullOrEmpty(cardId))",
        "                return false;",
        "            return ByCardId.TryGetValue(cardId, out description);",
        "        }",
        "",
        "        static Dictionary<string, string> BuildByName() => new()",
        "        {",
    ]
    for name in sorted(by_name.keys()):
        lines.append(f'            ["{cs_escape(name)}"] = "{cs_escape(by_name[name])}",')
    lines.extend([
        "        };",
        "",
        "        static Dictionary<string, string> BuildById() => new()",
        "        {",
    ])
    for cid in sorted(by_id.keys()):
        lines.append(f'            ["{cs_escape(cid)}"] = "{cs_escape(by_id[cid])}",')
    lines.extend(["        };", "    }", "}", ""])
    DESC_CS.write_text("\n".join(lines), encoding="utf-8")
    print(f"Added {added} legacy catalog entries; total ids={len(by_id)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
