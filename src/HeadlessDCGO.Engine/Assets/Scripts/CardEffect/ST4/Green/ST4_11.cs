// Source: Assets/Scripts/CardEffect/ST4/Green/ST4_11.cs
// 1:1 mirror of the original ST4_11:
//   [Your Turn][Once Per Turn] When this Digimon deletes an opponent's Digimon in battle and survives,
//   trash the top card of your opponent's security stack.
//   -> ActivatedEffect(OnEndBattle, canUse = IsExistOnBattleArea + IsOwnerTurn +
//        CanTriggerWhenDeleteOpponentDigimonByBattle(winner = this card's permanent, loser = opponent,
//        isOnlyWinnerSurvive: true), canActivate = IsExistOnBattleArea, body = TrashSecurityBody(opponent, 1,
//        fromTop: true), maxCountPerTurn = 1 [AS-IS ORDER=1], isOptional = false).
// OnEndBattle now threads its battle-result metadata (winnerIds/loserIds) to the CanUse gate — it is an
// EventBroadcastActivatedTimings member (GameFlowProcessor), so the delete-survive gate reads the driving
// event's "event.winnerIds"/"event.loserIds" (was previously a boundary timing that discarded the event).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST4.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST4_11 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEndBattle)
        {
            // AS-IS winnerCondition: permanent.cardSources.Contains(card) — the winning permanent is the one
            // this card belongs to (top or a digivolution source). A winner Permanent's InstanceId is its top
            // card; PermanentOfThisCard().TopInstanceId is the top of the stack this card is part of — equal
            // exactly when this card belongs to that winner (whether it is the top or a buried source).
            bool WinnerCondition(Permanent permanent) =>
                card.PermanentOfThisCard() is { TopInstanceId: var top } && !top.IsEmpty && permanent.InstanceId == top;

            bool LoserCondition(Permanent permanent) => CardEffectCommons.IsOpponentPermanent(permanent, card);

            bool CanUse(CardEffectResolveContext ctx) =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && CardEffectCommons.IsOwnerTurn(card)
                && CardEffectCommons.CanTriggerWhenDeleteOpponentDigimonByBattle(
                    ctx, card, WinnerCondition, LoserCondition, isOnlyWinnerSurvive: true);

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnEndBattle,
                canUse: CanUse,
                canActivate: () => CardEffectCommons.IsExistOnBattleArea(card),
                body: new TrashSecurityBody(CardEffectCommons.OpponentOf(card), 1, fromTop: true),
                maxCountPerTurn: 1,
                isOptional: false,
                description: "[Your Turn][Once Per Turn] When this Digimon deletes an opponent's Digimon in battle and survives, trash the top card of your opponent's security stack."));
        }

        return cardEffects;
    }
}
