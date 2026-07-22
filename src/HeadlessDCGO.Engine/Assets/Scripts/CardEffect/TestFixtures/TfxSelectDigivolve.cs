// TEST FIXTURE (not a real card). [Main] (OptionSkill) is the AS-IS DigivolveIntoHandOrTrashCard shape: select
// 1 of the owner's battle-area Digimon and a source Digimon in hand, then digivolve. The cost mode is read from
// the "digCost" metadata flag ("free" or "normal"). Used by tests/PRIM-P0 (Build Order 3 batch C). Inert in play.
// (R7 종점) Re-pointed off the retired invented `SelectAndDigivolveEffect` carrier onto the AS-IS inline
// `new ActivateClass()` idiom (BT1_046 / real digivolve cards): the ActivateCoroutine drives the SAME live
// substrate the carrier composed (DigivolveAction.TryGetEvolutionCost/AttachTargetAsSource, ZoneMover moves,
// MemoryController.Pay, WhenDigivolving emit, CardEffectRegistrar) — resolved by the resolver's
// ActivateICardEffect case, not a bespoke switch arm.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

public sealed class TfxSelectDigivolve : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing != EffectTiming.OptionSkill)
        {
            return effects;
        }

        string costMode = card.Context.CardInstanceRepository.TryGetInstance(card.InstanceId, out CardInstanceRecord? record) &&
                          record is not null && record.Metadata.TryGetValue("digCost", out object? raw) && raw is string s
            ? s
            : "free";

        bool freeCost = costMode != "normal";
        const string description = "Digivolve 1 of your Digimon from your hand.";

        ActivateClass activateClass = new ActivateClass();
        activateClass.SetUpICardEffect(description, _ => true, card);
        activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, description);
        effects.Add(activateClass);
        return effects;

        // AS-IS DigivolveIntoHandOrTrashCard: select target Digimon + source card in hand that can digivolve
        // onto it, pay the (free/normal) cost, place the source onto the target (stack fold + WhenDigivolving).
        async Task ActivateCoroutine(Hashtable _hashtable)
        {
            EngineContext context = card.Context;
            if (context.ZoneMover is not IZoneStateReader zones)
            {
                return;
            }

            var targetCandidates = zones.GetCards(card.Owner, ChoiceZone.BattleArea)
                .Where(id => CardEffectCommons.IsOwnerBattleAreaDigimon(card, id))
                .Where(id => !ContinuousRestrictionGate.EvaluateDigivolve(context, id).IsRestricted)
                .Select(id => new ChoiceCandidate(id, id.Value, ChoiceZone.BattleArea, IsSelectable: true, ownerId: card.Owner))
                .ToList();
            if (targetCandidates.Count == 0)
            {
                return;
            }

            ChoiceResult targetResult = await context.ChoiceProvider.ChooseAsync(
                new ChoiceRequest(ChoiceType.Card, card.Owner, description, minCount: 1, maxCount: 1, canSkip: false, ChoiceZone.BattleArea, targetCandidates),
                CancellationToken.None).ConfigureAwait(false);
            if (targetResult.SelectedIds.Count == 0)
            {
                return;
            }

            HeadlessEntityId targetId = targetResult.SelectedIds[0];

            var sourceCandidates = zones.GetCards(card.Owner, ChoiceZone.Hand)
                .Where(id => DigivolveAction.TryGetEvolutionCost(context, id, targetId, out _, out _))
                .Select(id => new ChoiceCandidate(id, id.Value, ChoiceZone.Hand, IsSelectable: true, ownerId: card.Owner))
                .ToList();
            if (sourceCandidates.Count == 0)
            {
                return;
            }

            ChoiceResult sourceResult = await context.ChoiceProvider.ChooseAsync(
                new ChoiceRequest(ChoiceType.Card, card.Owner, description, minCount: 1, maxCount: 1, canSkip: false, ChoiceZone.Hand, sourceCandidates),
                CancellationToken.None).ConfigureAwait(false);
            if (sourceResult.SelectedIds.Count == 0)
            {
                return;
            }

            HeadlessEntityId sourceId = sourceResult.SelectedIds[0];

            int normalCost = DigivolveAction.TryGetEvolutionCost(context, sourceId, targetId, out int resolved, out _) ? resolved : 0;
            int payCost = freeCost ? 0 : normalCost;

            if (payCost > 0)
            {
                if (!context.MemoryController.CanPay(payCost))
                {
                    return;
                }

                context.MemoryController.Pay(payCost);
            }

            await context.ZoneMover.MoveAsync(
                new ZoneMoveRequest(card.Owner, targetId, ChoiceZone.BattleArea, ChoiceZone.None,
                    Metadata: PermanentBookkeepingStore.ContinuityMoveMetadata), CancellationToken.None).ConfigureAwait(false);
            await context.ZoneMover.MoveAsync(
                new ZoneMoveRequest(card.Owner, sourceId, ChoiceZone.Hand, ChoiceZone.BattleArea,
                    Metadata: PermanentBookkeepingStore.ContinuityMoveMetadata), CancellationToken.None).ConfigureAwait(false);
            DigivolveAction.AttachTargetAsSource(context.CardInstanceRepository, sourceId, targetId);
            TriggerEventEmitter.Emit(context.GameEventQueue, TriggerTimings.WhenDigivolving, actor: card.Owner, subject: sourceId);
            CardEffectRegistrar.RegisterCard(context, sourceId, card.Owner);
        }
    }
}
