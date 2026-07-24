// Source: Assets/Scripts/CardEffect/BT3/Red/BT3_014.cs (a Red Digimon, mixed timings)
// AS-IS has two branches:
//   [When Digivolving] Change the original DP of 1 of your opponent's level 4 or lower Digimon to 1000 for
//   the turn. -> ActivateClass on OnEnterFieldAnyone gated by CanTriggerWhenDigivolving; mandatory select 1
//   opponent level<=4 Digimon (SelectPermanentEffect.Mode.Custom — no built-in mutation), then a nested
//   ChangeBaseDPClass sets the selection's ORIGIN (base) DP to a FIXED value (1000, isUpDownFunc=false —
//   not a delta) for the turn.
//   [Continuous] This card is also treated as yellow, while it exists on the battle area on your turn.
//   -> ChangeCardColorClass (timing None), a 1:1-present headless primitive.
//
// STOP (genuine primitive gap, [When Digivolving] branch only): the headless select flow
// (ActivatedSelectEffect / SelectPermanentEffect.Mode) has no "set base/origin DP to a FIXED value"
// mode — Mode.Custom is an explicit no-op in SelectPermanentEffect.BuildMutation (SelectPermanentEffect.cs:213,
// "Mode.Attack or Mode.Custom => null"), and CardEffectFactory.SelectAndBuffDpEffect / ChangeDPStaticEffect /
// ChangeBaseDPGlobalEffect only ever apply a DELTA (changeValue), never overwrite to an absolute value. The
// engine DOES carry an imperative Permanent-taking mirror (CardEffectCommons.ChangeBaseDigimonDP(Permanent?,
// changeValue, EffectDuration, CardSource) — GiveEffectToPermanent/ChangeOriginDP.cs:10 verbatim), but no
// activated-select factory wires an interactively-chosen target into it (no "select then apply a custom
// per-selection mutation" hook exists on ActivatedSelectEffect). Composing "select 1 opponent Digimon, then
// SET (not add to) its base DP" is a new IActivatedCardEffect, out of scope for a single-card porting pass.
// No cardEffects registered for WhenDigivolving.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT3_014 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // STOP: [When Digivolving] "Change the original DP of 1 of your opponent's level 4 or lower Digimon
        // to 1000 for the turn." — needs a select-then-SET-base-DP-to-fixed-value activated body that does
        // not exist yet (see file header).
        // if (timing == EffectTiming.WhenDigivolving) { ... }

        if (timing == EffectTiming.None)
        {
            bool ChangeCardColorsCanUseCondition() =>
                CardEffectCommons.IsExistOnBattleArea(card) && CardEffectCommons.IsOwnerTurn(card);

            List<string> ChangeCardColors(CardSource cardSource, List<string> colors)
            {
                if (cardSource.InstanceId == card.InstanceId)
                {
                    colors.Add("Yellow");
                }

                return colors;
            }

            var changeCardColorClass = new ChangeCardColorClass();
            changeCardColorClass.SetUpICardEffect("Also treated as yellow", ChangeCardColorsCanUseCondition, card);
            changeCardColorClass.SetUpChangeCardColorClass(ChangeCardColors);
            cardEffects.Add(changeCardColorClass);
        }

        return cardEffects;
    }
}
