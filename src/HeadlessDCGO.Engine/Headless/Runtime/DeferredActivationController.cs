namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Services;
using EffectTiming = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.EffectTiming;

/// <summary>
/// (G11-002) Holds the activation that suspended mid-resolution waiting for an agent choice
/// (DeferredChoicePendingException). The cost has already been paid and the card moved by its action; on
/// the next ResolveChoice the resolver is re-invoked for this same activation WITHOUT re-running the action
/// (no re-pay) — the DeferredChoiceProvider replays the agent's answer. Cleared once the activation
/// finishes (or another choice suspends it again, re-storing the same activation).
/// </summary>
public sealed class DeferredActivationController : IHeadlessMatchStateResettable
{
    public DeferredActivation? Pending { get; private set; }

    public bool HasPending => Pending is not null;

    public void Suspend(HeadlessEntityId cardId, EffectTiming timing, HeadlessPlayerId playerId)
    {
        Pending = new DeferredActivation(cardId, timing, playerId);
    }

    public void Clear() => Pending = null;

    public void ResetMatchState() => Pending = null;
}

public sealed record DeferredActivation(HeadlessEntityId CardId, EffectTiming Timing, HeadlessPlayerId PlayerId);
