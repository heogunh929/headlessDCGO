// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/EX9/Red/EX9_008.cs (Biyomon, Digimon / Red, 3 regions)
//    * Alternate Digivolution Requirement :15-33 (timing None — AddSelfDigivolutionRequirementStaticEffect,
//        Lv2 [DM] → cost 0)
//    * Training                           :36-43 (timing OnDeclaration — CardEffectFactory.TrainingEffect)
//    * ESS                                :45-52 (timing OnAllyAttack — RaidSelfEffect isInheritedEffect:true)
//
// ② 프리미티브 매핑: P:AddSelfDigivolutionRequirementStaticEffect, K:Training (TrainingEffect factory,
//    latent-ported at KeyWordEffects/Training.cs; live [Training] path = this factory inline — TfxMainTraining
//    관례), K:Raid (RaidSelfEffect).
//
// ③ 배선 관례: Training → OnDeclaration (실카드 관례 = EX9_026.cs:31-33 + TfxMainTraining). ESS Raid →
//    OnAllyAttack. Alt-digivolve → None.
//
// 치환(substrate translations only):
//    * `targetPermanent.TopCard.IsLevel2` → `.HasLevel && .Level == 2` (미러 idiom; AS-IS IsLevel2 = HasLevel && Level==2,
//      EX8_030/P_198 확립).
//    * `EqualsTraits("DM")` — 미러 실존(CardSource.cs:1406) 그대로.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX9.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class EX9_008 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Alternate Digivolution Requirement

        if (timing == EffectTiming.None)
        {
            static bool PermanentCondition(Permanent targetPermanent)
            {
                if (targetPermanent.TopCard.HasLevel && targetPermanent.TopCard.Level == 2)
                {
                    if (targetPermanent.TopCard.EqualsTraits("DM"))
                    {
                        return true;
                    }
                }
                return false;
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(permanentCondition: PermanentCondition, digivolutionCost: 0, ignoreDigivolutionRequirement: false, card: card, condition: null));
        }

        #endregion

        #region Training

        if (timing == EffectTiming.OnDeclaration)
        {
            cardEffects.Add(CardEffectFactory.TrainingEffect(card: card));
        }

        #endregion

        #region ESS

        if (timing == EffectTiming.OnAllyAttack)
        {
            cardEffects.Add(CardEffectFactory.RaidSelfEffect(isInheritedEffect: true, card: card, condition: null));
        }

        #endregion

        return cardEffects;
    }
}
