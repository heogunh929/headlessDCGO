// Source: DCGO/Assets/Scripts/CardEffect/ST1/Red/ST1_16.cs
// TRUE AS-IS-verbatim re-port (ST1/Red batch). 1:1 mirror of the original ST1_16 (Option).
//   [Main]     Delete 1 of your opponent's Digimon.
//   [Security] (use the Main effect)
// Replaces the PREVIOUS pass's old-model `CardEffectFactory.SelectAndDestroyEffect(...)` call (an invented
// helper with no AS-IS counterpart) with the literal AS-IS inline `new ActivateClass()` +
// `GManager.instance.GetComponent<SelectPermanentEffect>()` select flow (bridge W4 — see BT1_017.cs), Mode
// = Destroy. The `[Security]` block already calls the REAL AS-IS `CardEffectCommons.
// AddActivateMainOptionSecurityEffect(...)`, so it is left untouched.
// AS-IS structure kept verbatim: `SetUpActivateClass(null, ...)` (CanActivateCondition IS null),
// `canNoSelect: false, canEndNotMax: false`, `canEndSelectCondition: null`.
// Substrate translation only: IEnumerator->Task, `ContinuousController.instance.StartCoroutine(X)`->`await X`;
// AS-IS `CanSelectPermanentCondition(Permanent permanent)` kept on the canonical `Func<Permanent,bool>` shape
// (id-flip 3b).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST1.Red;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST1_16 : CEntity_Effect
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
                return "[Main] Delete 1 of your opponent's Digimon.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);
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
                        selectPermanentCoroutine: null,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Destroy,
                        cardEffect: activateClass);

                    await selectPermanentEffect.Activate();
                }
            }
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            CardEffectCommons.AddActivateMainOptionSecurityEffect(card: card, cardEffects: ref cardEffects, effectName: $"Delete 1 Digimon");
        }

        return cardEffects;
    }
}
