// TEST FIXTURE. A DigiXros card whose single material slot may be satisfied by a card FROM THE TRASH
// (AddMaxTrashCountDigiXros, count 1). Exercises the max-trash DigiXros material extension.
// (SpecialPlay re-migration) Re-shaped from the deleted CardEffectFactory.DigiXrosWithExtraMaterialsEffect
// (SpecialPlayRecipeRegistry currency) to the AS-IS pair of EffectTiming.None declarations the real cards use
// — AddDigiXrosConditionClass (material slots) + AddMaxTrashCountDigiXrosClass (trash allowance), the BT18_065
// exemplar. Consumed by SelectDigiXrosClass (maxTrashCount, :103-160).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxMaxTrashXros : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            AddMaxTrashCountDigiXrosClass addMaxTrashCountDigiXrosClass = new AddMaxTrashCountDigiXrosClass();
            addMaxTrashCountDigiXrosClass.SetUpICardEffect("Trash cards can be selected for DigiXros", CanUseCondition, card);
            addMaxTrashCountDigiXrosClass.SetUpAddMaxTrashCountDigiXrosClass(getMaxTrashCount: GetCount);
            addMaxTrashCountDigiXrosClass.SetNotShowUI(true);
            effects.Add(addMaxTrashCountDigiXrosClass);

            AddDigiXrosConditionClass addDigiXrosConditionClass = new AddDigiXrosConditionClass();
            addDigiXrosConditionClass.SetUpICardEffect("DigiXros", CanUseCondition, card);
            addDigiXrosConditionClass.SetUpAddDigiXrosConditionClass(getDigiXrosCondition: GetDigiXros);
            addDigiXrosConditionClass.SetNotShowUI(true);
            effects.Add(addDigiXrosConditionClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return true;
            }

            int GetCount(CardSource cardSource)
            {
                if (cardSource == card)
                {
                    return 1;
                }

                return 0;
            }

            DigiXrosCondition GetDigiXros(CardSource cardSource)
            {
                if (cardSource == card)
                {
                    DigiXrosConditionElement element = new DigiXrosConditionElement(CanSelectCardCondition, "MAT");

                    bool CanSelectCardCondition(CardSource source)
                    {
                        return source != null && source.CardNumber == "MAT";
                    }

                    return new DigiXrosCondition(
                        new List<DigiXrosConditionElement> { element },
                        CanTargetCondition_ByPreSelecetedList: null,
                        reduceCostPerCard: 0);
                }

                return null;
            }
        }

        return effects;
    }
}
