// Source: Assets/Scripts/CardEffect/BT1/Blue/BT1_100.cs
// 1:1 mirror of the original BT1_100 (BT1/Blue) — an Option (ref ST2_14).
//   [Main]     Until the end of your opponent's next turn, their Digimon with no digivolution cards
//              can't attack.
//   [Security] Your opponent's Digimon with no digivolution cards can't attack for the turn.
//   -> STOP (see below).
// AS-IS (BOTH timings): ActivateClass(CanUseCondition = CanTriggerOptionMainEffect / CanTriggerSecurityEffect,
// ORDER=-1, ISOPTIONAL=false). ActivateCoroutine has NO SelectPermanentEffect step at all (unlike ST2_14,
// which selects exactly 1 target) — it directly calls
// `yield return ContinuousController.instance.StartCoroutine(CardEffectCommons.GainCanNotAttackPlayerEffect(
//     attackerCondition: opponent-battle-area-Digimon-with-no-digivolution-cards,
//     defenderCondition: (permanent) => true,   // no-op filter
//     effectDuration: UntilOpponentTurnEnd (Main) / UntilEachTurnEnd (Security),
//     activateClass: activateClass, effectName: "Can't Attack"))`
// — i.e. it broadly grants the restriction to EVERY currently/future-qualifying opponent Digimon, with no
// player choice, for the stated duration.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_100 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // STOP: [Main] (OptionSkill) AND [Security] (SecuritySkill) — both use the identical shape (only the
        // scope-player/duration differ), so both are left unregistered together.
        //
        // The exact AS-IS primitive this needs IS already ported verbatim:
        //   CardEffectCommons.GainCanNotAttackPlayerEffect(attackerCondition, defenderCondition, effectDuration,
        //     sourceCard, effectName) — CardPortingFramework.cs ~8752, documented "AS-IS GainCanNotAttackPlayerEffect
        //     (GiveEffectToPlayer/CanNotAttack.cs:10, verbatim)". Calling it with attackerCondition = (opponent
        //     battle-area Digimon && HasNoDigivolutionCards) and defenderCondition = null (the AS-IS
        //     DefenderCondition always returns true, i.e. no-op) reproduces this card's text exactly, INCLUDING
        //     the correct duration per timing.
        //
        // But there is no composition that reaches this activation from an Option [Main]/[Security] skill without
        // a player selection:
        //   - ActivatedTargetRestrictionEffect / CardEffectFactory.SelectAndRestrictEffect (ST2_14's shape) forces
        //     a SelectPermanentEffect step (BuildRequest -> answer -> ApplyRestriction) — wrong here, since AS-IS
        //     has NO select at all; forcing one would add player agency the original doesn't have (fidelity fail).
        //   - ActivatedPlayerScopeBuffEffect (wired in ActivatedEffectResolver, case at line ~306) is the one
        //     broad-scope, no-select, activation-dispatched class — but it only carries a numeric delta under a
        //     ModifierHelpers key (DP/SA); it has no restriction-key or per-permanent-predicate support, so it
        //     cannot express CannotAttackKey + a HasNoDigivolutionCards filter.
        //   - ContinuousPlayerScopeRestrictionEffect (CardPortingFramework.cs ~866) DOES accept a scopePredicate
        //     (the per-card attacker filter this card needs) via CanNotAttackStaticEffect, but its ToBinding()
        //     hardcodes `duration: null` (a PERMANENT grant) — there is no duration parameter on the class at
        //     all. Routing this card's temporary ("until opponent turn end" / "until each turn end") restriction
        //     through it via GrantContinuousBody would register a restriction that never expires — materially
        //     stronger than the original, not a faithful mirror (fidelity-over-coverage). It is also a plain
        //     ICardEffect (not IActivatedCardEffect), so an Option/Security timing wouldn't even dispatch it.
        //   - CanNotAttackDefenderConditionEffect (~3810) is a SELF-scoped ("this Digimon can't attack certain
        //     defenders") continuous grant, also permanent — wrong scope (player-wide, not self) and wrong
        //     duration model.
        // Grepped every IEffectBody impl in ActivatedEffect.cs and every case in ActivatedEffectResolver.cs's
        // switch (twice) — confirmed gap, not a naming miss: no existing body/class composes "call
        // GainCanNotAttackPlayerEffect (or an equivalent duration+predicate player-scope CannotAttack grant)
        // directly from an Option/Security activation, with no target selection."
        // Engine-side primitive development (a new IEffectBody, e.g. a "GrantPlayerScopeRestrictionBody" wrapping
        // the existing Gain*PlayerEffect commons calls), deferred to the strong-model primitive pass. — 강모델
        // if (timing == EffectTiming.OptionSkill) { ... }
        // if (timing == EffectTiming.SecuritySkill) { ... }

        return cardEffects;
    }
}
