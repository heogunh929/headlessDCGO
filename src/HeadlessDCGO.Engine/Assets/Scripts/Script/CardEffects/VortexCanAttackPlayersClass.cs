// Source: DCGO/Assets/Scripts/Script/CardEffects/VortexCanAttackPlayersClass.cs
// (EFFECT-MODEL REBUILD / kind-class) 1:1 mirror of AS-IS public class VortexCanAttackPlayersClass : ICardEffect, IVortexCanAttackPlayersEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using System.Collections;
using System.Collections.Generic;
using System;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class VortexCanAttackPlayersClass : ICardEffect, IVortexCanAttackPlayersEffect
{
    Func<Permanent, bool> _attackerCondition = null;
    public void SetUpVortexCanAttackPlayersClass(Func<Permanent, bool> attackerCondition)
    {
        _attackerCondition = attackerCondition;
    }

    public bool VortexCanAttackPlayersPermanent(Permanent attacker)
    {
        return CardEffectCommons.IsPermanentExistsOnBattleArea(attacker)
            && (_attackerCondition == null || _attackerCondition(attacker));
    }
}
