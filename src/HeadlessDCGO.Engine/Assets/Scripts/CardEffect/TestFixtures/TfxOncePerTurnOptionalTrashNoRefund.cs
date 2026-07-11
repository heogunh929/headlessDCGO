// TEST FIXTURE (not a real card). The AS-IS DEFAULT counterpart of TfxOncePerTurnOptionalTrash: a CAPPED
// ([Once Per Turn]) uniform activated effect with a SKIPPABLE interactive body and NO RemoveUse — like the
// ~1,170 [Once Per Turn] cards that never refund (witness BT2_078: canNoSelect:true, no RemoveUse — selecting
// nothing still spends the use). Skipping the selection keeps the per-turn use CONSUMED (register-before-body,
// no refund), so the effect cannot re-fire this turn. Inert in actual play.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class TfxOncePerTurnOptionalTrashNoRefund : CEntity_Effect
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
                description: "[Once Per Turn] You may trash up to 1 card in your hand (no refund)."));
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
