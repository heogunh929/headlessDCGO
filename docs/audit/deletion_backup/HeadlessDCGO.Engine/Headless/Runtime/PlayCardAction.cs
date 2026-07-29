// ============================================================================
// ⛔ DELETION-TARGET · DO-NOT-REFERENCE
// 원장(docs/audit/filelist/merged_files_no_cards.csv): 삭제대상여부=Y · 결함여부=Y
// 분류: substrate 오배치(other)
// 미러 원가(재이관 대상): DCGO/Assets/Scripts/Script/CardController.cs; DCGO/Assets/Scripts/Script/CardSource.cs::CardController.PlayCardClass.PlayCard (합법성/진입 파이프라인); CardSource.GetPayingCost / CanPlayCardTargetFrame / CanEnt
// 이 파일은 AS-IS 원본에 동일-경로 대응이 없는 오배치/발명 코드다.
// 규칙 로직은 위 미러 원가로 재이관 후 이 파일은 삭제 예정.
// 서브에이전트/포팅 작업 시: 이 파일의 심볼을 참조·모방·확장하지 말 것.
// ============================================================================
namespace HeadlessDCGO.Engine.Headless.Runtime;

using System.Diagnostics.CodeAnalysis;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class PlayCardAction
{
    public IReadOnlyList<LegalAction> GetLegalActions(
        EngineContext context,
        HeadlessPlayerId playerId)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (playerId.IsEmpty || context.ZoneMover is not IZoneStateReader zoneReader)
        {
            return Array.Empty<LegalAction>();
        }

        return zoneReader
            .GetCards(playerId, ChoiceZone.Hand)
            .SelectMany(cardId => CreateLegalActionsIfPlayable(context, playerId, cardId))
            .OrderBy(action => action.Id.Value, StringComparer.Ordinal)
            .ToArray();
    }

    private IEnumerable<LegalAction> CreateLegalActionsIfPlayable(
        EngineContext context,
        HeadlessPlayerId playerId,
        HeadlessEntityId cardId)
    {
        LegalAction? normal = CreateLegalActionIfPlayable(context, playerId, cardId);
        if (normal is not null)
        {
            yield return normal;
        }

        // (AD1-A) the Assembly variant of the SAME play (AS-IS folds it into the ordinary play flow,
        // CardController.cs:753-761): the card declares an AssemblyCondition and the owner's TRASH can fill
        // the full material set -> offer the play at (base - reduceCost), materials parameterized. One
        // action per playable assembly with the first valid material assignment (the same reduction policy
        // as DigiXros/DNA material matching).
        if (CreateAssemblyActionIfPlayable(context, playerId, cardId) is LegalAction assembly)
        {
            yield return assembly;
        }
    }

    private static LegalAction? CreateAssemblyActionIfPlayable(
        EngineContext context,
        HeadlessPlayerId playerId,
        HeadlessEntityId cardId)
    {
        if (!TryGetPlayCost(context, cardId, out int playCost, out _, checkAvailability: true))
        {
            return null;
        }

        var view = new Assets.Scripts.Script.CardEffectCommons.CardSource(context, cardId, playerId);
        // AS-IS gates the Assembly play on `card.HasAssembly && !isEvolution` ALONE (CardController.cs:755) —
        // reduceCost-independent. Every AS-IS AssemblyCondition carries reduceCost > 0 (corpus values 2..7), and
        // even a reduceCost==0 condition (a cost-neutral material tuck) would be a legal AS-IS Assembly play; the
        // former `reduceCost <= 0` short-circuit was a stricter-than-AS-IS suppression with no AS-IS basis. The
        // offer is therefore gated only on the condition existing and its full material set being fillable from
        // the owner's trash.
        if (view.AssemblyConditionOf() is not Assets.Scripts.Script.CardEffectCommons.AssemblyCondition condition ||
            !Assets.Scripts.Script.SelectAssemblyClass.TryMatchMaterials(context, view, condition, out List<HeadlessEntityId> materials))
        {
            return null;
        }

        // AS-IS: Cost -= reduceCost only for the FULL set (GetPayingCost, CardSource.cs:705-737).
        int reducedCost = Math.Max(0, playCost - condition.reduceCost);
        PlayCardActionPayload payload = new(cardId, reducedCost, ChoiceZone.Hand, ChoiceZone.BattleArea)
        {
            AssemblyMaterials = materials,
        };
        if (!Validate(context, playerId, payload).IsLegal)
        {
            return null;
        }

        return HeadlessActionFactory.Create(
            HeadlessActionTypes.PlayCard,
            playerId,
            $"{playerId.Value}:{HeadlessActionTypes.PlayCard}:assembly:{cardId.Value}",
            payload.ToParameters());
    }

    public async Task<ActionProcessResult> ProcessAsync(
        LegalAction action,
        EngineContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (!PlayCardActionPayload.TryRead(action, out PlayCardActionPayload? payload, out string? error))
        {
            return ActionProcessResult.Failure(error ?? "Invalid PlayCard payload.", BaseMetadata(action));
        }

        PlayCardValidation validation = Validate(context, action.PlayerId, payload);
        if (!validation.IsLegal)
        {
            return ActionProcessResult.Illegal(action, validation.Reason, Metadata(action, payload, validation));
        }

        // F-6.7: wrap the play-cost payment with the Before/AfterPayCost windows (subject = the card).
        TriggerEventEmitter.Emit(context.GameEventQueue, TriggerTimings.BeforePayCost, actor: action.PlayerId, subject: payload.CardId,
            extraMetadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["isEvolution"] = false });

        // (EX8_074 Stage 3 brick 2) "When this card would be played" activated effects — e.g. suspend N of
        // your Digimon to reduce this card's play cost (SuspendCostReductionEffect). Resolve them BEFORE the
        // cost is locked in, then re-resolve so the reduction is actually paid. ResolveAsync is a no-op
        // (returns 0) for the vast majority of cards, which have no BeforePayCost effect — so the normal play
        // path is unchanged. NOTE: Validate already required the FULL (unreduced) cost to be payable, so this
        // brick only makes you pay LESS; offering the card when you can only afford the reduced cost is the
        // availability concern (brick 3).
        int memoryCost = payload.MemoryCost;
        context.CurrentPayCostRoot = PayCostRoot.Play;   // (B.O.4 #1) gate [BeforePayCost] effects to the play action.
        try
        {
            int beforePayCostResolved = await ActivatedEffectResolver
                .ResolveAsync(context, payload.CardId, action.PlayerId, EffectTiming.BeforePayCost, cancellationToken)
                .ConfigureAwait(false);
            if (beforePayCostResolved > 0 && TryGetPlayCost(context, payload.CardId, out int reResolvedCost, out _))
            {
                memoryCost = reResolvedCost;
            }
        }
        catch (DeferredChoicePendingException ex)
        {
            // (brick 2b) The BeforePayCost effect asked an interactive provider which Digimon to suspend. The
            // resolver did NOT flush its sink and the cost is NOT yet paid — the card is still in hand, nothing
            // is partially applied. Mirror the OptionActivate deferral: record the suspended PLAY so the next
            // ResolveChoice replays the answer and FINISHES the play (pay reduced cost + move) via
            // CompleteDeferredPlayAsync through the MetadataActionProcessor resume seam (no re-validate, no
            // re-emit of BeforePayCost — commit-once across the pre-payment boundary).
            context.DeferredActivations.Suspend(payload.CardId, EffectTiming.BeforePayCost, action.PlayerId);
            Dictionary<string, object?> pending = Metadata(action, payload, validation);
            pending["pendingChoice"] = true;
            pending["pendingChoiceMessage"] = ex.Message;
            return ActionProcessResult.Success("Card play awaiting BeforePayCost choice.", pending);
        }
        finally
        {
            context.CurrentPayCostRoot = PayCostRoot.None;
        }

        return await CompletePlayAsync(context, action, payload, validation, memoryCost, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>The play tail shared by the synchronous path and the brick-2b deferred resume: pay the
    /// (already-reduced) <paramref name="memoryCost"/>, move the card to the battle area, register its
    /// effects, and run the [All Turns] reactivation window. The BeforePayCost window has already been
    /// resolved by the caller (its reduction is folded into <paramref name="memoryCost"/>).</summary>
    private static async Task<ActionProcessResult> CompletePlayAsync(
        EngineContext context,
        LegalAction action,
        PlayCardActionPayload payload,
        PlayCardValidation validation,
        int memoryCost,
        CancellationToken cancellationToken)
    {
        HeadlessMemoryState previousMemory = context.MemoryController.Current;
        HeadlessMemoryState paidMemory = context.MemoryController.Pay(memoryCost);
        TriggerEventEmitter.Emit(context.GameEventQueue, TriggerTimings.AfterPayCost, actor: action.PlayerId, subject: payload.CardId);
        // F-1.7 / (R2-C): the fixed cost for this play is now locked in — expire the one-shot "until cost is
        // calculated" modifiers ATOMICALLY across the registry AND the payer's player bucket (AS-IS CardController
        // clears Player.UntilCalculateFixedCostEffect on play).
        EffectDurationExpiry.ExpireFixedCostCalc(context, action.PlayerId);
        // (C1d RDW-04) enrich the entry CardMoved with the AS-IS OnEnterFieldHashtable params so the DORMANT
        // SkillWindowSupply can byte-rebuild the OnEnterFieldAnyone payload at cutover. A player-initiated HAND play
        // is a non-evolution, non-jogress entry (evoRoots/oldLevels empty, isFromDigimonDigivolutionCards false,
        // cardEffect null); assemblyCount = the materials tucked at entry (AS-IS _assemblyCount). Purely additive —
        // no live consumer reads these keys before C2.
        var onEnterFieldMetadata = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [SkillWindowSupply.OnEnterFieldIsEvolutionKey] = false,
            [SkillWindowSupply.OnEnterFieldIsJogressKey] = false,
            [SkillWindowSupply.OnEnterFieldDigiXrosCountKey] = 0,
            [SkillWindowSupply.OnEnterFieldAssemblyCountKey] = payload.AssemblyMaterials.Count,
            [SkillWindowSupply.OnEnterFieldEvoRootIdsKey] = Array.Empty<string>(),
            [SkillWindowSupply.OnEnterFieldOldLevelsKey] = Array.Empty<int>(),
            [SkillWindowSupply.OnEnterFieldIsFromDigimonDigivolutionCardsKey] = false,
        };
        ZoneMoveResult movement = await context.ZoneMover.MoveAsync(
            new ZoneMoveRequest(
                action.PlayerId,
                payload.CardId,
                payload.FromZone,
                payload.ToZone,
                Metadata: onEnterFieldMetadata),
            cancellationToken).ConfigureAwait(false);

        // N-1 (summoning sickness): a freshly-played permanent entered the field this turn and cannot
        // attack until its controller's next turn unless it has Rush. This mirrors the original
        // CardController setting Permanent.EnterFieldTurnCount = TurnCount on a newly played permanent
        // (CardController.cs:1386). Digivolve/breeding-move keep the existing permanent and so inherit
        // their prior status instead (see DigivolveAction / the breeding flow). The flag is cleared at
        // the controller's Unsuspend step (HeadlessEarlyPhaseFlow).
        MarkEnteredThisTurn(context, payload.CardId);

        // G6-001: auto-register the played card's ported effects (no-op for un-ported cards).
        CardEffectRegistrar.RegisterCard(context, payload.CardId, action.PlayerId);

        // (AD1-A) Assembly: move the selected materials from the OWNER'S TRASH to UNDER the new permanent as
        // digivolution cards (AS-IS AddDigivolutiuonCards -> AddDigivolutionCardsBottom, SelectAssemblyClass
        // .cs:282-311) — done after entry, before the On-Play windows (CardController.cs:1630-1649 order).
        // A material an entry effect already consumed is skipped (the AS-IS isTrashCard guard).
        if (payload.AssemblyMaterials.Count > 0 && context.ZoneMover is IZoneStateReader assemblyZones)
        {
            List<HeadlessEntityId> stillInTrash = payload.AssemblyMaterials
                .Where(id => assemblyZones.GetCards(action.PlayerId, ChoiceZone.Trash).Contains(id))
                .ToList();
            await DigivolutionStackHelpers.AddSourcesBottomAsync(
                context.CardInstanceRepository, context.ZoneMover, payload.CardId, stillInTrash, ChoiceZone.Trash,
                cancellationToken, context: context).ConfigureAwait(false);
        }

        Dictionary<string, object?> metadata = Metadata(action, payload, validation);
        metadata[HeadlessActionParameterKeys.PreviousMemory] = previousMemory.Current;
        metadata[HeadlessActionParameterKeys.Memory] = paidMemory.Current;
        metadata["movementEventSequence"] = movement.Event.Sequence;
        metadata["cardDefinitionId"] = validation.CardDefinitionId?.Value;
        if (payload.AssemblyMaterials.Count > 0)
        {
            // AS-IS plumbs the material count into the OnEnterField hashtable (HashtableSetting.cs:143
            // "AssemblyCount") — no card effect reads it today, mirrored for parity.
            metadata["assemblyCount"] = payload.AssemblyMaterials.Count;
        }

        // [On Play]: resolve the just-played card's own OnEnterFieldAnyone activated effects (draw / select /
        // trash …) through the choice flow — mirrors DigivolveAction's WhenDigivolving resolution. These are
        // IActivatedCardEffect, so they are NOT auto-registered (RegisterOnEnterPlay skips them) and the trigger
        // bridge excludes OnEnterFieldAnyone (action-wired here); this is their only live resolution point.
        // No-op for a card without a ported [On Play] activated effect. A deferred agent choice suspends and
        // reports pending so the next ResolveChoice resumes it (no re-play).
        try
        {
            await ActivatedEffectResolver
                .ResolveAsync(context, payload.CardId, action.PlayerId, EffectTiming.OnEnterFieldAnyone, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (DeferredChoicePendingException ex)
        {
            context.DeferredActivations.Suspend(payload.CardId, EffectTiming.OnEnterFieldAnyone, action.PlayerId);
            metadata["pendingChoice"] = true;
            metadata["pendingChoiceMessage"] = ex.Message;
            return ActionProcessResult.Success("Card played; [On Play] awaiting choice.", metadata);
        }

        // LA-3: a Digimon entering play triggers eligible "[All Turns] (Once Per Turn) when Digimon are
        // played, activate this Digimon's [When Digivolving] effects" holders — this fires through the STANDARD
        // play window (AS-IS CardController.cs:1691 StackSkillInfos(OnEnterFieldAnyone) broadcast, delivered by
        // the C2 window pump), not a bespoke driver. EX8_074 (the only such card) inlines it at OnEnterFieldAnyone.

        return ActionProcessResult.Success("Card played.", metadata);
    }

    /// <summary>(brick 2b) Resume a play that suspended at its BeforePayCost choice. The MetadataActionProcessor
    /// resume seam has just re-resolved the suspended activation (replaying the agent's answer), so the cost
    /// reduction is now registered; finish the play at the reduced cost. The card is still in hand
    /// (commit-once) — this is the first and only time it is paid and moved.</summary>
    public static async Task<ActionProcessResult> CompleteDeferredPlayAsync(
        EngineContext context,
        HeadlessEntityId cardId,
        HeadlessPlayerId playerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!TryGetPlayCost(context, cardId, out int reducedCost, out string? error))
        {
            return ActionProcessResult.Failure(error ?? "Card play cost was not found.", new Dictionary<string, object?>());
        }

        context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? instance);
        PlayCardActionPayload payload = new(cardId, reducedCost, ChoiceZone.Hand, ChoiceZone.BattleArea);
        LegalAction action = HeadlessActionFactory.PlayCard(playerId, cardId, reducedCost);
        PlayCardValidation validation = PlayCardValidation.Legal(instance?.DefinitionId ?? cardId);
        return await CompletePlayAsync(context, action, payload, validation, reducedCost, cancellationToken).ConfigureAwait(false);
    }

    private LegalAction? CreateLegalActionIfPlayable(
        EngineContext context,
        HeadlessPlayerId playerId,
        HeadlessEntityId cardId)
    {
        if (!TryGetPlayCost(context, cardId, out int playCost, out _, checkAvailability: true))
        {
            return null;
        }

        PlayCardActionPayload payload = new(
            cardId,
            playCost,
            ChoiceZone.Hand,
            ChoiceZone.BattleArea);

        PlayCardValidation validation = Validate(context, playerId, payload);
        return validation.IsLegal
            ? HeadlessActionFactory.PlayCard(playerId, cardId, playCost)
            : null;
    }

    private static PlayCardValidation Validate(
        EngineContext context,
        HeadlessPlayerId playerId,
        PlayCardActionPayload payload)
    {
        if (playerId.IsEmpty)
        {
            return PlayCardValidation.Illegal("Player id must not be empty.");
        }

        if (payload.FromZone != ChoiceZone.Hand || payload.ToZone != ChoiceZone.BattleArea)
        {
            return PlayCardValidation.Illegal("PlayCard only supports Hand to BattleArea movement.");
        }

        if (payload.MemoryCost < 0)
        {
            return PlayCardValidation.Illegal("PlayCard memory cost must not be negative.");
        }

        if (!context.CardInstanceRepository.TryGetInstance(payload.CardId, out CardInstanceRecord? instance) ||
            instance is null)
        {
            return PlayCardValidation.Illegal($"Card instance '{payload.CardId}' was not found.");
        }

        if (instance.OwnerId != playerId)
        {
            return PlayCardValidation.Illegal(
                $"Card instance '{payload.CardId}' is owned by player '{instance.OwnerId}', not player '{playerId}'.",
                instance.DefinitionId);
        }

        if (context.ZoneMover is not IZoneStateReader zoneReader)
        {
            return PlayCardValidation.Illegal("Zone mover does not expose readable zone state.", instance.DefinitionId);
        }

        if (!zoneReader.GetCards(playerId, ChoiceZone.Hand).Contains(payload.CardId))
        {
            return PlayCardValidation.Illegal(
                $"Card instance '{payload.CardId}' is not in player '{playerId}' hand.",
                instance.DefinitionId);
        }

        if (!TryGetPlayCost(context, payload.CardId, out int repositoryCost, out string? costError, checkAvailability: true))
        {
            return PlayCardValidation.Illegal(costError ?? "Card play cost was not found.", instance.DefinitionId);
        }

        if (context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? card) &&
            card is not null &&
            card.IsCardType("Option"))
        {
            return PlayCardValidation.Illegal(
                $"Option card '{payload.CardId}' must be activated through ActivateOption.",
                instance.DefinitionId);
        }

        // (AD1-A) an Assembly play: re-derive the condition, validate the explicit material set (owner's
        // trash, per-element predicates, full set), and expect the FLAT discount (AS-IS GetPayingCost,
        // CardSource.cs:705-737: Cost -= reduceCost only when selected == elementCount).
        int expectedCost = repositoryCost;
        if (payload.AssemblyMaterials.Count > 0)
        {
            var view = new Assets.Scripts.Script.CardEffectCommons.CardSource(context, payload.CardId, playerId);
            if (view.AssemblyConditionOf() is not Assets.Scripts.Script.CardEffectCommons.AssemblyCondition condition)
            {
                return PlayCardValidation.Illegal($"Card '{payload.CardId}' has no Assembly condition.", instance.DefinitionId);
            }

            if (!Assets.Scripts.Script.SelectAssemblyClass.ValidateMaterials(context, view, condition, payload.AssemblyMaterials))
            {
                return PlayCardValidation.Illegal("Assembly materials do not satisfy the condition.", instance.DefinitionId);
            }

            expectedCost = Math.Max(0, repositoryCost - condition.reduceCost);
        }

        if (payload.MemoryCost != expectedCost)
        {
            return PlayCardValidation.Illegal(
                $"PlayCard memory cost {payload.MemoryCost} does not match card play cost {expectedCost}.",
                instance.DefinitionId);
        }

        // (EX8_074 Stage 3 brick 3 — availability; uniform-사멸 flip) The AS-IS availability pre-discount is a
        // [None] isCheckAvailability ChangeCostClass on the card itself (EX8_074 region #2), already folded into
        // `expectedCost` by TryGetPlayCost's checkAvailability:true resolve (GetPayingCostWithBaseCost) — the
        // former invented projection over the retired uniform SuspendCostReductionEffect body is deleted, so the
        // affordability check reads the folded cost directly.
        if (!context.MemoryController.CanPay(expectedCost))
        {
            return PlayCardValidation.Illegal(
                $"Cannot pay play cost {payload.MemoryCost}.",
                instance.DefinitionId);
        }

        // (G-Field RD-EXT3-03) the field-placement restriction gate. A HAND play puts a NEW permanent on an
        // empty battle-area frame, so the AS-IS empty-frame arm of CanPlayCardTargetFrame applies its
        // CanEnterField(cardEffect) scan AFTER the cost check (CardSource.cs:1163-1170) — an active
        // ICanNotPutFieldEffect (e.g. EX7_014 "opponent can't play Digimon with 6000 DP or less") forbids the
        // play. A player-initiated hand play carries no source cardEffect (AS-IS passes cardEffect=null through
        // CanPlayFromHandDuringMainPhase -> CanPutFieldThisPermanentCard(true, null)). Reuses the S3b-ported
        // CardSource.CanEnterField scan (CardSource.cs:404-449) — no new scan. Returns true for the vast
        // majority of plays (no producer registered), so the normal path is unchanged.
        var placementView = new CardSource(context, payload.CardId, playerId, instance.OwnerId);
        if (!placementView.CanEnterField(null))
        {
            return PlayCardValidation.Illegal(
                $"Card '{payload.CardId}' cannot be put onto the field (a field-placement restriction is active).",
                instance.DefinitionId);
        }

        return PlayCardValidation.Legal(instance.DefinitionId);
    }

    // (uniform-사멸 flip) BeforePayCostAvailabilityReduction DELETED — the invented availability projection
    // (it unwrapped the retired uniform SuspendCostReductionEffect body). The availability pre-discount is
    // now the AS-IS surface itself: a [None] isCheckAvailability ChangeCostClass on the card (EX8_074
    // region #2 / TfxBeforePayCost), folded into TryGetPlayCost's checkAvailability:true resolve
    // (CardSource.GetPayingCostWithBaseCost).


    private static void MarkEnteredThisTurn(EngineContext context, HeadlessEntityId cardId)
    {
        if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? instance) ||
            instance is null)
        {
            return;
        }

        Dictionary<string, object?> metadata = new(instance.Metadata, StringComparer.Ordinal)
        {
            ["enteredThisTurn"] = true
        };
        context.CardInstanceRepository.Upsert(instance with { Metadata = metadata });
    }

    private static bool TryGetPlayCost(
        EngineContext context,
        HeadlessEntityId cardId,
        out int playCost,
        out string? error,
        bool checkAvailability = false)
    {
        playCost = default;
        if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? instance) ||
            instance is null)
        {
            error = $"Card instance '{cardId}' was not found.";
            return false;
        }

        if (!context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? card) || card is null)
        {
            error = $"Card definition '{instance.DefinitionId}' was not found.";
            return false;
        }

        if (!PlayCostHelpers.TryResolveCost(card, instance, out int baseCost, out error))
        {
            return false;
        }

        // (R2-C) fold the play-cost pipeline through the single AS-IS orchestrator CardSource.GetPayingCostWithBaseCost
        // (DigiXros/Assembly, the IChangeCostEffect fold, the legacy substrate union, the 0 floor). Root.Hand — a
        // top-level play is from hand (effect-driven plays from other zones go through PlayPermanentCards, which
        // threads the real source root). Static (card/instance metadata) cost is the base.
        playCost = new CardSource(context, cardId, instance.OwnerId)
            // (EXEMPLAR-T1) checkAvailability threads the AS-IS split: the LANE/legality check mirrors
            // CanSelect/CanPutFieldThisPermanentCard's `PayingCost(..., checkAvailability: true)`
            // (CardSource.cs:143/1151) — an availability-only IChangeCostEffect (IsCheckAvailability()==true,
            // e.g. P_223's hidden "Play Cost -4") participates there; the PAY-time re-resolve keeps false
            // (AS-IS pays through the until-calc registration the BeforePayCost activation added, not the
            // availability-only class).
            .GetPayingCostWithBaseCost(baseCost, Assets.Scripts.Script.SelectCardEffect.Root.Hand, targetPermanents: null, checkAvailability: checkAvailability);
        error = null;
        return true;
    }

    private static Dictionary<string, object?> Metadata(
        LegalAction action,
        PlayCardActionPayload payload,
        PlayCardValidation validation)
    {
        Dictionary<string, object?> metadata = BaseMetadata(action);
        metadata[HeadlessActionParameterKeys.CardId] = payload.CardId.Value;
        metadata[HeadlessActionParameterKeys.MemoryCost] = payload.MemoryCost;
        metadata[HeadlessActionParameterKeys.FromZone] = payload.FromZone.ToString();
        metadata[HeadlessActionParameterKeys.ToZone] = payload.ToZone.ToString();
        metadata["cardDefinitionId"] = validation.CardDefinitionId?.Value;
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

public sealed record PlayCardActionPayload(
    HeadlessEntityId CardId,
    int MemoryCost,
    ChoiceZone FromZone,
    ChoiceZone ToZone)
{
    /// <summary>(AD1-A) parameter key carrying the Assembly material ids (comma-joined, element order).</summary>
    public const string AssemblyMaterialsKey = "assemblyMaterials";

    /// <summary>(AD1-A) the Assembly materials this play consumes from the OWNER'S TRASH (empty = a normal
    /// play). AS-IS folds Assembly into the ordinary play flow (CardController.cs:753) — headless it is the
    /// same PlayCard action parameterized with the chosen full material set.</summary>
    public IReadOnlyList<HeadlessEntityId> AssemblyMaterials { get; init; } = Array.Empty<HeadlessEntityId>();

    public IReadOnlyDictionary<string, object?> ToParameters()
    {
        var parameters = new Dictionary<string, object?>
        {
            [HeadlessActionParameterKeys.CardId] = CardId,
            [HeadlessActionParameterKeys.MemoryCost] = MemoryCost,
            [HeadlessActionParameterKeys.FromZone] = FromZone,
            [HeadlessActionParameterKeys.ToZone] = ToZone
        };
        if (AssemblyMaterials.Count > 0)
        {
            parameters[AssemblyMaterialsKey] = string.Join(",", AssemblyMaterials.Select(m => m.Value));
        }

        return parameters;
    }

    public static bool TryRead(
        LegalAction action,
        [NotNullWhen(true)] out PlayCardActionPayload? payload,
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

        if (!TryReadInt(action.Parameters, HeadlessActionParameterKeys.MemoryCost, out int memoryCost))
        {
            payload = null;
            error = $"Missing action parameter: {HeadlessActionParameterKeys.MemoryCost}.";
            return false;
        }

        ChoiceZone fromZone = HeadlessActionPayloadReader.ReadZoneOrDefault(
            action,
            HeadlessActionParameterKeys.FromZone,
            ChoiceZone.Hand);
        ChoiceZone toZone = HeadlessActionPayloadReader.ReadZoneOrDefault(
            action,
            HeadlessActionParameterKeys.ToZone,
            ChoiceZone.BattleArea);

        IReadOnlyList<HeadlessEntityId> assemblyMaterials = Array.Empty<HeadlessEntityId>();
        if (action.Parameters.TryGetValue(AssemblyMaterialsKey, out object? rawMaterials) &&
            rawMaterials?.ToString() is { Length: > 0 } materialsValue)
        {
            assemblyMaterials = materialsValue
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(id => new HeadlessEntityId(id))
                .ToArray();
        }

        payload = new PlayCardActionPayload(cardId, memoryCost, fromZone, toZone) { AssemblyMaterials = assemblyMaterials };
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

public sealed record PlayCardValidation(
    bool IsLegal,
    string Reason,
    HeadlessEntityId? CardDefinitionId)
{
    public static PlayCardValidation Legal(HeadlessEntityId cardDefinitionId)
    {
        return new PlayCardValidation(true, string.Empty, cardDefinitionId);
    }

    public static PlayCardValidation Illegal(
        string reason,
        HeadlessEntityId? cardDefinitionId = null)
    {
        return new PlayCardValidation(false, reason ?? string.Empty, cardDefinitionId);
    }
}
