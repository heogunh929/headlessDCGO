// Source: Assets/Scripts/CardEffect/BT3/Black/BT3_105.cs
// AS-IS has two branches (an Option):
//   [Main] 1 of your Digimon gains <Reboot> and "This Digimon can't have its DP reduced or be returned to
//   its owner's hand or deck" until the end of your opponent's next turn.
//   [Security] Your opponent's Digimon can't attack players for the turn.
//
// STOP [Main] (genuine primitive gap, grepped 2x+ per rule 4): needs "interactively select 1 of your
// Digimon, THEN apply FOUR separate duration-tagged grants (Reboot keyword + immune-from-DP-minus +
// can't-return-to-hand + can't-return-to-deck) to that SAME selection." Grepped
// Assets/Scripts/Script/CardEffectCommons/CardPortingFramework.cs's IActivatedCardEffect catalog
// (ActivatedSelectEffect / ActivatedTargetBuffEffect / ActivatedTargetRestrictionEffect /
// ActivatedSelectAndDeDigivolveEffect / ActivatedSelectAndPlayEffect / ActivatedPlayFromUnderEffect /
// SuspendCostReductionEffect / ActivatedPlayerScopeBuffEffect / ActivatedMemoryEffect /
// ActivatedSelectTrashDigivolutionEffect): every one applies exactly ONE fixed mutation kind (buff/
// restrict-attack-or-block/de-digivolve/play/bounce/etc.) to the selection — none applies an ARBITRARY set
// of grants, and ActivatedTargetRestrictionEffect's "restriction" shape is hardcoded to
// cannotAttack/cannotBlock only (no Reboot-keyword or ImmuneFromDpMinus/CannotReturnToHand/
// CannotReturnToDeck support). The CardEffectCommons.GainReboot / GainImmuneFromDPMinus /
// GainCanNotReturnToHand / GainCanNotReturnToDeck helpers (CardPortingFramework.cs) DO exist and DO accept
// a Permanent target + EffectDuration, but they are imperative "mutate the registry right now" calls with
// zero existing callers in any ported card — there is no composed IActivatedCardEffect whose Apply()
// invokes them against an interactively-selected target (they are prepared-but-unwired primitives; wiring
// them to a live selection is new engine-layer work, not per-card wiring). No factory composes "select 1 ->
// apply N duration-tagged grants to the selection."
//
// STOP [Security] (same rule-4 gap class): needs a MANDATORY, no-select, duration-tagged ("for the turn")
// grant of CannotAttack to ALL of the opponent's Digimon, resolved once when this Security card is checked.
// CardEffectCommons.GainCanNotAttackPlayerEffect is the exact AS-IS-shaped helper (player-scope + duration)
// but — like the Main-branch Gain* helpers above — it is an imperative "mutate the registry now" call with
// no existing IActivatedCardEffect wiring it to the SecuritySkill activation flow (ActivatedEffectResolver.
// ResolveListAsync's switch, CardPortingFramework.cs, has no matching case). The one EXISTING duration-aware
// player-scope activated primitive, ActivatedPlayerScopeBuffEffect (CardEffectFactory.
// PlayerScopeBuffSAttackEffect / PlayerScopeBuffDpEffect / etc.), only carries a NUMERIC delta (DP/Security
// Attack), not a restriction flag — it cannot express CannotAttack. The always-on CONTINUOUS restriction
// primitive (ContinuousPlayerScopeRestrictionEffect, used by this set's BT3_075) hardcodes
// `duration: null` in its ToBinding (verified: CardPortingFramework.cs) — i.e. PERMANENT only; it has no
// EffectDuration parameter, so it cannot express "for the turn" without over-strengthening the grant to
// permanent (forbidden by fidelity-over-coverage). No factory composes "no-select, duration-tagged,
// activated player-scope restriction grant." Per rule 4 both branches are primitive gaps, out of scope for
// a single-card porting pass. No cardEffects registered. — Sonnet

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Black;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT3_105 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // STOP: [Main] "1 of your Digimon gains <Reboot> and 'can't have its DP reduced or be returned to
        // its owner's hand or deck' until the end of your opponent's next turn." — needs a "select 1 ->
        // apply N duration-tagged grants to the selection" primitive that does not exist yet (see file
        // header).
        // if (timing == EffectTiming.OptionSkill) { ... }

        // STOP: [Security] "Your opponent's Digimon can't attack players for the turn." — needs a
        // no-select, duration-tagged, activated player-scope restriction-grant primitive that does not
        // exist yet (see file header).
        // if (timing == EffectTiming.SecuritySkill) { ... }

        return cardEffects;
    }
}
