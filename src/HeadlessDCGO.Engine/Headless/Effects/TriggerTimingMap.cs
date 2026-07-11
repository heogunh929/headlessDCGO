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
                // (R2-P1-4) a field→Trash move is a DELETION only when the deleting path stamped the
                // delete-batch id (all four deletion finishers do — sink / battle / sweep / security). An
                // unstamped field→Trash move is a TOP-SWAP (Armor Purge / Burst / De-Digivolve trash the top
                // while the permanent survives) or the no-DP direct trash — none of which stacks
                // OnDestroyedAnyone / OnLeaveFieldAnyone in AS-IS.
                bool isDeletionMove = gameEvent.Metadata.ContainsKey(MatchStateMutationSink.DeletionBatchIdKey);
                DeriveZoneTransition(gameEvent.ZoneFrom, gameEvent.ZoneTo, isDeletionMove, timings);
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

    private static void DeriveZoneTransition(ChoiceZone? from, ChoiceZone? to, bool isDeletionMove, List<string> timings)
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

        // D-5: "deletion" (AS-IS OnDestroyedAnyone) is a FIELD card being destroyed to the trash. A hand
        // discard, deck mill, or security check trashing a card is NOT a deletion, so OnDeletion only opens
        // when leaving a field zone.
        // (R2-P1-4) AND only when the move carries the deletion marker (DeletionBatchIdKey). A marker-less
        // field→Trash move is a TOP-SWAP (AS-IS ArmorPurge.cs:63 sets willBeRemoveField=false — only
        // WhenTopCardTrashed fires; same for Burst / De-Digivolve top trashes) or the no-DP direct trash
        // (AS-IS TrashNoDPPermanentProcess, AutoProcessing.cs:439-465 — no StackSkillInfos at all); neither
        // stacks OnDestroyedAnyone in AS-IS, and pre-fix both spuriously fired the deleted-card reactors.
        bool isDeletion = fromField && to == ChoiceZone.Trash && isDeletionMove;

        // (R2-P1-4) OnLeaveFieldAnyone / the RemoveField cut-ins fire only on a PERMANENT-DEPARTURE move.
        // AS-IS stacks OnLeaveFieldAnyone in exactly: DestroyPermanentsClass (deletion, CardController.cs:3756),
        // the hand/deck bounces (:2546/:2711), IPutSecurityPermanent (:3605) and the battle→breeding move
        // (CardObjectController.cs:1124 — field→field headless-side, not derived here). A top-swap trash is
        // NOT a departure (the permanent survives with a new top): derive the leave timings only for
        // field→Hand/Library/Security and a MARKED field→Trash deletion; field→Trash without the marker and
        // field→None (token removal) derive nothing.
        if (fromField && !toField
            && (isDeletion || to is ChoiceZone.Hand or ChoiceZone.Library or ChoiceZone.Security))
        {
            timings.Add(TriggerTimings.OnLeaveField);
            // (design item R2-P2-2) AS-IS WhenRemoveField is a PRE cut-in (stacked BEFORE the move fixes the
            // list); headless derives it POST-move — latent until a WhenRemoveField registrant is ported.
            // AS-IS also stacks the OnRemovedField cut-in inside CardObjectController.RemoveField itself
            // (:512-524), which the no-trigger no-DP trash still routes through — a nuance folded into the
            // same design item.
            timings.Add(TriggerTimings.WhenRemoveField);
            // F-6.5: OnRemovedField is the original's field-leave synonym alongside WhenRemoveField.
            timings.Add(TriggerTimings.OnRemovedField);
        }

        if (isDeletion)
        {
            timings.Add(TriggerTimings.OnDeletion);
        }

        // CV-A4: the original EffectTiming.OnMove fires only for a Digimon promoted out of the breeding
        // (training) area onto the battle area — not for arbitrary zone moves.
        if (from == ChoiceZone.BreedingArea && to == ChoiceZone.BattleArea)
        {
            timings.Add(TriggerTimings.OnMove);
        }

        if (to == ChoiceZone.Hand && from != ChoiceZone.Hand)
        {
            timings.Add(TriggerTimings.OnAddToHand);
            if (fromField)
            {
                timings.Add(TriggerTimings.OnReturnToHand);
                // F-6.5: a permanent (field card) returning to hand opens its dedicated window too.
                timings.Add(TriggerTimings.OnPermanentReturnedToHand);
            }

            // F-6.5: recovering a card out of the trash into the hand.
            if (from == ChoiceZone.Trash)
            {
                timings.Add(TriggerTimings.OnReturnCardsToHandFromTrash);
            }
        }

        if (to == ChoiceZone.Library && from != ChoiceZone.Library)
        {
            timings.Add(TriggerTimings.OnReturnToLibrary);

            // F-6.5: recovering a card out of the trash into the deck.
            if (from == ChoiceZone.Trash)
            {
                timings.Add(TriggerTimings.OnReturnCardsToLibraryFromTrash);
            }
        }

        // F-6.5: discards — a card sent to the trash from a NON-field zone is a discard, not a deletion
        // (OnDeletion above only opens for field→Trash). Each source zone has its own discard window.
        if (to == ChoiceZone.Trash)
        {
            switch (from)
            {
                case ChoiceZone.Hand:
                    timings.Add(TriggerTimings.OnDiscardHand);
                    break;
                case ChoiceZone.Security:
                    timings.Add(TriggerTimings.OnDiscardSecurity);
                    break;
                case ChoiceZone.Library:
                    timings.Add(TriggerTimings.OnDiscardLibrary);
                    break;
            }
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
