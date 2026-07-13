namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
// Aliased (not a namespace import) to avoid pulling the sibling `...Script.CardEffectFactory` namespace
// into scope, which would clash with the CardEffectFactory type below.
using SelectPermanentEffect = HeadlessDCGO.Engine.Assets.Scripts.Script.SelectPermanentEffect;
using PartitionCondition = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory.KeyWordEffects.PartitionCondition;


/// <summary>A read-only view of the permanent (digivolution stack) a card belongs to — the headless
/// stand-in for the original <c>Permanent</c> accessed via <c>CardSource.PermanentOfThisCard()</c>.</summary>
public sealed class PermanentView
{
    public PermanentView(DigivolutionStack stack)
    {
        ArgumentNullException.ThrowIfNull(stack);
        Stack = stack;
    }

    public DigivolutionStack Stack { get; }

    /// <summary>The under-cards (digivolution sources) of the permanent — mirrors
    /// <c>Permanent.DigivolutionCards</c>. <c>.Count</c> is the source count.</summary>
    public IReadOnlyList<StackedCard> DigivolutionCards => Stack.UnderCards;

    public bool IsEmpty => Stack.IsEmpty;

    /// <summary>The top card's instance id (the battling Digimon) — mirrors <c>Permanent.TopCard</c>.</summary>
    public HeadlessEntityId TopInstanceId => Stack.Cards.Count > 0 ? Stack.Cards[^1].InstanceId : default;
}


/// <summary>Minimal headless mirror of the original <c>Permanent</c> — used only for the signature of
/// card <c>permanentCondition</c> predicates. Player-scope effects scope to the owner's cards directly, so
/// the predicate body is not invoked by the headless evaluation (it exists for 1:1 source fidelity).</summary>
/// <summary>(PRIM-W5-0) A battle-area permanent view — the member surface card predicates read off
/// <c>permanent.*</c>. Backed by the engine: <see cref="TopCard"/> reuses <see cref="CardSource"/> for the
/// card-view members, DP folds continuous modifiers, and digivolution sources come from the stack.</summary>
public sealed class Permanent
{
    private readonly EngineContext _context;

    public Permanent(EngineContext context, HeadlessEntityId instanceId, HeadlessPlayerId ownerId, ChoiceZone? snapshotZone = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        InstanceId = instanceId;
        OwnerId = ownerId;
        SnapshotZone = snapshotZone;
    }

    public HeadlessEntityId InstanceId { get; }

    public HeadlessPlayerId OwnerId { get; }

    /// <summary>(D-2) The PRE-removal field zone of a card that has ALREADY left the field, captured from the
    /// driving leave event's <c>ZoneFrom</c>. Non-null only on the transient subject view an OnLeaveFieldAnyone
    /// gate builds (<see cref="CardEffectCommons.CanTriggerOnPermanentLeave"/>): the AS-IS leave batch is stacked
    /// while the leaving permanent is STILL on the battle area (CardController.cs:3748, before RemoveField), so its
    /// <c>IsPermanentExistsOnOpponentBattleAreaDigimon</c> gate reads TRUE — but headless has already moved the
    /// card to the trash by collect time, so a LIVE zone read would read FALSE. When set, the field-membership
    /// checks (<see cref="CardEffectCommons.IsPermanentExistsOnBattleArea"/> /
    /// <see cref="CardEffectCommons.IsPermanentExistsOnBreedingArea"/>) answer from this snapshot instead of the
    /// live zone, reproducing the AS-IS pre-removal truth. Null for every normally-constructed permanent, so no
    /// other gate is affected.</summary>
    public ChoiceZone? SnapshotZone { get; }

    /// <summary>The top (battling) card of this permanent as a <see cref="CardSource"/>.</summary>
    public CardSource TopCard => new(_context, InstanceId, OwnerId);

    /// <summary>(MIG2) AS-IS <c>Permanent.HasDP</c> (Permanent.cs:146-189): only a (treated-as) Digimon has DP,
    /// and a Digi-Egg without printed DP has none. The <c>IDontHaveDPEffect</c> scan (:166-185) has no headless
    /// producer yet — design item MIG2-DONTHAVEDP.</summary>
    public bool HasDP
    {
        get
        {
            if (!IsDigimon)
            {
                return false;
            }

            if (!TopCard.HasDP && TopCard.IsDigiEgg)
            {
                return false;
            }

            return true;
        }
    }

    /// <summary>(MIG2 substrate guard) Whether ANY dp value is defined for this permanent (instance or printed).
    /// AS-IS real Digimon always print DP, so its DP-rule predicates never meet a DP-less Digimon; headless
    /// abstract fixtures do — the D-2 sweep decision ("only when DP is actually DEFINED") is preserved by
    /// gating the rule predicates on this.</summary>
    public bool IsDpDefined =>
        (_context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? i) && i is not null
            && i.Metadata.TryGetValue("dp", out object? raw) && raw is int)
        || TopCard.HasDP;

    /// <summary>(MIG2) AS-IS <c>Permanent.DP</c> (Permanent.cs:499-692): -1 when the permanent has no DP at all
    /// (<see cref="HasDP"/> false — the <c>IsNotHavingDP</c> rule marker); a defined DP folds continuous
    /// modifiers and clamps at 0 (:686-689).</summary>
    public int DP
    {
        get
        {
            if (!HasDP)
            {
                return -1;
            }

            int resolved = ContinuousDpGate.ResolveDp(_context, InstanceId, BaseDp());
            return resolved < 0 ? 0 : resolved;
        }
    }

    /// <summary>(A3) Mirror of <c>Permanent.Level</c> (Permanent.cs:48-102): seeds from the top card's
    /// (already card-level-folded) level, then EVERY active <see cref="CardEffects.ChangePermanentLevelClass"/>
    /// effect transforms it (AS-IS scans all field permanents' + players' effects — the registry's active
    /// bindings are that set).</summary>
    public int Level
    {
        get
        {
            int level = TopCard.Level;
            foreach (Headless.Effects.EffectRequest effect in _context.EffectRegistry.GetContinuousEffects(
                new Headless.Services.EffectQueryContext(Headless.Runtime.ContinuousRestrictionGate.Scope)))
            {
                if (effect.Context.Values.TryGetValue(CardEffects.ChangePermanentLevelClass.GetPermanentLevelKey, out object? raw)
                    && raw is Func<Permanent, int, int> transform
                    && CardSource.EffectConditionPasses(effect))
                {
                    level = transform(this, level);
                }
            }

            return level;
        }
    }

    public bool HasNoDigivolutionCards => DigivolutionCards.Count == 0;

    /// <summary>(MIG2) AS-IS <c>Permanent.IsDigimon</c> (Permanent.cs:3438-3511) via the K4 chokepoint:
    /// face-down is never a Digimon; printed Digimon OR Digi-Egg is; else the live TreatAsDigimon
    /// (<c>ITreatAsDigimonEffect</c>) keyword scan decides.</summary>
    public bool IsDigimon => ContinuousKeywordGate.IsDigimon(_context, InstanceId);

    /// <summary>(MIG2) AS-IS <c>Permanent.IsTamer</c> (Permanent.cs:3515-3532): a face-down top is not a Tamer.</summary>
    public bool IsTamer => !TopCard.IsFlipped && TopCard.IsTamer;

    public bool IsToken => TopCard.IsToken;

    public bool IsSuspended =>
        _context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? i) && i is not null
        && i.Metadata.TryGetValue("isSuspended", out object? raw) && raw is bool b && b;

    /// <summary>The digivolution (under-)cards of this permanent (mirror of <c>DigivolutionCards</c>).</summary>
    public IReadOnlyList<CardSource> DigivolutionCards
    {
        get
        {
            DigivolutionStack stack = DigivolutionStackReader.Read(_context.CardInstanceRepository, _context.CardRepository, InstanceId);
            return stack.UnderCards.Select(u => new CardSource(_context, u.InstanceId, OwnerId)).ToArray();
        }
    }

    private int BaseDp() =>
        _context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? i) && i is not null
        && i.Metadata.TryGetValue("dp", out object? raw) && raw is int dp ? dp : 0;

    /// <summary>(W6-P) mirror of AS-IS <c>Permanent.BaseDP</c> — the unmodified DP (IsMinDP/IsMaxDP read it).</summary>
    public int BaseDP => BaseDp();

    // ===== (MIG2) link / rule-process members (AS-IS Permanent.cs) =============================================

    /// <summary>(MIG2) AS-IS <c>Permanent.LinkedCards</c> (Permanent.cs:1041) as live views (newest first —
    /// the substrate list mirrors the AS-IS insert-at-0 ordering).</summary>
    public List<CardSource> LinkedCards
    {
        get
        {
            if (!_context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? host) || host is null)
            {
                return new List<CardSource>();
            }

            return LinkHelpers.ReadLinkedCardIds(host.Metadata)
                .Select(id => new CardSource(
                    _context,
                    id,
                    _context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? link) && link is not null
                        ? link.OwnerId
                        : OwnerId))
                .ToList();
        }
    }

    /// <summary>(MIG2) AS-IS <c>Permanent.LinkedMax</c> (Permanent.cs:896): base 1 folded with active
    /// <c>IChangeLinkMaxEffect</c>s (the M-4 continuous linkedMaxDelta fold).</summary>
    public int LinkedMax => LinkHelpers.ResolveLinkedMax(_context, InstanceId);

    /// <summary>(MIG2) AS-IS <c>Permanent.HasNoLinkCards</c> (Permanent.cs:3958).</summary>
    public bool HasNoLinkCards => LinkedCards.Count == 0;

    /// <summary>(MIG2) AS-IS <c>Permanent.IsPlaceToTrashDueToNotHavingDP</c> (Permanent.cs:3694, default true;
    /// effects may clear the flag).</summary>
    public bool IsPlaceToTrashDueToNotHavingDP =>
        !(_context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? i) && i is not null
            && i.Metadata.TryGetValue(GameFlowProcessor.PlaceToTrashDueToNoDpKey, out object? optOut) && optOut is false);

    /// <summary>(MIG2) AS-IS <c>Permanent.IsPlayedOptionPermanent</c> (Permanent.cs:3946, default false — an
    /// Option a card effect legitimately keeps on the battle area).</summary>
    public bool IsPlayedOptionPermanent =>
        _context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? p) && p is not null
            && p.Metadata.TryGetValue(GameFlowProcessor.IsPlayedOptionPermanentKey, out object? played) && played is true;

    /// <summary>(MIG2) AS-IS <c>Permanent.CanBeDestroyed()</c> (Permanent.cs:3186-3229): no active
    /// <c>ICanNotBeDestroyedEffect</c> protects this permanent — the same Delete/Prevent replacement set the
    /// mutation sink consults (<c>IsDeletionPreventedByContinuous</c>), evaluated predicate-side so the DP-0
    /// rule never re-selects a protected Digimon.</summary>
    public bool CanBeDestroyed()
    {
        ContinuousEvaluationResult result = ContinuousScopeEvaluation.EvaluateForCard(
            _context, ContinuousRestrictionGate.Scope, InstanceId);
        foreach (ReplacementEffect replacement in result.Replacements)
        {
            if (replacement.EventKind == ReplacementEventKind.Delete && replacement.ActionKind == ReplacementActionKind.Prevent)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>(MIG2) AS-IS <c>Permanent.RemoveLinkedCard(cardSource, removeCount, trashCard)</c>
    /// (Permanent.cs:1306-1348). A direct removal does NOT open the OnLinkCardDiscarded window — the batch
    /// window is <see cref="ITrashLinkCards"/>' job (CardController.cs:5314). With <paramref name="removeCount"/>
    /// &gt; 0 the OWNER SELECTS which link cards to trash (AS-IS SelectCardEffect, mode Discard, root Custom =
    /// LinkedCards, canEndNotMax:false): the substrate opens the card choice and parks (request-id prefix
    /// <see cref="AutoProcessing.LinkTrimRequestIdPrefix"/>); MetadataActionProcessor routes each pick through
    /// ITrashLinkCards — the AS-IS Mode.Discard linked-card branch (SelectCardEffect.cs:715-724).</summary>
    public async Task RemoveLinkedCard(CardSource? cardSource, int removeCount = 0, bool trashCard = true, CancellationToken cancellationToken = default)
    {
        if (cardSource is not null && LinkedCards.Any(linked => linked.InstanceId == cardSource.InstanceId))
        {
            await LinkHelpers.RemoveLinkCardAsync(
                _context.CardInstanceRepository, _context.ZoneMover, InstanceId, cardSource.InstanceId,
                trash: trashCard, gameEventQueue: null, cancellationToken).ConfigureAwait(false);
        }

        if (removeCount > 0)
        {
            List<CardSource> linked = LinkedCards;
            int maxCount = Math.Min(removeCount, linked.Count);
            if (maxCount <= 0)
            {
                return;
            }

            ChoiceCandidate[] candidates = linked
                .Select(card => EffectChoiceHelpers.Candidate(
                    card.InstanceId, card.InstanceId.Value, Headless.Choices.ChoiceZone.LinkedCards, isSelectable: true, OwnerId))
                .ToArray();
            ChoiceRequest request = EffectChoiceHelpers.CreateCardRequest(
                OwnerId,
                $"Select {maxCount} card to trash.",
                maxCount,
                maxCount,
                canSkip: false,
                Headless.Choices.ChoiceZone.LinkedCards,
                candidates);
            _context.ChoiceController.RequestChoice(
                request,
                new HeadlessEntityId($"{Assets.Scripts.Script.AutoProcessing.LinkTrimRequestIdPrefix}{InstanceId.Value}"));
        }
    }

    // ===== (MIG4 goal-4 slice 1) AS-IS Permanent instance-method surface — the AS-IS methods card ports call
    // (permanent.DiscardEvoRoots() / AddDigivolutionCardsTop() / AddLinkCard() …), each delegating to the
    // verified headless helper so a local-LLM card port is a mechanical mirror. No current caller — an additive
    // AS-IS surface; unsupported AS-IS branches throw with a design item rather than fabricate behavior.

    /// <summary>(MIG4) AS-IS <c>Permanent.DiscardEvoRoots(ignoreOverflow, putToTrash)</c> (Permanent.cs:106-142):
    /// trash this permanent's digivolution sources AND link cards, applying the ACE-Overflow penalty to both
    /// first (unless <paramref name="ignoreOverflow"/>). Delegates to
    /// <see cref="DeletionSourceTrash.TrashEvoSourcesAsync"/> (the putToTrash==true path every headless deletion
    /// call site uses, always gameEventQueue:null — AS-IS's own trash-add is direct, no OnDigivolutionCardDiscarded).
    /// The AS-IS <c>putToTrash == false</c> RETURN variant has no headless bare-detach primitive — design item
    /// MIG4-DISCARDEVOROOTS-PUTTOTRASH.</summary>
    public async Task DiscardEvoRoots(bool ignoreOverflow = false, bool putToTrash = true, CancellationToken cancellationToken = default)
    {
        if (!putToTrash)
        {
            throw new NotSupportedException(
                "Permanent.DiscardEvoRoots(putToTrash: false) has no headless primitive yet — design item MIG4-DISCARDEVOROOTS-PUTTOTRASH.");
        }

        await DeletionSourceTrash.TrashEvoSourcesAsync(
            _context.CardInstanceRepository,
            _context.ZoneMover,
            InstanceId,
            gameEventQueue: null,
            cancellationToken: cancellationToken,
            memory: _context.MemoryController,
            turnPlayer: _context.TurnController.Current.TurnPlayerId,
            ignoreOverflow: ignoreOverflow).ConfigureAwait(false);
    }

    /// <summary>(MIG4) AS-IS <c>Permanent.AddDigivolutionCardsTop(added, cardEffect)</c> (Permanent.cs:1064-1123):
    /// move each card off its current zone and insert it just under the top card. AS-IS per-card
    /// <c>cardSources.Insert(1, ...)</c> REVERSES the batch's relative order under the top (last processed ends
    /// up highest) — replicated by reversing before <see cref="DigivolutionStackHelpers.AddSourcesTopAsync"/>'s
    /// single ordered prepend. The <c>!this.IsToken &amp;&amp; !card.IsToken</c> guard (:1088) is preserved (a token
    /// host/card is still pulled off its zone but never attached). AS-IS fires ONE OnAddDigivolutionCards for the
    /// whole batch; a batch spanning &gt;1 live zone splits into one emit per zone group — design item
    /// MIG4-ADDDIGI-MULTIZONE-EMIT.</summary>
    public async Task AddDigivolutionCardsTop(
        IReadOnlyList<CardSource> addedDigivolutionCards,
        HeadlessEntityId? causeEffectSourceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(addedDigivolutionCards);
        if (addedDigivolutionCards.Count == 0)
        {
            return;
        }

        bool hostIsToken = this.IsToken;
        var attachable = new List<HeadlessEntityId>();
        foreach (CardSource card in addedDigivolutionCards)
        {
            await DetachEmbeddedSourceOrLinkAsync(card, cancellationToken).ConfigureAwait(false);

            if (!hostIsToken && !card.IsToken)
            {
                attachable.Add(card.InstanceId);
            }
            else
            {
                await WithdrawToNoneAsync(card, cancellationToken).ConfigureAwait(false);
            }
        }

        attachable.Reverse();

        foreach (IGrouping<Headless.Choices.ChoiceZone, HeadlessEntityId> group in attachable.GroupBy(id => CurrentZoneOf(OwnerId, id)))
        {
            await DigivolutionStackHelpers.AddSourcesTopAsync(
                _context.CardInstanceRepository,
                _context.ZoneMover,
                InstanceId,
                group.ToArray(),
                group.Key,
                cancellationToken: cancellationToken,
                onceFlags: _context.OnceFlags,
                gameEventQueue: _context.GameEventQueue,
                causeSourceId: causeEffectSourceId ?? default).ConfigureAwait(false);
        }
    }

    /// <summary>(MIG4) AS-IS <c>Permanent.AddDigivolutionCardsBottom(added, cardEffect, skipEffectAndActivateSkill,
    /// isFacedown)</c> (Permanent.cs:1133-1227): move each card off its current zone and append it to the bottom
    /// of the stack (loop-order append, no reversal — unlike Top). Same token guard and multi-zone-emit design
    /// item as Top. AS-IS <c>isFacedown</c> (SetReverse the buried source) has no headless AddSources face write
    /// — design item MIG4-ADDDIGI-FACEDOWN.</summary>
    public async Task AddDigivolutionCardsBottom(
        IReadOnlyList<CardSource> addedDigivolutionCards,
        HeadlessEntityId? causeEffectSourceId,
        bool skipEffectAndActivateSkill = false,
        bool isFacedown = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(addedDigivolutionCards);
        if (addedDigivolutionCards.Count == 0)
        {
            return;
        }

        if (isFacedown)
        {
            throw new NotSupportedException(
                "Permanent.AddDigivolutionCardsBottom(isFacedown: true) has no headless primitive yet — design item MIG4-ADDDIGI-FACEDOWN.");
        }

        bool hostIsToken = this.IsToken;
        var toAttach = new List<HeadlessEntityId>();
        foreach (CardSource card in addedDigivolutionCards)
        {
            await DetachEmbeddedSourceOrLinkAsync(card, cancellationToken).ConfigureAwait(false);

            if (!hostIsToken && !card.IsToken)
            {
                toAttach.Add(card.InstanceId);
            }
            else
            {
                await WithdrawToNoneAsync(card, cancellationToken).ConfigureAwait(false);
            }
        }

        foreach (IGrouping<Headless.Choices.ChoiceZone, HeadlessEntityId> group in toAttach.GroupBy(id => CurrentZoneOf(OwnerId, id)))
        {
            await DigivolutionStackHelpers.AddSourcesBottomAsync(
                _context.CardInstanceRepository,
                _context.ZoneMover,
                InstanceId,
                group.ToArray(),
                group.Key,
                cancellationToken: cancellationToken,
                onceFlags: _context.OnceFlags,
                gameEventQueue: _context.GameEventQueue,
                causeSourceId: causeEffectSourceId ?? default,
                skipEffectAndActivateSkill: skipEffectAndActivateSkill).ConfigureAwait(false);
        }
    }

    /// <summary>(MIG4) AS-IS <c>Permanent.AddLinkCard(addedLinkCard, cardEffect)</c> (Permanent.cs:1237-1294):
    /// attach a link card to this permanent. Delegates to <see cref="LinkHelpers.AddLinkCardAsync"/> (excess-trim
    /// + attach + WhenLinked emit; its LinkedMax&gt;1 owner-selection is pre-existing design item MIG2-ADDLINK-SELECT).
    /// The <c>!this.IsToken &amp;&amp; !addedLinkCard.IsToken</c> guard (:1261) is preserved.</summary>
    public async Task AddLinkCard(
        CardSource addedLinkCard,
        HeadlessEntityId? causeEffectSourceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(addedLinkCard);
        _ = causeEffectSourceId; // AS-IS AddLinkCard's cardEffect is unused past the (stripped) UI — kept for surface parity.

        await DetachEmbeddedSourceOrLinkAsync(addedLinkCard, cancellationToken).ConfigureAwait(false);

        if (this.IsToken || addedLinkCard.IsToken)
        {
            await WithdrawToNoneAsync(addedLinkCard, cancellationToken).ConfigureAwait(false);
            return;
        }

        Headless.Choices.ChoiceZone fromZone = CurrentZoneOf(addedLinkCard.Owner, addedLinkCard.InstanceId);
        await LinkHelpers.AddLinkCardAsync(
            _context.CardInstanceRepository,
            _context.ZoneMover,
            InstanceId,
            addedLinkCard.InstanceId,
            fromZone,
            gameEventQueue: _context.GameEventQueue,
            cancellationToken: cancellationToken,
            context: _context).ConfigureAwait(false);
    }

    /// <summary>(MIG4) AS-IS <c>Permanent.RemoveCardSource(cardSource)</c> (Permanent.cs:1297-1302): a bare
    /// removal from this permanent's stack list (AS-IS `cardSources.Remove(cardSource)`) — NO zone move, NO
    /// trash/trigger. Delegates to <see cref="DigivolutionStackHelpers.PlaySpecificSourceAsync"/> with
    /// <c>destination: ChoiceZone.None</c> (its documented detach-only mode: remove from sourceIds, skip the
    /// physical move). No-ops silently if the card is not one of this permanent's sources (List.Remove parity).</summary>
    public async Task RemoveCardSource(CardSource cardSource, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cardSource);
        await DigivolutionStackHelpers.PlaySpecificSourceAsync(
            _context.CardInstanceRepository,
            _context.ZoneMover,
            InstanceId,
            cardSource.InstanceId,
            Headless.Choices.ChoiceZone.None,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>(MIG4) AS-IS <c>CardObjectController.RemoveFromAllArea</c> (CardObjectController.cs:370-404) the
    /// add-methods' shared pre-step: scan every permanent, and if the card is embedded there detach it (a link
    /// via RemoveLinkedCard(trashCard:false), a buried source via the bare RemoveCardSource) BEFORE the physical
    /// zone move the helper performs. AS-IS also strips a permanent's OWN live top out of its stack (re-parent /
    /// demote) when the added card is currently some permanent's battling top — an identity-corrupting edge the
    /// headless model (permanent id == top identity) cannot express — design item MIG4-DETACH-LIVE-TOP (throws).</summary>
    private async Task DetachEmbeddedSourceOrLinkAsync(CardSource card, CancellationToken cancellationToken)
    {
        PermanentView host = card.PermanentOfThisCard();
        if (host.IsEmpty)
        {
            return;
        }

        if (host.TopInstanceId == card.InstanceId)
        {
            throw new NotSupportedException(
                $"'{card.InstanceId.Value}' is currently a permanent's own live top card — no headless primitive " +
                "re-parents/demotes it (AS-IS IPlacePermanentToDigivolutionCards / RemoveDigivolveRootEffect) — " +
                "design item MIG4-DETACH-LIVE-TOP.");
        }

        if (_context.CardInstanceRepository.TryGetInstance(host.TopInstanceId, out CardInstanceRecord? hostRecord) && hostRecord is not null
            && LinkHelpers.ReadLinkedCardIds(hostRecord.Metadata).Contains(card.InstanceId))
        {
            await LinkHelpers.RemoveLinkCardAsync(
                _context.CardInstanceRepository,
                _context.ZoneMover,
                host.TopInstanceId,
                card.InstanceId,
                trash: false,
                gameEventQueue: null,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return;
        }

        await DigivolutionStackHelpers.PlaySpecificSourceAsync(
            _context.CardInstanceRepository,
            _context.ZoneMover,
            host.TopInstanceId,
            card.InstanceId,
            Headless.Choices.ChoiceZone.None,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>AS-IS RemoveFromAllArea's unconditional physical-zone withdrawal: pull a card out of whatever
    /// concrete zone it currently sits in, even when the caller ultimately does not attach it (the token
    /// guards still ran the withdrawal before deciding not to attach).</summary>
    private async Task WithdrawToNoneAsync(CardSource card, CancellationToken cancellationToken)
    {
        Headless.Choices.ChoiceZone from = CurrentZoneOf(card.Owner, card.InstanceId);
        if (from != Headless.Choices.ChoiceZone.None)
        {
            await _context.ZoneMover.MoveAsync(
                new ZoneMoveRequest(card.Owner, card.InstanceId, from, Headless.Choices.ChoiceZone.None),
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>The concrete zone <paramref name="cardId"/> currently sits in for <paramref name="owner"/> (or
    /// <see cref="Headless.Choices.ChoiceZone.None"/> if none) — the live fromZone a card's unknown AS-IS origin
    /// needs.</summary>
    private Headless.Choices.ChoiceZone CurrentZoneOf(HeadlessPlayerId owner, HeadlessEntityId cardId)
    {
        var zones = (IZoneStateReader)_context.ZoneMover;
        foreach (KeyValuePair<Headless.Choices.ChoiceZone, IReadOnlyList<HeadlessEntityId>> pair in zones.Snapshot(owner))
        {
            if (pair.Value.Contains(cardId))
            {
                return pair.Key;
            }
        }

        return Headless.Choices.ChoiceZone.None;
    }
}

