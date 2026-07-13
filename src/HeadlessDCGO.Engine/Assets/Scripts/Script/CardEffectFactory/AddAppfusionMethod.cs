// Source: DCGO/Assets/Scripts/Script/CardEffectFactory/AddAppfusionMethod.cs
// (EFFECT-MODEL REBUILD / P4 vertical slice) 1:1 mirror of the AS-IS AddAppfusionMethod.cs factory partial.
// Returns the ported AddAppFusionConditionClass kind-class (CardEffects/AddAppFusionConditionClass.cs).
// NOTE (diverged substrate; see docs/audit/rebuild_p4_factory_missing.md — kept VERBATIM per the
//   no-simplification directive): AS-IS `if (permanent.LinkedCards.Find(x => cardConditions[j](x)))` relies on
//   UnityEngine.Object's implicit bool conversion (List<CardSource>.Find returns a CardSource that Unity treats
//   as truthy). The mirror CardSource has no implicit-bool operator, so this expression does not lower cleanly.
//   UnityEngine/Photon stripped.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;  // AddAppFusionConditionClass (kind-class layer)

public partial class CardEffectFactory
{
    #region Static effect that gives the ability to appfuse by name
    public static AddAppFusionConditionClass AddAppfuseMethodByName(List<string> cardNames, CardSource card, int cost = 0, string effectName = "App Fusion")
    {
        return AddAppfuseMethodByCondition(cardNames.Select<string, Func<CardSource, bool>>(name => card => card.EqualsCardName(name)).ToList(), card, cost, effectName);
    }
    #endregion

    #region Static effect that gives the ability to appfuse by condition
    public static AddAppFusionConditionClass AddAppfuseMethodByCondition(List<Func<CardSource, bool>> cardConditions, CardSource card, int cost = 0, string effectName = "App Fusion")
    {
        AddAppFusionConditionClass addAppFusionConditionClass = new AddAppFusionConditionClass();
        addAppFusionConditionClass.SetUpICardEffect(effectName, (hashtable) => true, card);
        addAppFusionConditionClass.SetUpAddAppFusionConditionClass(getAppFusionCondition: GetAppFusion);
        addAppFusionConditionClass.SetNotShowUI(true);
        return addAppFusionConditionClass;

        AppFusionCondition GetAppFusion(CardSource cardSource)
        {
            bool linkCondition(Permanent permanent, CardSource source)
            {
                if (source != null)
                {
                    if (source != card)
                    {
                        for (int i = 0; i < cardConditions.Count; i++)
                        {
                            if (cardConditions[i](source))
                            {
                                for (int j = 0; j < cardConditions.Count; j++)
                                {
                                    if(i != j)
                                    {
                                        if (cardConditions[j](permanent.TopCard))
                                        {
                                            return true;
                                        }
                                    }
                                }
                            }

                        }
                    }
                }

                return false;
            }
            bool digimonCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card))
                {
                    for (int i = 0; i < cardConditions.Count; i++)
                    {
                        if (cardConditions[i](permanent.TopCard))
                        {
                            for (int j = 0; j < cardConditions.Count; j++)
                            {
                                if (i != j)
                                {
                                    if (permanent.LinkedCards.Find(x => cardConditions[j](x)))
                                    {
                                        return true;
                                    }
                                }
                            }
                        }

                    }
                }

                return false;
            }

            if (cardSource == card)
            {
                AppFusionCondition AppFusionCondition = new AppFusionCondition(
                    linkedCondition: linkCondition,
                    digimonCondition: digimonCondition,
                    cost: cost);

                return AppFusionCondition;
            }

            return null;
        }
    }
    #endregion
}
