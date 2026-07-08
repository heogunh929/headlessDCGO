// Source: Assets/Scripts/CardEffect/BT3/Blue/BT3_031.cs — a Digimon, mixed timings.
//
// STOP (timing == EffectTiming.None, branch 1): "While this card is in your hand, its digivolution cost is
// -2 when digivolving onto a Paildramon or Dinobeemon." AS-IS: CardEffectFactory.
// ChangeDigivolutionCostStaticEffect(changeValue:-2, permanentCondition = target permanent's TopCard.CardNames
// contains "Paildramon" or "Dinobeemon", cardCondition = cardSource==this && in owner's hand, rootCondition =
// root==Hand, condition = this card is in owner's HandCards, setFixedCost:false). The headless
// ChangeDigivolutionCostStaticEffect(int,bool,CardSource,Func<bool>?) (CardPortingFramework.cs:4653) only
// supports an unconditional self-delta gated by a single global Func<bool> — its own doc states "the
// original's setFixedCost ... and per-target permanent/root conditions are out of this delta primitive's
// scope (per-card follow-up)". This card's condition genuinely depends on WHICH permanent it is digivolving
// ONTO (permanentCondition), but DigivolveAction.TryResolveCost -> ContinuousModifierGate.
// ResolveDigivolutionCost(context, cardId, baseCost) (DigivolveAction.cs:549) never threads the resolved
// targetInstance/targetCard into the read-time condition callback — a card-level `condition: Func<bool>`
// cannot see the digivolve target at all. No per-target-conditioned digivolution-cost primitive exists
// (confirmed by reading DigivolveAction.cs directly, not just grep). Per rule 4 this is a real capability
// gap, engine-file work out of scope for a single-card pass. No cardEffects registered for this branch.
// — 강모델
// (would be: cardEffects.Add(CardEffectFactory.ChangeDigivolutionCostStaticEffect(-2, ..., targetConditioned)))
//
// Inherited continuous (timing == EffectTiming.None, branch 2): this Digimon has <Jamming>. ->
//   CardEffectFactory.JammingSelfStaticEffect, verbatim (1:1 mirror below).
//
// [When Digivolving] Unsuspend all of your Digimon with <Jamming>. AS-IS: ActivateClass on
//   OnEnterFieldAnyone, CanUseCondition = CanTriggerWhenDigivolving(hashtable, card), CanActivateCondition =
//   IsExistOnBattleArea(card) && HasMatchConditionPermanent(own battle-area Digimon with Jamming AND
//   suspended), ORDER=-1 (mandatory, no player choice — a plain foreach over ALL matching), ISOPTIONAL=false,
//   ActivateCoroutine = IUnsuspendPermanents(everyone matching).Unsuspend().
// Headless mirror: uniform ActivatedEffect whose body is ApplyToAllMatchingBody — the no-select "apply a
// mutation to EVERY matching permanent" body (same shape as BT1_110's [Security] suspend-all / BT1_101's
// trash-all), declared under EffectTiming.WhenDigivolving (bridge-resolved digivolve timing, BT1_074/BT1_025
// idiom). Per matching id it stages an Unsuspend mutation directly via the sink (no dedicated
// "UnsuspendPermanent" commons helper exists yet, unlike SuspendPermanent — the mutation is staged inline,
// same EffectMutation/UnsuspendKind shape TriggeredUnsuspendSelfEffect/MemoryCostThenUnsuspendSelfBody use).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT3_031 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.JammingSelfStaticEffect(isInheritedEffect: false, card: card, condition: null));
        }

        if (timing == EffectTiming.WhenDigivolving)
        {
            bool Match(HeadlessEntityId id) =>
                CardEffectCommons.IsOwnerBattleAreaDigimon(card, id)
                && ContinuousKeywordGate.HasKeyword(card.Context, id, ContinuousKeywordGate.Jamming)
                && CardEffectCommons.IsSuspended(card, id);

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.WhenDigivolving,
                canUse: ctx => CardEffectCommons.CanTriggerWhenDigivolving(ctx, card),
                canActivate: () => CardEffectCommons.IsExistOnBattleArea(card) && CardEffectCommons.HasMatchConditionPermanent(card, Match),
                body: new ApplyToAllMatchingBody(
                    match: Match,
                    perTarget: (c, sink, id) => sink.Apply(new EffectMutation(
                        MatchStateMutationSink.UnsuspendKind,
                        c.InstanceId,
                        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = id.Value }))),
                maxCountPerTurn: null,
                isOptional: false,
                description: "[When Digivolving] Unsuspend all of your Digimon with <Jamming>."));
        }

        return cardEffects;
    }
}
