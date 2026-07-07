// Source: Assets/Scripts/CardEffect/BT1/Green/BT1_077.cs
// 1:1 mirror of the original BT1_077 (BT1/Green):
//   [Your Turn] When this Digimon deletes an opponent's Digimon in battle and survives, gain 1 memory.
//   -> AddMemoryTriggerEffect (OnEndBattle, +1, condition = IsExistOnBattleArea + IsOwnerTurn, triggerGate =
//      CanTriggerWhenDeleteOpponentDigimonByBattle(winner = this card's permanent, loser = opponent,
//      isOnlyWinnerSurvive: true), maxCountPerTurn = null [AS-IS ORDER=-1], isOptional = false).
// Mirrors ST4_11's WinnerCondition/LoserCondition shape (same AS-IS predicate family) and ST3_04/ST3_05's use
// of CardEffectFactory.AddMemoryTriggerEffect for the "gain N memory" body (CanAddMemory gate is enforced by
// the AddMemory mutation sink itself, per that established pattern).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;

public sealed class BT1_077 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
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

            bool Condition() =>
                CardEffectCommons.IsExistOnBattleArea(card) && CardEffectCommons.IsOwnerTurn(card);

            bool TriggerGate(CardEffectResolveContext ctx) =>
                CardEffectCommons.CanTriggerWhenDeleteOpponentDigimonByBattle(
                    ctx, card, WinnerCondition, LoserCondition, isOnlyWinnerSurvive: true);

            cardEffects.Add(CardEffectFactory.AddMemoryTriggerEffect(
                timing: EffectTiming.OnEndBattle,
                amount: 1,
                isInheritedEffect: true,
                card: card,
                condition: Condition,
                description: "[Your Turn] When this Digimon deletes an opponent's Digimon in battle and survives, gain 1 memory.",
                triggerGate: TriggerGate,
                maxCountPerTurn: null,
                isOptional: false));
        }

        return cardEffects;
    }
}
