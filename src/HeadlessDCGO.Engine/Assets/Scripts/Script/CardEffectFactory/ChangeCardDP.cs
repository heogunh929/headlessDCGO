// Source: DCGO/Assets/Scripts/Script/CardEffectFactory/ChangeCardDP.cs
// (EFFECT-MODEL REBUILD / P4 vertical slice) 1:1 mirror of the AS-IS ChangeCardDP.cs factory partial.
// Returns the ported ChangeCardDPClass kind-class (CardEffects/ChangeCardDPClass.cs).
// NOTE (diverged substrate; see docs/audit/rebuild_p4_factory_missing.md — kept VERBATIM per the
//   no-simplification directive): AS-IS `GManager.instance.attackProcess.SecurityDigimon == cardSource`.
//   The mirror GManager / AttackProcess exist, but AttackProcess.SecurityDigimon is a HeadlessEntityId?
//   (AS-IS it is a CardSource), so the `== cardSource` comparison does not lower cleanly. This body-level
//   divergence is currently MASKED: CardEffectFactory is a partial class whose monolith part carries
//   type-level CS0246 (IActivatedCardEffect), which suppresses method-body diagnostics across all its
//   partials; it will surface once that part binds. UnityEngine/Photon stripped.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;  // ChangeCardDPClass (kind-class layer)

public partial class CardEffectFactory
{
    #region Static effect that changes security Digimon card DP
    public static ChangeCardDPClass ChangeSecurityDigimonCardDPStaticEffect<T>(Func<CardSource, bool> cardCondition, T changeValue, bool isInheritedEffect, CardSource card, Func<bool> condition, string effectName, bool islinkedEffect = false)
    {
        bool isInt = typeof(T) == typeof(int);
        bool isIntFunc = typeof(T) == typeof(Func<int>);

        if (!isInt && !isIntFunc) return null;

        if (isInt && (int)(object)changeValue == 0) return null;
        if (isIntFunc && changeValue as Func<int> == null) return null;

        int _changeValue() => isInt ? (int)(object)changeValue : (changeValue as Func<int>)();
        bool isUpValue() => _changeValue() > 0;

        ChangeCardDPClass changeCardDPClass = new ChangeCardDPClass();
        changeCardDPClass.SetUpICardEffect("", CanUseCondition, card);
        changeCardDPClass.SetUpChangeCardDPClass(changeDPFunc: ChangeDP, cardSourceCondition: CardCondition, isUpDown: _isUpDown, isMinusDP: () => !isUpValue());
        changeCardDPClass.SetIsInheritedEffect(isInheritedEffect);
        changeCardDPClass.SetIsLinkedEffect(islinkedEffect);

        bool CanUseCondition(Hashtable hashtable)
        {
            if (condition == null || condition())
            {
                changeCardDPClass.SetEffectName(effectName);

                return true;
            }

            return false;
        }

        int ChangeDP(CardSource cardSource, int DP)
        {
            if (CardCondition(cardSource))
            {
                DP += _changeValue();
            }

            return DP;
        }

        bool CardCondition(CardSource cardSource)
        {
            if (cardSource != null)
            {
                if (GManager.instance.attackProcess.SecurityDigimon == cardSource)
                {
                    if (cardCondition == null || cardCondition(cardSource))
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

        return changeCardDPClass;
    }
    #endregion
}
