namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (RD-2) AS-IS <c>CardSource.MatchColorRequirement</c> (CardSource.cs:255-321): an Option can only be played
/// if EVERY one of its colors is present on some field permanent the owner controls — a Digimon/Tamer in the
/// BATTLE area OR the BREEDING area (AS-IS <c>Owner.GetFieldPermanents()</c> spans both frame parents,
/// Player.cs:19-78) — UNLESS an "ignore color condition" effect (<c>IIgnoreColorConditionEffect</c>) applies.
/// <c>CardSource.CanNotPlayThisOption</c> gates play on <c>!MatchColorRequirement</c>. The headless port had no
/// such gate, so every option was playable regardless of colors in play.
/// </summary>
public static class OptionColorRequirement
{
    /// <summary>True when <paramref name="optionCardId"/> (owned by <paramref name="owner"/>) satisfies its
    /// color requirement, or is exempt via an ignore-color effect.</summary>
    public static bool Matches(EngineContext context, HeadlessPlayerId owner, HeadlessEntityId optionCardId)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (owner.IsEmpty || optionCardId.IsEmpty)
        {
            return true;
        }

        // (DEF-S9, 2026-07-24) SINGLE-SOURCE the game rule on the mirror. The colour requirement is a game
        // rule (not substrate), so it must live in ONE place: the mirror CardSource.MatchColorRequirement
        // (Script/CardSource.cs:313-343) IS the AS-IS CardSource.MatchColorRequirement (DCGO CardSource.cs:255-321)
        // verbatim — the three-region IIgnoreColorConditionEffect ignore-scan (via IgnoreColorConditionActive),
        // then colorsToCheck = IsDigimon ? DualCardColors : CardColors, then EVERY required colour must appear on
        // SOME owner field permanent whose TopCard.IsPermanent (Digimon/Tamer/DigiEgg). This substrate previously
        // carried a SECOND live copy of that logic (ignore-scan + colorsToCheck + field-permanent colour union),
        // which could drift from the mirror. It now DELEGATES to the mirror so there is one authoritative rule.
        // (MatchColorRequirement itself returns true for a non-Option, so the delegation preserves the "only
        // options gate" contract that this method is called under.)
        return new CardSource(context, optionCardId, owner, owner).MatchColorRequirement;
    }
}
