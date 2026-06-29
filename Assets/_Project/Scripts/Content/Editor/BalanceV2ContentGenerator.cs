#if UNITY_EDITOR
using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Battle.Status;
using Grimhand.Content;
using UnityEditor;
using UnityEngine;

namespace Grimhand.Content.Editor
{
    /// <summary>根据 Grimhand实际卡牌遗物总览表.xlsx 生成玩家 42 张卡牌。批量更新请优先运行 Docs/_tools/apply_overview_sheet.py。</summary>
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
                    FormationSlot.Front, 1, 50, 8, 6, 7,
                    BuildInitialWarriorDeck(cards.Warrior)),
                Pharaoh = SaveCharacter("Character_Mage", "char_mage", "法老", TeamSide.Player,
                    FormationSlot.Middle, 1, 40, 6, 4, 5,
                    BuildInitialPharaohDeck(cards.Pharaoh)),
                Demon = SaveCharacter("Character_Ranger", "char_ranger", "恶魔", TeamSide.Player,
                    FormationSlot.Back, 1, 30, 9, 2, 6,
                    BuildInitialDemonDeck(cards.Demon))
            };
        }

        struct WarriorCards
        {
            public CardDefinitionSO BasicSlash, ShieldBlock, DefensiveStance, PowerCleave, Taunt, IronParry, Charge,
                WarCry, Guardian, FatalStrike, Unyielding, AuthorRealmStrike;
        }

        struct PharaohCards
        {
            public CardDefinitionSO SandRay, Bless, SolarWrath, LifeSteal, Decree, UndeadCurse,
                ScarabShield, SandBarrier, ReviveBless, SolarJudgment;
        }

        struct DemonCards
        {
            public CardDefinitionSO ShadowClaw, DevilTouch, BloodTail, BloodArmor, BloodFlame, SoulRip, DarkSacrifice, DemonPact,
                VampAura, CurseChain, HellFire, DemonLord;
        }

        struct AllPlayerCards
        {
            public WarriorCards Warrior;
            public PharaohCards Pharaoh;
            public DemonCards Demon;
        }

        static AllPlayerCards CreatePlayerCards()
        {
            return new AllPlayerCards
            {
                Warrior = CreateWarriorCards(),
                Pharaoh = CreatePharaohCards(),
                Demon = CreateDemonCards()
            };
        }

        static WarriorCards CreateWarriorCards()
        {
            return new WarriorCards
            {
                BasicSlash = SaveCard("w_basic_slash", "基础斩击", "char_knight", 1, CardType.Attack,
                    null, CardRarity.Common, AtkDmg(5, 80)),
                ShieldBlock = SaveCard("w_shield_block", "举盾格挡", "char_knight", 1, CardType.Defense,
                    null, CardRarity.Common, DefBlock(3, 80)),
                DefensiveStance = SaveCard("w_defensive_stance", "防御架势", "char_knight", 1, CardType.Defense,
                    Kw("parry"), CardRarity.Common, RespondAttack(50)),
                PowerCleave = SaveCard("w_power_cleave", "猛力劈砍", "char_knight", 2, CardType.Attack,
                    null, CardRarity.Common, AtkDmg(7, 120, bonusHpBelowPercent: 50, bonusHpBelowFlat: 10)),
                Taunt = SaveCard("w_taunt", "嘲讽挑衅", "char_knight", 2, CardType.Defense,
                    null, CardRarity.Rare,
                    ApplyStat(StatusCatalog.Taunt, 1, 1, EffectTarget.Self),
                    DefBlock(0, 120)),
                IronParry = SaveCard("w_iron_parry", "铁壁弹反", "char_knight", 2, CardType.Defense,
                    Kw("parry"), CardRarity.Rare, Merge(RespondAttack(30, 100))),
                Charge = SaveCard("w_charge", "战士冲锋", "char_knight", 3, CardType.Attack,
                    null, CardRarity.Rare, AtkDmg(10, 160, ignoreDefPercent: 50)),
                WarCry = SaveCard("w_war_cry", "战吼鼓舞", "char_knight", 1, CardType.Status,
                    null, CardRarity.Common, Merge(TeamAttackUp(3, 1))),
                Guardian = SaveCard("w_guardian", "誓死守护", "char_knight", 2, CardType.Defense,
                    null, CardRarity.Rare, ApplyStat(StatusCatalog.Guard, 1, 1, EffectTarget.Self)),
                FatalStrike = SaveCard("w_fatal_strike", "致命打击", "char_knight", 3, CardType.Attack,
                    null, CardRarity.Rare, AtkDmg(8, 180, bonusHitThisTurnPercent: 50)),
                Unyielding = SaveCard("w_unyielding", "不屈意志", "char_knight", 0, CardType.Status,
                    Kw("exhaust"), CardRarity.Epic,
                    ApplyStat(StatusCatalog.Unyielding, 1, -1, EffectTarget.Self)),
                AuthorRealmStrike = SaveCard("w_author_realm_strike", "作者境的一击", "char_knight", 0,
                    CardType.Attack,
                    Kw("aoe"),
                    CardRarity.Legendary,
                    FixedAoeDmg(9999))
            };
        }

        static PharaohCards CreatePharaohCards()
        {
            return new PharaohCards
            {
                SandRay = SaveCard("p_sand_ray", "沙暴射线", "char_mage", 1, CardType.Attack,
                    null, CardRarity.Common, AtkDmg(5, 80)),
                Bless = SaveCard("p_bless", "祈祷祝福", "char_mage", 1, CardType.Status,
                    null, CardRarity.Common, HealScaled(2, 100, EffectTarget.FrontAlly)),
                SolarWrath = SaveCard("p_solar_wrath", "太阳之怒", "char_mage", 2, CardType.Attack,
                    Kw("aoe"), CardRarity.Rare, Merge(AoeDmg(5, 70))),
                LifeSteal = SaveCard("p_lifesteal", "生命汲取", "char_mage", 2, CardType.Attack,
                    null, CardRarity.Rare, AtkDmg(6, 100, lifestealPercent: 50)),
                Decree = SaveCard("p_decree", "法老权令", "char_mage", 2, CardType.Status,
                    null, CardRarity.Rare,
                    Draw(2),
                    ApplyStat(StatusCatalog.AttackUp, 3, 1, EffectTarget.FrontAlly),
                    ApplyStat(StatusCatalog.DefenseUp, 2, 1, EffectTarget.FrontAlly)),
                UndeadCurse = SaveCard("p_undead_curse", "亡灵诅咒", "char_mage", 3, CardType.Attack,
                    Kw("poison"), CardRarity.Epic,
                    AtkDmg(7, 120, reach: TargetReach.Any),
                    ApplyStat(StatusCatalog.Poison, 5, -1, EffectTarget.DefaultEnemy)),
                ScarabShield = SaveCard("p_scarab_shield", "圣甲虫护盾", "char_mage", 1, CardType.Defense,
                    null, CardRarity.Common, AllyDefBlock(EffectTarget.FrontAlly, 0, 120)),
                SandBarrier = SaveCard("p_sand_barrier", "沙尘结界", "char_mage", 2, CardType.Defense,
                    null, CardRarity.Common,
                    AllyDefBlock(EffectTarget.AllyFrontSlot, 0, 100),
                    AllyDefBlock(EffectTarget.AllyMiddleSlot, 0, 100),
                    AllyDefBlock(EffectTarget.AllyBackSlot, 0, 100)),
                ReviveBless = SaveCard("p_revive_bless", "复活祝福", "char_mage", 3, CardType.Status,
                    Kw("exhaust"), CardRarity.Epic,
                    ApplyStat(StatusCatalog.ReviveBlessing, 1, -1, EffectTarget.FrontAlly)),
                SolarJudgment = SaveCard("p_solar_judgment", "太阳审判", "char_mage", 4, CardType.Attack,
                    null, CardRarity.Rare, AtkDmg(10, 200, reach: TargetReach.Any))
            };
        }

        static DemonCards CreateDemonCards()
        {
            return new DemonCards
            {
                ShadowClaw = SaveCard("d_shadow_claw", "暗影爪击", "char_ranger", 1, CardType.Attack,
                    null, CardRarity.Common, AtkDmg(5, 80)),
                DevilTouch = SaveCard("d_devil_touch", "恶魔之触", "char_ranger", 1, CardType.Attack,
                    null, CardRarity.Common, AtkDmg(4, 50, lifestealPercent: 100)),
                BloodTail = SaveCard("d_blood_tail", "血尾贯穿", "char_ranger", 2, CardType.Attack,
                    null, CardRarity.Rare, AtkDmg(5, 100, splashBehind: true, splashPercent: 80)),
                BloodArmor = SaveCard("d_blood_armor", "鲜血铠甲", "char_ranger", 1, CardType.Defense,
                    Kw("sacrifice"), CardRarity.Common, SelfDmg(3), Block(12)),
                BloodFlame = SaveCard("d_blood_flame", "血焰爆发", "char_ranger", 2, CardType.Attack,
                    Kw("sacrifice"), CardRarity.Rare,
                    SelfDmg(8),
                    AtkDmg(10, 130)),
                SoulRip = SaveCard("d_soul_rip", "灵魂撕裂", "char_ranger", 2, CardType.Attack,
                    null, CardRarity.Rare, AtkDmg(6, 80, ignoreDefPercent: 100, reach: TargetReach.Any)),
                DarkSacrifice = SaveCard("d_dark_sacrifice", "暗黑献祭", "char_ranger", 3, CardType.Attack,
                    Kw("sacrifice"), CardRarity.Epic,
                    SelfDmg(15),
                    AtkDmg(14, 170)),
                DemonPact = SaveCard("d_demon_pact", "恶魔契约", "char_ranger", 2, CardType.Status,
                    Kw("sacrifice"), CardRarity.Common,
                    SelfDmg(5),
                    Draw(2),
                    ApplyStat(StatusCatalog.AttackUp, 3, 1, EffectTarget.Self)),
                VampAura = SaveCard("d_vamp_aura", "吸血光环", "char_ranger", 1, CardType.Status,
                    null, CardRarity.Common, ApplyStat(StatusCatalog.VampAura, 30, 1, EffectTarget.Self)),
                CurseChain = SaveCard("d_curse_chain", "诅咒之链", "char_ranger", 2, CardType.Attack,
                    null, CardRarity.Rare,
                    AtkDmg(5, 100, reach: TargetReach.Any),
                    ApplyStat(StatusCatalog.AttackDown, 3, 2, EffectTarget.DefaultEnemy)),
                HellFire = SaveCard("d_hell_fire", "地狱烈焰", "char_ranger", 3, CardType.Attack,
                    Kw("aoe", "sacrifice"), CardRarity.Rare,
                    SelfDmg(8),
                    AtkDmg(6, 100, EffectTarget.AllEnemies)),
                DemonLord = SaveCard("d_demon_lord", "魔王降临", "char_ranger", 4, CardType.Attack,
                    Kw("sacrifice"), CardRarity.Epic,
                    SelfDmg(20),
                    AtkDmg(15, 200, reach: TargetReach.Any, onKillHeal: 30))
            };
        }

        static CardDefinitionSO[] BuildInitialWarriorDeck(WarriorCards c) =>
            BuildDeckWithCounts(
                (c.BasicSlash, 3),
                (c.ShieldBlock, 2),
                (c.DefensiveStance, 1),
                (c.IronParry, 1),
                (c.AuthorRealmStrike, 1));

        static CardDefinitionSO[] BuildInitialPharaohDeck(PharaohCards c) =>
            BuildDeckWithCounts(
                (c.SandRay, 3),
                (c.Bless, 2),
                (c.ScarabShield, 1),
                (c.UndeadCurse, 1));

        static CardDefinitionSO[] BuildInitialDemonDeck(DemonCards c) =>
            BuildDeckWithCounts(
                (c.ShadowClaw, 3),
                (c.BloodArmor, 2),
                (c.DevilTouch, 1),
                (c.BloodTail, 1));

        static EffectActionDefinition AtkDmg(
            int fixedVal,
            int atkPercent,
            EffectTarget target = EffectTarget.DefaultEnemy,
            TargetReach reach = TargetReach.FrontAndMiddle,
            int ignoreDefPercent = 0,
            int bonusHpBelowPercent = 0,
            int bonusHpBelowFlat = 0,
            int bonusHitThisTurnPercent = 0,
            int lifestealPercent = 0,
            int onKillHeal = 0,
            bool splashBehind = false,
            int splashPercent = 100)
        {
            return new EffectActionDefinition
            {
                Type = EffectActionType.DealDamage,
                Target = target,
                Value = fixedVal,
                ScaleWithAttack = true,
                AttackScalePercent = atkPercent,
                IgnoreDefPercent = ignoreDefPercent,
                BonusIfTargetHpBelowPercent = bonusHpBelowPercent,
                BonusIfTargetHpBelowFlat = bonusHpBelowFlat,
                BonusIfTargetHitThisTurnPercent = bonusHitThisTurnPercent,
                LifestealPercent = lifestealPercent,
                OnKillHealAmount = onKillHeal,
                Reach = target == EffectTarget.AllEnemies ? TargetReach.Any : reach,
                SplashBehindTarget = splashBehind,
                SplashPowerPercent = splashPercent
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

        static EffectActionDefinition AllyDefBlock(EffectTarget target, int fixedVal, int defPercent)
        {
            return new EffectActionDefinition
            {
                Type = EffectActionType.GainBlock,
                Target = target,
                Value = fixedVal,
                ScaleWithDefense = true,
                DefenseScalePercent = defPercent,
                Reach = TargetReach.Any
            };
        }

        static EffectActionDefinition Block(int amount, EffectTarget target = EffectTarget.Self)
        {
            return new EffectActionDefinition
            {
                Type = EffectActionType.GainBlock,
                Target = target,
                Value = amount,
                Reach = IsAllyPickTarget(target) ? TargetReach.Any : TargetReach.FrontAndMiddle
            };
        }

        static EffectActionDefinition HealScaled(int flat, int atkPercent, EffectTarget target)
        {
            return new EffectActionDefinition
            {
                Type = EffectActionType.Heal,
                Target = target,
                Value = flat,
                ScaleWithAttack = true,
                AttackScalePercent = atkPercent,
                Reach = IsAllyPickTarget(target) ? TargetReach.Any : TargetReach.FrontAndMiddle
            };
        }

        static bool IsAllyPickTarget(EffectTarget target) =>
            target is EffectTarget.FrontAlly or EffectTarget.BackAlly;

        static EffectActionDefinition ApplyStat(
            string statusId,
            int stacks,
            int duration,
            EffectTarget target)
        {
            return new EffectActionDefinition
            {
                Type = EffectActionType.ApplyStatus,
                Target = target,
                StatusId = statusId,
                Stacks = stacks,
                Duration = duration,
                Reach = IsAllyPickTarget(target) ? TargetReach.Any : TargetReach.FrontAndMiddle
            };
        }

        static EffectActionDefinition[] TeamAttackUp(int stacks, int duration) =>
            new[]
            {
                ApplyStat(StatusCatalog.AttackUp, stacks, duration, EffectTarget.AllyFrontSlot),
                ApplyStat(StatusCatalog.AttackUp, stacks, duration, EffectTarget.AllyMiddleSlot),
                ApplyStat(StatusCatalog.AttackUp, stacks, duration, EffectTarget.AllyBackSlot)
            };

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

        static EffectActionDefinition[] AoeDmg(int fixedVal, int atkPercent) =>
            new[] { AtkDmg(fixedVal, atkPercent, EffectTarget.AllEnemies) };

        static EffectActionDefinition FixedAoeDmg(int amount) =>
            new EffectActionDefinition
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.AllEnemies,
                Value = amount,
                Reach = TargetReach.Any
            };

        static EffectActionDefinition[] RespondAttack(int reductionPercent, int reflectPercent = 0)
        {
            var actions = new List<EffectActionDefinition>
            {
                new()
                {
                    Type = EffectActionType.GainBlockFromLastDamagePercent,
                    Target = EffectTarget.Self,
                    Value = reductionPercent,
                    Condition = ReactionConditionType.LastActionAttackOnSelf
                }
            };

            if (reflectPercent > 0)
            {
                actions.Add(new EffectActionDefinition
                {
                    Type = EffectActionType.ReflectLastDamageToAttacker,
                    Target = EffectTarget.LastActionActor,
                    Value = reflectPercent,
                    Condition = ReactionConditionType.LastActionAttackOnSelf
                });
            }

            return actions.ToArray();
        }

        static EffectActionDefinition[] Merge(params EffectActionDefinition[] actions) => actions;

        static CardDefinitionSO SaveCard(
            string id,
            string displayName,
            string owner,
            int cost,
            CardType cardType,
            string[] keywords,
            params EffectActionDefinition[] actions) =>
            SaveCard(id, displayName, owner, cost, cardType, keywords, CardRarity.Common, actions);

        static CardDefinitionSO SaveCard(
            string id,
            string displayName,
            string owner,
            int cost,
            CardType cardType,
            string[] keywords,
            CardRarity rarity,
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
            card.Rarity = rarity;
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

        static CardDefinitionSO[] BuildDeckWithCounts(params (CardDefinitionSO card, int count)[] entries)
        {
            var list = new List<CardDefinitionSO>();
            foreach (var (card, count) in entries)
            {
                for (var i = 0; i < count; i++)
                    list.Add(card);
            }

            return list.ToArray();
        }

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
