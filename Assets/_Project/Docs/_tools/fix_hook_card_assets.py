#!/usr/bin/env python3
"""补全 HOOK 卡 asset StatusId / Actions。"""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CARDS = ROOT / "Data" / "Cards"

GUARDIAN = """  - Type: 3
    Target: 1
    Value: 0
    StatusId: guard
    Stacks: 1
    Duration: 1
    ScaleWithAttack: 0
    ScaleWithDefense: 0
    AttackScalePercent: 100
    DefenseScalePercent: 100
    Condition: 0
    Reach: 1
    SplashBehindTarget: 0
    SplashPowerPercent: 100
    BackRowPowerPercent: 100
    IgnoreDefPercent: 0
    BonusIfTargetHpBelowPercent: 0
    BonusIfTargetHpBelowFlat: 0
    BonusIfTargetHitThisTurnPercent: 0
    BonusIfTargetHasStatusId: 
    BonusIfTargetHasStatusFlat: 0
    LifestealPercent: 0
    HealMaxHpPercent: 0
    OnKillHealAmount: 0
    HitCount: 1
    AlternateAttackScalePercent: 0
    AlternateValue: 0
    UseAlternateIfTargetHasDebuff: 0
    AlternateAttackScaleIfActorUsedAttack: 0
    AlternateValueIfActorUsedAttack: 0
    DamageMultiplierPercentIfRespondArmed: 100
    SelfDamageFlat: 0
    RepeatPerEnemyAttackCardThisTurn: 0
    FallbackBlockDefenseScalePercent: 100
    FallbackBlockValue: 0
    SummonCharacterId: 
    GrantInvulnerableOnRespondArm: 0
    LifestealUnblockedOnly: 0
"""

STATUS_PATCH = {
    "m_final_summon": "final_summon_pending",
    "m_king_summon_workshop": "bone_workshop",
}


def replace_actions(text: str, block: str) -> str:
    if "Actions:\n\n" in text or re.search(r"Actions:\s*\n\s*CardArt:", text):
        return re.sub(r"  Actions:\s*\n\s*CardArt:", f"  Actions:\n{block}  CardArt:", text, count=1)
    return re.sub(
        r"  Actions:\n(?:  - Type:.*?\n(?:    .+\n)*?)+",
        f"  Actions:\n{block}",
        text,
        count=1,
        flags=re.S,
    )


def patch_status_id(text: str, sid: str) -> str:
    return re.sub(r"StatusId: \S*", f"StatusId: {sid}", text, count=1)


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    g = CARDS / "Card_w_guardian.asset"
    if g.exists():
        g.write_text(replace_actions(g.read_text(encoding="utf-8"), GUARDIAN), encoding="utf-8")
        print("fixed w_guardian")

    for cid, sid in STATUS_PATCH.items():
        path = CARDS / f"Card_{cid}.asset"
        if path.exists():
            path.write_text(patch_status_id(path.read_text(encoding="utf-8"), sid), encoding="utf-8")
            print(f"fixed {cid} → {sid}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
