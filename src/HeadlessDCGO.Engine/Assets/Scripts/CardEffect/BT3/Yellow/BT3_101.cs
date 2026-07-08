// STOP: AS-IS BT3_101 [Main]/[Security] both select 1 of the opponent's Digimon and apply BOTH a DP delta
// (-3000) AND a Security-Attack delta (-1) to the SAME selected target within one select flow
// (SelectPermanentEffect -> SelectPermanentCoroutine calls ChangeDigimonDP then ChangeDigimonSAttack on the
// same `permanent`). The only existing "select + buff" primitives (SelectAndBuffDpEffect /
// SelectAndBuffSAttackEffect / ActivatedTargetBuffEffect) each carry a SINGLE deltaKey/changeValue pair —
// there is no combined-modifier declarative effect. Wiring this as two independent SelectAndBuffXEffect
// calls would present two separate choices (possibly different targets), breaking the AS-IS "one target
// gets both" fidelity. No combining primitive exists, so both timings stay STOP.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT3_101 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // STOP: [Main] 1 opponent Digimon gets -3000 DP AND Security Attack -1 (same target, one select) —
        // no combined DP+SAttack single-select primitive exists (see file header).

        // STOP: [Security] 1 opponent Digimon gets -3000 DP AND Security Attack -1 (same target, one
        // select) — same gap as [Main].

        return cardEffects;
    }
}
