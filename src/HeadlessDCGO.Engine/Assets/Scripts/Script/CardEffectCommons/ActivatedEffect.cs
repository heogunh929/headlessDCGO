// Uniform activated effect — a 1:1 mirror of the AS-IS ActivateClass (see
// docs/audit/uniform_activated_primitive_design.md). ONE effect type parameterised by
// (timing, CanUse gate, CanActivate precondition, composable body, once-per-turn, isOptional) replaces the
// fragmented per-(action x timing) factory set that caused the combinatorial primitive-gap explosion.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>The composable effect body — mirrors the AS-IS ActivateCoroutine. Non-interactive bodies emit a
/// mutation in <see cref="Apply"/>; interactive bodies surface a choice via <see cref="BuildRequest"/> then act
/// on the answer in <see cref="Apply"/>.</summary>
public interface IEffectBody
{
    bool IsInteractive { get; }

    ChoiceRequest? BuildRequest(CardSource card, IReadOnlyList<HeadlessPlayerId> players);

    void Apply(CardSource card, MatchStateMutationSink sink, IReadOnlyList<HeadlessEntityId> selected);

    /// <summary>(B-5) Apply the body, allowing awaited work. The default just runs the synchronous
    /// <see cref="Apply"/> — almost every body is synchronous. A body whose AS-IS coroutine has an awaited
    /// step BEFORE/around its sink mutation (e.g. <c>DiscardEvoRoots</c> before a bounce, a ZoneMover move)
    /// overrides this; the resolver always drives the body through <see cref="ApplyAsync"/>.</summary>
    ValueTask ApplyAsync(
        CardSource card, MatchStateMutationSink sink, IReadOnlyList<HeadlessEntityId> selected, CancellationToken cancellationToken)
    {
        Apply(card, sink, selected);
        return ValueTask.CompletedTask;
    }
}

/// <summary>&lt;Draw N&gt; (AS-IS DrawClass.Draw). Non-interactive.</summary>
public sealed class DrawBody : IEffectBody
{
    private readonly int _count;

    public DrawBody(int count) => _count = count;

    public bool IsInteractive => false;

    public ChoiceRequest? BuildRequest(CardSource card, IReadOnlyList<HeadlessPlayerId> players) => null;

    public void Apply(CardSource card, MatchStateMutationSink sink, IReadOnlyList<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(sink);
        if (_count <= 0)
        {
            return; // AS-IS: `if (_drawCount <= 0) yield break;`
        }

        sink.Apply(new EffectMutation(
            MatchStateMutationSink.DrawCardsKind,
            card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [MatchStateMutationSink.PlayerIdKey] = card.Owner,
                [MatchStateMutationSink.CountKey] = _count,
            }));
    }
}

// (R6-Db) NINE consumer-0 IEffectBody bodies DELETED — CompositeBody, RecoveryBody, TrashSecurityBody,
// SuspendSelfAndGainMemoryBody, GrantPlayerScopeRestrictionBody, MemoryCostThenUnsuspendSelfBody,
// ReturnTopSecurityToHandThenUnsuspendSelfBody, SuspendSelfCostThenBody, ApplyToAllMatchingBody. Each reached
// 0 producers (whole-word census, src+tests, --binary-files=text): their former cards were all re-ported to
// literal AS-IS inline `ActivateClass` coroutines (BT8_057→TrashSecurity, BT9_043→ReturnTopSecurity,
// BT16_025→ApplyToAllMatching, BT1_081→MemoryCostThenUnsuspend, BT1_086→SuspendSelfCostThen,
// BT1_109→GrantPlayerScopeRestriction; CompositeBody/RecoveryBody/SuspendSelfAndGainMemory had none left),
// leaving only self-definitions + historical doc-comment mentions. Retirement-guard step 3 (consumer-0 →
// immediate deletion). The uniform survival core (ActivatedEffect + ActivatedSelectEffect + SelfToHandBody +
// the fixture-consumed DrawBody/MemoryBody/SelectTrashHandThenSelfMutationBody/GrantContinuousBody/SelectBody)
// is untouched — it stays for EX8_074 (RD-R6-07/R2-C STOP), ST4_15 (security-reuse afterMainBody), and the Tfx
// uniform-model fixtures (pending Tfx retirement).

/// <summary>Gain / lose N memory (AS-IS card.Owner.AddMemory). Non-interactive.</summary>
public sealed class MemoryBody : IEffectBody
{
    private readonly int _amount;

    public MemoryBody(int amount) => _amount = amount;

    public bool IsInteractive => false;

    public ChoiceRequest? BuildRequest(CardSource card, IReadOnlyList<HeadlessPlayerId> players) => null;

    public void Apply(CardSource card, MatchStateMutationSink sink, IReadOnlyList<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(sink);
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.AddMemoryKind,
            card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.AmountKey] = _amount }));
    }
}

/// <summary>Pay a "trash <c>count</c> cards from your own hand" cost, then apply a self mutation
/// (<paramref name="followUpKind"/>, e.g. <see cref="MatchStateMutationSink.UnsuspendKind"/> for BT1_039
/// "trash 3 cards in your hand to unsuspend this Digimon"). Interactive: the player picks exactly
/// <c>count</c> hand cards (the caller's CanActivate has already ensured enough are held), they are trashed,
/// then the self mutation resolves — mirroring the AS-IS SelectHandEffect(discard) → follow-up coroutine.</summary>
public sealed class SelectTrashHandThenSelfMutationBody : IEffectBody
{
    private readonly int _count;
    private readonly string _followUpKind;
    private readonly string _message;

    public SelectTrashHandThenSelfMutationBody(int count, string followUpKind, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(followUpKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        _count = count;
        _followUpKind = followUpKind;
        _message = message;
    }

    public bool IsInteractive => true;

    public ChoiceRequest? BuildRequest(CardSource card, IReadOnlyList<HeadlessPlayerId> players)
    {
        ArgumentNullException.ThrowIfNull(card);
        var reader = (IZoneStateReader)card.Context.ZoneMover;
        var candidates = reader.GetCards(card.Owner, ChoiceZone.Hand)
            .Select(id => EffectChoiceHelpers.Candidate(id, id.Value, ChoiceZone.Hand, isSelectable: true, card.Owner))
            .ToList();
        int max = Math.Min(_count, candidates.Count);
        if (max <= 0)
        {
            return null;
        }

        // canNoSelect:false (AS-IS) → exactly max, no skip.
        return EffectChoiceHelpers.CreatePermanentRequest(card.Owner, _message, minCount: max, maxCount: max, canSkip: false, candidates);
    }

    public void Apply(CardSource card, MatchStateMutationSink sink, IReadOnlyList<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(sink);
        // Cost: trash each selected hand card.
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

        // Effect: the self mutation (e.g. unsuspend this card).
        sink.Apply(new EffectMutation(
            _followUpKind, card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = card.InstanceId.Value }));
    }
}

/// <summary>Return THIS card to its owner's hand (AS-IS AddThisCardToHand). Non-interactive.</summary>
public sealed class SelfToHandBody : IEffectBody
{
    public bool IsInteractive => false;

    public ChoiceRequest? BuildRequest(CardSource card, IReadOnlyList<HeadlessPlayerId> players) => null;

    public void Apply(CardSource card, MatchStateMutationSink sink, IReadOnlyList<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(sink);
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.ReturnToHandKind,
            card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = card.InstanceId.Value }));
    }
}

/// <summary>Grant a continuous effect (a restriction / cost-modifier / keyword — any registered-continuous
/// <see cref="ICardEffect"/>) by registering its binding when the activated skill resolves. Subsumes the
/// "apply a CanNot* restriction / cost change via a [Main]/[Security]/trigger skill" shape — the ~18 static
/// restriction factories + the cost factories are the payload; this body just wires their registration into the
/// activation flow. Non-interactive.</summary>
public sealed class GrantContinuousBody : IEffectBody
{
    private readonly ICardEffect _continuous;

    public GrantContinuousBody(ICardEffect continuous)
    {
        ArgumentNullException.ThrowIfNull(continuous);
        _continuous = continuous;
    }

    public bool IsInteractive => false;

    public ChoiceRequest? BuildRequest(CardSource card, IReadOnlyList<HeadlessPlayerId> players) => null;

    public void Apply(CardSource card, MatchStateMutationSink sink, IReadOnlyList<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(card);
        // (P6 cluster3) old-model lowering via LegacyBindingBridge (ToBinding left the ICardEffect contract);
        // a NEW-model effect on this grant path has no grant store yet — STOP, design item RD-P6C3-C1.
        if (!LegacyBindingBridge.TryToBinding(
                _continuous,
                $"{card.InstanceId.Value}:grant:{_continuous.GetType().Name}",
                out EffectBinding? binding) || binding is null)
        {
            throw new NotSupportedException(
                $"GrantContinuousBody: '{_continuous.GetType().Name}' is a NEW-model effect — no new-model grant store exists yet (design item RD-P6C3-C1).");
        }

        card.Context.EffectRegistry.Register(binding);
    }
}

// (R3-C2b-2) MemoryGainThenScheduledReversalBody DELETED — the invented "gain +N now, schedule a fire-once -N
// registry reversal" body is retired. BT1_021 / BT1_090 (its only callers) are now AS-IS 1:1 ActivateClass
// re-ports: they gain +N via card.Owner.AddMemory THEN store the "-N at end of turn" reversal (EoTLose3Memory /
// a nested "Memory -N" ActivateClass) into the owning Player's UntilEachTurnEnd bucket via AddEffectToPlayer /
// UntilEachTurnEndEffects.Add — the flipped window's player.EffectList(OnEndTurn) scan fires it, and the
// per-duration bucket clear gives the fire-once semantics (no DelayedOneShot registry binding).

/// <summary>Select up to <c>maxCount</c> matching permanents and apply a <see cref="SelectPermanentEffect.Mode"/>
/// (Destroy / Tap / UnTap / Bounce / Discard / Custom …) — the AS-IS SelectPermanentEffect coroutine. When
/// <paramref name="onEachSelected"/> is supplied it runs, per chosen id, AFTER the Mode mutation — a 1:1 mirror
/// of the AS-IS <c>SelectPermanentCoroutine</c> / <c>afterSelectPermanentCoroutine</c> per-selected-permanent
/// follow-up (e.g. grant the picked Digimon a keyword, set its base DP, or attach a nested effect via
/// CardEffectCommons.GainBlocker / ChangeBaseDigimonDP / AddEffectToPermanent). Interactive.</summary>
public sealed class SelectBody : IEffectBody
{
    private readonly SelectPermanentEffect _select = new();
    private readonly Action<HeadlessEntityId>? _onEachSelected;
    private readonly Action<CardSource, MatchStateMutationSink, HeadlessEntityId>? _onEachSelectedWithSink;

    public SelectBody(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, bool canNoSelect, bool canEndNotMax,
        SelectPermanentEffect.Mode mode, string description, Action<HeadlessEntityId>? onEachSelected = null,
        Action<CardSource, MatchStateMutationSink, HeadlessEntityId>? onEachSelectedWithSink = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(canTarget);
        _select.SetUp(card.Owner, canTarget, maxCount, canNoSelect, canEndNotMax, mode, card.InstanceId, card.Context);
        _select.SetUpCustomMessage(description);
        _onEachSelected = onEachSelected;
        _onEachSelectedWithSink = onEachSelectedWithSink;
    }

    public bool IsInteractive => true;

    public ChoiceRequest? BuildRequest(CardSource card, IReadOnlyList<HeadlessPlayerId> players) =>
        _select.BuildRequest((IZoneStateReader)card.Context.ZoneMover, players);

    public void Apply(CardSource card, MatchStateMutationSink sink, IReadOnlyList<HeadlessEntityId> selected)
    {
        // AS-IS SelectPermanentEffect: apply the Mode mutation to the pick(s) (UnTap unsuspends, Bounce returns,
        // Custom is a no-op) ...
        _select.Apply(sink, selected);
        // ... then the AS-IS SelectPermanentCoroutine / afterSelectPermanentCoroutine per-selected-permanent
        // follow-up, scoped to exactly the id(s) the player chose (maxCount is <= 1 for every current caller, so
        // the per-id vs after-all ordering distinction is moot). The registry-scoped callback (keyword / DP
        // grants that self-register via EffectRegistry) runs first; the sink-scoped callback (mutations derived
        // from the pick — e.g. destroy every same-named permanent, trash the pick's digivolution cards) second.
        if (_onEachSelected is not null)
        {
            foreach (HeadlessEntityId id in selected)
            {
                _onEachSelected(id);
            }
        }

        if (_onEachSelectedWithSink is not null)
        {
            foreach (HeadlessEntityId id in selected)
            {
                _onEachSelectedWithSink(card, sink, id);
            }
        }
    }
}

/// <summary>The uniform activated effect. Resolved through the activation flow (bridge / OptionActivate /
/// PlayCardAction / DigivolveAction -> ActivatedEffectResolver), which honours the gate + precondition before
/// driving the body. The trigger timing + <see cref="CanUse"/> gate carry what the AS-IS put in the timing
/// block + CanUseCondition; <see cref="MaxCountPerTurn"/> carries the AS-IS once-per-turn order.</summary>
public sealed class ActivatedEffect : IActivatedCardEffect
{
    public ActivatedEffect(
        CardSource card,
        EffectTiming timing,
        Func<CardEffectResolveContext, bool>? canUse,
        Func<bool>? canActivate,
        IEffectBody body,
        int? maxCountPerTurn,
        bool isOptional,
        string description,
        bool refundWhenNotExecuted = false,
        Func<CardSource, IReadOnlyList<HeadlessEntityId>, bool>? executedPredicate = null,
        string? capHash = null,
        bool isInheritedEffect = false)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(body);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        Timing = timing;
        CanUse = canUse;
        CanActivate = canActivate;
        Body = body;
        MaxCountPerTurn = maxCountPerTurn;
        IsOptional = isOptional;
        Description = description;
        RefundWhenNotExecuted = refundWhenNotExecuted;
        ExecutedPredicate = executedPredicate;
        IsInheritedEffect = isInheritedEffect;
        // (2026-07-11 re-review) The once-per-turn cap PARTITION mirrors AS-IS IsSameEffect
        // (ICardEffect.cs:860-933): with NO HashString, every effect of the SAME SOURCE CARD counts as "the
        // same effect" for GetUseCountThisTurn — timing- and body-blind (a card whose two capped effects must
        // count separately sets SetHashString, e.g. ST16_11 "Unsuspend_ST16_11"/"Delete_ST16_11"). So the
        // default id collapses to the card ("{card}:ae"); a card mirroring an AS-IS SetHashString passes
        // capHash to split its partition. (A per-(timing,body) id was a NARROWER partition that let same-card
        // sibling caps count independently where AS-IS shares one count.)
        EffectId = new HeadlessEntityId(
            string.IsNullOrWhiteSpace(capHash)
                ? $"{card.InstanceId.Value}:ae"
                : $"{card.InstanceId.Value}:ae:{capHash}");
    }

    public CardSource Card { get; }

    /// <summary>Stable id for once-per-turn flag keying (the activation flow is imperative, not a registered binding).</summary>
    public HeadlessEntityId EffectId { get; }

    public EffectTiming Timing { get; }

    public Func<CardEffectResolveContext, bool>? CanUse { get; }

    public Func<bool>? CanActivate { get; }

    public IEffectBody Body { get; }

    public int? MaxCountPerTurn { get; }

    public bool IsOptional { get; }

    public string Description { get; }

    /// <summary>(F1-M1-INHERITSCAN) Mirror of the AS-IS <c>ActivateClass.SetIsInheritedEffect(true)</c> flag — an
    /// INHERITED (digivolution-source) activated skill. AS-IS <c>Permanent.EffectList_ForCard</c>
    /// (Permanent.cs:1526) exposes an inherited effect ONLY from a NON-TOP source of a Digimon permanent (never
    /// from the top card), and a top card contributes only its NON-inherited effects. The activated bridge
    /// (<c>WindowResolverWiring</c>) reads this to route the source-vs-top membership: a source-scan collects
    /// only inherited activated effects; a top-scan only non-inherited ones. Default <c>false</c> (a plain
    /// top-card [Main]/trigger activated skill) — every effect ported before this flag stays non-inherited, so
    /// the top-scan keeps it exactly as before (behaviour-neutral).</summary>
    public bool IsInheritedEffect { get; }

    /// <summary>(B-4 rework) PER-CARD opt-in mirror of the AS-IS body's explicit <c>if (!executed) RemoveUse()</c>
    /// (~38 cards, e.g. AD1_024:265 / BT14_029:114). The AS-IS DEFAULT (the other ~1,170 [Once Per Turn] cards) is
    /// that a registered use stays consumed even when the body does nothing — so this is <c>false</c> unless the
    /// AS-IS card calls RemoveUse.</summary>
    public bool RefundWhenNotExecuted { get; }

    /// <summary>(B-4 rework) The card-authored <c>executed</c> predicate, evaluated after the body ran (or was
    /// skipped) with the selection the player made. AS-IS <c>executed</c> is a card-defined composite (a board
    /// predicate for BT14_029, a 3-branch OR for AD1_024) — a bare "selection was skipped" only matches the
    /// simplest refund cards, so a refund card whose executed condition is not that must supply this.</summary>
    public Func<CardSource, IReadOnlyList<HeadlessEntityId>, bool>? ExecutedPredicate { get; }

    /// <summary>The full gate — AS-IS CanUseCondition (collect-time CanTrigger half) AND CanActivateCondition
    /// (per-pass CanActivate half). Used where AS-IS evaluates both: direct (collect-and-resolve-inline) paths and
    /// the declaration legal-move gate (CanUse = CanTrigger &amp;&amp; CanActivate). The event subject reaches
    /// <see cref="CanUse"/> through the enriched context.</summary>
    public bool CanResolve(CardEffectResolveContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return CanResolveUseHalf(context) && CanResolveActivateHalf();
    }

    /// <summary>The COLLECT-time half — AS-IS CanUseCondition inside CanTrigger (ICardEffect.cs:319-358).
    /// AS-IS evaluates this ONCE at collect (GetSkillInfos / EffectList) and NEVER again on the stacked skill
    /// (the execution path re-checks only CanActivate — ICardEffect.cs:1116-1286 has no CanTrigger re-check),
    /// so the window's per-pass gate must NOT include it.</summary>
    public bool CanResolveUseHalf(CardEffectResolveContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return CanUse is null || CanUse(context);
    }

    /// <summary>The PER-PASS half — AS-IS CanActivateCondition inside CanActivate (ICardEffect.cs:364-457),
    /// re-checked every window pass on the already-stacked skill (MultipleSkills.cs:122/164-165), at pick (:366)
    /// and at execution entry (AutoProcessing.cs:1068). The once-per-turn cap also lives in AS-IS CanActivate —
    /// the caller pairs this with <c>OnceFlags.CanActivate</c>.</summary>
    public bool CanResolveActivateHalf() => CanActivate is null || CanActivate();

    /// <summary>Resolve the body and report whether it EXECUTED. Interactive bodies surface a choice; the default
    /// executed signal is "the selection was not skipped" (non-interactive bodies always execute), overridden by
    /// <see cref="ExecutedPredicate"/> when the card defines its own executed condition. The caller consumes the
    /// per-turn use BEFORE this runs (AS-IS register-before-body) and refunds it afterwards ONLY when
    /// <see cref="RefundWhenNotExecuted"/> is set and this returns false (the AS-IS per-card RemoveUse opt-in).</summary>
    public async ValueTask<bool> ResolveBodyAsync(
        MatchStateMutationSink sink,
        IChoiceProvider choices,
        IReadOnlyList<HeadlessPlayerId> players,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sink);
        bool executed;
        IReadOnlyList<HeadlessEntityId> selected = Array.Empty<HeadlessEntityId>();
        if (Body.IsInteractive && Body.BuildRequest(Card, players) is ChoiceRequest request)
        {
            ChoiceResult result = await choices.ChooseAsync(request, cancellationToken).ConfigureAwait(false);
            if (result.IsSkipped)
            {
                executed = false;
            }
            else
            {
                selected = result.SelectedIds;
                await Body.ApplyAsync(Card, sink, selected, cancellationToken).ConfigureAwait(false);
                executed = true;
            }
        }
        else
        {
            await Body.ApplyAsync(Card, sink, selected, cancellationToken).ConfigureAwait(false);
            executed = true;
        }

        return ExecutedPredicate is null ? executed : ExecutedPredicate(Card, selected);
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Uniform activated effect is resolved via the activation flow, not registered: {Description}");
}
