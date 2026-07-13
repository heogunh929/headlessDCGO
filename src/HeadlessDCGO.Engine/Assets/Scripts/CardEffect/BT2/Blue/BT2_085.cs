// 1:1 mirror of ST4_14 with BT2_085's timing/trigger substituted.
//   [Your Turn] When one of your opponent's digivolution cards is trashed, you may suspend this
//   Tamer to gain 1 memory.
//     -> ActivatedEffect(OnDigivolutionCardDiscarded, canUse = IsExistOnBattleArea + IsOwnerTurn +
//        CanTriggerOnTrashDigivolutionCard(opponent-Digimon, null, null), canActivate =
//        CanActivateSuspendCostEffect (no IsExistOnBattleArea — AS-IS omits it here),
//        body = SuspendSelfAndGainMemoryBody(1), isOptional = true [may]).
//   [Security] Play this Tamer.  -> PlaySelfTamerSecurityEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;

public sealed class BT2_085 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDigivolutionCardDiscarded)
        {
            bool PermanentCondition(Permanent permanent) =>
                CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);

            bool CanUse(CardEffectResolveContext ctx) =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && CardEffectCommons.IsOwnerTurn(card)
                && CardEffectCommons.CanTriggerOnTrashDigivolutionCard(ctx, card, PermanentCondition, null, null);

            bool CanActivate() =>
                CardEffectCommons.CanActivateSuspendCostEffect(card);

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnDigivolutionCardDiscarded,
                canUse: CanUse,
                canActivate: CanActivate,
                body: new SuspendSelfAndGainMemoryBody(1),
                maxCountPerTurn: null,
                isOptional: true,
                description: "[Your Turn] When one of your opponent's digivolution cards is trashed, you may suspend this Tamer to gain 1 memory."));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlaySelfTamerSecurityEffect(card));
        }

        return cardEffects;
    }
}