// Source: Assets/Scripts/Script/CardEffects/CanNotSwitchAttackTargetClass.cs
// Decision: PORT
// Category: CardEffect
// Priority: HIGH
// Migration: Port core engine source
// Namespace hint: HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (AD1-S) AS-IS <c>CanNotSwitchAttackTargetClass</c> / <c>ICanNotSwitchAttackTargetEffect</c> — "this
/// attack's target can't be switched". Consumed through the single AS-IS chokepoint
/// <c>Permanent.CanSwitchAttackTarget</c> (Permanent.cs:3745), which gates EXACTLY two actions keyed on the
/// ATTACKING permanent: (1) <c>AttackProcess.SwitchDefender</c> (AttackProcess.cs:519) — the shared routine
/// for BOTH a blocker's redirect and effect retargets, checked before the isBlock branch; (2) block
/// eligibility (Permanent.cs:2156) — a locked attacker offers no blocker candidates at all. Headless
/// consumption: <see cref="AttackTargetSwitchGate"/> (BlockTiming + RaidAttackSwitch).
/// The <c>PermanentCondition</c> predicate (over the ATTACKER) is stored VERBATIM and evaluated live.
/// </summary>
public sealed class CanNotSwitchAttackTargetClass : ICardEffect
{
    /// <summary>Binding-values flag: this binding locks attack-target switching.</summary>
    public const string CannotSwitchAttackTargetKey = "cannotSwitchAttackTarget";

    /// <summary>Binding-values key carrying the AS-IS <c>PermanentCondition</c> (<c>Func&lt;Permanent,bool&gt;</c>
    /// over the ATTACKING permanent — <c>CanNotBeSwitchAttackTarget(this)</c>, Permanent.cs:3762).</summary>
    public const string AttackerConditionKey = "cannotSwitchAttackTarget.permanentCondition";

    private string _effectName = string.Empty;
    private Func<bool>? _canUseCondition;
    private CardSource? _card;
    private Func<Permanent, bool>? _permanentCondition;

    /// <summary>Mirror of the shared AS-IS <c>SetUpICardEffect(effectName, CanUseCondition, card)</c>.</summary>
    public void SetUpICardEffect(string effectName, Func<bool>? canUseCondition, CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        _effectName = effectName ?? string.Empty;
        _canUseCondition = canUseCondition;
        _card = card;
    }

    /// <summary>Mirror of AS-IS <c>SetUpCanNotSwitchAttackTargetClass(PermanentCondition)</c>.</summary>
    public void SetUpCanNotSwitchAttackTargetClass(Func<Permanent, bool> PermanentCondition)
    {
        ArgumentNullException.ThrowIfNull(PermanentCondition);
        _permanentCondition = PermanentCondition;
    }

    /// <summary>AS-IS UI toggle — no headless surface.</summary>
    public void SetNotShowUI(bool notShowUI)
    {
    }

    /// <summary>Registers as a continuous restriction binding; pass <paramref name="duration"/> for the
    /// AS-IS bucket registrations (e.g. <c>UntilEachTurnEndEffects.Add(...)</c> →
    /// <see cref="EffectDuration.UntilEachTurnEnd"/>).</summary>
    public EffectBinding ToBinding(string effectId) => ToBinding(effectId, duration: null);

    public EffectBinding ToBinding(string effectId, EffectDuration? duration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        if (_card is null || _permanentCondition is null)
        {
            throw new InvalidOperationException("CanNotSwitchAttackTargetClass requires SetUpICardEffect and SetUpCanNotSwitchAttackTargetClass before ToBinding.");
        }

        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [CannotSwitchAttackTargetKey] = true,
            [AttackerConditionKey] = _permanentCondition,
        };
        if (_canUseCondition is not null)
        {
            values[ContinuousSelfModifierEffect.ConditionKey] = _canUseCondition;
        }

        var context = new EffectContext(
            _card.Controller, _card.Owner, _card.InstanceId, triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>(), values: values);
        return new EffectBinding(
            new EffectRequest(new HeadlessEntityId(effectId), _card.Controller, "Continuous", context),
            keywords: null, EffectQueryRole.Continuous, new[] { ContinuousRestrictionGate.Scope }, effect: null, duration: duration);
    }
}
