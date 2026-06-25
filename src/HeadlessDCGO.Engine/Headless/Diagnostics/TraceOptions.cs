namespace HeadlessDCGO.Engine.Headless.Diagnostics;

public sealed record TraceOptions(
    bool Enabled = true,
    int? MaxEvents = null);
