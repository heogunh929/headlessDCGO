// Source: DCGO/Assets/Scripts/Script/CEntity_EffectController.cs
// (EFFECT-MODEL REBUILD / FOUNDATION) 1:1 mirror of the original `CEntity_EffectController` — the per-card-
// instance use-count / effect-list layer `ICardEffect.CanTrigger/CanActivate` reach through
// `CardSource.cEntity_EffectController`. `class CEntity_EffectController : MonoBehaviour` -> plain class
// (MonoBehaviour stripped). The reflection-based `AddCardEffect(string ID, string ClassName)` (AS-IS :179-241,
// `gameObject.AddComponent(Type.GetType(...))`) is STRIPPED per the FOUNDATION brief — the mirror's
// `CardEffectDispatch` (CardEffectCommons/CardEffectDispatch.cs) already plays that structural role
// (card-number -> ported `CEntity_Effect` subclass lookup), so there is nothing left to port here; a caller
// that needs a controller's `cEntity_Effect` populated should look up the dispatched type directly and set
// `cEntity_Effect` on the returned controller (no call site exists yet — MISSING.md).
//
// PER-INSTANCE STORE: AS-IS attaches one `CEntity_EffectController` Component per card GameObject (lifetime =
// the GameObject's). The headless `CardSource` is a lightweight VIEW reconstructed on every access (no stable
// per-card object), so a persistent per-(match, card-instance) store is needed for `CardSource.
// cEntity_EffectController` (added by this goal, see CardEffectCommons/CardSource.cs) to keep returning the
// SAME controller for the same card across accesses (so `UseEffectsThisTurn` actually accumulates). Backed here
// by <see cref="CEntity_EffectControllerStore"/>, keyed off the live `EngineContext` (one match) + the card's
// `HeadlessEntityId` — a minimal, additive store, not an `EngineContext` field (keeps this goal's diff to the
// FOUNDATION files only; promoting it onto `EngineContext` alongside `OnceFlags`/`PlayerTurnCounters` is a later-
// phase call, MISSING.md).

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>AS-IS <c>CEntity_EffectController</c> (CEntity_EffectController.cs:5).</summary>
public class CEntity_EffectController
{
    // AS-IS CEntity_EffectController.cs:8: "Number of skills used this turn (referenced by use limit)".
    List<ICardEffect> UseEffectsThisTurn = new List<ICardEffect>();

    #region CEntity_Effect

    // AS-IS CEntity_EffectController.cs:11.
    public CEntity_Effect cEntity_Effect { get; set; }

    #endregion

    #region Get effect list

    // AS-IS CEntity_EffectController.cs:15-28.
    public List<ICardEffect> GetCardEffects_ExceptAddedEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> GetCardEffects = new List<ICardEffect>();

        if (cEntity_Effect != null)
        {
            foreach (ICardEffect cardEffect in cEntity_Effect.GetCardEffects(timing, card))
            {
                GetCardEffects.Add(cardEffect);
            }
        }

        return GetCardEffects;
    }

    // AS-IS CEntity_EffectController.cs:29-168.
    // MISSING.md: Permanent.cardSources (Permanent.cs — the mirror `Permanent` class exposes
    // `DigivolutionCards`, not the AS-IS raw `cardSources` list including the top card itself);
    // Player.SecurityCards; CardSource.EffectList(EffectTiming) / Player.EffectList(EffectTiming) —
    // referenced verbatim throughout, per the FOUNDATION brief.
    public List<ICardEffect> GetCardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> GetCardEffects = new List<ICardEffect>();

        foreach (ICardEffect cardEffect in GetCardEffects_ExceptAddedEffects(timing, card))
        {
            GetCardEffects.Add(cardEffect);
        }

        // ADAPTATION: bridge `PermanentOfThisCard()`'s `PermanentView` to the real mirror `Permanent` — see
        // ICardEffect.ResolvePermanentOfThisCard (ICardEffect.cs, "ADAPTATION (2)") and
        // PERMANENT-PERMANENTVIEW-DUALITY in docs/audit/rebuild_p1_missing.md. `.Contains(card)` below is
        // reference-equality on a freshly-constructed `CardSource` — DESIGN ITEM CARDSOURCE-EQUALITY.
        Permanent thisPermanent = ICardEffect.ResolvePermanentOfThisCard(card);
        bool isDigivolutionCard = thisPermanent != null && thisPermanent.DigivolutionCards.Contains(card);

        if (!isDigivolutionCard)
        {
            // Effects added by other card effects
            if (timing != EffectTiming.None)
            {
                #region Effects added by other card effects
                foreach (Player player in GManager.instance.turnStateMachine.gameContext.Players_ForTurnPlayer)
                {
                    if (player != null)
                    {
                        #region Effects of permanents in play
                        foreach (Permanent permanent in player.GetFieldPermanents())
                        {
                            if (permanent.TopCard.cEntity_EffectController.cEntity_Effect != null)
                            {
                                foreach (CardSource cardSource in permanent.cardSources)
                                {
                                    if (cardSource != permanent.TopCard)
                                    {
                                        if (!permanent.IsDigimon)
                                        {
                                            continue;
                                        }
                                    }

                                    foreach (ICardEffect cardEffect in cardSource.cEntity_EffectController.cEntity_Effect.GetCardEffects(EffectTiming.None, permanent.TopCard))
                                    {
                                        if (cardEffect is IAddSkillEffect)
                                        {
                                            if (cardEffect.IsInheritedEffect == (cardSource == permanent.TopCard) || cardSource.IsFlipped)
                                            {
                                                continue;
                                            }

                                            if (((IAddSkillEffect)cardEffect).ShouldAddEffect(timing) && cardEffect.CanUse(null))
                                            {
                                                // ADAPTATION: AS-IS `card.CanNotBeAffected(cardEffect)` takes the
                                                // `ICardEffect` itself; the mirror `CardSource.CanNotBeAffected`
                                                // (CardEffectCommons/CardSource.cs:349-358, an EARLIER "MIG5
                                                // goal-5" adaptation, predating this goal) already takes the
                                                // causing effect's SOURCE CARD id instead (`HeadlessEntityId?`) —
                                                // headless `ICardEffect` objects have no stable identity to scan
                                                // immunity bindings by (see this file's DESIGN ITEM
                                                // CARDSOURCE-EQUALITY in ICardEffect.cs). Adapted to that existing
                                                // shape rather than referencing a non-existent overload.
                                                if (!card.CanNotBeAffected(cardEffect.EffectSourceCard?.InstanceId))
                                                    GetCardEffects = ((IAddSkillEffect)cardEffect).GetCardEffect(card, GetCardEffects, timing);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        #endregion

                        #region Effects from security
                        foreach (CardSource source in player.SecurityCards)
                        {
                            if (source.IsFlipped)
                                continue;

                            foreach (ICardEffect cardEffect in source.EffectList(EffectTiming.None))
                            {
                                if (cardEffect is IAddSkillEffect)
                                {
                                    if (((IAddSkillEffect)cardEffect).ShouldAddEffect(timing) && cardEffect.CanUse(null))
                                    {
                                        GetCardEffects = ((IAddSkillEffect)cardEffect).GetCardEffect(card, GetCardEffects, timing);
                                    }
                                }
                            }
                        }
                        #endregion

                        #region Effects added by players
                        foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
                        {
                            if (cardEffect is IAddSkillEffect)
                            {
                                if (((IAddSkillEffect)cardEffect).ShouldAddEffect(timing) && cardEffect.CanUse(null))
                                {
                                    GetCardEffects = ((IAddSkillEffect)cardEffect).GetCardEffect(card, GetCardEffects, timing);
                                }
                            }
                        }
                        #endregion
                    }
                }
                #endregion
            }

            // Explore about EffectTiming.None only if added by me
            else
            {
                if (thisPermanent != null)
                {
                    if (thisPermanent.TopCard.cEntity_EffectController.cEntity_Effect != null)
                    {
                        foreach (CardSource cardSource in thisPermanent.cardSources)
                        {
                            foreach (ICardEffect cardEffect in cardSource.cEntity_EffectController.cEntity_Effect.GetCardEffects(EffectTiming.None, thisPermanent.TopCard))
                            {
                                if (cardEffect is IAddSkillEffect)
                                {
                                    if (cardEffect.IsInheritedEffect == (cardSource == thisPermanent.TopCard) || cardSource.IsFlipped)
                                    {
                                        continue;
                                    }

                                    if (cardEffect.CanUse(null))
                                    {
                                        //GetCardEffects = ((IAddSkillEffect)cardEffect).GetCardEffect(card, GetCardEffects, timing);
                                    }
                                }
                            }
                        }
                    }
                }

                if (CardEffectCommons.IsExistInSecurity(card))
                {
                    foreach (ICardEffect cardEffect in card.cEntity_EffectController.cEntity_Effect.GetCardEffects(EffectTiming.None, card))
                    {
                        if (cardEffect is IAddSkillEffect)
                        {
                            if (((IAddSkillEffect)cardEffect).ShouldAddEffect(timing) && cardEffect.CanUse(null))
                            {
                                GetCardEffects = ((IAddSkillEffect)cardEffect).GetCardEffect(card, GetCardEffects, timing);
                            }
                        }
                    }
                }
            }
        }

        return GetCardEffects.Filter(cardEfect => cardEfect != null);
    }

    #endregion

    #region Reset the number of uses during that turn

    // AS-IS CEntity_EffectController.cs:172-176.
    public void InitUseCountThisTurn()
    {
        UseEffectsThisTurn = new List<ICardEffect>();
    }

    #endregion

    #region set card effects

    // AS-IS CEntity_EffectController.cs:179-241: `AddCardEffect(string ID, string ClassName)` — reflection-based
    // Unity Component instantiation (`gameObject.AddComponent(Type.GetType(ClassName))`, with a
    // `DCGO.CardEffects.{ID}.{ClassName}` / `DCGO.CardEffects.Tokens.{ClassName}` fallback, and an
    // `EmptyEffectClass` default when no matching type exists). STRIPPED per the FOUNDATION brief: the mirror's
    // `CardEffectDispatch.TryCreateForCard` already performs the structural equivalent (card-number ->
    // `CEntity_Effect` subclass lookup by reflection over the loaded assembly) — no call site assigns
    // `cEntity_Effect` through this controller yet (MISSING.md).

    #endregion

    #region Gets the number of times the effect was used this turn

    // AS-IS CEntity_EffectController.cs:245-258.
    public int GetUseCountThisTurn(ICardEffect cardEffect)
    {
        int useCount = 0;

        foreach (ICardEffect cardEffect1 in UseEffectsThisTurn)
        {
            if (cardEffect.IsSameEffect(cardEffect1))
            {
                useCount++;
            }
        }

        return useCount;
    }

    #endregion

    #region Whether the effect has reached the maximum number of times it can be used this turn.

    // AS-IS CEntity_EffectController.cs:262-265.
    public bool isOverMaxCountPerTurn(ICardEffect cardEffect, int MaxCountPerTurn)
    {
        return GetUseCountThisTurn(cardEffect) >= MaxCountPerTurn;
    }

    #endregion

    #region Register as effects used this turn

    // AS-IS CEntity_EffectController.cs:269-272.
    public void RegisterUseEffectThisTurn(ICardEffect cardEffect)
    {
        UseEffectsThisTurn.Add(cardEffect);
    }

    #endregion

    #region Remove a use of the effect this turn

    // AS-IS CEntity_EffectController.cs:276-279.
    public void RemoveUseEffectThisTurn(ICardEffect cardEffect)
    {
        UseEffectsThisTurn.Remove(cardEffect);
    }

    #endregion
}

// AS-IS CEntity_EffectController.cs:283-286: `public class EmptyEffectClass : CEntity_Effect { }` — the
// reflection fallback `AddCardEffect` attached when no ported type matched. Ported as the same trivial
// no-effects subclass (still meaningful without the reflection path: any FOUNDATION-era caller that needs an
// explicit "no effects" controller can new this up directly).
public class EmptyEffectClass : CEntity_Effect
{
}

/// <summary>(FOUNDATION, not AS-IS) Per-match, per-card-instance store backing
/// <see cref="CardSource.cEntity_EffectController"/> — see this file's header ("PER-INSTANCE STORE").</summary>
public static class CEntity_EffectControllerStore
{
    private static readonly ConditionalWeakTable<EngineContext, ConcurrentDictionary<HeadlessEntityId, CEntity_EffectController>> ByContext = new();

    public static CEntity_EffectController GetOrCreate(EngineContext context, HeadlessEntityId instanceId)
    {
        ConcurrentDictionary<HeadlessEntityId, CEntity_EffectController> perContext =
            ByContext.GetOrCreateValue(context);
        return perContext.GetOrAdd(instanceId, static _ => new CEntity_EffectController());
    }
}
