// Source: DCGO/Assets/Scripts/Script/CardEffects/AddJogressConditionClass.cs
// (EFFECT-MODEL REBUILD / kind-class) 1:1 mirror of AS-IS public class AddJogressConditionClass : ICardEffect, IAddJogressConditionEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using System.Collections;
using System.Collections.Generic;
using System;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class AddJogressConditionClass : ICardEffect, IAddJogressConditionEffect
{
    Func<CardSource, JogressCondition> _getJogressCondition { get; set; }
    public void SetUpAddJogressConditionClass(Func<CardSource, JogressCondition> getJogressCondition)
    {
        _getJogressCondition = getJogressCondition;
    }

    public JogressCondition GetJogressCondition(CardSource cardSource)
    {
        if (cardSource != null)
        {
            if (_getJogressCondition != null)
            {
                JogressCondition jogressCondition = _getJogressCondition(cardSource);

                if (jogressCondition != null)
                {
                    if (jogressCondition.elements != null)
                    {
                        JogressConditionElement[] newElements = jogressCondition.elements.Map((element) =>
                                                     {
                                                         bool EvoRootCondition(Permanent permanent)
                                                         {
                                                             return CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, cardSource) && (element.EvoRootCondition == null || element.EvoRootCondition(permanent));
                                                         }

                                                         return new JogressConditionElement(evoRootCondition: EvoRootCondition, selectMessage: element.SelectMessage);
                                                     });

                        return new JogressCondition(newElements, jogressCondition.cost);
                    }
                }
            }
        }

        return null;
    }
}
