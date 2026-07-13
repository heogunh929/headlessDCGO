// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/DigiXrosEffects.cs
// (P6 cluster3) 1:1 port of the AS-IS single member of this file. Sibling partial of CardEffectCommons.cs
// (same namespace, CardEffectCommons.cs itself is not edited — docs/audit/rebuild_p5_gates_missing.md's
// standing prohibition). Consumers: AD1_025.cs / BT16_025.cs (real cards) / TfxDigiXros.cs, via
// CardEffectFactory.DigiXrosEffectFromNames.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Collections.Generic;

public static partial class CardEffectCommons
{
    #region Create Jogress Conditions from Permanent Conditions

    /// <summary>1:1 mirror of AS-IS <c>GetDigiXrosConditionsFromNames</c> (DigiXrosEffects.cs:8-27): one
    /// material slot per name, matched by <see cref="CardSource.CardNames_DigiXros"/> membership on a
    /// same-owner Digimon.</summary>
    public static DigiXrosCondition GetDigiXrosConditionsFromNames(CardSource card, int CostReduction, Func<List<CardSource>, CardSource, bool> CanTargetCondition_ByPreSelecetedList, params string[] names)
    {
        List<DigiXrosConditionElement> elements = new List<DigiXrosConditionElement>();

        foreach (string name in names)
        {
            elements.Add(new DigiXrosConditionElement(cardSource => CardCondition(cardSource, name), name));
        }

        DigiXrosCondition digiXrosCondition = new DigiXrosCondition(elements, CanTargetCondition_ByPreSelecetedList, CostReduction);

        return digiXrosCondition;

        bool CardCondition(CardSource cardSource, string name)
        {
            return cardSource != null
                && cardSource.Owner == card.Owner
                && cardSource.IsDigimon
                && cardSource.CardNames_DigiXros.Contains(name);
        }
    }

    #endregion
}
