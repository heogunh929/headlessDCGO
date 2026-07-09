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

        // (1) AS-IS "ignore color requirement" effects — scanned over field permanents + players + itself for
        // IIgnoreColorConditionEffect. Mirrored by the condition-aware applicable set carrying the
        // IgnoreColorRequirementKey (the same key the digivolve color-ignore consumes).
        foreach (EffectRequest effect in ContinuousScopeEvaluation.ApplicableEffects(context, ContinuousRestrictionGate.Scope, optionCardId))
        {
            if (effect.Context.Values.TryGetValue(DigivolveAction.IgnoreColorRequirementKey, out object? raw) && raw is true)
            {
                return true;
            }
        }

        // (2) every option color must appear on some owner field/breeding permanent's effective colors
        // (AS-IS colorsToCheck = CardColors for an Option; permanent.TopCard.CardColors reflects color-change
        // effects — mirrored by CardSource.CardColors' two-stage fold).
        IReadOnlyList<string> optionColors = new CardSource(context, optionCardId, owner, owner).CardColors;
        if (optionColors.Count == 0)
        {
            return true;   // a colorless option imposes no requirement
        }

        var fieldColors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (context.ZoneMover is IZoneStateReader zones)
        {
            foreach (ChoiceZone zone in new[] { ChoiceZone.BattleArea, ChoiceZone.BreedingArea })
            {
                foreach (HeadlessEntityId permanentId in zones.GetCards(owner, zone))
                {
                    foreach (string color in new CardSource(context, permanentId, owner, owner).CardColors)
                    {
                        fieldColors.Add(color);
                    }
                }
            }
        }

        return optionColors.All(color => fieldColors.Contains(color));
    }
}
