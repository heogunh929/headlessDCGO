// 1:1 mirror of the original BT1_088 (BT1/Green) — a Tamer (mixed timings).
//   [Main] If you have a level 5 or higher green Digimon in play, you can suspend this Tamer to reveal the
//          top card of your deck. If that card is a Digimon card, add it to your hand. Otherwise place it at
//          the bottom of your deck.
//     -> STOP (see below).
//   [Security] Play this Tamer.  -> PlaySelfTamerSecurityEffect (security-skill flow, mirrors ST1_12/ST2_12/
//      ST3_12/ST4_14).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_088 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // STOP (missing ACTION subsystem, not a missing primitive): [Main] "If you have a level 5+ green Digimon
        // in play, you can suspend this Tamer to reveal the top card of your deck. If that card is a Digimon card,
        // add it to your hand. Otherwise place it at the bottom of your deck." AS-IS: an ActivateClass on
        // EffectTiming.OnDeclaration — a MAIN-PHASE activated skill declared on a battle-area permanent
        // (TurnStateMachine field-skill declaration).
        //
        // The blocker is NOT the composed body (a suspend-self cost + reveal-top-and-route body is straightforward,
        // and the reveal-route + self-suspend primitives already exist). The blocker is that headless has NO
        // main-phase "declare a battle-area permanent's [Main] activated skill" ACTION: EffectTiming.OnDeclaration
        // is emitted ONLY at attack declaration (AttackPermanentAction), and the Main-phase legal-action set
        // (HeadlessLegalActionDispatcher) offers only PlayCard / Digivolve / SpecialPlay / ActivateOption /
        // DeclareAttack — there is no ActivatePermanentSkill action, no legal-action generation for it, and no RL
        // factored-action lane (FactoredActionSchema). So a [Main] permanent skill has no way to fire regardless of
        // its body. Closing this needs a new top-level activation-action subsystem + RL action-space contract
        // change — out of scope for a card-porting / primitive pass, and disruptive to the RL lane offsets.
        // Left unregistered for OnDeclaration. — 강모델
        // if (timing == EffectTiming.OnDeclaration) { ... }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlaySelfTamerSecurityEffect(card));
        }

        return cardEffects;
    }
}
