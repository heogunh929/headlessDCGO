namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
// Aliased (not a namespace import) to avoid pulling the sibling `...Script.CardEffectFactory` namespace
// into scope, which would clash with the CardEffectFactory type below.
using SelectPermanentEffect = HeadlessDCGO.Engine.Assets.Scripts.Script.SelectPermanentEffect;
using PartitionCondition = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory.KeyWordEffects.PartitionCondition;


/// <summary>
/// Headless mirror of the original <c>ICardEffect</c>. A ported card returns these; the registrar lowers
/// each to an <see cref="EffectBinding"/> using the supplied unique effect id.
/// </summary>
public interface ICardEffect
{
    EffectBinding ToBinding(string effectId);
}


/// <summary>Marker for effects resolved via the activation / choice flow (Option / Security skills,
/// select-and-act, triggered-with-choice) rather than auto-registered continuous/trigger bindings.
/// <see cref="CardEffectRegistrar"/> skips these on enter-play; they are resolved imperatively until the
/// interactive activation path is wired.</summary>
public interface IActivatedCardEffect : ICardEffect
{
}


/// <summary>Headless mirror of the original card-effect base class <c>CEntity_Effect</c>.</summary>
public abstract class CEntity_Effect
{
    /// <summary>Returns the effects active for <paramref name="timing"/> (mirrors the original override).</summary>
    public abstract IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card);
}

