namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// (G6-002) Resolves a card's ACTIVATED effects (Option [Main] / Security skills: select-and-delete,
// select-and-buff, player-scope buff) at the ACTION layer, which has the live EngineContext — and thus
// the engine's IChoiceProvider. This is the seam the per-effect IHeadlessCardEffect.ResolveAsync lacks
// (no choice provider in its signature). The action that activates the card (e.g. OptionActivateAction)
// calls this instead of enqueueing onto the generic scheduler.
//
// Choice handling is delegated to context.ChoiceProvider:
//   - in tests / RL drivers it is a ScriptedChoiceProvider that answers immediately;
//   - in a live interactive match it is the DeferredChoiceProvider, whose ChooseAsync suspends via
//     DeferredChoicePendingException — driving that suspend/resume across the action boundary is the
//     remaining loop-integration step (see docs/audit/live_integration_goals.md G6-002).
public static class ActivatedEffectResolver
{
    /// <summary>Resolve all activated effects of <paramref name="cardInstanceId"/> for
    /// <paramref name="timing"/>. Returns the number of activated effects resolved (0 if the card has no
    /// ported activated effect — the caller can then fall back to its legacy path).</summary>
    public static async Task<int> ResolveAsync(
        EngineContext context,
        HeadlessEntityId cardInstanceId,
        HeadlessPlayerId controller,
        EffectTiming timing,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (cardInstanceId.IsEmpty
            || !context.CardInstanceRepository.TryGetInstance(cardInstanceId, out CardInstanceRecord? instance)
            || instance is null
            || !context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? def)
            || def is null
            || !CardEffectDispatch.TryCreateForCard(def, out CEntity_Effect? effect)
            || effect is null)
        {
            return 0;
        }

        var card = new CardSource(context, cardInstanceId, controller, instance.OwnerId);
        IReadOnlyList<HeadlessPlayerId> players = ResolvePlayers(context, controller);
        var sink = new MatchStateMutationSink(
            context.CardInstanceRepository, context.LogSink, context.ZoneMover, context.MemoryController, context.EffectRegistry, context.GameEventQueue);

        // G7-005: participate in the W7 deferred-choice cycle. With an interactive DeferredChoiceProvider,
        // a ChooseAsync below throws DeferredChoicePendingException to SUSPEND — we then do NOT flush the
        // (fresh, unflushed) sink or complete the cycle, so nothing is partially applied; the caller treats
        // it as pending and re-invokes once the agent answers, when BeginResolution replays the answer.
        var coordinator = context.ChoiceProvider as IDeferredChoiceCoordinator;
        coordinator?.BeginResolution();

        int resolved = await ResolveListAsync(
            context, effect, card, players, sink, effect.CardEffects(timing, card), cancellationToken).ConfigureAwait(false);

        await sink.FlushAsync(cancellationToken).ConfigureAwait(false);
        coordinator?.CompleteResolution();
        return resolved;
    }

    private static async Task<int> ResolveListAsync(
        EngineContext context,
        CEntity_Effect effectClass,
        CardSource card,
        IReadOnlyList<HeadlessPlayerId> players,
        MatchStateMutationSink sink,
        IReadOnlyList<ICardEffect> cardEffects,
        CancellationToken cancellationToken)
    {
        int resolved = 0;
        foreach (ICardEffect cardEffect in cardEffects)
        {
            switch (cardEffect)
            {
                case ActivatedSelectEffect select:
                {
                    ChoiceResult result = await context.ChoiceProvider
                        .ChooseAsync(select.BuildRequest(players), cancellationToken).ConfigureAwait(false);
                    if (!result.IsSkipped)
                    {
                        select.Apply(sink, result.SelectedIds);
                    }

                    resolved++;
                    break;
                }

                case ActivatedTargetBuffEffect targetBuff:
                {
                    ChoiceResult result = await context.ChoiceProvider
                        .ChooseAsync(targetBuff.BuildRequest(players), cancellationToken).ConfigureAwait(false);
                    if (!result.IsSkipped)
                    {
                        targetBuff.ApplyBuff(result.SelectedIds);
                    }

                    resolved++;
                    break;
                }

                case ActivatedPlayerScopeBuffEffect playerScopeBuff:
                {
                    playerScopeBuff.ApplyBuff();
                    resolved++;
                    break;
                }

                case ReuseMainOptionEffect:
                {
                    // (G8-004) "[Security] activate this card's [Main] effect" — resolve the card's Main
                    // (OptionSkill) activated effects, recursively, through the same sink / choice provider.
                    resolved += await ResolveListAsync(
                        context, effectClass, card, players, sink,
                        effectClass.CardEffects(EffectTiming.OptionSkill, card), cancellationToken).ConfigureAwait(false);
                    break;
                }

                // DeferredCardEffect / non-activated effects: not resolved here.
            }
        }

        return resolved;
    }

    private static IReadOnlyList<HeadlessPlayerId> ResolvePlayers(EngineContext context, HeadlessPlayerId controller)
    {
        var players = new List<HeadlessPlayerId>();
        void Add(HeadlessPlayerId? candidate)
        {
            if (candidate is HeadlessPlayerId id && !id.IsEmpty && !players.Contains(id))
            {
                players.Add(id);
            }
        }

        Add(controller);
        Add(context.TurnController.Current.TurnPlayerId);
        Add(context.TurnController.Current.NonTurnPlayerId);
        return players;
    }
}
