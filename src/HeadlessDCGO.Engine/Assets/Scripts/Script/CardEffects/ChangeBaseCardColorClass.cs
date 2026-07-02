// Source: Assets/Scripts/Script/CardEffects/ChangeBaseCardColorClass.cs
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
/// (A3) AS-IS <c>ChangeBaseCardColorClass</c> / <c>IChangeBaseCardColorEffect</c> — consumed by
/// <c>CardSource.BaseCardColors</c> (CardSource.cs:364-401): transforms the PRINTED colors BEFORE the
/// change-color pass (base-change → change two-stage fold, AS-IS ordering). Same scan/authoring shape as
/// <see cref="ChangeCardColorClass"/>.
/// </summary>
public sealed class ChangeBaseCardColorClass : ICardEffect
{
    /// <summary>Binding-values key carrying the transform (<c>Func&lt;CardSource,List&lt;string&gt;,List&lt;string&gt;&gt;</c>: (card, colors) → colors).</summary>
    public const string ChangeBaseCardColorsKey = "view.changeBaseCardColors";

    private string _effectName = string.Empty;
    private Func<bool>? _canUseCondition;
    private CardSource? _card;
    private Func<CardSource, List<string>, List<string>>? _changeBaseCardColors;

    /// <summary>Mirror of the shared AS-IS <c>SetUpICardEffect(effectName, CanUseCondition, card)</c>.</summary>
    public void SetUpICardEffect(string effectName, Func<bool>? canUseCondition, CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        _effectName = effectName ?? string.Empty;
        _canUseCondition = canUseCondition;
        _card = card;
    }

    /// <summary>Mirror of AS-IS <c>SetUpChangeBaseCardColorClass(ChangeBaseCardColors)</c>.</summary>
    public void SetUpChangeBaseCardColorClass(Func<CardSource, List<string>, List<string>> ChangeBaseCardColors)
    {
        ArgumentNullException.ThrowIfNull(ChangeBaseCardColors);
        _changeBaseCardColors = ChangeBaseCardColors;
    }

    /// <summary>AS-IS UI toggle — no headless surface.</summary>
    public void SetNotShowUI(bool notShowUI)
    {
    }

    public EffectBinding ToBinding(string effectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        if (_card is null || _changeBaseCardColors is null)
        {
            throw new InvalidOperationException("ChangeBaseCardColorClass requires SetUpICardEffect and SetUpChangeBaseCardColorClass before ToBinding.");
        }

        var values = new Dictionary<string, object?>(StringComparer.Ordinal) { [ChangeBaseCardColorsKey] = _changeBaseCardColors };
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
