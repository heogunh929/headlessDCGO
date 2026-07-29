// ============================================================================
// ⛔ DELETION-TARGET · DO-NOT-REFERENCE
// 원장(docs/audit/filelist/merged_files_no_cards.csv): 삭제대상여부=Y · 결함여부=Y
// 분류: substrate 오배치(other)
// 미러 원가(재이관 대상): DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Progress.cs; DCGO/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/Progress.cs::CardEffectCommons.ProgressProcess (공격중 상대효과 면역 CanNotA
// 이 파일은 AS-IS 원본에 동일-경로 대응이 없는 오배치/발명 코드다.
// 규칙 로직은 위 미러 원가로 재이관 후 이 파일은 삭제 예정.
// 서브에이전트/포팅 작업 시: 이 파일의 심볼을 참조·모방·확장하지 말 것.
// ============================================================================
namespace HeadlessDCGO.Engine.Headless.Runtime;

using System.Collections;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (C-15 Progress, S2) While a Progress Digimon attacks, it is not affected by the opponent's effects
/// (UntilEndAttack). Mirrors AS-IS <c>ProgressProcess</c> (KeyWordEffects/Progress.cs:62): a
/// <c>CanNotAffectedClass</c> with CardCondition = the attacker's top card, SkillCondition =
/// <c>IsOpponentEffect</c>, appended to the attacker's <c>UntilEndAttackEffects</c> bucket. It is a PASSIVE
/// static effect (no agent choice), so it is applied automatically when the Progress Digimon's attack is
/// declared. The immunity is read back LIVE by <c>CardSource.CanNotBeAffected</c> (the AS-IS
/// <c>Permanent.EffectList(None)</c> scan) and expires when <see cref="AttackProcess.Cleanup"/> resets the
/// UntilEndAttack bucket (AS-IS Cleanup :489-495).
/// </summary>
// (R3-W3c-2) PRODUCER FLIP: the attack-time Progress immunity is no longer registry.Register'd (the invented
// ContinuousImmunityGate binding) — it is now stored in the AS-IS UntilEndAttackEffects bucket, exactly as the
// mirror ProgressProcess does (RD-R2-01 already resolved: the W3 bucket is live and read by CanNotBeAffected).
// This drops the sole producer of the registry "ContinuousImmunity" scope (the CanNotAffectedStaticEffect
// factory flipped test-only in W3c-1), so BlocksOpponentEffect's registry read is now always-false; the AS-IS
// enforcement is the live CanNotBeAffected scan at the CardController / Commons sites.
public static class ProgressImmunity
{
    public const string HasProgressKey = "hasProgress";

    // (R3-W3c-2) per-attack dedup marker: TryRegister is called from AttackProcess.CounterTiming, which re-enters
    // across the cut-in drain loop; AS-IS ProgressProcess grants exactly once. The flag is cleared with the bucket
    // at attack end (AttackProcess.Cleanup).
    public const string AppliedKey = "progress:immunityApplied";

    /// <summary>Grants the opponent-only immunity on the current attacker if it has Progress (and not already
    /// granted this attack) by appending a <see cref="CanNotAffectedClass"/> to its UntilEndAttack bucket. No-op
    /// otherwise. Auto-applied — Progress is a passive static effect.</summary>
    public static void TryRegister(EngineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        HeadlessAttackState attack = context.AttackController.Current;
        if (!attack.IsPending ||
            attack.AttackerId is not HeadlessEntityId attackerId ||
            attack.AttackingPlayerId is not HeadlessPlayerId owner ||
            !HasProgress(context, attackerId))
        {
            return;
        }

        if (!context.CardInstanceRepository.TryGetInstance(attackerId, out CardInstanceRecord? attacker) || attacker is null)
        {
            return;
        }

        if (ReadFlag(attacker.Metadata, AppliedKey))
        {
            return;   // already granted this attack (dedup across the CounterTiming cut-in drain loop)
        }

        // AS-IS ProgressProcess (KeyWordEffects/Progress.cs:62-108), 1:1: while this Digimon attacks, append the
        // UntilEndAttack "Isn't affected by opponent's Digimon's effect" CanNotAffectedClass to the attacker's
        // UntilEndAttackEffects bucket (its CardCondition = the attacker's top card, SkillCondition =
        // IsOpponentEffect). The VFX terminal (CreateBuffEffect) is stripped as elsewhere.
        var selectedPermanent = new Permanent(context, attackerId, owner);
        CardSource cardSource = selectedPermanent.TopCard;

        var canNotAffectedClass = new CanNotAffectedClass();
        canNotAffectedClass.SetUpICardEffect("Isn't affected by opponent's Digimon's effect", CanUseCondition1, cardSource);
        canNotAffectedClass.SetUpCanNotAffectedClass(CardCondition: CardCondition, SkillCondition: SkillCondition);
        selectedPermanent.UntilEndAttackEffects.Add((_timing) => canNotAffectedClass);

        SetFlag(context, attackerId, AppliedKey);

        bool CanUseCondition1(Hashtable hashtable)
        {
            return CardEffectCommons.IsPermanentExistsOnBattleArea(selectedPermanent);
        }

        bool CardCondition(CardSource innerCardSource)
        {
            if (CardEffectCommons.IsPermanentExistsOnBattleArea(selectedPermanent))
            {
                if (innerCardSource == selectedPermanent.TopCard)
                {
                    return true;
                }
            }

            return false;
        }

        bool SkillCondition(ICardEffect cardEffect)
        {
            if (cardEffect != null)
            {
                if (cardEffect.EffectSourceCard != null)
                {
                    if (CardEffectCommons.IsOpponentEffect(cardEffect.EffectSourceCard, cardSource))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }

    /// <summary>(R3-W3c-2) Clears the per-attack dedup marker — called by <see cref="AttackProcess.Cleanup"/>
    /// alongside the UntilEndAttack bucket reset, so a subsequent attack re-grants.</summary>
    public static void ClearApplied(EngineContext context, HeadlessEntityId attackerId)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (attackerId.IsEmpty
            || !context.CardInstanceRepository.TryGetInstance(attackerId, out CardInstanceRecord? attacker)
            || attacker is null
            || !ReadFlag(attacker.Metadata, AppliedKey))
        {
            return;
        }

        var metadata = new Dictionary<string, object?>(attacker.Metadata, StringComparer.Ordinal);
        metadata.Remove(AppliedKey);
        context.CardInstanceRepository.Upsert(attacker with { Metadata = metadata });
    }

    private static void SetFlag(EngineContext context, HeadlessEntityId cardId, string key)
    {
        if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
        {
            return;
        }

        var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal) { [key] = true };
        context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
    }

    private static bool HasProgress(EngineContext context, HeadlessEntityId attackerId)
    {
        if (!context.CardInstanceRepository.TryGetInstance(attackerId, out CardInstanceRecord? attacker) || attacker is null)
        {
            return false;
        }

        if (ReadFlag(attacker.Metadata, HasProgressKey)
            || ContinuousKeywordGate.HasKeyword(context, attackerId, ContinuousKeywordGate.Progress)) // GR-005 C-group seal
        {
            return true;
        }

        return context.CardRepository.TryGetCard(attacker.DefinitionId, out CardRecord? card) &&
            card is not null &&
            ReadFlag(card.Metadata, HasProgressKey);
    }

    private static bool ReadFlag(IReadOnlyDictionary<string, object?> metadata, string key) =>
        metadata.TryGetValue(key, out object? raw) && raw is bool value && value;
}
