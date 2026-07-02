// Source: Assets/Scripts/Script/CardEffects/ChangeCardColorClass.cs
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
/// (A3) AS-IS <c>ChangeCardColorClass</c> / <c>IChangeCardColorEffect</c> — consumed by
/// <c>CardSource.CardColors</c> (CardSource.cs:446-483): seeds from the fully-resolved BASE colors, then
/// every active change-color effect transforms the list (<c>colors = GetCardColors(colors, this)</c>),
/// Distinct at the end. AS-IS scan = self (when not on a permanent) + ALL field permanents of both players;
/// the headless fold scans all active bindings carrying this key (registry membership mirrors "in play").
/// Colors are strings headless-side (no CardColor enum — card-facing tolerance per the porting standard).
/// </summary>
public sealed class ChangeCardColorClass : ICardEffect
{
    /// <summary>Binding-values key carrying the transform (<c>Func&lt;CardSource,List&lt;string&gt;,List&lt;string&gt;&gt;</c>: (card, colors) → colors).</summary>
    public const string ChangeCardColorsKey = "view.changeCardColors";

    private string _effectName = string.Empty;
    private Func<bool>? _canUseCondition;
    private CardSource? _card;
    private Func<CardSource, List<string>, List<string>>? _changeCardColors;

    /// <summary>Mirror of the shared AS-IS <c>SetUpICardEffect(effectName, CanUseCondition, card)</c>.</summary>
    public void SetUpICardEffect(string effectName, Func<bool>? canUseCondition, CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        _effectName = effectName ?? string.Empty;
        _canUseCondition = canUseCondition;
        _card = card;
    }

    /// <summary>Mirror of AS-IS <c>SetUpChangeCardColorClass(ChangeCardColors)</c>.</summary>
    public void SetUpChangeCardColorClass(Func<CardSource, List<string>, List<string>> ChangeCardColors)
    {
        ArgumentNullException.ThrowIfNull(ChangeCardColors);
        _changeCardColors = ChangeCardColors;
    }

    /// <summary>AS-IS UI toggle — no headless surface.</summary>
    public void SetNotShowUI(bool notShowUI)
    {
    }

    public EffectBinding ToBinding(string effectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        if (_card is null || _changeCardColors is null)
        {
            throw new InvalidOperationException("ChangeCardColorClass requires SetUpICardEffect and SetUpChangeCardColorClass before ToBinding.");
        }

        var values = new Dictionary<string, object?>(StringComparer.Ordinal) { [ChangeCardColorsKey] = _changeCardColors };
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
