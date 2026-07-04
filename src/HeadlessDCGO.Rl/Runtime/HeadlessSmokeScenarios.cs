namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Services;

// TODO: Move these scenarios into tests once executable test infrastructure exists.
public static class HeadlessSmokeScenarios
{
    public static HeadlessScenario EmptyTwoPlayer(
        string name = "empty-two-player",
        int randomSeed = 0)
    {
        HeadlessPlayerId playerOne = new(1);
        HeadlessPlayerId playerTwo = new(2);

        return new HeadlessScenario(
            name,
            CreateConfig(playerOne, playerTwo, randomSeed),
            Array.Empty<LegalAction>());
    }

    public static HeadlessScenario RandomSeedIsObserved(
        string name = "random-seed-is-observed",
        int randomSeed = 12345)
    {
        HeadlessPlayerId playerOne = new(1);
        HeadlessPlayerId playerTwo = new(2);

        return new HeadlessScenario(
            name,
            CreateConfig(playerOne, playerTwo, randomSeed),
            Array.Empty<LegalAction>());
    }

    public static HeadlessScenario SeedDeckFromDeckList(
        string name = "seed-deck-from-deck-list",
        int randomSeed = 0,
        HeadlessPlayerId? playerId = null)
    {
        HeadlessPlayerId playerOne = new(1);
        HeadlessPlayerId playerTwo = new(2);
        HeadlessPlayerId owner = playerId ?? playerOne;
        DeckList deckList = new(
            "smoke-deck",
            MainDeck: new[]
            {
                new DeckListEntry(new HeadlessEntityId("deck-smoke-card-001"), Count: 2),
                new DeckListEntry(new HeadlessEntityId("deck-smoke-card-002"), Count: 1)
            },
            DigitamaDeck: new[]
            {
                new DeckListEntry(new HeadlessEntityId("deck-smoke-digitama-001"), Count: 2)
            });

        return new HeadlessScenario(
            name,
            CreateConfig(playerOne, playerTwo, randomSeed),
            Array.Empty<LegalAction>())
        {
            Setup = new HeadlessScenarioSetup(
                ZoneSeeds: Array.Empty<HeadlessZoneSeed>(),
                LegalActions: Array.Empty<LegalAction>(),
                DeckSeeds: new[]
                {
                    new HeadlessDeckSeed(owner, deckList)
                })
        };
    }

    public static HeadlessScenario ConsumeSeededLegalAction(
        string name = "consume-seeded-legal-action",
        int randomSeed = 0,
        HeadlessPlayerId? actingPlayerId = null)
    {
        HeadlessPlayerId playerOne = new(1);
        HeadlessPlayerId playerTwo = new(2);
        HeadlessPlayerId actor = actingPlayerId ?? playerOne;
        LegalAction consumedAction = HeadlessActionFactory.NoOp(actor, actionId: "legal-noop");
        LegalAction remainingAction = HeadlessActionFactory.Pass(actor, actionId: "legal-pass");

        return new HeadlessScenario(
            name,
            CreateConfig(playerOne, playerTwo, randomSeed),
            new[] { consumedAction })
        {
            Setup = new HeadlessScenarioSetup(
                ZoneSeeds: Array.Empty<HeadlessZoneSeed>(),
                LegalActions: new[] { consumedAction, remainingAction })
        };
    }

    public static HeadlessScenario RejectActionOutsideMask(
        string name = "reject-action-outside-mask",
        int randomSeed = 0,
        HeadlessPlayerId? actingPlayerId = null)
    {
        HeadlessPlayerId playerOne = new(1);
        HeadlessPlayerId playerTwo = new(2);
        HeadlessPlayerId actor = actingPlayerId ?? playerOne;

        return new HeadlessScenario(
            name,
            CreateConfig(playerOne, playerTwo, randomSeed),
            new[]
            {
                HeadlessActionFactory.NoOp(actor, actionId: "not-in-action-mask")
            });
    }

    public static HeadlessScenario TerminalWin(
        string name = "terminal-win",
        int randomSeed = 0,
        HeadlessPlayerId? actingPlayerId = null,
        HeadlessPlayerId? winnerPlayerId = null,
        string reason = "Smoke terminal win.")
    {
        HeadlessPlayerId playerOne = new(1);
        HeadlessPlayerId playerTwo = new(2);
        HeadlessPlayerId actor = actingPlayerId ?? playerOne;
        HeadlessPlayerId winner = winnerPlayerId ?? playerOne;

        return new HeadlessScenario(
            name,
            CreateConfig(playerOne, playerTwo, randomSeed),
            new[]
            {
                HeadlessActionFactory.SetTerminalResult(
                    actor,
                    winnerPlayerId: winner,
                    reason: reason)
            });
    }

    public static HeadlessScenario TerminalDraw(
        string name = "terminal-draw",
        int randomSeed = 0,
        HeadlessPlayerId? actingPlayerId = null,
        string reason = "Smoke terminal draw.")
    {
        HeadlessPlayerId playerOne = new(1);
        HeadlessPlayerId playerTwo = new(2);
        HeadlessPlayerId actor = actingPlayerId ?? playerOne;

        return new HeadlessScenario(
            name,
            CreateConfig(playerOne, playerTwo, randomSeed),
            new[]
            {
                HeadlessActionFactory.SetTerminalResult(
                    actor,
                    isDraw: true,
                    reason: reason)
            });
    }

    public static HeadlessScenario MoveSeededCardToHand(
        string name = "move-seeded-card-to-hand",
        int randomSeed = 0,
        HeadlessPlayerId? actingPlayerId = null,
        HeadlessEntityId? cardId = null)
    {
        HeadlessPlayerId playerOne = new(1);
        HeadlessPlayerId playerTwo = new(2);
        HeadlessPlayerId actor = actingPlayerId ?? playerOne;
        HeadlessEntityId card = cardId ?? new HeadlessEntityId("smoke-card-001");

        return new HeadlessScenario(
            name,
            CreateConfig(playerOne, playerTwo, randomSeed),
            new[]
            {
                HeadlessActionFactory.MoveCard(
                    actor,
                    card,
                    ChoiceZone.Library,
                    ChoiceZone.Hand)
            })
        {
            Setup = new HeadlessScenarioSetup(
                ZoneSeeds: new[]
                {
                    new HeadlessZoneSeed(actor, card, ChoiceZone.Library)
                },
                LegalActions: Array.Empty<LegalAction>())
        };
    }

    public static HeadlessScenario DrawSeededLibraryCard(
        string name = "draw-seeded-library-card",
        int randomSeed = 0,
        HeadlessPlayerId? actingPlayerId = null)
    {
        HeadlessPlayerId playerOne = new(1);
        HeadlessPlayerId playerTwo = new(2);
        HeadlessPlayerId actor = actingPlayerId ?? playerOne;
        HeadlessEntityId firstCard = new("draw-smoke-card-001");
        HeadlessEntityId secondCard = new("draw-smoke-card-002");

        return new HeadlessScenario(
            name,
            CreateConfig(playerOne, playerTwo, randomSeed),
            new[]
            {
                HeadlessActionFactory.DrawCards(actor, count: 1)
            })
        {
            Setup = new HeadlessScenarioSetup(
                ZoneSeeds: new[]
                {
                    new HeadlessZoneSeed(actor, firstCard, ChoiceZone.Library),
                    new HeadlessZoneSeed(actor, secondCard, ChoiceZone.Library)
                },
                LegalActions: Array.Empty<LegalAction>())
        };
    }

    public static HeadlessScenario AddSecurityFromSeededLibrary(
        string name = "add-security-from-seeded-library",
        int randomSeed = 0,
        HeadlessPlayerId? actingPlayerId = null)
    {
        HeadlessPlayerId playerOne = new(1);
        HeadlessPlayerId playerTwo = new(2);
        HeadlessPlayerId actor = actingPlayerId ?? playerOne;
        HeadlessEntityId firstCard = new("security-smoke-card-001");
        HeadlessEntityId secondCard = new("security-smoke-card-002");
        HeadlessEntityId thirdCard = new("security-smoke-card-003");

        return new HeadlessScenario(
            name,
            CreateConfig(playerOne, playerTwo, randomSeed),
            new[]
            {
                HeadlessActionFactory.AddSecurityFromLibrary(actor, count: 2)
            })
        {
            Setup = new HeadlessScenarioSetup(
                ZoneSeeds: new[]
                {
                    new HeadlessZoneSeed(actor, firstCard, ChoiceZone.Library),
                    new HeadlessZoneSeed(actor, secondCard, ChoiceZone.Library),
                    new HeadlessZoneSeed(actor, thirdCard, ChoiceZone.Library)
                },
                LegalActions: Array.Empty<LegalAction>())
        };
    }

    public static HeadlessScenario TrashSeededSecurity(
        string name = "trash-seeded-security",
        int randomSeed = 0,
        HeadlessPlayerId? actingPlayerId = null)
    {
        HeadlessPlayerId playerOne = new(1);
        HeadlessPlayerId playerTwo = new(2);
        HeadlessPlayerId actor = actingPlayerId ?? playerOne;
        HeadlessEntityId firstCard = new("security-trash-smoke-card-001");
        HeadlessEntityId secondCard = new("security-trash-smoke-card-002");
        HeadlessEntityId thirdCard = new("security-trash-smoke-card-003");

        return new HeadlessScenario(
            name,
            CreateConfig(playerOne, playerTwo, randomSeed),
            new[]
            {
                HeadlessActionFactory.TrashSecurity(actor, count: 2)
            })
        {
            Setup = new HeadlessScenarioSetup(
                ZoneSeeds: new[]
                {
                    new HeadlessZoneSeed(actor, firstCard, ChoiceZone.Security),
                    new HeadlessZoneSeed(actor, secondCard, ChoiceZone.Security),
                    new HeadlessZoneSeed(actor, thirdCard, ChoiceZone.Security)
                },
                LegalActions: Array.Empty<LegalAction>())
        };
    }

    public static HeadlessScenario MoveSeededCardsToBattleAndBreeding(
        string name = "move-seeded-cards-to-battle-and-breeding",
        int randomSeed = 0,
        HeadlessPlayerId? actingPlayerId = null)
    {
        HeadlessPlayerId playerOne = new(1);
        HeadlessPlayerId playerTwo = new(2);
        HeadlessPlayerId actor = actingPlayerId ?? playerOne;
        HeadlessEntityId battleCard = new("battle-area-smoke-card-001");
        HeadlessEntityId breedingCard = new("breeding-area-smoke-card-001");

        return new HeadlessScenario(
            name,
            CreateConfig(playerOne, playerTwo, randomSeed),
            new[]
            {
                HeadlessActionFactory.MoveCard(
                    actor,
                    battleCard,
                    ChoiceZone.Hand,
                    ChoiceZone.BattleArea),
                HeadlessActionFactory.MoveCard(
                    actor,
                    breedingCard,
                    ChoiceZone.Hand,
                    ChoiceZone.BreedingArea)
            })
        {
            Setup = new HeadlessScenarioSetup(
                ZoneSeeds: new[]
                {
                    new HeadlessZoneSeed(actor, battleCard, ChoiceZone.Hand),
                    new HeadlessZoneSeed(actor, breedingCard, ChoiceZone.Hand)
                },
                LegalActions: Array.Empty<LegalAction>())
        };
    }

    public static HeadlessScenario HatchSeededDigitama(
        string name = "hatch-seeded-digitama",
        int randomSeed = 0,
        HeadlessPlayerId? actingPlayerId = null)
    {
        HeadlessPlayerId playerOne = new(1);
        HeadlessPlayerId playerTwo = new(2);
        HeadlessPlayerId actor = actingPlayerId ?? playerOne;
        HeadlessEntityId firstDigitama = new("digitama-smoke-card-001");
        HeadlessEntityId secondDigitama = new("digitama-smoke-card-002");

        return new HeadlessScenario(
            name,
            CreateConfig(playerOne, playerTwo, randomSeed),
            new[]
            {
                HeadlessActionFactory.HatchDigitama(actor)
            })
        {
            Setup = new HeadlessScenarioSetup(
                ZoneSeeds: new[]
                {
                    new HeadlessZoneSeed(actor, firstDigitama, ChoiceZone.DigitamaLibrary),
                    new HeadlessZoneSeed(actor, secondDigitama, ChoiceZone.DigitamaLibrary)
                },
                LegalActions: Array.Empty<LegalAction>())
        };
    }

    public static HeadlessScenario MoveSeededBreedingToBattle(
        string name = "move-seeded-breeding-to-battle",
        int randomSeed = 0,
        HeadlessPlayerId? actingPlayerId = null)
    {
        HeadlessPlayerId playerOne = new(1);
        HeadlessPlayerId playerTwo = new(2);
        HeadlessPlayerId actor = actingPlayerId ?? playerOne;
        HeadlessEntityId firstBreedingCard = new("breeding-move-smoke-card-001");
        HeadlessEntityId secondBreedingCard = new("breeding-move-smoke-card-002");

        return new HeadlessScenario(
            name,
            CreateConfig(playerOne, playerTwo, randomSeed),
            new[]
            {
                HeadlessActionFactory.MoveBreedingToBattle(actor, count: 1)
            })
        {
            Setup = new HeadlessScenarioSetup(
                ZoneSeeds: new[]
                {
                    new HeadlessZoneSeed(actor, firstBreedingCard, ChoiceZone.BreedingArea),
                    new HeadlessZoneSeed(actor, secondBreedingCard, ChoiceZone.BreedingArea)
                },
                LegalActions: Array.Empty<LegalAction>())
        };
    }

    public static HeadlessScenario DeclareSeededAttack(
        string name = "declare-seeded-attack",
        int randomSeed = 0,
        HeadlessPlayerId? actingPlayerId = null)
    {
        HeadlessPlayerId playerOne = new(1);
        HeadlessPlayerId playerTwo = new(2);
        HeadlessPlayerId actor = actingPlayerId ?? playerOne;
        HeadlessEntityId attacker = new("attack-smoke-card-001");
        HeadlessEntityId target = new("attack-target-smoke-card-001");

        return new HeadlessScenario(
            name,
            CreateConfig(playerOne, playerTwo, randomSeed),
            new[]
            {
                HeadlessActionFactory.DeclareAttack(
                    actor,
                    attacker,
                    playerTwo,
                    targetId: target)
            })
        {
            Setup = new HeadlessScenarioSetup(
                ZoneSeeds: new[]
                {
                    new HeadlessZoneSeed(actor, attacker, ChoiceZone.BattleArea),
                    new HeadlessZoneSeed(playerTwo, target, ChoiceZone.BattleArea)
                },
                LegalActions: Array.Empty<LegalAction>())
        };
    }

    public static HeadlessScenario ResolveSeededAttack(
        string name = "resolve-seeded-attack",
        int randomSeed = 0,
        HeadlessPlayerId? actingPlayerId = null,
        string reason = "Smoke attack resolved.")
    {
        HeadlessPlayerId playerOne = new(1);
        HeadlessPlayerId playerTwo = new(2);
        HeadlessPlayerId actor = actingPlayerId ?? playerOne;
        HeadlessEntityId attacker = new("attack-resolve-smoke-card-001");
        HeadlessEntityId target = new("attack-resolve-target-smoke-card-001");

        return new HeadlessScenario(
            name,
            CreateConfig(playerOne, playerTwo, randomSeed),
            new[]
            {
                HeadlessActionFactory.DeclareAttack(
                    actor,
                    attacker,
                    playerTwo,
                    targetId: target),
                HeadlessActionFactory.ResolveAttack(actor, reason)
            })
        {
            Setup = new HeadlessScenarioSetup(
                ZoneSeeds: new[]
                {
                    new HeadlessZoneSeed(actor, attacker, ChoiceZone.BattleArea),
                    new HeadlessZoneSeed(playerTwo, target, ChoiceZone.BattleArea)
                },
                LegalActions: Array.Empty<LegalAction>())
        };
    }

    public static HeadlessScenario ClearSeededAttack(
        string name = "clear-seeded-attack",
        int randomSeed = 0,
        HeadlessPlayerId? actingPlayerId = null,
        string reason = "Smoke attack resolved before clear.")
    {
        HeadlessPlayerId playerOne = new(1);
        HeadlessPlayerId playerTwo = new(2);
        HeadlessPlayerId actor = actingPlayerId ?? playerOne;
        HeadlessEntityId attacker = new("attack-clear-smoke-card-001");
        HeadlessEntityId target = new("attack-clear-target-smoke-card-001");

        return new HeadlessScenario(
            name,
            CreateConfig(playerOne, playerTwo, randomSeed),
            new[]
            {
                HeadlessActionFactory.DeclareAttack(
                    actor,
                    attacker,
                    playerTwo,
                    targetId: target),
                HeadlessActionFactory.ResolveAttack(actor, reason),
                HeadlessActionFactory.ClearAttack(actor)
            })
        {
            Setup = new HeadlessScenarioSetup(
                ZoneSeeds: new[]
                {
                    new HeadlessZoneSeed(actor, attacker, ChoiceZone.BattleArea),
                    new HeadlessZoneSeed(playerTwo, target, ChoiceZone.BattleArea)
                },
                LegalActions: Array.Empty<LegalAction>())
        };
    }

    public static HeadlessScenario RequestSeededCardChoice(
        string name = "request-seeded-card-choice",
        int randomSeed = 0,
        HeadlessPlayerId? actingPlayerId = null)
    {
        HeadlessPlayerId playerOne = new(1);
        HeadlessPlayerId playerTwo = new(2);
        HeadlessPlayerId actor = actingPlayerId ?? playerOne;
        HeadlessEntityId firstCard = new("choice-smoke-card-001");
        HeadlessEntityId secondCard = new("choice-smoke-card-002");

        return new HeadlessScenario(
            name,
            CreateConfig(playerOne, playerTwo, randomSeed),
            new[]
            {
                HeadlessActionFactory.RequestChoice(
                    actor,
                    ChoiceType.Card,
                    "Choose a smoke card.",
                    minCount: 1,
                    maxCount: 1,
                    canSkip: false,
                    sourceZone: ChoiceZone.Hand,
                    candidateIds: new[] { firstCard, secondCard })
            })
        {
            Setup = new HeadlessScenarioSetup(
                ZoneSeeds: new[]
                {
                    new HeadlessZoneSeed(actor, firstCard, ChoiceZone.Hand),
                    new HeadlessZoneSeed(actor, secondCard, ChoiceZone.Hand)
                },
                LegalActions: Array.Empty<LegalAction>())
        };
    }

    public static HeadlessScenario ResolveSeededCardChoice(
        string name = "resolve-seeded-card-choice",
        int randomSeed = 0,
        HeadlessPlayerId? actingPlayerId = null)
    {
        HeadlessPlayerId playerOne = new(1);
        HeadlessPlayerId playerTwo = new(2);
        HeadlessPlayerId actor = actingPlayerId ?? playerOne;
        HeadlessEntityId firstCard = new("choice-resolve-smoke-card-001");
        HeadlessEntityId secondCard = new("choice-resolve-smoke-card-002");

        return new HeadlessScenario(
            name,
            CreateConfig(playerOne, playerTwo, randomSeed),
            new[]
            {
                HeadlessActionFactory.RequestChoice(
                    actor,
                    ChoiceType.Card,
                    "Choose and resolve a smoke card.",
                    minCount: 1,
                    maxCount: 1,
                    canSkip: false,
                    sourceZone: ChoiceZone.Hand,
                    candidateIds: new[] { firstCard, secondCard }),
                HeadlessActionFactory.ResolveChoice(actor)
            })
        {
            Setup = new HeadlessScenarioSetup(
                ZoneSeeds: new[]
                {
                    new HeadlessZoneSeed(actor, firstCard, ChoiceZone.Hand),
                    new HeadlessZoneSeed(actor, secondCard, ChoiceZone.Hand)
                },
                LegalActions: Array.Empty<LegalAction>())
        };
    }

    public static HeadlessScenario EnqueueAndResolveEffect(
        string name = "enqueue-and-resolve-effect",
        int randomSeed = 0,
        HeadlessPlayerId? actingPlayerId = null)
    {
        HeadlessPlayerId playerOne = new(1);
        HeadlessPlayerId playerTwo = new(2);
        HeadlessPlayerId actor = actingPlayerId ?? playerOne;
        HeadlessEntityId effectId = new("effect-smoke-001");
        HeadlessEntityId sourceId = new("effect-source-smoke-card-001");

        return new HeadlessScenario(
            name,
            CreateConfig(playerOne, playerTwo, randomSeed),
            new[]
            {
                HeadlessActionFactory.EnqueueEffect(
                    actor,
                    effectId,
                    timing: "Smoke",
                    sourceEntityId: sourceId)
            });
    }

    public static HeadlessScenario AdvancePhaseToDraw(
        string name = "advance-phase-to-draw",
        int randomSeed = 0,
        HeadlessPlayerId? actingPlayerId = null)
    {
        HeadlessPlayerId playerOne = new(1);
        HeadlessPlayerId playerTwo = new(2);
        HeadlessPlayerId actor = actingPlayerId ?? playerOne;

        return new HeadlessScenario(
            name,
            CreateConfig(playerOne, playerTwo, randomSeed),
            new[]
            {
                HeadlessActionFactory.AdvancePhase(actor)
            });
    }

    public static HeadlessScenario MemorySetAddPay(
        string name = "memory-set-add-pay",
        int randomSeed = 0,
        HeadlessPlayerId? actingPlayerId = null)
    {
        HeadlessPlayerId playerOne = new(1);
        HeadlessPlayerId playerTwo = new(2);
        HeadlessPlayerId actor = actingPlayerId ?? playerOne;

        return new HeadlessScenario(
            name,
            CreateConfig(playerOne, playerTwo, randomSeed),
            new[]
            {
                HeadlessActionFactory.SetMemory(actor, 3),
                HeadlessActionFactory.AddMemory(actor, 2),
                HeadlessActionFactory.PayMemory(actor, 4)
            });
    }

    public static HeadlessRlEnvironmentOptions EnvironmentOptionsForPerspective(
        HeadlessPlayerId perspectivePlayerId,
        RlRewardOptions? rewardOptions = null)
    {
        return new HeadlessRlEnvironmentOptions
        {
            PerspectivePlayerId = perspectivePlayerId,
            RewardCalculator = rewardOptions is null
                ? TerminalRlRewardCalculator.Default
                : new TerminalRlRewardCalculator(rewardOptions)
        };
    }

    public static HeadlessScenarioExpectation EmptyTwoPlayerExpectation()
    {
        return new HeadlessScenarioExpectation
        {
            IsTerminal = false,
            StepCount = 0,
            FinalReward = 0d,
            TotalReward = 0d,
            FinalDiscount = 1d
        };
    }

    public static HeadlessScenarioExpectation RandomSeedIsObservedExpectation(
        int randomSeed = 12345)
    {
        return new HeadlessScenarioExpectation
        {
            IsTerminal = false,
            StepCount = 0,
            FinalReward = 0d,
            TotalReward = 0d,
            FinalDiscount = 1d,
            Features = new[]
            {
                new HeadlessFeatureExpectation("runtime.randomSeed.known", 1d),
                new HeadlessFeatureExpectation("runtime.randomSeed", randomSeed)
            }
        };
    }

    public static HeadlessScenarioExpectation SeedDeckFromDeckListExpectation(
        HeadlessPlayerId? playerId = null)
    {
        HeadlessPlayerId player = playerId ?? new HeadlessPlayerId(1);

        return new HeadlessScenarioExpectation
        {
            IsTerminal = false,
            StepCount = 0,
            FinalReward = 0d,
            TotalReward = 0d,
            FinalDiscount = 1d,
            ZoneCounts = new[]
            {
                new HeadlessZoneCountExpectation(player, ChoiceZone.Library, Count: 3),
                new HeadlessZoneCountExpectation(player, ChoiceZone.DigitamaLibrary, Count: 2)
            },
            Features = new[]
            {
                new HeadlessFeatureExpectation("runtime.cardInstanceCount", 5d)
            }
        };
    }

    public static HeadlessScenarioExpectation ConsumeSeededLegalActionExpectation()
    {
        return new HeadlessScenarioExpectation
        {
            IsTerminal = false,
            StepCount = 1,
            FinalReward = 0d,
            TotalReward = 0d,
            FinalDiscount = 1d,
            LegalActionCount = 1,
            LastActionProcessed = true,
            LastActionRejected = false,
            LastActionType = HeadlessActionTypes.NoOp,
            EventTypes = new[]
            {
                new HeadlessEventTypeExpectation(GameEventType.ActionQueued),
                new HeadlessEventTypeExpectation(GameEventType.ActionProcessed)
            }
        };
    }

    public static HeadlessScenarioExpectation RejectActionOutsideMaskExpectation()
    {
        return new HeadlessScenarioExpectation
        {
            IsTerminal = false,
            StepCount = 1,
            FinalReward = -1d,
            TotalReward = -1d,
            FinalDiscount = 1d,
            LegalActionCount = 0,
            LastActionProcessed = false,
            LastActionRejected = true,
            LastActionType = HeadlessActionTypes.NoOp,
            EventTypes = new[]
            {
                new HeadlessEventTypeExpectation(GameEventType.InvalidAction)
            }
        };
    }

    public static HeadlessScenarioExpectation TerminalWinExpectation(
        HeadlessPlayerId? winnerPlayerId = null,
        double expectedReward = 1d,
        string reason = "Smoke terminal win.")
    {
        HeadlessPlayerId winner = winnerPlayerId ?? new HeadlessPlayerId(1);

        return new HeadlessScenarioExpectation
        {
            IsTerminal = true,
            StepCount = 1,
            WinnerId = winner,
            IsDraw = false,
            IsSurrender = false,
            ResultReason = reason,
            FinalReward = expectedReward,
            TotalReward = expectedReward,
            FinalDiscount = 0d,
            LastActionProcessed = true,
            LastActionRejected = false,
            LastActionType = HeadlessActionTypes.SetTerminal,
            EventTypes = new[]
            {
                new HeadlessEventTypeExpectation(GameEventType.GameEnded)
            }
        };
    }

    public static HeadlessScenarioExpectation TerminalDrawExpectation(
        double expectedReward = 0d,
        string reason = "Smoke terminal draw.")
    {
        return new HeadlessScenarioExpectation
        {
            IsTerminal = true,
            StepCount = 1,
            IsDraw = true,
            IsSurrender = false,
            ResultReason = reason,
            FinalReward = expectedReward,
            TotalReward = expectedReward,
            FinalDiscount = 0d,
            LastActionProcessed = true,
            LastActionRejected = false,
            LastActionType = HeadlessActionTypes.SetTerminal,
            EventTypes = new[]
            {
                new HeadlessEventTypeExpectation(GameEventType.GameEnded)
            }
        };
    }

    public static HeadlessScenarioExpectation MoveSeededCardToHandExpectation(
        HeadlessPlayerId? playerId = null)
    {
        HeadlessPlayerId player = playerId ?? new HeadlessPlayerId(1);

        return new HeadlessScenarioExpectation
        {
            IsTerminal = false,
            StepCount = 1,
            FinalReward = 0d,
            TotalReward = 0d,
            FinalDiscount = 1d,
            ZoneCounts = new[]
            {
                new HeadlessZoneCountExpectation(player, ChoiceZone.Library, Count: 0),
                new HeadlessZoneCountExpectation(player, ChoiceZone.Hand, Count: 1)
            }
        };
    }

    public static HeadlessScenarioExpectation DrawSeededLibraryCardExpectation(
        HeadlessPlayerId? playerId = null)
    {
        HeadlessPlayerId player = playerId ?? new HeadlessPlayerId(1);

        return new HeadlessScenarioExpectation
        {
            IsTerminal = false,
            StepCount = 1,
            FinalReward = 0d,
            TotalReward = 0d,
            FinalDiscount = 1d,
            ZoneCounts = new[]
            {
                new HeadlessZoneCountExpectation(player, ChoiceZone.Library, Count: 1),
                new HeadlessZoneCountExpectation(player, ChoiceZone.Hand, Count: 1)
            }
        };
    }

    public static HeadlessScenarioExpectation AddSecurityFromSeededLibraryExpectation(
        HeadlessPlayerId? playerId = null)
    {
        HeadlessPlayerId player = playerId ?? new HeadlessPlayerId(1);

        return new HeadlessScenarioExpectation
        {
            IsTerminal = false,
            StepCount = 1,
            FinalReward = 0d,
            TotalReward = 0d,
            FinalDiscount = 1d,
            ZoneCounts = new[]
            {
                new HeadlessZoneCountExpectation(player, ChoiceZone.Library, Count: 1),
                new HeadlessZoneCountExpectation(player, ChoiceZone.Security, Count: 2)
            }
        };
    }

    public static HeadlessScenarioExpectation TrashSeededSecurityExpectation(
        HeadlessPlayerId? playerId = null)
    {
        HeadlessPlayerId player = playerId ?? new HeadlessPlayerId(1);

        return new HeadlessScenarioExpectation
        {
            IsTerminal = false,
            StepCount = 1,
            FinalReward = 0d,
            TotalReward = 0d,
            FinalDiscount = 1d,
            ZoneCounts = new[]
            {
                new HeadlessZoneCountExpectation(player, ChoiceZone.Security, Count: 1),
                new HeadlessZoneCountExpectation(player, ChoiceZone.Trash, Count: 2)
            }
        };
    }

    public static HeadlessScenarioExpectation MoveSeededCardsToBattleAndBreedingExpectation(
        HeadlessPlayerId? playerId = null)
    {
        HeadlessPlayerId player = playerId ?? new HeadlessPlayerId(1);

        return new HeadlessScenarioExpectation
        {
            IsTerminal = false,
            StepCount = 2,
            FinalReward = 0d,
            TotalReward = 0d,
            FinalDiscount = 1d,
            ZoneCounts = new[]
            {
                new HeadlessZoneCountExpectation(player, ChoiceZone.Hand, Count: 0),
                new HeadlessZoneCountExpectation(player, ChoiceZone.BattleArea, Count: 1),
                new HeadlessZoneCountExpectation(player, ChoiceZone.BreedingArea, Count: 1)
            }
        };
    }

    public static HeadlessScenarioExpectation HatchSeededDigitamaExpectation(
        HeadlessPlayerId? playerId = null)
    {
        HeadlessPlayerId player = playerId ?? new HeadlessPlayerId(1);

        return new HeadlessScenarioExpectation
        {
            IsTerminal = false,
            StepCount = 1,
            FinalReward = 0d,
            TotalReward = 0d,
            FinalDiscount = 1d,
            ZoneCounts = new[]
            {
                new HeadlessZoneCountExpectation(player, ChoiceZone.DigitamaLibrary, Count: 1),
                new HeadlessZoneCountExpectation(player, ChoiceZone.BreedingArea, Count: 1)
            }
        };
    }

    public static HeadlessScenarioExpectation MoveSeededBreedingToBattleExpectation(
        HeadlessPlayerId? playerId = null)
    {
        HeadlessPlayerId player = playerId ?? new HeadlessPlayerId(1);

        return new HeadlessScenarioExpectation
        {
            IsTerminal = false,
            StepCount = 1,
            FinalReward = 0d,
            TotalReward = 0d,
            FinalDiscount = 1d,
            ZoneCounts = new[]
            {
                new HeadlessZoneCountExpectation(player, ChoiceZone.BreedingArea, Count: 1),
                new HeadlessZoneCountExpectation(player, ChoiceZone.BattleArea, Count: 1)
            }
        };
    }

    public static HeadlessScenarioExpectation DeclareSeededAttackExpectation(
        HeadlessPlayerId? attackerPlayerId = null,
        HeadlessPlayerId? defenderPlayerId = null)
    {
        HeadlessPlayerId attackerPlayer = attackerPlayerId ?? new HeadlessPlayerId(1);
        HeadlessPlayerId defenderPlayer = defenderPlayerId ?? new HeadlessPlayerId(2);

        return new HeadlessScenarioExpectation
        {
            IsTerminal = false,
            StepCount = 1,
            FinalReward = 0d,
            TotalReward = 0d,
            FinalDiscount = 1d,
            ZoneCounts = new[]
            {
                new HeadlessZoneCountExpectation(attackerPlayer, ChoiceZone.BattleArea, Count: 1),
                new HeadlessZoneCountExpectation(defenderPlayer, ChoiceZone.BattleArea, Count: 1)
            },
            Features = new[]
            {
                new HeadlessFeatureExpectation("attack.count", 1d),
                new HeadlessFeatureExpectation("attack.isPending", 1d),
                new HeadlessFeatureExpectation("attack.isResolved", 0d),
                new HeadlessFeatureExpectation("attack.isDirect", 0d),
                new HeadlessFeatureExpectation("attack.attackingPlayerId", attackerPlayer.Value),
                new HeadlessFeatureExpectation("attack.defendingPlayerId", defenderPlayer.Value),
                new HeadlessFeatureExpectation("attack.attackerId.known", 1d),
                new HeadlessFeatureExpectation("attack.targetId.known", 1d)
            },
            EventTypes = new[]
            {
                new HeadlessEventTypeExpectation(GameEventType.AttackDeclared)
            }
        };
    }

    public static HeadlessScenarioExpectation ResolveSeededAttackExpectation(
        HeadlessPlayerId? attackerPlayerId = null,
        HeadlessPlayerId? defenderPlayerId = null)
    {
        HeadlessPlayerId attackerPlayer = attackerPlayerId ?? new HeadlessPlayerId(1);
        HeadlessPlayerId defenderPlayer = defenderPlayerId ?? new HeadlessPlayerId(2);

        return new HeadlessScenarioExpectation
        {
            IsTerminal = false,
            StepCount = 2,
            FinalReward = 0d,
            TotalReward = 0d,
            FinalDiscount = 1d,
            ZoneCounts = new[]
            {
                new HeadlessZoneCountExpectation(attackerPlayer, ChoiceZone.BattleArea, Count: 1),
                new HeadlessZoneCountExpectation(defenderPlayer, ChoiceZone.BattleArea, Count: 1)
            },
            Features = new[]
            {
                new HeadlessFeatureExpectation("attack.count", 1d),
                new HeadlessFeatureExpectation("attack.isPending", 0d),
                new HeadlessFeatureExpectation("attack.isResolved", 1d),
                new HeadlessFeatureExpectation("attack.isDirect", 0d),
                new HeadlessFeatureExpectation("attack.attackingPlayerId", attackerPlayer.Value),
                new HeadlessFeatureExpectation("attack.defendingPlayerId", defenderPlayer.Value),
                new HeadlessFeatureExpectation("attack.attackerId.known", 1d),
                new HeadlessFeatureExpectation("attack.targetId.known", 1d)
            },
            EventTypes = new[]
            {
                new HeadlessEventTypeExpectation(GameEventType.AttackDeclared),
                new HeadlessEventTypeExpectation(GameEventType.AttackResolved)
            }
        };
    }

    public static HeadlessScenarioExpectation ClearSeededAttackExpectation(
        HeadlessPlayerId? attackerPlayerId = null,
        HeadlessPlayerId? defenderPlayerId = null)
    {
        HeadlessPlayerId attackerPlayer = attackerPlayerId ?? new HeadlessPlayerId(1);
        HeadlessPlayerId defenderPlayer = defenderPlayerId ?? new HeadlessPlayerId(2);

        return new HeadlessScenarioExpectation
        {
            IsTerminal = false,
            StepCount = 3,
            FinalReward = 0d,
            TotalReward = 0d,
            FinalDiscount = 1d,
            ZoneCounts = new[]
            {
                new HeadlessZoneCountExpectation(attackerPlayer, ChoiceZone.BattleArea, Count: 1),
                new HeadlessZoneCountExpectation(defenderPlayer, ChoiceZone.BattleArea, Count: 1)
            },
            Features = new[]
            {
                new HeadlessFeatureExpectation("attack.count", 1d),
                new HeadlessFeatureExpectation("attack.isPending", 0d),
                new HeadlessFeatureExpectation("attack.isResolved", 0d),
                new HeadlessFeatureExpectation("attack.isDirect", 0d),
                new HeadlessFeatureExpectation("attack.attackingPlayerId.known", 0d),
                new HeadlessFeatureExpectation("attack.defendingPlayerId.known", 0d),
                new HeadlessFeatureExpectation("attack.attackerId.known", 0d),
                new HeadlessFeatureExpectation("attack.targetId.known", 0d)
            },
            EventTypes = new[]
            {
                new HeadlessEventTypeExpectation(GameEventType.AttackDeclared),
                new HeadlessEventTypeExpectation(GameEventType.AttackResolved),
                new HeadlessEventTypeExpectation(GameEventType.AttackCleared)
            }
        };
    }

    public static HeadlessScenarioExpectation RequestSeededCardChoiceExpectation(
        HeadlessPlayerId? playerId = null)
    {
        HeadlessPlayerId player = playerId ?? new HeadlessPlayerId(1);

        return new HeadlessScenarioExpectation
        {
            IsTerminal = false,
            StepCount = 1,
            FinalReward = 0d,
            TotalReward = 0d,
            FinalDiscount = 1d,
            ZoneCounts = new[]
            {
                new HeadlessZoneCountExpectation(player, ChoiceZone.Hand, Count: 2)
            },
            Features = new[]
            {
                new HeadlessFeatureExpectation("choice.isPending", 1d),
                new HeadlessFeatureExpectation("choice.isResolved", 0d),
                new HeadlessFeatureExpectation("choice.isSkipped", 0d),
                new HeadlessFeatureExpectation("choice.typeIndex", (int)ChoiceType.Card),
                new HeadlessFeatureExpectation("choice.playerId", player.Value),
                new HeadlessFeatureExpectation("choice.minCount", 1d),
                new HeadlessFeatureExpectation("choice.maxCount", 1d),
                new HeadlessFeatureExpectation("choice.candidateCount", 2d),
                new HeadlessFeatureExpectation("choice.selectedIds.count", 0d)
            },
            EventTypes = new[]
            {
                new HeadlessEventTypeExpectation(GameEventType.ChoiceRequested)
            }
        };
    }

    public static HeadlessScenarioExpectation ResolveSeededCardChoiceExpectation(
        HeadlessPlayerId? playerId = null)
    {
        HeadlessPlayerId player = playerId ?? new HeadlessPlayerId(1);

        return new HeadlessScenarioExpectation
        {
            IsTerminal = false,
            StepCount = 2,
            FinalReward = 0d,
            TotalReward = 0d,
            FinalDiscount = 1d,
            ZoneCounts = new[]
            {
                new HeadlessZoneCountExpectation(player, ChoiceZone.Hand, Count: 2)
            },
            Features = new[]
            {
                new HeadlessFeatureExpectation("choice.isPending", 0d),
                new HeadlessFeatureExpectation("choice.isResolved", 1d),
                new HeadlessFeatureExpectation("choice.isSkipped", 0d),
                new HeadlessFeatureExpectation("choice.typeIndex", (int)ChoiceType.Card),
                new HeadlessFeatureExpectation("choice.playerId", player.Value),
                new HeadlessFeatureExpectation("choice.candidateCount", 2d),
                new HeadlessFeatureExpectation("choice.selectedCount", 1d),
                new HeadlessFeatureExpectation("choice.selectedIds.count", 1d)
            },
            EventTypes = new[]
            {
                new HeadlessEventTypeExpectation(GameEventType.ChoiceRequested),
                new HeadlessEventTypeExpectation(GameEventType.ChoiceResolved)
            }
        };
    }

    public static HeadlessScenarioExpectation EnqueueAndResolveEffectExpectation()
    {
        return new HeadlessScenarioExpectation
        {
            IsTerminal = false,
            StepCount = 1,
            FinalReward = 0d,
            TotalReward = 0d,
            FinalDiscount = 1d,
            Features = new[]
            {
                new HeadlessFeatureExpectation("effects.pendingCount", 0d),
                new HeadlessFeatureExpectation("effects.hasPending", 0d),
                new HeadlessFeatureExpectation("effects.totalEnqueued", 1d),
                new HeadlessFeatureExpectation("effects.totalResolved", 1d),
                new HeadlessFeatureExpectation("effects.lastResolvedCount", 1d)
            },
            EventTypes = new[]
            {
                new HeadlessEventTypeExpectation(GameEventType.EffectResolved)
            }
        };
    }

    public static HeadlessScenarioExpectation AdvancePhaseToDrawExpectation(
        HeadlessPlayerId? playerId = null)
    {
        HeadlessPlayerId player = playerId ?? new HeadlessPlayerId(1);

        return new HeadlessScenarioExpectation
        {
            IsTerminal = false,
            StepCount = 1,
            FinalReward = 0d,
            TotalReward = 0d,
            FinalDiscount = 1d,
            Features = new[]
            {
                new HeadlessFeatureExpectation("turn.number", 1d),
                new HeadlessFeatureExpectation("turn.phaseIndex", (int)HeadlessPhase.Draw),
                new HeadlessFeatureExpectation("turn.phase.Draw", 1d),
                new HeadlessFeatureExpectation("turn.playerId", player.Value)
            }
        };
    }

    public static HeadlessScenarioExpectation MemorySetAddPayExpectation()
    {
        return new HeadlessScenarioExpectation
        {
            IsTerminal = false,
            StepCount = 3,
            FinalReward = 0d,
            TotalReward = 0d,
            FinalDiscount = 1d,
            Features = new[]
            {
                new HeadlessFeatureExpectation("memory.current", 1d),
                new HeadlessFeatureExpectation("memory.rawCurrent", 1d),
                new HeadlessFeatureExpectation("memory.minimum", -10d),
                new HeadlessFeatureExpectation("memory.maximum", 10d),
                new HeadlessFeatureExpectation("memory.isAtMinimum", 0d),
                new HeadlessFeatureExpectation("memory.isAtMaximum", 0d)
            }
        };
    }

    private static MatchConfig CreateConfig(
        HeadlessPlayerId playerOne,
        HeadlessPlayerId playerTwo,
        int randomSeed)
    {
        return new MatchConfig
        {
            PlayerIds = new[] { playerOne, playerTwo },
            RandomSeed = randomSeed,
            UseDeterministicChoices = true
        };
    }
}
