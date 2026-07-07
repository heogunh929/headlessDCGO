// 1:1 mirror of the original BT1_089 (BT1/Green) — a Tamer.
//   [Start of Your Turn] If you have 2 or less memory, set your memory to 3.
//     -> CardEffectFactory.SetMemoryTo3TamerEffect(card) (OnStartTurn).
//   [Main] If you have a level 5 or higher green Digimon in play, you can suspend this Tamer to hatch 1
//   Digi-Egg card to an empty space in your breeding area, or move 1 level 3 or higher Digimon from your
//   breeding area to your battle area.
//     -> STOP (OnDeclaration). See STOP note below — left unregistered.
//   [Security] Play this Tamer. -> PlaySelfTamerSecurityEffect (security-skill flow, mirrors ST1_12/ST2_12/ST3_12/ST4_14)

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_089 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnStartTurn)
        {
            cardEffects.Add(CardEffectFactory.SetMemoryTo3TamerEffect(card));
        }

        // STOP (missing ACTION subsystem, not a missing primitive): [Main] "you can suspend this Tamer to hatch 1
        // Digi-Egg card to an empty space in your breeding area, or move 1 level 3 or higher Digimon from your
        // breeding area to your battle area" (AS-IS ActivateClass on EffectTiming.OnDeclaration — a MAIN-PHASE
        // activated skill declared on a battle-area permanent).
        //
        // Same root blocker as BT1_088: headless has NO main-phase "declare a battle-area permanent's [Main]
        // activated skill" ACTION (OnDeclaration is emitted only at attack declaration; the Main-phase legal-action
        // set offers only PlayCard / Digivolve / SpecialPlay / ActivateOption / DeclareAttack). So a [Main]
        // permanent skill has no way to fire regardless of its body. (Secondary: the hatch-or-move body would also
        // want a "suspend self, then conditionally hatch OR move breeding→battle" async composite — but that is
        // moot while there is no activation action to reach it.) Closing this needs a new top-level
        // activation-action subsystem + RL action-space contract change — out of scope for a card-porting /
        // primitive pass. Left unregistered for OnDeclaration. — 강모델

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlaySelfTamerSecurityEffect(card));
        }

        return cardEffects;
    }
}
