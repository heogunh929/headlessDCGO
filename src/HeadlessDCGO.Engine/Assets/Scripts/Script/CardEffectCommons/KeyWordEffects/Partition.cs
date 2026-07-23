
// (P6 cluster2, purely additive — see file header) old-model CardEffectCommons Hashtable-based siblings
// (KeyWordEffects/Partition.cs) — a different namespace/type than the KeywordBaseBatch2Effect resolver above.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory.KeyWordEffects; // PartitionCondition
    using SelectCardEffect = HeadlessDCGO.Engine.Assets.Scripts.Script.SelectCardEffect;

    public static partial class CardEffectCommons
    {
        /// <summary>(P6 cluster2) AS-IS <c>CanTriggerPartition</c> (KeyWordEffects/Partition.cs:10, verbatim).</summary>
        public static bool CanTriggerPartition(Hashtable hashtable, CardSource card) =>
            CanTriggerWhenPermanentRemoveField(hashtable, permanent => permanent.cardSources.Contains(card))
            && !IsByBattle(hashtable)
            && !IsByEffect(hashtable, cardEffect => IsOwnerEffect(cardEffect.EffectSourceCard, card));

        /// <summary>(P6 cluster2) AS-IS <c>CanActivatePartition</c> (KeyWordEffects/Partition.cs:28, verbatim).</summary>
        public static bool CanActivatePartition(Permanent permanent) =>
            IsPermanentExistsOnBattleArea(permanent) && permanent.DigivolutionCards.Count >= 2;

        /// <summary>(R2-B) AS-IS <c>PartitionProcess</c> (KeyWordEffects/Partition.cs:43): play the two selected
        /// digivolution-card groups as new permanents for free. RD-P6C2-6 resolved — <see cref="PartitionClass"/>
        /// is ported 1:1 below (its SelectCardEffect selection + <c>PlayPermanentCards</c> play both have verified
        /// mirror substrate, already exercised by <see cref="FragmentProcess"/>/<see cref="FortitudeProcess"/>).
        /// (The LIVE Partition path also exists independently via DeletionReplacementTiming's PartitionOption;
        /// this is the old-model ActivateClass path a few real/Tfx cards still construct directly.)</summary>
        public static Task PartitionProcess(ICardEffect activateClass, Permanent permanent, List<CardSource> firstSources, List<CardSource> secondSources, List<PartitionCondition> partitionConditions) =>
            new PartitionClass(permanent, partitionConditions).Partition(activateClass, firstSources, secondSources);

        /// <summary>AS-IS <c>PartitionClass</c> (KeyWordEffects/Partition.cs:52-172, nested in CardEffectCommons):
        /// select one card from each qualifying digivolution-source group (auto-taking a group of exactly one)
        /// and, when both groups yield a card, play the two together as new permanents for free. CreateDebuffEffect
        /// and the Play-Log block are UI (stripped, established convention). <c>Color</c>/<c>Color2</c> are already
        /// <see cref="string"/> on the mirror <see cref="PartitionCondition"/>, so <c>GetConditionColors</c> drops
        /// the AS-IS <c>.ToString()</c>. Structure/order verbatim with AS-IS.</summary>
        public class PartitionClass
        {
            public PartitionClass(Permanent permanent, List<PartitionCondition> conditions)
            {
                _permanent = permanent;
                _conditions = conditions;
            }

            Permanent _permanent = null;
            List<PartitionCondition> _conditions = new List<PartitionCondition>();

            string GetConditionColors(PartitionCondition condition)
            {
                string str = condition.Color;

                if (condition.hasTwoColor)
                    str += "/" + condition.Color2;

                return str;
            }

            public async Task Partition(ICardEffect activateClass, List<CardSource> firstSources, List<CardSource> secondSources)
            {
                if (_permanent != null)
                {
                    // CreateDebuffEffect(_permanent) = UI (stripped, established convention).
                    CardSource topCard = _permanent.TopCard;

                    List<CardSource> selectedCards = new List<CardSource>();

                    bool CanSelectCondition(CardSource source)
                    {
                        return true;
                    }

                    string colorsToGrab = "";

                    if (_permanent.TopCard != null)
                    {
                        if (firstSources.Count > 1)
                        {
                            colorsToGrab = GetConditionColors(_conditions[0]);

                            SelectCardEffect selectCardEffect = GManager.instance!.GetComponent<SelectCardEffect>();
                            selectCardEffect.SetUp(
                                    canTargetCondition: CanSelectCondition,
                                    canTargetCondition_ByPreSelecetedList: null,
                                    canEndSelectCondition: null,
                                    canNoSelect: () => false,
                                    selectCardCoroutine: SelectCardCoroutine,
                                    afterSelectCardCoroutine: null,
                                    message: $"Select 1 {colorsToGrab} Digimon to play",
                                    maxCount: 1,
                                    canEndNotMax: false,
                                    isShowOpponent: false,
                                    mode: SelectCardEffect.Mode.Custom,
                                    root: SelectCardEffect.Root.Custom,
                                    customRootCardList: firstSources,
                                    canLookReverseCard: true,
                                    selectPlayer: topCard.Owner,
                                    cardEffect: activateClass);

                            await selectCardEffect.Activate().ConfigureAwait(false);
                        }
                        else
                            selectedCards.AddRange(firstSources);

                        if (secondSources.Count > 1)
                        {
                            colorsToGrab = GetConditionColors(_conditions[1]);

                            SelectCardEffect selectSecondCardEffect = GManager.instance!.GetComponent<SelectCardEffect>();
                            selectSecondCardEffect.SetUp(
                                    canTargetCondition: CanSelectCondition,
                                    canTargetCondition_ByPreSelecetedList: null,
                                    canEndSelectCondition: null,
                                    canNoSelect: () => false,
                                    selectCardCoroutine: SelectCardCoroutine,
                                    afterSelectCardCoroutine: null,
                                    message: $"Select 1 {colorsToGrab} Digimon to play",
                                    maxCount: 1,
                                    canEndNotMax: false,
                                    isShowOpponent: false,
                                    mode: SelectCardEffect.Mode.Custom,
                                    root: SelectCardEffect.Root.Custom,
                                    customRootCardList: secondSources,
                                    canLookReverseCard: true,
                                    selectPlayer: topCard.Owner,
                                    cardEffect: activateClass);

                            await selectSecondCardEffect.Activate().ConfigureAwait(false);
                        }
                        else
                            selectedCards.AddRange(secondSources);

                        Task SelectCardCoroutine(CardSource cardSource)
                        {
                            selectedCards.Add(cardSource);
                            secondSources = secondSources.Except(selectedCards).ToList();
                            return Task.CompletedTask;
                        }
                    }

                    if (selectedCards.Count == 2)
                    {
                        await PlayPermanentCards(
                            cardSources: selectedCards,
                            activateClass: activateClass,
                            payCost: false,
                            isTapped: false,
                            root: SelectCardEffect.Root.DigivolutionCards,
                            activateETB: true).ConfigureAwait(false);
                    }

                    // Play Log block = UI (stripped, established convention).
                }
            }
        }
    }
}
