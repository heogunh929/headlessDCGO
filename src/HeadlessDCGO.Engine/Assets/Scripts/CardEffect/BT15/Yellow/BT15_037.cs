// 1:1 mirror of the original BT15_037 (BT15/Yellow) — self-scope OnLoseSecurity witness for the F1-M1 activated
// bridge (the memory/scheduler-vs-activated boundary case).
//
// Ported effects (AS-IS BT15_037.cs):
//   * WhenPermanentWouldBeDeleted -> <Barrier> on self, BOTH the main effect (isInheritedEffect:false, AS-IS :14)
//     and the inherited effect (isInheritedEffect:true, AS-IS :19). CardEffectFactory.BarrierSelfEffect.
//   * [All Turns] [Once Per Turn] "When a card is removed from YOUR security stack, gain 1 memory." — timing
//     OnLoseSecurity -> uniform ActivatedEffect whose BODY is a MemoryBody(+1) (AS-IS ActivateCoroutine :58-61
//     `card.Owner.AddMemory(1)`), maxCountPerTurn 1, isOptional FALSE (mandatory). CanUse = IsExistOnBattleArea +
//     CanTriggerWhenLoseSecurity(player == card.Owner) — the PLAYER-SCOPE gate self-scopes on the LOSING player
//     being the OWNER (AS-IS `player => player == card.Owner`). CanActivate = IsExistOnBattleAreaDigimon.
//     KEY (F1-M1 double-fire boundary): AS-IS represents this gain-memory reactor as `new ActivateClass()` — the
//     uniform ACTIVATED shape — so it is ported as an ActivatedEffect and rides the OnLoseSecurity EventBroadcast
//     ACTIVATED bridge (HasActivatedEffectsAt true). It is NOT also a registered scheduler binding
//     (IHeadlessCardEffect / TriggeredGainMemoryEffect), so it SINGLE-fires — no scheduler-half double-collect.
//
// STOP / design item F1-BT15_037-DISCARDSECURITY — the AS-IS OnDiscardSecurity effect (AS-IS :68-94: "when an
// effect trashes this card from your security stack, you may play this card without paying the cost") is NOT
// ported: it is a self-play-from-security-trash whose primitive is not exposed today (play-this-card-free out of
// the trash). Deliberately omitted rather than mis-modelled; orthogonal to the OnLoseSecurity bridge under test.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT15.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;

public sealed class BT15_037 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        #region Barrier (timing WhenPermanentWouldBeDeleted)
        if (timing == EffectTiming.WhenPermanentWouldBeDeleted)
        {
            cardEffects.Add(CardEffectFactory.BarrierSelfEffect(isInheritedEffect: false, card: card, condition: null));
            cardEffects.Add(CardEffectFactory.BarrierSelfEffect(isInheritedEffect: true, card: card, condition: null));
        }
        #endregion

        #region All Turns [Once Per Turn] (timing OnLoseSecurity)
        if (timing == EffectTiming.OnLoseSecurity)
        {
            const string description =
                "[All Turns][Once per turn] When a card is removed from your security stack, gain 1 memory.";

            // AS-IS CanUseCondition: IsExistOnBattleArea && CanTriggerWhenLoseSecurity(player == card.Owner).
            bool CanUse(CardEffectResolveContext ctx) =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && CardEffectCommons.CanTriggerWhenLoseSecurity(ctx, card, player => player == card.Owner);

            // AS-IS CanActivateCondition: IsExistOnBattleAreaDigimon.
            bool CanActivate() => CardEffectCommons.IsExistOnBattleAreaDigimon(card);

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnLoseSecurity,
                canUse: CanUse,
                canActivate: CanActivate,
                body: new MemoryBody(1),
                maxCountPerTurn: 1,
                isOptional: false,
                description: description));
        }
        #endregion

        return cardEffects;
    }
}
