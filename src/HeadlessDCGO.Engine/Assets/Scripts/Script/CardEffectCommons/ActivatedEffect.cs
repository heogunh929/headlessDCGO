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

/// <summary>Run a fixed SEQUENCE of non-interactive bodies in order — the 1:1 mirror of an AS-IS
/// ActivateCoroutine that performs several sink mutations back-to-back (e.g. BT8_092/BT6_088 "gain 1 memory
/// and &lt;Draw 1&gt;" = <see cref="DrawBody"/> then <see cref="MemoryBody"/>). Every sub-body must be
/// non-interactive (no player choice); this composite is itself non-interactive and applies each in list order.
/// A body needing an interactive step must be expressed as a dedicated "…Then…" body, not composed here.</summary>
public sealed class CompositeBody : IEffectBody
{
    private readonly IReadOnlyList<IEffectBody> _bodies;

    public CompositeBody(params IEffectBody[] bodies)
    {
        ArgumentNullException.ThrowIfNull(bodies);
        foreach (IEffectBody b in bodies)
        {
            if (b is null)
            {
                throw new ArgumentException("Composite sub-body must not be null.", nameof(bodies));
            }

            if (b.IsInteractive)
            {
                throw new ArgumentException(
                    "CompositeBody composes only NON-interactive bodies; use a dedicated '…Then…' body for an interactive step.",
                    nameof(bodies));
            }
        }

        _bodies = bodies;
    }

    public bool IsInteractive => false;

    public ChoiceRequest? BuildRequest(CardSource card, IReadOnlyList<HeadlessPlayerId> players) => null;

    public void Apply(CardSource card, MatchStateMutationSink sink, IReadOnlyList<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(sink);
        foreach (IEffectBody body in _bodies)
        {
            body.Apply(card, sink, selected);
        }
    }
}

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

/// <summary>&lt;Recovery +N (Deck)&gt; — move the top N deck cards onto the owner's security stack. Non-interactive.</summary>
public sealed class RecoveryBody : IEffectBody
{
    private readonly int _amount;

    public RecoveryBody(int amount) => _amount = amount;

    public bool IsInteractive => false;

    public ChoiceRequest? BuildRequest(CardSource card, IReadOnlyList<HeadlessPlayerId> players) => null;

    public void Apply(CardSource card, MatchStateMutationSink sink, IReadOnlyList<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(sink);
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.RecoverKind,
            card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [MatchStateMutationSink.PlayerIdKey] = card.Owner.Value,
                [MatchStateMutationSink.CountKey] = _amount,
            }));
    }
}

/// <summary>Trash the top/bottom N security cards of <paramref name="player"/> (AS-IS IDestroySecurity /
/// DestroySecurity coroutine). Non-interactive.</summary>
public sealed class TrashSecurityBody : IEffectBody
{
    private readonly HeadlessPlayerId _player;
    private readonly int _count;
    private readonly bool _fromTop;

    public TrashSecurityBody(HeadlessPlayerId player, int count, bool fromTop)
    {
        _player = player;
        _count = count;
        _fromTop = fromTop;
    }

    public bool IsInteractive => false;

    public ChoiceRequest? BuildRequest(CardSource card, IReadOnlyList<HeadlessPlayerId> players) => null;

    public void Apply(CardSource card, MatchStateMutationSink sink, IReadOnlyList<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(sink);
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.TrashSecurityKind,
            card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [MatchStateMutationSink.PlayerIdKey] = _player.Value,
                [MatchStateMutationSink.CountKey] = _count,
                [MatchStateMutationSink.FromTopKey] = _fromTop,
            }));
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

/// <summary>Pay a self-suspend cost, then gain N memory (AS-IS <c>SuspendPermanentsClass(this).Tap()</c>
/// followed by <c>card.Owner.AddMemory(N)</c>) — e.g. ST4_14 "you may suspend this Tamer to gain 1 memory".
/// The optional "may" is the activation itself (isOptional on the ActivatedEffect); the cost is applied
/// first, then the memory gain, matching the AS-IS coroutine order. Non-interactive.</summary>
public sealed class SuspendSelfAndGainMemoryBody : IEffectBody
{
    private readonly int _amount;

    public SuspendSelfAndGainMemoryBody(int amount) => _amount = amount;

    public bool IsInteractive => false;

    public ChoiceRequest? BuildRequest(CardSource card, IReadOnlyList<HeadlessPlayerId> players) => null;

    public void Apply(CardSource card, MatchStateMutationSink sink, IReadOnlyList<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(sink);
        // Cost: suspend this card's own permanent (EntityId = source, TargetEntityIdKey = the suspend target).
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.SuspendKind,
            card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = card.InstanceId.Value }));
        // Effect: gain N memory for the card's owner (same shape as MemoryBody).
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.AddMemoryKind,
            card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.AmountKey] = _amount }));
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

/// <summary>Run a caller-supplied no-select PLAYER-SCOPE grant — the AS-IS ActivateCoroutine that calls a
/// <c>Gain*PlayerEffect</c> commons procedure (GainCanNotAttackPlayerEffect / GainCanNotUnsuspendPlayerEffect /
/// …) directly, with NO SelectPermanentEffect step (unlike <see cref="SelectBody"/>). Those procedures take their
/// own per-permanent scope predicate + duration and self-register a duration-tagged player-scope binding via
/// EffectRegistry (the AS-IS battle-area + !CanNotBeAffected guards ride the GainToPlayerScope live-CanUse), so
/// this body just invokes the grant when the activated skill resolves. Non-interactive. Mirrors how
/// <see cref="GrantContinuousBody"/> wires a registration into the activation flow, but for the self-registering
/// Gain*PlayerEffect procedures (which have no ICardEffect/ToBinding wrapper to hand GrantContinuousBody).</summary>
public sealed class GrantPlayerScopeRestrictionBody : IEffectBody
{
    private readonly Action<CardSource> _grant;

    public GrantPlayerScopeRestrictionBody(Action<CardSource> grant)
    {
        ArgumentNullException.ThrowIfNull(grant);
        _grant = grant;
    }

    public bool IsInteractive => false;

    public ChoiceRequest? BuildRequest(CardSource card, IReadOnlyList<HeadlessPlayerId> players) => null;

    public void Apply(CardSource card, MatchStateMutationSink sink, IReadOnlyList<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(card);
        _grant(card);
    }
}

/// <summary>Pay an N-memory cost then unsuspend this card's own permanent — the AS-IS ActivateCoroutine
/// <c>card.Owner.AddMemory(-N)</c> THEN <c>IUnsuspendPermanents(self).Unsuspend()</c> (e.g. BT1_081
/// "[End of Attack][Twice Per Turn] You can unsuspend this Digimon by decreasing your memory by 3"). The
/// payability gate (<c>MemoryController.CanPay(N)</c>) rides the owning ActivatedEffect's CanActivate. The
/// reverse-order sibling of <see cref="SuspendSelfAndGainMemoryBody"/> (cost = suspend, effect = gain memory).
/// Non-interactive.</summary>
public sealed class MemoryCostThenUnsuspendSelfBody : IEffectBody
{
    private readonly int _memoryCost;

    public MemoryCostThenUnsuspendSelfBody(int memoryCost) => _memoryCost = memoryCost;

    public bool IsInteractive => false;

    public ChoiceRequest? BuildRequest(CardSource card, IReadOnlyList<HeadlessPlayerId> players) => null;

    public void Apply(CardSource card, MatchStateMutationSink sink, IReadOnlyList<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(sink);
        // Cost: AS-IS card.Owner.AddMemory(-N).
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.AddMemoryKind,
            card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.AmountKey] = -_memoryCost }));
        // Effect: AS-IS IUnsuspendPermanents(self).Unsuspend().
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.UnsuspendKind,
            card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = card.InstanceId.Value }));
    }
}

/// <summary>Return the TOP security card to hand, then unsuspend this card's own permanent — the AS-IS BT9_043
/// OnEndAttack ActivateCoroutine (BT9_043.cs:115-135): <c>AddHandCards({SecurityCards[0]})</c> +
/// <c>IReduceSecurity().ReduceSecurity()</c> THEN <c>if (IsExistOnBattleArea) IUnsuspendPermanents(self).Unsuspend()</c>.
/// A SINGLE ReturnToHand on the top security card reproduces BOTH AS-IS steps: the security->hand zone move fires
/// OnAddHand (the AS-IS AddHandCards) AND derives OnLoseSecurity (the AS-IS IReduceSecurity broadcast — TriggerTimingMap
/// from==Security &amp;&amp; to!=Security). AS-IS "Top security = index 0" (SecurityCards[0]) == zone-reader index 0. A single
/// card moves, so the derived OnLoseSecurity fires exactly once (no batch-id needed — same proven path as
/// ReplaceBottomSecurityWithFaceUpEffect). Non-interactive.</summary>
public sealed class ReturnTopSecurityToHandThenUnsuspendSelfBody : IEffectBody
{
    public bool IsInteractive => false;

    public ChoiceRequest? BuildRequest(CardSource card, IReadOnlyList<HeadlessPlayerId> players) => null;

    public void Apply(CardSource card, MatchStateMutationSink sink, IReadOnlyList<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(sink);
        // AS-IS: topCard = SecurityCards[0]; AddHandCards({topCard}) + IReduceSecurity().
        if (card.Context.ZoneMover is IZoneStateReader reader)
        {
            IReadOnlyList<HeadlessEntityId> security = reader.GetCards(card.Owner, ChoiceZone.Security);
            if (security.Count > 0)
            {
                sink.Apply(new EffectMutation(
                    MatchStateMutationSink.ReturnToHandKind,
                    card.InstanceId,
                    new Dictionary<string, object?>(StringComparer.Ordinal)
                        { [MatchStateMutationSink.TargetEntityIdKey] = security[0].Value }));
            }
        }
        // AS-IS: if (IsExistOnBattleArea(card)) IUnsuspendPermanents(self).Unsuspend().
        if (CardEffectCommons.IsExistOnBattleArea(card))
        {
            CardEffectCommons.UnsuspendSelf(sink, card);
        }
    }
}

/// <summary>Gain N memory now, then schedule a one-shot reversal (lose N memory) at end of THIS turn — the AS-IS
/// ActivateCoroutine <c>card.Owner.AddMemory(+N)</c> THEN a nested one-shot <c>AddMemory(-N)</c> registered via
/// <c>AddEffectToPlayer(timing: OnEndTurn)</c> (e.g. BT1_090 "[Main] Gain 2 memory. At end of turn, lose 2
/// memory"). The reversal fires exactly once (AddEffectToPlayer's DelayedOneShot bookkeeping), so it is a
/// this-turn-only boost, not a permanent gain. Non-interactive.</summary>
public sealed class MemoryGainThenScheduledReversalBody : IEffectBody
{
    private readonly int _amount;

    public MemoryGainThenScheduledReversalBody(int amount) => _amount = amount;

    public bool IsInteractive => false;

    public ChoiceRequest? BuildRequest(CardSource card, IReadOnlyList<HeadlessPlayerId> players) => null;

    public void Apply(CardSource card, MatchStateMutationSink sink, IReadOnlyList<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(sink);
        // Immediate: AS-IS card.Owner.AddMemory(+N).
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.AddMemoryKind,
            card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.AmountKey] = _amount }));
        // Deferred: AS-IS nested ActivateClass "Memory -N" registered via AddEffectToPlayer(OnEndTurn) — a
        // fire-once end-of-turn reversal (DelayedOneShot semantics), mirrored by a TriggeredMemoryEffect(-N).
        ICardEffect reversal = CardEffectFactory.AddMemoryTriggerEffect(
            timing: EffectTiming.OnEndTurn,
            amount: -_amount,
            isInheritedEffect: false,
            card: card,
            condition: null,
            description: $"At end of turn, lose {_amount} memory.",
            // Unique per registration: AS-IS UntilEachTurnEndEffects is a LIST — activating twice the same
            // turn stacks TWO reversals (BT1_021 attacks twice → lose 6), so the ids must not collide.
            effectIdSuffix: Guid.NewGuid().ToString("N"));
        CardEffectCommons.AddEffectToPlayer(EffectDuration.UntilEachTurnEnd, card, reversal, EffectTiming.OnEndTurn);
    }
}

/// <summary>Pay a self-suspend cost (AS-IS <c>SuspendPermanentsClass(card.PermanentOfThisCard()).Tap()</c>) then
/// run an inner body — the "you can suspend this &lt;card&gt; to &lt;effect&gt;" activated cost shape (e.g. BT1_086
/// "you can suspend this Tamer to trash the bottom digivolution card of 1 opponent Digimon"). BuildRequest is
/// delegated to the inner body (so an interactive inner select still surfaces its choice); Apply pays the suspend
/// cost first, then runs the inner body. Interactive iff the inner body is.</summary>
public sealed class SuspendSelfCostThenBody : IEffectBody
{
    private readonly IEffectBody _inner;

    public SuspendSelfCostThenBody(IEffectBody inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    public bool IsInteractive => _inner.IsInteractive;

    public ChoiceRequest? BuildRequest(CardSource card, IReadOnlyList<HeadlessPlayerId> players) =>
        _inner.BuildRequest(card, players);

    public void Apply(CardSource card, MatchStateMutationSink sink, IReadOnlyList<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(sink);
        // Cost: AS-IS SuspendPermanentsClass(card.PermanentOfThisCard()).Tap() — suspend this card's own permanent.
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.SuspendKind,
            card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = card.InstanceId.Value }));
        _inner.Apply(card, sink, selected);
    }
}

/// <summary>Apply a caller-supplied per-target mutation to EVERY field permanent matching a predicate, with NO
/// player selection — the AS-IS ActivateCoroutine that runs <c>foreach (… GetBattleAreaDigimons().Filter(pred))
/// &lt;mutation&gt;</c> directly (e.g. "trash all digivolution cards under every opponent Digimon" / "suspend all
/// opponent Digimon without &lt;Blocker&gt;"). The match predicate + per-target action are evaluated live at
/// resolve time via <see cref="CardEffectCommons.MatchConditionPermanentIds"/>; the sink's centralised immunity
/// gates filter, mirroring <c>DestroyPermanentsEffect</c>. Non-interactive.</summary>
public sealed class ApplyToAllMatchingBody : IEffectBody
{
    private readonly Func<HeadlessEntityId, bool> _match;
    private readonly Action<CardSource, MatchStateMutationSink, HeadlessEntityId> _perTarget;

    public ApplyToAllMatchingBody(
        Func<HeadlessEntityId, bool> match, Action<CardSource, MatchStateMutationSink, HeadlessEntityId> perTarget)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(perTarget);
        _match = match;
        _perTarget = perTarget;
    }

    public bool IsInteractive => false;

    public ChoiceRequest? BuildRequest(CardSource card, IReadOnlyList<HeadlessPlayerId> players) => null;

    public void Apply(CardSource card, MatchStateMutationSink sink, IReadOnlyList<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(sink);
        foreach (HeadlessEntityId id in CardEffectCommons.MatchConditionPermanentIds(card, _match))
        {
            _perTarget(card, sink, id);
        }
    }
}

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
