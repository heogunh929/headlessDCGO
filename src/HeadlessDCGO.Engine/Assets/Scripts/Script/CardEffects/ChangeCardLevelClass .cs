// Source: Assets/Scripts/Script/CardEffects/ChangeCardLevelClass .cs
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
/// (A3) AS-IS <c>ChangeCardLevelClass</c> / <c>IChangeCardLevelEffect</c> — an accumulator-transform fold
/// consumed by <c>CardSource.TreatedLevel</c> (CardSource.cs:947-975): the card's own continuous effects
/// transform its printed level (<c>level = GetCardLevel(level, this)</c>). The AS-IS registration is
/// <c>SetUpICardEffect(name, CanUseCondition, card)</c> + <c>SetUpChangeCardLevelClass(GetLevel)</c>; the
/// transform Func is stored VERBATIM on the binding (no value/predicate decomposition) and evaluated live
/// by the <see cref="CardSource.Level"/> fold. Scan scope mirrors AS-IS: SELF effects only.
/// </summary>
public sealed class ChangeCardLevelClass : ICardEffect
{
    /// <summary>Binding-values key carrying the transform (<c>Func&lt;CardSource,int,int&gt;</c>: (card, level) → level).</summary>
    public const string GetLevelKey = "view.changeCardLevel";

    private string _effectName = string.Empty;
    private Func<bool>? _canUseCondition;
    private CardSource? _card;
    private Func<CardSource, int, int>? _getLevel;

    /// <summary>Mirror of the shared AS-IS <c>SetUpICardEffect(effectName, CanUseCondition, card)</c>. The
    /// headless condition is a plain <c>Func&lt;bool&gt;</c> (continuous effects are gated with
    /// <c>CanUse(null)</c> in the original, so the Hashtable parameter is never populated).</summary>
    public void SetUpICardEffect(string effectName, Func<bool>? canUseCondition, CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        _effectName = effectName ?? string.Empty;
        _canUseCondition = canUseCondition;
        _card = card;
    }

    /// <summary>Mirror of AS-IS <c>SetUpChangeCardLevelClass(GetLevel)</c>.</summary>
    public void SetUpChangeCardLevelClass(Func<CardSource, int, int> GetLevel)
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
            throw new InvalidOperationException("ChangeCardLevelClass requires SetUpICardEffect and SetUpChangeCardLevelClass before ToBinding.");
        }

        var values = new Dictionary<string, object?>(StringComparer.Ordinal) { [GetLevelKey] = _getLevel };
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
