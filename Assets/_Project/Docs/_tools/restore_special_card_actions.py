#!/usr/bin/env python3
"""Restore Actions blocks wiped from special/mechanic cards."""
from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CARDS = ROOT / "Data" / "Cards"

ACTION_TAIL = """    ScaleWithAttack: 0
    ScaleWithDefense: 0
    AttackScalePercent: 100
    DefenseScalePercent: 100
    Condition: {condition}
    Reach: {reach}
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


def action_block(
    *,
    type_id: int,
    target: int,
    value: int,
    status_id: str = "",
    stacks: int = 0,
    duration: int = -1,
    condition: int = 0,
    reach: int = 0,
) -> str:
    return (
        f"  - Type: {type_id}\n"
        f"    Target: {target}\n"
        f"    Value: {value}\n"
        f"    StatusId: {status_id}\n"
        f"    Stacks: {stacks}\n"
        f"    Duration: {duration}\n"
        + ACTION_TAIL.format(condition=condition, reach=reach)
    )


RESTORES: dict[str, str] = {
    "w_guardian": action_block(
        type_id=3, target=1, value=0, status_id="guard", stacks=1, duration=1, reach=1
    ),
    "m_bat_shadow_dodge": action_block(
        type_id=15, target=1, value=60, duration=1, reach=1
    ),
    "m_queen_command": action_block(
        type_id=13, target=1, value=0, condition=1, reach=1
    ),
}


def restore(card_id: str, block: str) -> bool:
    path = CARDS / f"Card_{card_id}.asset"
    text = path.read_text(encoding="utf-8")
    new_text = re.sub(
        r"  Actions:\s*\n\s*\n  CardArt:",
        "  Actions:\n" + block + "  CardArt:",
        text,
        count=1,
    )
    if new_text == text:
        return False
    path.write_text(new_text, encoding="utf-8")
    return True


def main() -> None:
    for cid, block in RESTORES.items():
        ok = restore(cid, block)
        print(f"{'OK' if ok else 'SKIP'} {cid}")


if __name__ == "__main__":
    main()
