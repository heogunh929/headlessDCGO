namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Services;

public sealed record MatchResult
{
    private HeadlessPlayerId? _winnerId;
    private bool _isDraw;
    private string _reason = string.Empty;

    public MatchResult(
        HeadlessPlayerId? WinnerId = null,
        bool IsDraw = false,
        bool IsSurrender = false,
        string Reason = "")
    {
        if (WinnerId.HasValue && IsDraw)
        {
            throw new ArgumentException("A drawn match cannot also have a winner.", nameof(IsDraw));
        }

        this.WinnerId = WinnerId;
        this.IsDraw = IsDraw;
        this.IsSurrender = IsSurrender;
        this.Reason = Reason;
    }

    public HeadlessPlayerId? WinnerId
    {
        get => _winnerId;
        init
        {
            if (value.HasValue && _isDraw)
            {
                throw new ArgumentException("A drawn match cannot also have a winner.", nameof(value));
            }

            _winnerId = value;
        }
    }

    public bool IsDraw
    {
        get => _isDraw;
        init
        {
            if (value && _winnerId.HasValue)
            {
                throw new ArgumentException("A drawn match cannot also have a winner.", nameof(value));
            }

            _isDraw = value;
        }
    }

    public bool IsSurrender { get; init; }

    public string Reason
    {
        get => _reason;
        init => _reason = value ?? string.Empty;
    }
}
