namespace HeadlessDCGO.Engine.Headless.Choices;

using HeadlessDCGO.Engine.Headless.Services;

/// <summary>(B5-1, 설계 §B5.6 — 핀 4, Hand/Card 표면) Deterministic open-time completability check for a
/// batch choice request: "does at least one confirmable selection exist?".
///
/// AS-IS anchor: the Hand/Panel surfaces have NO open-time combination check (SelectHandEffect.active()
/// :147-160 only requires one matching card). Their real unsatisfiable-forced behavior is the AI branch's
/// bounded search failing after the fact — candidates &lt; maxCount or every tried maxCount-subset failing
/// canEndSelectCondition ends in <c>SetTargetHandCards(null)</c> → <c>_noSelect = true</c>
/// (SelectHandEffect.cs :496-570 → :595-608), i.e. a forced request demotes to no-select instead of
/// deadlocking. The substrate translation moves that verdict to session-open time so a partial-selection
/// session (B5-2) is only ever opened when a confirmable completion exists (§B5.6: 완성 존재 보장 + 토글
/// 왕복 = 교착 0).
///
/// The search itself is the SAME deterministic translation already adopted for the AS-IS AI's bounded
/// random retry by RD-R4P4-02 (ScriptedChoiceProvider.CreateFallbackChoice :87-105): sizes from
/// min(MaxCount, selectable) down to MinCount (max-size first = the AS-IS AI's set size, smaller sizes =
/// the canEndNotMax early-end surface), lexicographic combinations in candidate order within a size,
/// capped at 200 validator evaluations (표면별 AS-IS cap 200/1000 중 기채택 200 — §B5 리스크 5:
/// existence 판정 목적이라 실차이는 조합수&gt;200 경계뿐, RD-R4P4-02 채택값과의 일관성 우선).
/// (B5-2, 설계 리스크 2) The search now lives here ONCE as <see cref="TryFindPassingSelection"/> and
/// ScriptedChoiceProvider consumes it — same loop, same cap accounting, so the provider's observable
/// answers (RD-R4P4-02 witness ST1_15 included) are unchanged.
///
/// Runtime seat (B5-2, 설계 §B5.6/리스크 4): the demotion is wired in EXACTLY ONE place —
/// <see cref="Runtime.DeferredChoiceProvider.ChooseAsync"/>, the substrate's choice-OPEN point for
/// agent-facing effect choices on both surfaces (pump await-mode and the legacy unwind model). An
/// unsatisfiable forced request returns an empty Select to the effect without ever registering a pending
/// choice (no session opens, no Validate runs on the empty set — the sanctioned bypass), and the demotion
/// is recorded for the game loop to surface as the
/// <see cref="Runtime.HeadlessActionParameterKeys.UnsatisfiableForcedChoice"/> metadata/trace marking
/// (퍼징 수확 D6 계수 채널; green-은폐 차단).</summary>
public static class ChoiceCompletability
{
    /// <summary>(설계 §B5.6) The demotion predicate for session opening: a FORCED batch selection
    /// (<c>MinCount &gt; 1 &amp;&amp; !CanSkip</c>, non-Count) with no completable selection. Requests
    /// outside that shape are never demoted: optional ones can always skip / confirm below max, and
    /// single-pick forced ones keep the existing per-candidate table (기존 궤적 보존 경계 §B5.5).</summary>
    public static bool IsUnsatisfiableForcedChoice(ChoiceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Type != ChoiceType.Count
            && !request.CanSkip
            && request.MinCount > 1
            && !HasCompletableSelection(request);
    }

    /// <summary>Whether the bounded deterministic search finds at least one selection the request would
    /// accept at confirm time. Validator-less requests reduce to the pure count gate (AS-IS
    /// <c>ValidCards.Count &gt;= maxCount</c> — 후보 부족이면 조합 탐색 없이 즉시 불충족, §B5.6).</summary>
    public static bool HasCompletableSelection(ChoiceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        HeadlessEntityId[] selectable = request.Candidates
            .Where(candidate => candidate.IsSelectable)
            .Select(candidate => candidate.Id)
            .ToArray();

        if (selectable.Length < request.MinCount)
        {
            return false;
        }

        if (request.SelectionValidator is null)
        {
            return true;
        }

        return TryFindPassingSelection(
            selectable, request.MinCount, request.MaxCount, request.SelectionValidator, out _);
    }

    /// <summary>The single copy of the RD-R4P4-02 deterministic bounded search (loop structure 1:1 with the
    /// former ScriptedChoiceProvider.CreateFallbackChoice :89-105, which now delegates here — B5-2 리스크 2
    /// 통합): sizes from min(<paramref name="maxCount"/>, selectable) down to <paramref name="minCount"/>,
    /// lexicographic combinations in candidate order within a size, capped at 200 validator evaluations.
    /// Returns the FIRST passing selection (the provider's auto-pick) or false when none passes within the
    /// cap (the completability verdict) — both consumers judge identically by construction.</summary>
    public static bool TryFindPassingSelection(
        IReadOnlyList<HeadlessEntityId> selectable,
        int minCount,
        int maxCount,
        Func<IReadOnlyList<HeadlessEntityId>, bool> validator,
        out HeadlessEntityId[]? selection)
    {
        ArgumentNullException.ThrowIfNull(selectable);
        ArgumentNullException.ThrowIfNull(validator);

        HeadlessEntityId[] pool = selectable as HeadlessEntityId[] ?? selectable.ToArray();
        int tries = 0;
        for (int size = Math.Min(maxCount, pool.Length); size >= minCount && tries < 200; size--)
        {
            foreach (HeadlessEntityId[] combination in Combinations(pool, size))
            {
                if (++tries > 200)
                {
                    break;
                }

                if (validator(combination))
                {
                    selection = combination;
                    return true;
                }
            }
        }

        selection = null;
        return false;
    }

    /// <summary>Lexicographic k-combinations of <paramref name="items"/> in candidate order (deterministic —
    /// the substrate translation of the AS-IS AI's bounded random subset retry).</summary>
    private static IEnumerable<HeadlessEntityId[]> Combinations(HeadlessEntityId[] items, int size)
    {
        if (size < 0 || size > items.Length)
        {
            yield break;
        }

        int[] indexes = Enumerable.Range(0, size).ToArray();
        while (true)
        {
            yield return indexes.Select(index => items[index]).ToArray();

            int position = size - 1;
            while (position >= 0 && indexes[position] == items.Length - size + position)
            {
                position--;
            }

            if (position < 0)
            {
                yield break;
            }

            indexes[position]++;
            for (int next = position + 1; next < size; next++)
            {
                indexes[next] = indexes[next - 1] + 1;
            }
        }
    }
}
