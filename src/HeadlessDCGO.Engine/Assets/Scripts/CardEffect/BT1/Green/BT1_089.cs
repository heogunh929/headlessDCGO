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

        // STOP: [Main] "you can suspend this Tamer to hatch 1 Digi-Egg card to an empty space in your
        // breeding area, or move 1 level 3 or higher Digimon from your breeding area to your battle area"
        // (OnDeclaration). The CanUseCondition gate (IsExistOnBattleArea + CanActivateSuspendCostEffect +
        // HasMatchConditionOwnersPermanent(green Digimon, level>=5)) would mirror cleanly with existing
        // CardEffectCommons predicates, but the AS-IS ActivateCoroutine body is: suspend this Tamer as a cost,
        // THEN conditionally either (a) hatch (CanHatch-gated) or (b) — only if not hatchable — move the
        // owner's first breeding-area permanent (GetBreedingAreaPermanents()[0], level>=3, CanMove) to the
        // battle area (CardObjectController.MovePermanent), gated on an empty battle-area frame. Grepped twice
        // for a reusable primitive:
        //   (1) Assets/Scripts/Script/CardEffectCommons/ActivatedEffect.cs — the full uniform IEffectBody set
        //       (DrawBody / MemoryBody / RecoveryBody / TrashSecurityBody /
        //       SelectTrashHandThenSelfMutationBody / SuspendSelfAndGainMemoryBody / SelfToHandBody /
        //       GrantContinuousBody / SelectBody) has no hatch-or-move body, and IEffectBody.Apply is
        //       synchronous — there is no path from it to the async ZoneMover.HatchDigitamaAsync /
        //       ZoneMover.MoveBreedingToBattleAsync calls that the two candidate outcomes require.
        //   (2) ActivatedEffectResolver.cs's switch dispatches existing async composites (HatchDigiEggEffect,
        //       PlayCardEffect, LinkSelfEffect, ArtsDigivolveSelfEffect, ...) as individually-cased
        //       IActivatedCardEffect types, each doing exactly one thing — there is no "suspend self cost,
        //       then conditionally hatch OR move" case, and no generic sequence/composite wrapper exists
        //       either (grepped for Composite/Sequence-shaped wrappers: none). Adding either a new IEffectBody
        //       async path or a new resolver case would be a new primitive / engine-layer change, which is out
        //       of scope for a single-card port (no primitive or engine changes here).
        // Left UNREGISTERED for OnDeclaration (no ICardEffect added) rather than throwing or inventing a
        // primitive.

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlaySelfTamerSecurityEffect(card));
        }

        return cardEffects;
    }
}
