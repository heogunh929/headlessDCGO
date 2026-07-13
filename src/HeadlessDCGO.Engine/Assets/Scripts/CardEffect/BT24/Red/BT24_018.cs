// 1:1 mirror of the original BT24_018 (BT24/Red) — OnLoseSecurity witness for the F1-M1 activated bridge.
//
// Ported effect (AS-IS BT24_018.cs:17-58):
//   * [All Turns] [Once Per Turn] "When your opponent's security stack is removed from, you may delete 1 of
//     their Digimon." — timing OnLoseSecurity -> uniform ActivatedEffect (capHash "BT24_18_AT_Sec_Removed",
//     maxCountPerTurn 1, isOptional TRUE = "you may"). CanUse = IsExistOnBattleAreaDigimon +
//     CanTriggerWhenLoseSecurity(player == OpponentOf(card)) — the PLAYER-SCOPE gate self-scopes on the LOSING
//     player being the OPPONENT (AS-IS `player => player == card.Owner.Enemy`). CanActivate =
//     IsExistOnBattleAreaDigimon + at least one opponent battle-area Digimon exists (AS-IS
//     HasMatchConditionPermanent(IsPermanentExistsOnOpponentBattleAreaDigimon)). Body = ActivatedSelectEffect
//     (Mode.Destroy) over the opponent's battle-area Digimon, maxCount 1, canNoSelect false — AS-IS
//     ActivateCoroutine (BT24_018.cs:29-56) select-destroy, no DP restriction, no cost.
//     OnLoseSecurity is an F1-M1 EventBroadcast bridge timing: headless derives it from the removed security
//     card's CardMoved (from==Security, TriggerTimingMap), threads that as the driving event, and the gate reads
//     the subject's owner = the losing player (ActivatedBridgeTimings.EventBroadcast).
//
// STOP / design item F1-M3-BT24_018-REMOVEFIELD — the AS-IS [All Turns][Once Per Turn] WhenRemoveField prevention
// (AS-IS BT24_018.cs:60+: "when any of your [Reptile]/[Dragonkin] would leave, by deleting 1 of your opponent's
// lowest-DP Digimon, they don't leave") is NOT ported: WhenRemoveField is a Tier-3 self-scoped PRE leave-hook
// (prevention/replacement) that the F-1 bridge does not open until M3 (design roadmap section 5). Deliberately
// omitted here rather than mis-modelled; this witness exercises only the OnLoseSecurity bridge.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT24.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using SelectPermanentEffect = HeadlessDCGO.Engine.Assets.Scripts.Script.SelectPermanentEffect;

public sealed class BT24_018 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        #region All Turns [Once Per Turn] (timing OnLoseSecurity)
        if (timing == EffectTiming.OnLoseSecurity)
        {
            const string description =
                "[All Turns] [Once Per Turn] When your opponent's security stack is removed from, you may delete 1 of their Digimon.";

            // AS-IS CanUseCondition: IsExistOnBattleAreaDigimon && CanTriggerWhenLoseSecurity(player == card.Owner.Enemy).
            bool CanUse(CardEffectResolveContext ctx) =>
                CardEffectCommons.IsExistOnBattleAreaDigimon(card)
                && CardEffectCommons.CanTriggerWhenLoseSecurity(ctx, card, player => player == CardEffectCommons.OpponentOf(card));

            // AS-IS CanActivateCondition: IsExistOnBattleAreaDigimon && HasMatchConditionPermanent(opponent Digimon).
            bool CanActivate() =>
                CardEffectCommons.IsExistOnBattleAreaDigimon(card)
                && CardEffectCommons.MatchConditionPermanentCount(card, id => CardEffectCommons.IsOpponentBattleAreaDigimon(card, id)) > 0;

            var body = new ActivatedSelectEffect(
                card,
                canTarget: id => CardEffectCommons.IsOpponentBattleAreaDigimon(card, id),
                maxCount: 1,
                canNoSelect: false,
                canEndNotMax: false,
                SelectPermanentEffect.Mode.Destroy,
                description);

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnLoseSecurity,
                canUse: CanUse,
                canActivate: CanActivate,
                body: body,
                maxCountPerTurn: 1,
                isOptional: true,
                description: description,
                capHash: "BT24_18_AT_Sec_Removed"));
        }
        #endregion

        return cardEffects;
    }
}
