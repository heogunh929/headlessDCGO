// Source: Assets/Scripts/Script/CardEffects/AddLinkConditionClass.cs
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
/// (W6-L) 1:1 mirror of AS-IS <c>AddLinkConditionClass</c> / <c>IAddLinkConditionEffect</c> — the thin
/// timing-None wrapper a card uses to DECLARE its Link condition
/// (<c>Func&lt;CardSource, LinkCondition&gt;</c>: host filter + base cost). Consumption:
/// <see cref="CardSource.LinkConditionOf"/> reads it; <c>CardEffectFactory.LinkEffect</c> /
/// <c>LinkSelfEffect</c> apply it (host candidates + paid cost via <c>LinkHelpers.ResolveLinkCost</c>).
/// Registered via <c>CardEffectFactory.AddSelfLinkConditionStaticEffect</c> (AddLinkRequirement.cs:11).
/// </summary>
public sealed class AddLinkConditionClass : ICardEffect
{
    /// <summary>Binding-values key carrying the stored <c>Func&lt;CardSource, LinkCondition?&gt;</c>.</summary>
    public const string GetLinkConditionKey = "link.getCondition";

    private string _effectName = string.Empty;
    private Func<bool>? _canUseCondition;
    private CardSource? _card;
    private Func<CardSource, LinkCondition?>? _getLinkCondition;

    /// <summary>Mirror of the shared AS-IS <c>SetUpICardEffect(effectName, CanUseCondition, card)</c>.</summary>
    public void SetUpICardEffect(string effectName, Func<bool>? canUseCondition, CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        _effectName = effectName ?? string.Empty;
        _canUseCondition = canUseCondition;
        _card = card;
    }

    /// <summary>Mirror of AS-IS <c>SetUpAddLinkConditionClass(getLinkCondition)</c>.</summary>
    public void SetUpAddLinkConditionClass(Func<CardSource, LinkCondition?> getLinkCondition)
    {
        ArgumentNullException.ThrowIfNull(getLinkCondition);
        _getLinkCondition = getLinkCondition;
    }

    /// <summary>Mirror of AS-IS <c>GetLinkCondition(cardSource)</c>.</summary>
    public LinkCondition? GetLinkCondition(CardSource cardSource) => _getLinkCondition?.Invoke(cardSource);

    /// <summary>Mirror of the shared AS-IS <c>CanUse</c> gate.</summary>
    public bool CanUse() => _canUseCondition?.Invoke() ?? true;

    /// <summary>AS-IS UI toggle — no headless surface.</summary>
    public void SetNotShowUI(bool notShowUI)
    {
    }

    public EffectBinding ToBinding(string effectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        if (_card is null || _getLinkCondition is null)
        {
            throw new InvalidOperationException("AddLinkConditionClass requires SetUpICardEffect and SetUpAddLinkConditionClass before ToBinding.");
        }

        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [GetLinkConditionKey] = _getLinkCondition,
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
