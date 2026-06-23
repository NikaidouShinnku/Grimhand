#!/usr/bin/env python3
"""从 Grimhand实际内容总览表.xlsx 导出卡牌升级表 + 关键词表（生成 C#）。"""
from __future__ import annotations

import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
JSON_PATH = ROOT / "Docs" / "_excel_authoritative.json"
UPGRADE_CS = ROOT / "Scripts" / "Expedition" / "CardUpgradeCatalog.cs"
KEYWORD_CS = ROOT / "Scripts" / "Battle" / "Rules" / "KeywordCatalog.cs"

SKIP_KEYWORD_LABELS = {
    "卡牌颜色", "白", "绿", "蓝", "紫", "橙/金", "橙金", "传说", "普通", "稀有", "史诗", "超稀有",
    "中毒×层数", "减速×层数", "灼烧×层数", "位置， 如前/中", "位置，如前/中",
}

KW_ID_MAP = {
    "消耗": "exhaust",
    "献祭": "sacrifice",
    "应对攻击": "parry",
    "应对防御": "respond_defense",
    "应对状态": "respond_status",
    "位置": "position",
    "AOE": "aoe",
    "中毒": "poison",
    "灼烧": "burn",
    "减速": "slow",
    "污染": "polluted",
    "召唤": "summon",
    "自毁": "self_destruct",
    "额外手牌": "bonus_hand",
    "贯通": "pierce",
    "吸血": "lifesteal",
    "破甲": "ignore_armor",
    "斩杀": "execute",
    "连击": "combo_hit",
}


def strip_brackets(s: str) -> str:
    return re.sub(r"[【】\[\]]", "", (s or "").strip())


def parse_upgrade_effect(effect: str) -> dict:
    effect = (effect or "").strip()
    if not effect or effect == "-":
        return {}
    m = re.search(r"[+＋](\d+)\s*伤害", effect)
    if m:
        return {"damagePerLevel": int(m.group(1))}
    m = re.search(r"[+＋](\d+)\s*护甲", effect)
    if m:
        return {"blockPerLevel": int(m.group(1))}
    m = re.search(r"[+＋](\d+)\s*恢复?HP", effect, re.I)
    if m:
        return {"healPerLevel": int(m.group(1))}
    m = re.search(r"费用[-－](\d+)", effect)
    if m:
        return {"costReductionPerLevel": int(m.group(1))}
    m = re.search(r"[+＋](\d+)\s*HP", effect, re.I)
    if m:
        return {"healPerLevel": int(m.group(1))}
    m = re.search(r"(\d+)\s*层", effect)
    if m and "中毒" in effect:
        return {"poisonStacksPerLevel": int(m.group(1))}
    if m and "减速" in effect:
        return {"slowStacksPerLevel": int(m.group(1))}
    return {"raw": effect}


def load_cards(data: dict) -> list[dict]:
    rows = []
    for row in data["卡牌"][1:]:
        if not row or len(row) < 6:
            continue
        name = (row[1] or "").strip()
        if not name or name == "卡牌名称":
            continue
        if row[0] in (None, "角色") and name == "卡牌名称":
            continue
        max_up = row[7] if len(row) > 7 else None
        if max_up in (None, "-", ""):
            continue
        try:
            max_up = int(float(max_up))
        except (TypeError, ValueError):
            continue
        eff = (row[8] or "").strip() if len(row) > 8 else ""
        if not eff or eff == "-":
            continue
        xp_raw = (row[9] or "").strip() if len(row) > 9 else ""
        xp_per_level = 0
        m_xp = re.match(r"(\d+)\s*/\s*级", xp_raw)
        if m_xp:
            xp_per_level = int(m_xp.group(1))
        if xp_per_level <= 0:
            continue
        kw = (row[12] or "").strip() if len(row) > 12 else ""
        kw_desc = (row[13] or "").strip() if len(row) > 13 else ""
        rows.append({
            "name": name,
            "maxUpgrades": max_up,
            "effect": eff,
            "upgrade": parse_upgrade_effect(eff),
            "xpPerLevel": xp_per_level,
            "keywordLabel": kw,
            "keywordDesc": kw_desc,
        })
    return rows


def keyword_id(label: str) -> str | None:
    label = strip_brackets(label)
    if not label or label in SKIP_KEYWORD_LABELS:
        return None
    if label in KW_ID_MAP:
        return KW_ID_MAP[label]
    if re.match(r"^[前中后/\\、]+$", label):
        return None
    return None


def emit_upgrade_cs(cards: list[dict]) -> None:
    lines = [
        "using System.Collections.Generic;",
        "",
        "namespace Grimhand.Expedition",
        "{",
        "    /// <summary>卡牌升级配置（对照 Grimhand实际内容总览表.xlsx · 卡牌 sheet）。</summary>",
        "    public static class CardUpgradeCatalog",
        "    {",
        "        public sealed class UpgradeSpec",
        "        {",
            "            public int MaxUpgrades { get; set; }",
        "            public int DamagePerLevel { get; set; }",
        "            public int BlockPerLevel { get; set; }",
        "            public int HealPerLevel { get; set; }",
        "            public int CostReductionPerLevel { get; set; }",
        "            public int PoisonStacksPerLevel { get; set; }",
        "            public int SlowStacksPerLevel { get; set; }",
        "            public int XpCostPerLevel { get; set; }",
        "        }",
        "",
        "        static readonly Dictionary<string, UpgradeSpec> ByDisplayName = Build();",
        "",
        "        public static bool TryGetByDisplayName(string displayName, out UpgradeSpec spec)",
        "        {",
        "            if (string.IsNullOrEmpty(displayName))",
        "            {",
        "                spec = null;",
        "                return false;",
        "            }",
        "",
        "            return ByDisplayName.TryGetValue(displayName, out spec);",
        "        }",
        "",
        "        public static bool CanUpgrade(string displayName, int currentLevel) =>",
        "            TryGetByDisplayName(displayName, out var spec) && currentLevel < spec.MaxUpgrades;",
        "",
        "        public static int GetXpCostPerLevel(string displayName) =>",
        "            TryGetByDisplayName(displayName, out var spec) ? spec.XpCostPerLevel : 0;",
        "",
        "        static Dictionary<string, UpgradeSpec> Build() => new()",
        "        {",
    ]
    for c in cards:
        u = c["upgrade"]
        lines.append(
            f'            ["{c["name"]}"] = new() {{ MaxUpgrades = {c["maxUpgrades"]}, '
            f'DamagePerLevel = {u.get("damagePerLevel", 0)}, '
            f'BlockPerLevel = {u.get("blockPerLevel", 0)}, '
            f'HealPerLevel = {u.get("healPerLevel", 0)}, '
            f'CostReductionPerLevel = {u.get("costReductionPerLevel", 0)}, '
            f'PoisonStacksPerLevel = {u.get("poisonStacksPerLevel", 0)}, '
            f'SlowStacksPerLevel = {u.get("slowStacksPerLevel", 0)}, '
            f'XpCostPerLevel = {c.get("xpPerLevel", 0)} }},'
        )
    lines.extend([
        "        };",
        "    }",
        "}",
        "",
    ])
    UPGRADE_CS.write_text("\n".join(lines), encoding="utf-8")


def emit_keyword_cs(keywords: dict[str, str]) -> None:
    lines = [
        "using System.Collections.Generic;",
        "using System.Text;",
        "using Grimhand.Battle.Model;",
        "",
        "namespace Grimhand.Battle.Rules",
        "{",
        "    /// <summary>关键词（对照 Grimhand实际内容总览表.xlsx · 卡牌 sheet）。</summary>",
        "    public static class KeywordCatalog",
        "    {",
        "        static readonly Dictionary<string, KeywordDefinition> Definitions = BuildDefinitions();",
        "",
        "        public static bool TryGet(string keywordId, out KeywordDefinition definition) =>",
        "            Definitions.TryGetValue(keywordId, out definition);",
        "",
        "        public static string BuildTooltipText(IReadOnlyList<string> keywordIds)",
        "        {",
        "            if (keywordIds == null || keywordIds.Count == 0)",
        "                return \"\";",
        "            var sb = new StringBuilder();",
        "            for (var i = 0; i < keywordIds.Count; i++)",
        "            {",
        "                var id = keywordIds[i];",
        "                if (string.IsNullOrEmpty(id) || !TryGet(id, out var def))",
        "                    continue;",
        "                if (sb.Length > 0) sb.Append(\"\\n\\n\");",
        "                sb.Append(def.DisplayName).Append(\"：\").Append(def.Description);",
        "            }",
        "            return sb.ToString();",
        "        }",
        "",
        "        public static string BuildRichTooltipText(IReadOnlyList<string> keywordIds)",
        "        {",
        "            if (keywordIds == null || keywordIds.Count == 0)",
        "                return \"\";",
        "            var sb = new StringBuilder();",
        "            for (var i = 0; i < keywordIds.Count; i++)",
        "            {",
        "                var id = keywordIds[i];",
        "                if (string.IsNullOrEmpty(id) || !TryGet(id, out var def))",
        "                    continue;",
        "                if (sb.Length > 0) sb.Append(\"\\n\\n\");",
        "                sb.Append(\"<b>\").Append(def.DisplayName).Append(\"</b>\\n\").Append(def.Description);",
        "            }",
        "            return sb.ToString();",
        "        }",
        "",
        "        static Dictionary<string, KeywordDefinition> BuildDefinitions() => new()",
        "        {",
    ]
    for label, desc in sorted(keywords.items(), key=lambda x: x[0]):
        kid = keyword_id(label)
        if not kid:
            continue
        display = strip_brackets(label) or label
        display_esc = (
            display.replace("\\", "\\\\")
            .replace("\"", "\\\"")
            .replace("\r", " ")
            .replace("\n", " ")
        )
        desc_esc = (
            desc.replace("\\", "\\\\")
            .replace("\"", "\\\"")
            .replace("\r", " ")
            .replace("\n", " ")
        )
        lines.append(f'            ["{kid}"] = new("{kid}", "{display_esc}", "{desc_esc}"),')
    lines.extend([
        "        };",
        "    }",
        "}",
        "",
    ])
    KEYWORD_CS.write_text("\n".join(lines), encoding="utf-8")


def main() -> None:
    data = json.loads(JSON_PATH.read_text(encoding="utf-8"))["data"]
    cards = load_cards(data)
    keywords: dict[str, str] = {}
    for c in cards:
        lbl = strip_brackets(c["keywordLabel"])
        if lbl and c["keywordDesc"]:
            keywords[lbl] = c["keywordDesc"]
    # also scan monster sheet if needed
    emit_upgrade_cs(cards)
    emit_keyword_cs(keywords)
    print(f"Upgrades: {len(cards)} keywords: {len(keywords)}")


if __name__ == "__main__":
    main()
