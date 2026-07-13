// Source: DCGO/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/Partition.cs
// (EFFECT-MODEL REBUILD / P4 KeyWord ASYNC slice) 1:1 mirror of the AS-IS Partition.cs factory partial.
// AS-IS declares top-level `PartitionCondition`. The MIRROR ALREADY HAS it (string-typed colours per the porting
// standard) and its `PartitionConditionsKey` const HAS MANY external consumers, so it is PRESERVED VERBATIM in
// namespace ...CardEffectFactory.KeyWordEffects (NOT re-declared/moved). The old mirror-invented `static class
// Partition` (.Create; ZERO consumers) is dropped. The ported factory methods go in `partial class
// CardEffectFactory` under ...CardEffectCommons (two block namespaces in one file), resolving PartitionCondition
// via `using ...KeyWordEffects;`.
// ADAPTATION: card.PermanentOfThisCard() -> ICardEffect.ResolvePermanentOfThisCard(card); coroutine
// `IEnumerator ActivateCoroutine` (statements + pure delegating return) -> non-async `Task ActivateCoroutine`.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory.KeyWordEffects
{
    /// <summary>
    /// (A4, PRESERVED — many external consumers of <c>PartitionConditionsKey</c>) AS-IS <c>PartitionCondition</c>
    /// 1:1 — a Partition holder always carries exactly TWO of these: [0] group 1, [1] group 2. Three constructor
    /// modes (mutually exclusive): one-colour (exact level + colour), two-colour (exact level + either colour),
    /// by-name (level ignored). Colours are strings headless-side (no CardColor enum — card-facing tolerance).
    /// </summary>
    public sealed class PartitionCondition
    {
        /// <summary>Binding-values key carrying the holder's <c>IReadOnlyList&lt;PartitionCondition&gt;</c>.</summary>
        public const string PartitionConditionsKey = "partition.conditions";

        public int Level;
        public string? Color;
        public string? Color2;
        public string? Name;
        public bool HasOneColour;
        public bool hasTwoColor;
        public bool hasName;

        public PartitionCondition(int level, string color)
        {
            Level = level;
            Color = color;
            HasOneColour = true;
        }

        public PartitionCondition(int level, string color, string color2)
        {
            Level = level;
            Color = color;
            Color2 = color2;
            hasTwoColor = true;
        }

        public PartitionCondition(string cardName)
        {
            Name = cardName;
            hasName = true;
        }
    }
}

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
    using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory.KeyWordEffects; // PartitionCondition

    public partial class CardEffectFactory
    {
        #region Trigger effect of [Partition] on oneself
        public static ActivateClass PartitionSelfEffect(bool isInheritedEffect, CardSource card, Func<bool> condition, List<PartitionCondition> cardSourceConditions)
        {
            Permanent targetPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

            bool CanUseCondition()
            {
                if (condition == null || condition())
                {
                    return true;
                }

                return false;
            }

            return PartitionEffect(targetPermanent: targetPermanent, isInheritedEffect: isInheritedEffect, condition: CanUseCondition, partitionConditions: cardSourceConditions, card);
        }
        #endregion

        #region Trigger effect of [Partition]
        public static ActivateClass PartitionEffect(Permanent targetPermanent, bool isInheritedEffect, Func<bool> condition, List<PartitionCondition> partitionConditions, CardSource card)
        {
            if (targetPermanent == null) return null;
            if (targetPermanent.TopCard == null) return null;
            if (card == null) return null;

            List<CardSource> sourceOneCard = new List<CardSource>();
            List<CardSource> sourceTwoCard = new List<CardSource>();

            sourceOneCard = targetPermanent.DigivolutionCards.Clone();
            sourceTwoCard = targetPermanent.DigivolutionCards.Clone();

            #region Setup Partition Cards

            // First Card

            if (partitionConditions[0].HasOneColour)
            {
                sourceOneCard = sourceOneCard.Filter(source =>
                        source.HasCardColor(partitionConditions[0].Color)
                        && (source.HasLevel && source.Level == partitionConditions[0].Level));
            }
            else if (partitionConditions[0].hasTwoColor)
            {
                sourceOneCard = sourceOneCard.Filter(source =>
                        (source.HasCardColor(partitionConditions[0].Color) || source.HasCardColor(partitionConditions[0].Color2))
                        && (source.HasLevel && source.Level == partitionConditions[0].Level));
            }
            else if (partitionConditions[0].hasName)
            {
                sourceOneCard = sourceOneCard.Filter(source => source.EqualsCardName(partitionConditions[0].Name));
            }
            else
            {
                sourceOneCard = null;
            }


            if (partitionConditions[1].HasOneColour)
            {
                sourceTwoCard = sourceTwoCard.Filter(source =>
                        source.HasCardColor(partitionConditions[1].Color)
                        && (source.HasLevel && source.Level == partitionConditions[1].Level));
            }
            else if (partitionConditions[1].hasTwoColor)
            {
                sourceTwoCard = sourceTwoCard.Filter(source =>
                        (source.HasCardColor(partitionConditions[1].Color) || source.HasCardColor(partitionConditions[1].Color2))
                        && (source.HasLevel && source.Level == partitionConditions[1].Level));
            }
            else if (partitionConditions[1].hasName)
            {
                sourceTwoCard = sourceTwoCard.Filter(source => source.EqualsCardName(partitionConditions[1].Name));
            }
            else
            {
                sourceTwoCard = null;
            }

            #endregion

            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Partition", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            activateClass.SetHashString($"Partition_{card.CardID}" + (isInheritedEffect ? "_inherited" : ""));
            activateClass.SetIsInheritedEffect(isInheritedEffect);

            string EffectDiscription()
            {
                return DataBase.PartitionEffectDiscription();
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.CanTriggerPartition(hashtable, card))
                {
                    if (condition == null || condition())
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.CanActivatePartition(targetPermanent))
                {
                    if (sourceOneCard == null || sourceOneCard.Count > 0)
                    {
                        if (sourceTwoCard == null || sourceTwoCard.Count > 0)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (sourceOneCard.Count == 1)
                    sourceTwoCard = sourceTwoCard.Except(sourceOneCard).ToList();

                if (sourceTwoCard.Count == 1)
                    sourceOneCard = sourceOneCard.Except(sourceTwoCard).ToList();

                return CardEffectCommons.PartitionProcess(activateClass, targetPermanent, sourceOneCard, sourceTwoCard, partitionConditions);
            }

            return activateClass;
        }
        #endregion
    }
}
