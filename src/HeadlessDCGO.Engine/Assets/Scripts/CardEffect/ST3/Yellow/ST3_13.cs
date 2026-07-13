// Source: DCGO/Assets/Scripts/CardEffect/ST3/Yellow/ST3_13.cs
// TRUE AS-IS-verbatim re-port (ST3 Yellow batch). 1:1 mirror of the original ST3_13 (ST3/Yellow) — an Option.
//   [Main]     1 of your Digimon gets +3000 DP for the turn.
//   [Security] All of your Digimon and Security Digimon get +5000 DP for the turn. Then add this card to its
//              owner's hand.
// Replaces the PREVIOUS pass's old-model `CardEffectFactory.SelectAndBuffDpEffect` /
// `PlayerScopeBuffDpEffect`/`PlayerScopeBuffSecurityDpEffect`/`AddThisCardToHandEffect` calls (invented helpers
// with no AS-IS counterpart) with the literal AS-IS inline `new ActivateClass()` structure per timing block.
// AS-IS structure kept verbatim: [Main] passes `null` for CanActivateCondition (only CanUseCondition gates;
// the ActivateCoroutine itself no-ops when there is no valid target); [Security] likewise passes `null` and
// calls `SetIsSecurityEffect(true)`.
// Substrate translation only: IEnumerator->Task, `ContinuousController.instance.StartCoroutine(X)`->`await X`;
// the [Main] AS-IS `Func<Permanent,bool>` CanSelectPermanentCondition is expressed as the established
// `Func<HeadlessEntityId,bool>` idiom (see ST3_08 header). The [Security] body's own local `PermanentCondition
// (Permanent permanent)` stays Permanent-shaped because the player-scope helpers it feeds
// (`ChangeDigimonDPPlayerEffect`/`ChangeSecurityDigimonCardDPPlayerEffect`) already take that AS-IS
// `Func<Permanent,bool>`/`Func<CardSource,bool>` shape directly. AS-IS `CardEffectCommons.AddThisCardToHand(card,
// activateClass)` -> the mirror `AddThisCardToHand(CardSource card1, CardSource sourceCard)` overload, where
// `sourceCard` plays the AS-IS `activateClass`/cause-card role (== `card`, since `SetUpICardEffect` sets
// `EffectSourceCard = card`), so both arguments are `card`.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST3.Yellow;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST3_13 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(card.BaseENGCardNameFromEntity, CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Main] 1 of your Digimon gets +3000 DP for the turn.";
            }

            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                return CardEffectCommons.IsOwnerBattleAreaDigimon(card, id);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                {
                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition));

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanentCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: SelectPermanentCoroutine,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Custom,
                        cardEffect: activateClass);

                    selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon that will get DP +3000.", "The opponent is selecting 1 Digimon that will get DP +3000.");

                    await selectPermanentEffect.Activate();

                    async Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        await CardEffectCommons.ChangeDigimonDP(targetPermanent: permanent, changeValue: +3000, effectDuration: EffectDuration.UntilEachTurnEnd, activateClass: activateClass);
                    }
                }
            }
        }


        if (timing == EffectTiming.SecuritySkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect($"DP +5000 and add this card to hand", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetIsSecurityEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Security] All of your Digimon and Security Digimon get +5000 DP for the turn. Then add this card to its owner's hand.";
            }
            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerSecurityEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                bool PermanentCondition(Permanent permanent)
                {
                    return CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card);
                }

                await CardEffectCommons.ChangeDigimonDPPlayerEffect(
                    permanentCondition: PermanentCondition,
                    changeValue: 5000,
                    effectDuration: EffectDuration.UntilEachTurnEnd,
                    activateClass: activateClass);

                await CardEffectCommons.ChangeSecurityDigimonCardDPPlayerEffect(
                    cardCondition: cardSource => cardSource.Owner == card.Owner,
                    changeValue: 5000,
                    effectDuration: EffectDuration.UntilEachTurnEnd,
                    activateClass: activateClass);

                await CardEffectCommons.AddThisCardToHand(card, card);
            }
        }

        return cardEffects;
    }
}
