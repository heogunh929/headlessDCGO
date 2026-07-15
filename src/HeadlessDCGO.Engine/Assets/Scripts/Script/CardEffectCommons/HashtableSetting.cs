// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/HashtableSetting.cs
// (EFFECT-MODEL REBUILD / P2 "Hashtable layer") 1:1 mirror of the AS-IS per-timing Hashtable BUILDERS. Sibling
// partial of CardEffectCommons.cs (same namespace). The key strings written here ("Permanent", "isEvolution",
// "AttackingPermanent", "CardEffect", "hashtables", ...) are the contract read back by GetFromHashtable.cs and
// MUST stay byte-identical between the two files. Only substrate plumbing is adapted (transient Permanent ctor,
// PermanentView->Permanent bridge); all logic is verbatim. UnityEngine using stripped.
// Missing (verbatim-referenced) symbols recorded in docs/audit/rebuild_p2_hashtable_missing.md.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;

public static partial class CardEffectCommons
{
    public static Hashtable CardEffectHashtable(ICardEffect cardEffect) => new Hashtable() { { "CardEffect", cardEffect } };

    #region Hashtable used when check whether the permanent can trigger [Pierce]
    public static Hashtable PierceCheckHashtableOfPermanent(Permanent permanent)
    {
        Hashtable hashtable = new Hashtable();

        if (permanent != null)
        {
            if (permanent.TopCard != null)
            {
                // ADAPTATION: AS-IS `cardSource.Owner == permanent.TopCard.Owner.Enemy` (live Player.Enemy) —
                // the mirror Owner is a HeadlessPlayerId, so the enemy id resolves via the established
                // BT2_023 `new Player(context, owner).Enemy` bridge.
                CardSource opponentCard = GManager.instance.turnStateMachine.gameContext.ActiveCardList.Find(cardSource => cardSource.Owner == new Player(permanent.TopCard.Context, permanent.TopCard.Owner).Enemy?.PlayerId);

                if (opponentCard != null)
                {
                    // ADAPTATION: AS-IS `new Permanent(permanent.cardSources)` (the single-arg list ctor over an
                    // EXISTING permanent's full stack) — the mirror stack view is (context, instanceId, ownerId).
                    Permanent WinnerPermanent = new Permanent(permanent.TopCard.Context, permanent.InstanceId, permanent.OwnerId);
                    // ADAPTATION: transient single-card permanent — new Permanent(new List<CardSource>(){ opponentCard }).
                    Permanent LoserPermanent = new Permanent(opponentCard.Context, opponentCard.InstanceId, opponentCard.Owner);
                    // ADAPTATION: transient single-card permanent — new Permanent(new List<CardSource>(){ opponentCard }).
                    Permanent LoserPermanents_real = new Permanent(opponentCard.Context, opponentCard.InstanceId, opponentCard.Owner) { IsDestroyedByBattle = true };

                    // (RD-CBTL-01) synthetic presence query — share the AS-IS battle-result key construction with the
                    // real battle finalize (BattleResolver.FinalizeAsync). LoserCard/WasTie are absent in the AS-IS
                    // presence probe; the shared builder adds them (null / false), harmless — the Pierce/Retaliation
                    // chains read only the winner/loser permanent lists.
                    IBattle battle = BuildBattleResultPayload(
                        winnerPermanents: new List<Permanent>() { WinnerPermanent },
                        winnerPermanentsReal: new List<Permanent>() { WinnerPermanent },
                        loserPermanents: new List<Permanent>() { LoserPermanent },
                        loserPermanentsReal: new List<Permanent>() { LoserPermanents_real },
                        loserCard: null,
                        wasTie: false);
                    hashtable.Add("battle", battle);
                }

            }
        }

        return hashtable;
    }
    #endregion

    #region Build the AS-IS battle-result IBattle payload (shared: synthetic Pierce presence probe + real battle finalize)
    // (RD-CBTL-01) 1:1 with AS-IS CardController.Battle :4694-4700 — populate an IBattle's own hashtable with the six
    // battle-result keys plus the "battle" self-reference. WinnerPermanents/LoserPermanents are the AS-IS
    // name/color/level SNAPSHOTS and *_real are the LIVE participant lists. The mirror Permanent is an InstanceId-
    // identity live VIEW that is zone-independent, so the identity reads the Retaliation/Pierce chains perform
    // POST-trash (cardSources.Contains / IsOpponentPermanent / IsDestroyedByBattle — the last backed by the
    // deletedByBattle metadata MarkDeletedByBattle already stamps) match the AS-IS `_real` semantics without a
    // frozen collect-time snapshot; snapshot and _real therefore share the same view list. This is the same
    // technique PierceCheckHashtableOfPermanent already used for its synthetic probe. "battle" self-references the
    // IBattle (AS-IS :4700 `hashtable.Add("battle", this)`).
    public static IBattle BuildBattleResultPayload(
        List<Permanent> winnerPermanents,
        List<Permanent> winnerPermanentsReal,
        List<Permanent> loserPermanents,
        List<Permanent> loserPermanentsReal,
        CardSource loserCard,
        bool wasTie)
    {
        IBattle battle = new IBattle(null, null, null);

        Hashtable battleHashtable = new Hashtable()
        {
            { "WinnerPermanents", winnerPermanents },
            { "WinnerPermanents_real", winnerPermanentsReal },
            { "LoserPermanents", loserPermanents },
            { "LoserPermanents_real", loserPermanentsReal },
            { "LoserCard", loserCard },
            { "WasTie", wasTie },
        };
        battleHashtable.Add("battle", battle);

        battle.hashtable = battleHashtable;
        return battle;
    }
    #endregion

    #region Hashtable used when check whether the permanent can trigger [On Deletion] effect
    public static Hashtable OnDeletionCheckHashtableOfPermanent(Permanent permanent)
    {
        Hashtable hashtable = new Hashtable();

        if (permanent != null)
        {
            if (permanent.TopCard != null)
            {
                List<Hashtable> hashtables = new List<Hashtable>();

                Hashtable hashtable1 = new Hashtable()
                {
                    // ADAPTATION: AS-IS `new Permanent(permanent.cardSources)` (single-arg list ctor over an
                    // EXISTING permanent's full stack) — mirror stack view = (context, instanceId, ownerId).
                    {"Permanent", new Permanent(permanent.TopCard.Context, permanent.InstanceId, permanent.OwnerId)}
                };
                hashtables.Add(hashtable1);

                hashtable.Add("hashtables", hashtables);
            }
        }

        return hashtable;
    }
    #endregion

    #region Hashtable used when check whether the permanent would remove field effect
    public static Hashtable WhenPermanentWouldRemoveFieldCheckHashtable(List<Permanent> permanents, ICardEffect cardEffect, IBattle battle, bool isDigixros = false)
    {
        Hashtable hashtable = new Hashtable()
        {
            {"CardEffect", cardEffect},
            {"Permanents", permanents},
            {"battle", battle},
            {"digixros", isDigixros}
        };

        return hashtable;
    }

    // ADAPTATION overload (C-Del 3c-1b battle PRE cut-in transport). The BattleResolver PRE cut-in opener has NO
    // live IBattle object (battle-rehousing non-scope, like the OnDeletionHashtable boolean overload below), so it
    // cannot use the AS-IS (List<Permanent>, ICardEffect, IBattle, bool) builder to mark the battle cause. It passes
    // the DERIVED byBattle boolean instead: this delegates to the faithful builder (cardEffect/battle = null,
    // exactly as AS-IS when the keys are absent) and stamps ByBattleCauseKey, which IsByBattle reads as a fallback
    // (OnDeletion.cs dual-read: GetBattleFromHashtable != null OR the marker). AS-IS Destroy() populates the
    // "battle" key from the LoserPermanents hashtable (CardController.cs:4700 "battle"=this → :3672 → :3694), so a
    // battle PRE cut-in reports IsByBattle=true — the effect-delete sink (3b) passes NO marker (IsByBattle=false).
    public static Hashtable WhenPermanentWouldRemoveFieldCheckHashtable(List<Permanent> permanents, bool byBattleCause, bool isDigixros = false)
    {
        Hashtable hashtable = WhenPermanentWouldRemoveFieldCheckHashtable(permanents, cardEffect: null!, battle: null!, isDigixros);

        if (byBattleCause)
        {
            hashtable[ByBattleCauseKey] = true;
        }

        return hashtable;
    }
    #endregion

    #region Hashtable used when check whether the permanent can activate [On Deletion], "Permanent leaves" or "Permanent returned to hand" effect
    public static Hashtable OnDeletionHashtable(List<Permanent> permanents, ICardEffect cardEffect, IBattle battle, bool isDPZero)
    {
        Hashtable hashtable = new Hashtable();

        if (cardEffect != null)
        {
            hashtable.Add("CardEffect", cardEffect);
        }

        if (battle != null)
        {
            hashtable.Add("battle", battle);
        }

        if (isDPZero)
        {
            hashtable.Add("DPZero", isDPZero);
        }

        List<Hashtable> hashtables = permanents
            .Clone()
            .Filter(permanent => permanent != null && permanent.TopCard != null)
            .Map(permanent =>
            {
                List<CardSource> cardSources = permanent.cardSources.Clone();
                // ADAPTATION: AS-IS `.Clone()` (shallow list copy) on the mirror's IReadOnlyList surface = `.ToList()`.
                List<string> cardNames = permanent.TopCard.CardNames.ToList();
                // ADAPTATION: the mirror `CardSource.CardColors` is string-typed; the AS-IS payload (read back as
                // List<CardColor> by GetFromHashtable's OnDeletion readers) is restored via CardSource.ToCardColorList.
                List<CardColor> cardColors = CardSource.ToCardColorList(permanent.TopCard.CardColors);

                Hashtable hashtableOfPermanent = new Hashtable()
                {
                    {"Permanent", permanent},
                    {"TopCard", permanent.TopCard},
                    {"CardSources", cardSources},
                    // ADAPTATION: AS-IS `.Clone()` (shallow list copy) on the mirror's IReadOnlyList surface = `.ToList()`.
                    {"DigivolutionSources", permanent.DigivolutionCards.ToList()},
                    {"CardNames", cardNames},
                    {"CardColors", cardColors},
                    {"HasSaveText", permanent.TopCard.HasSaveText},
                    {"Level", permanent.TopCard.Level},
                };

                return hashtableOfPermanent;
            });

        hashtable.Add("hashtables", hashtables);

        return hashtable;
    }

    // ADAPTATION overload (C2 decision-4 deletion transport). The sink/battle/deferred openers have NO live
    // IBattle/ICardEffect object (RD-C1-CARDEFFECT-IDTHREAD / battle-rehousing non-scope), so they cannot use the
    // AS-IS (List<Permanent>, ICardEffect, IBattle, bool) builder to mark the cause. They pass the DERIVED boolean
    // cause instead: byEffectCause (a non-DPZero effect delete carries a cause id) and byBattleCause (a battle
    // loser flagged by MarkDeletedByBattle). This delegates to the faithful builder (cardEffect/battle = null,
    // exactly as AS-IS when the keys are absent) and stamps the marker keys IsByEffect/IsByBattle read as a
    // fallback. A DP-zero sweep passes byEffectCause=false/byBattleCause=false/isDPZero=true, so it carries only
    // the DPZero flag — IsByBattle=false, IsByEffect=false, IsDPZeroDelete=true, matching AS-IS's DPZero-only
    // DestroyPermanentsClass hashtable (AutoProcessing.cs:477-480 / DigimonLackDPProcess).
    public static Hashtable OnDeletionHashtable(List<Permanent> permanents, bool byEffectCause, bool byBattleCause, bool isDPZero)
    {
        Hashtable hashtable = OnDeletionHashtable(permanents, cardEffect: null!, battle: null!, isDPZero);

        if (byEffectCause)
        {
            hashtable[ByEffectCauseKey] = true;
        }

        if (byBattleCause)
        {
            hashtable[ByBattleCauseKey] = true;
        }

        return hashtable;
    }
    #endregion

    #region Hashtable used when check whether the permanent can activate [On Play] [When Digivolving] or "Permanent enters the field" effect
    public static Hashtable OnEnterFieldHashtable(List<OnEnterFieldHashtableParams> hashtableParams, bool isEvolution, bool isJogress, int digiXrosCount, int assemblyCount,
    ICardEffect cardEffect)
    {
        Hashtable hashtable = new Hashtable()
        {
            {"isEvolution", isEvolution},
            {"isJogress", isJogress},
            {"DigiXrosCount", digiXrosCount },
            {"AssemblyCount", assemblyCount },
            {"isFromDigimonDigivolutionCards", hashtableParams.Some(param => param.IsFromDigimonDigivolutionCards)}
        };

        if (cardEffect != null)
        {
            hashtable.Add("CardEffect", cardEffect);
        }

        List<Hashtable> hashtables = hashtableParams
            .Clone()
            .Filter(hashtableParam => hashtableParam != null)
            .Map(hashtableParam =>
            {
                Hashtable hashtableOfPermanent = new Hashtable()
                {
                    {"Permanent", hashtableParam.Permanent},
                    {"evoRoots", hashtableParam.EvoRoots.Clone()},
                    {"evoRootTops", hashtableParam.EvoRootTops.Clone()},
                    {"Root", hashtableParam.Root},
                    {"oldLevels", hashtableParam.OldLevels.Clone()},
                    {"DigiXrosCount", hashtableParam.DigixrosCount},
                    {"AssemblyCount", hashtableParam.AssemblyCount}
                };

                return hashtableOfPermanent;
            });

        hashtable.Add("hashtables", hashtables);

        return hashtable;
    }
    #endregion

    #region Hashtable used when check whether the permanent would enter the field" effect
    public static Hashtable WouldEnterFieldHashtable(bool payCost, CardSource card, SelectCardEffect.Root root, bool isEvolution, PlayCardClass playCardClass,
    ICardEffect cardEffect, bool isJogress, List<Permanent> targetPermanents)
    {
        Hashtable hashtable = new Hashtable()
        {
            {"PayCost", payCost},
            {"Card", card},
            {"Root", root},
            {"isEvolution", isEvolution},
            {"PlayCardClass", playCardClass},
            {"CardEffect", cardEffect},
            {"isJogress", isJogress},
            {"Permanents", targetPermanents},
        };

        return hashtable;
    }
    #endregion

    #region Hashtable when a card would be linked
    public static Hashtable WouldLinkHashtable(CardSource card, Permanent targetPermanent, SelectCardEffect.Root root, ICardEffect cardEffect)
    {
        Hashtable hashtable = new Hashtable()
        {
            {"Card", card},
            {"Root", root},
            {"CardEffect", cardEffect},
            {"Permanent", targetPermanent},
        };

        return hashtable;
    }
    #endregion

    #region Hashtable used when check whether the card can trigger [On Play] effect
    public static Hashtable OnPlayCheckHashtableOfCard(CardSource cardSource)
    {
        return new Hashtable()
        {
            {"isEvolution", false},
            {
                "hashtables", new List<Hashtable>()
                {
                    new Hashtable()
                    {
                        // ADAPTATION: transient single-card permanent — new Permanent(new List<CardSource>(){cardSource}).
                        {"Permanent", new Permanent(cardSource.Context, cardSource.InstanceId, cardSource.Owner) },
                    }
                }
            },
        };
    }
    #endregion

    #region Hashtable used when check whether the card can trigger [When Digivolving] effect
    public static Hashtable WhenDigivolvingCheckHashtableOfCard(CardSource cardSource)
    {
        return new Hashtable()
        {
            {"isEvolution", true},
            {
                "hashtables", new List<Hashtable>()
                {
                    new Hashtable()
                    {
                        // ADAPTATION: transient single-card permanent — new Permanent(new List<CardSource>(){cardSource}).
                        {"Permanent", new Permanent(cardSource.Context, cardSource.InstanceId, cardSource.Owner) },
                    }
                }
            },
        };
    }
    #endregion

    #region Hashtable used when check whether the card can trigger option [Main] effect
    public static Hashtable OptionMainCheckHashtable(CardSource cardSource)
    {
        return new Hashtable()
        {
            {"Card", cardSource },
        };
    }
    #endregion

    #region Hashtable used when check whether the permanent can trigger [On Play] effect
    public static Hashtable OnPlayCheckHashtableOfPermanent(Permanent permanent)
    {
        return new Hashtable()
        {
            {"isEvolution", false},
            {
                "hashtables", new List<Hashtable>()
                {
                    new Hashtable()
                    {
                        {"Permanent", permanent },
                    }
                }
            },
        };
    }
    #endregion

    #region Hashtable used when check whether the permanent can trigger [When Digivolving] effect
    public static Hashtable WhenDigivolutionCheckHashtableOfPermanent(Permanent permanent)
    {
        return new Hashtable()
        {
            {"isEvolution", true},
            {
                "hashtables", new List<Hashtable>()
                {
                    new Hashtable()
                    {
                        {"Permanent", permanent },
                    }
                }
            },
        };
    }
    #endregion

    #region Hashtable used when check whether the card can trigger [When Attacking] effect
    public static Hashtable OnAttackCheckHashtableOfCard(CardSource cardSource, ICardEffect cardEffect)
    {
        return new Hashtable()
        {
            // ADAPTATION: PermanentView->Permanent bridge (cardSource.PermanentOfThisCard() returns a PermanentView);
            // ?? falls back to the transient single-card permanent (new Permanent(new List<CardSource>(){cardSource})).
            {"AttackingPermanent", ICardEffect.ResolvePermanentOfThisCard(cardSource) ?? new Permanent(cardSource.Context, cardSource.InstanceId, cardSource.Owner)},
            {"CardEffect", cardEffect},
        };
    }
    #endregion

    #region Hashtable used when check whether the permanent can trigger [When Attacking] effect
    public static Hashtable OnAttackCheckHashtableOfPermanent(Permanent attackingPermanent, ICardEffect cardEffect)
    {
        return new Hashtable()
        {
            {"AttackingPermanent", attackingPermanent},
            {"CardEffect", cardEffect},
        };
    }
    #endregion

    #region Hashtable used when check whether the permanent would remove field effect
    public static Hashtable WhenDigivolutionCardWouldDiscardedCheckHashtable(Permanent targetPermanent, List<CardSource> cardSources, ICardEffect cardEffect)
    {
        Hashtable hashtable = new Hashtable()
        {
            {"CardEffect", cardEffect},
            {"Permanent", targetPermanent},
            {"DiscardedCards", cardSources},
        };

        return hashtable;
    }
    #endregion
}
