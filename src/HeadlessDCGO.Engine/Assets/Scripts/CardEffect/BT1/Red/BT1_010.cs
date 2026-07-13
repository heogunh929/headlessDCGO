// 1:1 mirror of the original BT1_010 (BT1/Red) — a Tamer.
//   [On Play] Reveal 5 cards from the top of your deck. Add 1 Tamer card among them to your hand. Place
//             the remaining cards at the bottom of your deck in any order.
//   AS-IS: ActivateClass on EffectTiming.OnEnterFieldAnyone, CanUseCondition = CanTriggerOnPlay,
//   CanActivateCondition = IsExistOnBattleArea(card) && Owner.LibraryCards.Count >= 1, ORDER=-1, ISOPTIONAL=false,
//   ActivateCoroutine = SimplifiedRevealDeckTopCardsAndSelect(revealCount:5, SimplifiedSelectCardConditionClass
//   (IsTamer -> Mode.AddHand, maxCount:1), remainingCardsPlace: DeckBottom).
//   Headless mirror: the uniform ActivatedEffect (= AS-IS ActivateClass) with explicit CanUse/CanActivate gates
//   (CanTriggerOnPlay / IsExistOnBattleArea / LibraryCards >= 1, all faithfully evaluated — not folded away) and
//   a local adapter body wrapping CardEffectCommons.SimplifiedRevealDeckTopCardsAndSelect's underlying
//   SimplifiedRevealAndSelectEffect (AS-IS SimplifiedRevealDeckTopCardsAndSelect coroutine), which itself
//   already no-ops when the library is empty (belt-and-braces with the explicit LibraryCards >= 1 gate).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using System.Threading;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_010 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            const string description =
                "[On Play] Reveal 5 cards from the top of your deck. Add 1 Tamer card among them to your hand. Place the remaining cards at the bottom of your deck in any order.";

            bool CanSelectCardCondition(HeadlessEntityId id)
            {
                var candidate = new CardSource(card.Context, id, card.Owner, card.Owner);
                return candidate.IsTamer;
            }

            var condition = new SimplifiedSelectCardConditionClass(
                canTargetCondition: CanSelectCardCondition,
                message: "Select 1 Tamer card.",
                selectedTo: RevealDestination.Hand,
                maxCount: 1);

            var reveal = new SimplifiedRevealAndSelectEffect(
                card, revealCount: 5, conditions: new[] { condition }, remainingTo: RevealDestination.DeckBottom, description: description);

            // AS-IS CanUseCondition: CanTriggerOnPlay(hashtable, card).
            bool CanUse(CardEffectResolveContext ctx) => CardEffectCommons.CanTriggerOnPlay(ctx, card);

            // AS-IS CanActivateCondition: IsExistOnBattleArea(card) && Owner.LibraryCards.Count >= 1.
            bool CanActivate() =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && ((IZoneStateReader)card.Context.ZoneMover).GetCards(card.Owner, ChoiceZone.Library).Count >= 1;

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnEnterFieldAnyone,
                canUse: CanUse,
                canActivate: CanActivate,
                body: new NonInteractiveAdapterBody(reveal),
                maxCountPerTurn: null,
                isOptional: false,
                description: description));
        }

        return cardEffects;
    }

    // Adapts an IActivatedCardEffect whose ResolveAsync(sink, cancellationToken) shape predates the uniform
    // IEffectBody (B-5) split, so it can be driven as a non-interactive body of a hand-rolled ActivatedEffect
    // that carries EXPLICIT CanUse/CanActivate gates (rather than the folded, gate-less AsUniformActivated
    // wrapping CardEffectFactory.SimplifiedRevealDeckTopCardsAndSelect uses by default).
    private sealed class NonInteractiveAdapterBody : IEffectBody
    {
        private readonly SimplifiedRevealAndSelectEffect _inner;

        public NonInteractiveAdapterBody(SimplifiedRevealAndSelectEffect inner) => _inner = inner;

        public bool IsInteractive => false;

        public ChoiceRequest? BuildRequest(CardSource card, IReadOnlyList<HeadlessPlayerId> players) => null;

        public void Apply(CardSource card, MatchStateMutationSink sink, IReadOnlyList<HeadlessEntityId> selected) =>
            ApplyAsync(card, sink, selected, CancellationToken.None).AsTask().GetAwaiter().GetResult();

        public ValueTask ApplyAsync(
            CardSource card, MatchStateMutationSink sink, IReadOnlyList<HeadlessEntityId> selected, CancellationToken cancellationToken) =>
            new ValueTask(_inner.ResolveAsync(sink, cancellationToken));
    }
}
