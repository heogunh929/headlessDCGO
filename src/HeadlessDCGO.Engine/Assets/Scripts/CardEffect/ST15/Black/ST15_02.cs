// 1:1 mirror of the original ST15_02 (ST15/Black) — an F1-Tier2 OnAttackTargetChanged INHERITED (anyone) witness.
//
// Ported effect (AS-IS ST15_02.cs:69-113, timing OnAttackTargetChanged):
//   * [All Turns][Once Per Turn] "When an attack target is switched, gain 1 memory." — AS-IS `new ActivateClass()`
//     with SetIsInheritedEffect(true) + SetHashString("Memory+1_ST15_02") + SetUpActivateClass(..., 1, false, ...) =
//     maxActivationCount 1 (ONCE PER TURN), isOptional FALSE. INHERITED.
//     CanUse (AS-IS :83-87) = IsExistOnBattleArea(card) && CanTriggerOnPermanentAttackTargetSwitch(_ => true) — ANYONE.
//     CanActivate (AS-IS :96-100) = IsExistOnBattleArea(card). The AS-IS `card.Owner.CanAddMemory(activateClass)`
//       disjunct is dropped — the headless sink enforces the memory cap (CannotAddMemoryKey), same as BT22_044/EX6_001.
//     Body (AS-IS :109-111) = card.Owner.AddMemory(1) -> MemoryBody(1).
//
// The AS-IS timing==None (AddSelfDigivolutionRequirementStaticEffect, Koromon) and timing==OnStartMainPhase (Memory +1
// with an IsOwnerTurn gate) effects are ORTHOGONAL to the OnAttackTargetChanged reactor under test and are
// deliberately OMITTED (same witness scoping as the other F1 witnesses).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST15.Black;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;

public sealed class ST15_02 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        #region [All Turns][Once Per Turn] When an attack target is switched, gain 1 memory (OnAttackTargetChanged, INHERITED, anyone)
        if (timing == EffectTiming.OnAttackTargetChanged)
        {
            const string desc = "[All Turns] [Once Per Turn] When an attack target is switched, gain 1 memory.";
            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnAttackTargetChanged,
                canUse: ctx => CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.CanTriggerOnPermanentAttackTargetSwitch(ctx, card, _ => true),  // anyone
                canActivate: () => CardEffectCommons.IsExistOnBattleArea(card),
                body: new MemoryBody(1),
                maxCountPerTurn: 1,       // AS-IS ORDER=1 — [Once Per Turn]
                isOptional: false,
                description: desc,
                capHash: "Memory+1_ST15_02", // AS-IS SetHashString("Memory+1_ST15_02")
                isInheritedEffect: true));    // AS-IS SetIsInheritedEffect(true)
        }
        #endregion

        return cardEffects;
    }
}
