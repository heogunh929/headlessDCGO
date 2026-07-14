// Source: Assets/Scripts/Script/SelectPermanentEffect.cs
// Decision: PORT
// Category: AIUseful
// Migration: Port core engine source
// Namespace hint: HeadlessDCGO.Engine.Assets.Scripts.Script
//
// AS-IS mirror of the original DCGO SelectPermanentEffect (a MonoBehaviour selection flow).
// The original SetUp(...) captures a target predicate, max count, the canNoSelect/canEndNotMax
// rules, and a Mode that decides what is done to each selected Permanent. Headless port keeps the
// same authoring shape but is deterministic:
//   (1) BuildRequest  — enumerate the live board, filter by the target predicate, and build a
//                       Permanent ChoiceRequest honouring max/canNoSelect/canEndNotMax (CV-A2 / F-2).
//   (2) BuildMutations — map the selection Mode to MatchStateMutation(s) per selected target,
//                       reusing the existing mutation vocabulary (CV-A2 / F-2.3 / F-2.5).
// Resolution itself reuses EffectChoiceHelpers.ResolveAsync / DeferredChoiceProvider; this class
// only owns candidate enumeration and the Mode→mutation mapping.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class SelectPermanentEffect
{
    // 1:1 with the original SelectPermanentEffect.Mode.
    public enum Mode
    {
        Tap,
        UnTap,
        Destroy,
        Bounce,
        PutLibraryBottom,
        PutLibraryTop,
        PutSecurityBottom,
        PutSecurityTop,
        Degenerate,
        Attack,
        Custom,
    }

    private HeadlessPlayerId _selectPlayer;
    private Func<HeadlessEntityId, bool> _canTargetCondition = static _ => true;
    private int _maxCount = 1;
    private bool _canNoSelect;
    private bool _canEndNotMax;
    private Mode _mode = Mode.Custom;
    private HeadlessEntityId _sourceEntityId = new("select");
    private EngineContext? _context;
    private string _message = "Select target permanent(s).";
    private bool _faceUp;
    private int _degenerationCount = 1;
    private bool _canAttackPlayer = true;
    private Func<HeadlessEntityId, bool>? _defenderCondition;
    private Func<IReadOnlyList<HeadlessEntityId>, bool>? _canEndSelectCondition;

    // ===== (bridge W4) AS-IS SetUp(...).Activate() surface state (SelectPermanentEffect.cs:98-149) =========
    // The AS-IS-verbatim card corpus reaches this class via GManager.instance.GetComponent<SelectPermanentEffect>()
    // + the 11-param AS-IS SetUp + Activate(); these fields mirror the AS-IS private fields that surface uses.
    // AS-IS field names are kept where free; where the name is taken by a legacy mirror member of a DIFFERENT
    // shape, the W3 naming convention applies (…_Permanents suffix for the AS-IS List<Permanent> shapes).
    private Func<List<Permanent>, Permanent, bool>? _canTargetCondition_ByPreSelecetedList;
    private Func<List<Permanent>, bool>? _canEndSelectCondition_Permanents;
    private Func<Permanent, Task>? _selectPermanentCoroutine;
    private Func<List<Permanent>, Task>? _afterSelectPermanentCoroutine;
    private ICardEffect? _cardEffect;
    private bool _isLocal;                       // AS-IS Photon marker — no headless effect (single process).
    private bool _isdigiXros;                    // AS-IS UI banner flag — no headless effect.
    private string? _customMessage;
    private string? _customMessage_Enemy;        // AS-IS opponent-side UI text — no headless effect.
    private string? _customBackButtonMessage;    // AS-IS "No Selection" button label — no headless effect.
    private Func<Permanent, bool>? _defenderCondition_Permanent;   // AS-IS SetDefenderCondition shape.

    // AS-IS `List<Permanent> _targetPermanents` (:142) — the selected permanents of the last Activate().
    private List<Permanent> _targetPermanents = new();

    /// <summary>AS-IS public field <c>_noSelect</c> (SelectPermanentEffect.cs:144) — set by Activate() when the
    /// select player took the "No Selection" opt-out.</summary>
    public bool _noSelect;

    /// <summary>(bridge W4) The match context the AS-IS <c>GManager.instance.GetComponent&lt;…&gt;()</c> route
    /// injects (the AS-IS component reads the same state through the GManager singleton).</summary>
    internal void AttachContext(EngineContext context) => _context = context;

    /// <summary>Mirrors the original SetUp: who selects, the per-target predicate, the count rules, and
    /// the action mode. <paramref name="sourceEntityId"/> is the effect source the mutations are attributed
    /// to (the original threads this through <c>cardEffect.EffectSourceCard</c>).</summary>
    public void SetUp(
        HeadlessPlayerId selectPlayer,
        Func<HeadlessEntityId, bool> canTargetCondition,
        int maxCount,
        bool canNoSelect,
        bool canEndNotMax,
        Mode mode,
        HeadlessEntityId sourceEntityId,
        EngineContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(canTargetCondition);
        if (maxCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCount), "Max count must be at least 1.");
        }

        _selectPlayer = selectPlayer;
        _canTargetCondition = canTargetCondition;
        _maxCount = maxCount;
        _canNoSelect = canNoSelect;
        _canEndNotMax = canEndNotMax;
        _mode = mode;
        _sourceEntityId = sourceEntityId.IsEmpty ? new HeadlessEntityId("select") : sourceEntityId;
        _context = context;
    }

    public void SetUpCustomMessage(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            _message = message;
            // (bridge W4) a 1-arg call from an AS-IS-verbatim card (AS-IS SetUpCustomMessage has optional
            // parameters, so 1-arg calls bind HERE by C# no-optional-fill preference) must also reach the
            // AS-IS Activate() prompt channel; legacy consumers never read _customMessage, so this is inert
            // for them.
            _customMessage = message;
        }
    }

    /// <summary>(PutSecurity*) whether returned cards are placed face up.</summary>
    public void SetFaceUp(bool faceUp) => _faceUp = faceUp;

    /// <summary>(B5) AS-IS <c>_degenerationCount</c> — how many sources <see cref="Mode.Degenerate"/>
    /// removes per selected permanent (default 1).</summary>
    public void SetDegenerationCount(int count) => _degenerationCount = Math.Max(1, count);

    /// <summary>(B5) AS-IS ctor's <c>canAttackPlayer</c> / <c>defenderCondition</c> — forwarded into the
    /// <see cref="Mode.Attack"/> sub-flow (<c>SelectAttackEffect.SetUp(canAttackPlayerCondition,
    /// defenderCondition, ...)</c>).</summary>
    public void SetAttackOptions(bool canAttackPlayer, Func<HeadlessEntityId, bool>? defenderCondition = null)
    {
        _canAttackPlayer = canAttackPlayer;
        _defenderCondition = defenderCondition;
    }

    /// <summary>(B5) AS-IS <c>CanEndSelect</c>'s combination gate (<c>_canEndSelectCondition(permanents)</c>)
    /// — constrains which selection SETS are legal (e.g. "two of different colours"). Consult via
    /// <see cref="IsValidSelection"/> when resolving the choice.</summary>
    public void SetCanEndSelectCondition(Func<IReadOnlyList<HeadlessEntityId>, bool> canEndSelectCondition)
    {
        ArgumentNullException.ThrowIfNull(canEndSelectCondition);
        _canEndSelectCondition = canEndSelectCondition;
    }

    /// <summary>(B5) whether <paramref name="selection"/> satisfies the AS-IS combination gate (true when
    /// none is configured).</summary>
    public bool IsValidSelection(IReadOnlyList<HeadlessEntityId> selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        return _canEndSelectCondition is null || _canEndSelectCondition(selection);
    }

    /// <summary>Enumerate the battle areas of <paramref name="players"/>, filter by the target predicate,
    /// and build the Permanent ChoiceRequest. Count rules follow the original:
    /// canNoSelect ⇒ may finish with zero (skippable); canEndNotMax ⇒ may finish below max (min 1);
    /// otherwise an exact pick of max is required. Counts clamp to the available candidate pool.</summary>
    public ChoiceRequest BuildRequest(IZoneStateReader zones, IEnumerable<HeadlessPlayerId> players)
    {
        ArgumentNullException.ThrowIfNull(zones);
        ArgumentNullException.ThrowIfNull(players);

        var candidates = new List<ChoiceCandidate>();
        foreach (HeadlessPlayerId player in players)
        {
            foreach (HeadlessEntityId id in zones.GetCards(player, ChoiceZone.BattleArea))
            {
                // (d-remediation) AS-IS Permanent.CanSelectBySkill — a permanent with an untargetability
                // restriction is excluded from the candidate pool entirely (never offered as a choice).
                if (_canTargetCondition(id) && !IsUntargetableBySkill(id))
                {
                    candidates.Add(EffectChoiceHelpers.Candidate(id, id.Value, ChoiceZone.BattleArea, isSelectable: true, player));
                }
            }
        }

        int available = candidates.Count;
        int maxCount = Math.Min(_maxCount, available);
        int minCount = _canNoSelect ? 0 : (_canEndNotMax ? Math.Min(1, maxCount) : maxCount);
        bool canSkip = _canNoSelect;

        ChoiceRequest request = EffectChoiceHelpers.CreatePermanentRequest(_selectPlayer, _message, minCount, maxCount, canSkip, candidates);
        // (P2) the AS-IS combination gate (CanEndSelect) rides on the request so the choice controller
        // rejects an illegal SET centrally (try-reject-retry).
        return _canEndSelectCondition is null ? request : request with { SelectionValidator = _canEndSelectCondition };
    }

    /// <summary>(d-remediation, true-scan) AS-IS <c>Permanent.CanSelectBySkill(skill)</c>: SCAN every field
    /// permanent's continuous effects (1:1 with the original's nested foreach), and for each
    /// <c>CanNotSelectBySkill</c> effect that is usable, evaluate its JOINT predicate
    /// <c>CanNotSelectBySkill(candidate, skillSource)</c>. Any match ⇒ the candidate cannot be chosen. No context
    /// (legacy call path) ⇒ not untargetable.</summary>
    private bool IsUntargetableBySkill(HeadlessEntityId candidateId)
    {
        if (_context is null || candidateId.IsEmpty)
        {
            return false;
        }

        // (joint-migration) canonical scan (AS-IS Permanent.CanSelectBySkill): f(candidate, selecting skill source).
        // The sentinel default source resolves to no instance ⇒ RestrictionScan's counterpart is null and the joint
        // wrapper falls back to the candidate itself (self-cause), matching the prior behaviour.
        return Headless.Runtime.RestrictionScan.IsRestricted(
            _context, RestrictionHelpers.CannotBeSelectedBySkillKey, candidateId, _sourceEntityId);
    }

    /// <summary>Map the configured Mode to one mutation per selected target. Attack/Custom yield no
    /// built-in mutation (the original delegates those to a bespoke coroutine); Degenerate is the
    /// de-digivolve subsystem (D-4) and is not yet a mutation.</summary>
    public IReadOnlyList<EffectMutation> BuildMutations(IEnumerable<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(selected);

        var mutations = new List<EffectMutation>();
        foreach (HeadlessEntityId target in selected)
        {
            EffectMutation? mutation = BuildMutation(target);
            if (mutation is not null)
            {
                mutations.Add(mutation);
            }
        }

        return mutations;
    }

    /// <summary>Resolve the Mode's mutations against a live mutation sink (per-target, in selection order).</summary>
    public void Apply(MatchStateMutationSink sink, IEnumerable<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(sink);
        foreach (EffectMutation mutation in BuildMutations(selected))
        {
            sink.Apply(mutation);
        }
    }

    private EffectMutation? BuildMutation(HeadlessEntityId target)
    {
        return _mode switch
        {
            Mode.Tap => Mutation(MatchStateMutationSink.SuspendKind, target),
            Mode.UnTap => Mutation(MatchStateMutationSink.UnsuspendKind, target),
            Mode.Destroy => Mutation(MatchStateMutationSink.DeleteKind, target),
            Mode.Bounce => Mutation(MatchStateMutationSink.ReturnToHandKind, target),
            Mode.PutLibraryTop => Mutation(MatchStateMutationSink.ReturnToDeckTopKind, target),
            Mode.PutLibraryBottom => Mutation(MatchStateMutationSink.ReturnToDeckBottomKind, target),
            // (B5) NOTE: AS-IS guards every placement with Owner.CanAddSecurity(cardEffect); the restriction
            // effect (CannotAddSecurityClass) is an unported skeleton with no grants — fold the gate here
            // when it lands (fidelity_debt.md, same seam as Ascension/Save).
            Mode.PutSecurityTop => SecurityMutation(target, toBottom: false),
            Mode.PutSecurityBottom => SecurityMutation(target, toBottom: true),
            // (B5) AS-IS Degenerate: new IDegeneration(selected, _degenerationCount, effect) — the D-4
            // de-digivolve subsystem (rookie floor + WhenTopCardTrashed are inside the helper, 1:1 with
            // IDegeneration's Level==3 stop).
            Mode.Degenerate => new EffectMutation(
                MatchStateMutationSink.DeDigivolveKind,
                _sourceEntityId,
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [MatchStateMutationSink.TargetEntityIdKey] = target.Value,
                    [MatchStateMutationSink.CountKey] = _degenerationCount,
                }),
            Mode.Attack or Mode.Custom => null,
            _ => null,
        };
    }

    /// <summary>(B5/P3) AS-IS <see cref="Mode.Attack"/>: each selected permanent that can attack runs a
    /// <c>SelectAttackEffect</c> sub-flow honouring <c>canAttackPlayer</c>/<c>defenderCondition</c>,
    /// SEQUENTIALLY (SelectPermanentEffect.cs:1009-1027). The headless flow opens the target choice for the
    /// first attacker and queues the rest — the AttackPipeline dequeues the next once each attack fully
    /// completes (each re-checked alive/legal on its turn). Returns true when a choice opened.</summary>
    public bool TryOpenAttack(HeadlessDCGO.Engine.Headless.Bridge.EngineContext context, IEnumerable<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(selected);
        if (_mode != Mode.Attack)
        {
            return false;
        }

        // Normal attack targeting (suspended Digimon only) + the AS-IS per-effect narrowing.
        var options = new HeadlessDCGO.Engine.Headless.Runtime.EffectAttackOptions(
            WithoutTap: false,
            AllowPlayerTarget: _canAttackPlayer,
            AllowDigimonTarget: true,
            TargetUnsuspended: false)
        {
            DefenderCondition = _defenderCondition,
        };
        return HeadlessDCGO.Engine.Headless.Runtime.EffectDrivenAttack.RequestQueuedChoices(context, selected.ToArray(), options);
    }

    private EffectMutation Mutation(string kind, HeadlessEntityId target)
    {
        return new EffectMutation(
            kind,
            _sourceEntityId,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [MatchStateMutationSink.TargetEntityIdKey] = target.Value,
            });
    }

    private EffectMutation SecurityMutation(HeadlessEntityId target, bool toBottom)
    {
        return new EffectMutation(
            MatchStateMutationSink.AddToSecurityKind,
            _sourceEntityId,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [MatchStateMutationSink.TargetEntityIdKey] = target.Value,
                [MatchStateMutationSink.ToBottomKey] = toBottom,
                [MatchStateMutationSink.FaceUpKey] = _faceUp,
            });
    }

    // ================================================================================================
    // (bridge W4) AS-IS SetUp(...).Activate() surface — 1:1 with DCGO SelectPermanentEffect.cs, the
    // `GManager.instance.GetComponent<SelectPermanentEffect>()` flow verbatim card ports use
    // (BT1_017/023/092/094 pattern). Substrate translations only (IEnumerator→Task, Player→
    // HeadlessPlayerId, Func<Permanent,bool> canTargetCondition → the established Func<HeadlessEntityId,
    // bool> id idiom); UI/Photon statements stripped with their AS-IS line anchors cited in Activate().
    // See docs/audit/rebuild_bridge_w4_notes.md.
    // ================================================================================================

    /// <summary>(bridge W4) AS-IS <c>SetUp</c> (SelectPermanentEffect.cs:12-46) — the 11-param overload the
    /// card corpus calls. Resets exactly the fields AS-IS resets (<c>_isLocal</c>/<c>_isdigiXros</c>/custom
    /// messages/<c>_degenerationCount</c>); AS-IS quirk KEPT: <c>_canAttackPlayer</c>/<c>_defenderCondition</c>/
    /// <c>_isFaceUp</c>(=<see cref="_faceUp"/>) are NOT reset and persist across uses of the shared
    /// component instance.</summary>
    public void SetUp(
        HeadlessPlayerId selectPlayer,
        Func<HeadlessEntityId, bool> canTargetCondition,
        Func<List<Permanent>, Permanent, bool>? canTargetCondition_ByPreSelecetedList,
        Func<List<Permanent>, bool>? canEndSelectCondition,
        int maxCount,
        bool canNoSelect,
        bool canEndNotMax,
        Func<Permanent, Task>? selectPermanentCoroutine,
        Func<List<Permanent>, Task>? afterSelectPermanentCoroutine,
        Mode mode,
        ICardEffect cardEffect)
    {
        _selectPlayer = selectPlayer;
        _canTargetCondition = canTargetCondition ?? (static _ => false);
        _canTargetCondition_ByPreSelecetedList = canTargetCondition_ByPreSelecetedList;
        _canEndSelectCondition_Permanents = canEndSelectCondition;
        _maxCount = maxCount;
        _canNoSelect = canNoSelect;
        _canEndNotMax = canEndNotMax;
        _selectPermanentCoroutine = selectPermanentCoroutine;
        _afterSelectPermanentCoroutine = afterSelectPermanentCoroutine;
        _mode = mode;
        _cardEffect = cardEffect;
        _sourceEntityId = cardEffect?.EffectSourceCard?.InstanceId is { IsEmpty: false } sourceId
            ? sourceId
            : new HeadlessEntityId("select");

        _isLocal = false;
        _isdigiXros = false;

        _customMessage = null;
        _customMessage_Enemy = null;

        _customBackButtonMessage = null;

        _degenerationCount = 1;
    }

    /// <summary>AS-IS <c>SetIsLocal</c> (:48-51) — Photon RPC bypass marker; the headless engine is always
    /// single-process, so this is state-only.</summary>
    public void SetIsLocal() => _isLocal = true;

    /// <summary>AS-IS <c>SetDigiXros</c> (:53-56) — UI banner flag only.</summary>
    public void SetDigiXros() => _isdigiXros = true;

    /// <summary>AS-IS <c>SetUpCustomMessage</c> (:58-71) — the custom prompt text (and the AS-IS 2-element
    /// array override). The player-facing text becomes the ChoiceRequest message.</summary>
    public void SetUpCustomMessage(string CustomMessage = "", string CustomMessage_Enemy = "", string[]? customMessageArray = null)
    {
        _customMessage = CustomMessage;
        _customMessage_Enemy = CustomMessage_Enemy;

        if (customMessageArray != null)
        {
            if (customMessageArray.Length == 2)
            {
                _customMessage = customMessageArray[0];
                _customMessage_Enemy = customMessageArray[1];
            }
        }
    }

    /// <summary>AS-IS <c>SetUpCustomBackButtonMessage</c> (:73-76) — "No Selection" button label (UI-only;
    /// the skip option itself is the ChoiceRequest's canSkip).</summary>
    public void SetUpCustomBackButtonMessage(string CustomBackButtonMessage) =>
        _customBackButtonMessage = CustomBackButtonMessage;

    /// <summary>AS-IS <c>SetCanNotAttackPlayer</c> (:83-86) — Mode.Attack may not target the player.</summary>
    public void SetCanNotAttackPlayer() => _canAttackPlayer = false;

    /// <summary>AS-IS <c>SetDefenderCondition</c> (:87-90) — Mode.Attack defender narrowing, AS-IS
    /// <c>Func&lt;Permanent,bool&gt;</c> shape (wrapped onto the id-shape substrate option at Activate time).</summary>
    public void SetDefenderCondition(Func<Permanent, bool> defenderCondition) =>
        _defenderCondition_Permanent = defenderCondition;

    /// <summary>AS-IS <c>SetPlaceFaceUp</c> (:92-95) — PutSecurity* places the cards face up.</summary>
    public void SetPlaceFaceUp() => _faceUp = true;

    /// <summary>AS-IS <c>CanTarget</c> (:150-178): the joint per-permanent gate — the CanSelectBySkill
    /// untargetability scan applies only to permanents owned by NEITHER the effect-source owner NOR the
    /// select player (AS-IS :158), then the card predicate, then the face-down exclusion.</summary>
    private bool CanTargetAsIs(EngineContext context, Permanent permanent)
    {
        if (_cardEffect != null)
        {
            if (permanent.TopCard != null)
            {
                if (_cardEffect.EffectSourceCard != null)
                {
                    if (permanent.TopCard.Owner != _cardEffect.EffectSourceCard.Owner && permanent.TopCard.Owner != _selectPlayer)
                    {
                        // (R1-d) AS-IS permanent.CanSelectBySkill(_cardEffect) — now the mirror Permanent getter
                        // (AS-IS-literal ICanNotSelectBySkillEffect EffectList scan), was the unioned registry scan.
                        if (!permanent.CanSelectBySkill(_cardEffect))
                        {
                            return false;
                        }
                    }
                }
            }
        }

        if (_canTargetCondition != null)
        {
            if (_canTargetCondition(permanent.InstanceId))
            {
                return !permanent.TopCard!.IsFlipped;
            }
        }

        return false;
    }

    /// <summary>AS-IS <c>active()</c> (:181-217): any candidate at all; and for a forced-count selection
    /// (neither canNoSelect nor canEndNotMax) enough candidates AND at least one max-count combination
    /// satisfying <see cref="CanEndSelectAsIs"/> (AS-IS ParameterComparer.Enumerate; combination enumeration
    /// implemented locally — no mirror ParameterComparer — and short-circuited when no combination gate is
    /// configured, where every max-count combination trivially passes).</summary>
    public bool active()
    {
        EngineContext context = RequireContext();
        List<Permanent> canSelectedPermanents = FieldCandidates(context);

        if (canSelectedPermanents.Count == 0)
        {
            return false;
        }

        if (!_canNoSelect && !_canEndNotMax)
        {
            if (canSelectedPermanents.Count < _maxCount)
            {
                return false;
            }

            if (_canEndSelectCondition_Permanents != null &&
                !Combinations(canSelectedPermanents, _maxCount).Any(permanents => CanEndSelectAsIs(permanents)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>AS-IS <c>CanEndSelect</c> (:221-239) — the selection-set termination gate.</summary>
    private bool CanEndSelectAsIs(List<Permanent> permanents)
    {
        // 選択枚数が必要枚数に達していない場合 (AS-IS :224)
        if (!(permanents.Count == _maxCount || (permanents.Count <= _maxCount && _canEndNotMax)))
        {
            return false;
        }

        // 特定の条件により失敗 (AS-IS :230)
        if (_canEndSelectCondition_Permanents != null)
        {
            if (!_canEndSelectCondition_Permanents(permanents))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>(bridge W4) AS-IS <c>Activate()</c> (SelectPermanentEffect.cs:242-1039) — the full selection
    /// flow: candidate scan → forced-selection shortcut / player choice (batch or per-pick incremental) →
    /// per-selected custom coroutine → the Mode action batch → the always-run after-coroutine.
    /// UI/Photon strips (AS-IS anchors): IsSelecting save/restore (:244/:293/:1038 — the choice-pause
    /// substrate replaces the polling flag, MIG6/RD-W4-4), trash-card display (:263-285), Off*Target
    /// (:287-291), message/side-bar/outline/back-button plumbing (:297-360, :404-575), the RPC/WaitUntil
    /// selection transport (:579-723 — the ChoiceProvider request IS the transport), the reset region
    /// (:725-746), target arrows + WaitForSeconds (:767-870, :928-937), PlayLog (:755-761, :940-947).</summary>
    public async Task Activate()
    {
        _targetPermanents = new List<Permanent>();

        _noSelect = false;

        if (active())
        {
            EngineContext context = RequireContext();
            List<Permanent> pool = FieldCandidates(context);

            bool forcesSelection = false;

            // AS-IS forced selection (:366-399): with neither opt-out nor early-end, an exactly-max candidate
            // pool is auto-selected without a player choice (AS-IS EndSelect_RPC fires directly).
            if (!_canNoSelect && !_canEndNotMax && pool.Count == _maxCount)
            {
                _targetPermanents = new List<Permanent>(pool);
                forcesSelection = true;
            }

            if (!forcesSelection)
            {
                (List<Permanent> selected, bool noSelect) = await RunAsIsSelectionAsync(context, pool).ConfigureAwait(false);
                _targetPermanents = selected;
                _noSelect = noSelect;
            }

            if (CanEndSelectAsIs(_targetPermanents))
            {
                // AS-IS builds hashtable {"CardEffect": _cardEffect} (:750-751); the sink mutations carry the
                // same cause as SourceEntityId (set from the effect source in SetUp).
                if (!_noSelect)
                {
                    // AS-IS per-selected processing (:873-925): the Custom coroutine runs per target IN
                    // SELECTION ORDER before the mode batch (a single Activate has a single Mode, so the
                    // AS-IS per-mode buckets each collapse to "all selected targets").
                    if (_selectPermanentCoroutine != null)
                    {
                        foreach (Permanent targetPermanent in _targetPermanents)
                        {
                            await _selectPermanentCoroutine(targetPermanent).ConfigureAwait(false);
                        }
                    }

                    await ApplyAsIsModeBatchAsync(context).ConfigureAwait(false);
                }
            }
        }

        if (_afterSelectPermanentCoroutine != null)
        {
            await _afterSelectPermanentCoroutine(_targetPermanents).ConfigureAwait(false);
        }
    }

    /// <summary>The AS-IS Activate mode batches (:949-1028), in AS-IS statement order, on the verified
    /// substrate carriers: Destroy/Bounce/PutLibrary*/PutSecurity* are the centralised sink mutation kinds
    /// (the AS-IS DestroyPermanentsClass/HandBounceClaass/Deck*BounceClass/IPutSecurityPermanent carriers
    /// have no mirror classes — the sink routes ARE their established mirrors, with the immunity/restriction
    /// gates centralised); Tap/UnTap/Degenerate use the 1:1 mirror classes (SuspendPermanentsClass/
    /// IUnsuspendPermanents/IDegeneration); Attack uses the established queued effect-attack flow.</summary>
    private async Task ApplyAsIsModeBatchAsync(EngineContext context)
    {
        if (_targetPermanents.Count == 0)
        {
            return;
        }

        HeadlessEntityId? causeId = _cardEffect?.EffectSourceCard?.InstanceId;

        switch (_mode)
        {
            case Mode.Destroy:   // AS-IS :949-952 — ONE DestroyPermanentsClass call = ONE delete batch.
            case Mode.Bounce:    // AS-IS :996-999 — ONE HandBounceClaass call (one flush = one add-hand batch).
            case Mode.PutLibraryBottom:   // AS-IS :964-967 DeckBottomBounceClass.
            case Mode.PutLibraryTop:      // AS-IS :969-972 DeckTopBounceClass.
            {
                var sink = NewSink(context);
                Apply(sink, _targetPermanents.Select(permanent => permanent.InstanceId));
                await sink.FlushAsync().ConfigureAwait(false);
                break;
            }

            case Mode.Tap:   // AS-IS :954-957 SuspendPermanentsClass(tapPermanents, hashtable).Tap().
                await new SuspendPermanentsClass(new List<Permanent>(_targetPermanents), _cardEffect, isBlock: false)
                    .Tap().ConfigureAwait(false);
                break;

            case Mode.UnTap: // AS-IS :959-962 IUnsuspendPermanents(untapPermanents, _cardEffect).Unsuspend().
                await new IUnsuspendPermanents(new List<Permanent>(_targetPermanents), _cardEffect)
                    .Unsuspend().ConfigureAwait(false);
                break;

            case Mode.PutSecurityBottom:  // AS-IS :974-983 — batch gated on the EFFECT-SOURCE owner's CanAddSecurity.
            case Mode.PutSecurityTop:     // AS-IS :985-994.
            {
                if (new Player(context, _cardEffect!.EffectSourceCard.Owner).CanAddSecurity(causeId))
                {
                    // Per-permanent IPutSecurityPermanent (AS-IS sequential) — the sink allocates per-card
                    // add-security batch ids in staging order, preserving the AS-IS per-card resolution order.
                    var sink = NewSink(context);
                    Apply(sink, _targetPermanents.Select(permanent => permanent.InstanceId));
                    await sink.FlushAsync().ConfigureAwait(false);
                }

                break;
            }

            case Mode.Degenerate: // AS-IS :1001-1007 per-permanent IDegeneration(selected, count, _cardEffect).
                foreach (Permanent selectedPermanent in _targetPermanents)
                {
                    await new IDegeneration(selectedPermanent, _degenerationCount, causeId)
                        .Degeneration().ConfigureAwait(false);
                }

                break;

            case Mode.Attack:     // AS-IS :1009-1028 — per-permanent CanAttack + SelectAttackEffect, SEQUENTIAL.
            {
                // The established queued effect-attack mirror (see TryOpenAttack): opens the first attacker's
                // target choice, queues the rest, each re-checked alive/legal on its turn — the AS-IS
                // per-selected `if (selectedPermanent.CanAttack(_cardEffect))` re-check. The AS-IS
                // SetCanNotSelectNotAttack (mandatory attack when !_canNoSelect) has no thread into the
                // deferred choice — design item RD-W4-6 (latent; zero AS-IS callers in the bridged corpus).
                if (_defenderCondition_Permanent is { } defenderPermanentCondition)
                {
                    _defenderCondition = id => defenderPermanentCondition(new Permanent(context, id, OwnerOf(context, id)));
                }

                TryOpenAttack(context, _targetPermanents.Select(permanent => permanent.InstanceId));
                break;
            }

            case Mode.Custom:     // The per-selected coroutine (already run) is the whole action.
                break;
        }
    }

    /// <summary>The AS-IS candidate scan (:183-194 / :406-418): every field permanent (battle + breeding, both
    /// players — AS-IS <c>gameContext.Players → GetFieldPermanents()</c>) passing <see cref="CanTargetAsIs"/>.</summary>
    private List<Permanent> FieldCandidates(EngineContext context)
    {
        var candidates = new List<Permanent>();
        foreach (Player player in new GameContext(context).Players)
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                if (CanTargetAsIs(context, permanent))
                {
                    candidates.Add(permanent);
                }
            }
        }

        return candidates;
    }

    /// <summary>The player selection (AS-IS panel semantics, SelectPermanentEffect.cs:402-576 / the AI branch
    /// :629-684 — both collapse to the same ChoiceProvider request): batch request when no path-dependent
    /// per-pick filter; incremental one-pick-at-a-time loop when <c>canTargetCondition_ByPreSelecetedList</c>
    /// is present (the AS-IS panel re-filters clickable permanents per pick, :439-450/:519-533). The
    /// termination rule is <see cref="CanEndSelectAsIs"/> verbatim (count==max, OR any count ≤ max with
    /// canEndNotMax — including zero, AS-IS :224), so the batch request's minimum is 0 when either opt-out
    /// exists; "No Selection" (canNoSelect) maps to the request skip and sets <see cref="_noSelect"/>.
    /// (When canNoSelect AND canEndNotMax both hold, an empty result is reported as the skip — the AS-IS
    /// back-button-vs-EndSelect distinction is unobservable downstream: both leave zero targets. RD-W4-5.)</summary>
    private async Task<(List<Permanent> Selected, bool NoSelect)> RunAsIsSelectionAsync(EngineContext context, List<Permanent> pool)
    {
        string message = BuildAsIsMessage();

        if (_canTargetCondition_ByPreSelecetedList == null)
        {
            int maxCount = Math.Min(_maxCount, pool.Count);
            int minCount = (_canNoSelect || _canEndNotMax) ? 0 : maxCount;
            var candidates = pool
                .Select(permanent => EffectChoiceHelpers.Candidate(
                    permanent.InstanceId, permanent.InstanceId.Value, ChoiceZone.BattleArea,
                    isSelectable: true, permanent.OwnerId))
                .ToList();

            ChoiceRequest request = EffectChoiceHelpers.CreatePermanentRequest(
                _selectPlayer, message, minCount, maxCount, _canNoSelect, candidates);
            if (_canEndSelectCondition_Permanents != null)
            {
                // The AS-IS combination gate rides the request as the SelectionValidator (the established
                // try-reject-retry route, same as the legacy BuildRequest path).
                request = request with
                {
                    SelectionValidator = ids => CanEndSelectAsIs(
                        ids.Select(id => pool.First(permanent => permanent.InstanceId == id)).ToList()),
                };
            }

            ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request).ConfigureAwait(false);
            if (result.IsSkipped)
            {
                return (new List<Permanent>(), true);
            }

            var selected = new List<Permanent>();
            foreach (HeadlessEntityId id in result.SelectedIds)
            {
                Permanent? pick = pool.FirstOrDefault(permanent => permanent.InstanceId == id);
                if (pick is not null && !selected.Contains(pick))
                {
                    selected.Add(pick);
                }
            }

            return (selected, false);
        }

        // Incremental path — AS-IS OnClickFieldPermanentCard's per-pick byPreSelectedList filter. The one
        // panel corner a one-pick loop cannot reproduce (UN-picking at maxCount to satisfy the combination
        // gate) is unreachable for prefix-monotone AS-IS conditions — design item RD-W4-2 (== RD-W3-1).
        var picked = new List<Permanent>();
        while (picked.Count < _maxCount)
        {
            List<Permanent> legal = pool
                .Where(permanent => !picked.Contains(permanent)
                    && _canTargetCondition_ByPreSelecetedList(picked, permanent))
                .ToList();
            if (legal.Count == 0)
            {
                break;
            }

            bool canEndNow = (_canNoSelect && picked.Count == 0) || CanEndSelectAsIs(picked);
            var request = new ChoiceRequest(
                ChoiceType.Permanent, _selectPlayer, message,
                minCount: canEndNow ? 0 : 1, maxCount: 1, canSkip: canEndNow, ChoiceZone.BattleArea,
                legal.Select(permanent => EffectChoiceHelpers.Candidate(
                    permanent.InstanceId, permanent.InstanceId.Value, ChoiceZone.BattleArea,
                    isSelectable: true, permanent.OwnerId)).ToList());
            ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request).ConfigureAwait(false);
            if (result.IsSkipped || result.SelectedIds.Count == 0)
            {
                if (picked.Count == 0 && _canNoSelect)
                {
                    return (picked, true);
                }

                break;
            }

            Permanent? pick = legal.FirstOrDefault(permanent => permanent.InstanceId == result.SelectedIds[0]);
            if (pick is null)
            {
                break;
            }

            picked.Add(pick);
        }

        return (picked, false);
    }

    /// <summary>The AS-IS player-facing prompt (:297-358): the custom message when set, else the per-Mode
    /// default text, verbatim.</summary>
    private string BuildAsIsMessage()
    {
        if (!string.IsNullOrEmpty(_customMessage))
        {
            return _customMessage!;
        }

        return _mode switch
        {
            Mode.Tap => "Select cards to suspend.",
            Mode.UnTap => "Select cards to unsuspend.",
            Mode.Destroy => "Select cards to delete.",
            Mode.Bounce => "Select cards to return to hand.",
            Mode.PutLibraryBottom => "Select cards to put on bottom of the deck.",
            Mode.PutLibraryTop => "Select cards to put on top of the deck.",
            Mode.PutSecurityBottom => "Select cards to put on bottom of security.",
            Mode.PutSecurityTop => "Select cards to put on top of security.",
            Mode.Degenerate => $"Select cards to De-Digivolve {_degenerationCount}.",
            Mode.Attack => "Select a Digimon to attack with.",
            Mode.Custom => "Select cards.",
            _ => "Select cards.",
        };
    }

    private EngineContext RequireContext() =>
        _context
        ?? _cardEffect?.EffectSourceCard?.Context
        ?? throw new InvalidOperationException(
            "SelectPermanentEffect has no EngineContext — obtain the instance via " +
            "GManager.instance.GetComponent<SelectPermanentEffect>() (bridge W4) or pass a context to SetUp.");

    private static HeadlessPlayerId OwnerOf(EngineContext context, HeadlessEntityId id) =>
        context.CardInstanceRepository.TryGetInstance(id, out var record) && record is not null
            ? record.OwnerId
            : default;

    private static MatchStateMutationSink NewSink(EngineContext context) =>
        new(context.CardInstanceRepository, log: null, context.ZoneMover, memory: context.MemoryController,
            context.EffectRegistry, context.GameEventQueue, context: context);

    /// <summary>AS-IS <c>ParameterComparer.Enumerate(list, k)</c> — all k-element combinations (no mirror
    /// ParameterComparer exists; local index-based enumeration, lazily yielded).</summary>
    private static IEnumerable<List<Permanent>> Combinations(List<Permanent> pool, int size)
    {
        if (size <= 0)
        {
            yield return new List<Permanent>();
            yield break;
        }

        if (size > pool.Count)
        {
            yield break;
        }

        int[] indices = Enumerable.Range(0, size).ToArray();
        while (true)
        {
            yield return indices.Select(index => pool[index]).ToList();

            int position = size - 1;
            while (position >= 0 && indices[position] == pool.Count - size + position)
            {
                position--;
            }

            if (position < 0)
            {
                yield break;
            }

            indices[position]++;
            for (int next = position + 1; next < size; next++)
            {
                indices[next] = indices[next - 1] + 1;
            }
        }
    }
}
