// 1:1 mirror of the original BT22_044 (BT22/Green) — an F1-Tier2 OnAddDigivolutionCards SELF (top-instance) witness.
//
// Ported effect (AS-IS BT22_044.cs:35-77, timing OnAddDigivolutionCards):
//   * [Your Turn][Once Per Turn] "When effects place Digimon cards with the [CS] trait in this Digimon's
//     digivolution cards, gain 1 memory." — AS-IS `new ActivateClass()` with SetHashString("GainMemory_BT22_044") +
//     SetUpActivateClass(..., 1, false, ...) = maxActivationCount 1 (ONCE PER TURN), isOptional FALSE. Non-inherited
//     (top/main effect). This is the "memory latent gap" witness: AS-IS wraps AddMemory in an ActivateClass, so the
//     headless port is a uniform ActivatedEffect + MemoryBody (activated-half) covered by the EventBroadcast bridge —
//     no separate scheduler-half broadcast needed.
//     CanUse (AS-IS :48-51) = CanTriggerOnAddDigivolutionCard(IsThisPermanent, cardEffectSourceCondition:null,
//       IsCsDigimon) — the trigger gate ONLY (no outer battle-area/turn wrapper). AS-IS also hard-requires the add be
//       effect-driven (CardEffect != null); the headless gate's mandatory causeSourceId enforces that, so
//       cardEffectSourceCondition is null (1:1 reduction).
//     CanActivate (AS-IS :53-58) = IsExistOnBattleAreaDigimon && IsOwnerTurn. The AS-IS `CanAddMemory(activateClass)`
//       disjunct is dropped — the headless sink enforces the memory cap (CannotAddMemoryKey), established by
//       BT1_076/BT8_092.
//     Body (AS-IS :70-76) = AddMemory(1).
//
// The AS-IS timing==None (AddSelfDigivolutionRequirementStaticEffect, alt-digivolve) and OnDeclaration (inherited ESS
// [Main] top->bottom + Draw 1) effects are ORTHOGONAL to the OnAddDigivolutionCards reactor under test and are
// deliberately OMITTED (same witness scoping as the other F1 witnesses).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT22.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;

public sealed class BT22_044 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        #region [Your Turn][Once Per Turn] gain 1 memory when a [CS] Digimon is placed under by an effect (OnAddDigivolutionCards)
        if (timing == EffectTiming.OnAddDigivolutionCards)
        {
            const string desc =
                "[Your Turn] [Once Per Turn] When effects place Digimon cards with the [CS] trait in this Digimon's digivolution cards, gain 1 memory.";

            // AS-IS IsThisPermanent (:60-63): IsPermanentExistsOnBattleAreaDigimon && permanent == card.PermanentOfThisCard().
            bool IsThisPermanent(Permanent permanent) =>
                CardEffectCommons.IsPermanentExistsOnBattleAreaDigimon(permanent)
                && permanent.InstanceId == card.PermanentOfThisCard().TopInstanceId;

            // AS-IS IsCsDigimon (:65-68): IsDigimon && HasCSTraits (EqualsTraits("CS")).
            bool IsCsDigimon(CardSource cs) => cs.IsDigimon && cs.EqualsTraits("CS");

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnAddDigivolutionCards,
                canUse: ctx => CardEffectCommons.CanTriggerOnAddDigivolutionCard(ctx, card, IsThisPermanent, null, IsCsDigimon),
                canActivate: () => CardEffectCommons.IsExistOnBattleAreaDigimon(card)
                    && CardEffectCommons.IsOwnerTurn(card),
                body: new MemoryBody(1),
                maxCountPerTurn: 1,       // AS-IS ORDER=1 — [Once Per Turn]
                isOptional: false,
                description: desc,
                capHash: "GainMemory_BT22_044", // AS-IS SetHashString("GainMemory_BT22_044")
                isInheritedEffect: false));      // AS-IS: no SetIsInheritedEffect (top/main effect)
        }
        #endregion

        return cardEffects;
    }
}
