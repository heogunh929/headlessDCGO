// 1:1 mirror of the original BT1_088 (BT1/Green) — a Tamer (mixed timings).
//   [Main] If you have a level 5 or higher green Digimon in play, you can suspend this Tamer to reveal the
//          top card of your deck. If that card is a Digimon card, add it to your hand. Otherwise place it at
//          the bottom of your deck.
//     AS-IS: an ActivateClass on EffectTiming.OnDeclaration — CanUseCondition = IsExistOnBattleArea(card)
//     && CanActivateSuspendCostEffect(card) && HasMatchConditionOwnersPermanent(IsDigimon && green &&
//     Level >= 5 && HasLevel); CanActivateCondition = null; ORDER=-1 (uncapped), ISOPTIONAL=false.
//     ActivateCoroutine = SuspendPermanentsClass(self).Tap() THEN RevealDeckTopCardsAndProcessForAll(
//     revealCount:1, Digimon -> Mode.AddHand (maxCount -1), remaining -> DeckBottom).
//     Headless: a uniform ActivatedEffect (declared via the B-2 ActivateMain action) whose canUse mirrors
//     CanUseCondition and whose body suspends self (cost) then drives the same reveal-route primitive the
//     other reveal cards use (SimplifiedRevealAndSelectEffect — ST4_03's exact shape). Formerly STOP for the
//     missing main-phase declaration ACTION; that subsystem exists since B-2 (P1-5).
//   [Security] Play this Tamer.  -> PlaySelfTamerSecurityEffect (security-skill flow, mirrors ST1_12/ST2_12/
//      ST3_12/ST4_14).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_088 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDeclaration)
        {
            // AS-IS CanUseCondition (BT1_088.cs): on the battle area, the suspend cost is payable, and a
            // level 5+ green Digimon (with a printed level) is among the owner's permanents.
            bool CanUseCondition(CardEffectResolveContext _) =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && CardEffectCommons.CanActivateSuspendCostEffect(card)
                && CardEffectCommons.HasMatchConditionOwnersPermanent(card, permanent =>
                    permanent.IsDigimon && permanent.TopCard.HasCardColor("Green")
                    && permanent.Level >= 5 && permanent.TopCard.HasLevel);

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnDeclaration,
                canUse: CanUseCondition,
                canActivate: null,
                body: new SuspendSelfThenRevealRouteBody(),
                maxCountPerTurn: null,
                isOptional: false,
                description: "[Main] If you have a level 5 or higher green Digimon in play, you can suspend this Tamer to reveal the top card of your deck. If that card is a Digimon card, add it to your hand. Otherwise place it at the bottom of your deck."));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlaySelfTamerSecurityEffect(card));
        }

        return cardEffects;
    }

    /// <summary>AS-IS ActivateCoroutine: suspend self (cost) THEN reveal top 1 — Digimon to hand, otherwise
    /// deck bottom. Async-only (the reveal drives the ChoiceProvider / stages on the shared sink itself).</summary>
    private sealed class SuspendSelfThenRevealRouteBody : IEffectBody
    {
        public bool IsInteractive => false;

        public ChoiceRequest? BuildRequest(CardSource card, IReadOnlyList<HeadlessPlayerId> players) => null;

        public void Apply(CardSource card, MatchStateMutationSink sink, IReadOnlyList<HeadlessEntityId> selected) =>
            throw new NotSupportedException("BT1_088 [Main] body must be applied via ApplyAsync (awaited reveal-route).");

        async ValueTask IEffectBody.ApplyAsync(
            CardSource card, MatchStateMutationSink sink, IReadOnlyList<HeadlessEntityId> selected, CancellationToken cancellationToken)
        {
            // Cost: AS-IS SuspendPermanentsClass(card.PermanentOfThisCard()).Tap().
            sink.Apply(new EffectMutation(
                MatchStateMutationSink.SuspendKind, card.InstanceId,
                new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = card.InstanceId.Value }));

            // Effect: AS-IS RevealDeckTopCardsAndProcessForAll(1, Digimon -> AddHand (maxCount -1), rest -> DeckBottom).
            bool IsDigimonCard(HeadlessEntityId id) => new CardSource(card.Context, id, card.Owner, card.Owner).IsDigimon;
            var reveal = new SimplifiedRevealAndSelectEffect(
                card,
                revealCount: 1,
                new[]
                {
                    new SimplifiedSelectCardConditionClass(
                        canTargetCondition: IsDigimonCard,
                        message: "",
                        selectedTo: RevealDestination.Hand,
                        maxCount: -1),
                },
                remainingTo: RevealDestination.DeckBottom,
                description: "Reveal the top card of your deck.");
            await reveal.ResolveAsync(sink, cancellationToken).ConfigureAwait(false);
        }
    }
}
