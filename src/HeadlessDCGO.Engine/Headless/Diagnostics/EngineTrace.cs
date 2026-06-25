namespace HeadlessDCGO.Engine.Headless.Diagnostics;

using System.Security.Cryptography;
using System.Text;

public sealed class EngineTrace : ITraceSink
{
    private readonly List<TraceEvent> _events = new();
    private readonly TraceOptions _options;
    private long _sequence;

    public EngineTrace()
        : this(new TraceOptions())
    {
    }

    public EngineTrace(TraceOptions options)
    {
        _options = options;
    }

    public void Record(string category, string message, IReadOnlyDictionary<string, object?>? metadata = null)
    {
        if (!_options.Enabled)
        {
            return;
        }

        _events.Add(new TraceEvent(
            ++_sequence,
            category,
            message,
            metadata ?? new Dictionary<string, object?>()));

        if (_options.MaxEvents is int maxEvents && maxEvents >= 0)
        {
            while (_events.Count > maxEvents)
            {
                _events.RemoveAt(0);
            }
        }
    }

    public IReadOnlyList<TraceEvent> Snapshot()
    {
        return _events.ToArray();
    }

    public string Fingerprint()
    {
        var builder = new StringBuilder();
        foreach (TraceEvent traceEvent in _events)
        {
            traceEvent.AppendFingerprintData(builder);
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    public void Clear()
    {
        _events.Clear();
        _sequence = 0;
    }
}
