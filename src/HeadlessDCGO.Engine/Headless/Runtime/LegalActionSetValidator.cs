namespace HeadlessDCGO.Engine.Headless.Runtime;

using System.Collections;
using System.Globalization;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// Authoritative legality boundary for the <em>agent-facing</em> action space (G3.5-RL-A1).
/// Generation and acceptance share one predicate: an agent action is legal iff a semantically
/// equal action (same normalized type and matching identifying parameters) exists in the
/// dispatcher-generated legal set for the acting player.
/// <para>
/// Engine-internal / system action types (memory ops, zone mutations, choice / terminal control,
/// effect enqueue, cheat, raw <c>MoveCard</c>, etc.) are not part of the agent action space and
/// are deferred to their per-handler validation, so existing engine scripting and tests that drive
/// those actions through <c>ApplyActionAsync</c> are unaffected.
/// </para>
/// </summary>
public sealed class LegalActionSetValidator : IActionLegality
{
    private static readonly HashSet<string> AgentFacingTypes = new(StringComparer.Ordinal)
    {
        HeadlessActionTypes.NormalizedPass,
        HeadlessActionTypes.NormalizedPlayCard,
        HeadlessActionTypes.NormalizedDigivolve,
        HeadlessActionTypes.NormalizedActivateOption,
        HeadlessActionTypes.NormalizedDeclareAttack,
        HeadlessActionTypes.NormalizedAdvancePhase,
        HeadlessActionTypes.NormalizedEndTurn,
        HeadlessActionTypes.NormalizedResolveChoice,
        // D-6: breeding-step decisions are now agent actions, not auto-resolved.
        HeadlessActionTypes.NormalizedHatchDigitama,
        HeadlessActionTypes.NormalizedMoveBreedingToBattle,
    };

    private readonly HeadlessLegalActionDispatcher _dispatcher;

    public LegalActionSetValidator(HeadlessLegalActionDispatcher? dispatcher = null)
    {
        _dispatcher = dispatcher ?? new HeadlessLegalActionDispatcher();
    }

    /// <summary>The set of normalized action types treated as the agent action space.</summary>
    public static IReadOnlyCollection<string> AgentActionTypes => AgentFacingTypes;

    public LegalityVerdict Validate(LegalAction action, EngineContext context)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(context);

        string normalizedType = HeadlessActionTypes.Normalize(action.ActionType);
        if (!AgentFacingTypes.Contains(normalizedType))
        {
            // Out of the agent action space: defer to the per-handler validation downstream.
            return LegalityVerdict.Legal;
        }

        IReadOnlyList<LegalAction> legalSet = _dispatcher.GetLegalActions(context, action.PlayerId);
        foreach (LegalAction candidate in legalSet)
        {
            if (HeadlessActionTypes.Normalize(candidate.ActionType) == normalizedType &&
                ParametersMatch(candidate.Parameters, action.Parameters))
            {
                return LegalityVerdict.Legal;
            }
        }

        return LegalityVerdict.Illegal(
            $"Action '{action.ActionType}' for player {action.PlayerId.Value} is not in the current legal action set.");
    }

    /// <summary>
    /// Every identifying parameter the dispatcher produced must be present and equal in the
    /// submitted action. The submitted action may carry extra metadata, which is ignored.
    /// </summary>
    private static bool ParametersMatch(
        IReadOnlyDictionary<string, object?> candidate,
        IReadOnlyDictionary<string, object?> submitted)
    {
        foreach (KeyValuePair<string, object?> pair in candidate)
        {
            if (!submitted.TryGetValue(pair.Key, out object? value) || !ValueEquals(pair.Value, value))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ValueEquals(object? left, object? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        if (left.Equals(right))
        {
            return true;
        }

        // GPT-#1: collection-valued parameters (e.g. ChoiceSelectedIds) must be compared ELEMENT-WISE.
        // Convert.ToString(array/list) returns the type name (e.g. "HeadlessEntityId[]"), so two
        // collections with different contents would otherwise compare equal — letting a ResolveChoice
        // that selected a different candidate pass the legality boundary. Strings are IEnumerable but
        // are value-compared above, so they are excluded here.
        if (left is IEnumerable leftSequence && right is IEnumerable rightSequence &&
            left is not string && right is not string)
        {
            return SequenceValueEquals(leftSequence, rightSequence);
        }

        // Fall back to invariant-string comparison for boxed / loosely-typed scalar parameter values.
        return string.Equals(
            Convert.ToString(left, CultureInfo.InvariantCulture),
            Convert.ToString(right, CultureInfo.InvariantCulture),
            StringComparison.Ordinal);
    }

    private static bool SequenceValueEquals(IEnumerable left, IEnumerable right)
    {
        IEnumerator leftEnumerator = left.GetEnumerator();
        IEnumerator rightEnumerator = right.GetEnumerator();
        try
        {
            while (true)
            {
                bool leftHasNext = leftEnumerator.MoveNext();
                bool rightHasNext = rightEnumerator.MoveNext();
                if (leftHasNext != rightHasNext)
                {
                    return false; // different lengths
                }

                if (!leftHasNext)
                {
                    return true; // both ended together
                }

                if (!ValueEquals(leftEnumerator.Current, rightEnumerator.Current))
                {
                    return false;
                }
            }
        }
        finally
        {
            (leftEnumerator as IDisposable)?.Dispose();
            (rightEnumerator as IDisposable)?.Dispose();
        }
    }
}
