// Source: DCGO/Assets/Scripts/Script/CardEffectFactory/AddDigivolutionRequirement.cs
// (EFFECT-MODEL REBUILD / P4 vertical slice) 1:1 mirror of the AS-IS AddDigivolutionRequirement.cs factory
// partial. Returns the ported AddDigivolutionRequirementClass kind-class (CardEffects/AddEvolutionConditionClass.cs).
// NOTES (P6C3 resolution; see docs/audit/rebuild_p4_factory_missing.md for the original gap record):
//   - cardSource.Owner.CanIgnoreDigivolutionRequirement(permanent, cardSource) -> Player.CanIgnoreDigivolutionRequirement
//     (Script/Player.cs, AS-IS Player.cs:1422-1465, ported 1:1 this pass) via new Player(cardSource.Context,
//     cardSource.Owner) — the established BT2_023 `.Enemy`-route bridge (bare HeadlessPlayerId can't carry it).
//   - permanent.TopCard.CardColors.Contains(cardColor): the mirror CardColors is IReadOnlyList<string>; bridged
//     via CardSource.ToCardColorList (the Group A COLOR-MODEL-DUALITY helper, docs/audit/rebuild_p6_cluster3_notes.md §1).
//   UnityEngine/Photon stripped.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;  // AddDigivolutionRequirementClass (kind-class layer)

public partial class CardEffectFactory
{
    #region Static effect that adds one's own card's digivolution requirement
    public static AddDigivolutionRequirementClass AddSelfDigivolutionRequirementStaticEffect(Func<Permanent, bool> permanentCondition, int digivolutionCost, bool ignoreDigivolutionRequirement, CardSource card, Func<bool> condition, string effectName = null, Func<CardSource, bool> cardCondition = null, Func<int> costEquation = null, CardColor cardColor = CardColor.None, int level = -1, int minLevel = -1, int maxLevel = -1)
    {
        bool CanUseCondition()
        {
            return condition == null || condition();
        }

        return AddDigivolutionRequirementStaticEffect(
            permanentCondition: permanentCondition,
            cardCondition: cardCondition ?? ((cardSource) => cardSource == card),
            ignoreDigivolutionRequirement: ignoreDigivolutionRequirement,
            digivolutionCost: digivolutionCost,
            isInheritedEffect: false,
            card: card,
            condition: CanUseCondition,
            effectName: effectName ?? "Can digivolve to this card",
            costEquation: costEquation,
            cardColor,
            level,
            minLevel,
            maxLevel);
    }
    #endregion

    #region Static effect that adds digivolution requirement
    public static AddDigivolutionRequirementClass AddDigivolutionRequirementStaticEffect(Func<Permanent, bool> permanentCondition, Func<CardSource, bool> cardCondition, bool ignoreDigivolutionRequirement, int digivolutionCost, bool isInheritedEffect, CardSource card, Func<bool> condition, string effectName, Func<int> costEquation = null, CardColor cardColor = CardColor.None, int level = -1, int minLevel = -1, int maxLevel = -1)
    {
        AddDigivolutionRequirementClass addDigivolutionRequirementClass = new AddDigivolutionRequirementClass();
        addDigivolutionRequirementClass.SetUpICardEffect(effectName, CanUseCondition, card);
        addDigivolutionRequirementClass.SetUpAddDigivolutionRequirementClass(getEvoCost: GetEvoCost);

        if (isInheritedEffect)
        {
            addDigivolutionRequirementClass.SetIsInheritedEffect(true);
        }

        bool CanUseCondition(Hashtable hashtable)
        {
            return condition == null || condition();
        }

        int GetEvoCost(Permanent permanent, CardSource cardSource, CardEffectCommons.IgnoreRequirement ignore, bool checkAvailability)
        {
            // ???
            if (ignoreDigivolutionRequirement && !new Player(cardSource.Context, cardSource.Owner).CanIgnoreDigivolutionRequirement(permanent, cardSource))
            {
                return -1;
            }

            if (cardColor == CardColor.None
                || ((ignore.Equals(CardEffectCommons.IgnoreRequirement.Color) || ignore.Equals(CardEffectCommons.IgnoreRequirement.All))
                    && new Player(cardSource.Context, cardSource.Owner).CanIgnoreDigivolutionRequirement(permanent, cardSource))
                || CardSource.ToCardColorList(permanent.TopCard.CardColors).Contains(cardColor))
            {
                bool ignoreLevel = (level < 0 && minLevel < 0 && maxLevel < 0)
                                || ((ignore.Equals(CardEffectCommons.IgnoreRequirement.Level) || ignore.Equals(CardEffectCommons.IgnoreRequirement.All))
                                    && new Player(cardSource.Context, cardSource.Owner).CanIgnoreDigivolutionRequirement(permanent, cardSource));

                //Level matters and doesn't have one
                if(ignoreLevel //If not checking level or if ignoring level requirements
                    || (permanent.TopCard.HasLevel //all other checks will require the permanent has a level
                        && (permanent.TopCard.Level == level //if equal for exact
                            || (level < 0 && (minLevel < 0 || permanent.TopCard.Level >= minLevel) && (maxLevel < 0 || permanent.TopCard.Level <= maxLevel))))) //for "or higher", "or lower" conditons, Lucemon (X Antibody)
                {
                    if (CardCondition(cardSource) && PermanentCondition(permanent))
                    {
                        return costEquation != null ? costEquation() : digivolutionCost;
                    }
                }
            }

            return -1;
        }

        bool PermanentCondition(Permanent permanent)
        {
            if (permanent != null)
            {
                if (permanent.TopCard != null)
                {
                    if (permanentCondition == null || permanentCondition(permanent))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        bool CardCondition(CardSource cardSource)
        {
            if (cardSource != null)
            {
                if (cardCondition == null || cardCondition(cardSource))
                {
                    return true;
                }
            }

            return false;
        }

        return addDigivolutionRequirementClass;
    }
    #endregion
}
