// ============================================================================
// ⛔ DELETION-TARGET · DO-NOT-REFERENCE
// 원장(docs/audit/filelist/merged_files_no_cards.csv): 삭제대상여부=Y · 결함여부=Y
// 분류: substrate 오배치(other)
// 미러 원가(재이관 대상): DCGO/Assets/Scripts/Script/Permanent.cs::Permanent.BaseDP (getter) / Permanent.GetDP@193/327
// 이 파일은 AS-IS 원본에 동일-경로 대응이 없는 오배치/발명 코드다.
// 규칙 로직은 위 미러 원가로 재이관 후 이 파일은 삭제 예정.
// 서브에이전트/포팅 작업 시: 이 파일의 심볼을 참조·모방·확장하지 말 것.
// ============================================================================
namespace HeadlessDCGO.Engine.Headless.State;

/// <summary>
/// Computes a permanent's effective DP from its base (top-card) DP and a set of typed
/// <see cref="DpModifier"/>s, faithfully mirroring the original <c>Permanent.BaseDP</c> accumulation
/// (G3.5-RL-B1):
/// <list type="number">
/// <item>start from the base (printed) DP;</item>
/// <item>apply all <see cref="DpModifierKind.Relative"/> up/down deltas (summed);</item>
/// <item>apply <see cref="DpModifierKind.Absolute"/> "set" modifiers in <see cref="DpModifier.ActivatedOrder"/>
/// order — each replaces the value, so the last activated set wins;</item>
/// <item>clamp the result at zero.</item>
/// </list>
/// </summary>
public static class DpCalculator
{
    public static int ComputeDp(int baseDp, IEnumerable<DpModifier> modifiers)
    {
        ArgumentNullException.ThrowIfNull(modifiers);

        DpModifier[] all = modifiers as DpModifier[] ?? modifiers.ToArray();

        int dp = baseDp;

        foreach (DpModifier modifier in all)
        {
            if (modifier.IsRelative)
            {
                dp += modifier.Value;
            }
        }

        foreach (DpModifier modifier in all
                     .Where(modifier => modifier.IsAbsolute)
                     .OrderBy(modifier => modifier.ActivatedOrder))
        {
            dp = modifier.Value;
        }

        return dp < 0 ? 0 : dp;
    }
}
