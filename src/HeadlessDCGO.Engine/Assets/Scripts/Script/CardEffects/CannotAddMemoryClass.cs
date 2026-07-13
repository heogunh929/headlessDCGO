// Source: DCGO/Assets/Scripts/Script/CardEffects/CannotAddMemoryClass.cs
// (EFFECT-MODEL REBUILD / kind-class) 1:1 mirror of AS-IS public class CannotAddMemoryClass : ICardEffect, ICannotAddMemoryEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using System.Collections;
using System.Collections.Generic;
using System;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class CannotAddMemoryClass : ICardEffect, ICannotAddMemoryEffect
{
    public void SetUpCannotAddMemoryClass(Func<Player, bool> PlayerCondition, Func<ICardEffect, bool> CardEffectCondition)
    {
        this.PlayerCondition = PlayerCondition;
        this.CardEffectCondition = CardEffectCondition;
    }

    Func<Player, bool> PlayerCondition { get; set; }
    Func<ICardEffect, bool> CardEffectCondition { get; set; }

    public bool cannotAddMemory(Player player, ICardEffect cardEffect)
    {
        if (player != null)
        {
            if (PlayerCondition != null)
            {
                if (CardEffectCondition != null)
                {
                    if (PlayerCondition(player))
                    {
                        if (CardEffectCondition(cardEffect))
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }
}
