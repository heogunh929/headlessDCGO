// Source: DCGO/Assets/Scripts/CardEffect/ST3/Yellow/ST3_15.cs
// TRUE AS-IS-verbatim re-port (ST3 Yellow batch). 1:1 mirror of the original ST3_15 (ST3/Yellow) — an Option.
//   [Main]     1 of your opponent's Digimon gains <Security Attack -3> until the end of your opponent's next
//              turn.
//   [Security] All of your opponent's Digimon gain <Security Attack -1> for the turn.
// Replaces the PREVIOUS pass's old-model `CardEffectFactory.SelectAndBuffSAttackEffect`/
// `OpponentScopeBuffSAttackEffect` calls (invented helpers with no AS-IS counterpart) with the literal AS-IS
// inline `new ActivateClass()` structure per timing block (see ST3_13 header for the general translation
// rationale). The [Security] body's own local `PermanentCondition(Permanent permanent)` stays Permanent-shaped
// because `ChangeDigimonSAttackPlayerEffect` already takes that AS-IS `Func<Permanent,bool>` shape directly.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST3.Yellow;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST3_15 : CEntity_Effect
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
                return "[Main] 1 of your opponent's Digimon gains <Security Attack -3>This Digimon checks 3 fewer security cards) until the end of your opponent's next turn.";
            }

            bool PermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, PermanentCondition))
                {
                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, PermanentCondition));

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: PermanentCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: SelectPermanentCoroutine,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Custom,
                        cardEffect: activateClass);

                    selectPermanentEffect.SetUpCustomMessage(
                        "Select 1 Digimon that will get Security Attack -3.",
                        "The opponent is selecting 1 Digimon that will get Security Attack -3.");

                    await selectPermanentEffect.Activate();

                    async Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        await CardEffectCommons.ChangeDigimonSAttack(
                            targetPermanent: permanent,
                            changeValue: -3,
                            effectDuration: EffectDuration.UntilOpponentTurnEnd,
                            activateClass: activateClass);
                    }
                }
            }
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect($"Security Attack -1", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetIsSecurityEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Security] All of your opponent's Digimon gain <Security Attack -1> (This Digimon checks 1 fewer security card) for the turn. ";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerSecurityEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                bool PermanentCondition(Permanent permanent)
                {
                    return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);
                }

                await CardEffectCommons.ChangeDigimonSAttackPlayerEffect(
                    permanentCondition: PermanentCondition,
                    changeValue: -1,
                    effectDuration: EffectDuration.UntilEachTurnEnd,
                    activateClass: activateClass);
            }
        }

        return cardEffects;
    }
}
