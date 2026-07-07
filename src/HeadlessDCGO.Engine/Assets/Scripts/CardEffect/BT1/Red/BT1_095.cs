// Source: Assets/Scripts/CardEffect/BT1/Red/BT1_095.cs (Red Option)
// AS-IS:
//   [Main] (EffectTiming.OptionSkill). CanUseCondition = CanTriggerOptionMainEffect(hashtable, card) [ported:
//   CardEffectCommons.CanTriggerOptionMainEffect]. ORDER=-1, ISOPTIONAL=false. Description: "[Main] Unsuspend 1
//   of your Digimon. Until the end of your opponent's next turn, that Digimon gains <Blocker>." CanSelectPermanentCondition
//   = IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card) [ported: CardEffectCommons.
//   IsPermanentExistsOnOwnerBattleAreaDigimon]. ActivateCoroutine: guarded by HasMatchConditionPermanent; maxCount
//   = Min(1, MatchConditionPermanentCount); SelectPermanentEffect(selectPlayer: card.Owner, mode: UnTap,
//   canNoSelect:false, canEndNotMax:false) picks exactly 1 own battle-area Digimon and (via the UnTap mode)
//   unsuspends it, THEN (afterSelectPermanentCoroutine, per selected permanent) CardEffectCommons.GainBlocker(
//   targetPermanent: permanent, effectDuration: EffectDuration.UntilOpponentTurnEnd, activateClass) grants
//   <Blocker> to THAT SPECIFIC selected permanent until the end of the opponent's next turn.
//
//   [Security] (EffectTiming.SecuritySkill) is NOT a "(use the Main effect)" reuse here — it is an independently
//   authored ActivateClass with its OWN CanUseCondition = CanTriggerSecurityEffect(hashtable, card) [ported:
//   CardEffectCommons.CanTriggerSecurityEffect] and its own coroutine. Structurally identical select (same
//   CanSelectPermanentCondition, same SelectPermanentEffect Mode.UnTap, maxCount = Min(1, matching count)) but the
//   granted Blocker duration DIFFERS from [Main]: CardEffectCommons.GainBlocker(targetPermanent: permanent,
//   effectDuration: EffectDuration.UntilEachTurnEnd, activateClass) — "for the turn" (current turn only), not
//   "until the end of your opponent's next turn". Because the two branches genuinely diverge (different
//   EffectDuration), this is NOT the AddActivateMainOptionSecurityEffect reuse-Main shape (that mirror is only
//   correct when Security literally replays Main's body/duration verbatim, e.g. ST1_15/16, ST2_15/16, ST3_16,
//   ST4_15/16) — reusing [Main] here would silently grant the WRONG (longer) duration on the Security branch.
//
// STOP (both branches — genuine uniform-ActivatedEffect primitive gap, not a per-card shortcut; grepped 2x per
// rule 4, plus an Explore-agent catalog sweep):
//
// Both [Main] and [Security] need a body of "interactively select 1 of the owner's own battle-area Digimon
// (Mode.UnTap-shaped: unsuspend the pick), THEN — using the SPECIFIC selected permanent as the target — register
// a continuous keyword grant (Blocker) scoped to that exact permanent for a bounded duration". Grepped the uniform
// IEffectBody catalog (Assets/Scripts/Script/CardEffectCommons/ActivatedEffect.cs): DrawBody / MemoryBody /
// RecoveryBody / TrashSecurityBody / SelectTrashHandThenSelfMutationBody / SuspendSelfAndGainMemoryBody /
// SelfToHandBody / GrantContinuousBody / SelectBody — SelectBody's Apply delegates entirely to a single fixed
// SelectPermanentEffect.Mode mutation (UnTap here), with no hook to also register a follow-up continuous
// keyword-grant binding scoped to the id(s) the player actually picked; GrantContinuousBody registers a continuous
// ICardEffect bound to the SOURCE card, not to an interactively-selected OTHER permanent, and is non-interactive
// (IsInteractive=false) so it cannot itself drive the select. Also grepped CardPortingFramework.cs's legacy
// per-shape factories: SelectAndUnsuspendEffect / ActivatedSelectEffect(Mode.UnTap) only apply the UnTap mutation
// to the pick, nothing else; ActivatedTargetBuffEffect / SelectAndBuffSAttackEffect (ST3_15 shape) DOES select +
// register a continuous binding scoped to the selected target(s) — the closest structural analog — but it is
// hardcoded to a numeric ModifierHelpers deltaKey (ContinuousModifierGate.Scope, DP/SAttack), not a
// ContinuousKeywordGate keyword grant, and it does not also apply an UnTap mutation to the pick (its select mode
// is Mode.Custom, a no-op mutation on BuildMutation, since the "effect" is purely the registered buff — see
// SelectPermanentEffect.cs Mode.Custom => null). CardEffectCommons.GainBlocker(targetPermanent, effectDuration,
// sourceCard) itself is a fully-working, already-verbatim-ported primitive (registers the correct
// ContinuousKeywordGate.Blocker binding on an explicit target permanent) — but nothing in the activation-flow
// catalog THREADS the id a player picks via an interactive select into GainBlocker's targetPermanent parameter
// while ALSO applying the UnTap mutation to that same pick. No factory composes "select 1 own Digimon -> unsuspend
// it -> grant it Blocker for a duration". Per rule 4 this is a primitive gap requiring a new composed
// IActivatedCardEffect/IEffectBody in the shared catalog (e.g. an UnTap-mode sibling of ActivatedTargetBuffEffect
// that also accepts a keyword instead of a deltaKey), out of scope for a single-card porting pass and outside the
// no-engine-edit constraint for this task.
//
// No cardEffects registered for either branch. — 강모델
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_095 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // STOP: [Main] "Unsuspend 1 of your Digimon. Until the end of your opponent's next turn, that Digimon
        // gains <Blocker>." — needs a "select 1 own Digimon -> unsuspend it -> grant Blocker to that selected
        // Digimon (UntilOpponentTurnEnd)" activated body that does not exist yet (see file header).
        // if (timing == EffectTiming.OptionSkill) { ... }

        // STOP: [Security] "Unsuspend 1 of your Digimon. That Digimon gains <Blocker> for the turn." — same
        // missing compound body, but a DIFFERENT duration (UntilEachTurnEnd) than [Main]; NOT an
        // AddActivateMainOptionSecurityEffect reuse-Main case (see file header).
        // if (timing == EffectTiming.SecuritySkill) { ... }

        return cardEffects;
    }
}
