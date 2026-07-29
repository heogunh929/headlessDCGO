// ============================================================================
// ⛔ DELETION-TARGET · DO-NOT-REFERENCE
// 원장(docs/audit/filelist/merged_files_no_cards.csv): 삭제대상여부=Y · 결함여부=Y
// 분류: substrate 오배치(other)
// 미러 원가(재이관 대상): Script/SkillInfo.cs::SkillInfo (loose analog)@SkillInfo.cs:3
// 이 파일은 AS-IS 원본에 동일-경로 대응이 없는 오배치/발명 코드다.
// 규칙 로직은 위 미러 원가로 재이관 후 이 파일은 삭제 예정.
// 서브에이전트/포팅 작업 시: 이 파일의 심볼을 참조·모방·확장하지 말 것.
// ============================================================================
namespace HeadlessDCGO.Engine.Headless.Effects;

public sealed record PendingEffect
{
    public PendingEffect(
        EffectRequest request,
        EffectResolutionMode mode)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), "Effect resolution mode must be a known value.");
        }

        Request = request;
        Mode = mode;
    }

    public EffectRequest Request { get; }

    public EffectResolutionMode Mode { get; }
}
