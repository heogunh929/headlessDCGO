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
/// ScriptedChoiceProvider itself is deliberately untouched in B5-1 (배치 핀: provider 층 무접촉 —
/// RD-R4P4-02 witness ST1_15 불변); consolidating the two copies of the search rides B5-2's provider-layer
/// wiring.
///
/// B5-1 boundary: NO runtime call site — wiring the demotion into the dispatcher/controller changes
/// behavior (a today-stalling forced request would start resolving) and is B5-2 scope, together with the
/// <see cref="Runtime.HeadlessActionParameterKeys.UnsatisfiableForcedChoice"/> action-metadata marking.</summary>
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

        // Loop structure kept 1:1 with ScriptedChoiceProvider.CreateFallbackChoice :89-105 (the
        // RD-R4P4-02 translation) so both surfaces judge "a passing set exists within 200 evaluations"
        // identically — same order, same cap accounting.
        int tries = 0;
        for (int size = Math.Min(request.MaxCount, selectable.Length); size >= request.MinCount && tries < 200; size--)
        {
            foreach (HeadlessEntityId[] combination in Combinations(selectable, size))
            {
                if (++tries > 200)
                {
                    break;
                }

                if (request.SelectionValidator(combination))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Lexicographic k-combinations of <paramref name="items"/> in candidate order (deterministic —
    /// the substrate translation of the AS-IS AI's bounded random subset retry). Same enumeration as
    /// ScriptedChoiceProvider.Combinations :112-141 (kept duplicated until the B5-2 provider-layer pass —
    /// see the class remarks).</summary>
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
