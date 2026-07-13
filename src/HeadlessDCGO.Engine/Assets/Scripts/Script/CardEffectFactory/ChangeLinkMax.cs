// Source: DCGO/Assets/Scripts/Script/CardEffectFactory/ChangeLinkMax.cs
// (EFFECT-MODEL REBUILD / P4 vertical slice) 1:1 mirror of the AS-IS ChangeLinkMax.cs factory partial.
// Returns the ported ChangeLinkMaxClass kind-class (CardEffects/ChangeLinkMaxClass.cs).
// ADAPTATIONS (substrate only; logic verbatim):
//   (1) card.PermanentOfThisCard() (mirror returns PermanentView) -> ICardEffect.ResolvePermanentOfThisCard(card).
//   (2) AS-IS permanent.TopCard.CanNotBeAffected(<ICardEffect>) -> mirror CanNotBeAffected(<class>.EffectSourceCard?.InstanceId).
//   UnityEngine/Photon stripped.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;  // ChangeLinkMaxClass (kind-class layer)

public partial class CardEffectFactory
{
    #region Static effect that changes one's own Link Max
    public static ChangeLinkMaxClass ChangeSelfLinkMaxStaticEffect<T>(
        T changeValue,
        bool isInheritedEffect,
        CardSource card,
        Func<bool> condition)
    {
        bool CanUseCondition()
        {
            if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
            {
                if (condition == null || condition())
                {
                    return true;
                }
            }

            return false;
        }

        return ChangeTargetLinkMaxStaticEffect(
            targetPermanent: ICardEffect.ResolvePermanentOfThisCard(card),  // ADAPTATION (1)
            changeValue: changeValue,
            isInheritedEffect: isInheritedEffect,
            card: card,
            condition: CanUseCondition);
    }
    #endregion

    #region Static effect that changes LinkMax
    public static ChangeLinkMaxClass ChangeTargetLinkMaxStaticEffect<T>(
        Permanent targetPermanent,
        T changeValue,
        bool isInheritedEffect,
        CardSource card,
        Func<bool> condition,
        string hashstring = null)
    {
        bool PermanentCondition(Permanent permanent)
        {
            return permanent == targetPermanent;
        }

        return ChangeLinkMaxStaticEffect(
            permanentCondition: PermanentCondition,
            changeValue: changeValue,
            isInheritedEffect: isInheritedEffect,
            card: card,
            condition: condition,
            hashstring: hashstring
        );
    }

    public static ChangeLinkMaxClass ChangeLinkMaxStaticEffect<T>(
        Func<Permanent, bool> permanentCondition,
        T changeValue,
        bool isInheritedEffect,
        CardSource card,
        Func<bool> condition,
        string hashstring = null)
    {
        bool isInt = typeof(T) == typeof(int);
        bool isIntFunc = typeof(T) == typeof(Func<int>);

        if (!isInt && !isIntFunc) return null;

        if (isInt && (int)(object)changeValue == 0) return null;
        if (isIntFunc && changeValue as Func<int> == null) return null;

        int _changeValue() => isInt ? (int)(object)changeValue : (changeValue as Func<int>)();
        bool isUpValue() => _changeValue() > 0;
        string effectName() => isUpValue() ? $"Link Max +{_changeValue()}" : $"Link Max {_changeValue()}";

        ChangeLinkMaxClass changeLinkMaxClass = new ChangeLinkMaxClass();
        changeLinkMaxClass.SetUpICardEffect("", CanUseCondition, card);
        changeLinkMaxClass.SetUpChangeLinkMaxClass(changeLinkMaxFunc: ChangeLinkMax, permanentCondition: PermanentCondition, isUpDown: _isUpDown);

        if (hashstring != null)
        {
            changeLinkMaxClass.SetHashString(hashstring);
        }

        if (isInheritedEffect)
        {
            changeLinkMaxClass.SetIsInheritedEffect(true);
        }

        bool CanUseCondition(Hashtable hashtable)
        {
            if (condition == null || condition())
            {
                changeLinkMaxClass.SetEffectName(effectName());

                return true;
            }

            return false;
        }

        int ChangeLinkMax(Permanent permanent, int linkMax)
        {
            if (PermanentCondition(permanent))
            {
                linkMax += _changeValue();
            }

            return linkMax;
        }

        bool PermanentCondition(Permanent permanent)
        {
            if (CardEffectCommons.IsPermanentExistsOnBattleArea(permanent))
            {
                if (!permanent.TopCard.CanNotBeAffected(changeLinkMaxClass.EffectSourceCard?.InstanceId))  // ADAPTATION (2)
                {
                    if (permanentCondition == null || permanentCondition(permanent))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        CalculateOrder _isUpDown()
        {
            return CalculateOrder.UpDownValue;
        }

        return changeLinkMaxClass;
    }
    #endregion

}
