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
                    IBattle battle = new IBattle(null, null, null);
                    hashtable.Add("battle", battle);

                    Hashtable battleHashtable = new Hashtable();
                    // ADAPTATION: AS-IS `new Permanent(permanent.cardSources)` (the single-arg list ctor over an
                    // EXISTING permanent's full stack) — the mirror stack view is (context, instanceId, ownerId).
                    Permanent WinnerPermanent = new Permanent(permanent.TopCard.Context, permanent.InstanceId, permanent.OwnerId);
                    battleHashtable.Add("WinnerPermanents", new List<Permanent>() { WinnerPermanent });
                    battleHashtable.Add("WinnerPermanents_real", new List<Permanent>() { WinnerPermanent });
                    // ADAPTATION: transient single-card permanent — new Permanent(new List<CardSource>(){ opponentCard }).
                    Permanent LoserPermanent = new Permanent(opponentCard.Context, opponentCard.InstanceId, opponentCard.Owner);
                    battleHashtable.Add("LoserPermanents", new List<Permanent>() { LoserPermanent });
                    // ADAPTATION: transient single-card permanent — new Permanent(new List<CardSource>(){ opponentCard }).
                    Permanent LoserPermanents_real = new Permanent(opponentCard.Context, opponentCard.InstanceId, opponentCard.Owner) { IsDestroyedByBattle = true };
                    battleHashtable.Add("LoserPermanents_real", new List<Permanent>() { LoserPermanents_real });

                    battle.hashtable = battleHashtable;
                }

            }
        }

        return hashtable;
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
