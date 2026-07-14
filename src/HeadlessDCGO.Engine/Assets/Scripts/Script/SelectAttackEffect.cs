// Source: Assets/Scripts/Script/SelectAttackEffect.cs
// Decision: PORT
// Category: AIUseful
// Namespace hint: HeadlessDCGO.Engine.Assets.Scripts.Script
//
// (BIG-BANG REBUILD v2 / R5-B) 1:1 mirror of the AS-IS DCGO SelectAttackEffect (a MonoBehaviour selection
// flow). SetUp captures the attacker, the player-target condition, the defender predicate, and the effect;
// the setters (SetWithoutTap/SetIsVortex/SetCanNotSelectNotAttack/custom message/before+after coroutines)
// mirror the AS-IS surface verbatim. The condition/candidate-enumeration methods (CanTarget / CanAttackDigimon
// / CanAttackPlayer / active / CanEndSelect) are ported 1:1 on the R1 Permanent.CanAttack /
// CanAttackTargetDigimon getters.
//
// ADAPTATION (substrate translations only, same shape as SelectPermanentEffect / CardObjectController's
// IZoneMover delegation):
//   * The AS-IS target-selection UI + Photon-RPC/WaitUntil transport (SelectAttackEffect.cs:227-723) is the
//     ChoiceProvider request — the established Select* transport. The SAME candidates (CanTarget field
//     permanents + the "attack the player/security" click target) are offered; the pick resolves to _defender
//     (a Permanent, or null for a direct player attack) or _noSelect ("Not Attack").
//   * The attack INITIATION (AS-IS `attackProcess.Attack(_attacker, _defender, _cardEffect, _withoutTap,
//     _beforeOnAttackCoroutine)`, :1013-1017) is delegated to the mirror AttackProcess surface via the shared
//     declaration chokepoint AttackDeclarationCommons.Declare (== EffectDrivenAttack.Initiate's declaration,
//     the MIG1 split: declare on the controller, then run the AS-IS Attack() sequence). This component supplies
//     the in-place SelectAttackEffect the R2-A callers (Overclock/Vortex/Execute) previously reached through
//     the EffectDrivenAttack.RequestChoice ADAPTATION.
//   * `_beforeOnAttackCoroutine` is NOT threaded through the declaration chokepoint (Declare passes null) —
//     design item MIG1-BEFOREONATTACK (latent; none of the bridged callers set a before-coroutine, and the
//     [On Attack] window fires inside Attack, so it cannot run after Declare). The AS-IS SetCanNotSelectNotAttack
//     "mandatory attack" (no skip) IS honoured (canSkip = _canSelectNotAttack).

namespace HeadlessDCGO.Engine.Assets.Scripts.Script;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class SelectAttackEffect
{
    // (transport strip) AS-IS `SecurityIndex = -1` / `NopIndex = -2` (SelectAttackEffect.cs:10-11) encode the
    // "attack the player" and "Not Attack" picks over the Photon RPC int transport; the ChoiceProvider request
    // carries a synthetic player candidate + canSkip instead.

    private Permanent? _attacker;
    private Func<bool>? _canAttackPlayerCondition;
    private Func<Permanent, bool>? _defenderCondition;
    private bool _canSelectNotAttack = true;
    private bool _withoutTap;
    private bool _isVortex;

    private string? _customMessage;
    private string? _customMessage_Enemy;   // AS-IS opponent-side UI text — no headless effect (single process).

    private Permanent? _defender;
    private ICardEffect? _cardEffect;
    private Func<Task>? _beforeOnAttackCoroutine;   // (substrate) AS-IS Func<IEnumerator> -> Func<Task>.
    private Func<Task>? _afterOnAttackCoroutine;

    /// <summary>AS-IS public field <c>_noSelect</c> — set by <see cref="Activate"/> when the select player took
    /// the "Not Attack" opt-out.</summary>
    public bool _noSelect;

    private EngineContext? _context;

    /// <summary>(bridge) The match context the AS-IS <c>GManager.instance.GetComponent&lt;SelectAttackEffect&gt;()</c>
    /// route injects (the AS-IS component reads the same state through the GManager singleton).</summary>
    internal void AttachContext(EngineContext context) => _context = context;

    /// <summary>AS-IS <c>SetUp</c> (SelectAttackEffect.cs:13-31) — the attacker, the player-attack condition, the
    /// defender predicate, and the effect. Resets exactly the fields AS-IS resets.</summary>
    public void SetUp(
        Permanent attacker,
        Func<bool> canAttackPlayerCondition,
        Func<Permanent, bool> defenderCondition,
        ICardEffect cardEffect)
    {
        _attacker = attacker;
        _canAttackPlayerCondition = canAttackPlayerCondition;
        _defenderCondition = defenderCondition;
        _cardEffect = cardEffect;

        _canSelectNotAttack = true;
        _withoutTap = false;
        _isVortex = false;
        _customMessage = null;
        _customMessage_Enemy = null;
        _beforeOnAttackCoroutine = null;
        _afterOnAttackCoroutine = null;
    }

    /// <summary>AS-IS <c>SetUpCustomMessage</c> (:33-37).</summary>
    public void SetUpCustomMessage(string customMessage, string customMessage_Enemy)
    {
        _customMessage = customMessage;
        _customMessage_Enemy = customMessage_Enemy;
    }

    /// <summary>AS-IS <c>SetCanNotSelectNotAttack</c> (:39-42) — the attack is mandatory (no "Not Attack" skip).</summary>
    public void SetCanNotSelectNotAttack() => _canSelectNotAttack = false;

    /// <summary>AS-IS <c>SetWithoutTap</c> (:44-47) — the attacker is NOT suspended by the attack.</summary>
    public void SetWithoutTap() => _withoutTap = true;

    /// <summary>AS-IS <c>SetIsVortex</c> (:49-52) — a Vortex attack (unsuspended Digimon are also targetable,
    /// threaded through <c>CanAttackTargetDigimon(..., isVortex: true)</c>).</summary>
    public void SetIsVortex() => _isVortex = true;

    /// <summary>AS-IS <c>SetBeforeOnAttackCoroutine</c> (:54-57) — see MIG1-BEFOREONATTACK.</summary>
    public void SetBeforeOnAttackCoroutine(Func<Task> beforeOnAttackCoroutine) =>
        _beforeOnAttackCoroutine = beforeOnAttackCoroutine;

    /// <summary>AS-IS <c>SetAfterOnAttackCoroutine</c> (:59-62) — runs once the attack sequence completes.</summary>
    public void SetAfterOnAttackCoroutine(Func<Task> afterOnAttackCoroutine) =>
        _afterOnAttackCoroutine = afterOnAttackCoroutine;

    /// <summary>AS-IS <c>CanTarget</c> (SelectAttackEffect.cs:81-108): the attacker can attack, can attack THIS
    /// Digimon (R1 <c>CanAttackTargetDigimon</c>), and the per-effect defender predicate accepts it.</summary>
    private bool CanTarget(Permanent permanent)
    {
        if (_attacker != null)
        {
            if (_attacker.TopCard != null)
            {
                if (_attacker.CanAttack(_cardEffect, _withoutTap, _isVortex))
                {
                    if (_attacker.CanAttackTargetDigimon(permanent, _cardEffect, _withoutTap, _isVortex))
                    {
                        if (_defenderCondition != null)
                        {
                            if (!_defenderCondition(permanent))
                            {
                                return false;
                            }
                        }

                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>AS-IS <c>CanAttackDigimon</c> (:110-125): any field permanent (both players) passes
    /// <see cref="CanTarget"/>.</summary>
    public bool CanAttackDigimon()
    {
        foreach (Player player in new GameContext(RequireContext()).Players)
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                if (CanTarget(permanent))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>AS-IS <c>CanAttackPlayer</c> (:127-155): the attacker can attack, can attack the player (R1
    /// <c>CanAttackTargetDigimon(null, ...)</c>), and the per-effect player-attack condition accepts it.</summary>
    private bool CanAttackPlayer()
    {
        if (_attacker != null)
        {
            if (_attacker.TopCard != null)
            {
                if (_attacker.CanAttack(_cardEffect, _withoutTap, _isVortex))
                {
                    if (_attacker.CanAttackTargetDigimon(null, _cardEffect, _withoutTap, _isVortex))
                    {
                        if (_canAttackPlayerCondition != null)
                        {
                            if (!_canAttackPlayerCondition())
                            {
                                return false;
                            }
                        }

                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>AS-IS <c>active</c> (:157-174): a Digimon OR player target exists, and no attack is in flight.</summary>
    public bool active()
    {
        if (!CanAttackDigimon())
        {
            if (!CanAttackPlayer())
            {
                return false;
            }
        }

        if (AttackProcess.For(RequireContext()).IsAttacking)
        {
            return false;
        }

        return true;
    }

    /// <summary>AS-IS <c>CanEndSelect</c> (:176-197): the null pick (player) requires <see cref="CanAttackPlayer"/>,
    /// a Digimon pick requires <see cref="CanAttackDigimon"/>.</summary>
    private bool CanEndSelect(Permanent? selectedPermanent)
    {
        if (selectedPermanent == null)
        {
            if (!CanAttackPlayer())
            {
                return false;
            }
        }
        else
        {
            if (!CanAttackDigimon())
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>AS-IS <c>Activate</c> (SelectAttackEffect.cs:199-618) — the target selection then the attack.
    /// Guards (:203-205) verbatim; UI/Photon strips (AS-IS anchors): Off*Target + IsSelecting (:214-224 — the
    /// choice-pause substrate), message/side-bar/outline/back-button plumbing + the RPC/WaitUntil transport
    /// (:227-723), and the visual cleanup region (:725-746). The enumeration + `attackProcess.Attack` initiation
    /// (:1013-1017) are the ADAPTATION described in the file header.</summary>
    public async Task Activate()
    {
        _defender = null;
        _noSelect = false;

        if (_attacker == null)
        {
            return;
        }

        if (_attacker.TopCard == null)
        {
            return;
        }

        if (!_attacker.CanAttack(_cardEffect, _withoutTap, _isVortex))
        {
            return;
        }

        if (!active())
        {
            return;
        }

        EngineContext context = RequireContext();

        // AS-IS candidate scan (:294-320): every CanTarget field permanent is an OnClickFieldPermanentCard target.
        var candidates = new List<ChoiceCandidate>();
        var targetByCandidateId = new Dictionary<HeadlessEntityId, Permanent>();

        foreach (Player player in new GameContext(context).Players)
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                if (CanTarget(permanent) && !targetByCandidateId.ContainsKey(permanent.InstanceId))
                {
                    candidates.Add(EffectChoiceHelpers.Candidate(
                        permanent.InstanceId, permanent.InstanceId.Value, ChoiceZone.BattleArea,
                        isSelectable: true, permanent.OwnerId));
                    targetByCandidateId[permanent.InstanceId] = permanent;
                }
            }
        }

        // AS-IS "attack the player / security" click target (:280-292) — a synthetic candidate resolving to a
        // null _defender (direct attack).
        var playerTargetId = new HeadlessEntityId($"{_attacker.InstanceId.Value}:attack-player");
        if (CanAttackPlayer())
        {
            candidates.Add(EffectChoiceHelpers.Candidate(
                playerTargetId, "Attack the player", ChoiceZone.BattleArea,
                isSelectable: true, _attacker.OwnerId));
        }

        if (candidates.Count == 0)
        {
            // active() already guaranteed a target; defensive.
            return;
        }

        // canSkip / minCount = AS-IS "Not Attack" back-button (:322-331), suppressed by SetCanNotSelectNotAttack.
        var request = new ChoiceRequest(
            ChoiceType.Permanent, _attacker.OwnerId, BuildMessage(),
            minCount: _canSelectNotAttack ? 0 : 1, maxCount: 1, canSkip: _canSelectNotAttack,
            ChoiceZone.BattleArea, candidates);

        ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request).ConfigureAwait(false);

        if (result.IsSkipped || result.SelectedIds.Count == 0)
        {
            _noSelect = true;
        }
        else
        {
            HeadlessEntityId picked = result.SelectedIds[0];
            if (picked == playerTargetId)
            {
                _defender = null;
            }
            else if (targetByCandidateId.TryGetValue(picked, out Permanent? defenderPermanent))
            {
                _defender = defenderPermanent;
            }
        }

        if (_noSelect)
        {
            return;
        }

        if (!CanEndSelect(_defender))
        {
            return;
        }

        // (ADAPTATION) attack initiation — AS-IS `attackProcess.Attack(_attacker, _defender, _cardEffect,
        // _withoutTap, _beforeOnAttackCoroutine)`. The mirror declares on the controller then runs the AS-IS
        // Attack() sequence through the shared chokepoint (== EffectDrivenAttack.Initiate's declaration).
        HeadlessPlayerId attackingPlayer = _attacker.OwnerId;
        HeadlessEntityId? attackEffectSourceId = _cardEffect?.EffectSourceCard?.InstanceId;

        if (_defender is not null)
        {
            Headless.Runtime.AttackDeclarationCommons.Declare(
                context, attackingPlayer, _attacker.InstanceId,
                _defender.OwnerId, _defender.InstanceId, isDirectAttack: false,
                _withoutTap, attackEffectSourceId);
        }
        else
        {
            // AS-IS defending player = `_attacker.TopCard.Owner.Enemy` (the direct-attack security target).
            Player? enemy = new Player(context, attackingPlayer).Enemy;
            if (enemy is null)
            {
                return;
            }

            Headless.Runtime.AttackDeclarationCommons.Declare(
                context, attackingPlayer, _attacker.InstanceId,
                enemy.PlayerId, targetId: null, isDirectAttack: true,
                _withoutTap, attackEffectSourceId);
        }

        // AS-IS after-attack coroutine (:1019-1020).
        if (_afterOnAttackCoroutine != null)
        {
            await _afterOnAttackCoroutine().ConfigureAwait(false);
        }
    }

    /// <summary>AS-IS player-facing prompt (:236-260): the custom message when set, else the Digimon/player
    /// default text.</summary>
    private string BuildMessage()
    {
        if (!string.IsNullOrEmpty(_customMessage))
        {
            return _customMessage!;
        }

        return CanAttackDigimon() ? "Which target will you attack?" : "Will you attack to the opponent?";
    }

    private EngineContext RequireContext() =>
        _context
        ?? _cardEffect?.EffectSourceCard?.Context
        ?? _attacker?.TopCard?.Context
        ?? throw new InvalidOperationException(
            "SelectAttackEffect has no EngineContext — obtain the instance via " +
            "GManager.instance.GetComponent<SelectAttackEffect>() or AttachContext.");
}
