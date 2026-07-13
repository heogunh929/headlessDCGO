// 1:1 mirror of the original EX10_002 (EX10/Black) — an F1-Tier2 OnAttackTargetChanged INHERITED (anyone) witness
// (the card's ONLY effect).
//
// Ported effect (AS-IS EX10_002.cs:14-43, timing OnAttackTargetChanged):
//   * [All Turns][Once Per Turn] "When attack targets change, <Draw 1>." — AS-IS `new ActivateClass()` with
//     SetIsInheritedEffect(true) + SetHashString("ESS_EX10-002") + SetUpActivateClass(..., 1, false, ...) =
//     maxActivationCount 1 (ONCE PER TURN), isOptional FALSE. INHERITED (digivolution-source) — ScanZones collects it
//     from under the attacker. The OnAttackTargetChanged analogue of BT22_003/EX6_001.
//     CanUse (AS-IS :28-31) = IsExistOnBattleAreaDigimon(card) && CanTriggerOnPermanentAttackTargetSwitch(_ => true).
//       ANYONE scope: permanentCondition = `_ => true`, so it reacts to ANY attacker's target switch (self-attacker or
//       not) — NOT the self gate CanTriggerOnAttackTargetSwitch. This is why the timing must be EventBroadcast.
//     CanActivate (AS-IS :34-36) = IsExistOnBattleAreaDigimon(card). No IsOwnerTurn (All Turns).
//     Body (AS-IS :39-41) = DrawClass(card.Owner, 1) -> DrawBody(1).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX10.Black;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;

public sealed class EX10_002 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        #region [All Turns][Once Per Turn] When attack targets change, Draw 1 (OnAttackTargetChanged, INHERITED, anyone)
        if (timing == EffectTiming.OnAttackTargetChanged)
        {
            const string desc = "[All Turns] [Once Per Turn] When attack targets change, <Draw 1>.";
            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnAttackTargetChanged,
                canUse: ctx => CardEffectCommons.IsExistOnBattleAreaDigimon(card)
                    && CardEffectCommons.CanTriggerOnPermanentAttackTargetSwitch(ctx, card, _ => true),  // anyone
                canActivate: () => CardEffectCommons.IsExistOnBattleAreaDigimon(card),
                body: new DrawBody(1),
                maxCountPerTurn: 1,       // AS-IS ORDER=1 — [Once Per Turn]
                isOptional: false,
                description: desc,
                capHash: "ESS_EX10-002",  // AS-IS SetHashString("ESS_EX10-002")
                isInheritedEffect: true));  // AS-IS SetIsInheritedEffect(true)
        }
        #endregion

        return cardEffects;
    }
}
