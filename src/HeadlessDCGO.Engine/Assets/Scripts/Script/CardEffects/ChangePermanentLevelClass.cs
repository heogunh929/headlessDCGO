// Source: Assets/Scripts/Script/CardEffects/ChangePermanentLevelClass.cs
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
/// (A3) AS-IS <c>ChangePermanentLevelClass</c> / <c>IChangePermanentLevelEffect</c> — consumed by
/// <c>Permanent.Level</c> (Permanent.cs:48-102): EVERY field permanent's and player's continuous effects
/// transform the queried permanent's level (<c>Level = GetPermanentLevel(Level, this)</c>). Targeting lives
/// inside the card's transform closure (AS-IS pattern). The transform Func is stored VERBATIM and evaluated
/// live by the <see cref="Permanent.Level"/> fold, which scans ALL active bindings (mirror of the AS-IS
/// all-players field + player-effect scan).
/// </summary>
public sealed class ChangePermanentLevelClass : ICardEffect
{
    /// <summary>Binding-values key carrying the transform (<c>Func&lt;Permanent,int,int&gt;</c>: (permanent, level) → level).</summary>
    public const string GetPermanentLevelKey = "view.changePermanentLevel";

    private string _effectName = string.Empty;
    private Func<bool>? _canUseCondition;
    private CardSource? _card;
    private Func<Permanent, int, int>? _getLevel;

    /// <summary>Mirror of the shared AS-IS <c>SetUpICardEffect(effectName, CanUseCondition, card)</c>.</summary>
    public void SetUpICardEffect(string effectName, Func<bool>? canUseCondition, CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        _effectName = effectName ?? string.Empty;
        _canUseCondition = canUseCondition;
        _card = card;
    }

    /// <summary>Mirror of AS-IS <c>SetUpChangePermanentLevelClass(GetLevel)</c> (guards TopCard != null in
    /// the original — the headless fold only runs for permanents with a top card).</summary>
    public void SetUpChangePermanentLevelClass(Func<Permanent, int, int> GetLevel)
    {
        ArgumentNullException.ThrowIfNull(GetLevel);
        _getLevel = GetLevel;
    }

    /// <summary>AS-IS UI toggle — no headless surface.</summary>
    public void SetNotShowUI(bool notShowUI)
    {
    }

    public EffectBinding ToBinding(string effectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        if (_card is null || _getLevel is null)
        {
            throw new InvalidOperationException("ChangePermanentLevelClass requires SetUpICardEffect and SetUpChangePermanentLevelClass before ToBinding.");
        }

        var values = new Dictionary<string, object?>(StringComparer.Ordinal) { [GetPermanentLevelKey] = _getLevel };
        if (_canUseCondition is not null)
        {
            values[ContinuousSelfModifierEffect.ConditionKey] = _canUseCondition;
        }

        var context = new EffectContext(
            _card.Controller, _card.Owner, _card.InstanceId, triggerEntityId: null, targetEntityIds: new[] { _card.InstanceId }, values: values);
        return new EffectBinding(
            new EffectRequest(new HeadlessEntityId(effectId), _card.Controller, "Continuous", context),
            keywords: null, EffectQueryRole.Continuous, new[] { ContinuousRestrictionGate.Scope }, effect: null, duration: null);
    }
}
