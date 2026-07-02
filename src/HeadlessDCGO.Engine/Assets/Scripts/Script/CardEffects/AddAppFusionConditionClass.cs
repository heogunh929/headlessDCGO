// Source: Assets/Scripts/Script/CardEffects/AddAppFusionConditionClass.cs
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
/// (W6-F) 1:1 mirror of AS-IS <c>AddAppFusionConditionClass</c> — the thin timing-None wrapper a card uses
/// to DECLARE its App-Fusion condition (<c>Func&lt;CardSource, AppFusionCondition&gt;</c>). Registered via
/// <c>CardEffectFactory.AddAppfuseMethodByName/ByCondition</c> (AddAppfusionMethod.cs); consumed by
/// <see cref="CardSource.AppFusionConditionOf"/> and the DigivolveAction App-Fusion rider.
/// </summary>
public sealed class AddAppFusionConditionClass : ICardEffect
{
    /// <summary>Binding-values key carrying the stored <c>Func&lt;CardSource, AppFusionCondition?&gt;</c>.</summary>
    public const string GetAppFusionConditionKey = "appfusion.getCondition";

    private string _effectName = string.Empty;
    private Func<bool>? _canUseCondition;
    private CardSource? _card;
    private Func<CardSource, AppFusionCondition?>? _getAppFusionCondition;

    /// <summary>Mirror of the shared AS-IS <c>SetUpICardEffect(effectName, CanUseCondition, card)</c>.</summary>
    public void SetUpICardEffect(string effectName, Func<bool>? canUseCondition, CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        _effectName = effectName ?? string.Empty;
        _canUseCondition = canUseCondition;
        _card = card;
    }

    /// <summary>Mirror of AS-IS <c>SetUpAddAppFusionConditionClass(getAppFusionCondition)</c>.</summary>
    public void SetUpAddAppFusionConditionClass(Func<CardSource, AppFusionCondition?> getAppFusionCondition)
    {
        ArgumentNullException.ThrowIfNull(getAppFusionCondition);
        _getAppFusionCondition = getAppFusionCondition;
    }

    /// <summary>Mirror of AS-IS <c>GetAppFusionCondition(cardSource)</c>.</summary>
    public AppFusionCondition? GetAppFusionCondition(CardSource cardSource) => _getAppFusionCondition?.Invoke(cardSource);

    /// <summary>Mirror of the shared AS-IS <c>CanUse</c> gate.</summary>
    public bool CanUse() => _canUseCondition?.Invoke() ?? true;

    /// <summary>AS-IS UI toggle — no headless surface.</summary>
    public void SetNotShowUI(bool notShowUI)
    {
    }

    public EffectBinding ToBinding(string effectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        if (_card is null || _getAppFusionCondition is null)
        {
            throw new InvalidOperationException("AddAppFusionConditionClass requires SetUpICardEffect and SetUpAddAppFusionConditionClass before ToBinding.");
        }

        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [GetAppFusionConditionKey] = _getAppFusionCondition,
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
