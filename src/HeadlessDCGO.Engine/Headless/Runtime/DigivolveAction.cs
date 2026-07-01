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

        return actions
            .OrderBy(action => action.Id.Value, StringComparer.Ordinal)
            .ToArray();
    }

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
        // GR-004: digivolution is IN PLACE — the new top card lands in the SAME area the target occupied
        // (breeding stays in breeding, battle stays in battle); only hardcoding BattleArea would have
        // teleported an in-breeding digivolution onto the battle area.
        ChoiceZone targetZone =
            context.ZoneMover is IZoneStateReader reader
            && reader.GetCards(action.PlayerId, ChoiceZone.BreedingArea).Contains(payload.TargetCardId)
                ? ChoiceZone.BreedingArea
                : ChoiceZone.BattleArea;
        ZoneMoveResult targetRemoval = await context.ZoneMover.MoveAsync(
            new ZoneMoveRequest(
                action.PlayerId,
                payload.TargetCardId,
                targetZone,
                ChoiceZone.None),
            cancellationToken).ConfigureAwait(false);
        ZoneMoveResult cardMovement = await context.ZoneMover.MoveAsync(
            new ZoneMoveRequest(
                action.PlayerId,
                payload.CardId,
                ChoiceZone.Hand,
                targetZone),
            cancellationToken).ConfigureAwait(false);
        // F-6.7: wrap the digivolve-cost payment with the Before/AfterPayCost windows.
        TriggerEventEmitter.Emit(context.GameEventQueue, TriggerTimings.BeforePayCost, actor: action.PlayerId, subject: payload.CardId);
        HeadlessMemoryState paidMemory = context.MemoryController.Pay(payload.MemoryCost);
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

        // W1: open the WhenDigivolving timing window for the card that just digivolved.
        TriggerEventEmitter.Emit(
            context.GameEventQueue,
            TriggerTimings.WhenDigivolving,
            actor: action.PlayerId,
            subject: payload.CardId);

        // F-6.4: digivolving places the previous card(s) under the new top as digivolution sources —
        // open the OnAddDigivolutionCards window scoped to the receiving (top) card.
        if (sourceIds.Count > 0)
        {
            TriggerEventEmitter.Emit(
                context.GameEventQueue,
                TriggerTimings.OnAddDigivolutionCards,
                actor: action.PlayerId,
                subject: payload.CardId);
        }

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

    private static DigivolveValidation Validate(
        EngineContext context,
        HeadlessPlayerId playerId,
        DigivolveActionPayload payload)
    {
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
        if (!MatchesEvolutionCondition(evolvingCard.EvolutionCondition, targetCard)
            && !CanIgnoreDigivolutionRequirement(context, playerId, payload.CardId)
            && !(CanIgnoreColorRequirement(context, playerId, payload.CardId)
                && MatchesEvolutionCondition(evolvingCard.EvolutionCondition, targetCard, ignoreColor: true))
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

    private static bool TryGetEvolutionCost(
        EngineContext context,
        HeadlessEntityId cardId,
        HeadlessEntityId targetCardId,
        out int evolutionCost,
        out string? error)
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
            out error))
        {
            return false;
        }

        // D-8: fold in continuous digivolution-cost modifiers (effect-driven ±cost), honouring a
        // continuous "cost cannot be reduced" replacement. Static cost is the base.
        evolutionCost = ContinuousModifierGate.ResolveDigivolutionCost(context, cardId, baseCost);
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

    private static bool CanIgnoreDigivolutionRequirement(EngineContext context, HeadlessPlayerId playerId, HeadlessEntityId cardId) =>
        HasContinuousFlag(context, playerId, cardId, IgnoreDigivolutionRequirementKey);

    private static bool CanIgnoreColorRequirement(EngineContext context, HeadlessPlayerId playerId, HeadlessEntityId cardId) =>
        HasContinuousFlag(context, playerId, cardId, IgnoreColorRequirementKey);

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
        foreach (EffectRequest effect in ContinuousScopeEvaluation.ApplicableEffects(context, ContinuousRestrictionGate.Scope, cardId))
        {
            if (!(AddedConditionActive(effect, targetCard) || AddedPredicateActive(context, effect, targetInstanceId, targetOwner)))
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

        return false;
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
        foreach (EffectRequest effect in ContinuousScopeEvaluation.ApplicableEffects(context, scope, cardId))
        {
            if (AddedConditionActive(effect, targetCard) || AddedPredicateActive(context, effect, targetInstanceId, targetOwner))
            {
                return true;
            }
        }

        return false;
    }

    // (PRIM-W5) honour a predicate-based added source: build the target under-card as a Permanent and evaluate
    // the card-authored Func<Permanent,bool> (with the same continuous.condition gating).
    private static bool AddedPredicateActive(EngineContext context, EffectRequest effect, HeadlessEntityId targetInstanceId, HeadlessPlayerId targetOwner)
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

        return predicate(new Assets.Scripts.Script.CardEffectCommons.Permanent(context, targetInstanceId, targetOwner));
    }

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

    private static IReadOnlyList<HeadlessEntityId> AttachTargetAsSource(
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
    public IReadOnlyDictionary<string, object?> ToParameters()
    {
        return new Dictionary<string, object?>
        {
            [HeadlessActionParameterKeys.CardId] = CardId,
            [HeadlessActionParameterKeys.TargetCardId] = TargetCardId,
            [HeadlessActionParameterKeys.MemoryCost] = MemoryCost
        };
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

        payload = new DigivolveActionPayload(cardId, targetCardId, memoryCost);
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
