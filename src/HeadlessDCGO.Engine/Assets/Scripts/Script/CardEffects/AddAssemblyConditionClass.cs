// Source: Assets/Scripts/Script/CardEffects/AddAssemblyConditionClass.cs
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
/// (AD1-A) 1:1 mirror of AS-IS <c>AddAssemblyConditionClass</c> / <c>IAddAssemblyConditionEffect</c> — the
/// thin timing-None wrapper a card uses to DECLARE its Assembly condition
/// (<c>Func&lt;CardSource, AssemblyCondition&gt;</c>; AD1_025.cs:214-261 shape, always with
/// <c>SetNotShowUI(true)</c>). Consumption: <see cref="CardSource.HasAssembly"/> /
/// <see cref="CardSource.AssemblyConditionOf"/> read the stored Func from the registry; the play-time rider
/// lives in <c>PlayCardAction</c> (materials from the OWNER'S TRASH, flat <c>reduceCost</c> when the full
/// set is chosen, materials stacked UNDER the played permanent).
/// </summary>
public sealed class AddAssemblyConditionClass : ICardEffect
{
    /// <summary>Binding-values key carrying the stored <c>Func&lt;CardSource, AssemblyCondition?&gt;</c>.</summary>
    public const string GetAssemblyConditionKey = "assembly.getCondition";

    private string _effectName = string.Empty;
    private Func<bool>? _canUseCondition;
    private CardSource? _card;
    private Func<CardSource, AssemblyCondition?>? _getAssemblyCondition;

    /// <summary>Mirror of the shared AS-IS <c>SetUpICardEffect(effectName, CanUseCondition, card)</c>.</summary>
    public void SetUpICardEffect(string effectName, Func<bool>? canUseCondition, CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        _effectName = effectName ?? string.Empty;
        _canUseCondition = canUseCondition;
        _card = card;
    }

    /// <summary>Mirror of AS-IS <c>SetUpAddAssemblyConditionClass(getAssemblyCondition)</c>.</summary>
    public void SetUpAddAssemblyConditionClass(Func<CardSource, AssemblyCondition?> getAssemblyCondition)
    {
        ArgumentNullException.ThrowIfNull(getAssemblyCondition);
        _getAssemblyCondition = getAssemblyCondition;
    }

    /// <summary>Mirror of AS-IS <c>GetAssemblyCondition(cardSource)</c> (null-guarded invoke).</summary>
    public AssemblyCondition? GetAssemblyCondition(CardSource cardSource) =>
        _getAssemblyCondition?.Invoke(cardSource);

    /// <summary>Mirror of the shared AS-IS <c>CanUse</c> gate (the accessor at CardSource.cs:3043 only
    /// consults a USABLE IAddAssemblyConditionEffect).</summary>
    public bool CanUse() => _canUseCondition?.Invoke() ?? true;

    /// <summary>AS-IS UI toggle — no headless surface.</summary>
    public void SetNotShowUI(bool notShowUI)
    {
    }

    public EffectBinding ToBinding(string effectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        if (_card is null || _getAssemblyCondition is null)
        {
            throw new InvalidOperationException("AddAssemblyConditionClass requires SetUpICardEffect and SetUpAddAssemblyConditionClass before ToBinding.");
        }

        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [GetAssemblyConditionKey] = _getAssemblyCondition,
        };
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
