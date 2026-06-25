namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Services;

public sealed class InMemoryHeadlessPlayerStatusController : IHeadlessPlayerStatusController
{
    private readonly Dictionary<HeadlessPlayerId, string> _losingPlayers = new();

    public void MarkLose(HeadlessPlayerId playerId, string reason = "")
    {
        if (playerId.IsEmpty)
        {
            throw new ArgumentException("Player id must not be empty.", nameof(playerId));
        }

        // First loss reason wins so the original cause is preserved across re-evaluations.
        if (!_losingPlayers.ContainsKey(playerId))
        {
            _losingPlayers[playerId] = reason ?? string.Empty;
        }
    }

    public bool IsLose(HeadlessPlayerId playerId)
    {
        return _losingPlayers.ContainsKey(playerId);
    }

    public bool TryGetLoseReason(HeadlessPlayerId playerId, out string reason)
    {
        if (_losingPlayers.TryGetValue(playerId, out string? value))
        {
            reason = value;
            return true;
        }

        reason = string.Empty;
        return false;
    }

    public IReadOnlyList<HeadlessPlayerId> LosingPlayers =>
        _losingPlayers.Keys
            .OrderBy(playerId => playerId.Value)
            .ToArray();

    public void ResetMatchState()
    {
        _losingPlayers.Clear();
    }
}
