// Source: DCGO/Assets/Scripts/Script/CardEffectFactory/ChangeDP.cs
// (EFFECT-MODEL REBUILD / P4 vertical slice: continuous DP) 1:1 mirror of the AS-IS ChangeDP.cs factory
// partial. Returns the ported ChangeDPClass kind-class (CardEffects/ChangeDPClass.cs). Replaces the monolith's
// old ContinuousSelfModifierEffect/PlayerScopeModifierEffect-based ChangeSelfDPStaticEffect/ChangeDPStaticEffect.
//
// File at the AS-IS path Script/CardEffectFactory/ChangeDP.cs; namespace ...CardEffectCommons (the canonical
// CardEffectFactory partial class's namespace) so it merges into the same `partial class CardEffectFactory`.
//
// ADAPTATIONS (substrate only; logic verbatim):
//   (1) card.PermanentOfThisCard() returns a PermanentView on the mirror, not a Permanent → bridge via
//       ICardEffect.ResolvePermanentOfThisCard(card) (ICardEffect.cs).
//   (2) AS-IS permanent.TopCard.CanNotBeAffected(ICardEffect) → the mirror CardSource.CanNotBeAffected takes the
//       cause effect's source-card instance id (goal-5 surface), so pass changeDPClass.EffectSourceCard?.InstanceId.
//   `permanent == targetPermanent` now works: mirror Permanent has instance-id value equality (CARDSOURCE-EQUALITY).

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;  // ChangeDPClass (kind-class layer)

public partial class CardEffectFactory
{
    #region Static effect that changes one's own DP
    public static ChangeDPClass ChangeSelfDPStaticEffect<T>(
        T changeValue,
        bool isInheritedEffect,
        CardSource card,
        Func<bool> condition,
        bool isLinkedEffect = false)
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

        return ChangeTargetDPStaticEffect(
            targetPermanent: ICardEffect.ResolvePermanentOfThisCard(card),  // ADAPTATION (1): PermanentView -> Permanent bridge
            changeValue: changeValue,
            isInheritedEffect: isInheritedEffect,
            card: card,
            condition: CanUseCondition,
            isLinkedEffect: isLinkedEffect);
    }
    #endregion

    #region Static effect that changes DP
    public static ChangeDPClass ChangeTargetDPStaticEffect<T>(
        Permanent targetPermanent,
        T changeValue,
        bool isInheritedEffect,
        CardSource card,
        Func<bool> condition,
        bool isLinkedEffect = false)
    {
        bool PermanentCondition(Permanent permanent)
        {
            return permanent == targetPermanent;
        }

        return ChangeDPStaticEffect(
            permanentCondition: PermanentCondition,
            changeValue: changeValue,
            isInheritedEffect: isInheritedEffect,
            card: card,
            condition: condition,
            effectName: null,
            isLinkedEffect: isLinkedEffect
        );
    }

    public static ChangeDPClass ChangeDPStaticEffect<T>(
        Func<Permanent, bool> permanentCondition,
        T changeValue,
        bool isInheritedEffect,
        CardSource card,
        Func<bool> condition,
        Func<string> effectName,
        bool isLinkedEffect = false)
    {
        bool isInt = typeof(T) == typeof(int);
        bool isIntFunc = typeof(T) == typeof(Func<int>);

        if (!isInt && !isIntFunc) return null;

        if (isInt && (int)(object)changeValue == 0) return null;
        if (isIntFunc && changeValue as Func<int> == null) return null;

        int _changeValue() => isInt ? (int)(object)changeValue : (changeValue as Func<int>)();
        bool isUpValue() => _changeValue() > 0;
        string EffectName()
        {
            if (effectName != null)
            {
                return effectName();
            }

            return isUpValue() ? $"DP +{_changeValue()}" : $"DP {_changeValue()}";
        }

        ChangeDPClass changeDPClass = new ChangeDPClass();
        changeDPClass.SetUpICardEffect("", CanUseCondition, card);
        changeDPClass.SetUpChangeDPClass(ChangeDP: ChangeDP, permanentCondition: PermanentCondition, isUpDown: _isUpDown, isMinusDP: () => !isUpValue());
        changeDPClass.SetIsInheritedEffect(isInheritedEffect);
        changeDPClass.SetIsLinkedEffect(isLinkedEffect);

        bool CanUseCondition(Hashtable hashtable)
        {
            if (condition == null || condition())
            {
                changeDPClass.SetEffectName(EffectName());

                return true;
            }

            return false;
        }

        int ChangeDP(Permanent permanent, int DP)
        {
            if (PermanentCondition(permanent))
            {
                DP += _changeValue();
            }

            return DP;
        }

        bool PermanentCondition(Permanent permanent)
        {
            if (CardEffectCommons.IsPermanentExistsOnBattleArea(permanent))
            {
                if (!permanent.TopCard.CanNotBeAffected(changeDPClass))  // ADAPTATION (2)
                {
                    if (permanentCondition == null || permanentCondition(permanent))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        bool _isUpDown()
        {
            return true;
        }

        return changeDPClass;
    }
    #endregion
}
