// TEST FIXTURE (not a real card). TWO uniform activated effects at the SAME timing: [0] a CAPPED non-interactive
// memory gain (+2 — an IMMEDIATELY-applied sink mutation) and [1] a CAPPED interactive hand-trash. Exercises the
// multi-effect suspend/replay of the uniform resolution cycle (adversarial-review P0-3): effect [0] completes
// (memory applied immediately + its use consumed/staged), effect [1] suspends for the agent's selection — the
// resumed re-invocation must (a) NOT double-apply [0]'s memory (mutation replay journal skips the purely-immediate
// call), (b) NOT double-consume or cap-out [0]'s staged use against its own replay (OnceFlags uniform-cycle
// replay cursor), and (c) complete [1] normally. Inert in actual play.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class TfxCappedMemoryThenSelectTrash : CEntity_Effect
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
                body: new MemoryBody(2),
                maxCountPerTurn: 1,
                isOptional: false,
                description: "[Once Per Turn] Gain 2 memory.",
                // Distinct capHash per effect — this fixture models a SetHashString'd card (ST16_11 pattern):
                // its two [Once Per Turn] effects count separately. Unhashed same-card caps share one count.
                capHash: "mem"));
            effects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnEnterFieldAnyone,
                canUse: null,
                canActivate: null,
                body: new SelectTrashBody(),
                maxCountPerTurn: 1,
                isOptional: false,
                description: "[Once Per Turn] Trash 1 card in your hand.",
                capHash: "trash"));
        }

        return effects;
    }

    private sealed class SelectTrashBody : IEffectBody
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

            return EffectChoiceHelpers.CreatePermanentRequest(
                card.Owner, "trash 1 hand card", minCount: 1, maxCount: 1, canSkip: false, candidates);
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
