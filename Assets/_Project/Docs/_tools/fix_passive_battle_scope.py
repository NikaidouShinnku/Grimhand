#!/usr/bin/env python3
"""修复已知错误的被动/状态卡 asset Actions。"""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CARDS = ROOT / "Data" / "Cards"

ACTION_BLOCK = """  - Type: 3
    Target: 1
    Value: 0
    StatusId: {status}
    Stacks: 1
    Duration: -1
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

ANUBIS = """  - Type: 10
    Target: 1
    Value: 0
    StatusId: 
    Stacks: 1
    Duration: -1
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

TIDE_CHARGE = """  - Type: 0
    Target: 0
    Value: 12
    StatusId: 
    Stacks: 1
    Duration: -1
    ScaleWithAttack: 0
    ScaleWithDefense: 0
    AttackScalePercent: 100
    DefenseScalePercent: 100
    Condition: 0
    Reach: 0
    SplashBehindTarget: 0
    SplashPowerPercent: 100
    BackRowPowerPercent: 100
    IgnoreDefPercent: 0
    BonusIfTargetHpBelowPercent: 0
    BonusIfTargetHpBelowFlat: 8
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


def replace_actions(text: str, new_actions: str) -> str:
    return re.sub(
        r"  Actions:\n(?:  - Type:.*?\n(?:    .+\n)*?)+",
        f"  Actions:\n{new_actions}",
        text,
        count=1,
        flags=re.S,
    )


def fix_target_self(path: Path) -> None:
    text = path.read_text(encoding="utf-8")
    text = re.sub(r"Target: 0\n(\s+Value: 0\n\s+StatusId: rot_avatar)", r"Target: 1\n\1", text, count=1)
    path.write_text(text, encoding="utf-8")


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    fixes = {
        "w_heavy_armor": ACTION_BLOCK.format(status="heavy_armor"),
        "w_god_descends": ACTION_BLOCK.format(status="god_descends"),
        "d_final_blood_ritual": ACTION_BLOCK.format(status="final_blood_ritual"),
        "p_anubis_avatar": ANUBIS,
        "m_tide_charge": TIDE_CHARGE,
    }
    for cid, block in fixes.items():
        path = CARDS / f"Card_{cid}.asset"
        if not path.exists():
            print(f"skip missing {cid}")
            continue
        path.write_text(replace_actions(path.read_text(encoding="utf-8"), block), encoding="utf-8")
        print(f"fixed {cid}")

    rot = CARDS / "Card_p_rot_avatar.asset"
    if rot.exists():
        fix_target_self(rot)
        print("fixed p_rot_avatar Target→Self")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
