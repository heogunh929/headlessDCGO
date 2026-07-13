// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/CanUseEffects/OptionEffect.cs
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;

public static partial class CardEffectCommons
{
    #region Can trigger option [Main] effect
    public static bool CanTriggerOptionMainEffect(Hashtable hashtable, CardSource card)
    {
        CardSource Card = GetCardFromHashtable(hashtable);

        if (Card != null)
        {
            if (Card == card)
            {
                return true;
            }
        }

        return false;
    }
    #endregion

    // AS-IS CanDeclareOptionDelayEffect(CardSource) is already defined in CardEffectCommons.cs (substrate
    // reimplementation, identical signature — no Hashtable/ctx param to make it an overload). Duplicating it
    // verbatim would be CS0111, and CardEffectCommons.cs must not be edited, so it is omitted here.
    // See docs/audit/rebuild_p5_gates_missing.md. The verbatim body was:
    //   if (IsExistOnBattleArea(card))
    //   {
    //       if (ICardEffect.ResolvePermanentOfThisCard(card).EnterFieldTurnCount != GManager.instance.turnStateMachine.TurnCount)
    //       {
    //           return true;
    //       }
    //   }
    //   return false;
}
