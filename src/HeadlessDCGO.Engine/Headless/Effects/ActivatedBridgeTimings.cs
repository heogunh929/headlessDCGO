namespace HeadlessDCGO.Engine.Headless.Effects;

using EffectTiming = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.EffectTiming;

/// <summary>
/// (PRIM triggered-activated bridge) The single source of truth for WHICH trigger timings resolve a card's
/// ACTIVATED effects (draw/trash/delete/select via <c>ActivatedEffectResolver</c>) rather than its bound
/// mutation <c>IHeadlessCardEffect</c> (memory/DP). Shared by the batch pipeline's
/// <c>GameFlowProcessor.BridgeActivatedTriggersAsync</c> and the Stage-5 window loop's
/// <see cref="WindowResolverWiring"/> so both classify a timing identically (Stage-5 unifies them into one
/// seed at cut-over — see design RD 3b-ii/iii).
///
/// The four sets differ only in HOW the reacting card is found and how it is capped:
///  - <see cref="SubjectScoped"/>: the event's subject (the attacker / deleted card) resolves — fires once per
///    event, so no per-turn cap (except the multi-fire timings in <see cref="OncePerTurn"/>).
///  - <see cref="Boundary"/>: a turn boundary carries no subject; EVERY battle-area card resolves and gates
///    itself — one such event per turn, so the per-turn cap is natural.
///  - <see cref="EventBroadcast"/>: every battle-area card resolves PER driving event, with the event threaded
///    through so each card's own gate decides scope (self / ally / opponent host); a "[Once Per Turn]" reactor
///    caps itself via <c>MaxCountPerTurn</c> inside the resolver.
/// </summary>
public static class ActivatedBridgeTimings
{
    /// <summary>SUBJECT-scoped timings — the ACTIVATED effects of the card the event is about (the attacker /
    /// the deleted card) resolve. These fire once per event (per attack / per deletion), so no per-turn cap is
    /// needed. ALLOW-LIST excludes action-wired timings (OnEnterFieldAnyone → PlayCardAction, WhenDigivolving →
    /// DigivolveAction, OptionSkill/SecuritySkill) so nothing double-fires.</summary>
    public static readonly IReadOnlySet<EffectTiming> SubjectScoped = new HashSet<EffectTiming>
    {
        EffectTiming.OnAllyAttack,
        EffectTiming.OnDestroyedAnyone,
        EffectTiming.OnUnTappedAnyone,
        // OnTappedAnyone lives in EventBroadcast: "[Your Turn] when an OPPONENT'S Digimon is suspended, …" lives
        // on a DIFFERENT card (e.g. a Tamer, ST4_14) than the suspended subject, so it needs the cross-card
        // broadcast, not the subject-scoped path.
        // (v4) attack-declaration window — emitted with subject = attacker alongside OnAttack/OnAllyAttack
        // (AttackPermanentAction:151), so "[When Attacking]" activated effects (incl. Digi-Burst bodies declared
        // at OnDeclaration) resolve at declaration. Once per declaration event; a card gates itself.
        EffectTiming.OnDeclaration,
    };

    /// <summary>(v3) Subject-scoped timings that can fire MULTIPLE times per turn (a card can be re-suspended and
    /// unsuspended). Their activated triggers are capped ONCE PER TURN via <c>OnceFlagController</c> — the AS-IS
    /// default for on-unsuspend triggers is "[Once Per Turn]", and under-firing a re-unsuspend is safer than
    /// over-firing. memory/DP on-unsuspend triggers keep their IHeadlessCardEffect forms (scheduler-capped) and
    /// are not bridged here.</summary>
    public static readonly IReadOnlySet<EffectTiming> OncePerTurn = new HashSet<EffectTiming>
    {
        EffectTiming.OnUnTappedAnyone,
    };

    /// <summary>(v2) BOUNDARY timings — carry no card subject (a turn boundary emitted with only an actor). The
    /// ACTIVATED effects of EVERY battle-area card resolve (each card gates "[End of YOUR turn]" itself). These
    /// fire exactly ONCE per turn (one such event per turn), so the per-turn cap is natural — no OnceFlags
    /// needed.</summary>
    public static readonly IReadOnlySet<EffectTiming> Boundary = new HashSet<EffectTiming>
    {
        EffectTiming.OnEndTurn,
        EffectTiming.OnStartTurn,
        EffectTiming.OnStartMainPhase,
        // (Other emitted windows use TriggerTimings names with no matching EffectTiming enum member yet —
        // OnEndAttackPhase/OnEndMainPhase/OnDraw — so they need enum + name reconciliation first.)
        // NOTE: OnEndBattle lives in EventBroadcast — it carries battle-result metadata (winnerIds/loserIds)
        // that "[End of Battle] when this deletes an opponent in battle" gates need, which the boundary branch
        // discards.
    };

    /// <summary>(Phase D broadcast) EVENT-BROADCAST timings — the AS-IS fires these via a global
    /// StackSkillInfos(hashtable, timing) that offers the event to EVERY field card; each card's OWN gate
    /// (e.g. <c>CanTriggerOnTrashDigivolutionCard</c>) decides scope. The bridge scans every battle-area card
    /// and passes the DRIVING EVENT through so gates read the event subject and metadata. Fires once per event
    /// (a second trash that turn fires again, as AS-IS), so no per-turn cap here; a card caps itself via
    /// MaxCountPerTurn.
    /// KNOWN BOUNDARY: AS-IS GetSkillInfos also offers the timing to trash / hand / face-up security cards; this
    /// scan covers the battle area only (same model as the boundary scan) — every current listener at these
    /// timings lives on the field. Extend the scan zones when a trash/hand listener is actually ported.</summary>
    public static readonly IReadOnlySet<EffectTiming> EventBroadcast = new HashSet<EffectTiming>
    {
        EffectTiming.OnDigivolutionCardDiscarded,
        // (c-remediation) digivolution cards returned to deck bottom — cross-card reactors thread the driving event.
        EffectTiming.OnDigivolutionCardReturnToDeckBottom,
        // (Phase D) [End of Battle] — emitted actor-only with battle-result metadata (winnerIds/loserIds/…).
        // Broadcast to every field card, threading the driving event so gates like
        // CanTriggerWhenDeleteOpponentDigimonByBattle read "event.winnerIds"/"event.loserIds"; each card
        // self-gates. One resolve per battle event (a turn with N battles opens N windows, as AS-IS).
        EffectTiming.OnEndBattle,
        // (Phase D) [Your Turn] "when an opponent's Digimon is suspended, …" — the reacting effect lives on a
        // different card (Tamer, ST4_14) than the suspended subject, so it broadcasts. The event carries
        // subject = the suspended card; each listener's CanTriggerWhenPermanentSuspends self-gates on it.
        EffectTiming.OnTappedAnyone,
        // (G2) [Your Turn]/[All Turns] "when an Option is used, …" — the reacting effect lives on a DIFFERENT
        // card than the option card being used. Broadcast to every battle-area card, threading the driving event
        // so each listener's CanTriggerWhenUseOption / CanTriggerWhenOwnerUseOption self-gates on the used option
        // (subject) + its cost. Fires once per option use; a "[Once Per Turn]" reactor caps itself via
        // MaxCountPerTurn.
        EffectTiming.OnUseOption,
    };
}
