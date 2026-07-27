using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Battle.Status;

namespace Grimhand.Battle.Demo
{
    public static class DemoBattleFactory
    {
        public static BattleConfig CreateDefault3v3()
        {
            var config = new BattleConfig { Seed = 0 };

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
                Card("p_sand_ray", "沙暴射线", "char_mage", 1, CardType.Attack, CardEffectKind.DealDamage, 7),
                Card("p_sand_ray2", "沙暴射线", "char_mage", 1, CardType.Attack, CardEffectKind.DealDamage, 7),
                Card("m_shield", "魔盾", "char_mage", 1, CardType.Defense, CardEffectKind.GainBlock, 5),
                Card("m_shield2", "魔盾", "char_mage", 1, CardType.Defense, CardEffectKind.GainBlock, 5),
                Card("m_fire", "火球", "char_mage", 2, CardType.Attack, CardEffectKind.DealDamage, 16),
                Card("m_fire2", "火球", "char_mage", 2, CardType.Attack, CardEffectKind.DealDamage, 16),
                Card("m_mend", "愈合", "char_mage", 2, CardType.Status, CardEffectKind.Heal, 8),
                Card("m_mend2", "愈合", "char_mage", 2, CardType.Status, CardEffectKind.Heal, 8),
                Card("m_focus2", "专注", "char_mage", 1, CardType.Status, CardEffectKind.DrawCards, 1),
                Card("m_focus3", "专注", "char_mage", 1, CardType.Status, CardEffectKind.DrawCards, 1),
            };

            var rangerCards = new[]
            {
                Card("r_snipe", "狙击", "char_ranger", 2, CardType.Attack, CardEffectKind.DealDamage, 15),
                Card("r_snipe2", "狙击", "char_ranger", 2, CardType.Attack, CardEffectKind.DealDamage, 15),
                Card("r_pierce", "贯射", "char_ranger", 2, CardType.Attack, CardEffectKind.DealDamage, 11),
                Card("r_pierce2", "贯射", "char_ranger", 2, CardType.Attack, CardEffectKind.DealDamage, 11),
                Card("r_pierce3", "贯射", "char_ranger", 2, CardType.Attack, CardEffectKind.DealDamage, 11),
                Card("r_far_shot", "远射", "char_ranger", 2, CardType.Attack, CardEffectKind.DealDamage, 10),
                Card("r_far_shot2", "远射", "char_ranger", 2, CardType.Attack, CardEffectKind.DealDamage, 10),
                Card("r_mark", "缚足", "char_ranger", 1, CardType.Status, CardEffectKind.Heal, 0),
                Card("r_mark2", "缚足", "char_ranger", 1, CardType.Status, CardEffectKind.Heal, 0),
                Card("r_mark3", "缚足", "char_ranger", 1, CardType.Status, CardEffectKind.Heal, 0),
            };

            var bruteCards = new[]
            {
                Card("g_bite", "撕咬", "char_goblin_brute", 1, CardType.Attack, CardEffectKind.DealDamage, 6),
                Card("g_bite2", "撕咬", "char_goblin_brute", 1, CardType.Attack, CardEffectKind.DealDamage, 6),
                Card("g_bite3", "撕咬", "char_goblin_brute", 1, CardType.Attack, CardEffectKind.DealDamage, 6),
                Card("g_bite4", "撕咬", "char_goblin_brute", 1, CardType.Attack, CardEffectKind.DealDamage, 6),
                Card("g_scratch", "抓挠", "char_goblin_brute", 1, CardType.Attack, CardEffectKind.DealDamage, 5),
                Card("g_scratch2", "抓挠", "char_goblin_brute", 1, CardType.Attack, CardEffectKind.DealDamage, 5),
                Card("g_lunge", "猛扑", "char_goblin_brute", 2, CardType.Attack, CardEffectKind.DealDamage, 10),
                Card("g_lunge2", "猛扑", "char_goblin_brute", 2, CardType.Attack, CardEffectKind.DealDamage, 10),
            };

            var shamanCards = new[]
            {
                Card("g_hex", "邪咒", "char_goblin_shaman", 2, CardType.Status, CardEffectKind.Heal, 0),
                Card("g_hex2", "邪咒", "char_goblin_shaman", 2, CardType.Status, CardEffectKind.Heal, 0),
                Card("g_hex3", "邪咒", "char_goblin_shaman", 2, CardType.Status, CardEffectKind.Heal, 0),
                Card("g_hex4", "邪咒", "char_goblin_shaman", 2, CardType.Status, CardEffectKind.Heal, 0),
                Card("g_wither", "虚弱", "char_goblin_shaman", 1, CardType.Status, CardEffectKind.Heal, 0),
                Card("g_wither2", "虚弱", "char_goblin_shaman", 1, CardType.Status, CardEffectKind.Heal, 0),
                Card("g_wither3", "虚弱", "char_goblin_shaman", 1, CardType.Status, CardEffectKind.Heal, 0),
                Card("g_wither4", "虚弱", "char_goblin_shaman", 1, CardType.Status, CardEffectKind.Heal, 0),
            };

            var archerCards = new[]
            {
                Card("g_arrow", "箭矢", "char_goblin_archer", 1, CardType.Attack, CardEffectKind.DealDamage, 8),
                Card("g_arrow2", "箭矢", "char_goblin_archer", 1, CardType.Attack, CardEffectKind.DealDamage, 8),
                Card("g_arrow3", "箭矢", "char_goblin_archer", 1, CardType.Attack, CardEffectKind.DealDamage, 8),
                Card("g_arrow4", "箭矢", "char_goblin_archer", 1, CardType.Attack, CardEffectKind.DealDamage, 8),
                Card("g_arrow5", "箭矢", "char_goblin_archer", 1, CardType.Attack, CardEffectKind.DealDamage, 8),
                Card("g_arrow6", "箭矢", "char_goblin_archer", 1, CardType.Attack, CardEffectKind.DealDamage, 8),
                Card("g_aim", "瞄准", "char_goblin_archer", 2, CardType.Attack, CardEffectKind.DealDamage, 14),
                Card("g_aim2", "瞄准", "char_goblin_archer", 2, CardType.Attack, CardEffectKind.DealDamage, 14),
            };

            config.Combatants.Add(Combatant("p_knight", "骑士", TeamSide.Player, FormationSlot.Front,
                "char_knight", level: 2, hp: 40, baseAtk: 6, baseDef: 4, spd: 10, knightCards));
            config.Combatants.Add(Combatant("p_mage", "法师", TeamSide.Player, FormationSlot.Middle,
                "char_mage", level: 1, hp: 28, baseAtk: 5, baseDef: 2, spd: 5, mageCards));
            config.Combatants.Add(Combatant("p_ranger", "游侠", TeamSide.Player, FormationSlot.Back,
                "char_ranger", level: 1, hp: 30, baseAtk: 7, baseDef: 2, spd: 7, rangerCards));
            config.Combatants.Add(Combatant("e_brute", "哥布林蛮兵", TeamSide.Enemy, FormationSlot.Front,
                "char_goblin_brute", level: 2, hp: 45, baseAtk: 7, baseDef: 2, spd: 8, bruteCards));
            config.Combatants.Add(Combatant("e_shaman", "骷髅萨满", TeamSide.Enemy, FormationSlot.Middle,
                "char_goblin_shaman", level: 1, hp: 32, baseAtk: 4, baseDef: 1, spd: 6, shamanCards));
            config.Combatants.Add(Combatant("e_archer", "怨灵弓手", TeamSide.Enemy, FormationSlot.Back,
                "char_goblin_archer", level: 1, hp: 28, baseAtk: 8, baseDef: 1, spd: 9, archerCards));

            return config;
        }

        public static BattleConfig CreateDefault3v1() => CreateDefault3v3();

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

            if (id == "r_snipe" || id == "r_snipe2")
            {
                return CardTemplate.Create(id, name, owner, cost, type,
                    new EffectActionSpec
                    {
                        Type = EffectActionType.DealDamage,
                        Target = EffectTarget.DefaultEnemy,
                        Value = power,
                        ScaleWithAttack = true,
                        Reach = TargetReach.Any
                    });
            }

            if (id.StartsWith("r_pierce"))
            {
                return WithKeywords(
                    CardTemplate.Create(id, name, owner, cost, type,
                        new EffectActionSpec
                        {
                            Type = EffectActionType.DealDamage,
                            Target = EffectTarget.DefaultEnemy,
                            Value = power,
                            ScaleWithAttack = true,
                            Reach = TargetReach.FrontAndMiddle,
                            SplashBehindTarget = true,
                            SplashPowerPercent = 80
                        }),
                    "pierce");
            }

            if (id.StartsWith("r_far_shot"))
            {
                return CardTemplate.Create(id, name, owner, cost, type,
                    new EffectActionSpec
                    {
                        Type = EffectActionType.DealDamage,
                        Target = EffectTarget.DefaultEnemy,
                        Value = power,
                        ScaleWithAttack = true,
                        Reach = TargetReach.Any,
                        BackRowPowerPercent = 70
                    });
            }

            if (id == "r_mark" || id == "r_mark2" || id == "r_mark3")
            {
                return WithKeywords(
                    CardTemplate.Create(id, name, owner, cost, type,
                        new EffectActionSpec
                        {
                            Type = EffectActionType.ApplyStatus,
                            Target = EffectTarget.EnemyBackSlot,
                            StatusId = StatusCatalog.Slow,
                            Stacks = 1,
                            Duration = 2
                        }),
                    "slow", "slot");
            }

            if (id.StartsWith("g_hex"))
            {
                return CardTemplate.Create(id, name, owner, cost, type,
                    new EffectActionSpec
                    {
                        Type = EffectActionType.ApplyStatus,
                        Target = EffectTarget.DefaultEnemy,
                        StatusId = StatusCatalog.Poison,
                        Stacks = 5,
                        Duration = -1
                    });
            }

            if (id.StartsWith("g_wither"))
            {
                return CardTemplate.Create(id, name, owner, cost, type,
                    new EffectActionSpec
                    {
                        Type = EffectActionType.ApplyStatus,
                        Target = EffectTarget.DefaultEnemy,
                        StatusId = StatusCatalog.Slow,
                        Stacks = 1,
                        Duration = 2
                    });
            }

            var template = CardTemplate.FromLegacy(id, name, owner, cost, type, effect, power, drawNextTurn);
            return template;
        }

        static CardTemplate WithKeywords(CardTemplate template, params string[] keywords)
        {
            template.Keywords.AddRange(keywords);
            return template;
        }
    }
}
