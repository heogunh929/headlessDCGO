namespace HeadlessDCGO.Engine.Headless.Runtime;

using System.Diagnostics.CodeAnalysis;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

public sealed class DigivolveAction
{
    private const string SourceIdsMetadataKey = "sourceIds";

    public IReadOnlyList<LegalAction> GetLegalActions(
        EngineContext context,
        HeadlessPlayerId playerId)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (playerId.IsEmpty || context.ZoneMover is not IZoneStateReader zoneReader)
        {
            return Array.Empty<LegalAction>();
        }

        HeadlessEntityId[] handCards = zoneReader.GetCards(playerId, ChoiceZone.Hand).ToArray();
        // GR-004: a card can digivolve onto a Digimon in the BATTLE area OR a permanent in the BREEDING area
        // (the digi-egg ramp: hatch a Lv.2 egg, then digivolve it into a Lv.3 in breeding, then move it out).
        // Mirrors AS-IS — the breeding digi-egg frame is a valid digivolution target (CardController.cs).
        HeadlessEntityId[] targetCards = zoneReader.GetCards(playerId, ChoiceZone.BattleArea)
            .Concat(zoneReader.GetCards(playerId, ChoiceZone.BreedingArea))
            .ToArray();
        List<LegalAction> actions = new();

        foreach (HeadlessEntityId cardId in handCards)
        {
            foreach (HeadlessEntityId targetCardId in targetCards)
            {
                if (!TryGetEvolutionCost(context, cardId, targetCardId, out int evolutionCost, out _))
                {
                    continue;
                }

                DigivolveActionPayload payload = new(cardId, targetCardId, evolutionCost);
                DigivolveValidation validation = Validate(context, playerId, payload);
                if (validation.IsLegal)
                {
                    actions.Add(HeadlessActionFactory.Digivolve(playerId, cardId, targetCardId, evolutionCost));
                }
            }
        }

        // (W6-F) App Fusion (AS-IS CardController.cs:400 — an EVOLUTION variant): a hand card declaring an
        // AppFusionCondition may fuse onto an owner Digimon whose top matches one material and one of whose
        // LINK cards matches a different material; the chosen link card is consumed into the fused sources.
        foreach (HeadlessEntityId cardId in handCards)
        {
            var view = new Assets.Scripts.Script.CardEffectCommons.CardSource(context, cardId, playerId, playerId);
            if (view.AppFusionConditionOf() is not Assets.Scripts.Script.CardEffectCommons.AppFusionCondition condition)
            {
                continue;
            }

            int fusionCost = Math.Max(0, ContinuousModifierGate.ResolvePlayCost(context, cardId, condition.cost));
            foreach (HeadlessEntityId hostId in zoneReader.GetCards(playerId, ChoiceZone.BattleArea))
            {
                var host = new Assets.Scripts.Script.CardEffectCommons.Permanent(context, hostId, playerId);
                if (!condition.digimonCondition(host))
                {
                    continue;
                }

                foreach (HeadlessEntityId linkId in LinkedIdsOf(context, hostId))
                {
                    var linkView = new Assets.Scripts.Script.CardEffectCommons.CardSource(context, linkId, playerId, playerId);
                    if (!condition.linkedCondition(host, linkView))
                    {
                        continue;
                    }

                    DigivolveActionPayload payload = new(cardId, hostId, fusionCost) { AppFusionLinkCardId = linkId };
                    if (Validate(context, playerId, payload).IsLegal)
                    {
                        actions.Add(HeadlessActionFactory.Create(
                            HeadlessActionTypes.Digivolve, playerId,
                            $"{playerId.Value}:{HeadlessActionTypes.Digivolve}:appfusion:{cardId.Value}:{hostId.Value}:{linkId.Value}",
                            payload.ToParameters()));
                    }
                }
            }
        }

        return actions
            .OrderBy(action => action.Id.Value, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AppendSourceId(ICardInstanceRepository repository, HeadlessEntityId hostId, HeadlessEntityId sourceId)
    {
        if (!repository.TryGetInstance(hostId, out CardInstanceRecord? host) || host is null)
        {
            return;
        }

        List<string> sources = host.Metadata.TryGetValue(SourceIdsMetadataKey, out object? raw) && raw is IEnumerable<string> existing
            ? existing.ToList()
            : new List<string>();
        sources.Add(sourceId.Value);
        repository.Upsert(host with
        {
            Metadata = new Dictionary<string, object?>(host.Metadata, StringComparer.Ordinal) { [SourceIdsMetadataKey] = sources.ToArray() }
        });
    }

    private static IReadOnlyList<HeadlessEntityId> LinkedIdsOf(EngineContext context, HeadlessEntityId hostId) =>
        context.CardInstanceRepository.TryGetInstance(hostId, out CardInstanceRecord? host) && host is not null
            ? LinkHelpers.ReadLinkedCardIds(host.Metadata)
            : Array.Empty<HeadlessEntityId>();

    public async Task<ActionProcessResult> ProcessAsync(
        LegalAction action,
        EngineContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (!DigivolveActionPayload.TryRead(action, out DigivolveActionPayload? payload, out string? error))
        {
            return ActionProcessResult.Failure(error ?? "Invalid Digivolve payload.", BaseMetadata(action));
        }

        DigivolveValidation validation = Validate(context, action.PlayerId, payload);
        if (!validation.IsLegal)
        {
            return ActionProcessResult.Illegal(action, validation.Reason, Metadata(action, payload, validation));
        }

        HeadlessMemoryState previousMemory = context.MemoryController.Current;

        // (W6-F) App Fusion: convert the chosen LINK card into a digivolution source of the HOST before the
        // normal stacking (AS-IS selectAppFusionEffect.AddToSources at CardController.cs:786) — the fused
        // card then stacks the host + its (now link-including) sources as usual.
        if (!payload.AppFusionLinkCardId.IsEmpty)
        {
            await LinkHelpers.RemoveLinkCardAsync(
                context.CardInstanceRepository, context.ZoneMover,
                payload.TargetCardId, payload.AppFusionLinkCardId, trash: false,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            AppendSourceId(context.CardInstanceRepository, payload.TargetCardId, payload.AppFusionLinkCardId);
        }

        // GR-004: digivolution is IN PLACE — the new top card lands in the SAME area the target occupied
        // (breeding stays in breeding, battle stays in battle); only hardcoding BattleArea would have
        // teleported an in-breeding digivolution onto the battle area.
        ChoiceZone targetZone =
            context.ZoneMover is IZoneStateReader reader
            && reader.GetCards(action.PlayerId, ChoiceZone.BreedingArea).Contains(payload.TargetCardId)
                ? ChoiceZone.BreedingArea
                : ChoiceZone.BattleArea;

        // (C1d RDW-04) capture the PRE-digivolve target level (AS-IS OnEnterField "oldLevels" is read before
        // RemoveFromAllArea, CardController.cs:1367) so the entry CardMoved can carry the AS-IS
        // OnEnterFieldHashtable params for the DORMANT SkillWindowSupply. Read BEFORE the target leaves the field.
        int preDigivolveTargetLevel =
            new Assets.Scripts.Script.CardEffectCommons.CardSource(context, payload.TargetCardId, action.PlayerId, action.PlayerId).Level;
        ZoneMoveResult targetRemoval = await context.ZoneMover.MoveAsync(
            new ZoneMoveRequest(
                action.PlayerId,
                payload.TargetCardId,
                targetZone,
                ChoiceZone.None),
            cancellationToken).ConfigureAwait(false);
        // (C1d RDW-04) enrich the digivolve entry with the AS-IS OnEnterFieldHashtable params (isEvolution=true;
        // evoRoots == evoRootTops == the pre-digivolve top = the target; oldLevels = [pre-digivolve level]; root
        // None; from-digimon-digivolution-cards false for a HAND digivolve; cardEffect null for a player-initiated
        // digivolve). Additive — no live consumer reads these before C2.
        var digivolveEnterFieldMetadata = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [SkillWindowSupply.OnEnterFieldIsEvolutionKey] = true,
            [SkillWindowSupply.OnEnterFieldIsJogressKey] = false,
            [SkillWindowSupply.OnEnterFieldDigiXrosCountKey] = 0,
            [SkillWindowSupply.OnEnterFieldAssemblyCountKey] = 0,
            [SkillWindowSupply.OnEnterFieldEvoRootIdsKey] = new[] { payload.TargetCardId.Value },
            [SkillWindowSupply.OnEnterFieldOldLevelsKey] = new[] { preDigivolveTargetLevel },
            [SkillWindowSupply.OnEnterFieldIsFromDigimonDigivolutionCardsKey] = false,
        };
        ZoneMoveResult cardMovement = await context.ZoneMover.MoveAsync(
            new ZoneMoveRequest(
                action.PlayerId,
                payload.CardId,
                ChoiceZone.Hand,
                targetZone,
                Metadata: digivolveEnterFieldMetadata),
            cancellationToken).ConfigureAwait(false);
        // F-6.7: wrap the digivolve-cost payment with the Before/AfterPayCost windows.
        TriggerEventEmitter.Emit(context.GameEventQueue, TriggerTimings.BeforePayCost, actor: action.PlayerId, subject: payload.CardId,
            extraMetadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["isEvolution"] = true, ["targetCardId"] = payload.TargetCardId.Value });

        // (PRIM-P0 B.O.4 #1) resolve [BeforePayCost] one-shot digivolve-cost reductions, then re-resolve the
        // cost. CurrentPayCostRoot = Digivolve gates effects so PLAY-intended before-pay effects do NOT fire here.
        // Interactive (deferred) before-pay on digivolve is v1-unsupported -> pay the original cost.
        int memoryCost = payload.MemoryCost;
        context.CurrentPayCostRoot = PayCostRoot.Digivolve;
        try
        {
            int beforePayResolved = await ActivatedEffectResolver
                .ResolveAsync(context, payload.CardId, action.PlayerId, EffectTiming.BeforePayCost, cancellationToken)
                .ConfigureAwait(false);
            if (beforePayResolved > 0 && TryGetEvolutionCost(context, payload.CardId, payload.TargetCardId, out int reResolvedCost, out _))
            {
                memoryCost = reResolvedCost;
            }
        }
        catch (DeferredChoicePendingException)
        {
            memoryCost = payload.MemoryCost;
        }
        finally
        {
            context.CurrentPayCostRoot = PayCostRoot.None;
        }

        HeadlessMemoryState paidMemory = context.MemoryController.Pay(memoryCost);
        TriggerEventEmitter.Emit(context.GameEventQueue, TriggerTimings.AfterPayCost, actor: action.PlayerId, subject: payload.CardId);
        // F-1.7: fixed cost locked — expire one-shot "until cost is calculated" modifiers.
        EffectDurationExpiry.ExpireFixedCostCalc(context.EffectRegistry);
        IReadOnlyList<HeadlessEntityId> sourceIds = AttachTargetAsSource(
            context.CardInstanceRepository,
            payload.CardId,
            payload.TargetCardId);

        // B1b: project the freshly attached sourceIds storage into the typed stack read-model and
        // carry its typed depth forward (instead of trusting a raw count). The reader also asserts the
        // stack is well-formed (DigiEgg..Top ordering), so a malformed attach surfaces here.
        DigivolutionStack stack = DigivolutionStackReader.Read(
            context.CardInstanceRepository,
            context.CardRepository,
            payload.CardId);

        Dictionary<string, object?> metadata = Metadata(action, payload, validation);
        metadata[HeadlessActionParameterKeys.PreviousMemory] = previousMemory.Current;
        metadata[HeadlessActionParameterKeys.Memory] = paidMemory.Current;
        metadata["removedTargetEventSequence"] = targetRemoval.Event.Sequence;
        metadata["movedDigivolveCardEventSequence"] = cardMovement.Event.Sequence;
        metadata[SourceIdsMetadataKey] = sourceIds.Select(id => id.Value).ToArray();
        metadata["stackDepth"] = stack.Depth;
        metadata["stackBaseDp"] = stack.BaseDp;

        // (RD-1 / AS-IS CardController.cs:1526-1529) the digivolve is physically done — bump the per-turn
        // digivolve counter and draw one card BEFORE the digivolve windows are opened (AS-IS: the isEvolution
        // draw at :1526 precedes the OnEnterField/WhenDigivolving window stack at :1691, so the OnDraw window
        // queues ahead of the digivolve windows).
        await DigivolveCommons.OnDigivolveCompletedAsync(context, action.PlayerId, cancellationToken).ConfigureAwait(false);

        // W1: open the WhenDigivolving timing window for the card that just digivolved.
        // (W6 tail) oldLevel = the PRE-digivolve top's level (AS-IS OnEnterField "oldLevels").
        TriggerEventEmitter.Emit(
            context.GameEventQueue,
            TriggerTimings.WhenDigivolving,
            actor: action.PlayerId,
            subject: payload.CardId,
            extraMetadata: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["oldLevel"] = new Assets.Scripts.Script.CardEffectCommons.CardSource(context, payload.TargetCardId, action.PlayerId, action.PlayerId).Level,
                // (C1d RDW-04) same OnEnterFieldHashtable params as the entry CardMoved, so the DORMANT
                // SkillWindowSupply can byte-rebuild the WhenDigivolving payload too (uses the PRE-digivolve level).
                [SkillWindowSupply.OnEnterFieldIsEvolutionKey] = true,
                [SkillWindowSupply.OnEnterFieldIsJogressKey] = false,
                [SkillWindowSupply.OnEnterFieldDigiXrosCountKey] = 0,
                [SkillWindowSupply.OnEnterFieldAssemblyCountKey] = 0,
                [SkillWindowSupply.OnEnterFieldEvoRootIdsKey] = new[] { payload.TargetCardId.Value },
                [SkillWindowSupply.OnEnterFieldOldLevelsKey] = new[] { preDigivolveTargetLevel },
                [SkillWindowSupply.OnEnterFieldIsFromDigimonDigivolutionCardsKey] = false,
            });

        // (F1-Tier2 OnAddDigivolutionCards fidelity) NATURAL digivolution does NOT emit OnAddDigivolutionCards — AS-IS
        // reserves that timing for EFFECT-driven place-under (Permanent.AddDigivolutionCardsTop/Bottom, gate hard-
        // requires CardEffect != null). AS-IS natural digivolve stacks the previous top via a plain AddCardSource
        // (CardController.cs:1365-1375) with NO StackSkillInfos. The prior emit here fired on EVERY digivolve (over-
        // fire); the effect place-under emit now lives in DigivolutionStackHelpers (the AS-IS AddDigivolutionCards*
        // port). See ActivatedBridgeTimings OnAddDigivolutionCards note.

        // G6-001: the digivolving card is the new top entering play — auto-register its ported effects.
        // The previous top (now a digivolution source) keeps its bindings: its inherited effect now folds
        // into this new top, while its main effect is inert (it is no longer an evaluated permanent).
        CardEffectRegistrar.RegisterCard(context, payload.CardId, action.PlayerId);

        // LA-1: resolve the digivolved card's [When Digivolving] activated effects through the choice flow
        // (ActivatedEffectResolver has the full EngineContext / ChoiceProvider). No-op for cards without a
        // ported WhenDigivolving activated effect. The interactive deferred path mirrors OptionActivateAction:
        // suspend the activation and report pending so the next ResolveChoice resumes it (no re-digivolve).
        try
        {
            await ActivatedEffectResolver
                .ResolveAsync(context, payload.CardId, action.PlayerId, EffectTiming.WhenDigivolving, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (DeferredChoicePendingException ex)
        {
            context.DeferredActivations.Suspend(payload.CardId, EffectTiming.WhenDigivolving, action.PlayerId);
            metadata["pendingChoice"] = true;
            metadata["pendingChoiceMessage"] = ex.Message;
            return ActionProcessResult.Success("Card digivolved; [When Digivolving] awaiting choice.", metadata);
        }

        return ActionProcessResult.Success("Card digivolved.", metadata);
    }

    /// <summary>(W6-F) App-Fusion legality: the declared condition is the requirement (the normal
    /// digivolution level/color requirement does NOT apply — AS-IS gates on
    /// CanAppFusionFromTargetPermanent); cost = the condition's cost through the play-cost pipeline.</summary>
    private static DigivolveValidation ValidateAppFusion(
        EngineContext context,
        HeadlessPlayerId playerId,
        DigivolveActionPayload payload)
    {
        if (context.ZoneMover is not IZoneStateReader zones ||
            !zones.GetCards(playerId, ChoiceZone.Hand).Contains(payload.CardId))
        {
            return DigivolveValidation.Illegal("App-Fusion card must be in hand.");
        }

        if (!zones.GetCards(playerId, ChoiceZone.BattleArea).Contains(payload.TargetCardId))
        {
            return DigivolveValidation.Illegal("App-Fusion host must be on the battle area.");
        }

        var view = new Assets.Scripts.Script.CardEffectCommons.CardSource(context, payload.CardId, playerId, playerId);
        if (view.AppFusionConditionOf() is not Assets.Scripts.Script.CardEffectCommons.AppFusionCondition condition)
        {
            return DigivolveValidation.Illegal("Card declares no App-Fusion condition.");
        }

        var host = new Assets.Scripts.Script.CardEffectCommons.Permanent(context, payload.TargetCardId, playerId);
        if (!condition.digimonCondition(host))
        {
            return DigivolveValidation.Illegal("App-Fusion host does not match the material condition.");
        }

        // AS-IS CanAppFusionFromTargetPermanent requires !CanNotEvolve(target) — same restriction gate as
        // the normal digivolve path (:421).
        CannotRestrictionResult fusionRestriction = ContinuousRestrictionGate.EvaluateDigivolve(context, payload.TargetCardId);
        if (fusionRestriction.IsRestricted)
        {
            return DigivolveValidation.Illegal($"App-Fusion host cannot digivolve ({fusionRestriction.Reason}).");
        }

        if (!LinkedIdsOf(context, payload.TargetCardId).Contains(payload.AppFusionLinkCardId) ||
            !condition.linkedCondition(host, new Assets.Scripts.Script.CardEffectCommons.CardSource(context, payload.AppFusionLinkCardId, playerId, playerId)))
        {
            return DigivolveValidation.Illegal("App-Fusion link material does not match.");
        }

        int expected = Math.Max(0, ContinuousModifierGate.ResolvePlayCost(context, payload.CardId, condition.cost));
        if (payload.MemoryCost != expected)
        {
            return DigivolveValidation.Illegal($"App-Fusion cost {payload.MemoryCost} does not match {expected}.");
        }

        if (!context.MemoryController.CanPay(expected))
        {
            return DigivolveValidation.Illegal($"Cannot pay App-Fusion cost {expected}.");
        }

        context.CardInstanceRepository.TryGetInstance(payload.CardId, out CardInstanceRecord? card);
        context.CardInstanceRepository.TryGetInstance(payload.TargetCardId, out CardInstanceRecord? target);
        return DigivolveValidation.Legal(card?.DefinitionId ?? payload.CardId, target?.DefinitionId ?? payload.TargetCardId);
    }

    private static DigivolveValidation Validate(
        EngineContext context,
        HeadlessPlayerId playerId,
        DigivolveActionPayload payload)
    {
        if (!payload.AppFusionLinkCardId.IsEmpty)
        {
            return ValidateAppFusion(context, playerId, payload);
        }

        if (playerId.IsEmpty)
        {
            return DigivolveValidation.Illegal("Player id must not be empty.");
        }

        if (payload.CardId == payload.TargetCardId)
        {
            return DigivolveValidation.Illegal("A card cannot digivolve onto itself.");
        }

        if (payload.MemoryCost < 0)
        {
            return DigivolveValidation.Illegal("Digivolve memory cost must not be negative.");
        }

        if (context.ZoneMover is not IZoneStateReader zoneReader)
        {
            return DigivolveValidation.Illegal("Zone mover does not expose readable zone state.");
        }

        if (!TryReadInstance(context, payload.CardId, out CardInstanceRecord? card, out string? cardError))
        {
            return DigivolveValidation.Illegal(cardError ?? "Digivolve card was not found.");
        }

        if (!TryReadInstance(context, payload.TargetCardId, out CardInstanceRecord? target, out string? targetError))
        {
            return DigivolveValidation.Illegal(targetError ?? "Target card was not found.", card!.DefinitionId);
        }

        if (card.OwnerId != playerId)
        {
            return DigivolveValidation.Illegal(
                $"Card instance '{payload.CardId}' is owned by player '{card.OwnerId}', not player '{playerId}'.",
                card.DefinitionId,
                target.DefinitionId);
        }

        if (target.OwnerId != playerId)
        {
            return DigivolveValidation.Illegal(
                $"Target card '{payload.TargetCardId}' is owned by player '{target.OwnerId}', not player '{playerId}'.",
                card.DefinitionId,
                target.DefinitionId);
        }

        if (!zoneReader.GetCards(playerId, ChoiceZone.Hand).Contains(payload.CardId))
        {
            return DigivolveValidation.Illegal(
                $"Card instance '{payload.CardId}' is not in player '{playerId}' hand.",
                card.DefinitionId,
                target.DefinitionId);
        }

        // GR-004: the target may be in the battle area or the breeding area (in-breeding digivolution).
        if (!zoneReader.GetCards(playerId, ChoiceZone.BattleArea).Contains(payload.TargetCardId)
            && !zoneReader.GetCards(playerId, ChoiceZone.BreedingArea).Contains(payload.TargetCardId))
        {
            return DigivolveValidation.Illegal(
                $"Target card '{payload.TargetCardId}' is not in player '{playerId}' battle or breeding area.",
                card.DefinitionId,
                target.DefinitionId);
        }

        CardRecord evolvingCard = context.CardRepository.GetCard(card.DefinitionId);
        CardRecord targetCard = context.CardRepository.GetCard(target.DefinitionId);

        if (!TryGetEvolutionCost(context, payload.CardId, payload.TargetCardId, out int repositoryCost, out string? costError))
        {
            return DigivolveValidation.Illegal(costError ?? "Card evolution cost was not found.", card.DefinitionId, target.DefinitionId);
        }

        // (FR2/M-3) When the printed condition fails and the digivolve uses an ADDED requirement, that added
        // path's OWN cost applies (AS-IS costEquation() ?? digivolutionCost) instead of the printed cost.
        if (!MatchesEvolutionCondition(evolvingCard.EvolutionCondition, targetCard)
            && TryGetAddedDigivolutionCost(context, payload.CardId, playerId, targetCard, payload.TargetCardId, target.OwnerId, out int addedPathCost))
        {
            repositoryCost = addedPathCost;
        }

        if (payload.MemoryCost != repositoryCost)
        {
            return DigivolveValidation.Illegal(
                $"Digivolve memory cost {payload.MemoryCost} does not match card evolution cost {repositoryCost}.",
                card.DefinitionId,
                target.DefinitionId);
        }
        // F-5.3: a continuous "ignore digivolution requirement" effect (AS-IS CanIgnoreDigivolutionRequirement)
        // lets the player digivolve without satisfying the printed evolution condition.
        // (d-remediation) AS-IS Player.CanIgnoreDigivolutionRequirement returns FALSE while a
        // CannotIgnoreDigivolutionCondition effect is active (BT8_059) — the ignore grants are then negated.
        bool ignoreBlocked = IsDigivolveIgnoreBlocked(context, payload.CardId, playerId, payload.TargetCardId, target.OwnerId);
        if (!MatchesEvolutionCondition(evolvingCard.EvolutionCondition, targetCard)
            && (ignoreBlocked || !CanIgnoreDigivolutionRequirement(context, playerId, payload.CardId))
            // The headless color-ignore is the negation-gated `ignore==Color && CanIgnoreDigivolutionRequirement`
            // path (CardSource.cs:596): a CannotIgnoreDigivolutionCondition effect (BT8_059) negates it too — colour
            // is part of the digivolution requirement. Verified by FAILd-06 (deliberate CannotIgnore feature test).
            && (ignoreBlocked || !(CanIgnoreColorRequirement(context, playerId, payload.CardId)
                && MatchesEvolutionCondition(evolvingCard.EvolutionCondition, targetCard, ignoreColor: true)))
            && !MatchesAddedDigivolutionRequirement(context, payload.CardId, playerId, targetCard, payload.TargetCardId, target.OwnerId))
        {
            return DigivolveValidation.Illegal(
                $"Target card '{target.DefinitionId}' does not satisfy evolution condition '{evolvingCard.EvolutionCondition}'.",
                card.DefinitionId,
                target.DefinitionId);
        }

        if (!context.MemoryController.CanPay(payload.MemoryCost))
        {
            return DigivolveValidation.Illegal(
                $"Cannot pay digivolve cost {payload.MemoryCost}.",
                card.DefinitionId,
                target.DefinitionId);
        }

        // (D-A5) Continuous effects from other cards can forbid the under-card permanent from digivolving.
        // No-op until such a restriction is registered (Phase 4 card pool); mirrors the attack/block gates.
        CannotRestrictionResult digivolveRestriction = ContinuousRestrictionGate.EvaluateDigivolve(context, payload.TargetCardId);
        if (digivolveRestriction.IsRestricted)
        {
            return DigivolveValidation.Illegal(
                $"Target '{payload.TargetCardId}' cannot digivolve ({digivolveRestriction.Reason}).",
                card.DefinitionId,
                target.DefinitionId);
        }

        return DigivolveValidation.Legal(card.DefinitionId, target.DefinitionId);
    }

    private static bool TryReadInstance(
        EngineContext context,
        HeadlessEntityId cardId,
        [NotNullWhen(true)] out CardInstanceRecord? instance,
        out string? error)
    {
        if (!context.CardInstanceRepository.TryGetInstance(cardId, out instance) || instance is null)
        {
            error = $"Card instance '{cardId}' was not found.";
            return false;
        }

        if (!context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? _))
        {
            error = $"Card definition '{instance.DefinitionId}' was not found.";
            return false;
        }

        error = null;
        return true;
    }

    internal static bool TryGetEvolutionCost(
        EngineContext context,
        HeadlessEntityId cardId,
        HeadlessEntityId targetCardId,
        out int evolutionCost,
        out string? error,
        bool ignoreLevel = false,
        bool ignoreColor = false)
    {
        evolutionCost = default;
        if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? instance) ||
            instance is null)
        {
            error = $"Card instance '{cardId}' was not found.";
            return false;
        }

        if (!context.CardInstanceRepository.TryGetInstance(targetCardId, out CardInstanceRecord? targetInstance) ||
            targetInstance is null)
        {
            error = $"Target card instance '{targetCardId}' was not found.";
            return false;
        }

        if (!context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? card) || card is null)
        {
            error = $"Card definition '{instance.DefinitionId}' was not found.";
            return false;
        }

        if (!context.CardRepository.TryGetCard(targetInstance.DefinitionId, out CardRecord? targetCard) || targetCard is null)
        {
            error = $"Target definition '{targetInstance.DefinitionId}' was not found.";
            return false;
        }

        if (!DigivolutionCostHelpers.TryResolveCost(
            card,
            instance,
            targetCard,
            targetInstance,
            out int baseCost,
            out error,
            ignoreLevel: ignoreLevel,
            ignoreColor: ignoreColor))
        {
            return false;
        }

        // D-8: fold in continuous digivolution-cost modifiers (effect-driven ±cost), honouring a
        // continuous "cost cannot be reduced" replacement. Static cost is the base. (BT1_109) pass the
        // digivolving-FROM target permanent so a two-sided player-scope cost predicate can gate on it.
        evolutionCost = ContinuousModifierGate.ResolveDigivolutionCost(context, cardId, baseCost, digivolveTargetPermanentId: targetCardId);
        error = null;
        return true;
    }

    /// <summary>(F-5.3) Whether a continuous "ignore digivolution requirement" effect applies for the
    /// digivolving player / card (card-targeted on the evolving card, or player-scope for the player).
    /// Mirrors AS-IS <c>Player.CanIgnoreDigivolutionRequirement</c>.</summary>
    public const string IgnoreDigivolutionRequirementKey = "ignoreDigivolutionRequirement";

    // (PRIM-W3 UseRequirements / IgnoreColorConditionClass) continuous "ignore the color part of the
    // digivolution requirement" (level still enforced).
    public const string IgnoreColorRequirementKey = "ignoreColorRequirement";

    // NOTE: AS-IS has NO continuous "ignore level" grant (no IgnoreLevelConditionClass) — ignore-level exists ONLY as
    // the effect-driven IgnoreRequirement.Level (CardController.SetIgnoreRequirements), handled by the effect-driven
    // digivolve path. So the player-initiated Validate deliberately has no ignore-level branch (matches AS-IS).

    private static bool CanIgnoreDigivolutionRequirement(EngineContext context, HeadlessPlayerId playerId, HeadlessEntityId cardId) =>
        HasContinuousFlag(context, playerId, cardId, IgnoreDigivolutionRequirementKey);

    private static bool CanIgnoreColorRequirement(EngineContext context, HeadlessPlayerId playerId, HeadlessEntityId cardId) =>
        HasContinuousFlag(context, playerId, cardId, IgnoreColorRequirementKey)
        || NewModelIgnoreColorActive(context, playerId, cardId);

    /// <summary>(RD-P6B-17) The new-model IIgnoreColorConditionEffect scan (AS-IS CardSource.MatchColorRequirement,
    /// mirror <see cref="Assets.Scripts.Script.CardEffectCommons.CardSource.IgnoreColorConditionActive"/>) —
    /// UseRequirements builds an IgnoreColorConditionClass that registers no binding, so the legacy
    /// IgnoreColorRequirementKey flag never carries it.</summary>
    private static bool NewModelIgnoreColorActive(EngineContext context, HeadlessPlayerId playerId, HeadlessEntityId cardId)
    {
        // (substrate adaptation 3) CanUse reads GManager.instance via AmbientMatchContext — enter the match scope
        // so the disable-check does not NRE outside a live match loop (nested enter is safe).
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        var card = new Assets.Scripts.Script.CardEffectCommons.CardSource(context, cardId, playerId, playerId);
        return card.IgnoreColorConditionActive();
    }

    /// <summary>(d-remediation, true-scan) AS-IS <c>Player.CanIgnoreDigivolutionRequirement</c>: SCAN every field
    /// permanent's effects (1:1 with the original nested foreach) and, for each usable
    /// <c>CannotIgnoreDigivolutionCondition</c> effect, evaluate its JOINT predicate
    /// <c>cannotIgnoreDigivolutionCondition(digivolvingCard, target)</c>. Any match ⇒ ignore-grants are negated.</summary>
    private static bool IsDigivolveIgnoreBlocked(EngineContext context, HeadlessEntityId cardId, HeadlessPlayerId playerId, HeadlessEntityId targetId, HeadlessPlayerId targetOwner)
    {
        // (R3-W3c-4b D) AS-IS Player.CanIgnoreDigivolutionRequirement (Player.cs:1422-1465): the live
        // ICannotIgnoreDigivolutionConditionEffect veto scan; "ignore-blocked" is its NEGATION. Rehoused from the
        // retired registry RestrictionScan (JointRestrictionEffect) to the AS-IS-literal live getter so the
        // new-model kind-class producer (CannotIgnoreDigivolutionConditionClass) is seen. digivolvingCard =
        // cardSource(cardId); target under-card = Permanent(targetId).
        if (cardId.IsEmpty || targetId.IsEmpty || playerId.IsEmpty || targetOwner.IsEmpty)
        {
            return false;
        }

        var player = new Player(context, playerId);
        var target = new Permanent(context, targetId, targetOwner);
        var digivolvingCard = new CardSource(context, cardId, playerId, playerId);
        return !player.CanIgnoreDigivolutionRequirement(target, digivolvingCard);
    }

    private static bool HasContinuousFlag(EngineContext context, HeadlessPlayerId playerId, HeadlessEntityId cardId, string flagKey)
    {
        // (FR2/M-1) Use the condition-aware applicable set (card-targeted + player-scope + predicate, with the
        // effect's `condition` gate honoured) so an ignore-flag that is CONDITIONAL — e.g. UseRequirements
        // "ignore color IF you control a <matching> Digimon" — is only seen when its gate passes.
        foreach (EffectRequest effect in ContinuousScopeEvaluation.ApplicableEffects(context, ContinuousRestrictionGate.Scope, cardId))
        {
            if (ReadBool(effect.Context.Values, flagKey))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>(PRIM-W1-6/9) A continuous "added digivolution requirement" — an ADDITIONAL "Color@Level"
    /// from-condition granted by an effect (AS-IS AddDigivolutionRequirementStaticEffect /
    /// AddDigivolutionRequirementClass). If the printed condition fails but an added condition matches the
    /// target, the digivolve is legal. Read off continuous effects on the evolving card (and player-scope),
    /// same query seam as the ignore-requirement effect. (Per-path digivolution cost is composed via
    /// ChangeDigivolutionCostStaticEffect / handled per-card.)</summary>
    public const string AddedEvolutionConditionKey = "addedEvolutionCondition";

    // (PRIM-W5) added-source digivolution requirement key carrying an arbitrary predicate over the under-card
    // (AS-IS AddSelfDigivolutionRequirementStaticEffect's Func<Permanent,bool>).
    public const string AddedEvolutionPredicateKey = "addedEvolutionPredicate";

    // (A2) AS-IS AddDigivolutionRequirement.cs GetEvoCost: the level range is a HARD GATE separate from (and
    // OUTSIDE of) permanentCondition — exact `level` wins; min/max apply only when `level < 0`; a permanent
    // without a level never passes a configured gate. -1 = unset (AS-IS default).
    public const string AddedEvolutionLevelKey = "addedEvolutionLevel";
    public const string AddedEvolutionMinLevelKey = "addedEvolutionMinLevel";
    public const string AddedEvolutionMaxLevelKey = "addedEvolutionMaxLevel";

    // (FR2/M-3) the memory cost of an added digivolution path. When the printed condition fails and this added
    // requirement is used, this cost applies instead of the printed cost. AddedEvolutionCostEquationKey (a
    // Func<int>) overrides the fixed AddedEvolutionCostKey when present (AS-IS costEquation() ?? digivolutionCost).
    public const string AddedEvolutionCostKey = "addedEvolutionCost";
    public const string AddedEvolutionCostEquationKey = "addedEvolutionCostEquation";

    // (FR2/M-3) The cost of the MATCHING added digivolution path for this (evolving card -> target), or false if
    // none. costEquation() (dynamic) overrides the fixed cost when present, mirroring the original.
    private static bool TryGetAddedDigivolutionCost(
        EngineContext context, HeadlessEntityId cardId, HeadlessPlayerId playerId, CardRecord targetCard,
        HeadlessEntityId targetInstanceId, HeadlessPlayerId targetOwner, out int cost)
    {
        cost = 0;
        foreach (EffectRequest effect in ContinuousScopeEvaluation.ApplicableEffects(context, ContinuousRestrictionGate.Scope, cardId)
                     .Concat(OwnAddedRequirementRequests(context, cardId)))
        {
            if (!(AddedConditionActive(effect, targetCard) || AddedPredicateActive(context, effect, playerId, cardId, targetInstanceId, targetOwner)))
            {
                continue;
            }

            IReadOnlyDictionary<string, object?> values = effect.Context.Values;
            if (values.TryGetValue(AddedEvolutionCostEquationKey, out object? eqRaw) && eqRaw is Func<int> equation)
            {
                cost = equation();
                return true;
            }

            if (values.TryGetValue(AddedEvolutionCostKey, out object? costRaw) && costRaw is int fixedCost)
            {
                cost = fixedCost;
                return true;
            }
        }

        // (RD-P6B-15) UNION the new-model IAddDigivolutionRequirementEffect scan (AS-IS CardSource.CostList) —
        // AddDigivolutionRequirementClass registers no binding, so the legacy loop above never sees it. The
        // added path's own cost is its GetEvoCost return (color/level/predicate all gated in the closure);
        // AS-IS PayingCost uses costList.Min() when the printed path fails.
        List<int> newModelCosts = NewModelAddedDigivolutionCosts(context, cardId, playerId, targetInstanceId, targetOwner);
        if (newModelCosts.Count > 0)
        {
            cost = newModelCosts.Min();
            return true;
        }

        return false;
    }

    /// <summary>(RD-P6B-15) The new-model added-requirement costs for digivolving <paramref name="cardId"/>
    /// onto the target permanent, via the AS-IS <c>CardSource.EvoCosts</c>/<c>CostList</c> interface scan
    /// (<see cref="Assets.Scripts.Script.CardEffectCommons.CardSource.AddedDigivolutionCosts"/>). Registered
    /// legacy grants are read by the loop above; this reaches new-model <c>AddDigivolutionRequirementClass</c>
    /// grants (which register no binding).</summary>
    private static List<int> NewModelAddedDigivolutionCosts(
        EngineContext context, HeadlessEntityId cardId, HeadlessPlayerId playerId,
        HeadlessEntityId targetInstanceId, HeadlessPlayerId targetOwner)
    {
        // (substrate adaptation 3, per NewModelContinuousScan) CanUse/CanTrigger/IsDisabled read game state
        // through GManager.instance, which the mirror resolves from AmbientMatchContext — enter the match scope
        // so the disable-check (CheckEffectDisabledClass reads GManager.instance...gameContext.Players) does not
        // NRE outside a live match loop. Nested enter is safe (in real play the digivolve already runs inside one).
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        var evolvingCard = new Assets.Scripts.Script.CardEffectCommons.CardSource(context, cardId, playerId, playerId);
        var targetPermanent = new Assets.Scripts.Script.CardEffectCommons.Permanent(context, targetInstanceId, targetOwner);
        return evolvingCard.AddedDigivolutionCosts(
            targetPermanent, Assets.Scripts.Script.CardEffectCommons.CardEffectCommons.IgnoreRequirement.None, checkAvailability: false);
    }

    private static bool MatchesAddedDigivolutionRequirement(
        EngineContext context, HeadlessEntityId cardId, HeadlessPlayerId playerId, CardRecord targetCard,
        HeadlessEntityId targetInstanceId, HeadlessPlayerId targetOwner)
    {
        string scope = ContinuousRestrictionGate.Scope;

        // (FR2/M-1) The added requirement applies to the TARGET card (cardId). Use the condition-aware applicable
        // set — card-targeted AND player-scope with the cardCondition predicate (AS-IS: which cards receive the
        // added "can digivolve from X" requirement, e.g. ST8_04 "your UlforceVeedramon cards in hand") evaluated
        // 1:1 against the target — so a non-self cardCondition reaches every matching card, not just the source.
        foreach (EffectRequest effect in ContinuousScopeEvaluation.ApplicableEffects(context, scope, cardId)
                     .Concat(OwnAddedRequirementRequests(context, cardId)))
        {
            if (AddedConditionActive(effect, targetCard) || AddedPredicateActive(context, effect, playerId, cardId, targetInstanceId, targetOwner))
            {
                return true;
            }
        }

        // (RD-P6B-15) UNION the new-model IAddDigivolutionRequirementEffect scan (AS-IS CardSource.EvoCosts):
        // any matching added path (GetEvoCost >= 0) makes the target a legal digivolution source.
        return NewModelAddedDigivolutionCosts(context, cardId, playerId, targetInstanceId, targetOwner).Count > 0;
    }

    /// <summary>(C5-witness / EX8_061) The MOVING card's OWN added-requirement statics. A card declaring
    /// <c>AddSelfDigivolutionRequirementStaticEffect</c> at <c>EffectTiming.None</c> is typically still in
    /// HAND when the digivolve is checked — never registered (registration is enter-play only) — while AS-IS
    /// reads <c>cardSource.EffectList(timing)</c> zone-independently (Player.EffectList scans hand/trash too).
    /// Mirror with the established dispatch-first idiom (<c>CardSource.LinkConditionOf</c> /
    /// <c>DigivolutionCostGateEffect.CollectOwnGatedModifiers</c>): build the moving card's None-timing
    /// effects via <c>CardEffectDispatch</c> and surface the added-requirement requests alongside the registry
    /// set. A non-null AS-IS <c>cardCondition</c> (which cards RECEIVE the requirement) is evaluated against
    /// the moving card itself here — the cross-card grant of a REGISTERED source still flows through the
    /// registry player-scope path. Duplicates with a registered self-binding are harmless: both consumers
    /// are any-match / first-cost reads.</summary>
    private static IEnumerable<EffectRequest> OwnAddedRequirementRequests(EngineContext context, HeadlessEntityId cardId)
    {
        if (cardId.IsEmpty
            || !context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? instance) || instance is null
            || !context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? def) || def is null
            || !Assets.Scripts.Script.CardEffectCommons.CardEffectDispatch.TryCreateForCard(def, out Assets.Scripts.Script.CardEffectCommons.CEntity_Effect? entity) || entity is null)
        {
            yield break;
        }

        var movingCard = new Assets.Scripts.Script.CardEffectCommons.CardSource(context, cardId, instance.OwnerId, instance.OwnerId);
        int index = 0;
        foreach (Assets.Scripts.Script.CardEffectCommons.ICardEffect effect in entity.CardEffects(EffectTiming.None, movingCard))
        {
            if (effect is Assets.Scripts.Script.CardEffectCommons.AddedDigivolutionRequirementPredicateEffect predicateEffect)
            {
                if (predicateEffect.TargetCardCondition is not null && !predicateEffect.TargetCardCondition(movingCard))
                {
                    index++;
                    continue;
                }

                yield return predicateEffect.ToBinding($"dispatch:{cardId.Value}:asdr:{index++}").Request;
            }
            else if (effect is Assets.Scripts.Script.CardEffectCommons.AddedDigivolutionRequirementEffect colorLevelEffect)
            {
                yield return colorLevelEffect.ToBinding($"dispatch:{cardId.Value}:asdr:{index++}").Request;
            }
        }
    }

    // (PRIM-W5) honour a predicate-based added source: build the target under-card as a Permanent and evaluate
    // the card-authored Func<Permanent,bool> (with the same continuous.condition gating).
    private static bool AddedPredicateActive(EngineContext context, EffectRequest effect, HeadlessPlayerId playerId, HeadlessEntityId cardId, HeadlessEntityId targetInstanceId, HeadlessPlayerId targetOwner)
    {
        if (!effect.Context.Values.TryGetValue(AddedEvolutionPredicateKey, out object? raw)
            || raw is not Func<Assets.Scripts.Script.CardEffectCommons.Permanent, bool> predicate)
        {
            return false;
        }

        if (effect.Context.Values.TryGetValue(Assets.Scripts.Script.CardEffectCommons.ContinuousSelfModifierEffect.ConditionKey, out object? condRaw)
            && condRaw is Func<bool> condition && !condition())
        {
            return false;
        }

        var permanent = new Assets.Scripts.Script.CardEffectCommons.Permanent(context, targetInstanceId, targetOwner);
        // (A2) AS-IS GetEvoCost: the level gate is the OUTER guard — only after it passes are the card /
        // permanent conditions evaluated (and only then is the cost returned).
        if (!AddedLevelGatePasses(context, effect.Context.Values, playerId, cardId, permanent))
        {
            return false;
        }

        return predicate(permanent);
    }

    // (A2) AS-IS AddDigivolutionRequirement.cs:64-72 — 1:1: no configured level => pass ("ignoreLevel");
    // an active ignore-requirement effect also waives the gate (AS-IS ignore==Level/All &&
    // CanIgnoreDigivolutionRequirement); otherwise the source permanent must HAVE a level and match the
    // exact `level`, or (only when `level < 0`) the min/max range.
    private static bool AddedLevelGatePasses(
        EngineContext context, IReadOnlyDictionary<string, object?> values,
        HeadlessPlayerId playerId, HeadlessEntityId cardId,
        Assets.Scripts.Script.CardEffectCommons.Permanent permanent)
    {
        int level = ReadIntValue(values, AddedEvolutionLevelKey);
        int minLevel = ReadIntValue(values, AddedEvolutionMinLevelKey);
        int maxLevel = ReadIntValue(values, AddedEvolutionMaxLevelKey);
        // (P1-DV-2) AS-IS AddDigivolutionRequirement gates the ignore-level waive with the negation
        // (`ignore==Level/All && CanIgnoreDigivolutionRequirement`, AddDigivolutionRequirement.cs:64-72): while a
        // CannotIgnoreDigivolutionCondition effect is active the ignore is VOIDED, so the level gate is NOT waived.
        // The headless splits grant (CanIgnoreDigivolutionRequirement) and negation (IsDigivolveIgnoreBlocked) —
        // the added path previously consulted only the grant, letting an ignore-path digivolve through a live negation.
        if ((level < 0 && minLevel < 0 && maxLevel < 0)
            || (CanIgnoreDigivolutionRequirement(context, playerId, cardId)
                && !IsDigivolveIgnoreBlocked(context, cardId, playerId, permanent.InstanceId, permanent.OwnerId)))
        {
            return true;
        }

        if (!permanent.TopCard.HasLevel)
        {
            return false;
        }

        int actual = permanent.TopCard.Level;
        return actual == level
            || (level < 0 && (minLevel < 0 || actual >= minLevel) && (maxLevel < 0 || actual <= maxLevel));
    }

    private static int ReadIntValue(IReadOnlyDictionary<string, object?> values, string key) =>
        values.TryGetValue(key, out object? raw) && raw is int value ? value : -1;

    // A raw registry read does not run ContinuousScopeEvaluation's condition filter, so honour the
    // card-authored `continuous.condition` predicate here before applying the added requirement.
    private static bool AddedConditionActive(EffectRequest effect, CardRecord targetCard)
    {
        if (!effect.Context.Values.TryGetValue(AddedEvolutionConditionKey, out object? raw) || raw is not string token)
        {
            return false;
        }

        if (effect.Context.Values.TryGetValue(Assets.Scripts.Script.CardEffectCommons.ContinuousSelfModifierEffect.ConditionKey, out object? condRaw)
            && condRaw is Func<bool> condition && !condition())
        {
            return false;
        }

        return MatchesEvolutionCondition(token, targetCard);
    }

    private static bool MatchesEvolutionCondition(string? condition, CardRecord targetCard, bool ignoreColor = false)
    {
        if (string.IsNullOrWhiteSpace(condition))
        {
            return true;
        }

        string[] tokens = condition
            .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return tokens.Any(token =>
        {
            string normalized = token;
            if (normalized.StartsWith("definition:", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized["definition:".Length..];
            }
            else if (normalized.StartsWith("from:", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized["from:".Length..];
            }

            // (G8-001) The card-data loader encodes each printed digivolution requirement as
            // "Color@Level(:Cost)" (e.g. "Red@3:2" = digivolve from a Red Lv.3). Match it against the
            // target's actual color(s) and level; only fall back to the legacy id/number/type tokens.
            if (TryParseColorLevel(normalized, out string? fromColor, out int fromLevel))
            {
                // (PRIM-W3 UseRequirements / IgnoreColorConditionClass) when color is ignored, only the
                // level must match — the printed color requirement is waived.
                return (ignoreColor || TargetHasColor(targetCard, fromColor!)) && TargetLevel(targetCard) == fromLevel;
            }

            return string.Equals(normalized, targetCard.Id.Value, StringComparison.Ordinal) ||
                string.Equals(normalized, targetCard.CardNumber, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, targetCard.CardType, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static bool TryParseColorLevel(string token, out string? color, out int level)
    {
        color = null;
        level = -1;
        int at = token.IndexOf('@');
        if (at <= 0 || at >= token.Length - 1)
        {
            return false;
        }

        color = token[..at].Trim();
        string rest = token[(at + 1)..];
        int colon = rest.IndexOf(':');
        string levelText = (colon >= 0 ? rest[..colon] : rest).Trim();
        return int.TryParse(levelText, out level) && color.Length > 0;
    }

    private static bool TargetHasColor(CardRecord targetCard, string color)
    {
        if (targetCard.Metadata.TryGetValue("colors", out object? raw) && raw is IEnumerable<string> colors)
        {
            return colors.Any(c => string.Equals(c, color, StringComparison.OrdinalIgnoreCase));
        }

        return targetCard.Metadata.TryGetValue("color", out object? single)
            && string.Equals(single?.ToString(), color, StringComparison.OrdinalIgnoreCase);
    }

    private static int? TargetLevel(CardRecord targetCard)
    {
        if (!targetCard.Metadata.TryGetValue("level", out object? raw) || raw is null)
        {
            return null;
        }

        return raw switch
        {
            int i => i,
            long l => (int)l,
            string s when int.TryParse(s, out int p) => p,
            _ => null,
        };
    }

    internal static IReadOnlyList<HeadlessEntityId> AttachTargetAsSource(
        ICardInstanceRepository repository,
        HeadlessEntityId cardId,
        HeadlessEntityId targetCardId)
    {
        CardInstanceRecord card = repository.TryGetInstance(cardId, out CardInstanceRecord? currentCard) && currentCard is not null
            ? currentCard
            : throw new InvalidOperationException($"Card instance '{cardId}' was not found.");
        CardInstanceRecord target = repository.TryGetInstance(targetCardId, out CardInstanceRecord? currentTarget) && currentTarget is not null
            ? currentTarget
            : throw new InvalidOperationException($"Target card '{targetCardId}' was not found.");

        HeadlessEntityId[] sourceIds = new[] { targetCardId }
            .Concat(ReadSourceIds(target.Metadata))
            .Concat(ReadSourceIds(card.Metadata))
            .Distinct()
            .ToArray();

        // N-1 (summoning sickness): digivolving keeps the SAME permanent in the original (the top card
        // is swapped, EnterFieldTurnCount is not reset), so the evolved Digimon INHERITS the under-card's
        // entered-this-turn status. A Digimon that has been on the field since a prior turn stays able to
        // attack after digivolving; one played this turn remains sick. Jogress/breeding paths are exempt
        // (they never set the flag), matching the original's EnterFieldTurnCount = -1.
        bool inheritedEnteredThisTurn = ReadBool(target.Metadata, "enteredThisTurn");
        Dictionary<string, object?> metadata = new(card.Metadata, StringComparer.Ordinal)
        {
            [SourceIdsMetadataKey] = sourceIds.Select(id => id.Value).ToArray(),
            ["digivolvedFromCardId"] = targetCardId.Value,
            ["digivolvedFromDefinitionId"] = target.DefinitionId.Value,
            ["enteredThisTurn"] = inheritedEnteredThisTurn
        };
        repository.Upsert(card with { Metadata = metadata });
        return sourceIds;
    }

    private static bool ReadBool(IReadOnlyDictionary<string, object?> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out object? rawValue) || rawValue is null)
        {
            return false;
        }

        return rawValue switch
        {
            bool value => value,
            string value => bool.TryParse(value, out bool parsed) && parsed,
            _ => false
        };
    }

    private static IReadOnlyList<HeadlessEntityId> ReadSourceIds(IReadOnlyDictionary<string, object?> metadata)
    {
        if (!metadata.TryGetValue(SourceIdsMetadataKey, out object? rawValue) || rawValue is null)
        {
            return Array.Empty<HeadlessEntityId>();
        }

        if (rawValue is IEnumerable<HeadlessEntityId> entityIds)
        {
            return entityIds.ToArray();
        }

        if (rawValue is IEnumerable<string> stringIds)
        {
            return stringIds
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => new HeadlessEntityId(value))
                .ToArray();
        }

        return rawValue is string stringValue
            ? stringValue
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => new HeadlessEntityId(value))
                .ToArray()
            : Array.Empty<HeadlessEntityId>();
    }

    private static Dictionary<string, object?> Metadata(
        LegalAction action,
        DigivolveActionPayload payload,
        DigivolveValidation validation)
    {
        Dictionary<string, object?> metadata = BaseMetadata(action);
        metadata[HeadlessActionParameterKeys.CardId] = payload.CardId.Value;
        metadata[HeadlessActionParameterKeys.TargetCardId] = payload.TargetCardId.Value;
        metadata[HeadlessActionParameterKeys.MemoryCost] = payload.MemoryCost;
        metadata["cardDefinitionId"] = validation.CardDefinitionId?.Value;
        metadata["targetDefinitionId"] = validation.TargetDefinitionId?.Value;
        return metadata;
    }

    private static Dictionary<string, object?> BaseMetadata(LegalAction action)
    {
        return new Dictionary<string, object?>
        {
            [HeadlessActionParameterKeys.ActionId] = action.Id.Value,
            [HeadlessActionParameterKeys.PlayerId] = action.PlayerId.Value,
            [HeadlessActionParameterKeys.ActionType] = action.ActionType
        };
    }
}

public sealed record DigivolveActionPayload(
    HeadlessEntityId CardId,
    HeadlessEntityId TargetCardId,
    int MemoryCost)
{
    /// <summary>(W6-F) parameter key carrying the App-Fusion link material (the host's LINK card that is
    /// consumed into the fused stack's sources — AS-IS selectAppFusionEffect.AddToSources).</summary>
    public const string AppFusionLinkCardKey = "appFusionLinkCard";

    /// <summary>(W6-F) the App-Fusion link material; empty = a normal digivolution.</summary>
    public HeadlessEntityId AppFusionLinkCardId { get; init; }

    public IReadOnlyDictionary<string, object?> ToParameters()
    {
        var parameters = new Dictionary<string, object?>
        {
            [HeadlessActionParameterKeys.CardId] = CardId,
            [HeadlessActionParameterKeys.TargetCardId] = TargetCardId,
            [HeadlessActionParameterKeys.MemoryCost] = MemoryCost
        };
        if (!AppFusionLinkCardId.IsEmpty)
        {
            parameters[AppFusionLinkCardKey] = AppFusionLinkCardId.Value;
        }

        return parameters;
    }

    public static bool TryRead(
        LegalAction action,
        [NotNullWhen(true)] out DigivolveActionPayload? payload,
        out string? error)
    {
        if (!HeadlessActionPayloadReader.TryReadEntityId(
                action,
                HeadlessActionParameterKeys.CardId,
                out HeadlessEntityId cardId,
                out error))
        {
            payload = null;
            return false;
        }

        if (!HeadlessActionPayloadReader.TryReadEntityId(
                action,
                HeadlessActionParameterKeys.TargetCardId,
                out HeadlessEntityId targetCardId,
                out error))
        {
            payload = null;
            return false;
        }

        if (!TryReadInt(action.Parameters, HeadlessActionParameterKeys.MemoryCost, out int memoryCost))
        {
            payload = null;
            error = $"Missing action parameter: {HeadlessActionParameterKeys.MemoryCost}.";
            return false;
        }

        HeadlessEntityId appFusionLink = default;
        if (action.Parameters.TryGetValue(AppFusionLinkCardKey, out object? rawLink) &&
            rawLink?.ToString() is { Length: > 0 } linkValue)
        {
            appFusionLink = new HeadlessEntityId(linkValue);
        }

        payload = new DigivolveActionPayload(cardId, targetCardId, memoryCost) { AppFusionLinkCardId = appFusionLink };
        error = null;
        return true;
    }

    private static bool TryReadInt(
        IReadOnlyDictionary<string, object?> parameters,
        string key,
        out int value)
    {
        if (!parameters.TryGetValue(key, out object? rawValue) || rawValue is null)
        {
            value = default;
            return false;
        }

        if (rawValue is int intValue)
        {
            value = intValue;
            return true;
        }

        if (rawValue is long longValue &&
            longValue >= int.MinValue &&
            longValue <= int.MaxValue)
        {
            value = (int)longValue;
            return true;
        }

        if (rawValue is string stringValue && int.TryParse(stringValue, out int parsedValue))
        {
            value = parsedValue;
            return true;
        }

        value = default;
        return false;
    }
}

public sealed record DigivolveValidation(
    bool IsLegal,
    string Reason,
    HeadlessEntityId? CardDefinitionId,
    HeadlessEntityId? TargetDefinitionId)
{
    public static DigivolveValidation Legal(
        HeadlessEntityId cardDefinitionId,
        HeadlessEntityId targetDefinitionId)
    {
        return new DigivolveValidation(true, string.Empty, cardDefinitionId, targetDefinitionId);
    }

    public static DigivolveValidation Illegal(
        string reason,
        HeadlessEntityId? cardDefinitionId = null,
        HeadlessEntityId? targetDefinitionId = null)
    {
        return new DigivolveValidation(false, reason ?? string.Empty, cardDefinitionId, targetDefinitionId);
    }
}
