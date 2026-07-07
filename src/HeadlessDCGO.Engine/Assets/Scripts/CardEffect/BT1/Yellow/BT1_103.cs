// Source: Assets/Scripts/CardEffect/BT1/Yellow/BT1_103.cs — an Option.
// 1:1 mirror of the AS-IS BT1_103.
//   [Main] (OptionSkill) "Until the end of your opponent's next turn, 1 of your Digimon gains <Blocker>."
//     ActivateClass(CanUseCondition = CanTriggerOptionMainEffect, ORDER=-1, ISOPTIONAL=false). ActivateCoroutine
//     (guarded by HasMatchConditionPermanent): SelectPermanentEffect.SetUp(canTargetCondition =
//     IsPermanentExistsOnOwnerBattleAreaDigimon, maxCount = Min(1, MatchConditionPermanentCount),
//     canNoSelect:false, canEndNotMax:false, mode: Custom, selectPermanentCoroutine: SelectPermanentCoroutine).
//     SelectPermanentCoroutine(selectedPermanent): CardEffectCommons.GainBlocker(targetPermanent:
//     selectedPermanent, EffectDuration.UntilOpponentTurnEnd, activateClass).
//   [Security] (SecuritySkill, independent of [Main]) DrawClass(owner, 1).Draw() THEN AddThisCardToHand(card).
// Headless mirror: uniform ActivatedEffect + SelectBody(Mode.Custom) with the AS-IS SelectPermanentCoroutine
//   follow-up wired via SelectBody.onEachSelected -> GainBlocker on the picked id (UntilOpponentTurnEnd).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_103 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

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
                    description: "[Main] Until the end of your opponent's next turn, 1 of your Digimon gains <Blocker>.",
                    onEachSelected: id => CardEffectCommons.GainBlocker(
                        new Permanent(card.Context, id, card.Owner), EffectDuration.UntilOpponentTurnEnd, card)),
                maxCountPerTurn: null,
                isOptional: false,
                description: "[Main] Until the end of your opponent's next turn, 1 of your Digimon gains <Blocker>."));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(new DrawEffect(card, drawCount: 1, "[Security] Trigger <Draw 1>."));
            cardEffects.Add(new AddThisCardToHandEffect(card, "Then, add this card to your hand."));
        }

        return cardEffects;
    }
}
