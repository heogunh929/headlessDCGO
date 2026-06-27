namespace HeadlessDCGO.Engine.Headless.Effects;

using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Runtime;

/// <summary>
/// Derives the canonical <see cref="TriggerTimings"/> a game event opens (W1). Replaces the old
/// "single timing = event type name" behaviour: one structured event (e.g. a CardMoved with B2
/// ZoneFrom/ZoneTo) can open several timings (a Hand→BattleArea move is both OnPlay and OnEnterField).
/// An explicit metadata timing override always takes precedence and is returned alone.
/// </summary>
public static class TriggerTimingMap
{
    public static IReadOnlyList<string> Derive(GameEvent gameEvent)
    {
        ArgumentNullException.ThrowIfNull(gameEvent);

        // An explicit override (set by an effect that schedules its own timing) wins outright.
        if (TryReadExplicitTiming(gameEvent, out string? explicitTiming))
        {
            return new[] { explicitTiming! };
        }

        var timings = new List<string>();
        switch (gameEvent.Type)
        {
            case GameEventType.CardMoved:
                DeriveZoneTransition(gameEvent.ZoneFrom, gameEvent.ZoneTo, timings);
                break;
            case GameEventType.AttackDeclared:
                timings.Add(TriggerTimings.OnAttack);
                break;
            case GameEventType.SecurityCheck:
                timings.Add(TriggerTimings.OnSecurityCheck);
                break;
            case GameEventType.GameEnded:
            case GameEventType.Unknown:
                break;
        }

        // Always also expose the raw event-type timing so this stays purely additive: effects
        // registered against the low-level event name (or before the semantic vocabulary existed)
        // keep firing, while the derived semantic timings are added on top.
        timings.Add(gameEvent.Type.ToString());

        return Distinct(timings);
    }

    private static void DeriveZoneTransition(ChoiceZone? from, ChoiceZone? to, List<string> timings)
    {
        bool fromField = IsField(from);
        bool toField = IsField(to);

        if (to == ChoiceZone.BattleArea && from == ChoiceZone.Hand)
        {
            timings.Add(TriggerTimings.OnPlay);
        }

        if (toField && !fromField)
        {
            timings.Add(TriggerTimings.OnEnterField);
        }

        if (fromField && !toField)
        {
            timings.Add(TriggerTimings.OnLeaveField);
            timings.Add(TriggerTimings.WhenRemoveField);
        }

        // D-5: "deletion" (AS-IS OnDestroyedAnyone) is a FIELD card being destroyed to the trash. A
        // hand discard, deck mill, or security check trashing a card is NOT a deletion (the original
        // routes those through distinct timings), so OnDeletion only opens when leaving a field zone.
        if (fromField && to == ChoiceZone.Trash)
        {
            timings.Add(TriggerTimings.OnDeletion);
        }

        if (to == ChoiceZone.Hand && from != ChoiceZone.Hand)
        {
            timings.Add(TriggerTimings.OnAddToHand);
            if (fromField)
            {
                timings.Add(TriggerTimings.OnReturnToHand);
            }
        }

        if (to == ChoiceZone.Library && from != ChoiceZone.Library)
        {
            timings.Add(TriggerTimings.OnReturnToLibrary);
        }

        if (to == ChoiceZone.Security && from != ChoiceZone.Security)
        {
            timings.Add(TriggerTimings.OnAddToSecurity);
        }

        if (from == ChoiceZone.Security && to != ChoiceZone.Security)
        {
            timings.Add(TriggerTimings.OnLoseSecurity);
        }
    }

    private static bool IsField(ChoiceZone? zone)
    {
        return zone is ChoiceZone.BattleArea or ChoiceZone.BreedingArea;
    }

    private static bool TryReadExplicitTiming(GameEvent gameEvent, out string? timing)
    {
        foreach (string key in new[]
        {
            AutoProcessingTriggerCollector.TriggerTimingKey,
            AutoProcessingTriggerCollector.TimingKey,
            AutoProcessingTriggerCollector.EffectTimingKey,
        })
        {
            if (gameEvent.Metadata.TryGetValue(key, out object? raw) && raw is string text && !string.IsNullOrWhiteSpace(text))
            {
                timing = text.Trim();
                return true;
            }
        }

        timing = null;
        return false;
    }

    private static IReadOnlyList<string> Distinct(List<string> timings)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>(timings.Count);
        foreach (string timing in timings)
        {
            if (seen.Add(timing))
            {
                result.Add(timing);
            }
        }

        return result;
    }
}
