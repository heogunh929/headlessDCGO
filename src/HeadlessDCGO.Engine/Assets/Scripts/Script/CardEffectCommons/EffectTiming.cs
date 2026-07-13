namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
// Aliased (not a namespace import) to avoid pulling the sibling `...Script.CardEffectFactory` namespace
// into scope, which would clash with the CardEffectFactory type below.
using SelectPermanentEffect = HeadlessDCGO.Engine.Assets.Scripts.Script.SelectPermanentEffect;
using PartitionCondition = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory.KeyWordEffects.PartitionCondition;


// (Phase 1) Card-porting recipe foundation.
//
// The original DCGO authors each card as `public class <Id> : CEntity_Effect` overriding
// `CardEffects(EffectTiming timing, CardSource card)` which returns the `ICardEffect`s active for that
// timing (see DCGO/Assets/Scripts/CardEffect/<set>/<color>/<id>.cs). To keep ported card files a 1:1
// mirror of that source (AS-IS structure-mirror rule), this file provides the headless equivalents of
// the Unity authoring surface — `CEntity_Effect`, `CardSource`, `EffectTiming`, `ICardEffect`, a
// `CardEffectFactory` whose method names match the original, and `CardEffectCommons` condition predicates
// — so a ported card body reads identically to the original and compiles against the headless engine.
//
// Each `ICardEffect` lowers to an `EffectBinding` that the existing continuous / keyword gates already
// consume (no new resolution plumbing). The original evaluates conditions against global singletons; the
// headless threads the live `EngineContext` through `CardSource` so a `condition` lambda evaluates against
// real turn / zone / digivolution state at read time. `CardEffectRegistrar` materialises a card's
// bindings into the EffectRegistry when it enters play.

/// <summary>
/// Headless mirror of the original (large) <c>EffectTiming</c> enum. Only the timings used by ported
/// cards are listed; grow this as cards require new ones. <see cref="None"/> is the original's marker for
/// always-on continuous / static effects (registered once while the card is in play).
/// </summary>
public enum EffectTiming
{
    None = 0,
    OnEnterFieldAnyone,
    OnDetermineDoSecurityCheck,
    OnUseAttack,
    WhenDigivolving,
    OnDestroyedAnyone,
    OnAllyAttack,
    OnBlockAnyone,
    OnEndTurn,
    OnStartTurn,

    // Player-activated abilities (NOT auto-registered on enter-play; activation flow is Wave 3).
    OptionSkill,
    SecuritySkill,

    // (EX8_074 Stage 1) "When this card would be played" — the original BeforePayCost timing. Engine-level
    // string trigger `TriggerTimings.BeforePayCost` already fires in PlayCardAction; this enum value lets a
    // ported card return BeforePayCost effects. The interactive pre-payment cost-reduction WINDOW that
    // consumes them is a later stage (PlayCardAction's cost is currently locked at action-generation time).
    BeforePayCost,
    // (PRIM-W4 WhenMovingClass) mirrors the original EffectTiming.OnMove — fires when a Digimon is promoted
    // out of the breeding area (CV-A4). ToTriggerName -> "OnMove" matches the engine's TriggerTimings.OnMove
    // emit. Appended at the end to keep existing enum ordinals stable.
    OnMove,

    // (PRIM-P0-timing) High-volume card-facing timings from ALL_CARD_PRIMITIVE_BACKLOG P0. Each enum name
    // is string-equal to an emitted TriggerTimings value (ToTriggerName -> ToString()); appended at the end
    // to keep existing ordinals stable.
    //   OnStartMainPhase — main-phase entry (emit exists: MetadataActionProcessor OnStartMainPhase). 222 cards.
    //   OnEndBattle      — after battle resolved/deletions applied (emit exists: BattleResolver). 84 cards.
    //   OnDeclaration    — attack declared; new emit added alongside OnAttack/OnAllyAttack. 298 cards.
    OnStartMainPhase,
    OnEndBattle,
    OnDeclaration,

    // (PRIM-P0-timing batch 2) Timings ALREADY emitted by the engine (verified emit sites) that only lacked
    // a card-facing enum member. Each name is string-equal to its emitted TriggerTimings value. Pure enum
    // additions against existing emits (same low-risk shape as OnEndBattle) — collection/resolution reuse the
    // generic path. "...Anyone" board timings are self-scoped here (cross-card broadcast is a per-card
    // follow-up via TriggerTimings.BroadcastTimings, as with the existing OnBlockAnyone).
    //   OnTappedAnyone 139 · OnCounterTiming 111 · WhenLinked 64 · OnAddDigivolutionCards 50 · OnUseOption 30
    //   OnUnTappedAnyone 29 · OnDiscardSecurity 14 · OnLinkCardDiscarded 7 · AfterPayCost 7 · WhenTopCardTrashed 3
    //   OnFaceUpSecurityIncreased 1
    OnTappedAnyone,
    OnUnTappedAnyone,
    OnCounterTiming,
    WhenLinked,
    OnLinkCardDiscarded,
    OnAddDigivolutionCards,
    OnUseOption,
    OnDiscardSecurity,
    AfterPayCost,
    WhenTopCardTrashed,
    OnFaceUpSecurityIncreased,

    // (PRIM-P0-timing batch 3a) Timings already DERIVED from CardMoved zone transitions (or the SecurityCheck
    // event) by TriggerTimingMap.Derive — already available, no emit needed, only a card-facing enum member.
    // Same low-risk shape as batch 2; the derivation is existing engine behavior exercised by the suite.
    //   WhenRemoveField 164 · OnLoseSecurity 73 · OnDiscardHand 34 · OnAddHand 21 · OnDiscardLibrary 20
    //   OnAddSecurity 14 · WhenReturntoHandAnyone 9 · WhenReturntoLibraryAnyone 9 · OnSecurityCheck 9
    //   OnReturnCardsToHandFromTrash 2 · OnPermamemtReturnedToHand 2 (sic) · OnRemovedField 2 ·
    //   OnLeaveFieldAnyone 1 · OnReturnCardsToLibraryFromTrash 1
    WhenRemoveField,
    OnLoseSecurity,
    OnDiscardHand,
    OnAddHand,
    OnDiscardLibrary,
    OnAddSecurity,
    WhenReturntoHandAnyone,
    WhenReturntoLibraryAnyone,
    OnSecurityCheck,
    OnReturnCardsToHandFromTrash,
    OnPermamemtReturnedToHand,
    OnRemovedField,
    OnLeaveFieldAnyone,
    OnReturnCardsToLibraryFromTrash,

    // (PRIM-P0-timing batch 3b) OnEndAttack (80 cards): end of a single attack. Already collected by
    // EndAttackTriggerHook (keys on "OnEndAttack") at AttackPipeline.AdvanceEndAttackAsync — enum-only add.
    OnEndAttack,

    // (PRIM-P0-timing batch 3b) new emit sites added:
    //   OnDigivolutionCardDiscarded 53 — source (under) card trashed by an effect (DigivolutionStackHelpers).
    //   OnAttackTargetChanged 31 — attack defender switched by raid/block (RaidAttackSwitch/BlockTiming).
    // Both are broadcast (see TriggerTimings.BroadcastTimings) to mirror the AS-IS global StackSkillInfos.
    OnDigivolutionCardDiscarded,
    OnAttackTargetChanged,
    //   OnDigivolutionCardReturnToDeckBottom — a Digimon's digivolution cards returned to the deck (c-remediation,
    //   AS-IS ReturnToLibraryBottomDigivolutionCardsClass). Broadcast; emitted by DigivolutionStackHelpers.
    OnDigivolutionCardReturnToDeckBottom,

    // (PRIM-P0-timing batch 4) The would-be-deleted replacement/prevention window (206 cards). A card
    // registered here surfaces as a PRE option in the existing DeletionReplacementTiming synchronous window;
    // activating it prevents/replaces the deletion. See docs/porting/when_permanent_would_be_deleted_design.md.
    WhenPermanentWouldBeDeleted,

    // (F1-M0-1) Bridge-expansion enum reconciliation — the 9 AS-IS EffectTiming members (ICardEffect.cs:969)
    // that had NO headless enum member yet, appended AT THE END to keep every existing ordinal stable
    // (serialization/binding regression point #6). Each name is string-equal to its AS-IS enum member so
    // EffectTimings.ToTriggerName (== ToString()) matches the emitted TriggerTimings value. These are pure
    // placeholders: none is registered in ActivatedBridgeTimings' sets and none has an emit wired for the
    // bridge, so NO new trigger window opens (behavior-neutral). The activated-bridge wiring per timing is the
    // per-timing F-1 milestones (M1+). The 6 AS-IS DEAD timings (OnEndAttackPhase/OnEndBlockDesignation/
    // OnEndCoinToss/OnEndMainPhase/OnGetDamage/OnKnockOut — AS-IS never emits them and no card reacts) are
    // deliberately NOT added — they stay inert.
    AfterEffectsActivate,
    OnDraw,
    OnStartBattle,
    OnUseDigiburst,
    RulesTiming,
    WhenDigisorption,
    WhenUntapAnyone,
    WhenWouldDigivolutionCardDiscarded,
    WhenWouldLink,

    // (F1-DEAD) The 6 AS-IS DEAD timings — declared in the AS-IS EffectTiming enum (ICardEffect.cs:975/984/987/
    // 996/998/1008) but NEVER stacked/gated there and reacted to by ZERO cards (verified true-scan: each name
    // appears ONLY at its enum declaration in DCGO/, no StackSkillInfos/GetSkillInfos/gate). Appended AT THE END
    // (ordinal-stable, regression point #6). Each name is string-equal to its AS-IS enum member so
    // EffectTimings.ToTriggerName (== ToString()) matches the emitted TriggerTimings value. Included per the
    // uniform-wiring principle (a missing call-site is not a skip reason): the enum slot + set classification +
    // emit wiring (where a source exists) are prebuilt so the infra is symmetric with the live timings. They are
    // behavior-neutral (0 cards react), so no regression can arise; the activated bridge only ever produces a
    // marker for a TEST FIXTURE (F1-DeadTimingInfra). Emit status per timing (see ActivatedBridgeTimings /
    // TriggerTimings comments):
    //   OnEndAttackPhase / OnEndMainPhase — queue-emitted (PassAction.cs:28-29); Boundary set opens the bridge.
    //   OnKnockOut  — emitted via a SYNC window (BattleResolver.ResolveKnockOutWindowAsync), NOT the GameEventQueue,
    //                 so the activated bridge never sees it: SubjectScoped registration is LATENT (design item
    //                 F1-DEAD-KNOCKOUT). It does NOT alter the C-4 vestigial phase-1 window (that path uses its own
    //                 AutoProcessingTriggerCollector, never ActivatedBridgeTimings), so no double-fire with
    //                 OnDestroyedAnyone (a distinct timing).
    //   OnEndCoinToss / OnGetDamage / OnEndBlockDesignation — NO emit source exists in headless (no coin-toss /
    //                 damage / block-designation pipeline). Set classification is a LATENT placeholder; emit is a
    //                 design item (F1-DEAD-COINTOSS / F1-DEAD-DAMAGE / F1-DEAD-BLOCKDESIGNATION) — do NOT invent the
    //                 pipeline.
    OnEndAttackPhase,
    OnEndBlockDesignation,
    OnEndCoinToss,
    OnEndMainPhase,
    OnGetDamage,
    OnKnockOut,
}


/// <summary>The headless <see cref="EffectTiming"/> mirror values are named after the engine trigger
/// strings (the "...Anyone" forms used by <c>TriggerTimings</c> / <c>GetEffectsForTiming</c>), so the
/// engine timing string is just the enum name.</summary>
public static class EffectTimings
{
    public static string ToTriggerName(EffectTiming timing) => timing.ToString();
}

