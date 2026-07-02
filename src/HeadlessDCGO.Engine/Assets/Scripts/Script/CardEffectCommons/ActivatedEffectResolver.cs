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
            context.CardInstanceRepository, context.LogSink, context.ZoneMover, context.MemoryController, context.EffectRegistry, context.GameEventQueue,
            // (FR-P3) pass the EngineContext so a deletion/suspend/return honours PLAYER-SCOPE restrictions with
            // an arbitrary permanentCondition ("your <X> Digimon cannot be ..."), not just the card's own self.
            context: context);

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

                case DrawEffect draw:
                {
                    // (BT-PRE-A1) "draw N" — no choice; stage the DrawCards mutation on the shared sink.
                    draw.Apply(sink);
                    resolved++;
                    break;
                }

                case SimplifiedRevealAndSelectEffect reveal:
                {
                    // (BT-PRE-A2) reveal top N + per-condition select + destination routing. Drives the
                    // ChoiceProvider itself (multi-step), staging every move on the shared sink.
                    await reveal.ResolveAsync(sink, cancellationToken).ConfigureAwait(false);
                    resolved++;
                    break;
                }

                case ArtsDigivolveSelfEffect arts:
                {
                    // (W6-A2) Arts Digivolve: cost-free evolution out of the executing area.
                    await arts.ResolveAsync(cancellationToken).ConfigureAwait(false);
                    resolved++;
                    break;
                }

                case RevealMultiSelectEffect revealMulti:
                {
                    // (P4) FULL multi-condition reveal (shared pool, per-pass destination incl. Custom,
                    // opt-out, mutual rule, remaining ordering). ChoiceProvider-driven, sink-staged.
                    await revealMulti.ResolveAsync(sink, cancellationToken).ConfigureAwait(false);
                    resolved++;
                    break;
                }

                case DestroyPermanentsEffect destroy:
                {
                    // (BT-PRE-A3) direct-delete a pre-computed target list — no choice; the sink's centralised
                    // immunity / deletion-prevention gates filter.
                    destroy.Apply(sink);
                    resolved++;
                    break;
                }

                case DeckBottomBounceEffect bounce:
                {
                    // (PRIM-W2) direct-return a pre-computed target list to the deck bottom — no choice.
                    bounce.Apply(sink);
                    resolved++;
                    break;
                }

                case LinkSelfEffect link:
                {
                    // (PRIM-W2) <Link>: choose a host + attach this card as a link card (LinkHelpers).
                    await link.ResolveAsync(cancellationToken).ConfigureAwait(false);
                    resolved++;
                    break;
                }

                case HatchDigiEggEffect hatch:
                {
                    // (BT-PRE-A4) CanHatch-gated digi-egg hatch — no choice; a direct ZoneMover move (no sink
                    // kind for hatch), re-run safe via the empty-breeding-area guard.
                    await hatch.ResolveAsync(cancellationToken).ConfigureAwait(false);
                    resolved++;
                    break;
                }

                case PlayCardEffect playCard:
                {
                    // (BT-PRE-A5) cost-free play of a pre-selected card — no choice; stage the PlayCard mutation.
                    playCard.Apply(sink);
                    resolved++;
                    break;
                }

                case PlayThisCardToBattleEffect playSelf:
                {
                    // (G10-003) A Tamer's [Security] "play this Tamer": play the revealed card onto the
                    // battle area cost-free; the PlayCard mutation auto-registers its effects.
                    playSelf.Apply(sink);
                    resolved++;
                    break;
                }

                case ActivatedPlayFromUnderEffect playFromUnder:
                {
                    // (G10-007) "Choose a Digimon digivolution card under your Digimon and play it as another
                    // Digimon" — select an under-card, then move it onto the battle area cost-free.
                    ChoiceResult result = await context.ChoiceProvider
                        .ChooseAsync(playFromUnder.BuildRequest(players), cancellationToken).ConfigureAwait(false);
                    if (!result.IsSkipped)
                    {
                        playFromUnder.Apply(sink, result.SelectedIds);
                    }

                    resolved++;
                    break;
                }

                case SuspendCostReductionEffect suspendReduce:
                {
                    // (EX8_074 Stage 3 brick) "Suspend N of your Digimon to reduce this card's play cost by M":
                    // select exactly N own Digimon, suspend them, and register the one-shot cost reduction.
                    ChoiceResult result = await context.ChoiceProvider
                        .ChooseAsync(suspendReduce.BuildRequest(players), cancellationToken).ConfigureAwait(false);
                    if (!result.IsSkipped)
                    {
                        suspendReduce.Apply(sink, result.SelectedIds);
                    }

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

                case ReuseWhenDigivolvingEffect:
                {
                    // (EX8-2 brick) "[All Turns] activate this card's [When Digivolving] effects" — resolve the
                    // card's WhenDigivolving activated effects, recursively, through the same sink / choice
                    // provider (same shape as ReuseMainOptionEffect, different timing).
                    resolved += await ResolveListAsync(
                        context, effectClass, card, players, sink,
                        effectClass.CardEffects(EffectTiming.WhenDigivolving, card), cancellationToken).ConfigureAwait(false);
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
