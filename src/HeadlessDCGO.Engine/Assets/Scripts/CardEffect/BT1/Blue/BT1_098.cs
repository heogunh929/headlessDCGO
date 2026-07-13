// Source: Assets/Scripts/CardEffect/BT1/Blue/BT1_098.cs — an Option.
// 1:1 mirror of the AS-IS BT1_098.
//   [Main] (OptionSkill) ActivateClass(CanUseCondition = CanTriggerOptionMainEffect, ORDER=-1, ISOPTIONAL=false).
//     ActivateCoroutine: SelectPermanentEffect.SetUp(selectPlayer: owner, canTargetCondition =
//     IsPermanentExistsOnOwnerBattleAreaDigimon, maxCount = Min(1, MatchConditionPermanentCount),
//     canNoSelect:false, canEndNotMax:false, mode: Custom, selectPermanentCoroutine: SelectPermanentCoroutine).
//     SelectPermanentCoroutine(permanent): CardEffectCommons.GainJamming(targetPermanent: permanent,
//     EffectDuration.UntilEachTurnEnd, activateClass).
//   [Security] (SecuritySkill) AddThisCardToHand(card, activateClass) — its own CanTriggerSecurityEffect-gated
//     ActivateClass (does NOT reuse [Main]).
// Headless mirror: uniform ActivatedEffect + SelectBody(Mode.Custom) with the AS-IS SelectPermanentCoroutine
//   follow-up wired via SelectBody.onEachSelected -> GainJamming on the picked id (UntilEachTurnEnd). Security =
//   AddThisCardToHandEffect (sibling ST3_14/BT1_093 shape).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_098 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
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
                    description: "[Main] 1 of your Digimon gains <Jamming> (This Digimon can't be deleted in battles against Security Digimon) for the turn.",
                    onEachSelected: id => CardEffectCommons.GainJamming(
                        new Permanent(card.Context, id, card.Owner), EffectDuration.UntilEachTurnEnd, card)),
                maxCountPerTurn: null,
                isOptional: false,
                description: "[Main] 1 of your Digimon gains <Jamming> for the turn."));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(new AddThisCardToHandEffect(card, "[Security] Add this card to its owner's hand."));
        }

        return cardEffects;
    }
}
