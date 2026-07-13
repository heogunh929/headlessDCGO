// Source: DCGO/Assets/Scripts/CardEffect/BT2/Blue/BT2_095.cs
// TRUE AS-IS-verbatim re-port (batch 3). 1:1 mirror of the original BT2_095 (BT2/Blue, an Option).
//   [Main] Return up to 3 of your opponent's level 3 Digimon to their hand. (Trash all of the digivolution
//   cards of those Digimon.) No [Security] effect in AS-IS.
// Replaces the PREVIOUS pass's old-model `CardEffectFactory.SelectAndBounceEffect(...)` call (an invented
// helper with no AS-IS counterpart) with the literal AS-IS inline `new ActivateClass()` structure +
// `GManager.instance.GetComponent<SelectPermanentEffect>()` (Mode.Bounce) selection pattern (bridge W4).
// Substrate translations: IEnumerator->Task, `ContinuousController.instance.StartCoroutine(X)`->`await X`;
// AS-IS `Func<Permanent,bool> CanSelectPermanentCondition` (with `permanent.Level == 3` / `permanent.TopCard.
// HasLevel`) -> the established entity-id predicate idiom (`CardEffectCommons.LevelOf(card, id)` / the id
// carries the same top-card-level-gate the mirror `LevelOf` helper already applies internally).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Blue;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT2_095 : CEntity_Effect
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
                return "[Main] Return up to 3 of your opponent's level 3 Digimon to their hand. (Trash all of the digivolution cards of those Digimon.)";
            }

            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                return CardEffectCommons.IsOpponentBattleAreaDigimon(card, id)
                    && CardEffectCommons.LevelOf(card, id) == 3;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                {
                    int maxCount = Math.Min(3, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition));

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanentCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: CanEndSelectCondition,
                        maxCount: maxCount,
                        canNoSelect: false,
                        canEndNotMax: true,
                        selectPermanentCoroutine: null,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Bounce,
                        cardEffect: activateClass);

                    await selectPermanentEffect.Activate();

                    bool CanEndSelectCondition(List<Permanent> permanents)
                    {
                        if (CardEffectCommons.HasNoElement(permanents))
                        {
                            return false;
                        }

                        return true;
                    }
                }
            }
        }

        return cardEffects;
    }
}
