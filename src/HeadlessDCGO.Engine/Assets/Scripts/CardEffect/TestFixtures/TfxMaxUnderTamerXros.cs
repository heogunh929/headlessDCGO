// TEST FIXTURE. A DigiXros card whose single material slot may be satisfied by a card UNDER A TAMER
// (digivolution source, count 1). Exercises the max-under-Tamer DigiXros material extension.
// (SpecialPlay re-migration) Re-shaped from the deleted CardEffectFactory.DigiXrosWithExtraMaterialsEffect
// (SpecialPlayRecipeRegistry currency) to the AS-IS pair of EffectTiming.None declarations the real cards use
// — AddDigiXrosConditionClass (material slots) + AddMaxUnderTamerCountDigiXrosClass (Tamer-source allowance),
// the EX4_062 / BT19_081 exemplar. Consumed by SelectDigiXrosClass (maxTamerDigivolutionCardsCount, :189-275).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxMaxUnderTamerXros : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            AddMaxUnderTamerCountDigiXrosClass addMaxUnderTamerCountDigiXrosClass = new AddMaxUnderTamerCountDigiXrosClass();
            addMaxUnderTamerCountDigiXrosClass.SetUpICardEffect("Can select DigiXros cards from Tamer's digivolution cards", CanUseCondition, card);
            addMaxUnderTamerCountDigiXrosClass.SetUpAddMaxUnderTamerCountDigiXrosClass(getMaxUnderTamerCount: GetCount);
            addMaxUnderTamerCountDigiXrosClass.SetNotShowUI(true);
            effects.Add(addMaxUnderTamerCountDigiXrosClass);

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
