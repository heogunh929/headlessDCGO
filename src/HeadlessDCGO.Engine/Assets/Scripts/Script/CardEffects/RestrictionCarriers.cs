// ============================================================================
// ⛔ DELETION-TARGET · DO-NOT-REFERENCE
// 원장(docs/audit/filelist/merged_files_no_cards.csv): 삭제대상여부=Y · 결함여부=Y
// 분류: 미러발명(AS-IS 무대응)
// 미러 원가(재이관 대상): (미확정)
// 이 파일은 AS-IS 원본에 동일-경로 대응이 없는 오배치/발명 코드다.
// 규칙 로직은 위 미러 원가로 재이관 후 이 파일은 삭제 예정.
// 서브에이전트/포팅 작업 시: 이 파일의 심볼을 참조·모방·확장하지 말 것.
// ============================================================================
// (C3 REHOUSED) The two LIVE joint-carrier restriction effects were relocated here verbatim from
// CardEffectCommons/ContinuousAndRestrictionEffects.cs to sit in the CardEffects/ kind-class neighborhood,
// beside their AS-IS analogues CanNotMoveClass.cs / CanNotSelectBySkillClass.cs. Same namespace as those
// kind-classes (...CardEffects); their sole constructor site (CardEffectFactory) already imports it, so the
// bodies below are byte-identical to the pre-move source. These carry the AS-IS JOINT predicate (NOT split into
// scope + causing) as a single runtime Func — true-scan-for-joint-predicates.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using System.Collections;
using System.Collections.Generic;
using System;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

/// <summary>(d-remediation, true-scan) AS-IS <c>ICanNotSelectBySkillEffect</c> / <c>Permanent.CanSelectBySkill</c>:
/// a continuous effect that, when SCANNED over every field permanent's effects, forbids a candidate from being
/// CHOSEN by a skill. Carries the AS-IS JOINT predicate <c>CanNotSelectBySkill(candidate, skillSource)</c> as a
/// single runtime Func (NOT split into scope + causing) so a non-separable predicate is preserved, exactly like
/// the original's <c>foreach permanent … effect.CanNotSelectBySkill(this, skill)</c> loop.</summary>
public sealed class CanNotSelectBySkillEffect : ICardEffect, ICanNotSelectBySkillEffect
{
    private readonly Func<CardSource, CardSource, bool> _predicate;
    private readonly Func<bool>? _condition;

    public CanNotSelectBySkillEffect(CardSource card, Func<CardSource, CardSource, bool> predicate, Func<bool>? condition)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(predicate);
        Card = card;
        _predicate = predicate;
        _condition = condition;
        // (R3-W3c-4c D-1) wire the base ICardEffect so the LIVE getter Permanent.CanSelectBySkill can consult
        // this effect over a permanent's EffectList(None) (its scan gates on cardEffect.CanUse(null), which
        // returns false unless a CanUseCondition is set). Mirrors the AS-IS SetUpICardEffect construction.
        SetUpICardEffect("Can't be selected by skill", h => _condition is null || _condition(), card);
        SetNotShowUI(true);
    }

    public CardSource Card { get; }

    /// <summary>(R3-W3c-4c D-1 joint↔separate reconciliation) AS-IS
    /// <c>ICanNotSelectBySkillEffect.CanNotSelectBySkill(permanent, cardEffect)</c>: the AS-IS kind-class ANDs a
    /// SEPARATE <c>PermanentCondition(permanent)</c> and <c>CardEffectCondition(cardEffect)</c>. The headless
    /// carrier instead holds the (possibly non-separable) JOINT predicate <c>f(candidate, skillSource)</c> as a
    /// single Func (memory: scope+causing must NOT be split). This adapter maps the AS-IS interface args onto the
    /// joint — the candidate = <c>permanent.TopCard</c>, the skill source = <c>cardEffect.EffectSourceCard</c> —
    /// and reproduces the AS-IS non-null guards verbatim (both the permanent's top and the causing effect's source
    /// must exist), so no card that AS-IS would treat as untargetable is missed and none is over-blocked.</summary>
    public bool CanNotSelectBySkill(Permanent permanent, ICardEffect cardEffect)
    {
        if (permanent?.TopCard is not null && cardEffect?.EffectSourceCard is not null)
        {
            return _predicate(permanent.TopCard, cardEffect.EffectSourceCard);
        }

        return false;
    }

    // (이연④-e, substrate reclassification) The old-model registry-WRITE (`EffectBinding ToBinding(string)`) was
    // DELETED. It wrote the `JointRestrictionEffect.PredicateKey(CannotBeSelectedBySkillKey)` binding, but that key
    // has ZERO readers: `CannotBeSelectedBySkillKey` is never passed to `RestrictionScan.IsRestricted` — the
    // select-by-skill restriction was rehoused (R1-d) onto the live AS-IS interface scan
    // `Permanent.CanSelectBySkill` / `SelectPermanentEffect`, which enumerates the `ICanNotSelectBySkillEffect`
    // this class implements (over a permanent's `EffectList(None)`; see `SetUpICardEffect` in the ctor). With no
    // registry reader, the registry-half was 사문 (dead-letter), so removing it registers NOTHING — the class is a
    // pure kind-class-style substrate effect, live only through the interface (④-a ToBinding-removal precedent).
}


/// <summary>(d-remediation, true-scan) AS-IS <c>ICanNotMoveEffect</c> / <c>Permanent.CanMove</c>: SCANNED over every
/// field permanent's effects; while any usable one's predicate <c>CanNotMove(candidate, causing)</c> holds, the
/// candidate cannot move (the move gate passes a null causing effect, AS-IS <c>CanNotMove(TopCard, null)</c>).
///
/// (이연④-f, registry-half retired) LIVE-only now. Formerly DUAL: the <c>ToBinding</c> registry-half fed the
/// breeding→battle move gate (BT1_089) via <c>RestrictionScan.IsRestricted(CannotMoveKey)</c>. That consumer was
/// re-pointed to the AS-IS interface-scan half (<c>Permanent.CanMove</c> — the same <c>permanent.CanMove</c> the
/// AS-IS BT1_089:56 uses), so nothing reads the CannotMoveKey binding any more. The <c>ToBinding</c> is dropped:
/// with no <c>ToBinding</c> method the <see cref="LegacyBindingBridge"/> reflective lowering returns false and the
/// registrar leaves this effect out of the registry entirely — the interface half (this effect sits on the card's
/// live EffectList and implements <see cref="ICanNotMoveEffect"/>) is the sole path, feeding <c>Permanent.CanMove</c>
/// exactly as AS-IS <c>CanNotMoveClass</c> does. Its joint predicate <c>f(candidate, causingSource)</c> is retained
/// (the joint carrier the fixture/factory build via a single Func; not splittable into the kind-class's separate
/// cardCondition/cardEffectCondition in general — true-scan-for-joint-predicates).</summary>
public sealed class CanNotMoveEffect : ICardEffect, ICanNotMoveEffect
{
    private readonly Func<CardSource, CardSource?, bool> _predicate;
    private readonly Func<bool>? _condition;

    public CanNotMoveEffect(CardSource card, Func<CardSource, CardSource?, bool> predicate, Func<bool>? condition)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(predicate);
        Card = card;
        _predicate = predicate;
        _condition = condition;
        // (R3-W3c-4c D-1) wire the base ICardEffect so the LIVE getter Permanent.CanMove can consult this effect
        // over a permanent's EffectList(None) (its scan gates on cardEffect.CanUse(null)). AS-IS SetUpICardEffect.
        SetUpICardEffect("Can't move", h => _condition is null || _condition(), card);
        SetNotShowUI(true);
    }

    public CardSource Card { get; }

    /// <summary>(R3-W3c-4c D-1 joint↔separate reconciliation) AS-IS
    /// <c>ICanNotMoveEffect.CanNotMove(cardSource, cardEffect)</c>: the AS-IS <c>CanNotMoveClass</c> ANDs a
    /// SEPARATE <c>_cardCondition(cardSource)</c> and <c>_cardEffectCondition(cardEffect)</c>. The headless carrier
    /// holds the JOINT predicate <c>f(candidate, causingSource)</c> as a single Func; this adapter maps the AS-IS
    /// interface args onto it (the causing source = <c>cardEffect.EffectSourceCard</c>, or null — the move gate
    /// passes <c>CanNotMove(TopCard, null)</c>, AS-IS Permanent.CanMove).</summary>
    public bool CanNotMove(CardSource cardSource, ICardEffect cardEffect)
    {
        if (cardSource is not null)
        {
            return _predicate(cardSource, cardEffect?.EffectSourceCard);
        }

        return false;
    }

    // (이연④-f) ToBinding retired — see the class summary. No registry-half: the reflective LegacyBindingBridge
    // lowering finds no ToBinding and the registrar skips registry registration; the live EffectList /
    // ICanNotMoveEffect path (Permanent.CanMove) is the sole consumer.
}
