// Source: DCGO/Assets/Scripts/Script/CardEffects/CanNotTrashFromDigivolutionCardsClass.cs
// (EFFECT-MODEL REBUILD / kind-class) 1:1 mirror of AS-IS public class CanNotTrashFromDigivolutionCardsClass : ICardEffect, ICanNotTrashFromDigivolutionCardsEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using System.Collections;
using System.Collections.Generic;
using System;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class CanNotTrashFromDigivolutionCardsClass : ICardEffect, ICanNotTrashFromDigivolutionCardsEffect
{
    public void SetUpCanNotTrashFromDigivolutionCardsClass(Func<CardSource, bool> CardCondition, Func<ICardEffect, bool> CardEffectCondition)
    {
        this.CardCondition = CardCondition;
        this.CardEffectCondition = CardEffectCondition;
    }

    Func<CardSource, bool> CardCondition { get; set; }
    Func<ICardEffect, bool> CardEffectCondition { get; set; }

    public bool CanNotTrashFromDigivolutionCards(CardSource cardSource, ICardEffect cardEffect)
    {
        if (cardSource != null)
        {
            if (CardEffectCondition != null)
            {
                if (CardCondition(cardSource))
                {
                    if (CardEffectCondition(cardEffect))
                    {
                        return !cardSource.IsFlipped;
                    }
                }
            }
        }

        return false;
    }
}
