// Source: Assets/Scripts/CardEffect/BT1/Green/BT1_112.cs — a Green Option (two timings).
// 1:1 mirror of the AS-IS BT1_112.
//   [Main] (OptionSkill) "1 of your Digimon gains the following effect for the turn: When this Digimon deletes an
//     opponent's Digimon in battle and survives, unsuspend it." ActivateClass(CanUseCondition =
//     CanTriggerOptionMainEffect, ORDER=-1, ISOPTIONAL=false). CanSelectPermanentCondition =
//     IsPermanentExistsOnOwnerBattleAreaDigimon. ActivateCoroutine: SelectPermanentEffect.SetUp(mode: Custom,
//     maxCount = Min(1, count), canNoSelect:false, canEndNotMax:false). SelectPermanentCoroutine(selected) builds a
//     nested ActivateClass1 ("Unsuspend this Digimon", self = selected.TopCard) with CanUseCondition1 =
//     IsPermanentExistsOnBattleArea(selected) && CanTriggerWhenDeleteOpponentDigimonByBattle(winner = permanent
//     contains selected.TopCard, loser = IsOpponentPermanent, isOnlyWinnerSurvive:true), CanActivateCondition1 =
//     IsPermanentExistsOnBattleArea(selected) && CanUnsuspend(selected), ActivateCoroutine1 = unsuspend selected;
//     then registers it on the SELECTED permanent via CardEffectCommons.AddEffectToPermanent(targetPermanent:
//     selected, EffectDuration.UntilEachTurnEnd, card, cardEffect: activateClass1, timing: OnEndBattle).
//   [Security] (SecuritySkill) AddThisCardToHand(card, activateClass).
// Headless mirror: uniform ActivatedEffect + SelectBody(Mode.Custom) with the AS-IS SelectPermanentCoroutine
//   follow-up wired via SelectBody.onEachSelected: build the nested triggered "unsuspend-self on delete-and-survive"
//   effect (CardEffectFactory.UnsuspendSelfTriggerEffect on the SELECTED card — the exact BT1_077/ST4_11 payload,
//   with its CanUnsuspend CanActivate gate internal) and attach it to the picked permanent for the turn via
//   CardEffectCommons.AddEffectToPermanent. The nested effect self-scopes to the selected id, so its
//   delete-and-survive gate + unsuspend both target exactly the chosen Digimon.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_112 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            bool CanSelect(HeadlessEntityId id) => CardEffectCommons.IsOwnerBattleAreaDigimon(card, id);

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OptionSkill,
                canUse: ctx => CardEffectCommons.CanTriggerOptionMainEffect(ctx, card),
                canActivate: () => CardEffectCommons.HasMatchConditionPermanent(card, CanSelect),
                body: new SelectBody(
                    card: card,
                    canTarget: CanSelect,
                    maxCount: 1,
                    canNoSelect: false,
                    canEndNotMax: false,
                    mode: SelectPermanentEffect.Mode.Custom,
                    description: "[Main] 1 of your Digimon gains 'When this Digimon deletes an opponent's Digimon in battle and survives, unsuspend it.' for the turn.",
                    onEachSelected: id =>
                    {
                        var selectedPermanent = new Permanent(card.Context, id, card.Owner);
                        CardSource targetCard = selectedPermanent.TopCard;

                        // AS-IS CanUseCondition1: the winning permanent is the SELECTED stack (permanent contains
                        // selected.TopCard) and the loser is an opponent's Digimon that does NOT survive.
                        bool WinnerCondition(Permanent permanent) =>
                            targetCard.PermanentOfThisCard() is { TopInstanceId: var top } && !top.IsEmpty && permanent.InstanceId == top;

                        bool LoserCondition(Permanent permanent) => CardEffectCommons.IsOpponentPermanent(permanent, card);

                        bool TriggerGate(CardEffectResolveContext ctx) =>
                            CardEffectCommons.CanTriggerWhenDeleteOpponentDigimonByBattle(
                                ctx, targetCard, WinnerCondition, LoserCondition, isOnlyWinnerSurvive: true);

                        ICardEffect nested = CardEffectFactory.UnsuspendSelfTriggerEffect(
                            timing: EffectTiming.OnEndBattle,
                            card: targetCard,
                            description: "When this Digimon deletes an opponent's Digimon in battle and survives, unsuspend it.",
                            triggerGate: TriggerGate);

                        CardEffectCommons.AddEffectToPermanent(
                            selectedPermanent, EffectDuration.UntilEachTurnEnd, card, nested, EffectTiming.OnEndBattle);
                    }),
                maxCountPerTurn: null,
                isOptional: false,
                description: "[Main] 1 of your Digimon gains 'When this Digimon deletes an opponent's Digimon in battle and survives, unsuspend it.' for the turn."));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.AddThisCardToHandEffect(card));
        }

        return cardEffects;
    }
}
