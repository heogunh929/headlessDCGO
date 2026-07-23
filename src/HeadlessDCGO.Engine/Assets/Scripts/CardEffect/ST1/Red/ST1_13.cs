// Source: DCGO/Assets/Scripts/CardEffect/ST1/Red/ST1_13.cs
// TRUE AS-IS-verbatim re-port (ST1/Red batch). 1:1 mirror of the original ST1_13 (Option).
//   [Main]     1 of your Digimon gets +3000 DP for the turn.
//   [Security] All of your Digimon gain <Security Attack +1> (This Digimon checks 1 additional security card)
//              until the end of your next turn.
// Replaces the PREVIOUS pass's old-model `CardEffectFactory.SelectAndBuffDpEffect(...)` /
// `PlayerScopeBuffSAttackEffect(...)` calls (invented helpers with no AS-IS counterpart) with the literal
// AS-IS structure: inline `new ActivateClass()` + `GManager.instance.GetComponent<SelectPermanentEffect>()`
// select flow for [Main] (bridge W4 — see BT1_017.cs), and the real AS-IS
// `CardEffectCommons.ChangeDigimonSAttackPlayerEffect(...)` call for [Security].
// AS-IS structure kept verbatim: `SetUpActivateClass(null, ...)` (CanActivateCondition IS null in both AS-IS
// blocks), `SetIsSecurityEffect(true)` on the Security block only.
// Substrate translation only: IEnumerator->Task, `ContinuousController.instance.StartCoroutine(X)`->`await X`;
// AS-IS `CanSelectPermanentCondition(Permanent permanent)` kept on the canonical `Func<Permanent,bool>` shape
// (id-flip 3b) for the [Main] select predicate (serves SetUp/HasMatchConditionPermanent/
// MatchConditionPermanentCount alike); the [Security] `PermanentCondition(Permanent permanent)` keeps its
// AS-IS `Func<Permanent,bool>` shape verbatim since `ChangeDigimonSAttackPlayerEffect`'s AS-IS-signature
// overload takes that shape directly (no id-conversion needed there).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST1.Red;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST1_13 : CEntity_Effect
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

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card);
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

                    selectPermanentEffect.SetUpCustomMessage(customMessageArray: CardEffectCommons.customPermanentMessageArray_ChangeDP(changeValue: 3000, maxCount: maxCount));

                    await selectPermanentEffect.Activate();

                    async Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        await CardEffectCommons.ChangeDigimonDP(
                            targetPermanent: permanent,
                            changeValue: 3000,
                            effectDuration: EffectDuration.UntilEachTurnEnd,
                            activateClass: activateClass);
                    }
                }
            }
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect($"Security Attack +1", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetIsSecurityEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Security] All of your Digimon gain <Security Attack +1>  (This Digimon checks 1 additional security card) until the end of your next turn.";
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

                await CardEffectCommons.ChangeDigimonSAttackPlayerEffect(
                    permanentCondition: PermanentCondition,
                    changeValue: 1,
                    effectDuration: EffectDuration.UntilOwnerTurnEnd,
                    activateClass: activateClass);
            }
        }

        return cardEffects;
    }
}
