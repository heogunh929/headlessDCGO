// TEST FIXTURE (not a real card — no real <Material Save> card is ported yet; this carries the REAL factory
// shape). Mirrors the AS-IS consumer pair (DCGO EX4_020.cs): a DigiXros card returns its
// AddDigiXrosConditionClass at EffectTiming.None (SetUpAddDigiXrosConditionClass(getDigiXrosCondition) — the
// digiXrosCondition property MaterialSave's CanSelectCardCondition reads via card.IsContainDigiXrosCondition)
// AND CardEffectFactory.MaterialSaveEffect(card, materialSaveCount: 2) at WhenPermanentWouldBeDeleted
// (EX4_020.cs:159-162). The material condition here is the fixture shape: an own Digimon card whose card
// number starts with "TfxXrosMat". Used by the C-Del 3c-2b witness matrix to prove the retired-gate Material
// Save fires through the AS-IS PRE cut-in window (select 1 Tamer → place up to 2 matching sources under it;
// the deletion still proceeds — Material Save is NOT a survival replacement). Inert in actual play (no real
// card numbered "TfxMaterialSave").

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxMaterialSave : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            AddDigiXrosConditionClass addDigiXrosConditionClass = new AddDigiXrosConditionClass();
            addDigiXrosConditionClass.SetUpICardEffect("DigiXros -1", CanUseCondition, card);
            addDigiXrosConditionClass.SetUpAddDigiXrosConditionClass(getDigiXrosCondition: GetDigiXros);
            addDigiXrosConditionClass.SetNotShowUI(true);
            cardEffects.Add(addDigiXrosConditionClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return true;
            }

            DigiXrosCondition GetDigiXros(CardSource cardSource)
            {
                if (cardSource == card)
                {
                    DigiXrosConditionElement element = new DigiXrosConditionElement(CanSelectCardCondition, "TfxXrosMat");

                    bool CanSelectCardCondition(CardSource source)
                    {
                        if (source != null)
                        {
                            if (source.Owner == card.Owner)
                            {
                                if (source.IsDigimon)
                                {
                                    if (source.CardNumber.StartsWith("TfxXrosMat", StringComparison.Ordinal))
                                    {
                                        return true;
                                    }
                                }
                            }
                        }

                        return false;
                    }

                    return new DigiXrosCondition(
                        new List<DigiXrosConditionElement> { element },
                        CanTargetCondition_ByPreSelecetedList: null,
                        reduceCostPerCard: 1);
                }

                return null;
            }
        }

        if (timing == EffectTiming.WhenPermanentWouldBeDeleted)
        {
            cardEffects.Add(CardEffectFactory.MaterialSaveEffect(card: card, materialSaveCount: 2));
        }

        return cardEffects;
    }
}
