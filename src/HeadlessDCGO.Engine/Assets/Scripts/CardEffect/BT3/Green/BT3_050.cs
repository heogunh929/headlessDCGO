// Source: Assets/Scripts/CardEffect/BT3/Green/BT3_050.cs — a Digimon.
// 1:1 mirror of the original BT3_050.
//   [Your Turn][Once Per Turn] When this Digimon deletes an opponent's Digimon in battle and survives,
//   gain 1 memory.
//   AS-IS: ActivateClass on EffectTiming.OnEndBattle, CanUseCondition = IsExistOnBattleArea(card) &&
//   IsOwnerTurn(card) && CanTriggerWhenDeleteOpponentDigimonByBattle(winnerCondition: permanent.cardSources.
//   Contains(card) [the winning permanent belongs to THIS card], loserCondition: IsOpponentPermanent,
//   isOnlyWinnerSurvive:true). CanActivateCondition = IsExistOnBattleArea(card). ORDER=1 (maxCountPerTurn:1 +
//   SetHashString -> [Once Per Turn]), ISOPTIONAL=false. ActivateCoroutine: unconditional card.Owner.AddMemory(1).
// Headless mirror: uniform ActivatedEffect + MemoryBody(1) — the exact ST4_11 sibling shape (same winner/
// loser gate), body swapped for the memory gain instead of TrashSecurityBody.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT3_050 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEndBattle)
        {
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
                body: new MemoryBody(1),
                maxCountPerTurn: 1,
                isOptional: false,
                description: "[Your Turn][Once Per Turn] When this Digimon deletes an opponent's Digimon in battle and survives, gain 1 memory."));
        }

        return cardEffects;
    }
}
