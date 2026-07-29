// ============================================================================================================
// A SEAT THAT PLAYS AT RANDOM — the policy the stage-2 gate calls for ("무작위 대전 N판 완주").
//
// WHY THE LEGALITY PREDICATES LOOK FAMILIAR. The OPPONENT seat in vs-AI mode is played by the AS-IS engine
// itself (`#region AIモード`, TurnStateMachine.cs:990-1150): it filters with `CanAttack`,
// `CanAttackTargetDigimon`, `CanPlayFromHandDuringMainPhase` and `CanPlayCardTargetFrame`, then picks at
// random. This class gives the YOU seat the same move surface through the seam a human uses —
// `QueueMainPhaseAction` — with the SAME predicates, so every action it queues is one the AS-IS UI would have
// allowed a click to produce. The predicates are fidelity; the WEIGHTS are not (this is the substrate's own
// policy layer, which an RL agent later replaces).
//
// WHAT IT DOES NOT DO. DNA/jogress, burst and app-fusion plays need frame-pair arguments the simple play path
// does not construct — `SetPlayCard` ignores those arguments unless exactly paired, so passing none plays the
// card the ordinary way. Selection prompts stay on the minimal channel answers (SelectionChannels); random
// selection answers are a later refinement, not part of this seat.
// ============================================================================================================

namespace HeadlessDCGO.Engine.Headless.Choices;

/// <summary>Plays random legal main-phase actions on a deterministic seed; answers prompts minimally.</summary>
public sealed class RandomVirtualPlayer : VirtualPlayer
{
    private readonly Random _rng;

    public RandomVirtualPlayer(int seed) => _rng = new Random(seed);

    /// <summary>Actions already tried SINCE THE GAME STATE LAST CHANGED. An action the engine asked us to
    /// replace without anything changing was a no-op (e.g. a DigiXros play that aborted when the minimal
    /// channel answers selected no materials) — retrying it forever livelocks the match, which is exactly
    /// what a human avoids by clicking something else. Cleared whenever the state digest moves.</summary>
    private readonly HashSet<string> _tried = new(StringComparer.Ordinal);

    private string _lastDigest = "";

    /// <summary>Prompts answered so far, for reporting.</summary>
    public List<ChoicePrompt> Answered { get; } = new();

    protected override void Record(ChoicePrompt prompt) => Answered.Add(prompt);

    protected override bool Decide(SelectCardPanel panel, ChoicePrompt prompt)
    {
        Record(prompt);
        panel.OnClickNotSelectButton();

        return true;
    }

    protected override bool Act(Player actor, ChoicePrompt prompt)
    {
        Record(prompt);

        GameContext context = GManager.instance!.turnStateMachine.gameContext;

        string digest = Digest(context);

        if (digest != _lastDigest)
        {
            _lastDigest = digest;
            _tried.Clear();
        }

        MainPhaseAction action = PickAttack(actor, context) ?? PickPlay(actor, context) ?? new PassAction();
        GManager.instance.turnStateMachine.QueueMainPhaseAction(actor, action);

        return true;
    }

    /// <summary>What "the state changed" means for the no-op guard: zone sizes, memory and suspensions —
    /// everything a resolved action moves. An aborted action moves none of it.</summary>
    private static string Digest(GameContext context)
    {
        static string Seat(Player p) =>
            $"{p.HandCards.Count}.{p.LibraryCards.Count}.{p.SecurityCards.Count}.{p.TrashCards.Count}" +
            $".{p.GetFieldPermanents().Count}.{p.GetFieldPermanents().Count(x => x.IsSuspended)}";

        return $"{context.Memory}|{Seat(context.You)}|{Seat(context.Opponent)}";
    }

    /// <summary>An attack the AS-IS UI would have allowed, or null. Mirrors the opponent-AI filter:
    /// attacker `CanAttack`, defender `CanAttackTargetDigimon(defender)`, security `CanAttackTargetDigimon(null)`
    /// (TurnStateMachine.cs:994-1056). Index -1 is the AS-IS "attack the player" value (:1599).</summary>
    private MainPhaseAction? PickAttack(Player actor, GameContext context)
    {
        if (_rng.Next(2) == 0)
        {
            return null;    // half the turns look to play first, so both orders get exercised
        }

        List<Permanent> attackers = actor.GetFieldPermanents().Where(p => p.CanAttack(null)).ToList();

        if (attackers.Count == 0)
        {
            return null;
        }

        Permanent attacker = attackers[_rng.Next(attackers.Count)];
        List<Permanent> enemies = context.NonTurnPlayer.GetFieldPermanents();
        List<int> targets = new();

        if (attacker.CanAttackTargetDigimon(null, null))
        {
            targets.Add(-1);
        }

        for (int i = 0; i < enemies.Count; i++)
        {
            if (attacker.CanAttackTargetDigimon(enemies[i], null))
            {
                targets.Add(i);
            }
        }

        if (targets.Count == 0)
        {
            return null;
        }

        int attackerIndex = actor.GetFieldPermanents().IndexOf(attacker);
        int target = targets[_rng.Next(targets.Count)];

        return _tried.Add($"atk:{attackerIndex}:{target}")
            ? new AttackPermanentAction(attackerIndex, target)
            : null;
    }

    /// <summary>A card play the AS-IS UI would have allowed, or null. Same filter as the opponent AI
    /// (TurnStateMachine.cs:1067-1131): hand cards with `CanPlayFromHandDuringMainPhase`; a permanent needs a
    /// frame passing `CanPlayCardTargetFrame`, preferring the AS-IS `PreferredFrame` for an empty one.</summary>
    private MainPhaseAction? PickPlay(Player actor, GameContext context)
    {
        List<CardSource> playable = actor.HandCards
            .Where(c => c.CanPlayFromHandDuringMainPhase)
            .Where(c => !_tried.Contains($"play:{context.ActiveCardList.IndexOf(c)}"))
            .ToList();

        if (playable.Count == 0)
        {
            return null;
        }

        CardSource card = playable[_rng.Next(playable.Count)];
        int targetFrame = -1;

        if (card.IsPermanent)
        {
            List<FieldCardFrame> frames = actor.fieldCardFrames
                .Where(f => card.CanPlayCardTargetFrame(f, true, null)).ToList();

            if (frames.Count == 0)
            {
                return null;
            }

            FieldCardFrame chosen = frames[_rng.Next(frames.Count)];

            // 빈 프레임 픽은 -1 위임: 큐잉 시점에 프레임을 박제하면 소비 시점까지의 비동기 이동이 그
            // 프레임을 점유해 검증 없는 진화로 둔갑한다(TOCTOU, 실측 2026-07-30 00:1x). -1이면
            // PlayCardClass가 소비 시점에 PreferredFrame을 재계산한다(CardController.cs:1310).
            targetFrame = chosen.IsEmptyFrame() ? -1 : chosen.FrameID;
        }

        int cardIndex = context.ActiveCardList.IndexOf(card);

        if (cardIndex < 0)
        {
            return null;
        }

        _tried.Add($"play:{cardIndex}");

        return new PlayCardAction(cardIndex, targetFrame, null, -1, null);
    }
}
