// Source: DCGO/Assets/Scripts/Script/CardEffects/DontBattleSecurityDigimonClass.cs
// (EFFECT-MODEL REBUILD / kind-class) 1:1 mirror of AS-IS public class DontBattleSecurityDigimonClass : ICardEffect, IDontBattleSecurityDigimonEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using System.Collections;
using System.Collections.Generic;
using System;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class DontBattleSecurityDigimonClass : ICardEffect, IDontBattleSecurityDigimonEffect
{
    public void SetUpDontBattleSecurityDigimonClass(Func<CardSource, bool> CardSourceCondition)
    { 
        this.CardSourceCondition = CardSourceCondition;
    }

    Func<CardSource, bool> CardSourceCondition { get; set; }

    public bool DontBattleSecurityDigimon(CardSource cardSource)
    {
        if(CardSourceCondition != null)
        {
            if(cardSource != null)
            {
                if(CardSourceCondition(cardSource))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
