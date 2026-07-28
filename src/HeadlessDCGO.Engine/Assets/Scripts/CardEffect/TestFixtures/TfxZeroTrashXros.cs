// TEST FIXTURE. The zero-allowance twin of TfxMaxTrashXros: the same single-slot DigiXros, but the
// AddMaxTrashCountDigiXrosClass grants 0 trash materials (the boundary case for SelectDigiXrosClass's
// maxTrashCount gate).
// (SpecialPlay re-migration) Re-shaped from the deleted CardEffectFactory.DigiXrosWithExtraMaterialsEffect
// (SpecialPlayRecipeRegistry currency) to the AS-IS EffectTiming.None declarations (BT18_065 exemplar).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxZeroTrashXros : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var e = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            AddMaxTrashCountDigiXrosClass addMaxTrashCountDigiXrosClass = new AddMaxTrashCountDigiXrosClass();
            addMaxTrashCountDigiXrosClass.SetUpICardEffect("Trash cards can be selected for DigiXros", CanUseCondition, card);
            addMaxTrashCountDigiXrosClass.SetUpAddMaxTrashCountDigiXrosClass(getMaxTrashCount: GetCount);
            addMaxTrashCountDigiXrosClass.SetNotShowUI(true);
            e.Add(addMaxTrashCountDigiXrosClass);

            AddDigiXrosConditionClass addDigiXrosConditionClass = new AddDigiXrosConditionClass();
            addDigiXrosConditionClass.SetUpICardEffect("DigiXros", CanUseCondition, card);
            addDigiXrosConditionClass.SetUpAddDigiXrosConditionClass(getDigiXrosCondition: GetDigiXros);
            addDigiXrosConditionClass.SetNotShowUI(true);
            e.Add(addDigiXrosConditionClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return true;
            }

            int GetCount(CardSource cardSource)
            {
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

        return e;
    }
}
