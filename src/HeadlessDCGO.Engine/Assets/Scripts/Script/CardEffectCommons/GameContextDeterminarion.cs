// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs
// (P6 cluster3) Sibling partial of CardEffectCommons.cs (same namespace, CardEffectCommons.cs itself is not
// edited — docs/audit/rebuild_p5_gates_missing.md's standing prohibition). CardEffectCommons.cs already ports
// the AS-IS IsExistOnBattleAreaTrigger/IsExistOnBattleAreaActivate pair (:1357-1366) — the AS-IS "cache the
// permanent at trigger time, re-check identity at activation" pair collapses to a plain live battle-area check
// because the headless permanent identity IS the instance id (stacks keep their top instance across the
// window). Consumed by CardEffectFactory.WhenLinkingClass (AS-IS CardEffectFactory.cs:1130/1137) — Digimon is
// the only permanent kind that can link, so AS-IS uses the Digimon-filtered variant of the same pair.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public static partial class CardEffectCommons
{
    /// <summary>AS-IS <c>IsExistOnBattleAreaDigimonTrigger</c>/<c>IsExistOnBattleAreaDigimonActivate</c>
    /// (GameContextDeterminarion.cs:49/95) — same collapse as <see cref="IsExistOnBattleAreaTrigger"/>/
    /// <see cref="IsExistOnBattleAreaActivate"/>, gated on <see cref="IsExistOnBattleAreaDigimon"/> instead of
    /// the untyped battle-area check.</summary>
    public static bool IsExistOnBattleAreaDigimonTrigger(CardSource card, ICardEffect? cardEffect = null) =>
        IsExistOnBattleAreaDigimon(card);

    public static bool IsExistOnBattleAreaDigimonActivate(CardSource card, ICardEffect? cardEffect = null) =>
        IsExistOnBattleAreaDigimon(card);
}
