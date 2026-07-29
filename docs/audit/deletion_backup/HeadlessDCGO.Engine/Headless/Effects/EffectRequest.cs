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

using HeadlessDCGO.Engine.Headless.Services;

public sealed record EffectRequest
{
    public EffectRequest(
        HeadlessEntityId effectId,
        HeadlessPlayerId controllerId,
        string timing,
        EffectContext context)
    {
        if (effectId.IsEmpty)
        {
            throw new ArgumentException("Effect id must not be empty.", nameof(effectId));
        }

        if (controllerId.IsEmpty)
        {
            throw new ArgumentException("Effect controller id must not be empty.", nameof(controllerId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(timing);
        ArgumentNullException.ThrowIfNull(context);

        EffectId = effectId;
        ControllerId = controllerId;
        Timing = timing.Trim();
        Context = context;
    }

    public HeadlessEntityId EffectId { get; }

    public HeadlessPlayerId ControllerId { get; }

    public string Timing { get; }

    public EffectContext Context { get; }
}
