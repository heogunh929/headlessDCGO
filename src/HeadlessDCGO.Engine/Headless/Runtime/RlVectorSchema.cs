namespace HeadlessDCGO.Engine.Headless.Runtime;

// TODO: Version this schema once the final observation/action tensor layout stabilizes.
public sealed record RlVectorSchema(
    IReadOnlyList<string> ObservationFeatureNames,
    IReadOnlyList<string> ActionSlotNames)
{
    public int ObservationLength => ObservationFeatureNames.Count;

    public int ActionMaskLength => ActionSlotNames.Count;

    public static RlVectorSchema FromState(RlStepResult state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return new RlVectorSchema(
            state.Observation.FeatureNames,
            state.ActionMask.ActionSlotNames);
    }

    public RlVectorSchemaValidationResult ValidateSample(RlTransitionSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);

        List<string> failures = new();
        ValidateLength("Observation", ObservationLength, sample.Observation.Length, failures);
        ValidateLength("ActionMask", ActionMaskLength, sample.ActionMask.Length, failures);
        ValidateLength("ActionCounts", ActionMaskLength, sample.ActionCounts.Length, failures);
        ValidateLength("NextObservation", ObservationLength, sample.NextObservation.Length, failures);
        ValidateLength("NextActionMask", ActionMaskLength, sample.NextActionMask.Length, failures);
        ValidateLength("NextActionCounts", ActionMaskLength, sample.NextActionCounts.Length, failures);

        return new RlVectorSchemaValidationResult(failures.ToArray());
    }

    private static void ValidateLength(
        string field,
        int expected,
        int actual,
        List<string> failures)
    {
        if (expected != actual)
        {
            failures.Add($"{field}: expected {expected}, actual {actual}");
        }
    }
}

public sealed record RlVectorSchemaValidationResult(IReadOnlyList<string> Failures)
{
    public bool Passed => Failures.Count == 0;
}
