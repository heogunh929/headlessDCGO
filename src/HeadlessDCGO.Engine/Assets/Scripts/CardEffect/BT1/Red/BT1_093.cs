// Source: Assets/Scripts/CardEffect/BT1/Red/BT1_093.cs (ref ST3_13, ST3/Yellow)
// 1:1 mirror of the original BT1_093 — an Option.
//   [Main]     Select 1 of your Digimon: it gets +2000 DP AND <Security Attack +1> (both applied to the SAME
//              selected target from a SINGLE SelectPermanentEffect) for the turn.
//              -> STOP: no headless primitive applies two modifiers (DP delta + Security-Attack delta) to
//              one select-once target. CardEffectFactory only has single-attribute select-buffs
//              (SelectAndBuffDpEffect / SelectAndBuffSAttackEffect, confirmed via 2x grep of
//              CardPortingFramework.cs). Registering them as two separate OptionSkill activations would let
//              each buff land on a DIFFERENT chosen Digimon, which is not 1:1 with the AS-IS single
//              select+dual-buff coroutine (CanSelectPermanentCondition / SelectPermanentCoroutine in
//              BT1_093.cs). Left unregistered per fidelity-over-coverage; see report.
//   [Security] Add this card to its owner's hand.                    -> AddThisCardToHandEffect (mirrors ST3_13)

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_093 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // STOP: [Main] "1 of your Digimon gets +2000 DP and <Security Attack +1> (This Digimon checks 1
        // additional security card) for the turn." — needs a select-once/apply-two-modifiers-to-same-target
        // primitive. No such factory exists (grepped CardEffectFactory.cs for SelectAndBuffDpEffect,
        // SelectAndBuffSAttackEffect, and any Multi/Combo/Dual-buff variant — none found). Not registered;
        // no new primitive added, no engine change made.

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(new AddThisCardToHandEffect(card, "[Security] Add this card to its owner's hand."));
        }

        return cardEffects;
    }
}
