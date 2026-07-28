// Source: DCGO/Assets/Scripts/Script/GManager.cs
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (EFFECT-MODEL REBUILD) Headless mirror of the original Unity <c>GManager</c> singleton — the global handle
/// the AS-IS effect object model reads game state through (<c>GManager.instance.turnStateMachine.gameContext</c>,
/// <c>GManager.instance.attackProcess</c>, <c>GManager.instance.autoProcessing</c>). A 1:1 port of that model
/// needs this global-looking accessor so ported effect code reads verbatim, WITHOUT threading an EngineContext
/// into every effect (which AS-IS does not do).
///
/// <c>instance</c> resolves the current match from <see cref="AmbientMatchContext"/> — an AsyncLocal scope the
/// engine sets per match operation — so it is match-scoped, not a true process-global (N concurrent matches
/// never collide). The AS-IS-named members below are thin views over the mirror state classes / substrate
/// services. UI/Photon/component members of the original GManager are NOT ported (substrate-stripped); only the
/// game-logic handles effect code actually reads are exposed, grown as ported effects need more.
/// (File lives at the AS-IS path <c>Script/GManager.cs</c>; namespace kept <c>...CardEffectCommons</c> so
/// existing references are unaffected — namespace normalisation is a later, separate pass.)
/// </summary>
public sealed class GManager
{
    private readonly EngineContext _context;

    private GManager(EngineContext context)
    {
        _context = context;
        turnStateMachine = TurnStateMachine.For(context);

        // (isAI restore) AS-IS assigns IsAI in `AwakeCoroutine` (GManager.cs:245-261), the manager's own start-up.
        // The mirror GManager has no Awake — `instance` constructs the view — so the assignment is mirrored here,
        // its only construction point. Verified in the original, both assignment sites:
        //   GManager.cs:250-253  `if (!PhotonNetwork.IsConnected) { IsAI = true; }`
        //   GManager.cs:255-261  `if (ContinuousController.instance != null) { if (ContinuousController.instance
        //                         .isAI) { IsAI = true; } }`
        // Only the second is mirrored: there is no `PhotonNetwork` mirror type (Photon is substrate-stripped) and
        // no headless notion of "connected", so the first site has no faithful source to read. It is deliberately
        // NOT approximated as `true` — headless has no Photon session at all, so an `!IsConnected` reading would
        // latch IsAI true and change every restored branch's live outcome.
        if (HeadlessDCGO.Engine.Assets.Scripts.Script.ContinuousController.instance != null)
        {
            if (HeadlessDCGO.Engine.Assets.Scripts.Script.ContinuousController.instance!.isAI)
            {
                IsAI = true;
            }
        }
    }

    /// <summary>AS-IS <c>GManager.instance</c> — the current match's manager, or null outside any match scope
    /// (mirrors the AS-IS null-check `if (GManager.instance != null)`).</summary>
    public static GManager? instance =>
        AmbientMatchContext.Current is { } context ? new GManager(context) : null;

    /// <summary>(isAI restore) AS-IS <c>public bool IsAI { get; private set; } = false;</c> (GManager.cs:213) —
    /// whether this match is against the built-in AI. Assigned in the constructor from the same source AS-IS
    /// <c>AwakeCoroutine</c> reads (<c>ContinuousController.instance.isAI</c>, GManager.cs:255-261); see the
    /// constructor for the Photon assignment site that has no mirror source. Nothing headless sets
    /// <c>ContinuousController.isAI</c>, so this stays false.</summary>
    public bool IsAI { get; private set; } = false;

    /// <summary>AS-IS <c>GManager.instance.turnStateMachine</c> (→ <c>.gameContext</c> / <c>.DoneStartGame</c>).</summary>
    public TurnStateMachine turnStateMachine { get; }

    /// <summary>(isAI restore) AS-IS <c>public Player GetPlayerFromID(int playerID)</c> (GManager.cs:494-506):
    /// returns whichever of the two seats carries that id, else null. SUBSTRATE: AS-IS compares the int
    /// <c>Player.PlayerID</c> of the <c>You</c>/<c>Opponent</c> fields (GManager.cs:12/15); the mirror has neither
    /// (seats are <see cref="HeadlessPlayerId"/> and are read from the turn controller's seat order — the same
    /// source <c>Player.Enemy</c> uses), so the parameter carries the mirror's seat-identity type and the lookup
    /// scans the seat order. Same answer shape: the seated player, or null.</summary>
    public Player? GetPlayerFromID(HeadlessPlayerId playerID)
    {
        foreach (HeadlessPlayerId seat in _context.TurnController.Current.PlayerOrder)
        {
            if (seat == playerID)
            {
                return new Player(_context, playerID);
            }
        }

        return null;
    }

    /// <summary>(EFFECT-MODEL REBUILD / P2) AS-IS <c>GManager.autoProcessing</c> (GManager.cs:84, a field) — the
    /// rule/trigger-stack processor. Resolves the match-scoped mirror <see cref="AutoProcessing"/> service
    /// (context-cached via <c>AutoProcessing.For</c>). NOTE (design item P2-STACKSKILLINFOS): the AS-IS
    /// <c>autoProcessing.StackSkillInfos(Hashtable, EffectTiming, …)</c> coroutine (AutoProcessing.cs:984) is
    /// NOT yet on the mirror AutoProcessing (the mirror replaced it with the async trigger collector /
    /// WindowResolver) — the foundation `ActivateICardEffectExtensionClass` tail references it verbatim; wiring
    /// it is a P5 (trigger/window) item. <c>RuleProcess()</c> IS present.</summary>
    public AutoProcessing autoProcessing =>
        HeadlessDCGO.Engine.Assets.Scripts.Script.AutoProcessing.For(_context);

    /// <summary>(P6C1) AS-IS <c>GManager.autoProcessing_CutIn</c> (GManager.cs:112, a field) — the SECOND
    /// AutoProcessing component instance the play pipeline stacks/drains the BeforePayCost/AfterPayCost cut-in
    /// effects on (its <c>StackedSkillInfos</c> must not collide with the main trigger stack). Resolves the
    /// match-scoped mirror via <see cref="AutoProcessing.ForCutIn"/>.</summary>
    public AutoProcessing autoProcessing_CutIn =>
        HeadlessDCGO.Engine.Assets.Scripts.Script.AutoProcessing.ForCutIn(_context);

    /// <summary>(EFFECT-MODEL REBUILD / P2) AS-IS <c>GManager.attackProcess</c> (GManager.cs:99, a field) — the
    /// attack pipeline. Resolves the match-scoped mirror <see cref="AttackProcess"/> service (context-cached via
    /// <c>AttackProcess.For</c>; its optional blockTiming/battleResolver/securityResolver default to the
    /// context's registered instances). Card gates read <c>attackProcess.DefendingPermanent</c> through this.</summary>
    public AttackProcess attackProcess =>
        HeadlessDCGO.Engine.Assets.Scripts.Script.AttackProcess.For(_context);

    /// <summary>The live <see cref="EngineContext"/> backing this manager (substrate escape hatch for members
    /// still being ported — not an AS-IS surface).</summary>
    public EngineContext Context => _context;

    /// <summary>(EFFECT-MODEL REBUILD / bridge W5) AS-IS <c>GManager.userSelectionManager</c> (GManager.cs:114,
    /// a public field — THE one <c>UserSelectionManager</c> component instance on the GManager GameObject).
    /// Card effects reach the mandatory labeled mode/branch pick through it
    /// (<c>SetBoolSelection/SetIntSelection/SetBool/SetInt</c> → <c>WaitForEndSelect</c> →
    /// <c>SelectedBoolValue/SelectedIntValue</c>, e.g. BT1_111). Mirrored as ONE match-scoped instance,
    /// context-cached exactly like <see cref="GetComponent{T}"/> (its cross-call field state —
    /// <c>_selectedIntValue</c> persisting after the post-wait reset — is per-instance in AS-IS too).</summary>
    public Assets.Scripts.Script.UserSelectionManager userSelectionManager
    {
        get
        {
            if (_context.TryGetService(out Assets.Scripts.Script.UserSelectionManager? existing) && existing is not null)
            {
                return existing;
            }

            var created = new Assets.Scripts.Script.UserSelectionManager();
            created.AttachContext(_context);
            _context.RegisterService(created);
            return created;
        }
    }

    /// <summary>(SpecialPlay re-migration) AS-IS <c>GManager.selectJogressEffect</c> (GManager.cs:102, a public
    /// field — THE one <c>SelectJogressEffect</c> component instance on the GManager GameObject). AS-IS callers
    /// (TurnStateMachine.cs:2203/2346, CardEffectCommons/DNADigivolveEffects.cs:559-570) reach the Jogress /
    /// DNA-Digivolution pre-play selection through it verbatim. Backed by the same one-instance,
    /// context-cached policy as <see cref="GetComponent{T}"/>.</summary>
    public Assets.Scripts.Script.SelectJogressEffect selectJogressEffect =>
        GetComponent<Assets.Scripts.Script.SelectJogressEffect>();

    /// <summary>(EFFECT-MODEL REBUILD / bridge W4) AS-IS <c>GManager.instance.GetComponent&lt;T&gt;()</c> — the
    /// Unity idiom card effects use to reach the singleton selection-flow components living on the GManager
    /// GameObject (<c>SelectPermanentEffect</c>/<c>SelectCardEffect</c>/…). Unity returns THE one component
    /// instance per GameObject; each <c>SetUp(...)</c> overwrites its state, and the fields <c>SetUp</c> does
    /// NOT reset (e.g. SelectPermanentEffect's <c>_canAttackPlayer</c>/<c>_defenderCondition</c>/<c>_isFaceUp</c>)
    /// genuinely persist across uses in AS-IS — so the mirror is likewise ONE match-scoped instance,
    /// context-cached exactly like <see cref="AutoProcessing.For"/> (<c>TryGetService</c>/<c>RegisterService</c>).
    ///
    /// Supported T (bridge W4): <see cref="Assets.Scripts.Script.SelectPermanentEffect"/> and
    /// <see cref="Assets.Scripts.Script.SelectCardEffect"/> (the two the AS-IS-verbatim card corpus reaches via
    /// this path today — BT1_011/017/023/092/094), plus (P6 stage A)
    /// <see cref="Assets.Scripts.Script.OptionalSkill"/> (the Activate_Optional yes/no prompt,
    /// ICardEffect.cs:1054). Any other T throws (STOP, design item RD-W4-3 in
    /// docs/audit/rebuild_bridge_w4_notes.md): the other AS-IS components
    /// (SelectAssemblyClass, SelectCountEffect, SelectDigiXrosClass, Effects, …) either have non-component
    /// mirror shapes or no mirror — grow this switch as bridge batches land them, never silently.</summary>
    public T GetComponent<T>() where T : class
    {
        if (_context.TryGetService(out T? existing) && existing is not null)
        {
            return existing;
        }

        object created;
        if (typeof(T) == typeof(Assets.Scripts.Script.SelectPermanentEffect))
        {
            var selectPermanentEffect = new Assets.Scripts.Script.SelectPermanentEffect();
            selectPermanentEffect.AttachContext(_context);
            created = selectPermanentEffect;
        }
        else if (typeof(T) == typeof(Assets.Scripts.Script.SelectCardEffect))
        {
            var selectCardEffect = new Assets.Scripts.Script.SelectCardEffect();
            selectCardEffect.AttachContext(_context);
            created = selectCardEffect;
        }
        else if (typeof(T) == typeof(Assets.Scripts.Script.SelectHandEffect))
        {
            // (R5-A) the AS-IS hand-select component (Script/SelectHandEffect.cs) — the HAND-scoped sibling of
            // SelectCardEffect, reached the same GetComponent<>() way by the AS-IS-verbatim card corpus.
            var selectHandEffect = new Assets.Scripts.Script.SelectHandEffect();
            selectHandEffect.AttachContext(_context);
            created = selectHandEffect;
        }
        else if (typeof(T) == typeof(Assets.Scripts.Script.OptionalSkill))
        {
            // (P6 stage A) the AS-IS optional yes/no prompt (ICardEffect.cs:1054 Activate_Optional →
            // OptionalSkill.SelectOptional) — now a real mirror component (Script/OptionalSkill.cs).
            var optionalSkill = new Assets.Scripts.Script.OptionalSkill();
            optionalSkill.AttachContext(_context);
            created = optionalSkill;
        }
        else if (typeof(T) == typeof(Assets.Scripts.Script.SelectCountEffect))
        {
            // (P6C1) the AS-IS digivolution-cost / count picker (PlayCardClass SelectCost,
            // CardController.cs:534-556) — the mirror component carries the AS-IS SetUp/SetCandidates/
            // SetPreferMin/SetIsDigivolutionCost/Activate surface (Script/SelectCountEffect.cs).
            var selectCountEffect = new Assets.Scripts.Script.SelectCountEffect();
            selectCountEffect.AttachContext(_context);
            created = selectCountEffect;
        }
        else if (typeof(T) == typeof(Assets.Scripts.Script.SelectDigiXrosClass))
        {
            // (P6C1 → RD-EXT3-01) the AS-IS DigiXros pre-play selection component — now a full 1:1 mirror
            // (state surface + CanSelectDigiXros + Select area-loop + AddDigivolutiuonCards apply halves,
            // Script/SelectDigiXrosClass.cs). Context-injected like the other Select* components.
            var selectDigiXrosClass = new Assets.Scripts.Script.SelectDigiXrosClass();
            selectDigiXrosClass.AttachContext(_context);
            created = selectDigiXrosClass;
        }
        else if (typeof(T) == typeof(Assets.Scripts.Script.SelectAssemblyClass))
        {
            // (RD-EXT3-02) the AS-IS Assembly pre-play selection component — the instance surface of the
            // now-non-static SelectAssemblyClass (its STATIC feasibility half still serves PlayCardAction).
            var selectAssemblyClass = new Assets.Scripts.Script.SelectAssemblyClass();
            selectAssemblyClass.AttachContext(_context);
            created = selectAssemblyClass;
        }
        else if (typeof(T) == typeof(Assets.Scripts.Script.SelectDNACondition))
        {
            // (P6C1) the AS-IS DNA(jogress)-condition picker — full 1:1 mirror (Script/SelectDNACondition.cs).
            created = new Assets.Scripts.Script.SelectDNACondition();
        }
        else if (typeof(T) == typeof(Assets.Scripts.Script.SelectJogressEffect))
        {
            // (SpecialPlay re-migration) the AS-IS Jogress / DNA-Digivolution pre-play picker
            // (Script/SelectJogressEffect.cs) — reached AS-IS through the `selectJogressEffect` field above.
            var selectJogressEffect = new Assets.Scripts.Script.SelectJogressEffect();
            selectJogressEffect.AttachContext(_context);
            created = selectJogressEffect;
        }
        else if (typeof(T) == typeof(Assets.Scripts.Script.SelectAttackEffect))
        {
            // (R5-B) the AS-IS attack-target picker (Script/SelectAttackEffect.cs, full 1:1 mirror —
            // Overclock/Vortex processes reach it via this path, KeyWordEffects/Overclock.cs:79).
            var selectAttackEffect = new Assets.Scripts.Script.SelectAttackEffect();
            selectAttackEffect.AttachContext(_context);
            created = selectAttackEffect;
        }
        else if (typeof(T) == typeof(Assets.Scripts.Script.SelectAppFusionEffect))
        {
            // (R5-C) the AS-IS App-Fusion link picker (Script/SelectAppFusionEffect.cs, full 1:1 mirror).
            var selectAppFusionEffect = new Assets.Scripts.Script.SelectAppFusionEffect();
            selectAppFusionEffect.AttachContext(_context);
            created = selectAppFusionEffect;
        }
        else if (typeof(T) == typeof(Assets.Scripts.Script.SelectBurstDigivolutionEffect))
        {
            // (R5-C) the AS-IS Burst-digivolution picker (Script/SelectBurstDigivolutionEffect.cs, partial —
            // SelectTamer/BounceTamer/AddTrashTopCardAtTurnEnd are STOP RD-R5-01/02/03).
            var selectBurstDigivolutionEffect = new Assets.Scripts.Script.SelectBurstDigivolutionEffect();
            selectBurstDigivolutionEffect.AttachContext(_context);
            created = selectBurstDigivolutionEffect;
        }
        else
        {
            throw new NotSupportedException(
                $"GManager.GetComponent<{typeof(T).Name}>() has no mirror component for this type yet " +
                "(bridge W4 supports SelectPermanentEffect/SelectCardEffect; design item RD-W4-3, " +
                "docs/audit/rebuild_bridge_w4_notes.md).");
        }

        var component = (T)created;
        _context.RegisterService(component);
        return component;
    }
}
