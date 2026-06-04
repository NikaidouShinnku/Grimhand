#if UNITY_EDITOR
using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Battle.Status;
using Grimhand.Content;
using UnityEditor;
using UnityEngine;

namespace Grimhand.Content.Editor
{
    /// <summary>根据数值策划表 v2 生成玩家角色与 30 张卡牌。</summary>
    public static class BalanceV2ContentGenerator
    {
        const string Root = "Assets/_Project/Data";

        public struct PlayerContent
        {
            public CharacterDefinitionSO Warrior;
            public CharacterDefinitionSO Pharaoh;
            public CharacterDefinitionSO Demon;
        }

        public static PlayerContent GeneratePlayerContent()
        {
            EnsureFolder(Root + "/Cards");
            EnsureFolder(Root + "/Characters");

            var cards = CreatePlayerCards();
            return new PlayerContent
            {
                Warrior = SaveCharacter("Character_Knight", "char_knight", "战士", TeamSide.Player,
                    FormationSlot.Front, 1, 50, 8, 6, 4,
                    BuildDeck(cards.Warrior)),
                Pharaoh = SaveCharacter("Character_Mage", "char_mage", "法老", TeamSide.Player,
                    FormationSlot.Middle, 1, 40, 6, 4, 6,
                    BuildDeck(cards.Pharaoh)),
                Demon = SaveCharacter("Character_Ranger", "char_ranger", "恶魔", TeamSide.Player,
                    FormationSlot.Back, 1, 30, 9, 2, 8,
                    BuildDeck(cards.Demon))
            };
        }

        struct WarriorCards
        {
            public CardDefinitionSO BasicSlash, ShieldBlock, PowerCleave, Taunt, IronParry, Charge,
                WarCry, Guardian, FatalStrike, Unyielding;
        }

        struct PharaohCards
        {
            public CardDefinitionSO SandRay, Bless, SolarWrath, LifeSteal, Decree, UndeadCurse,
                ScarabShield, SandBarrier, ReviveBless, SolarJudgment;
        }

        struct DemonCards
        {
            public CardDefinitionSO ShadowClaw, DevilTouch, BloodFlame, SoulRip, DarkSacrifice, DemonPact,
                VampAura, CurseChain, HellFire, DemonLord;
        }

        struct AllPlayerCards
        {
            public CardDefinitionSO[] Warrior;
            public CardDefinitionSO[] Pharaoh;
            public CardDefinitionSO[] Demon;
        }

        static AllPlayerCards CreatePlayerCards()
        {
            var warrior = CreateWarriorCards();
            var pharaoh = CreatePharaohCards();
            var demon = CreateDemonCards();

            return new AllPlayerCards
            {
                Warrior = new[]
                {
                    warrior.BasicSlash, warrior.ShieldBlock, warrior.PowerCleave, warrior.Taunt,
                    warrior.IronParry, warrior.Charge, warrior.WarCry, warrior.Guardian,
                    warrior.FatalStrike, warrior.Unyielding
                },
                Pharaoh = new[]
                {
                    pharaoh.SandRay, pharaoh.Bless, pharaoh.SolarWrath, pharaoh.LifeSteal,
                    pharaoh.Decree, pharaoh.UndeadCurse, pharaoh.ScarabShield, pharaoh.SandBarrier,
                    pharaoh.ReviveBless, pharaoh.SolarJudgment
                },
                Demon = new[]
                {
                    demon.ShadowClaw, demon.DevilTouch, demon.BloodFlame, demon.SoulRip,
                    demon.DarkSacrifice, demon.DemonPact, demon.VampAura, demon.CurseChain,
                    demon.HellFire, demon.DemonLord
                }
            };
        }

        static WarriorCards CreateWarriorCards()
        {
            return new WarriorCards
            {
                BasicSlash = SaveCard("w_basic_slash", "基础斩击", "char_knight", 1, CardType.Attack,
                    Kw("melee"), AtkDmg(3, 80)),
                ShieldBlock = SaveCard("w_shield_block", "举盾格挡", "char_knight", 1, CardType.Defense,
                    Kw("block"), DefBlock(2, 150)),
                PowerCleave = SaveCard("w_power_cleave", "猛力劈砍", "char_knight", 2, CardType.Attack,
                    Kw("melee"), AtkDmg(5, 120)),
                Taunt = SaveCard("w_taunt", "嘲讽挑衅", "char_knight", 2, CardType.Defense,
                    Kw("taunt"),
                    Block(15)),
                IronParry = SaveCard("w_iron_parry", "铁壁弹反", "char_knight", 2, CardType.Defense,
                    Kw("parry"), Merge(Parry(50, 100))),
                Charge = SaveCard("w_charge", "战士冲锋", "char_knight", 3, CardType.Attack,
                    Kw("melee"), AtkDmg(8, 160)),
                WarCry = SaveCard("w_war_cry", "战吼鼓舞", "char_knight", 1, CardType.Status,
                    Kw("buff"),
                    AllyBlock(EffectTarget.AllyFrontSlot, 3),
                    AllyBlock(EffectTarget.AllyMiddleSlot, 3),
                    AllyBlock(EffectTarget.AllyBackSlot, 3)),
                Guardian = SaveCard("w_guardian", "誓死守护", "char_knight", 2, CardType.Defense,
                    Kw("guard"), Block(12)),
                FatalStrike = SaveCard("w_fatal_strike", "致命打击", "char_knight", 3, CardType.Attack,
                    Kw("melee"), AtkDmg(6, 180)),
                Unyielding = SaveCard("w_unyielding", "不屈意志", "char_knight", 0, CardType.Status,
                    Kw("survival"), Heal(20, EffectTarget.Self))
            };
        }

        static PharaohCards CreatePharaohCards()
        {
            return new PharaohCards
            {
                SandRay = SaveCard("p_sand_ray", "沙暴射线", "char_mage", 1, CardType.Attack,
                    Kw("magic"), AtkDmg(3, 80)),
                Bless = SaveCard("p_bless", "祈祷祝福", "char_mage", 1, CardType.Status,
                    Kw("heal"), Heal(12, EffectTarget.FrontAlly)),
                SolarWrath = SaveCard("p_solar_wrath", "太阳之怒", "char_mage", 2, CardType.Attack,
                    Kw("aoe", "magic"), Merge(AoeDmg(3, 70))),
                LifeSteal = SaveCard("p_lifesteal", "生命汲取", "char_mage", 2, CardType.Attack,
                    Kw("magic", "lifesteal"),
                    AtkDmg(4, 100),
                    Heal(5, EffectTarget.Self)),
                Decree = SaveCard("p_decree", "法老权令", "char_mage", 2, CardType.Status,
                    Kw("buff"),
                    Draw(2),
                    AllyBlock(EffectTarget.AllyFrontSlot, 3)),
                UndeadCurse = SaveCard("p_undead_curse", "亡灵诅咒", "char_mage", 3, CardType.Attack,
                    Kw("magic", "poison"),
                    AtkDmg(6, 120),
                    Poison(5)),
                ScarabShield = SaveCard("p_scarab_shield", "圣甲虫护盾", "char_mage", 1, CardType.Defense,
                    Kw("shield"), Block(12, EffectTarget.FrontAlly)),
                SandBarrier = SaveCard("p_sand_barrier", "沙尘结界", "char_mage", 2, CardType.Defense,
                    Kw("shield"),
                    AllyBlock(EffectTarget.AllyFrontSlot, 8),
                    AllyBlock(EffectTarget.AllyMiddleSlot, 8),
                    AllyBlock(EffectTarget.AllyBackSlot, 8)),
                ReviveBless = SaveCard("p_revive_bless", "复活祝福", "char_mage", 3, CardType.Status,
                    Kw("revive"), Heal(10, EffectTarget.FrontAlly)),
                SolarJudgment = SaveCard("p_solar_judgment", "太阳审判", "char_mage", 4, CardType.Attack,
                    Kw("magic"), AtkDmg(10, 200))
            };
        }

        static DemonCards CreateDemonCards()
        {
            return new DemonCards
            {
                ShadowClaw = SaveCard("d_shadow_claw", "暗影爪击", "char_ranger", 1, CardType.Attack,
                    Kw("melee"), AtkDmg(3, 80)),
                DevilTouch = SaveCard("d_devil_touch", "恶魔之触", "char_ranger", 1, CardType.Attack,
                    Kw("lifesteal"),
                    AtkDmg(2, 60),
                    Heal(7, EffectTarget.Self)),
                BloodFlame = SaveCard("d_blood_flame", "血焰爆发", "char_ranger", 2, CardType.Attack,
                    Kw("sacrifice"),
                    SelfDmg(8),
                    AtkDmg(8, 130)),
                SoulRip = SaveCard("d_soul_rip", "灵魂撕裂", "char_ranger", 2, CardType.Attack,
                    Kw("melee"), AtkDmg(5, 120)),
                DarkSacrifice = SaveCard("d_dark_sacrifice", "暗黑献祭", "char_ranger", 3, CardType.Attack,
                    Kw("sacrifice"),
                    SelfDmg(15),
                    AtkDmg(12, 170)),
                DemonPact = SaveCard("d_demon_pact", "恶魔契约", "char_ranger", 2, CardType.Status,
                    Kw("sacrifice"),
                    SelfDmg(5),
                    Draw(2)),
                VampAura = SaveCard("d_vamp_aura", "吸血光环", "char_ranger", 1, CardType.Status,
                    Kw("lifesteal"), Heal(3, EffectTarget.Self)),
                CurseChain = SaveCard("d_curse_chain", "诅咒之链", "char_ranger", 2, CardType.Attack,
                    Kw("curse"),
                    AtkDmg(3, 100),
                    Slow(1, 2)),
                HellFire = SaveCard("d_hell_fire", "地狱烈焰", "char_ranger", 3, CardType.Attack,
                    Kw("aoe", "sacrifice"),
                    SelfDmg(8),
                    AtkDmg(5, 100, EffectTarget.EnemyFrontSlot),
                    AtkDmg(5, 100, EffectTarget.EnemyMiddleSlot),
                    AtkDmg(5, 100, EffectTarget.EnemyBackSlot)),
                DemonLord = SaveCard("d_demon_lord", "魔王降临", "char_ranger", 4, CardType.Attack,
                    Kw("sacrifice"),
                    SelfDmg(20),
                    AtkDmg(15, 200))
            };
        }

        static EffectActionDefinition AtkDmg(int fixedVal, int atkPercent, EffectTarget target = EffectTarget.DefaultEnemy)
        {
            return new EffectActionDefinition
            {
                Type = EffectActionType.DealDamage,
                Target = target,
                Value = fixedVal,
                ScaleWithAttack = true,
                AttackScalePercent = atkPercent
            };
        }

        static EffectActionDefinition DefBlock(int fixedVal, int defPercent)
        {
            return new EffectActionDefinition
            {
                Type = EffectActionType.GainBlock,
                Target = EffectTarget.Self,
                Value = fixedVal,
                ScaleWithDefense = true,
                DefenseScalePercent = defPercent
            };
        }

        static EffectActionDefinition Block(int amount, EffectTarget target = EffectTarget.Self)
        {
            return new EffectActionDefinition
            {
                Type = EffectActionType.GainBlock,
                Target = target,
                Value = amount
            };
        }

        static EffectActionDefinition AllyBlock(EffectTarget slot, int amount) => Block(amount, slot);

        static EffectActionDefinition Heal(int amount, EffectTarget target)
        {
            return new EffectActionDefinition
            {
                Type = EffectActionType.Heal,
                Target = target,
                Value = amount
            };
        }

        static EffectActionDefinition SelfDmg(int amount)
        {
            return new EffectActionDefinition
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.Self,
                Value = amount
            };
        }

        static EffectActionDefinition Draw(int count)
        {
            return new EffectActionDefinition
            {
                Type = EffectActionType.DrawCards,
                Target = EffectTarget.Self,
                Value = count
            };
        }

        static EffectActionDefinition Poison(int stacks)
        {
            return new EffectActionDefinition
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.DefaultEnemy,
                StatusId = StatusCatalog.Poison,
                Stacks = stacks
            };
        }

        static EffectActionDefinition Slow(int stacks, int duration)
        {
            return new EffectActionDefinition
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.DefaultEnemy,
                StatusId = StatusCatalog.Slow,
                Stacks = stacks,
                Duration = duration
            };
        }

        static EffectActionDefinition[] AoeDmg(int fixedVal, int atkPercent)
        {
            return new[]
            {
                AtkDmg(fixedVal, atkPercent, EffectTarget.EnemyFrontSlot),
                AtkDmg(fixedVal, atkPercent, EffectTarget.EnemyMiddleSlot),
                AtkDmg(fixedVal, atkPercent, EffectTarget.EnemyBackSlot)
            };
        }

        static EffectActionDefinition[] Parry(int reductionPercent, int reflectPercent)
        {
            return new[]
            {
                new EffectActionDefinition
                {
                    Type = EffectActionType.GainBlockFromLastDamagePercent,
                    Target = EffectTarget.Self,
                    Value = reductionPercent,
                    Condition = ReactionConditionType.LastActionAttackOnSelf
                },
                new EffectActionDefinition
                {
                    Type = EffectActionType.ReflectLastDamageToAttacker,
                    Target = EffectTarget.LastActionActor,
                    Value = reflectPercent,
                    Condition = ReactionConditionType.LastActionAttackOnSelf
                }
            };
        }

        static EffectActionDefinition[] Merge(params EffectActionDefinition[] actions) => actions;

        static CardDefinitionSO SaveCard(
            string id,
            string displayName,
            string owner,
            int cost,
            CardType cardType,
            string[] keywords,
            params EffectActionDefinition[] actions)
        {
            var path = $"{Root}/Cards/Card_{id}.asset";
            var card = AssetDatabase.LoadAssetAtPath<CardDefinitionSO>(path);
            if (card == null)
            {
                card = ScriptableObject.CreateInstance<CardDefinitionSO>();
                AssetDatabase.CreateAsset(card, path);
            }

            card.CardId = id;
            card.DisplayName = displayName;
            card.OwnerCharacterId = owner;
            card.Cost = cost;
            card.CardType = cardType;
            card.Keywords.Clear();
            if (keywords != null)
                card.Keywords.AddRange(keywords);
            card.Actions.Clear();
            card.Actions.AddRange(actions);
            EditorUtility.SetDirty(card);
            return card;
        }

        static CharacterDefinitionSO SaveCharacter(
            string assetName,
            string charId,
            string displayName,
            TeamSide team,
            FormationSlot slot,
            int level,
            int hp,
            int atk,
            int def,
            int spd,
            CardDefinitionSO[] deck)
        {
            var path = $"{Root}/Characters/{assetName}.asset";
            var character = AssetDatabase.LoadAssetAtPath<CharacterDefinitionSO>(path);
            if (character == null)
            {
                character = ScriptableObject.CreateInstance<CharacterDefinitionSO>();
                AssetDatabase.CreateAsset(character, path);
            }

            character.CharacterId = charId;
            character.DisplayName = displayName;
            character.Team = team;
            character.Slot = slot;
            character.Level = level;
            character.MaxHp = hp;
            character.BaseAttack = atk;
            character.BaseDefense = def;
            character.Speed = spd;
            character.Deck.Clear();
            character.Deck.AddRange(deck);
            EditorUtility.SetDirty(character);
            return character;
        }

        static string[] Kw(params string[] ids) => ids;

        static CardDefinitionSO[] BuildDeck(params CardDefinitionSO[] cards) => cards;

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var parts = path.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
