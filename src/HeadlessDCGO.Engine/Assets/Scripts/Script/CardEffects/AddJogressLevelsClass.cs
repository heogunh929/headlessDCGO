// Source: Assets/Scripts/Script/CardEffects/AddJogressLevelsClass.cs
// Decision: PORT
// Category: CardEffect
// Namespace hint: HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// AS-IS <c>AddJogressLevelsClass</c> / <c>IAddJogressLevelsEffect</c> (Permanent.cs:3560-3600): a card that,
/// as a Jogress / DNA-Digivolution MATERIAL, is TREATED AS one or more additional levels (e.g. BT20_025 "Also
/// treated as level 6 for DNA Digivolution" when the digivolving card is Examon). The AS-IS scan is board-wide,
/// but every real effect self-gates on <c>permanent == card.PermanentOfThisCard()</c>, so the contribution is
/// effectively SELF — mirrored here as a self-scoped continuous modifier (same shape as
/// <see cref="ChangeCardLevelClass"/>), read by <c>CardSource.JogressLevelsAgainst</c>. The AS-IS
/// <c>getJogressLevels(cardSource, permanent)</c> collapses to <c>Func&lt;CardSource, IReadOnlyList&lt;int&gt;&gt;</c>
/// (permanent is always self); <c>cardSource</c> is the digivolving (Jogress) card.
/// </summary>
public sealed class AddJogressLevelsClass : ICardEffect
{
    /// <summary>Binding-values key carrying the extra-levels Func (<c>Func&lt;CardSource, IReadOnlyList&lt;int&gt;&gt;</c>:
    /// (jogress card) → the extra levels THIS material provides).</summary>
    public const string GetJogressLevelsKey = "view.addJogressLevels";

    private string _effectName = string.Empty;
    private Func<bool>? _canUseCondition;
    private CardSource? _card;
    private Func<CardSource, IReadOnlyList<int>>? _getJogressLevels;

    /// <summary>Mirror of the shared AS-IS <c>SetUpICardEffect(effectName, CanUseCondition, card)</c>.</summary>
    public void SetUpICardEffect(string effectName, Func<bool>? canUseCondition, CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        _effectName = effectName ?? string.Empty;
        _canUseCondition = canUseCondition;
        _card = card;
    }

    /// <summary>Mirror of AS-IS <c>SetUpAddJogressLevelsClass(getJogressLevels)</c> (permanent arg dropped — always self).</summary>
    public void SetUpAddJogressLevelsClass(Func<CardSource, IReadOnlyList<int>> getJogressLevels)
    {
        ArgumentNullException.ThrowIfNull(getJogressLevels);
        _getJogressLevels = getJogressLevels;
    }

    public EffectBinding ToBinding(string effectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        if (_card is null || _getJogressLevels is null)
        {
            throw new InvalidOperationException("AddJogressLevelsClass requires SetUpICardEffect and SetUpAddJogressLevelsClass before ToBinding.");
        }

        var values = new Dictionary<string, object?>(StringComparer.Ordinal) { [GetJogressLevelsKey] = _getJogressLevels };
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
