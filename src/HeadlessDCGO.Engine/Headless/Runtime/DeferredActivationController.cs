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

    public void Suspend(
        HeadlessEntityId cardId, EffectTiming timing, HeadlessPlayerId playerId, GameEvent? drivingEvent = null,
        bool declarative = false, bool windowDispatched = false)
    {
        Pending = new DeferredActivation(cardId, timing, playerId, drivingEvent, declarative, windowDispatched);
    }

    public void Clear() => Pending = null;

    public void ResetMatchState() => Pending = null;
}

/// <summary><paramref name="DrivingEvent"/> keeps the game event that drove a bridged broadcast activation
/// (e.g. OnDigivolutionCardDiscarded) across the suspend, so the re-invoked resolver re-evaluates the gate
/// with the same subject + event metadata (the AS-IS coroutine keeps its hashtable alive across the choice).
/// <paramref name="Declarative"/>/<paramref name="WindowDispatched"/> keep the resolution FLAVOR so the resumed
/// re-invocation replays the SAME gate/consume order the original run staged (a flavor mismatch would desync the
/// OnceFlags uniform-cycle replay cursor from the staged consumes).</summary>
public sealed record DeferredActivation(
    HeadlessEntityId CardId, EffectTiming Timing, HeadlessPlayerId PlayerId, GameEvent? DrivingEvent = null,
    bool Declarative = false, bool WindowDispatched = false);
