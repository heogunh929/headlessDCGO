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

public sealed class EffectResolutionQueue
{
    private readonly Queue<PendingEffect> _effects = new();

    public int Count => _effects.Count;

    public void Enqueue(PendingEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        _effects.Enqueue(effect);
    }

    public bool TryPeek(out PendingEffect? effect)
    {
        if (_effects.Count == 0)
        {
            effect = null;
            return false;
        }

        effect = _effects.Peek();
        return true;
    }

    public bool TryDequeue(out PendingEffect? effect)
    {
        if (_effects.Count == 0)
        {
            effect = null;
            return false;
        }

        effect = _effects.Dequeue();
        return true;
    }

    public IReadOnlyList<PendingEffect> Snapshot()
    {
        return Array.AsReadOnly(_effects.ToArray());
    }

    public int Clear()
    {
        int removedCount = _effects.Count;
        _effects.Clear();
        return removedCount;
    }
}
