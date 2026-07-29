// ============================================================================
// ⛔ DELETION-TARGET · DO-NOT-REFERENCE
// 원장(docs/audit/filelist/merged_files_no_cards.csv): 삭제대상여부=Y · 결함여부=Y
// 분류: substrate 오배치(other)
// 미러 원가(재이관 대상): Script/CardEffectCommons/GiveEffect/GiveEffectToPermanentOrPlayer.cs;Script/ICardEffect.cs;Script/AttackProcess.cs::GiveEffectToPermanentOrPlayer.AddEffectToPlayer/AddEffectToPermanent · ICardEffect.I
// 이 파일은 AS-IS 원본에 동일-경로 대응이 없는 오배치/발명 코드다.
// 규칙 로직은 위 미러 원가로 재이관 후 이 파일은 삭제 예정.
// 서브에이전트/포팅 작업 시: 이 파일의 심볼을 참조·모방·확장하지 말 것.
// ============================================================================
namespace HeadlessDCGO.Engine.Headless.Effects;

/// <summary>
/// (RC-6) CONST HOLDER. This type was the invented registry trigger-reader: it queried
/// <c>IEffectQueryService.GetEffectsForTiming</c> over the EffectRegistry to collect the triggered effects a
/// game event opened, filtered/enqueued them, and reclassified their kind. That whole query half is EXCISED —
/// no real card ever produced a trigger/effect-payload binding (producer 0), the type had no production
/// instantiation, and its only feed was two now-retargeted test fixtures. The live trigger pipeline collects
/// through the new-model SkillInfo scan (AutoProcessing.GetSkillInfos), not this reader.
///
/// The const KEYS below survive as LIVE substrate: TriggerTimingMap / TriggerEventEmitter /
/// MatchStateMutationSink / GameFlowProcessor read the event-metadata keys (TriggerTimingKey / TimingKey /
/// EffectTimingKey / SourceEntityIdKey / CardIdKey / SurviveOwnLeaveKey / DelayedOneShotKey / …). They remain
/// housed here so those call sites keep their canonical key names.
/// </summary>
public static class AutoProcessingTriggerCollector
{
    /// <summary>(PRIM-P0 B.O.5) marks a delayed one-shot player effect (AS-IS AddEffectToPlayer): it fires once
    /// at its timing then GameFlowProcessor removes its binding after resolution (fire-then-clear).</summary>
    public const string DelayedOneShotKey = "delayedOneShot";

    /// <summary>(PRIM-P0 B.O.5-tail) marks a granted effect that must SURVIVE its own source card leaving play —
    /// a self-[On Deletion] grant (AS-IS temp AddEffectToPermanent) whose whole point is to fire ON the target's
    /// removal. The leave-play cleanup (CardLeavePlayCleanup.OnLeftPlay + the sink's delete path) skips these so
    /// the grant is still registered when OnDeletion resolves; it is then removed via <see cref="DelayedOneShotKey"/>
    /// (fire-then-clear), with the grant's EffectDuration as the backstop for a non-deletion departure.</summary>
    public const string SurviveOwnLeaveKey = "surviveOwnLeave";
    public const string TriggerTimingKey = "triggerTiming";
    public const string TimingKey = "timing";
    public const string EffectTimingKey = "effectTiming";
    public const string ResolutionModeKey = "resolutionMode";
    public const string TriggerKindKey = "triggerKind";
    public const string PriorityKey = "priority";
    public const string PlayerIdKey = "playerId";
    public const string SourceEntityIdKey = "sourceEntityId";
    public const string TargetEntityIdKey = "targetEntityId";
    public const string CardIdKey = "cardId";

    /// <summary>(P5) event-metadata key selecting a counter-timing PASS: <c>"regular"</c> collects only
    /// effects WITHOUT the [Counter] marker, <c>"counter"</c> only marked ones (AS-IS AttackProcess
    /// CounterTiming's two ordered passes over EffectTiming.OnCounterTiming split by IsCounterEffect).
    /// Absent = no filtering (non-attack counter windows behave as before).</summary>
    public const string CounterPassKey = "counterPass";
    public const string CounterPassRegular = "regular";
    public const string CounterPassCounter = "counter";

    /// <summary>(P5) binding-values marker mirroring AS-IS <c>ICardEffect.IsCounterEffect</c> — a true
    /// [Counter] effect (resolves in the second counter pass).</summary>
    public const string IsCounterEffectKey = "counter.isCounterEffect";

    /// <summary>(PRIM-P0 AddSkill) marks a player-scope TRIGGER GRANT (AS-IS AddSkillClass whose getEffects
    /// splices a TRIGGERED activated effect onto a live-matched set).</summary>
    public const string TriggerGrantKey = "triggerGrant";
}
