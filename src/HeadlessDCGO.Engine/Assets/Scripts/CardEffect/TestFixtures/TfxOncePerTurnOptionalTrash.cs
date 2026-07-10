// TEST FIXTURE (not a real card). A CAPPED ([Once Per Turn]) UNIFORM activated effect whose interactive body is
// SKIPPABLE ("you MAY trash up to 1 hand card"). Exercises B-4 (P1-7): when the agent SKIPS the selection the body
// does nothing (ResolveBodyAsync returns executed=false), so the resolver REFUNDS the per-turn use instead of
// consuming it — AS-IS `if (!executed) activateClass.RemoveUse()`. Inert in actual play.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class TfxOncePerTurnOptionalTrash : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            effects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnEnterFieldAnyone,
                canUse: null,
                canActivate: null,
                body: new OptionalTrashBody(),
                maxCountPerTurn: 1,
                isOptional: false,
                description: "[Once Per Turn] You may trash up to 1 card in your hand."));
        }

        return effects;
    }

    private sealed class OptionalTrashBody : IEffectBody
    {
        public bool IsInteractive => true;

        public ChoiceRequest? BuildRequest(CardSource card, IReadOnlyList<HeadlessPlayerId> players)
        {
            var reader = (IZoneStateReader)card.Context.ZoneMover;
            var candidates = reader.GetCards(card.Owner, ChoiceZone.Hand)
                .Select(id => EffectChoiceHelpers.Candidate(id, id.Value, ChoiceZone.Hand, isSelectable: true, card.Owner))
                .ToList();
            if (candidates.Count == 0)
            {
                return null;
            }

            // canSkip: true, minCount: 0 → "up to 1", skippable — a Skip answer routes to the B-4 refund.
            return EffectChoiceHelpers.CreatePermanentRequest(
                card.Owner, "you may trash up to 1 hand card", minCount: 0, maxCount: 1, canSkip: true, candidates);
        }

        public void Apply(CardSource card, MatchStateMutationSink sink, IReadOnlyList<HeadlessEntityId> selected)
        {
            foreach (HeadlessEntityId id in selected)
            {
                if (id.IsEmpty)
                {
                    continue;
                }

                sink.Apply(new EffectMutation(
                    MatchStateMutationSink.TrashCardKind, card.InstanceId,
                    new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = id.Value }));
            }
        }
    }
}
