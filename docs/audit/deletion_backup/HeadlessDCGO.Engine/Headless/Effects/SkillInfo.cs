// ============================================================================
// ⛔ DELETION-TARGET · DO-NOT-REFERENCE
// 원장(docs/audit/filelist/merged_files_no_cards.csv): 삭제대상여부=Y · 결함여부=Y
// 분류: substrate 오배치(other)
// 미러 원가(재이관 대상): Script/SkillInfo.cs::SkillInfo (구조 상이)@SkillInfo.cs:3-14
// 이 파일은 AS-IS 원본에 동일-경로 대응이 없는 오배치/발명 코드다.
// 규칙 로직은 위 미러 원가로 재이관 후 이 파일은 삭제 예정.
// 서브에이전트/포팅 작업 시: 이 파일의 심볼을 참조·모방·확장하지 말 것.
// ============================================================================
namespace HeadlessDCGO.Engine.Headless.Effects;

using System.Collections.ObjectModel;
using HeadlessDCGO.Engine.Headless.Services;

public sealed record SkillInfo
{
    public SkillInfo(
        CardEffectDefinition definition,
        EffectRequest request,
        EffectResolutionMode mode = EffectResolutionMode.MainStack,
        int priority = 0,
        long sequence = 0,
        IReadOnlyDictionary<string, object?>? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(request);

        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), "SkillInfo mode must be a known value.");
        }

        if (sequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), "SkillInfo sequence must not be negative.");
        }

        if (request.EffectId != definition.EffectId)
        {
            throw new ArgumentException("SkillInfo request effect id must match the effect definition.", nameof(request));
        }

        if (request.Context.SourceEntityId != definition.SourceEntityId)
        {
            throw new ArgumentException("SkillInfo request source entity id must match the effect definition.", nameof(request));
        }

        if (!string.Equals(request.Timing, definition.Timing, StringComparison.Ordinal))
        {
            throw new ArgumentException("SkillInfo request timing must match the effect definition timing.", nameof(request));
        }

        Definition = definition;
        Request = request;
        Mode = mode;
        Priority = priority;
        Sequence = sequence;
        Metadata = CopyMetadata(metadata);
    }

    public CardEffectDefinition Definition { get; }

    public EffectRequest Request { get; }

    public EffectResolutionMode Mode { get; }

    public int Priority { get; }

    public long Sequence { get; }

    public IReadOnlyDictionary<string, object?> Metadata { get; }

    public HeadlessEntityId EffectId => Definition.EffectId;

    public HeadlessEntityId SourceEntityId => Definition.SourceEntityId;

    public HeadlessPlayerId ControllerId => Request.ControllerId;

    public string Timing => Definition.Timing;

    public EffectContext Context => Request.Context;

    public bool IsOptional => Definition.IsOptional;

    public bool IsBackgroundProcess => Definition.IsBackgroundProcess;

    public int? MaxCountPerTurn => Definition.MaxCountPerTurn;

    public string? Hash => Definition.Hash;

    public PendingEffect ToPendingEffect()
    {
        return new PendingEffect(Request, Mode);
    }

    // (④) ToBinding(...) DELETED — lowered SkillInfo to the invented EffectBinding (registry producer 0).

    public SkillInfo WithMetadata(IReadOnlyDictionary<string, object?> metadata)
    {
        return new SkillInfo(Definition, Request, Mode, Priority, Sequence, metadata);
    }

    // (④) FromEffect(IHeadlessCardEffect, ...) DELETED — the invented scheduler-effect contract is gone and this
    // factory had no src caller.

    private static IReadOnlyDictionary<string, object?> CopyMetadata(
        IReadOnlyDictionary<string, object?>? metadata)
    {
        if (metadata is null)
        {
            return new ReadOnlyDictionary<string, object?>(
                new Dictionary<string, object?>(StringComparer.Ordinal));
        }

        var copy = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, object?> pair in metadata)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                throw new ArgumentException("SkillInfo metadata keys must not be null or whitespace.", nameof(metadata));
            }

            copy[pair.Key.Trim()] = pair.Value;
        }

        return new ReadOnlyDictionary<string, object?>(copy);
    }
}
