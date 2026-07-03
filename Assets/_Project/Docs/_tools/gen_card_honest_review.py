#!/usr/bin/env python3
"""生成诚实的逐卡核对讨论稿（238 张），供人工 review。"""
from __future__ import annotations

import json
import sys
from datetime import datetime, timezone
from pathlib import Path

TOOLS = Path(__file__).resolve().parent
sys.path.insert(0, str(TOOLS))

import audit_card_effects as audit  # noqa: E402
from card_reach_rules import parse_position_reach, reach_matches_desc, expects_manual_enemy_pick  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
MASTER = ROOT / "Docs" / "_card_master_v09.json"
VERIFY = ROOT / "Docs" / "_card_verification_master.json"
OUT = ROOT / "Docs" / "_card_honest_review_v09.md"

SPECIAL = {
    "w_author_realm_strike": "测试卡：Catalog 与 xlsx 故意不一致，无行为测试",
    "m_hp": "总览表遗留占位，无 asset，已删除",
    "m_220": "Boss 特性卡，逻辑在 BossTraitRules，无独立 asset",
}

# 批量脚本改过、需手测/行为测试重点关注的卡
BATCH_FIXED = {
    "w_heavy_armor": "曾 Actions 为空；已补 heavy_armor Permanent，需验证获甲 +20%",
    "w_god_descends": "曾误写为 AOE 8 伤；已改为 god_descends 被动，需验证获甲触发",
    "d_final_blood_ritual": "曾误写为 Heal；已改为 final_blood_ritual 被动",
    "p_anubis_avatar": "已改为 Type=10 ApplyAnubisAvatar，需验证禁出牌 2 回合",
    "p_rot_avatar": "曾 Target=敌人；已改 Self + rot_avatar，需验证回合开始中毒",
    "m_tide_charge": "曾 Target=全体；已改单目标 Reach=0；**速度快于敌人 +8 伤未实装**（asset 误用 BonusIfTargetHpBelowFlat）",
    "w_guardian": "曾 Actions 为空；已补 guard 1 回合，需验证伤害转移",
    "m_final_summon": "补 StatusId=final_summon_pending",
    "m_king_summon_workshop": "补 StatusId=bone_workshop",
    "d_endless_blade": "Reach 批量修正；翻倍伤害靠 PassiveCardMechanicsRules 非 status",
}


def risk_level(flags: list[str]) -> str:
    if any("未实装" in f or "误用" in f for f in flags):
        return "高"
    if "SPECIAL" in flags or "批量修复" in flags or "PASSIVE_HANDLED" in flags:
        return "中"
    if "仅Catalog" in flags or "应对" in flags:
        return "中"
    if "位置" in flags:
        return "低～中"
    return "低"


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    master_cards = json.loads(MASTER.read_text(encoding="utf-8"))["cards"]
    verify_map = {e["cardId"]: e for e in json.loads(VERIFY.read_text(encoding="utf-8"))["entries"]}
    desc = audit.load_descriptions()
    ph = audit.PASSIVE_HANDLED

    rows: list[dict] = []
    for c in master_cards:
        cid = c["cardId"]
        eff = (c.get("effect") or "").strip()
        name = c.get("displayName", cid)
        ap = ROOT / "Data" / "Cards" / f"Card_{cid}.asset"
        has_asset = ap.exists()
        actions = audit.parse_actions(ap.read_text(encoding="utf-8")) if has_asset else []
        flags: list[str] = []
        notes: list[str] = []

        if cid in SPECIAL:
            flags.append("SPECIAL豁免")
            notes.append(SPECIAL[cid])
        if cid in ph:
            flags.append("PASSIVE_HANDLED")
            notes.append("数值/语义 compare 整卡跳过")
        if cid in BATCH_FIXED:
            flags.append("批量修复")
            notes.append(BATCH_FIXED[cid])
        reach = parse_position_reach(eff)
        if reach is not None:
            flags.append("位置括号")
            ok, detail = reach_matches_desc(eff, actions)
            if not ok:
                notes.append(f"Reach 仍不一致: {detail}")
            if expects_manual_enemy_pick(eff, actions):
                notes.append("描述要求先选目标（未跑 Unity ShouldPromptForTarget）")
        if "本场战斗" in eff:
            flags.append("本场战斗")
        if any(k in eff for k in ("【应对攻击】", "【应对状态】", "【应对防御】", "应对攻击", "应对状态", "应对防御")):
            flags.append("应对卡")
            notes.append("check3 不验证 Condition 句号分段（§3.4）")
        if not has_asset:
            notes.append("缺少 Card_*.asset")

        v = verify_map.get(cid, {})
        test_ref = v.get("testRef", "")
        if test_ref in ("", "CardV09CatalogRegressionTests") or test_ref == "测试卡":
            flags.append("仅Catalog或无行为测试")
        elif "Catalog" in test_ref:
            flags.append("Catalog+部分")

        raw_issues = audit.compare(cid, desc.get(cid, eff), actions) if cid not in SPECIAL else []
        if raw_issues:
            notes.extend(raw_issues[:4])

        catalog = desc.get(cid, "")
        if catalog and catalog != eff and cid not in SPECIAL:
            notes.append("Catalog ≠ xlsx effect")

        rows.append({
            "idx": len(rows) + 1,
            "cardId": cid,
            "name": name,
            "effect": eff,
            "flags": flags,
            "risk": risk_level(flags + notes),
            "notes": notes,
            "testRef": test_ref or "—",
            "strictOK": v.get("status") == "OK",
        })

    high = [r for r in rows if r["risk"] == "高"]
    med = [r for r in rows if r["risk"] == "中"]
    pos = [r for r in rows if "位置括号" in r["flags"]]
    scope = [r for r in rows if "本场战斗" in r["flags"]]
    catalog_only = [r for r in rows if "仅Catalog或无行为测试" in r["flags"]]

    lines = [
        "# v0.9 卡牌实装诚实核对稿（讨论用）",
        "",
        f"> 生成时间：{datetime.now(timezone.utc).strftime('%Y-%m-%d %H:%M UTC')}  ",
        "> **目的**：说明「238/238 strict OK」实际证明了什么、没证明什么，并逐张列出风险与待验证项。  ",
        "> **不是**「全部实装完成」的签字页。",
        "",
        "---",
        "",
        "## 一、为什么我说「问题可能还多着」",
        "",
        "`verify_card_strict.py` 的 7 项检查是**静态**的，且有多处「能通过但不代表能玩对」：",
        "",
        "| 检查项 | 实际在查什么 | 没查什么 |",
        "|--------|-------------|----------|",
        "| check1 effect_text | Catalog 与 xlsx 逐字 | 运行时 UI 是否刷新、升级牌实例 |",
        "| check2 cost/keywords | asset 存在、无 respond_attack | Cost 与 xlsx 逐字段、Rarity |",
        "| check3 actions_semantic | 简单 regex：伤害/护甲/中毒数值 | 应对 Condition 分段、多段效果、X 费、条件分支 |",
        "| check4 battle_position | YAML Reach 与括号一致 | `ShouldPromptForTarget` 3v3 实测、选后排拒选 |",
        "| check5 hooks | C# 里能搜到 cardId/statusId/动作类型名 | 钩子**逻辑**是否与描述一致 |",
        "| check6 presentation | **几乎恒 true** | §十二 演出、Pose、VFX |",
        "| check7 regression | 235/238 仅 `CardV09CatalogRegressionTests`（有描述且无 TODO） | 出牌、伤害、被动触发 |",
        "",
        f"- **PASSIVE_HANDLED 白名单**：{len(ph)} 张卡 check3 **整卡跳过**数值对比。",
        f"- **SPECIAL 豁免**：{len(SPECIAL)} 张（见下文）。",
        f"- **批量脚本改 asset**：Reach {len([r for r in rows if '位置括号' in r['flags']])} 张量级、被动/钩子若干张，**无逐张 PlayMode 证据**。",
        f"- **Unity 测试**：MCP Test Runner 跑 `Grimhand.Battle.Tests` 返回 **0 tests**（环境/程序集问题），`TargetPickRulesTests` / `BattleScopePassiveTests` **未在 CI 里绿过**。",
        "",
        "---",
        "",
        "## 二、统计摘要",
        "",
        f"| 维度 | 数量 |",
        f"|------|------|",
        f"| 总卡数 | {len(rows)} |",
        f"| strict 标 OK | {sum(1 for r in rows if r['strictOK'])} |",
        f"| 高风险（含未实装/批量修复标注） | {len(high)} |",
        f"| 中风险 | {len(med)} |",
        f"| 含位置括号 | {len(pos)} |",
        f"| 含「本场战斗」 | {len(scope)} |",
        f"| 仅 Catalog 回归 / 无行为测试 | {len(catalog_only)} |",
        f"| PASSIVE_HANDLED 跳过语义 | {len([r for r in rows if 'PASSIVE_HANDLED' in r['flags']])} |",
        "",
        "---",
        "",
        "## 三、明确有问题的卡（优先手测 / 补代码）",
        "",
    ]

    for r in high:
        lines += [
            f"### {r['idx']}. `{r['cardId']}` · {r['name']}",
            f"- **xlsx**：{r['effect']}",
            f"- **风险**：{r['risk']}",
        ]
        for n in r["notes"]:
            lines.append(f"- {n}")
        lines.append("")

    lines += [
        "---",
        "",
        "## 四、SPECIAL 豁免三张（strict OK 但不算「正常卡」）",
        "",
    ]
    for cid, note in SPECIAL.items():
        r = next(x for x in rows if x["cardId"] == cid)
        lines += [
            f"### `{cid}` · {r['name']}",
            f"- {note}",
            f"- xlsx：`{r['effect']}`",
            "",
        ]

    lines += [
        "---",
        "",
        "## 五、「本场战斗中」19 张 — 静态 OK ≠ 行为 OK",
        "",
        "清单见 `_battle_scope_cards_v09.json`。静态检查只要求：Permanent status 或已知钩子字符串存在。",
        "**以下逐张列出 xlsx、批量修复备注、待验证行为。**",
        "",
    ]
    for r in scope:
        lines.append(f"#### `{r['cardId']}` · {r['name']}")
        lines.append(f"- xlsx：{r['effect']}")
        if r["notes"]:
            for n in r["notes"]:
                lines.append(f"- ⚠ {n}")
        else:
            lines.append("- 静态通过；**缺行为测试**")
        lines.append("")

    lines += [
        "---",
        "",
        "## 六、批量改过 Reach 的位置卡（约 90 张）",
        "",
        "由 `fix_reach_on_assets.py` 按描述括号写入 YAML Reach。",
        "**只保证 YAML 数字与括号一致，不保证：**",
        "- 规划阶段是否弹出选目标",
        "- 结算时是否打在正确站位",
        "- 多 action 卡是否每个 action Reach 都对",
        "",
        "| # | cardId | 名称 | Reach期望 | 备注 |",
        "|---|--------|------|-----------|------|",
    ]
    for r in pos:
        exp = parse_position_reach(r["effect"])
        note = "; ".join(r["notes"][:2]) if r["notes"] else "—"
        lines.append(f"| {r['idx']} | `{r['cardId']}` | {r['name']} | {exp} | {note} |")

    lines += [
        "",
        "---",
        "",
        "## 七、全量 238 张逐卡表（按 xlsx 顺序）",
        "",
        "| # | cardId | 名称 | 风险 | 标签 | 测试 | 备注 |",
        "|---|--------|------|------|------|------|------|",
    ]
    for r in rows:
        flags = ", ".join(r["flags"]) or "—"
        notes = "；".join(r["notes"][:2]) if r["notes"] else "—"
        tr = r["testRef"][:40] + ("…" if len(r["testRef"]) > 40 else "")
        eff_short = r["effect"][:35] + ("…" if len(r["effect"]) > 35 else "")
        lines.append(
            f"| {r['idx']} | `{r['cardId']}` | {r['name']} | {r['risk']} | {flags} | {tr} | {notes} |"
        )

    lines += [
        "",
        "---",
        "",
        "## 八、建议下一步（讨论用）",
        "",
        "1. **先手测「高 + 中」里你常玩的牌**（尤其位置、应对、本场被动）。",
        "2. **补 check3**：应对卡 Condition 分段 parser；PASSIVE_HANDLED 缩小到「真有钩子」的子集。",
        "3. **check7 升级**：行为测试按角色分批，不能 235 张共用 Catalog 参数化就标 OK。",
        "4. **修 Unity Test Runner**（`UNITY_INCLUDE_TESTS` / asmdef），让 TargetPickRulesTests 真正跑起来。",
        "5. **单卡失败就回到 xlsx 第 2 步**，改 asset/引擎后再 strict + 手测，禁止只跑 batch fix。",
        "",
        "复现命令：`python Assets/_Project/Docs/_tools/verify_card_strict.py --card <cardId>`",
        "",
    ]

    OUT.write_text("\n".join(lines), encoding="utf-8")
    print(f"Wrote {OUT} ({len(rows)} cards, high={len(high)}, med={len(med)})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
