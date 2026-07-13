// Source: Assets/Scripts/CardEffect/BT1/Green/BT1_111.cs — an Option.
//   [Main]     Suspend 1 of your opponent's Digimon or 2 of your opponent's Digimon with 5000 DP or less.
//   [Security] (use the Main effect)                                     -> AddActivateMainOptionSecurityEffect
// AS-IS [Main] (OptionSkill): ActivateClass(CanUseCondition = CanTriggerOptionMainEffect, ORDER=-1,
// ISOPTIONAL=false). ActivateCoroutine computes canSuspend1 = HasMatchConditionPermanent(CanSelectPermanentCondition)
// [any opponent battle-area Digimon] and canSuspend2 = HasMatchConditionPermanent(CanSelectPermanentCondition1)
// [opponent battle-area Digimon with DP<=5000]; if both true, the owner picks a mode via
// UserSelectionManager.SetBoolSelection ("Suspend 1" / "Suspend 2"); if only one is true, that branch is used
// with no menu; if neither, the coroutine no-ops. The chosen branch runs SelectPermanentEffect(Mode.Tap) with
// maxCount = Math.Min(1, MatchConditionPermanentCount(CanSelectPermanentCondition)) for "Suspend 1", or
// Math.Min(2, MatchConditionPermanentCount(CanSelectPermanentCondition1)) for "Suspend 2".
// Headless mirror: CardEffectFactory.SelectModeEffect (PRIM-P0-flow ModeChoiceEffect) — a labeled two-branch
// menu, each branch an existing CardEffectFactory.SelectAndSuspendEffect (Mode.Tap). The AS-IS "only one branch
// available -> skip the menu" shortcut collapses to the SAME game-state outcome here: ModeChoiceEffect only
// offers AVAILABLE modes (its IsAvailable predicate mirrors canSuspend1/canSuspend2 exactly), so when only one
// mode passes, the menu ChoiceRequest carries a single mandatory candidate — no alternate branch is ever
// reachable, matching the AS-IS auto-pick; when neither passes, AvailableModes() is empty and the resolver
// no-ops (ActivatedEffectResolver.ResolveListAsync's `if (available.Count > 0)` guard), matching the AS-IS
// "neither -> do nothing" branch. The AS-IS Math.Min(N, MatchConditionPermanentCount(...)) maxCount clamp is
// subsumed by SelectAndSuspendEffect's own "select up to maxCount matching permanents" behaviour (fixed literal
// maxCount; SelectPermanentEffect.BuildRequest clamps to the live candidate count — same precedent as
// BT1_110/ST4_15's plain "Suspend 1").
//
// AS-IS [Security] (SecuritySkill): ActivateClass(CanUseCondition = CanTriggerSecurityEffect, ORDER=-1,
// ISOPTIONAL=false, IsSecurityEffect=true) via AddActivateMainOptionSecurityEffect (reuse [Main] verbatim, no
// afterMainEffect callback).
// Headless mirror: CardEffectCommons.AddActivateMainOptionSecurityEffect (ReuseMainOptionEffect), same as
// ST1_15/ST1_16/ST2_15/ST2_16/ST3_16/BT1_110's [Security].

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_111 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            const string description =
                "[Main] Suspend 1 of your opponent's Digimon or 2 of your opponent's Digimon with 5000 DP or less.";

            bool CanSelectPermanentCondition(HeadlessEntityId id) =>
                CardEffectCommons.IsOpponentBattleAreaDigimon(card, id);

            bool CanSelectPermanentCondition1(HeadlessEntityId id) =>
                CardEffectCommons.IsOpponentBattleAreaDigimon(card, id)
                    && CardEffectCommons.CurrentDp(card, id) <= 5000;

            cardEffects.Add(CardEffectFactory.SelectModeEffect(
                card,
                description,
                new ModeChoiceEffect.Mode(
                    "Suspend 1",
                    IsAvailable: () => CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition),
                    Branch: CardEffectFactory.SelectAndSuspendEffect(
                        card: card,
                        canTarget: CanSelectPermanentCondition,
                        maxCount: 1,
                        canEndNotMax: false,
                        description: description)),
                new ModeChoiceEffect.Mode(
                    "Suspend 2",
                    IsAvailable: () => CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition1),
                    Branch: CardEffectFactory.SelectAndSuspendEffect(
                        card: card,
                        canTarget: CanSelectPermanentCondition1,
                        maxCount: 2,
                        canEndNotMax: false,
                        description: description))));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            CardEffectCommons.AddActivateMainOptionSecurityEffect(
                card: card,
                cardEffects: ref cardEffects,
                effectName: "Suspend Digimon");
        }

        return cardEffects;
    }
}
