// ============================================================================
// ⛔ DELETION-TARGET · DO-NOT-REFERENCE
// 원장(docs/audit/filelist/merged_files_no_cards.csv): 삭제대상여부=Y · 결함여부=Y
// 분류: substrate 오배치(other)
// 미러 원가(재이관 대상): DCGO/Assets/Scripts/Script/CardEffectCommons/GetFromHashtable.cs::IsDPZeroDelete(hashtable) + DP<=0 sweep(CutInProcess.cs); DPZero 마킹 소비=CardController.IsDPZeroDelete@GetFromHashtable.cs:374(IsDPZeroD
// 이 파일은 AS-IS 원본에 동일-경로 대응이 없는 오배치/발명 코드다.
// 규칙 로직은 위 미러 원가로 재이관 후 이 파일은 삭제 예정.
// 서브에이전트/포팅 작업 시: 이 파일의 심볼을 참조·모방·확장하지 말 것.
// ============================================================================
namespace HeadlessDCGO.Engine.Headless.Runtime;

/// <summary>
/// (F-8.5) DP-zero deletion metadata keys: a battle-area Digimon deleted because its RESOLVED DP
/// (printed + continuous modifiers) reached 0 or less is stamped <c>DPZero</c> on its metadata so an
/// <c>IsDpZeroDelete</c> condition can distinguish it from a battle/effect deletion. The DP-zero sweep
/// itself is carried by <c>GameFlowProcessor.StateBasedDeletionSweepAsync</c>.
/// </summary>
public static class DpZeroDeletionHelpers
{
    public const string DpKey = "dp";
    public const string DpZeroKey = "DPZero";
    public const string DeletedByEffectKey = "deletedByEffect";
}
