// Source: Assets/Scripts/CardEffect/BT3/Green/BT3_094.cs — a Tamer (three timing blocks).
// 1:1 mirror of the original BT3_094.
//   [Start of Your Turn] If you have 2 or less memory, set your memory to 3. AS-IS:
//   CardEffectFactory.SetMemoryTo3TamerEffect(card) on OnStartTurn — verbatim factory match (BT1_086 sibling).
//
//   [Your Turn] When one of your green or blue Digimon deletes an opponent's Digimon in battle and survives,
//   you may suspend this Tamer to gain 1 memory.
//   AS-IS: ActivateClass on EffectTiming.OnEndBattle, CanUseCondition = IsExistOnBattleArea(card) &&
//   IsOwnerTurn(card) && CanTriggerWhenDeleteOpponentDigimonByBattle(winnerCondition: IsOwnerPermanent(permanent,
//   card) [ANY of the owner's permanents, not just this Tamer], loserCondition: IsOpponentPermanent,
//   isOnlyWinnerSurvive:true, winnerRealCondition: permanent.TopCard.CardColors contains Green or Blue).
//   CanActivateCondition = IsExistOnBattleArea(card) && CanActivateSuspendCostEffect(card). ORDER=-1
//   (maxCountPerTurn:null — no [Once Per Turn] cap here, unlike the BT3_050/BT3_111 Digimon siblings),
//   ISOPTIONAL=true ("you may"). ActivateCoroutine: suspend this Tamer's own permanent (cost), THEN
//   card.Owner.AddMemory(1) (effect).
//
//   [Security] Play this Tamer. AS-IS: CardEffectFactory.PlaySelfTamerSecurityEffect(card) on SecuritySkill —
//   verbatim factory match (BT1_086/ST1_12/ST2_12/ST3_12/ST4_14 pattern).
// Headless mirror: uniform ActivatedEffect + SuspendSelfAndGainMemoryBody(1) for the OnEndBattle branch — the
// suspend-self cost + memory-gain effect shape (isOptional:true mirrors AS-IS "you may").

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT3_094 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnStartTurn)
        {
            cardEffects.Add(CardEffectFactory.SetMemoryTo3TamerEffect(card));
        }

        if (timing == EffectTiming.OnEndBattle)
        {
            bool WinnerCondition(Permanent permanent) => CardEffectCommons.IsOwnerPermanent(permanent, card);

            bool WinnerRealCondition(Permanent permanent) =>
                permanent.TopCard.HasCardColor("Green") || permanent.TopCard.HasCardColor("Blue");

            bool LoserCondition(Permanent permanent) => CardEffectCommons.IsOpponentPermanent(permanent, card);

            bool CanUse(CardEffectResolveContext ctx) =>
                CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.IsOwnerTurn(card)
                    && CardEffectCommons.CanTriggerWhenDeleteOpponentDigimonByBattle(
                        ctx, card, WinnerCondition, LoserCondition, isOnlyWinnerSurvive: true,
                        winnerRealCondition: WinnerRealCondition);

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnEndBattle,
                canUse: CanUse,
                canActivate: () => CardEffectCommons.IsExistOnBattleArea(card) && CardEffectCommons.CanActivateSuspendCostEffect(card),
                body: new SuspendSelfAndGainMemoryBody(1),
                maxCountPerTurn: null,
                isOptional: true,
                description: "[Your Turn] When one of your green or blue Digimon deletes an opponent's Digimon in battle and survives, you may suspend this Tamer to gain 1 memory."));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlaySelfTamerSecurityEffect(card));
        }

        return cardEffects;
    }
}
