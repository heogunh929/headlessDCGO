// 1:1 mirror of the original BT9_043 (BT9/Yellow) — an F1-Tier2 OnEndAttack SELF (top-card) witness.
//
// Ported effect (AS-IS BT9_043.cs:84-136, timing OnEndAttack):
//   * [End of Attack][Once Per Turn] "You may add the top card of your security stack to your hand to unsuspend this
//     Digimon." — AS-IS `new ActivateClass()` with SetHashString("Unsuspend_BT9_043") (:89) and
//     SetUpActivateClass(..., 1, true, ...) = maxActivationCount 1 (ONCE PER TURN) + isOptional TRUE ("You may").
//     CanUse (AS-IS :97-100) = CanTriggerOnAttack(hashtable, card) — the self/attacker gate shared by OnEndAttack.
//     CanActivate (AS-IS :102-113) = IsExistOnBattleArea(card) && Owner.SecurityCards.Count >= 1.
//     Body (AS-IS ActivateCoroutine :115-135) = AddHandCards({SecurityCards[0]}) + IReduceSecurity() THEN
//       if (IsExistOnBattleArea) IUnsuspendPermanents(self).Unsuspend() — mirrored by the composite
//       ReturnTopSecurityToHandThenUnsuspendSelfBody (a single ReturnToHand on the top security card reproduces both
//       the OnAddHand of AddHandCards and the OnLoseSecurity of IReduceSecurity, then self-unsuspend).
//
// The AS-IS timing==None self-digivolution-requirement static (:15-23) and the OnEnterFieldAnyone [When Digivolving]
// opponent-DP-down effect (:25-82) are ORTHOGONAL to the OnEndAttack self reactor under test; they are deliberately
// OMITTED (same witness scoping as BT9_021 / the other F1 witnesses — only the timing under test is ported).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT9.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;

public sealed class BT9_043 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        #region [End of Attack] add top security to hand to unsuspend this Digimon (timing OnEndAttack, SELF)
        if (timing == EffectTiming.OnEndAttack)
        {
            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnEndAttack,
                canUse: ctx => CardEffectCommons.CanTriggerOnAttack(ctx, card),
                canActivate: () => CardEffectCommons.IsExistOnBattleArea(card)
                                && CardEffectCommons.SecurityCount(card) >= 1,
                body: new ReturnTopSecurityToHandThenUnsuspendSelfBody(),
                maxCountPerTurn: 1,
                isOptional: true,
                description: "[End of Attack][Once Per Turn] You may add the top card of your security stack to your hand to unsuspend this Digimon.",
                capHash: "Unsuspend_BT9_043")); // AS-IS SetHashString("Unsuspend_BT9_043")
        }
        #endregion

        return cardEffects;
    }
}
