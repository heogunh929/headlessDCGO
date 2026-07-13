// Source: DCGO/Assets/Scripts/Script/CardEffects/CanNotAttackTargetDefendingPermanentClass.cs
// (EFFECT-MODEL REBUILD / kind-class) 1:1 mirror of AS-IS public class CanNotAttackTargetDefendingPermanentClass : ICardEffect, ICanNotAttackTargetDefendingPermanentEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using System.Collections;
using System.Collections.Generic;
using System;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class CanNotAttackTargetDefendingPermanentClass : ICardEffect, ICanNotAttackTargetDefendingPermanentEffect
{
    Func<Permanent, bool> _attackerCondition = null;
    Func<Permanent, bool> _defenderCondition = null;
    public void SetUpCanNotAttackTargetDefendingPermanentClass(Func<Permanent, bool> attackerCondition, Func<Permanent, bool> defenderCondition)
    {
        _attackerCondition = attackerCondition;
        _defenderCondition = defenderCondition;
    }

    public bool CanNotAttackTargetDefendingPermanent(Permanent attacker, Permanent defender)
    {
        if (CardEffectCommons.IsPermanentExistsOnBattleArea(attacker))
        {
            if (_attackerCondition == null || _attackerCondition(attacker))
            {
                if (_defenderCondition == null || _defenderCondition(defender))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
