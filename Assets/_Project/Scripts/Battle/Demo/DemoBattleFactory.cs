using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Battle.Status;

namespace Grimhand.Battle.Demo
{
    public static class DemoBattleFactory
    {
        public static BattleConfig CreateDefault3v1()
        {
            var config = new BattleConfig { Seed = 42 };

            var knightCards = new[]
            {
                Card("k_strike", "重击", "char_knight", 1, CardType.Attack, CardEffectKind.DealDamage, 8),
                Card("k_strike2", "重击", "char_knight", 1, CardType.Attack, CardEffectKind.DealDamage, 8),
                Card("k_guard", "盾挡", "char_knight", 1, CardType.Defense, CardEffectKind.GainBlock, 6),
                Card("k_parry", "弹反", "char_knight", 2, CardType.Defense, CardEffectKind.GainBlock, 0),
                Card("k_slash", "斩击", "char_knight", 2, CardType.Attack, CardEffectKind.DealDamage, 14),
                Card("k_slash2", "斩击", "char_knight", 2, CardType.Attack, CardEffectKind.DealDamage, 14),
                Card("k_taunt", "战吼", "char_knight", 1, CardType.Status, CardEffectKind.GainBlock, 4),
                Card("k_taunt2", "战吼", "char_knight", 1, CardType.Status, CardEffectKind.GainBlock, 4),
                Card("k_cleave", "顺劈", "char_knight", 2, CardType.Attack, CardEffectKind.DealDamage, 10),
                Card("k_cleave2", "顺劈", "char_knight", 2, CardType.Attack, CardEffectKind.DealDamage, 10),
            };

            var mageCards = new[]
            {
                Card("m_bolt", "魔弹", "char_mage", 1, CardType.Attack, CardEffectKind.DealDamage, 7),
                Card("m_bolt2", "魔弹", "char_mage", 1, CardType.Attack, CardEffectKind.DealDamage, 7),
                Card("m_shield", "魔盾", "char_mage", 1, CardType.Defense, CardEffectKind.GainBlock, 5),
                Card("m_shield2", "魔盾", "char_mage", 1, CardType.Defense, CardEffectKind.GainBlock, 5),
                Card("m_fire", "火球", "char_mage", 2, CardType.Attack, CardEffectKind.DealDamage, 16),
                Card("m_fire2", "火球", "char_mage", 2, CardType.Attack, CardEffectKind.DealDamage, 16),
                Card("m_mend", "愈合", "char_mage", 2, CardType.Status, CardEffectKind.Heal, 8),
                Card("m_mend2", "愈合", "char_mage", 2, CardType.Status, CardEffectKind.Heal, 8),
                Card("m_focus", "毒云", "char_mage", 2, CardType.Status, CardEffectKind.Heal, 0),
                Card("m_focus2", "专注", "char_mage", 1, CardType.Status, CardEffectKind.DrawCards, 1),
            };

            var rangerCards = new[]
            {
                Card("r_shot", "射击", "char_ranger", 1, CardType.Attack, CardEffectKind.DealDamage, 6),
                Card("r_shot2", "射击", "char_ranger", 1, CardType.Attack, CardEffectKind.DealDamage, 6),
                Card("r_dodge", "闪避", "char_ranger", 1, CardType.Defense, CardEffectKind.GainBlock, 5),
                Card("r_dodge2", "闪避", "char_ranger", 1, CardType.Defense, CardEffectKind.GainBlock, 5),
                Card("r_snipe", "狙击", "char_ranger", 2, CardType.Attack, CardEffectKind.DealDamage, 15),
                Card("r_snipe2", "狙击", "char_ranger", 2, CardType.Attack, CardEffectKind.DealDamage, 15),
                Card("r_bandage", "包扎", "char_ranger", 1, CardType.Status, CardEffectKind.Heal, 5),
                Card("r_bandage2", "包扎", "char_ranger", 1, CardType.Status, CardEffectKind.Heal, 5),
                Card("r_mark", "缚足", "char_ranger", 1, CardType.Status, CardEffectKind.Heal, 0),
                Card("r_mark2", "标记", "char_ranger", 1, CardType.Status, CardEffectKind.DrawCards, 1),
            };

            var goblinCards = new[]
            {
                Card("g_bite", "撕咬", "char_goblin", 1, CardType.Attack, CardEffectKind.DealDamage, 6),
                Card("g_bite2", "撕咬", "char_goblin", 1, CardType.Attack, CardEffectKind.DealDamage, 6),
                Card("g_bite3", "撕咬", "char_goblin", 1, CardType.Attack, CardEffectKind.DealDamage, 6),
                Card("g_scratch", "抓挠", "char_goblin", 1, CardType.Attack, CardEffectKind.DealDamage, 5),
                Card("g_scratch2", "抓挠", "char_goblin", 1, CardType.Attack, CardEffectKind.DealDamage, 5),
                Card("g_hiss", "威吓", "char_goblin", 1, CardType.Status, CardEffectKind.GainBlock, 3),
                Card("g_hiss2", "威吓", "char_goblin", 1, CardType.Status, CardEffectKind.GainBlock, 3),
                Card("g_lunge", "猛扑", "char_goblin", 2, CardType.Attack, CardEffectKind.DealDamage, 12),
                Card("g_lunge2", "猛扑", "char_goblin", 2, CardType.Attack, CardEffectKind.DealDamage, 12),
                Card("g_lunge3", "猛扑", "char_goblin", 2, CardType.Attack, CardEffectKind.DealDamage, 12),
            };

            config.Combatants.Add(Combatant("p_knight", "骑士", TeamSide.Player, FormationSlot.Front,
                "char_knight", level: 2, hp: 40, baseAtk: 6, baseDef: 4, spd: 10, knightCards));
            config.Combatants.Add(Combatant("p_mage", "法师", TeamSide.Player, FormationSlot.Middle,
                "char_mage", level: 1, hp: 28, baseAtk: 5, baseDef: 2, spd: 5, mageCards));
            config.Combatants.Add(Combatant("p_ranger", "游侠", TeamSide.Player, FormationSlot.Back,
                "char_ranger", level: 1, hp: 30, baseAtk: 7, baseDef: 2, spd: 7, rangerCards));
            config.Combatants.Add(Combatant("e_goblin", "哥布林", TeamSide.Enemy, FormationSlot.Front,
                "char_goblin", level: 2, hp: 50, baseAtk: 7, baseDef: 1, spd: 8, goblinCards));

            return config;
        }

        static CombatantConfig Combatant(
            string id,
            string name,
            TeamSide team,
            FormationSlot slot,
            string charId,
            int level,
            int hp,
            int baseAtk,
            int baseDef,
            int spd,
            CardTemplate[] deck)
        {
            var c = new CombatantConfig
            {
                Id = id,
                DisplayName = name,
                Team = team,
                Slot = slot,
                CharacterDefinitionId = charId,
                Level = level,
                MaxHp = hp,
                BaseAttack = baseAtk,
                BaseDefense = baseDef,
                Speed = spd
            };
            c.DeckTemplates.AddRange(deck);
            return c;
        }

        static CardTemplate Card(
            string id,
            string name,
            string owner,
            int cost,
            CardType type,
            CardEffectKind effect,
            int power,
            int drawNextTurn = 0)
        {
            if (id == "k_parry")
            {
                return CardTemplate.Create(id, name, owner, cost, type,
                    new EffectActionSpec
                    {
                        Type = EffectActionType.GainBlockFromLastDamagePercent,
                        Target = EffectTarget.Self,
                        Value = 50,
                        Condition = ReactionConditionType.LastActionAttackOnSelf
                    },
                    new EffectActionSpec
                    {
                        Type = EffectActionType.ReflectLastDamageToAttacker,
                        Target = EffectTarget.LastActionActor,
                        Value = 200,
                        Condition = ReactionConditionType.LastActionAttackOnSelf
                    });
            }

            if (id == "m_focus")
            {
                return CardTemplate.Create(id, name, owner, cost, type,
                    new EffectActionSpec
                    {
                        Type = EffectActionType.ApplyStatus,
                        Target = EffectTarget.DefaultEnemy,
                        StatusId = StatusCatalog.Poison,
                        Stacks = 10,
                        Duration = -1
                    });
            }

            if (id == "r_snipe" || id == "r_snipe2")
            {
                return CardTemplate.Create(id, name, owner, cost, type,
                    new EffectActionSpec
                    {
                        Type = EffectActionType.DealDamage,
                        Target = EffectTarget.DefaultEnemy,
                        Value = power,
                        ScaleWithAttack = true
                    });
            }

            if (id == "r_mark" || id == "r_mark2")
            {
                return CardTemplate.Create(id, name, owner, cost, type,
                    new EffectActionSpec
                    {
                        Type = EffectActionType.ApplyStatus,
                        Target = EffectTarget.EnemyBackSlot,
                        StatusId = StatusCatalog.Slow,
                        Stacks = 1,
                        Duration = 2
                    });
            }

            return CardTemplate.FromLegacy(id, name, owner, cost, type, effect, power, drawNextTurn);
        }
    }
}
