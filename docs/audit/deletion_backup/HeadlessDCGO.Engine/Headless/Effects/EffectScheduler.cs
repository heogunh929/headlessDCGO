// ============================================================================
// ⛔ DELETION-TARGET · DO-NOT-REFERENCE
// 원장(docs/audit/filelist/merged_files_no_cards.csv): 삭제대상여부=Y · 결함여부=Y
// 분류: substrate 오배치(other)
// 미러 원가(재이관 대상): 무대응(AS-IS 없음)
// 이 파일은 AS-IS 원본에 동일-경로 대응이 없는 오배치/발명 코드다.
// 규칙 로직은 위 미러 원가로 재이관 후 이 파일은 삭제 예정.
// 서브에이전트/포팅 작업 시: 이 파일의 심볼을 참조·모방·확장하지 말 것.
// ============================================================================
namespace HeadlessDCGO.Engine.Headless.Effects;

public sealed class EffectScheduler
{
    private readonly EffectResolutionQueue _queue;
    private readonly Func<EffectRequest, CancellationToken, Task<EffectResult>> _resolver;

    public EffectScheduler()
        : this(new EffectResolutionQueue())
    {
    }

    public EffectScheduler(
        EffectResolutionQueue queue,
        Func<EffectRequest, CancellationToken, Task<EffectResult>>? resolver = null)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _resolver = resolver ?? DefaultResolverAsync;
    }

    public bool HasPendingEffects => _queue.Count > 0;

    public int PendingCount => _queue.Count;

    public int TotalEnqueuedCount { get; private set; }

    public int TotalResolvedCount { get; private set; }

    public int LastResolvedCount { get; private set; }

    // G3.5-RL-B3: how many resolutions had no bound effect body (skeleton coverage gaps).
    public int TotalUnboundCount { get; private set; }

    public void Enqueue(EffectRequest request)
    {
        Enqueue(request, EffectResolutionMode.Unknown);
    }

    public void Enqueue(
        EffectRequest request,
        EffectResolutionMode mode)
    {
        ArgumentNullException.ThrowIfNull(request);
        _queue.Enqueue(new PendingEffect(request, mode));
        TotalEnqueuedCount++;
    }

    public void Clear()
    {
        _queue.Clear();
        TotalEnqueuedCount = 0;
        TotalResolvedCount = 0;
        LastResolvedCount = 0;
        TotalUnboundCount = 0;
    }

    public async Task<EffectResult> ResolveNextAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_queue.TryPeek(out PendingEffect? effect) || effect is null)
        {
            LastResolvedCount = 0;
            return EffectResult.Failure("No pending effects.");
        }

        EffectResult result;
        try
        {
            result = await _resolver(effect.Request, cancellationToken).ConfigureAwait(false)
                ?? CreateFailure(effect, "Effect resolver returned a null result.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            result = CreateFailure(
                effect,
                "Effect resolver failed.",
                new Dictionary<string, object?>
                {
                    ["error"] = ex.Message,
                    ["errorType"] = ex.GetType().Name,
                });
        }

        // (RD-10) A FIZZLE (resolution-time gate failed) is dequeued and skipped so it never wedges the queue
        // (AS-IS skip/continue); a real Failure/Suspended is left at the head (parked for diagnostics / the
        // agent's answer). Both report Resolved=false, so distinguish by the Skipped status.
        if (result.IsSkipped)
        {
            _queue.TryDequeue(out _);
            LastResolvedCount = 0;
            return result;
        }

        if (!result.Resolved)
        {
            LastResolvedCount = 0;
            return result;
        }

        if (!_queue.TryDequeue(out PendingEffect? resolvedEffect)
            || resolvedEffect is null
            || !ReferenceEquals(effect, resolvedEffect))
        {
            LastResolvedCount = 0;
            return CreateFailure(effect, "Effect queue head changed before resolution completed.");
        }

        TotalResolvedCount++;
        if (result.IsUnbound)
        {
            TotalUnboundCount++;
        }

        LastResolvedCount = 1;
        return result;
    }

    public async Task<IReadOnlyList<EffectResult>> ResolveAllAsync(CancellationToken cancellationToken = default)
    {
        List<EffectResult> results = new();
        int resolvedCount = 0;

        while (HasPendingEffects)
        {
            EffectResult result = await ResolveNextAsync(cancellationToken).ConfigureAwait(false);
            results.Add(result);
            // (RD-10) a Skipped effect was dequeued (fizzle) — keep draining the rest of the window. A real
            // Failure/Suspended still stops the drain (parked at the head).
            if (result.IsSkipped)
            {
                continue;
            }

            if (!result.Resolved)
            {
                break;
            }

            resolvedCount++;
        }

        LastResolvedCount = resolvedCount;
        return results;
    }

    private static Task<EffectResult> DefaultResolverAsync(
        EffectRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(EffectResult.Success());
    }

    private static EffectResult CreateFailure(
        PendingEffect effect,
        string message,
        IReadOnlyDictionary<string, object?>? values = null)
    {
        var mergedValues = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["effectId"] = effect.Request.EffectId.Value,
            ["mode"] = effect.Mode.ToString(),
        };

        if (values is not null)
        {
            foreach (KeyValuePair<string, object?> pair in values)
            {
                mergedValues[pair.Key] = pair.Value;
            }
        }

        return EffectResult.Failure(message, mergedValues);
    }
}
