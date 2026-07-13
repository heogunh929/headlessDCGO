// Source: DCGO/Assets/Scripts/CardEffect/BT2/Green/BT2_102.cs
// TRUE AS-IS-verbatim re-port (batch 3). 1:1 mirror of the original BT2_102 (BT2/Green, an Option).
//   [Main]     Return 1 of your opponent's suspended Digimon to the bottom of their deck. (Trash all of the
//              digivolution cards of that Digimon.)
//   [Security] (use the Main effect, custom description)
// Replaces the PREVIOUS pass's old-model `CardEffectFactory.SelectAndReturnToDeckEffect(...)` call (an
// invented helper — `SelectAndReturnToDeckEffect` is explicitly prohibited/retired) with the literal AS-IS
// inline `new ActivateClass()` structure + `GManager.instance.GetComponent<SelectPermanentEffect>()`
// (Mode.PutLibraryBottom) selection pattern (bridge W4). `CardEffectCommons.AddActivateMainOptionSecurityEffect`
// on SecuritySkill IS the real AS-IS call (verbatim, unchanged).
// Substrate translations: IEnumerator->Task, `ContinuousController.instance.StartCoroutine(X)`->`await X`;
// AS-IS `Func<Permanent,bool> CanSelectPermanentCondition` (`permanent.IsSuspended`) -> the established
// entity-id predicate idiom (`CardEffectCommons.IsSuspended(card, id)`).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Green;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT2_102 : CEntity_Effect
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
                return "[Main] Return 1 of your opponent's suspended Digimon to the bottom of their deck. (Trash all of the digivolution cards of that Digimon.)";
            }

            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                return CardEffectCommons.IsOpponentBattleAreaDigimon(card, id)
                    && CardEffectCommons.IsSuspended(card, id);
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
                        mode: SelectPermanentEffect.Mode.PutLibraryBottom,
                        cardEffect: activateClass);

                    await selectPermanentEffect.Activate();
                }
            }
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            CardEffectCommons.AddActivateMainOptionSecurityEffect(card: card, cardEffects: ref cardEffects, effectName: $"Return 1 Digimon to deck");
        }

        return cardEffects;
    }
}
