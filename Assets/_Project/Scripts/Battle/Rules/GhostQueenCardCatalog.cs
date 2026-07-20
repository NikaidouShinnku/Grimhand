using Grimhand.Battle.Model;
using Grimhand.Battle.Status;

namespace Grimhand.Battle.Rules
{
    /// <summary>
    /// 幽灵女王关键卡的权威效果定义。
    /// SO 资产曾出现空 Actions / 错枚举；ToTemplate / CreateCardInstance 一律以此处为准。
    /// </summary>
    public static class GhostQueenCardCatalog
    {
        public const string CharacterId = "char_ghost_queen";

        public static bool TryApplyCanonical(CardTemplate template)
        {
            if (template == null || string.IsNullOrEmpty(template.DefinitionId))
                return false;

            CardTemplate canonical = template.DefinitionId switch
            {
                "m_queen_deterrence" => BuildDeterrence(),
                "m_queen_soul_drain" => BuildSoulDrain(),
                "m_queen_curse" => BuildCurse(),
                "m_queen_command" => BuildCommand(),
                _ => null
            };

            if (canonical == null)
                return false;

            template.DisplayName = canonical.DisplayName;
            template.OwnerCharacterId = canonical.OwnerCharacterId;
            template.Cost = canonical.Cost;
            template.CardType = canonical.CardType;
            template.Keywords.Clear();
            template.Keywords.AddRange(canonical.Keywords);
            template.Actions.Clear();
            foreach (var action in canonical.Actions)
                template.Actions.Add(EffectActionSpec.Clone(action));
            return true;
        }

        public static CardTemplate BuildDeterrence()
        {
            var card = Base("m_queen_deterrence", "女王的威慑", 1, CardType.Status, "slow");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.LockRandomPlayerPlaysThisTurn,
                Target = EffectTarget.DefaultEnemy
            });
            return card;
        }

        public static CardTemplate BuildSoulDrain()
        {
            var card = Base("m_queen_soul_drain", "摄魂", 1, CardType.Status, "slow");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ReducePlayerEnergyRegenNextTurn,
                Target = EffectTarget.AllEnemies,
                Value = 2
            });
            return card;
        }

        public static CardTemplate BuildCurse()
        {
            var card = Base("m_queen_curse", "女王的诅咒", 2, CardType.Status, "poison", "aoe");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.AllEnemies,
                StatusId = StatusCatalog.Poison,
                Stacks = 6,
                Duration = -1,
                Reach = TargetReach.Any
            });
            return card;
        }

        public static CardTemplate BuildCommand()
        {
            var card = Base("m_queen_command", "女王的命令", 2, CardType.Defense, "parry");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ArmRespondDamageRedirect,
                Target = EffectTarget.Self,
                Condition = ReactionConditionType.None
            });
            return card;
        }

        static CardTemplate Base(
            string id,
            string name,
            int cost,
            CardType type,
            params string[] keywords)
        {
            var card = new CardTemplate
            {
                DefinitionId = id,
                DisplayName = name,
                OwnerCharacterId = CharacterId,
                Cost = cost,
                CardType = type
            };
            if (keywords != null)
            {
                foreach (var keyword in keywords)
                {
                    if (!string.IsNullOrEmpty(keyword))
                        card.Keywords.Add(keyword);
                }
            }

            return card;
        }
    }
}
