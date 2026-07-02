namespace HeadlessDCGO.Engine.Headless.Choices;

using HeadlessDCGO.Engine.Headless.Services;

public sealed record ChoiceResult
{
    public ChoiceResult(
        bool isSkipped,
        IReadOnlyList<HeadlessEntityId> selectedIds,
        int? selectedCount = null)
    {
        ArgumentNullException.ThrowIfNull(selectedIds);

        HeadlessEntityId[] selectedIdSnapshot = selectedIds.ToArray();
        if (selectedIdSnapshot.Any(id => id.IsEmpty))
        {
            throw new ArgumentException("Selected ids must not contain empty values.", nameof(selectedIds));
        }

        if (isSkipped && (selectedIdSnapshot.Length > 0 || selectedCount is not null))
        {
            throw new ArgumentException("Skipped choice results must not include selected ids or a selected count.");
        }

        if (selectedCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(selectedCount), "Selected count must not be negative.");
        }

        IsSkipped = isSkipped;
        SelectedIds = Array.AsReadOnly(selectedIdSnapshot);
        SelectedCount = selectedCount;
    }

    public bool IsSkipped { get; }

    public IReadOnlyList<HeadlessEntityId> SelectedIds { get; }

    public int? SelectedCount { get; }

    public static ChoiceResult Skip()
    {
        return new ChoiceResult(isSkipped: true, Array.Empty<HeadlessEntityId>());
    }

    public static ChoiceResult Select(params HeadlessEntityId[] selectedIds)
    {
        return Select((IEnumerable<HeadlessEntityId>)selectedIds);
    }

    public static ChoiceResult Select(IEnumerable<HeadlessEntityId> selectedIds)
    {
        ArgumentNullException.ThrowIfNull(selectedIds);
        return new ChoiceResult(isSkipped: false, selectedIds.ToArray());
    }

    public static ChoiceResult SelectCount(int count)
    {
        return new ChoiceResult(isSkipped: false, Array.Empty<HeadlessEntityId>(), count);
    }

    public ChoiceResultValidation Validate(ChoiceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var failures = new List<string>();

        if (IsSkipped)
        {
            if (!request.CanSkip)
            {
                failures.Add("Choice result skipped a request that does not allow skipping.");
            }

            return ChoiceResultValidation.FromFailures(failures);
        }

        if (SelectedCount is not null)
        {
            ValidateSelectedCount(request, SelectedCount.Value, failures);
            return ChoiceResultValidation.FromFailures(failures);
        }

        ValidateSelectedIds(request, failures);

        // (P2) the AS-IS combination gate (CanEndSelect) — an individually-legal set can still be an
        // illegal combination.
        if (failures.Count == 0 && request.SelectionValidator is not null && !request.SelectionValidator(SelectedIds))
        {
            failures.Add("Selected ids are individually legal but the combination fails the request's selection validator.");
        }

        return ChoiceResultValidation.FromFailures(failures);
    }

    public void ThrowIfInvalid(ChoiceRequest request)
    {
        ChoiceResultValidation validation = Validate(request);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(validation.ToString());
        }
    }

    private void ValidateSelectedCount(
        ChoiceRequest request,
        int selectedCount,
        List<string> failures)
    {
        if (request.Type != ChoiceType.Count)
        {
            failures.Add("Selected count is only valid for count choice requests.");
        }

        if (SelectedIds.Count > 0)
        {
            failures.Add("Selected count results must not include selected ids.");
        }

        if (selectedCount < request.MinCount || selectedCount > request.MaxCount)
        {
            failures.Add($"Selected count {selectedCount} is outside the allowed range {request.MinCount}-{request.MaxCount}.");
        }
    }

    private void ValidateSelectedIds(
        ChoiceRequest request,
        List<string> failures)
    {
        if (request.Type == ChoiceType.Count)
        {
            failures.Add("Count choice requests require a selected count result.");
        }

        if (SelectedIds.Count < request.MinCount || SelectedIds.Count > request.MaxCount)
        {
            failures.Add($"Selected id count {SelectedIds.Count} is outside the allowed range {request.MinCount}-{request.MaxCount}.");
        }

        HashSet<HeadlessEntityId> selectableIds = request.Candidates
            .Where(candidate => candidate.IsSelectable)
            .Select(candidate => candidate.Id)
            .ToHashSet();

        var seen = new HashSet<HeadlessEntityId>();
        foreach (HeadlessEntityId id in SelectedIds)
        {
            if (!seen.Add(id))
            {
                failures.Add($"Selected id '{id}' was selected more than once.");
                continue;
            }

            if (!selectableIds.Contains(id))
            {
                failures.Add($"Selected id '{id}' is not a selectable candidate for this request.");
            }
        }
    }
}

public sealed record ChoiceResultValidation(
    bool IsValid,
    IReadOnlyList<string> Failures)
{
    public static ChoiceResultValidation FromFailures(IEnumerable<string> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);
        string[] snapshot = failures
            .Where(failure => !string.IsNullOrWhiteSpace(failure))
            .Select(failure => failure.Trim())
            .ToArray();

        return new ChoiceResultValidation(
            IsValid: snapshot.Length == 0,
            Failures: Array.AsReadOnly(snapshot));
    }

    public override string ToString()
    {
        return IsValid
            ? "Choice result is valid."
            : string.Join(" ", Failures);
    }
}
