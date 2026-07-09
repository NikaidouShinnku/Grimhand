#!/usr/bin/env python3
"""从 xlsx 效果文本解析位置括号 → TargetReach 枚举值（与 EffectEnums.cs 一致）。"""
from __future__ import annotations

import re

# TargetReach: Any=0, FrontAndMiddle=1, BackOnly=2, MiddleAndBack=3
REACH_ANY = 0
REACH_FRONT_MID = 1
REACH_BACK_ONLY = 2
REACH_MID_BACK = 3

RE_POSITION = re.compile(r"【([^】]+)】")


def parse_position_reach(desc: str) -> int | None:
    """返回 None 表示无位置括号或 AOE/随机/自身等非单目标选敌场景由调用方处理。"""
    if not desc:
        return None
    for raw in RE_POSITION.findall(desc):
        label = raw.strip()
        if not re.match(r"^[前中后/、\\]+$", label):
            continue
        normalized = label.replace("、", "/").replace("\\", "/")
        if normalized in ("前/中/后", "前/后/中", "中/前/后"):
            return REACH_ANY
        if normalized in ("前/中", "中/前"):
            return REACH_FRONT_MID
        if normalized in ("中/后", "后/中"):
            return REACH_MID_BACK
        if normalized in ("后", "后排"):
            return REACH_BACK_ONLY
        if normalized in ("前", "前排"):
            return REACH_FRONT_MID
    return None


def expects_manual_enemy_pick(desc: str, actions: list[dict]) -> bool:
    """描述含位置括号且 action 指向单个敌人时需要先选目标。"""
    if parse_position_reach(desc) is None:
        return False
    if any(x in (desc or "") for x in ("全体", "所有敌人", "AOE", "随机")):
        return False
    directed = {0, 1}  # DefaultEnemy, ManualSelected — 与 asset Target 字段一致
    pick_types = {0, 3, 4, 23, 27, 28, 29, 30, 32, 16, 18, 21, 22, 23, 37}  # damage-like
    for a in actions:
        if a.get("Condition", 0) != 0:
            continue
        t = a.get("Target", 0)
        if t == 0 and a.get("Type") in pick_types:
            return True
    return False


def reach_matches_desc(desc: str, actions: list[dict]) -> tuple[bool, str]:
    expected = parse_position_reach(desc)
    if expected is None:
        return True, ""
    # 怪物应对卡：描述中的【前/中】对应应对触发伤害，无无条件出牌 action
    if actions and all(a.get("Condition", 0) != 0 for a in actions if a.get("Type") in (0, 3, 21, 23, 27, 28, 29, 30, 32, 37)):
        return True, ""
    reaches = [
        a.get("Reach", 1)
        for a in actions
        if a.get("Condition", 0) == 0 and a.get("Target", 0) == 0
        and a.get("Type") in (0, 3, 4, 16, 18, 21, 22, 23, 27, 28, 29, 30, 32, 37)
    ]
    if not reaches:
        return False, f"位置括号期望 Reach={expected} 但无对应 action"
    bad = [r for r in reaches if r != expected]
    if bad:
        return False, f"Reach 不一致: 期望={expected} 实际={reaches}"
    return True, ""
