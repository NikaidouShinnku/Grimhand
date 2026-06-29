#!/usr/bin/env python3
"""Patch player Card_*.asset and Character_*.asset to match Gramhand card sheet."""
from __future__ import annotations

import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2] / "Data"
CARDS = ROOT / "Cards"
CHARS = ROOT / "Characters"
SCRIPT_GUID = "d1e8e1f9605a6fb4fa4e4fa5586de5ee"
CHAR_SCRIPT_GUID = "84c30176d181a5747a85983cdd126a0c"

# Existing guids from .meta files
GUIDS = {
    "w_basic_slash": "844da30287e5467395dcbbbd4888b2dd",
    "w_shield_block": "1a8c6acca3ec45b8a74753b378427b00",
    "w_power_cleave": "55a69b17b1054fbe8c3bed098ccef5a4",
    "w_taunt": "ba0fe2396a9c48d4b8b680f40afd8c17",
    "w_iron_parry": "4c6abdc96a944cd1980eb53292202ef2",
    "w_charge": "d5367f85d1c041f9b31974e33fdae3b4",
    "w_war_cry": "4f0d41c3ab054150af563896db46ce7c",
    "w_guardian": "561899d85e6c4131bac9f1aaf1d0c04a",
    "w_fatal_strike": "5a713138f93745e392a2d9511b64da2b",
    "w_unyielding": "4567d436621b41bb8319b3b3469ff6bc",
    "p_sand_ray": "40b6cb6a321a4d12ac8e17f725057c08",
    "p_bless": "cb1e2a8cacd84850ac1721066a4d6195",
    "p_solar_wrath": "5a110342e9a142cca41206e4dba9574d",
    "p_lifesteal": "f9d47d937e4b41559c16a6bb6f70ca8b",
    "p_decree": "86be297d405d4e61a37ce34f9726033b",
    "p_undead_curse": "5cf7b7b2ee9a4ff29b48c9fce165ce11",
    "p_scarab_shield": "57d519ea1b75420382a734faa363429a",
    "p_sand_barrier": "788fda69c68544ee9a6e439ec4015c36",
    "p_revive_bless": "aafaaa675a274768b390290eb1f446bc",
    "p_solar_judgment": "38f9645f0c4f4d9f809b4e4c6f33fda8",
    "d_shadow_claw": "81911c57da184dad97f9646394a9da1d",
    "d_devil_touch": "4afcbf17bcff4d5e88d30c1b32b8c92a",
    "d_blood_flame": "295eb981fb524ba9be99392540ab03fd",
    "d_soul_rip": "933053b0588c47148b2475dd52afb834",
    "d_dark_sacrifice": "717920915ad24d5ba0c81b4b8b5b948f",
    "d_demon_pact": "3e6d0a81afaa4ca187eaa1aa7d04637f",
    "d_vamp_aura": "771c13e11d934303b61721d1e1ddf087",
    "d_curse_chain": "3943faaa99fe4ab19da8114310f013bb",
    "d_hell_fire": "19472ba32ea64468a041917f0a65955b",
    "d_demon_lord": "24fa52cd31714a10af0b000f9d2703a5",
}

# New cards
GUIDS["w_defensive_stance"] = uuid.uuid4().hex
GUIDS["d_blood_tail"] = uuid.uuid4().hex

DealDamage = 0
GainBlock = 1
Heal = 2
ApplyStatus = 3
DrawCards = 7
Reflect = 8
BlockFromDmgPct = 9

DefaultEnemy = 0
Self = 1
FrontAlly = 2
BackAlly = 3
LastActionActor = 4
AllyFront = 9
AllyMiddle = 10
AllyBack = 11
AllEnemies = 12

ReachAny = 0
ReachFrontMiddle = 1
CondNone = 0
CondAttackOnSelf = 1

Attack = 0
Defense = 1
Status = 2


def action(**kw) -> dict:
    base = {
        "Type": 0, "Target": 0, "Value": 0, "StatusId": "", "Stacks": 1, "Duration": -1,
        "ScaleWithAttack": 0, "ScaleWithDefense": 0, "AttackScalePercent": 100,
        "DefenseScalePercent": 100, "Condition": 0, "Reach": 1, "SplashBehindTarget": 0,
        "SplashPowerPercent": 100, "BackRowPowerPercent": 100, "IgnoreDefPercent": 0,
        "BonusIfTargetHpBelowPercent": 0, "BonusIfTargetHpBelowFlat": 0,
        "BonusIfTargetHitThisTurnPercent": 0, "LifestealPercent": 0, "OnKillHealAmount": 0,
    }
    base.update(kw)
    return base


def atk(flat, pct=100, target=DefaultEnemy, reach=ReachFrontMiddle, ignore=0, hp_below=0,
        hp_bonus=0, hit_bonus=0, lifesteal=0, on_kill=0, splash=False, splash_pct=100):
    return action(
        Type=DealDamage, Target=target, Value=flat, ScaleWithAttack=1,
        AttackScalePercent=pct, Reach=reach if target != AllEnemies else ReachAny,
        IgnoreDefPercent=ignore, BonusIfTargetHpBelowPercent=hp_below,
        BonusIfTargetHpBelowFlat=hp_bonus, BonusIfTargetHitThisTurnPercent=hit_bonus,
        LifestealPercent=lifesteal, OnKillHealAmount=on_kill,
        SplashBehindTarget=1 if splash else 0, SplashPowerPercent=splash_pct,
    )


def def_block(flat, pct, target=Self):
    return action(Type=GainBlock, Target=target, Value=flat, ScaleWithDefense=1,
                  DefenseScalePercent=pct, Reach=ReachAny if target != Self else ReachFrontMiddle)


def heal_scaled(flat, pct, target=FrontAlly):
    return action(Type=Heal, Target=target, Value=flat, ScaleWithAttack=1,
                  AttackScalePercent=pct, Reach=ReachAny)


def self_dmg(v):
    return action(Type=DealDamage, Target=Self, Value=v)


def draw(n):
    return action(Type=DrawCards, Target=Self, Value=n)


def status(sid, stacks, duration, target, reach=ReachFrontMiddle):
    return action(Type=ApplyStatus, Target=target, StatusId=sid, Stacks=stacks,
                  Duration=duration, Reach=reach if target not in (FrontAlly,) else ReachAny)


def respond(reduction, reflect=0):
    acts = [
        action(Type=BlockFromDmgPct, Target=Self, Value=reduction, Condition=CondAttackOnSelf),
    ]
    if reflect > 0:
        acts.append(action(Type=Reflect, Target=LastActionActor, Value=reflect,
                           Condition=CondAttackOnSelf))
    return acts


def render_action(a: dict) -> str:
    lines = ["  - Type: {Type}".format(**a)]
    for k, v in a.items():
        if k == "Type":
            continue
        if k == "StatusId":
            lines.append(f"    StatusId: {v}")
        elif isinstance(v, str):
            lines.append(f"    {k}: {v}")
        else:
            lines.append(f"    {k}: {v}")
    return "\n".join(lines)


def render_card(card_id, display, owner, cost, ctype, keywords, actions):
    if keywords:
        kw_block = "  Keywords:\n" + "\n".join(f"  - {k}" for k in keywords)
    else:
        kw_block = "  Keywords: []"
    act_lines = "\n".join(render_action(a) for a in actions)
    return f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {SCRIPT_GUID}, type: 3}}
  m_Name: Card_{card_id}
  m_EditorClassIdentifier: Grimhand.Content::Grimhand.Content.CardDefinitionSO
  CardId: {card_id}
  DisplayName: "{display}"
  OwnerCharacterId: {owner}
  Cost: {cost}
  CardType: {ctype}
  Rarity: 0
{kw_block}
  Actions:
{act_lines}
  CardArt: {{fileID: 0}}
  CardFrame: {{fileID: 0}}
  CardIcon: {{fileID: 0}}
"""


def write_meta(path: Path, guid: str):
    meta = f"""fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
    path.write_text(meta, encoding="utf-8")


def write_card(card_id, **spec):
    path = CARDS / f"Card_{card_id}.asset"
    path.write_text(render_card(card_id, **spec), encoding="utf-8")
    meta = path.with_suffix(".asset.meta")
    if not meta.exists():
        write_meta(meta, GUIDS[card_id])
    print(f"  card {card_id}")


def deck_refs(entries):
    lines = []
    for cid, count in entries:
        for _ in range(count):
            lines.append(f"  - {{fileID: 11400000, guid: {GUIDS[cid]}, type: 2}}")
    return "\n".join(lines)


def write_character(name, char_id, display, slot, deck_entries):
    deck_block = deck_refs(deck_entries)
    text = f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {CHAR_SCRIPT_GUID}, type: 3}}
  m_Name: {name}
  m_EditorClassIdentifier: Grimhand.Content::Grimhand.Content.CharacterDefinitionSO
  CharacterId: {char_id}
  DisplayName: "{display}"
  Team: 0
  Slot: {slot}
  Level: 1
  MaxHp: {50 if slot == 1 else 40 if slot == 2 else 30}
  BaseAttack: {8 if slot == 1 else 6 if slot == 2 else 9}
  BaseDefense: {6 if slot == 1 else 4 if slot == 2 else 2}
  Speed: {7 if slot == 1 else 5 if slot == 2 else 6}
  Deck:
{deck_block}
  SkillPool: []
  EnemyRandomDeckSize: 8
  EnemySkillPickMin: 2
  EnemySkillPickMax: 4
"""
    path = CHARS / f"{name}.asset"
    path.write_text(text, encoding="utf-8")
    print(f"  character {name} ({sum(c for _, c in deck_entries)} cards)")


def main():
    print("Writing cards...")
    write_card("w_basic_slash", display="基础斩击", owner="char_knight", cost=1, ctype=Attack,
               keywords=[], actions=[atk(3, 80)])
    write_card("w_shield_block", display="举盾格挡", owner="char_knight", cost=1, ctype=Defense,
               keywords=[], actions=[def_block(2, 80)])
    write_card("w_defensive_stance", display="防御架势", owner="char_knight", cost=1, ctype=Defense,
               keywords=["parry"], actions=respond(50))
    write_card("w_power_cleave", display="猛力劈砍", owner="char_knight", cost=2, ctype=Attack,
               keywords=[], actions=[atk(5, 120, hp_below=50, hp_bonus=10)])
    write_card("w_taunt", display="嘲讽挑衅", owner="char_knight", cost=2, ctype=Defense,
               keywords=[], actions=[status("taunt", 1, 1, Self), def_block(0, 120)])
    write_card("w_iron_parry", display="铁壁弹反", owner="char_knight", cost=2, ctype=Defense,
               keywords=["parry"], actions=respond(30, 100))
    write_card("w_charge", display="战士冲锋", owner="char_knight", cost=3, ctype=Attack,
               keywords=[], actions=[atk(8, 160, ignore=50)])
    write_card("w_war_cry", display="战吼鼓舞", owner="char_knight", cost=1, ctype=Status,
               keywords=[], actions=[
                   status("attack_up", 3, 1, AllyFront, ReachAny),
                   status("attack_up", 3, 1, AllyMiddle, ReachAny),
                   status("attack_up", 3, 1, AllyBack, ReachAny),
               ])
    write_card("w_guardian", display="誓死守护", owner="char_knight", cost=2, ctype=Defense,
               keywords=[], actions=[status("guard", 1, 1, Self)])
    write_card("w_fatal_strike", display="致命打击", owner="char_knight", cost=3, ctype=Attack,
               keywords=[], actions=[atk(6, 180, hit_bonus=50)])
    write_card("w_unyielding", display="不屈意志", owner="char_knight", cost=0, ctype=Status,
               keywords=["exhaust"], actions=[status("unyielding", 1, -1, Self)])

    write_card("p_sand_ray", display="沙暴射线", owner="char_mage", cost=1, ctype=Attack,
               keywords=[], actions=[atk(3, 80)])
    write_card("p_bless", display="祈祷祝福", owner="char_mage", cost=1, ctype=Status,
               keywords=[], actions=[heal_scaled(2, 100)])
    write_card("p_solar_wrath", display="太阳之怒", owner="char_mage", cost=2, ctype=Attack,
               keywords=["aoe"], actions=[atk(3, 70, target=AllEnemies)])
    write_card("p_lifesteal", display="生命汲取", owner="char_mage", cost=2, ctype=Attack,
               keywords=[], actions=[atk(4, 100, lifesteal=50)])
    write_card("p_decree", display="法老权令", owner="char_mage", cost=2, ctype=Status,
               keywords=[], actions=[
                   draw(2),
                   status("attack_up", 3, 1, FrontAlly, ReachAny),
                   status("defense_up", 2, 1, FrontAlly, ReachAny),
               ])
    write_card("p_undead_curse", display="亡灵诅咒", owner="char_mage", cost=3, ctype=Attack,
               keywords=["poison"], actions=[
                   atk(6, 120, reach=ReachAny),
                   status("necrotic_poison", 1, 3, DefaultEnemy),
               ])
    write_card("p_scarab_shield", display="圣甲虫护盾", owner="char_mage", cost=1, ctype=Defense,
               keywords=[], actions=[def_block(0, 120, FrontAlly)])
    write_card("p_sand_barrier", display="沙尘结界", owner="char_mage", cost=2, ctype=Defense,
               keywords=[], actions=[
                   def_block(0, 100, AllyFront),
                   def_block(0, 100, AllyMiddle),
                   def_block(0, 100, AllyBack),
               ])
    write_card("p_revive_bless", display="复活祝福", owner="char_mage", cost=3, ctype=Status,
               keywords=["exhaust"], actions=[status("revive_blessing", 1, -1, FrontAlly, ReachAny)])
    write_card("p_solar_judgment", display="太阳审判", owner="char_mage", cost=4, ctype=Attack,
               keywords=[], actions=[atk(10, 200, reach=ReachAny)])

    write_card("d_shadow_claw", display="暗影爪击", owner="char_ranger", cost=1, ctype=Attack,
               keywords=[], actions=[atk(3, 80)])
    write_card("d_devil_touch", display="恶魔之触", owner="char_ranger", cost=1, ctype=Attack,
               keywords=[], actions=[atk(2, 50, lifesteal=100)])
    write_card("d_blood_tail", display="血尾贯穿", owner="char_ranger", cost=2, ctype=Attack,
               keywords=[], actions=[atk(3, 100, splash=True, splash_pct=80)])
    write_card("d_blood_flame", display="血焰爆发", owner="char_ranger", cost=2, ctype=Attack,
               keywords=["sacrifice"], actions=[self_dmg(8), atk(8, 130)])
    write_card("d_soul_rip", display="灵魂撕裂", owner="char_ranger", cost=2, ctype=Attack,
               keywords=[], actions=[atk(4, 80, reach=ReachAny, ignore=100)])
    write_card("d_dark_sacrifice", display="暗黑献祭", owner="char_ranger", cost=3, ctype=Attack,
               keywords=["sacrifice"], actions=[self_dmg(15), atk(12, 170)])
    write_card("d_demon_pact", display="恶魔契约", owner="char_ranger", cost=2, ctype=Status,
               keywords=["sacrifice"], actions=[self_dmg(5), draw(2), status("attack_up", 3, 1, Self)])
    write_card("d_vamp_aura", display="吸血光环", owner="char_ranger", cost=1, ctype=Status,
               keywords=[], actions=[status("vamp_aura", 30, 1, Self)])
    write_card("d_curse_chain", display="诅咒之链", owner="char_ranger", cost=2, ctype=Attack,
               keywords=[], actions=[atk(3, 100, reach=ReachAny), status("attack_down", 3, 2, DefaultEnemy)])
    write_card("d_hell_fire", display="地狱烈焰", owner="char_ranger", cost=3, ctype=Attack,
               keywords=["aoe", "sacrifice"], actions=[self_dmg(8), atk(5, 100, target=AllEnemies)])
    write_card("d_demon_lord", display="魔王降临", owner="char_ranger", cost=4, ctype=Attack,
               keywords=["sacrifice"], actions=[self_dmg(20), atk(15, 200, reach=ReachAny, on_kill=30)])

    print("Writing initial decks...")
    write_character("Character_Knight", "char_knight", "战士", 1, [
        ("w_basic_slash", 3), ("w_shield_block", 2), ("w_defensive_stance", 1),
        ("w_iron_parry", 1), ("w_author_realm_strike", 1),
    ])
    write_character("Character_Mage", "char_mage", "法老", 2, [
        ("p_sand_ray", 3), ("p_bless", 2), ("p_scarab_shield", 1),
        ("p_undead_curse", 1),
    ])
    write_character("Character_Ranger", "char_ranger", "恶魔", 3, [
        ("d_shadow_claw", 3), ("d_blood_armor", 2), ("d_devil_touch", 1),
        ("d_blood_tail", 1),
    ])
    print("Done.")


if __name__ == "__main__":
    main()
