// (R4 S3b — docs/audit/r4_tsm_s1_design_2026-07-16.md "S3 실행 설계", decision 3 = B)
// ============================================================================================================
// The pump-match action processor: converts the agent's MAIN-PHASE LegalActions into the mirror
// MainPhaseAction packets (the AS-IS network/UI transport seat) and queues them on the acting player —
// the TurnStateMachine selection wait (MainPhaseAsync :971-1170) dequeues and Executes them, exactly like the
// original. Everything else (ResolveChoice incl. the pump deposit seam, system/zone actions) delegates to
// MetadataActionProcessor. The OLD turn-flow actions (AdvancePhase / EndTurn) are ILLEGAL on a pump match —
// the pump owns phase flow (decision 3 = B retired the invented step cadence); the explicit pass is the
// mirror PassAction → TurnStateMachine.PassTurn → EndTurnProcess.
//
// INDEX CURRENCY (the AS-IS packet payloads are list indexes): entity-id action params are resolved to the
// AS-IS indexes here — ActiveCardList index (SetPlayCard/SetActCardSkill), turn-player field-permanent index
// (SetActSkill/attacker), non-turn field-permanent index (defender), and the FRAME ADAPTATION target index
// (TargetFrameID = turn-player field-permanent list index; -1 = empty-frame play — no frame/slot model,
// RD-P6C1-2 family).
// ============================================================================================================

namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Services;
using Cec = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TurnFlowDriver : IActionProcessor
{
    private readonly MetadataActionProcessor _inner = new();

    public async Task<ActionProcessResult> ProcessAsync(
        LegalAction action,
        EngineContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(context);

        if (TurnFlowPumpHost.Find(context) is null)
        {
            // Not a pump match — behave exactly like the default (OLD) processor.
            return await _inner.ProcessAsync(action, context, cancellationToken).ConfigureAwait(false);
        }

        using AmbientMatchContext.Scope _scope = AmbientMatchContext.Enter(context);
        var gameContext = new Cec.GameContext(context);

        switch (HeadlessActionTypes.Normalize(action.ActionType))
        {
            case HeadlessActionTypes.NormalizedPass:
                return Queue(action, context, new Cec.PassAction(), "Pass");

            case HeadlessActionTypes.NormalizedPlayCard:
            case HeadlessActionTypes.NormalizedDigivolve:
            {
                if (!TryReadEntityId(action, HeadlessActionParameterKeys.CardId, out HeadlessEntityId cardId))
                {
                    return ActionProcessResult.Failure("PlayCard action is missing a cardId.", Base(action));
                }

                int cardIndex = IndexOfActiveCard(gameContext, cardId);
                if (cardIndex < 0)
                {
                    return ActionProcessResult.Failure($"Card {cardId.Value} is not an active card.", Base(action));
                }

                // Digivolve / play-onto-target: TargetCardId → the AS-IS TargetFrameID (frame adaptation).
                int targetFrameId = -1;
                if (TryReadEntityId(action, HeadlessActionParameterKeys.TargetCardId, out HeadlessEntityId targetId))
                {
                    targetFrameId = IndexOfFieldPermanent(gameContext.TurnPlayer!, targetId);
                    if (targetFrameId < 0)
                    {
                        return ActionProcessResult.Failure($"Target {targetId.Value} is not a field permanent.", Base(action));
                    }
                }

                return Queue(
                    action, context,
                    new Cec.PlayCardAction(cardIndex, targetFrameId, null, -1, null),
                    "PlayCard");
            }

            case HeadlessActionTypes.NormalizedDeclareAttack:
            {
                if (!TryReadEntityId(action, HeadlessActionParameterKeys.AttackerId, out HeadlessEntityId attackerId))
                {
                    return ActionProcessResult.Failure("DeclareAttack action is missing an attackerId.", Base(action));
                }

                int attackerIndex = IndexOfFieldPermanent(gameContext.TurnPlayer!, attackerId);
                if (attackerIndex < 0)
                {
                    return ActionProcessResult.Failure($"Attacker {attackerId.Value} is not a field permanent.", Base(action));
                }

                int targetIndex = -1;   // -1 = no defender ⇒ direct (security) attack.
                if (TryReadEntityId(action, HeadlessActionParameterKeys.AttackTargetId, out HeadlessEntityId attackTargetId))
                {
                    targetIndex = IndexOfFieldPermanent(gameContext.NonTurnPlayer!, attackTargetId);
                    if (targetIndex < 0)
                    {
                        // An explicit target that fails to resolve is an illegal action, not a silent
                        // fallback into a direct (security) attack — same honesty contract as PlayCard.
                        return ActionProcessResult.Failure($"Attack target {attackTargetId.Value} is not a field permanent.", Base(action));
                    }
                }

                return Queue(
                    action, context,
                    new Cec.AttackPermanentAction(attackerIndex, targetIndex),
                    "DeclareAttack");
            }

            case HeadlessActionTypes.NormalizedActivateMain:
            {
                if (!TryReadEntityId(action, HeadlessActionParameterKeys.CardId, out HeadlessEntityId sourceId))
                {
                    return ActionProcessResult.Failure("ActivateMain action is missing a cardId.", Base(action));
                }

                int skillIndex = ReadInt(action, HeadlessActionParameterKeys.SkillIndex, defaultValue: 0);

                // A field permanent's top card declares via SetActSkill (permanent index); anything else via
                // SetActCardSkill (ActiveCardList index) — the AS-IS two packet kinds.
                int permanentIndex = IndexOfFieldPermanentByTopCard(gameContext.TurnPlayer!, sourceId);
                if (permanentIndex >= 0)
                {
                    return Queue(
                        action, context,
                        new Cec.ActivatePermanentAction(permanentIndex, skillIndex),
                        "ActivateMain(permanent)");
                }

                int cardIndex = IndexOfActiveCard(gameContext, sourceId);
                if (cardIndex < 0)
                {
                    return ActionProcessResult.Failure($"Card {sourceId.Value} is not an active card.", Base(action));
                }

                return Queue(
                    action, context,
                    new Cec.ActivateCardAction(cardIndex, skillIndex),
                    "ActivateMain(card)");
            }

            case HeadlessActionTypes.NormalizedActivateOption:
            {
                // (S3c-b) The AS-IS main-phase option play IS a card play (PlayCardClass → UseOptionClass);
                // the OLD ActivateOption currency maps onto the same PlayCard packet.
                if (!TryReadEntityId(action, HeadlessActionParameterKeys.CardId, out HeadlessEntityId optionId))
                {
                    return ActionProcessResult.Failure("ActivateOption action is missing a cardId.", Base(action));
                }

                int optionIndex = IndexOfActiveCard(gameContext, optionId);
                if (optionIndex < 0)
                {
                    return ActionProcessResult.Failure($"Card {optionId.Value} is not an active card.", Base(action));
                }

                return Queue(
                    action, context,
                    new Cec.PlayCardAction(optionIndex, -1, null, -1, null),
                    "ActivateOption(play)");
            }

            case HeadlessActionTypes.NormalizedSpecialPlay:
                // (RD-RLENV-05 — Option A landed, batch 7b) A pump-match SpecialPlay is DELIBERATELY not a pump
                // action. The DigiXros/Assembly cluster is fully ported (RD-P6C1-5 / RD-R5-04 상환), and its pump
                // EXECUTION is the mirror interactive pre-play selection reached through the ORDINARY PlayCard
                // packet: a HasDigiXros / HasAssembly card is offered by HeadlessLegalActionDispatcher
                // .PlayCardLegalActions (the re-homed enumeration lane) and this
                // Queue()s Cec.PlayCardAction, whose PlayCardClass.PlayCard runs SelectDigiXros/SelectAssembly
                // (CardController.cs:3387-3404). So the dispatcher does NOT offer SpecialPlay on a pump match
                // (HeadlessLegalActionDispatcher) — routing a SpecialPlay packet through a separate special-play
                // driver would be a REDUNDANT invented shortcut for the same play, and that driver is now DELETED
                // outright (SpecialPlay re-migration: no lane enumerates or executes SpecialPlay any more). The
                // seat is unreachable via the legal set (validator boundary rejects it first); it stays an honest
                // rejection here as a backstop.
                return ActionProcessResult.Illegal(
                    action,
                    "SpecialPlay is not a pump-match action (Option A: a DigiXros/Assembly play is the ordinary PlayCard entry, which routes through the mirror interactive SelectDigiXros/SelectAssembly).",
                    Base(action));

            case HeadlessActionTypes.NormalizedAdvancePhase:
            case HeadlessActionTypes.NormalizedEndTurn:
                // Decision 3 = B: the invented step cadence is not part of the pump surface — phases flow
                // automatically; the turn ends by Pass or by the in-pump EndTurnCheck threshold.
                return ActionProcessResult.Illegal(
                    action,
                    $"{action.ActionType} is not a pump-match action (the turn pump owns phase flow; pass explicitly instead).",
                    Base(action));

            default:
                return await _inner.ProcessAsync(action, context, cancellationToken).ConfigureAwait(false);
        }
    }

    private static ActionProcessResult Queue(
        LegalAction action,
        EngineContext context,
        Cec.MainPhaseAction packet,
        string kind)
    {
        // (리뷰3 P2-⑤) The AS-IS packet PRODUCER is gated by the UI: a main-phase packet can only be produced
        // while the turn player's main selection surface is armed (NextPhaseButton.OnClick `!IsSelecting &&
        // !isExecuting && !isSecurityCehck && TurnPlayer.isYou && TurnPhase == Main`; the SetMainPhase card
        // clicks are armed only during the :971-1170 selection wait, and the click tears the surface down, so
        // at most ONE packet exists per arm). AS-IS never drains Player.mainPhaseActions at a turn boundary —
        // the queue simply can never hold a stale packet. This gate is that producer discipline at the mirror
        // producer seat (this driver IS the AS-IS UI/packet transport): accept a packet only when the machine
        // is at THIS player's main selection wait with no packet already pending; otherwise the packet is
        // refused without touching the queue (the agent re-reads the mask and retries), so a second packet
        // validated against a stale mask can never leak across the turn boundary.
        Cec.GameContext gameContext = new(context);
        string? refusal =
            gameContext.TurnPhase != Cec.GameContext.phase.Main
                ? "the turn is not at the main phase"
            : gameContext.TurnPlayer is not { } turnPlayer || turnPlayer.PlayerId != action.PlayerId
                ? "the acting player is not the turn player"
            : context.ChoiceController.Current.IsPending
                ? "a choice is pending"
            : new Cec.Player(context, action.PlayerId).HasMainPhaseAction()
                ? "a queued main-phase packet is already awaiting the selection wait"
            : null;
        if (refusal is not null)
        {
            Dictionary<string, object?> refusedMetadata = Base(action);
            refusedMetadata["mainPhaseActionRefused"] = refusal;
            return ActionProcessResult.Failure(
                $"{kind} refused: {refusal} (the main-phase packet surface is not armed — AS-IS UI gate).",
                refusedMetadata);
        }

        new Cec.Player(context, action.PlayerId).QueueMainPhaseAction(packet);
        Dictionary<string, object?> metadata = Base(action);
        metadata["mainPhaseActionQueued"] = kind;
        return ActionProcessResult.Success($"{kind} queued to the main-phase seam.", metadata);
    }

    private static int IndexOfActiveCard(Cec.GameContext gameContext, HeadlessEntityId cardId)
    {
        List<Cec.CardSource> activeCards = gameContext.ActiveCardList;
        for (int i = 0; i < activeCards.Count; i++)
        {
            if (activeCards[i].InstanceId.Equals(cardId))
            {
                return i;
            }
        }

        return -1;
    }

    private static int IndexOfFieldPermanent(Cec.Player player, HeadlessEntityId permanentId)
    {
        List<Cec.Permanent> field = player.GetFieldPermanents();
        for (int i = 0; i < field.Count; i++)
        {
            if (field[i].InstanceId.Equals(permanentId))
            {
                return i;
            }
        }

        return -1;
    }

    private static int IndexOfFieldPermanentByTopCard(Cec.Player player, HeadlessEntityId topCardId)
    {
        List<Cec.Permanent> field = player.GetFieldPermanents();
        for (int i = 0; i < field.Count; i++)
        {
            if (field[i].InstanceId.Equals(topCardId) || field[i].TopCard?.InstanceId.Equals(topCardId) == true)
            {
                return i;
            }
        }

        return -1;
    }

    private static bool TryReadEntityId(LegalAction action, string key, out HeadlessEntityId id)
    {
        if (action.Parameters.TryGetValue(key, out object? raw) && raw is not null)
        {
            switch (raw)
            {
                case HeadlessEntityId entityId:
                    id = entityId;
                    return true;
                case string s when !string.IsNullOrEmpty(s):
                    id = new HeadlessEntityId(s);
                    return true;
            }
        }

        id = default;
        return false;
    }

    private static int ReadInt(LegalAction action, string key, int defaultValue)
    {
        if (action.Parameters.TryGetValue(key, out object? raw))
        {
            switch (raw)
            {
                case int i: return i;
                case long l: return (int)l;
                case string s when int.TryParse(s, out int parsed): return parsed;
            }
        }

        return defaultValue;
    }

    private static Dictionary<string, object?> Base(LegalAction action)
    {
        return new Dictionary<string, object?>
        {
            [HeadlessActionParameterKeys.ActionId] = action.Id.Value,
            [HeadlessActionParameterKeys.PlayerId] = action.PlayerId.Value,
            [HeadlessActionParameterKeys.ActionType] = action.ActionType,
        };
    }
}
