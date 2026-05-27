using Grimhand.Battle;
using Grimhand.Battle.Demo;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class DeathPollutionTests
    {
        [Test]
        public void DeadCharacterCards_RemainInHand_ButNotSelectable()
        {
            var engine = new BattleEngine(DemoBattleFactory.CreateDefault3v1());
            engine.StartBattle();

            var state = engine.State;
            var mage = state.GetCombatant("p_mage");
            Assert.NotNull(mage);

            mage.Hp = 0;
            var events = new System.Collections.Generic.List<Events.BattleEvent>();
            CombatantDeathRules.OnCharacterDied(state, mage, events);

            var pollutedTotal = 0;
            foreach (var card in state.CardsById.Values)
            {
                if (card.OwnerCharacterId == "char_mage" && !card.IsUsable)
                    pollutedTotal++;
            }

            Assert.Greater(pollutedTotal, 0);

            CardInstanceState pollutedCard = null;
            foreach (var card in state.PlayerHand)
            {
                if (card.OwnerCharacterId == "char_mage")
                {
                    pollutedCard = card;
                    break;
                }
            }

            if (pollutedCard == null)
            {
                foreach (var card in state.PlayerDrawPile)
                {
                    if (card.OwnerCharacterId == "char_mage")
                    {
                        state.PlayerHand.Add(card);
                        state.PlayerDrawPile.Remove(card);
                        pollutedCard = card;
                        break;
                    }
                }
            }

            Assert.NotNull(pollutedCard);
            Assert.IsFalse(pollutedCard.IsUsable);
            Assert.IsFalse(engine.Draft.TrySelectCard(pollutedCard.InstanceId));
        }

        [Test]
        public void StartWithZeroHp_PollutesOwnerCardsOnInit()
        {
            var config = DemoBattleFactory.CreateDefault3v3();
            foreach (var cc in config.Combatants)
            {
                if (cc.CharacterDefinitionId == "char_knight")
                    cc.StartHp = 0;
            }

            var engine = new BattleEngine(config);
            var polluted = 0;
            foreach (var card in engine.State.CardsById.Values)
            {
                if (card.OwnerCharacterId == "char_knight" && !card.IsUsable)
                    polluted++;
            }

            Assert.Greater(polluted, 0);
        }
    }
}
