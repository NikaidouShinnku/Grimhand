using System;
using System.Collections.Generic;
using Grimhand.Battle.Consumables;
using Grimhand.Battle.Model;
using Grimhand.Core;
using Grimhand.Expedition.Events;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition
{
    /// <summary>规划事件选项的交互步骤与结算结果。</summary>
    public static class ExpeditionEventPlanner
    {
        public static ExpeditionEventOutcome Resolve(
            ExpeditionRunState run,
            ExpeditionConfig config,
            string eventId,
            int choiceIndex,
            BattleRng rng)
        {
            if (!ExpeditionEventCatalog.TryGet(eventId, out var definition))
                return new ExpeditionEventOutcome { Message = "事件已结束。" };

            if (choiceIndex < 0 || choiceIndex >= definition.Choices.Count)
                return new ExpeditionEventOutcome { Message = "无效选择。" };

            return eventId switch
            {
                ExpeditionEventIds.MysteriousTraveler => PlanMysteriousTraveler(run, config, choiceIndex, rng),
                ExpeditionEventIds.AncientTemple => PlanAncientTemple(run, choiceIndex),
                ExpeditionEventIds.InjuredAdventurer => PlanInjuredAdventurer(run, config, choiceIndex, rng),
                ExpeditionEventIds.MagicSpring => PlanMagicSpring(run, choiceIndex, rng),
                ExpeditionEventIds.GamblerDice => PlanGamblerDice(run, choiceIndex, rng),
                ExpeditionEventIds.MirrorPhantom => PlanMirrorPhantom(run, choiceIndex, config, rng),
                ExpeditionEventIds.CursedBookshelf => PlanCursedBookshelf(run, choiceIndex, rng, config),
                ExpeditionEventIds.AdventurerRevenge => PlanAdventurerRevenge(run, choiceIndex),
                ExpeditionEventIds.TrainingDummy => PlanTrainingDummy(run, choiceIndex),
                ExpeditionEventIds.SoulRift => PlanSoulRift(run, choiceIndex, rng),
                ExpeditionEventIds.WanderingSmith => PlanWanderingSmith(run, choiceIndex),
                ExpeditionEventIds.TiredCamp => PlanTiredCamp(run, choiceIndex, rng),
                ExpeditionEventIds.JadeWorkshop => PlanJadeWorkshop(run, choiceIndex),
                ExpeditionEventIds.AncientFurnace => PlanAncientFurnace(run, choiceIndex, rng),
                ExpeditionEventIds.AbyssWhisper => PlanAbyssWhisper(run, choiceIndex, rng, config),
                _ => new ExpeditionEventOutcome { Message = "事件结束。" }
            };
        }

        static ExpeditionEventOutcome PlanMysteriousTraveler(
            ExpeditionRunState run,
            ExpeditionConfig config,
            int choice,
            BattleRng rng)
        {
            return choice switch
            {
                0 when run.Gold >= 30 => PlanBuyRandomCard(run, config, rng, 30, "神秘旅者"),
                0 => Fail("金币不足，无法购买（需要 30 金币）。"),
                1 => PlanTravelerGift(run, config, rng),
                _ => Leave("你拒绝交易，悄然离开。")
            };
        }

        static ExpeditionEventOutcome PlanBuyRandomCard(
            ExpeditionRunState run,
            ExpeditionConfig config,
            BattleRng rng,
            int cost,
            string header)
        {
            if (config == null)
                return Fail("无法购买卡牌（配置缺失）。");

            run.Gold -= cost;
            ExpeditionRewardPickup pickup = null;
            if (ExpeditionCardPool.TryRollCardReward(config, run, CardRarity.Rare, rng, out var card, out var owner))
                pickup = RewardCardPickup(card, owner, header);
            else if (ExpeditionCardPool.TryRollCardReward(config, run, CardRarity.Common, rng, out card, out owner))
                pickup = RewardCardPickup(card, owner, header);

            return MessageThenReward(
                pickup != null ? "购得一张卡牌，请点击领取。" : "旅者翻找行囊，却什么也没给你。",
                pickup);
        }

        static ExpeditionEventOutcome PlanTravelerGift(ExpeditionRunState run, ExpeditionConfig config, BattleRng rng)
        {
            var relicId = ExpeditionRewardPickupFactory.RollRelicId(run, rng);
            ExpeditionRewardPickup pickup = null;
            if (!string.IsNullOrEmpty(relicId))
                pickup = ExpeditionRewardPickupFactory.Relic(relicId, "神秘旅者");

            if (run?.Party != null && run.Party.Count > 0 && config != null)
            {
                var idx = rng.NextIndex(run.Party.Count);
                var member = run.Party[idx];
                pickup ??= new ExpeditionRewardPickup { HeaderText = "神秘旅者", Kind = RewardPickupKind.EventOrShrine };
                pickup.CardDefinitionId = "curse_chaos_touch";
                pickup.CardOwnerCharacterId = member.CharacterDefinitionId;
                pickup.CardDisplayName = "混沌之触";
            }

            return MessageThenReward("一张诅咒牌被塞入了牌组……", pickup);
        }

        static ExpeditionEventOutcome PlanAncientTemple(ExpeditionRunState run, int choice)
        {
            return choice switch
            {
                0 => TeamHpThen(
                    run,
                    -10,
                    "祈祷生效：远征期间全队 ATK+1。",
                    ExpeditionRewardPickupFactory.TeamStats("古神殿", teamAttack: 1, grantXp: 5)),
                1 => MessageThenReward(
                    "亵渎圣堂获得 50 金币，但神罚将至。",
                    ExpeditionRewardPickupFactory.Gold(50, "亵渎圣堂", enableDivinePunishment: true)),
                _ => Leave("你静默离开神殿。")
            };
        }

        static ExpeditionEventOutcome PlanInjuredAdventurer(
            ExpeditionRunState run,
            ExpeditionConfig config,
            int choice,
            BattleRng rng)
        {
            return choice switch
            {
                0 => TeamHpThen(
                    run,
                    -15,
                    "冒险者感激地留下一件遗物。",
                    deferredAction: state =>
                    {
                        var relicId = ExpeditionRewardPickupFactory.RollRelicId(state, rng);
                        if (!string.IsNullOrEmpty(relicId))
                            state.PendingDeferredReward = ExpeditionRewardPickupFactory.Relic(relicId, "冒险者的谢礼");
                    }),
                1 => PlanInjuredLoot(run, config, rng),
                _ => Leave("你选择无视，继续赶路。")
            };
        }

        static ExpeditionEventOutcome PlanInjuredLoot(
            ExpeditionRunState run,
            ExpeditionConfig config,
            BattleRng rng)
        {
            run.Modifiers.LootedInjuredAdventurer = true;
            run.EventFlags.Add("looted_adventurer");

            var pickup = ExpeditionRewardPickupFactory.Gold(20, "搜刮冒险者");
            if (config != null &&
                ExpeditionCardPool.TryRollCardReward(config, run, CardRarity.Common, rng, out var card, out var owner))
            {
                pickup.CardDefinitionId = card.DefinitionId;
                pickup.CardOwnerCharacterId = string.IsNullOrEmpty(card.OwnerCharacterId)
                    ? owner.CharacterDefinitionId
                    : card.OwnerCharacterId;
                pickup.CardDisplayName = card.DisplayName;
            }

            return MessageThenReward("你搜刮了冒险者。", pickup);
        }

        static ExpeditionEventOutcome PlanMagicSpring(ExpeditionRunState run, int choice, BattleRng rng)
        {
            if (choice == 2)
                return Leave("你没有触碰泉水。");

            if (choice == 1)
            {
                return MessageThenReward(
                    "你带走了 2 瓶泉水。",
                    ExpeditionRewardPickupFactory.Consumable(ConsumableIds.SpringBottle, 2, "魔法泉"));
            }

            var roll = Roll100(run, rng);
            if (roll < 60)
                return TeamHealThen(run, 25, "泉水治愈了队伍。");

            if (roll < 85)
            {
                var outcome = new ExpeditionEventOutcome { Message = "一名队员永久 ATK+2。" };
                outcome.InteractionSteps.Add(new ExpeditionEventInteractionStep
                {
                    Kind = ExpeditionEventStepKind.PickMemberForBuff,
                    PersonalAttackBonus = 2
                });
                outcome.InteractionSteps.Add(new ExpeditionEventInteractionStep
                {
                    Kind = ExpeditionEventStepKind.ShowMessage,
                    Message = "一名队员永久 ATK+2。"
                });
                outcome.DeferredOutcome = BuildDeferredRewardOutcome(
                    "一名队员永久 ATK+2。",
                    ExpeditionRewardPickupFactory.MemberPersonalAttack("魔法泉", 2));
                return outcome;
            }

            return TeamHpThen(run, -15, "泉水有毒，全队失去 15% HP。");
        }

        static ExpeditionEventOutcome PlanGamblerDice(ExpeditionRunState run, int choice, BattleRng rng)
        {
            if (choice == 2)
                return Leave("你没有参与赌博。");

            if (choice == 0)
            {
                if (run.Gold < 20)
                    return Fail("金币不足。");

                run.Gold -= 20;
                if (Roll100(run, rng) < 50)
                {
                    return MessageThenReward(
                        "小赌获胜：+50 金币。",
                        ExpeditionRewardPickupFactory.Gold(50, "小赌获胜"));
                }

                return MessageOnly("小赌失败，金币打了水漂。");
            }

            var all = run.Gold;
            run.Gold = 0;
            var big = Roll100(run, rng);
            if (big < 40)
            {
                return MessageThenReward(
                    "大赌翻倍！",
                    ExpeditionRewardPickupFactory.Gold(all * 2, "大赌翻倍"));
            }

            if (big < 70)
                return MessageOnly("大赌输光所有金币。");

            var relicId = ExpeditionRewardPickupFactory.RollRelicId(run, rng);
            return MessageThenReward(
                string.IsNullOrEmpty(relicId) ? "大赌失败，什么也没得到。" : "大赌获得稀有遗物！",
                ExpeditionRewardPickupFactory.Relic(relicId, "大赌遗物"));
        }

        static ExpeditionEventOutcome PlanMirrorPhantom(
            ExpeditionRunState run,
            int choice,
            ExpeditionConfig config,
            BattleRng rng)
        {
            return choice switch
            {
                0 => PlanMirrorBattle(run, config, rng),
                1 => MessageThenReward(
                    "镜之碎片落入手心。",
                    ExpeditionRewardPickupFactory.Consumable(ConsumableIds.MirrorShard, 1, "镜中幻影")),
                _ => Leave("你离开了魔镜。")
            };
        }

        static ExpeditionEventOutcome PlanMirrorBattle(
            ExpeditionRunState run,
            ExpeditionConfig config,
            BattleRng rng)
        {
            run.PendingEventBattleVictoryReward = null;
            run.PendingEventBattleBonusXp = 5;
            if (config != null &&
                ExpeditionCardPool.TryRollCardReward(config, run, CardRarity.SuperRare, rng, out var card, out var owner))
            {
                run.PendingEventBattleVictoryReward =
                    ExpeditionRewardPickupFactory.Card(card, owner, "镜中挑战");
            }

            return new ExpeditionEventOutcome
            {
                Message = "镜中挑战开始！",
                StartsCombat = true,
                EventBattleKey = MirrorPhantomEncounterBuilder.BattleKey
            };
        }

        static ExpeditionEventOutcome PlanCursedBookshelf(
            ExpeditionRunState run,
            int choice,
            BattleRng rng,
            ExpeditionConfig config)
        {
            return choice switch
            {
                0 => PlanCursedBookshelfRead(run, rng, config),
                1 => MessageThenReward(
                    "带走古卷残页。",
                    ExpeditionRewardPickupFactory.Consumable(ConsumableIds.ScrollPage, 1, "被诅咒的书架")),
                _ => Leave("你合上了书。")
            };
        }

        static ExpeditionEventOutcome PlanCursedBookshelfRead(
            ExpeditionRunState run,
            BattleRng rng,
            ExpeditionConfig config)
        {
            if (run.Party.Count == 0)
                return MessageOnly("无人阅读古书。");

            var idx = rng.NextIndex(run.Party.Count);
            var member = run.Party[idx];
            var message = $"{member.DisplayName} 失去 10 HP，获得一张蓝色卡牌。请点击领取。";

            return SingleMemberHpThen(
                run,
                member.CharacterDefinitionId,
                0,
                -10,
                message,
                deferredAction: state =>
                {
                    if (config == null ||
                        !ExpeditionCardPool.TryRollCardReward(config, state, CardRarity.SuperRare, rng, out var card, out var owner))
                        return;

                    var cardOwner = card.OwnerCharacterId == member.CharacterDefinitionId
                                    || string.IsNullOrEmpty(card.OwnerCharacterId)
                        ? member
                        : owner;

                    state.PendingDeferredReward =
                        ExpeditionRewardPickupFactory.Card(card, cardOwner, "古书奖励");
                });
        }

        static ExpeditionEventOutcome PlanAdventurerRevenge(ExpeditionRunState run, int choice)
        {
            return choice switch
            {
                0 when run.Gold >= 40 => MessageThenReward(
                    "对方接受赔偿，并告知前方路况。",
                    null,
                    state =>
                    {
                        state.Gold -= 40;
                        state.Modifiers.ForeseenLayerCount = Math.Max(state.Modifiers.ForeseenLayerCount, 3);
                        RevealForeseenLayers(state);
                    }),
                0 => Fail("金币不足，无法赔偿（需要 40 金币）。"),
                1 => new ExpeditionEventOutcome
                {
                    Message = "冒险者发起复仇战！",
                    StartsCombat = true,
                    EventBattleKey = AdventurerRevengeEncounterBuilder.BattleKey
                }.Also(() =>
                {
                    run.PendingEventBattleBonusXp = 8;
                    run.PendingEventBattleVictoryReward =
                        ExpeditionRewardPickupFactory.Gold(30, "复仇战利品");
                }),
                _ => TeamHpThen(run, -5, "你在混乱中逃离。")
            };
        }

        static ExpeditionEventOutcome PlanTrainingDummy(ExpeditionRunState run, int choice)
        {
            return choice switch
            {
                0 => TeamHpThen(
                    run,
                    -10,
                    "全队 DEF+1。",
                    ExpeditionRewardPickupFactory.TeamStats("训练木桩", teamDefense: 1)),
                1 => PlanTrainingDummySolo(run),
                _ => TeamHealThen(run, 10, "短暂休息后继续前行。")
            };
        }

        static ExpeditionEventOutcome PlanTrainingDummySolo(ExpeditionRunState run)
        {
            const string message = "特训成功：该角色 ATK+2。";
            var outcome = new ExpeditionEventOutcome { Message = message };
            outcome.InteractionSteps.Add(new ExpeditionEventInteractionStep
            {
                Kind = ExpeditionEventStepKind.PickMemberHpLoss,
                PercentHpDelta = -20
            });
            outcome.InteractionSteps.Add(new ExpeditionEventInteractionStep
            {
                Kind = ExpeditionEventStepKind.PickMemberForBuff,
                PersonalAttackBonus = 2
            });
            outcome.InteractionSteps.Add(new ExpeditionEventInteractionStep
            {
                Kind = ExpeditionEventStepKind.ShowMessage,
                Message = message
            });
            outcome.DeferredOutcome = BuildDeferredRewardOutcome(
                message,
                ExpeditionRewardPickupFactory.MemberPersonalAttack("训练木桩", 2));
            return outcome;
        }

        static ExpeditionEventOutcome PlanSoulRift(ExpeditionRunState run, int choice, BattleRng rng)
        {
            return choice switch
            {
                0 => MessageThenReward(
                    "裂隙能量涌入：能量上限 +1。",
                    ExpeditionRewardPickupFactory.TeamStats(
                        "灵魂裂隙",
                        energyCap: 1,
                        enableSoulRiftBattleStartRandomHpLoss: true)),
                1 => PlanSoulRiftSeal(run, rng),
                _ => Leave("你绕行而过。")
            };
        }

        static ExpeditionEventOutcome PlanSoulRiftSeal(ExpeditionRunState run, BattleRng rng)
        {
            var relicId = ExpeditionRewardPickupFactory.RollRelicId(run, rng);
            const string message = "裂隙被封印，留下一件遗物。";
            var outcome = new ExpeditionEventOutcome();
            outcome.InteractionSteps.Add(new ExpeditionEventInteractionStep
            {
                Kind = ExpeditionEventStepKind.PickCardRemove
            });
            outcome.InteractionSteps.Add(new ExpeditionEventInteractionStep
            {
                Kind = ExpeditionEventStepKind.ShowMessage,
                Message = message
            });
            outcome.DeferredOutcome = BuildDeferredRewardOutcome(
                message,
                ExpeditionRewardPickupFactory.Relic(relicId, "灵魂裂隙"));
            return outcome;
        }

        static ExpeditionEventOutcome PlanWanderingSmith(ExpeditionRunState run, int choice)
        {
            return choice switch
            {
                0 when run.Gold >= 15 => PlanSmithUpgrade(run),
                0 => Fail("金币不足（需要 15 金币）。"),
                1 => PlanSmithFusion(run),
                _ => Leave("你谢绝了铁匠。")
            };
        }

        static ExpeditionEventOutcome PlanSmithUpgrade(ExpeditionRunState run)
        {
            run.Gold -= 15;
            var outcome = new ExpeditionEventOutcome();
            outcome.InteractionSteps.Add(new ExpeditionEventInteractionStep
            {
                Kind = ExpeditionEventStepKind.PickCardUpgrade,
                PersonalAttackBonus = 20
            });
            outcome.InteractionSteps.Add(new ExpeditionEventInteractionStep
            {
                Kind = ExpeditionEventStepKind.ShowMessage,
                Message = "铁匠接过金币，用锤子在你选中的卡牌上敲打出新的纹路，使其威力增强。"
            });
            return outcome;
        }

        static ExpeditionEventOutcome PlanSmithFusion(ExpeditionRunState run)
        {
            const string message = "铁匠将两张卡牌投入炉火，熔炼出一张品质更高的新卡牌。";
            var outcome = new ExpeditionEventOutcome { Message = message };
            outcome.InteractionSteps.Add(new ExpeditionEventInteractionStep
            {
                Kind = ExpeditionEventStepKind.PickTwoCardsForFusion
            });
            outcome.InteractionSteps.Add(new ExpeditionEventInteractionStep
            {
                Kind = ExpeditionEventStepKind.ShowMessage,
                Message = message
            });
            outcome.DeferredOutcome = BuildDeferredRewardOutcome(message);
            return outcome;
        }

        static ExpeditionEventOutcome PlanTiredCamp(ExpeditionRunState run, int choice, BattleRng rng)
        {
            return choice switch
            {
                0 => TeamHealThen(
                    run,
                    30,
                    "深度休息：跳过下一层路线，全队回复 30% HP。",
                    state => state.Modifiers.SkipNextRouteSelect = true),
                1 => TeamHealThen(run, 15, "简单休息后恢复了一些体力。"),
                _ => MessageThenReward(
                    "搜刮营地。",
                    ExpeditionRewardPickupFactory.Gold(rng.NextInt(10, 26), "营地搜刮"))
            };
        }

        static ExpeditionEventOutcome PlanJadeWorkshop(ExpeditionRunState run, int choice)
        {
            if (!run.Relics.Contains(RelicIds.JadeStone))
                return Fail("你没有合适的材料。");

            return choice switch
            {
                0 => MessageThenReward(
                    "进化完成：翡翠戒指。",
                    ExpeditionRewardPickupFactory.RelicEvolution(RelicIds.JadeStone, RelicIds.JadeRing, "玉匠工坊")),
                1 => MessageThenReward(
                    "进化完成：翡翠短刀。",
                    ExpeditionRewardPickupFactory.RelicEvolution(RelicIds.JadeStone, RelicIds.JadeDagger, "玉匠工坊")),
                _ => Leave("你离开了工坊。")
            };
        }

        static ExpeditionEventOutcome PlanAncientFurnace(ExpeditionRunState run, int choice, BattleRng rng)
        {
            if (choice == 2)
                return PlanAncientFurnaceExplore(run, rng);

            if (!run.Relics.Contains(RelicIds.BurningBoots))
                return Fail("熔炉对你的装备没有反应。");

            return choice switch
            {
                0 => TeamHpThen(
                    run,
                    -10,
                    "淬火完成：燃烬之靴进化为赤红烈焰靴。",
                    ExpeditionRewardPickupFactory.RelicEvolution(
                        RelicIds.BurningBoots,
                        RelicIds.CrimsonBurningBoots,
                        "古老熔炉")),
                _ => Leave("你保留了原样的靴子。")
            };
        }

        static ExpeditionEventOutcome PlanAncientFurnaceExplore(ExpeditionRunState run, BattleRng rng)
        {
            var roll = Roll100(run, rng);
            if (roll < 40)
            {
                run.PendingEventBattleBonusXp = 10;
                return new ExpeditionEventOutcome
                {
                    Message = "石傀儡从熔渣中苏醒！",
                    StartsCombat = true,
                    EventBattleKey = AncientFurnaceEncounterBuilder.BattleKey
                };
            }

            if (roll < 70)
            {
                var relicId = ExpeditionRewardPickupFactory.RollRelicId(run, rng);
                return MessageThenReward(
                    "你在炉灰中翻找出一件遗物。",
                    ExpeditionRewardPickupFactory.Relic(relicId, "古老熔炉"));
            }

            return Leave("你仔细搜索了一番，但什么也没找到。");
        }

        static ExpeditionEventOutcome PlanAbyssWhisper(
            ExpeditionRunState run,
            int choice,
            BattleRng rng,
            ExpeditionConfig config)
        {
            return choice switch
            {
                0 => PlanAbyssListen(run, rng, config),
                1 => PlanAbyssSacrifice(run),
                _ => Leave("你捂耳离开。")
            };
        }

        static ExpeditionEventOutcome PlanAbyssListen(
            ExpeditionRunState run,
            BattleRng rng,
            ExpeditionConfig config)
        {
            PartyMemberSnapshot ranger = null;
            foreach (var member in run.Party)
            {
                if (member.CharacterDefinitionId != "char_ranger")
                    continue;

                ranger = member;
                break;
            }

            if (ranger == null)
                return Fail("队伍中没有恶魔。");

            return SingleMemberHpThen(
                run,
                "char_ranger",
                -20,
                0,
                "恶魔听懂了低语，获得一张专属卡牌。",
                deferredAction: state =>
                {
                    if (config == null ||
                        !ExpeditionCardPool.TryRollCardReward(config, state, CardRarity.Epic, rng, out var card, out _))
                        return;

                    card.OwnerCharacterId = "char_ranger";
                    state.PendingDeferredReward =
                        ExpeditionRewardPickupFactory.Card(card, ranger, "深渊低语");
                });
        }

        static ExpeditionEventOutcome PlanAbyssSacrifice(ExpeditionRunState run)
        {
            const string message = "记忆献祭完成：全队 ATK+1。";
            var outcome = new ExpeditionEventOutcome();
            outcome.InteractionSteps.Add(new ExpeditionEventInteractionStep
            {
                Kind = ExpeditionEventStepKind.PickCardRemove
            });
            outcome.InteractionSteps.Add(new ExpeditionEventInteractionStep
            {
                Kind = ExpeditionEventStepKind.ShowMessage,
                Message = message
            });
            outcome.DeferredOutcome = BuildDeferredRewardOutcome(
                message,
                ExpeditionRewardPickupFactory.TeamStats("深渊低语", teamAttack: 1));
            return outcome;
        }

        static ExpeditionEventOutcome TeamHpThen(
            ExpeditionRunState run,
            int percent,
            string message,
            ExpeditionRewardPickup statPickup = null,
            Action<ExpeditionRunState> deferredAction = null)
        {
            var outcome = new ExpeditionEventOutcome { Message = message };
            outcome.InteractionSteps.Add(new ExpeditionEventInteractionStep
            {
                Kind = ExpeditionEventStepKind.ShowTeamHpLoss,
                PercentHpDelta = percent
            });
            outcome.InteractionSteps.Add(new ExpeditionEventInteractionStep
            {
                Kind = ExpeditionEventStepKind.ShowMessage,
                Message = message
            });
            outcome.DeferredOutcome = BuildDeferredRewardOutcome(message, statPickup, deferredAction, run);
            return outcome;
        }

        static ExpeditionEventOutcome SingleMemberHpThen(
            ExpeditionRunState run,
            string characterId,
            int percent,
            int flat,
            string message,
            Action<ExpeditionRunState> deferredAction = null)
        {
            var outcome = new ExpeditionEventOutcome { Message = message };
            outcome.InteractionSteps.Add(new ExpeditionEventInteractionStep
            {
                Kind = ExpeditionEventStepKind.ShowTeamHpLoss,
                PercentHpDelta = percent,
                FlatHpDelta = flat,
                TargetCharacterId = characterId
            });
            outcome.InteractionSteps.Add(new ExpeditionEventInteractionStep
            {
                Kind = ExpeditionEventStepKind.ShowMessage,
                Message = message
            });
            outcome.DeferredOutcome = BuildDeferredRewardOutcome(message, null, deferredAction, run);
            return outcome;
        }

        static ExpeditionEventOutcome BuildDeferredRewardOutcome(
            string message,
            ExpeditionRewardPickup pickup = null,
            Action<ExpeditionRunState> deferredAction = null,
            ExpeditionRunState run = null)
        {
            var deferred = new ExpeditionEventOutcome { Message = message };

            if (run?.PendingDeferredReward != null)
            {
                deferred.PendingRewardPickup = run.PendingDeferredReward;
                run.PendingDeferredReward = null;
            }
            else if (pickup != null && pickup.HasAnyReward)
            {
                pickup.Kind = RewardPickupKind.EventOrShrine;
                if (string.IsNullOrEmpty(pickup.HeaderText))
                    pickup.HeaderText = "拾取奖励";
                deferred.PendingRewardPickup = pickup;
            }

            if (deferredAction != null)
                deferred.DeferredRunAction = deferredAction;

            return deferred;
        }

        static ExpeditionEventOutcome BuildFinishOutcome(
            string message,
            ExpeditionRewardPickup pickup) =>
            BuildDeferredRewardOutcome(message, pickup);

        static ExpeditionEventOutcome MessageOnly(string message)
        {
            var outcome = new ExpeditionEventOutcome { Message = message };
            outcome.InteractionSteps.Add(new ExpeditionEventInteractionStep
            {
                Kind = ExpeditionEventStepKind.ShowMessage,
                Message = message
            });
            outcome.DeferredOutcome = new ExpeditionEventOutcome { Message = message };
            return outcome;
        }

        static ExpeditionEventOutcome MessageThenReward(
            string message,
            ExpeditionRewardPickup pickup,
            Action<ExpeditionRunState> deferredAction = null)
        {
            var outcome = new ExpeditionEventOutcome { Message = message };
            outcome.InteractionSteps.Add(new ExpeditionEventInteractionStep
            {
                Kind = ExpeditionEventStepKind.ShowMessage,
                Message = message
            });
            outcome.DeferredOutcome = BuildDeferredRewardOutcome(message, pickup, deferredAction);
            return outcome;
        }

        static ExpeditionRewardPickup RewardCardPickup(
            CardTemplate card,
            PartyMemberSnapshot owner,
            string header) =>
            ExpeditionRewardPickupFactory.Card(card, owner, header);

        static int Roll100(ExpeditionRunState run, BattleRng rng)
        {
            if (run?.EventResolutionFixedRoll100 is int fixedRoll)
                return fixedRoll;

            return rng.NextIndex(100);
        }

        static ExpeditionEventOutcome Leave(string message) => new() { Message = message };

        static ExpeditionEventOutcome Fail(string message) => new() { Message = message };

        static ExpeditionEventOutcome TeamHealThen(
            ExpeditionRunState run,
            int percent,
            string message,
            Action<ExpeditionRunState> deferredAction = null)
        {
            var outcome = new ExpeditionEventOutcome { Message = message };
            outcome.InteractionSteps.Add(new ExpeditionEventInteractionStep
            {
                Kind = ExpeditionEventStepKind.ShowTeamHpLoss,
                PercentHpDelta = percent
            });
            outcome.InteractionSteps.Add(new ExpeditionEventInteractionStep
            {
                Kind = ExpeditionEventStepKind.ShowMessage,
                Message = message
            });
            outcome.DeferredOutcome = deferredAction != null
                ? BuildDeferredRewardOutcome(message, null, deferredAction)
                : new ExpeditionEventOutcome { Message = message };
            return outcome;
        }

        static void RevealForeseenLayers(ExpeditionRunState run)
        {
            if (run.Map == null)
                return;

            var start = run.Map.NodesCompleted + 1;
            for (var i = 0; i < run.Modifiers.ForeseenLayerCount; i++)
            {
                var layer = run.Map.GetLayer(start + i);
                if (layer != null)
                    layer.IsRevealed = true;
            }
        }

        static ExpeditionEventOutcome Also(this ExpeditionEventOutcome outcome, Action action)
        {
            action?.Invoke();
            return outcome;
        }
    }
}
