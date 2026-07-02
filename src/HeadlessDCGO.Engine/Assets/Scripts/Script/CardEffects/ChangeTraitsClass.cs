// Source: Assets/Scripts/Script/CardEffects/ChangeTraitsClass.cs
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
/// (A3) AS-IS <c>ChangeTraitsClass</c> / <c>IChangeTraitsEffect</c> — consumed by
/// <c>CardSource.CardTraits</c> (CardSource.cs:2581-2604): the card's OWN continuous effects transform the
/// printed traits (<c>traits = ChangTraits(traits, this)</c>; the AS-IS method name is misspelled, the
/// SetUp surface is mirrored). Scan scope mirrors AS-IS: SELF effects only, no Distinct.
/// </summary>
public sealed class ChangeTraitsClass : ICardEffect
{
    /// <summary>Binding-values key carrying the transform (<c>Func&lt;CardSource,List&lt;string&gt;,List&lt;string&gt;&gt;</c>: (card, traits) → traits).</summary>
    public const string ChangeTraitsKey = "view.changeTraits";

    private string _effectName = string.Empty;
    private Func<bool>? _canUseCondition;
    private CardSource? _card;
    private Func<CardSource, List<string>, List<string>>? _changeTraits;

    /// <summary>Mirror of the shared AS-IS <c>SetUpICardEffect(effectName, CanUseCondition, card)</c>.</summary>
    public void SetUpICardEffect(string effectName, Func<bool>? canUseCondition, CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        _effectName = effectName ?? string.Empty;
        _canUseCondition = canUseCondition;
        _card = card;
    }

    /// <summary>Mirror of AS-IS <c>SetUpChangeTraitsClass(changeeTraits)</c>.</summary>
    public void SetUpChangeTraitsClass(Func<CardSource, List<string>, List<string>> changeeTraits)
    {
        ArgumentNullException.ThrowIfNull(changeeTraits);
        _changeTraits = changeeTraits;
    }

    /// <summary>AS-IS UI toggle — no headless surface.</summary>
    public void SetNotShowUI(bool notShowUI)
    {
    }

    public EffectBinding ToBinding(string effectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        if (_card is null || _changeTraits is null)
        {
            throw new InvalidOperationException("ChangeTraitsClass requires SetUpICardEffect and SetUpChangeTraitsClass before ToBinding.");
        }

        var values = new Dictionary<string, object?>(StringComparer.Ordinal) { [ChangeTraitsKey] = _changeTraits };
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
