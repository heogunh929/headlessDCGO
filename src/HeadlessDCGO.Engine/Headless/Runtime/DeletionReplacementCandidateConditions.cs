namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (#3 porting-readiness seam) The card-specific candidate condition for a deletion-replacement
/// sub-selection. AS-IS keyword effects (Decoy/Scapegoat/Save …) take a <c>Func&lt;Permanent,bool&gt;
/// permanentCondition</c> closure supplied by the individual card (e.g. "only red allies", "only a
/// Tamer"); when null the candidate set is generic (any eligible owner battle-area card). The headless
/// candidate enumerators (<see cref="DeletionReplacementGate"/>) mirror that with an optional
/// <c>Func&lt;CardInstanceRecord,bool&gt;</c> predicate, and <see cref="DeletionReplacementTiming"/>
/// resolves the per-card predicate through this service.
///
/// No card ports a conditional deletion-replacement keyword yet, so the default
/// <see cref="NoDeletionReplacementCandidateConditions"/> returns null (generic) and the engine
/// behaves exactly as before. When porting such a card, register a card-aware implementation via
/// <c>EngineContext.RegisterService&lt;IDeletionReplacementCandidateConditions&gt;(…)</c> — the
/// enumeration seam is already in place, so no engine refactor is needed.
/// </summary>
public interface IDeletionReplacementCandidateConditions
{
    /// <summary>The candidate predicate the holder's <paramref name="option"/> imposes on each
    /// candidate record (matching the <c>DeletionReplacementTiming.*Option</c> constants), or null
    /// for the generic (unconstrained) candidate set.</summary>
    Func<CardInstanceRecord, bool>? Resolve(CardInstanceRecord holder, string option);
}

/// <summary>The default: every deletion-replacement candidate set is generic (no card-specific
/// condition). Used whenever no card-aware resolver is registered on the context.</summary>
public sealed class NoDeletionReplacementCandidateConditions : IDeletionReplacementCandidateConditions
{
    public static NoDeletionReplacementCandidateConditions Instance { get; } = new();

    private NoDeletionReplacementCandidateConditions()
    {
    }

    public Func<CardInstanceRecord, bool>? Resolve(CardInstanceRecord holder, string option) => null;
}

/// <summary>A delegate-backed resolver — the convenient way for a ported card (or a test) to register
/// per-(holder, option) candidate conditions without a bespoke class.</summary>
public sealed class DelegateDeletionReplacementCandidateConditions : IDeletionReplacementCandidateConditions
{
    private readonly Func<CardInstanceRecord, string, Func<CardInstanceRecord, bool>?> _resolver;

    public DelegateDeletionReplacementCandidateConditions(
        Func<CardInstanceRecord, string, Func<CardInstanceRecord, bool>?> resolver)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    public Func<CardInstanceRecord, bool>? Resolve(CardInstanceRecord holder, string option)
    {
        ArgumentNullException.ThrowIfNull(holder);
        ArgumentNullException.ThrowIfNull(option);
        return _resolver(holder, option);
    }
}
