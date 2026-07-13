// Source: DCGO/Assets/Scripts/Script/GManager.cs
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using HeadlessDCGO.Engine.Headless.Bridge;

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
    }

    /// <summary>AS-IS <c>GManager.instance</c> — the current match's manager, or null outside any match scope
    /// (mirrors the AS-IS null-check `if (GManager.instance != null)`).</summary>
    public static GManager? instance =>
        AmbientMatchContext.Current is { } context ? new GManager(context) : null;

    /// <summary>AS-IS <c>GManager.instance.turnStateMachine</c> (→ <c>.gameContext</c> / <c>.DoneStartGame</c>).</summary>
    public TurnStateMachine turnStateMachine { get; }

    /// <summary>(EFFECT-MODEL REBUILD / P2) AS-IS <c>GManager.autoProcessing</c> (GManager.cs:84, a field) — the
    /// rule/trigger-stack processor. Resolves the match-scoped mirror <see cref="AutoProcessing"/> service
    /// (context-cached via <c>AutoProcessing.For</c>). NOTE (design item P2-STACKSKILLINFOS): the AS-IS
    /// <c>autoProcessing.StackSkillInfos(Hashtable, EffectTiming, …)</c> coroutine (AutoProcessing.cs:984) is
    /// NOT yet on the mirror AutoProcessing (the mirror replaced it with the async trigger collector /
    /// WindowResolver) — the foundation `ActivateICardEffectExtensionClass` tail references it verbatim; wiring
    /// it is a P5 (trigger/window) item. <c>RuleProcess()</c> IS present.</summary>
    public AutoProcessing autoProcessing =>
        HeadlessDCGO.Engine.Assets.Scripts.Script.AutoProcessing.For(_context);

    /// <summary>(EFFECT-MODEL REBUILD / P2) AS-IS <c>GManager.attackProcess</c> (GManager.cs:99, a field) — the
    /// attack pipeline. Resolves the match-scoped mirror <see cref="AttackProcess"/> service (context-cached via
    /// <c>AttackProcess.For</c>; its optional blockTiming/battleResolver/securityResolver default to the
    /// context's registered instances). Card gates read <c>attackProcess.DefendingPermanent</c> through this.</summary>
    public AttackProcess attackProcess =>
        HeadlessDCGO.Engine.Assets.Scripts.Script.AttackProcess.For(_context);

    /// <summary>The live <see cref="EngineContext"/> backing this manager (substrate escape hatch for members
    /// still being ported — not an AS-IS surface).</summary>
    public EngineContext Context => _context;

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
    /// this path today — BT1_011/017/023/092/094). Any other T throws (STOP, design item RD-W4-3 in
    /// docs/audit/rebuild_bridge_w4_notes.md): the AS-IS <c>OptionalSkill</c> mirror file declares no type at
    /// all (its SelectOptional yes/no flow is WindowResolver territory), and the other AS-IS components
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
