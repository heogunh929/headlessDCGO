// Source: DCGO/Assets/Scripts/Script/CardEffectCommons.cs:711
// (P6 cluster3) 1:1 port of AS-IS OptionMainEffect(card) — sibling partial of CardEffectCommons.cs (same
// namespace, CardEffectCommons.cs is not edited directly per docs/audit/rebuild_p5_gates_missing.md's standing
// prohibition). Consumed by CardEffectFactory.ActivateMainOptionSecurityEffect (17 real-card consumers:
// BT1_094/095/101/102/107/111, BT2_091/097/102/110, ST1_15/16, ST2_15/16, ST3_16, ST4_15/16).

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public static partial class CardEffectCommons
{
    /// <summary>AS-IS <c>CardEffectCommons.OptionMainEffect(card)</c> (CardEffectCommons.cs:711): the resolved
    /// [Main]-tagged <see cref="ActivateClass"/> among this card's OptionSkill-timing effects (or null).</summary>
    public static ActivateClass? OptionMainEffect(CardSource card) =>
        card.EffectList(EffectTiming.OptionSkill)
            .Find(cardEffect => cardEffect != null && cardEffect is ActivateClass && cardEffect.EffectDiscription.Contains("[Main]"))
            as ActivateClass;
}
