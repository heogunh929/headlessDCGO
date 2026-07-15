// Source: Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Decode.cs
// AS-IS mirror: per-keyword partial of KeywordBaseBatch2Effect (Decode). Shared scaffolding lives in
// KeywordBaseBatch2.cs; this file holds only Decode's resolution branch (1:1 with the original
// CardEffectCommons.CanActivateDecode). The LIVE play-a-source-for-free path is engine plumbing in
// DeletionReplacementTiming (DecodeOption) + DeletionReplacementGate.TryDecodePlaySourceAsync — this
// branch is the grant/mirror layer (resolving emits GrantDecode -> hasDecode).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects
{
    using HeadlessDCGO.Engine.Headless.Effects;
    using HeadlessDCGO.Engine.Headless.Services;
    using HeadlessDCGO.Engine.Headless.State;

    public sealed partial class KeywordBaseBatch2Effect
    {
        private CardEffectCanResolveResult CanResolveDecode(
            CardEffectResolveContext context,
            CardInstanceState target)
        {
            if (!context.EffectContext.TryGetValue(KeywordBaseBatch2ContextKeys.RemovedFromField, out bool removedFromField)
                || !removedFromField)
            {
                return Failure("Decode requires a field removal event.", "removedFromField", context, target.InstanceId);
            }

            if (!context.EffectContext.TryGetValue(KeywordBaseBatch2ContextKeys.RemovedCardId, out HeadlessEntityId removedCardId)
                || removedCardId != target.InstanceId)
            {
                return Failure("Decode requires the keyword target to be removed.", "removedCardId", context, target.InstanceId);
            }

            // AS-IS !IsByBattle: Decode fires only on a non-battle field-leave (CanTriggerWhenRemoveField).
            if (context.EffectContext.TryGetValue(KeywordBaseBatch2ContextKeys.DeletedByBattle, out bool deletedByBattle)
                && deletedByBattle)
            {
                return Failure("Decode does not trigger on battle removal.", "deletedByBattle", context, target.InstanceId);
            }

            if (target.SourceIds.Count < 1)
            {
                return Failure("Decode requires at least one digivolution source to play.", "sourceIds", context, target.InstanceId);
            }

            return CardEffectCanResolveResult.Success("Decode can play a digivolution source for free.", BaseValues(context, target));
        }
    }
}

// (P6 cluster2, purely additive — see file header §"AS-IS mirror" note) old-model CardEffectCommons
// Hashtable-based siblings (KeyWordEffects/Decode.cs) — a different namespace/type than the
// KeywordBaseBatch2Effect resolver above.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
    using HeadlessDCGO.Engine.Headless.Choices;
    using HeadlessDCGO.Engine.Headless.Effects;
    using HeadlessDCGO.Engine.Headless.Services;

    public static partial class CardEffectCommons
    {
        private static bool CanSelectDecodeSourceCardCondition(CardSource source, Func<CardSource, bool> sourceCondition, ICardEffect activateClass) =>
            source.IsDigimon && sourceCondition(source) && CanPlayAsNewPermanent(cardSource: source, payCost: false, cardEffect: activateClass);

        /// <summary>(P6 cluster2) AS-IS <c>CanActivateDecode</c> (KeyWordEffects/Decode.cs:16, verbatim).</summary>
        public static bool CanActivateDecode(CardSource cardSource, Func<CardSource, bool> sourceCondition, ICardEffect activateClass) =>
            IsExistOnBattleAreaDigimon(cardSource) &&
            ICardEffect.ResolvePermanentOfThisCard(cardSource).DigivolutionCards.Some(source => CanSelectDecodeSourceCardCondition(source, sourceCondition, activateClass));

        /// <summary>(P6 cluster2) AS-IS <c>DecodeProcess</c> (KeyWordEffects/Decode.cs:27): owner selects 1
        /// matching digivolution card and plays it for free.</summary>
        public static async Task DecodeProcess(CardSource cardSource, Func<CardSource, bool> sourceCondition, string[] decodeStrings, ICardEffect activateClass)
        {
            Permanent permanent = ICardEffect.ResolvePermanentOfThisCard(cardSource);
            var selectedCards = new System.Collections.Generic.List<CardSource>();

            var selectCardEffect = GManager.instance!.GetComponent<SelectCardEffect>();
            selectCardEffect.SetUp(
                canTargetCondition: source => CanSelectDecodeSourceCardCondition(source, sourceCondition, activateClass),
                canTargetCondition_ByPreSelecetedList: null,
                canEndSelectCondition: null,
                canNoSelect: () => true,
                selectCardCoroutine: (CardSource source) => { selectedCards.Add(source); return Task.CompletedTask; },
                afterSelectCardCoroutine: null,
                message: $"Select 1 {decodeStrings[0]} digivolution card to play.",
                maxCount: 1,
                canEndNotMax: false,
                isShowOpponent: true,
                mode: SelectCardEffect.Mode.Custom,
                root: SelectCardEffect.Root.Custom,
                customRootCardList: permanent.DigivolutionCards.ToList(),
                canLookReverseCard: true,
                selectPlayer: cardSource.Owner,
                cardEffect: activateClass);
            selectCardEffect.SetUpCustomMessage(
                $"Select 1 {decodeStrings[0]} digivolution card to play.",
                $"The opponent is selecting 1 {decodeStrings[0]} digivolution card to play.");
            selectCardEffect.SetUpCustomMessage_ShowCard("Played Card");

            await selectCardEffect.Activate().ConfigureAwait(false);

            await PlayPermanentCards(
                cardSources: selectedCards,
                sourceCard: cardSource,
                payCost: false,
                isTapped: false,
                root: ChoiceZone.DigivolutionCards,
                activateETB: true).ConfigureAwait(false);
        }

        /// <summary>(C-Del 3b grant rehousing) AS-IS <c>CardEffectCommons.GainDecode</c>
        /// (KeyWordEffects/Decode.cs:79-114, 1:1). "Target 1 Digimon gains [Decode]": builds the printed-style
        /// <see cref="CardEffectFactory.DecodeEffect"/> ActivateClass (rooted at the granting effect) and stores it in
        /// the target permanent's <c>WhenRemoveField</c> duration bucket via <see cref="AddEffectToPermanent"/> — the
        /// W3 bucket the deletion PRE cut-in window collects (GetSkillInfos). ADAPTATION: <c>IEnumerator</c> -> <c>Task</c>;
        /// the trailing <c>CreateBuffEffect</c> (a Unity presentation coroutine) has no headless substrate — dropped
        /// (same as <see cref="GainRetaliation"/> / <see cref="GainEvade"/>).</summary>
        public static async Task GainDecode(
            Permanent targetPermanent, string[] decodeStrings, Func<CardSource, bool> sourceCondition,
            EffectDuration effectDuration, ICardEffect activateClass)
        {
            if (targetPermanent == null) return;
            if (!IsPermanentExistsOnBattleArea(targetPermanent)) return;
            if (activateClass == null) return;
            if (activateClass.EffectSourceCard == null) return;

            CardSource card = activateClass.EffectSourceCard;

            bool CanUseCondition()
            {
                return IsPermanentExistsOnBattleArea(targetPermanent) &&
                       !targetPermanent.TopCard.CanNotBeAffected(activateClass);
            }

            ActivateClass decode = CardEffectFactory.DecodeEffect(
                targetPermanent: targetPermanent, isInheritedEffect: false, decodeStrings,
                condition: CanUseCondition, sourceCondition: sourceCondition, rootCardEffect: activateClass, card);

            AddEffectToPermanent(
                targetPermanent: targetPermanent,
                effectDuration: effectDuration,
                card: card,
                cardEffect: decode,
                timing: EffectTiming.WhenRemoveField);

            await Task.CompletedTask;
        }
    }
}
