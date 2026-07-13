// 1:1 mirror of the original ST4_14 (ST4/Green) — a Tamer.
//   [Your Turn] When one of your opponent's Digimon becomes suspended, you may suspend this Tamer to
//   gain 1 memory.
//     -> ActivatedEffect(OnTappedAnyone, canUse = IsExistOnBattleArea + IsOwnerTurn +
//        CanTriggerWhenPermanentSuspends(opponent-Digimon), canActivate = IsExistOnBattleArea +
//        CanActivateSuspendCostEffect, body = SuspendSelfAndGainMemoryBody(1), isOptional = true [may]).
//        OnTappedAnyone is now an EventBroadcastActivatedTimings member (GameFlowProcessor), so the suspend
//        event reaches this Tamer (a different card than the suspended subject); the gate reads the event's
//        subject (the tapped opponent Digimon) via CanTriggerWhenPermanentSuspends. The self-suspend cost +
//        memory gain compose in SuspendSelfAndGainMemoryBody.
//   [Security] Play this Tamer.  -> PlaySelfTamerSecurityEffect (security-skill flow, mirrors ST1_12/ST2_12/ST3_12)

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST4.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;

public sealed class ST4_14 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnTappedAnyone)
        {
            bool PermanentCondition(Permanent permanent) =>
                CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);

            bool CanUse(CardEffectResolveContext ctx) =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && CardEffectCommons.IsOwnerTurn(card)
                && CardEffectCommons.CanTriggerWhenPermanentSuspends(ctx, card, PermanentCondition);

            bool CanActivate() =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && CardEffectCommons.CanActivateSuspendCostEffect(card);

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnTappedAnyone,
                canUse: CanUse,
                canActivate: CanActivate,
                body: new SuspendSelfAndGainMemoryBody(1),
                maxCountPerTurn: null,
                isOptional: true,
                description: "[Your Turn] When one of your opponent's Digimon becomes suspended, you may suspend this Tamer to gain 1 memory."));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlaySelfTamerSecurityEffect(card));
        }

        return cardEffects;
    }
}
