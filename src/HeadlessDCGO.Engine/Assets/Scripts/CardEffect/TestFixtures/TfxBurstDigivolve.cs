// TEST FIXTURE. Burst Digivolution: this hand card digivolves onto a battle-area "TARGET" Digimon while a
// "TAMER" is returned to the hand (AS-IS BurstDigivolutionCondition).
// (SpecialPlay re-migration) Re-shaped from the deleted CardEffectFactory.BurstDigivolveEffect
// (SpecialPlayRecipeRegistry currency, SpecialPlayKind.Burst) to the AS-IS EffectTiming.None declaration the
// real cards use — AddBurstDigivolutionConditionClass + SetUpAddBurstDigivolutionConditionClass, the
// BT25_104 / BT13_033 exemplar. Consumed by SelectBurstDigivolutionEffect (SelectTamer/BounceTamer) via
// CardSource.BurstDigivolutionConditionOf(). NOTE the AS-IS shape difference: BurstDigivolutionCondition's
// two predicates are Func<Permanent,bool> (not Func<CardSource,bool>), so the card-number tests read
// permanent.TopCard.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxBurstDigivolve : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            AddBurstDigivolutionConditionClass addBurstDigivolutionConditionClass = new AddBurstDigivolutionConditionClass();
            addBurstDigivolutionConditionClass.SetUpICardEffect("Burst Digivolution", CanUseCondition, card);
            addBurstDigivolutionConditionClass.SetUpAddBurstDigivolutionConditionClass(getBurstDigivolutionCondition: GetBurstDigivolution);
            addBurstDigivolutionConditionClass.SetNotShowUI(true);
            effects.Add(addBurstDigivolutionConditionClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return true;
            }

            BurstDigivolutionCondition GetBurstDigivolution(CardSource cardSource)
            {
                if (cardSource == card)
                {
                    bool tamerCondition(Permanent permanent)
                    {
                        return permanent != null
                            && permanent.TopCard != null
                            && permanent.TopCard.Owner == card.Owner
                            && permanent.TopCard.CardNumber == "TAMER";
                    }

                    bool digimonCondition(Permanent permanent)
                    {
                        return permanent != null
                            && permanent.TopCard != null
                            && permanent.TopCard.Owner == card.Owner
                            && permanent.TopCard.CardNumber == "TARGET";
                    }

                    return new BurstDigivolutionCondition(
                        tamerCondition: tamerCondition,
                        selectTamerMessage: "1 [TAMER]",
                        digimonCondition: digimonCondition,
                        selectDigimonMessage: "1 [TARGET]",
                        cost: 0);
                }

                return null;
            }
        }

        return effects;
    }
}
