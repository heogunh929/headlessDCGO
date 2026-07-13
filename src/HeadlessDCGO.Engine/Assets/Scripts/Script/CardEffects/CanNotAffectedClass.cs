// Source: DCGO/Assets/Scripts/Script/CardEffects/CanNotAffectedClass.cs
// (EFFECT-MODEL REBUILD / kind-class) 1:1 mirror of AS-IS public class CanNotAffectedClass : ICardEffect, ICanNotAffectedEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using System.Collections;
using System.Collections.Generic;
using System;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class CanNotAffectedClass : ICardEffect, ICanNotAffectedEffect
{
    Func<CardSource, bool> CardCondition { get; set; }
    Func<ICardEffect, bool> SkillCondition { get; set; }
    public void SetUpCanNotAffectedClass(Func<CardSource, bool> CardCondition, Func<ICardEffect, bool> SkillCondition)
    {
        this.CardCondition = CardCondition;
        this.SkillCondition = SkillCondition;
    }

    public bool CanNotAffect(CardSource cardSource, ICardEffect cardEffect)
    {
        if(cardSource != null && cardEffect != null)
        {
            if(CardCondition != null && SkillCondition != null)
            {
                if(CardCondition(cardSource) && SkillCondition(cardEffect))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
