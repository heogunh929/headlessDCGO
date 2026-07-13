// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/CanNotBeDeletedByEffect.cs
// (EFFECT-MODEL REBUILD / bridge W2, Group A) AS-IS-signature `Task` overload; delegates to the verified
// substrate `GainCanNotBeDeletedByEffect` (CardEffectCommons.cs:3345).
//
// Design item RD-W2-1 (docs/audit/rebuild_bridge_w2_notes.md): AS-IS `cardEffectCondition` is
// `Func<ICardEffect,bool>` (tests the causing EFFECT INSTANCE) but the substrate's `causingEffectPredicate`
// gate (MatchStateMutationSink.IsRestrictedFromCause / RestrictionScan) only ever supplies the causing
// effect's SOURCE CARD as a `CardSource`, so a real `ICardEffect` cannot be reconstructed at gate-evaluation
// time. `AdaptCardEffectCondition` (defined here, shared by all 7 Group A helpers in this batch) invokes the
// REAL AS-IS predicate delegate against a minimal carrier `ActivateClass` whose `EffectSourceCard` is the
// causing card and every other `ICardEffect` flag sits at its honest `SetUpICardEffect` ctor-default (false)
// — this is faithful (not a re-implementation/simplification of the predicate logic) for every confirmed
// real-usage shape (grep of all 77 Group-A call sites across the 7 helpers: `null` / `cardEffect != null` /
// `true` / `IsOpponentEffect(cardEffect, card)` — all either constant or `EffectSourceCard`-only). It is
// lossy ONLY for a predicate that also inspects a flag never set on the carrier — the one such case found is
// BT19_089's `SkillCondition` (`!cardEffect.IsDigimonEffect || !cardEffect.IsTamerEffect`, passed to
// `GainImmuneFromDPMinus`): with both flags defaulting false, the adapted predicate answers `true` whenever
// `Owner == Enemy`, dropping the "excludes an effect flagged as both Digimon- and Tamer-effect" refinement.
// BT19_089 is not yet ported (residual gap only fires when/if it is) — see the notes doc for detail.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;

public static partial class CardEffectCommons
{
    /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.GainCanNotBeDeletedByEffect(...)</c>
    /// (GiveEffect/GiveEffectToPermanent/CanNotBeDeletedByEffect.cs:10) — AS-IS-signature overload; delegates
    /// to the verified substrate implementation. See RD-W2-1 above for the <paramref name="cardEffectCondition"/>
    /// adaptation.</summary>
    public static async Task GainCanNotBeDeletedByEffect(Permanent targetPermanent, Func<ICardEffect, bool> cardEffectCondition, EffectDuration effectDuration, ICardEffect activateClass, string effectName)
    {
        GainCanNotBeDeletedByEffect(targetPermanent, AdaptCardEffectCondition(cardEffectCondition), effectDuration, activateClass?.EffectSourceCard, effectName);
        await Task.CompletedTask;
    }

    /// <summary>RD-W2-1 shared adapter (see class-level design-item note above) — reused by all 7 Group A
    /// bridge wrappers in this batch (CanNotBeDeletedByEffect / CanNoReturnToDeck(PlayerEffect) /
    /// CanNotReturnToHand(PlayerEffect) / ImmuneFromDPMinus(PlayerEffect)). Invokes the real AS-IS
    /// <c>Func&lt;ICardEffect,bool&gt;</c> predicate against a minimal cause-effect carrier built from the
    /// bare <see cref="CardSource"/> the substrate gate supplies — no re-implementation of the predicate's
    /// logic, so it stays correct for any future predicate shape that only reads <c>EffectSourceCard</c>-derived
    /// data (the confirmed common case).</summary>
    private static Func<CardSource, bool>? AdaptCardEffectCondition(Func<ICardEffect, bool> cardEffectCondition)
    {
        if (cardEffectCondition == null)
        {
            return null;
        }

        return causingCard =>
        {
            if (causingCard == null)
            {
                return false;
            }

            var carrier = new ActivateClass();
            carrier.SetUpICardEffect("(RD-W2-1 bridge cause-effect carrier)", _ => true, causingCard);
            return cardEffectCondition(carrier);
        };
    }
}
