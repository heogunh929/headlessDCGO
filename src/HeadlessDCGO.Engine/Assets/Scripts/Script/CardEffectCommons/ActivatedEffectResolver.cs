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
        CancellationToken cancellationToken = default,
        bool skipReactivationHolder = false,
        GameEvent? drivingEvent = null)
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

        IReadOnlyList<ICardEffect> effects = effect.CardEffects(timing, card);
        if (skipReactivationHolder)
        {
            // The [On Play] play path resolves a card's own OnEnterFieldAnyone [On Play] effects, but the
            // [All Turns] reactivation-holder effect (ReuseWhenDigivolvingEffect) shares that timing and must NOT
            // self-fire — it reacts to OTHER cards' plays (driven by OnPlayReactivation, which excludes the
            // just-played card). Skip it here so playing a holder doesn't wrongly trigger its own reactivation.
            effects = effects.Where(e => e is not ReuseWhenDigivolvingEffect).ToList();
        }

        int resolved = await ResolveListAsync(
            context, effect, card, players, sink, effects, cancellationToken, drivingEvent).ConfigureAwait(false);

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
        CancellationToken cancellationToken,
        GameEvent? drivingEvent = null)
    {
        int resolved = 0;
        foreach (ICardEffect cardEffect in cardEffects)
        {
            switch (cardEffect)
            {
                case ModeChoiceEffect mode:
                {
                    // (PRIM-P0-flow) present the mode menu (available modes only), then dispatch the chosen
                    // branch through this same resolver — sharing the one sink and deferred-choice cycle.
                    IReadOnlyList<ModeChoiceEffect.Mode> available = mode.AvailableModes();
                    if (available.Count > 0)
                    {
                        ChoiceResult result = await context.ChoiceProvider
                            .ChooseAsync(mode.BuildRequest(available), cancellationToken).ConfigureAwait(false);
                        if (!result.IsSkipped && result.SelectedIds.Count > 0)
                        {
                            ICardEffect branch = mode.BranchFor(available, result.SelectedIds[0]);
                            resolved += await ResolveListAsync(
                                context, effectClass, card, players, sink, new[] { branch }, cancellationToken, drivingEvent).ConfigureAwait(false);
                        }
                    }

                    break;
                }

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

                case ActivatedSelectBounceAndDiscardSourcesEffect bounceDiscard:
                {
                    // (ST4_16) select + bounce, where the bounce ALSO discards all of the target's
                    // digivolution cards — AS-IS HandBounceClaass.Bounce() runs DiscardEvoRoots()
                    // unconditionally BEFORE the permanent leaves the field, so the discard is awaited
                    // here first, then the bounce mutation is queued through the SAME sink.
                    ChoiceResult result = await context.ChoiceProvider
                        .ChooseAsync(bounceDiscard.BuildRequest(players), cancellationToken).ConfigureAwait(false);
                    if (!result.IsSkipped)
                    {
                        await bounceDiscard.DiscardSourcesAsync(result.SelectedIds, cancellationToken).ConfigureAwait(false);
                        bounceDiscard.Apply(sink, result.SelectedIds);
                    }

                    resolved++;
                    break;
                }

                case ActivatedSelectFromZoneEffect selectZone:
                {
                    // (PRIM-P0-flow B.O.3) zone-card select-follow-up (add-to-hand / trash-from-zone).
                    ChoiceResult result = await context.ChoiceProvider
                        .ChooseAsync(selectZone.BuildRequest(players), cancellationToken).ConfigureAwait(false);
                    if (!result.IsSkipped)
                    {
                        selectZone.Apply(sink, result.SelectedIds);
                    }

                    resolved++;
                    break;
                }

                case DigiBurstActivatedEffect burst:
                {
                    // (PRIM special-play) AS-IS IDigiBurst.CanDigiBurst: gate on the card's own permanent holding
                    // >= Count TRASHABLE digivolution sources (per-source trash-protection honoured, mirroring
                    // !CanNotTrashFromDigivolutionCards). Pay by trashing Count from the bottom (face-down), then
                    // resolve/register the inner effect through the SAME sink / choice cycle.
                    if (CardEffectCommons.TrashableDigivolutionCount(burst.Card, burst.Card.InstanceId) >= burst.Count)
                    {
                        sink.Apply(new EffectMutation(
                            MatchStateMutationSink.TrashDigivolutionCardsKind, burst.Card.InstanceId,
                            new Dictionary<string, object?>(StringComparer.Ordinal)
                            {
                                [MatchStateMutationSink.CountKey] = burst.Count,
                                [MatchStateMutationSink.FromBottomKey] = true,
                            }));

                        // The Digi-Burst body is either an ACTIVATED effect (draw/delete/trash — resolve it) or a
                        // CONTINUOUS grant (e.g. "your Digimon gain <keyword>" — register it, as at enter-play).
                        if (burst.InnerEffect is IActivatedCardEffect)
                        {
                            resolved += await ResolveListAsync(
                                context, effectClass, burst.Card, players, sink, new[] { burst.InnerEffect }, cancellationToken).ConfigureAwait(false);
                        }
                        else
                        {
                            context.EffectRegistry.Register(burst.InnerEffect.ToBinding(
                                $"{burst.Card.InstanceId.Value}:digiburst:{Guid.NewGuid():N}"));
                        }
                    }

                    resolved++;
                    break;
                }

                case DnaFromHandOrTrashActivatedEffect dna:
                {
                    // (PRIM special-play) AS-IS DNADigivolveWithHandOrTrashCardIntoHandOrTrash: auto-match an
                    // into-card (hand/trash), a battle-area permanent, and a hand/trash material, then fuse the
                    // permanent + material under the into-card (DNA digivolution). Consistent with the other
                    // special plays' auto-match model.
                    var reader = (Headless.Services.IZoneStateReader)context.ZoneMover;
                    HeadlessPlayerId owner = dna.Card.Owner;
                    ChoiceZone intoZone = dna.IntoFromHand ? ChoiceZone.Hand : ChoiceZone.Trash;
                    ChoiceZone materialZone = dna.MaterialFromHand ? ChoiceZone.Hand : ChoiceZone.Trash;

                    HeadlessEntityId? into = FirstMatch(context, reader.GetCards(owner, intoZone), owner, dna.IntoCondition, exclude: default);
                    HeadlessEntityId? permanent = FirstMatch(context, reader.GetCards(owner, ChoiceZone.BattleArea), owner, dna.PermanentCondition, exclude: default);
                    HeadlessEntityId? material = into is HeadlessEntityId intoId
                        ? FirstMatch(context, reader.GetCards(owner, materialZone), owner, dna.MaterialCondition, exclude: intoId)
                        : null;

                    if (into is HeadlessEntityId topId && permanent is HeadlessEntityId permId && material is HeadlessEntityId matId)
                    {
                        IReadOnlyList<HeadlessEntityId> merged = await FusionDigivolveHelpers.FuseAsync(
                            context.CardInstanceRepository, context.ZoneMover, topId, intoZone,
                            new[] { permId, matId }, gameEventQueue: context.GameEventQueue,
                            kind: FusionKind.DnaDigivolve, cancellationToken: cancellationToken).ConfigureAwait(false);
                        if (merged.Count > 0)
                        {
                            resolved++;
                        }
                    }

                    resolved++;
                    break;
                }

                case PlayOptionCardEffect playOption:
                {
                    // (PRIM-P0 B.O.5) select Option card(s) from a zone and play each as a nested effect: trash it
                    // (matching the headless OptionActivate order: trash-before-resolve), open OnUseOption, then
                    // resolve its [Main] (OptionSkill) through the SAME sink / deferred-choice cycle (recursive
                    // ResolveListAsync, NOT a nested ResolveAsync — same reason as ReuseMainOptionEffect).
                    ChoiceResult result = await context.ChoiceProvider
                        .ChooseAsync(playOption.BuildRequest(players), cancellationToken).ConfigureAwait(false);
                    if (!result.IsSkipped)
                    {
                        foreach (HeadlessEntityId optionId in result.SelectedIds)
                        {
                            if (optionId.IsEmpty ||
                                !context.CardInstanceRepository.TryGetInstance(optionId, out CardInstanceRecord? optInstance) || optInstance is null ||
                                !context.CardRepository.TryGetCard(optInstance.DefinitionId, out CardRecord? optDef) || optDef is null ||
                                !CardEffectDispatch.TryCreateForCard(optDef, out CEntity_Effect? optEffect) || optEffect is null)
                            {
                                continue;
                            }

                            await context.ZoneMover.MoveAsync(
                                new ZoneMoveRequest(optInstance.OwnerId, optionId, playOption.SourceZone, ChoiceZone.Trash),
                                cancellationToken).ConfigureAwait(false);
                            TriggerEventEmitter.Emit(context.GameEventQueue, TriggerTimings.OnUseOption, actor: card.Controller, subject: optionId);

                            var optCard = new CardSource(context, optionId, card.Controller, optInstance.OwnerId);
                            resolved += await ResolveListAsync(
                                context, optEffect, optCard, players, sink,
                                optEffect.CardEffects(EffectTiming.OptionSkill, optCard), cancellationToken).ConfigureAwait(false);
                        }
                    }

                    resolved++;
                    break;
                }

                case ActivatedSelectAndPlayEffect selectPlay:
                {
                    // (B.O.3) wire the zone-select play into the activation flow (was previously only driven by
                    // direct BuildRequest/Apply in tests).
                    ChoiceResult result = await context.ChoiceProvider
                        .ChooseAsync(selectPlay.BuildRequest(players), cancellationToken).ConfigureAwait(false);
                    if (!result.IsSkipped)
                    {
                        selectPlay.Apply(sink, result.SelectedIds);
                    }

                    resolved++;
                    break;
                }

                case ActivatedSelectAndPlayFromZonesEffect selectPlayZones:
                {
                    // (BT1_056) multi-zone (Hand ∪ Trash) select-and-play — same choose -> Apply shape as
                    // ActivatedSelectAndPlayEffect, but candidates span several zones (each tagged with its origin).
                    ChoiceResult result = await context.ChoiceProvider
                        .ChooseAsync(selectPlayZones.BuildRequest(players), cancellationToken).ConfigureAwait(false);
                    if (!result.IsSkipped)
                    {
                        selectPlayZones.Apply(sink, result.SelectedIds);
                    }

                    resolved++;
                    break;
                }

                case RevealSelectThenPlaySelectedEffect revealPlay:
                {
                    // (BT1_078 / BT3_063 / BT3_070 / BT3_073) reveal top N -> optional select 1 -> remaining to
                    // deck bottom -> play-as-new-permanent OR free-digivolve onto self (RevealPlayMode). Drives
                    // the ChoiceProvider itself; the follow-up is a direct play/digivolve.
                    await revealPlay.ResolveAsync(cancellationToken).ConfigureAwait(false);
                    resolved++;
                    break;
                }

                case ActivatedDrawThenDiscardEffect drawDiscard:
                {
                    // (G4) draw N -> discard M (atomic; DrawAndDiscardCards flushes the draw before building the
                    // discard pool). Drives the ChoiceProvider itself for the discard select.
                    await drawDiscard.ResolveAsync(cancellationToken).ConfigureAwait(false);
                    resolved++;
                    break;
                }

                case ChooseCountThenTrashDigivolutionEffect chooseCount:
                {
                    // (G12 / BT3_100) choose count 0..N -> trash that many (capped) digivolution cards from every matching target.
                    await chooseCount.ResolveAsync(sink, cancellationToken).ConfigureAwait(false);
                    resolved++;
                    break;
                }

                case OpponentBinaryChoiceEffect oppBinary:
                {
                    // (G13 / BT3_102) opponent yes/no decision -> branch (auto-no when nothing to decide).
                    await oppBinary.ResolveAsync(sink, cancellationToken).ConfigureAwait(false);
                    resolved++;
                    break;
                }

                case SelectDeDigivolveThenConditionalDestroyEffect selDeDig:
                {
                    // (G10 / BT3_107) select 1 -> de-digivolve N (flush) -> destroy if post-state predicate holds.
                    await selDeDig.ResolveAsync(sink, cancellationToken).ConfigureAwait(false);
                    resolved++;
                    break;
                }

                case MassDeDigivolveThenConditionalDestroyEffect massDeDig:
                {
                    // (G10 / BT3_112 WD) de-digivolve all matching (flush) -> destroy each satisfying post-state predicate.
                    await massDeDig.ResolveAsync(sink, cancellationToken).ConfigureAwait(false);
                    resolved++;
                    break;
                }

                case SelectHandAttachToOwnStackThenMemoryEffect attachStack:
                {
                    // (G8 / BT3_019) optional select 1 hand card -> attach on top of this card's own stack ->
                    // gain memory (only if placed). Drives the ChoiceProvider + a direct attach move; the
                    // memory is staged on the sink.
                    await attachStack.ResolveAsync(sink, cancellationToken).ConfigureAwait(false);
                    resolved++;
                    break;
                }

                case SelectDigivolutionSourceToHandThenSelfFollowUpEffect sourceToHand:
                {
                    // (BT1_084 br2 / BT3_112 br2) select 1 source from this card's own stack -> hand -> self
                    // follow-up. Drives the ChoiceProvider + a direct source-return move; the follow-up
                    // (unsuspend via sink / GainCanNotBeBlocked via registry) runs after the return.
                    await sourceToHand.ResolveAsync(sink, cancellationToken).ConfigureAwait(false);
                    resolved++;
                    break;
                }

                case SecuritySelectToHandColorRecoveryShuffleEffect securitySelect:
                {
                    // (BT1_087) select 1 security card -> hand, color-gated Recovery+1, then shuffle security.
                    // Drives the ChoiceProvider; the add-to-hand / recovery / shuffle stage on the sink in order.
                    await securitySelect.ResolveAsync(sink, cancellationToken).ConfigureAwait(false);
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

                case ActivatedSelectTrashDigivolutionEffect trashDigivolution:
                {
                    // (ST2_03/06/09) trash-digivolution select — same BuildRequest -> answer -> Apply shape as the
                    // other selects. Without this case the bridge reaches the effect but the switch silently drops
                    // it, so nothing is trashed in live play (only the unit-test's direct Apply worked).
                    ChoiceResult result = await context.ChoiceProvider
                        .ChooseAsync(trashDigivolution.BuildRequest(players), cancellationToken).ConfigureAwait(false);
                    if (!result.IsSkipped)
                    {
                        trashDigivolution.Apply(sink, result.SelectedIds);
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

                case ActivatedEffect uniform:
                {
                    // Uniform activated effect (mirror of AS-IS ActivateClass): honour the CanUse gate (subject
                    // scope) + CanActivate precondition BEFORE the once-per-turn cap is consumed, then drive the
                    // composable body (interactive choice or direct mutation). Without a driving event the card
                    // being resolved IS the event subject (subject-scoped bridge/onplay/digivolve route by
                    // subject), so TriggerEntityId falls back to the card itself. A BROADCAST bridge timing
                    // (AS-IS StackSkillInfos offers the event to every field card) passes the driving event:
                    // TriggerEntityId is then the event's subject (e.g. the Digimon whose sources were trashed,
                    // not this listener) and the event's primitive metadata is threaded as "event.<key>" values
                    // so gates (CanTriggerOnTrashDigivolutionCard …) read the AS-IS hashtable mirror.
                    HeadlessEntityId triggerId =
                        drivingEvent?.Subject is HeadlessEntityId eventSubject && !eventSubject.IsEmpty
                            ? eventSubject
                            : uniform.Card.InstanceId;
                    Dictionary<string, object?>? eventValues = null;
                    if (drivingEvent is not null)
                    {
                        eventValues = new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            [GameFlowProcessor.EventTypeKey] = drivingEvent.Type.ToString(),
                        };
                        foreach (KeyValuePair<string, object?> pair in drivingEvent.Metadata)
                        {
                            if (pair.Value is string or int or bool or long)
                            {
                                eventValues[$"{GameFlowProcessor.EventValuePrefix}{pair.Key}"] = pair.Value;
                            }
                        }
                    }

                    var subjectCtx = new EffectContext(
                        uniform.Card.Controller, uniform.Card.Owner, uniform.Card.InstanceId,
                        triggerEntityId: triggerId, targetEntityIds: Array.Empty<HeadlessEntityId>(),
                        values: eventValues);
                    var resolveCtx = new CardEffectResolveContext(new EffectRequest(
                        uniform.EffectId, uniform.Card.Controller, EffectTimings.ToTriggerName(uniform.Timing), subjectCtx));
                    if (!uniform.CanResolve(resolveCtx))
                    {
                        break;
                    }

                    if (uniform.MaxCountPerTurn is int cap && !context.OnceFlags.TryActivate(resolveCtx.Request, cap))
                    {
                        break;
                    }

                    await uniform.ResolveBodyAsync(sink, context.ChoiceProvider, players, cancellationToken).ConfigureAwait(false);
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

                case SelectAndDigivolveEffect selectDigivolve:
                {
                    // (PRIM-P0-flow B.O.3) select target + source card (hand/trash) then digivolve, paying cost.
                    await selectDigivolve.ResolveAsync(cancellationToken).ConfigureAwait(false);
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

                case BeforePayCostReductionEffect beforePayReduce:
                {
                    // (PRIM-P0 B.O.4) non-interactive one-shot before-pay reduction of this play's own cost.
                    beforePayReduce.Apply();
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

    /// <summary>(DNA-from-hand/trash) The first card in <paramref name="pool"/> that satisfies
    /// <paramref name="condition"/> (evaluated as a <see cref="CardSource"/>), other than <paramref name="exclude"/>.</summary>
    private static HeadlessEntityId? FirstMatch(
        EngineContext context, IReadOnlyList<HeadlessEntityId> pool, HeadlessPlayerId owner,
        Func<CardSource, bool> condition, HeadlessEntityId exclude)
    {
        foreach (HeadlessEntityId id in pool)
        {
            if (id != exclude && condition(new CardSource(context, id, owner, owner)))
            {
                return id;
            }
        }

        return null;
    }
}
