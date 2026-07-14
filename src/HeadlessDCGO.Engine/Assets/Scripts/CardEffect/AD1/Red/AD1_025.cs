// 1:1 mirror of the original AD1_025 (AD1/Red) — Omnimon (DNA Lv.7).
//
// Ported effects (AS-IS AD1_025.cs):
//   * DNA Digivolution ([Lv.6 w/[Greymon] in name] + [Lv.6 w/[Garurumon] in name]) — timing None ->
//     GetJogressConditionClass with the two Permanent predicates (name fragment + jogress-level-6). The
//     primitive scopes each material slot to the owner's battle-area Digimon (AS-IS AddJogressConditionClass).
//   * <Raid> — timing OnAllyAttack -> RaidSelfEffect.
//   * <Blocker> — timing None -> BlockerSelfStaticEffect.
//   * <Partition> ([WarGreymon]/[MetalGarurumon]) — timing WhenRemoveField -> PartitionSelfEffect with the two
//     NAME-based PartitionCondition groups.
//   * [All Turns] [Once Per Turn] "When any of your opponent's Digimon leave the battle area, trash 1 of their
//     Option cards in the battle area and trash their top security card." — timing OnLeaveFieldAnyone ->
//     P8 CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass), AS-IS ActivateCoroutine
//     (AD1_025.cs:181-210) mirrored 1:1: SetHashString("AD1-025_AT"), ORDER=1 ([Once Per Turn]), ISOPTIONAL=false.
//     CanUseCondition = IsExistOnBattleArea && CanTriggerOnPermanentLeave(hashtable, IsOpponentsDigimon);
//     CanActivateCondition = IsExistOnBattleArea; ActivateCoroutine = (guarded by HasMatchConditionOpponentsPermanent)
//     GManager SelectPermanentEffect(Mode.Destroy, maxCount 1) over enemy Options THEN IDestroySecurity(fromTop) on
//     the opponent's security. Substrate: IEnumerator->Task, StartCoroutine->await; AS-IS `Func<Permanent,bool>`
//     IsEnemyOptionPermanent (`IsPermanentExistsOnOpponentBattleArea && IsOption`) -> the entity-id predicate
//     `CardEffectCommons.IsOpponentBattleAreaOption(card, id)` (used for both HasMatchConditionOpponentsPermanent
//     and the SelectPermanentEffect canTargetCondition); `new IDestroySecurity(player: card.Owner.Enemy, 1,
//     activateClass, fromTop:true)` -> the MIG3-3a mirror carrier `(EngineContext, HeadlessPlayerId, int,
//     HeadlessEntityId? cause, bool fromTop)` (player = `CardEffectCommons.OpponentOf(card)`, cause =
//     `activateClass.EffectSourceCard?.InstanceId`). OnLeaveFieldAnyone is an EventBroadcast bridge timing; the
//     N-simultaneous-leaves batch collapses to ONE reactor fire at collect (WindowResolverWiring.
//     CollectActivatedBridgeTriggers), mirroring the AS-IS single-StackSkillInfos any-match.
//   * <Assembly> ([WarGreymon] + [MetalGarurumon], -6 cost) — timing None -> AddAssemblyConditionClass.
//
// FIDELITY DEBT (design item D2w-25) — the [On Play]/[When Digivolving] shared effect (AS-IS AD1_025.cs:88-150,
// SharedActivateCoroutine) is NOT yet ported: "Return all of your opponent's Digimon with as many or fewer
// digivolution cards as this Digimon to the bottom of the deck. Then, delete 1 of your opponent's Digimon."
// This composes a MASS deck-bottom-bounce (non-interactive) FOLLOWED BY a select-destroy whose candidate pool
// must be computed AFTER the bounce commits (the remaining, higher-source enemy Digimon). The uniform-body
// activation model builds its single ChoiceRequest BEFORE ApplyAsync, so a composite body would offer the
// select over the STALE pre-bounce board (including the about-to-be-bounced Digimon) — an infidelity. A faithful
// port needs a multi-step activated resolver seam (commit the bounce, THEN enumerate select candidates), which
// no ported primitive exposes today. GAP = mass-mutation-then-select-over-post-mutation-board activation.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.AD1.Red;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;
using PartitionCondition = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory.KeyWordEffects.PartitionCondition;

public sealed class AD1_025 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        // AS-IS AD1_025.cs:70 — shared "opponent's Digimon" predicate.
        bool IsOpponentsDigimon(Permanent permanent) =>
            CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);

        #region DNA Condition (timing None)
        if (timing == EffectTiming.None)
        {
            bool PermanentCondition1(Permanent permanent) =>
                permanent.TopCard.ContainsCardName("Greymon")
                && permanent.TopCard.JogressLevelsAgainst(card).Contains(6);

            bool PermanentCondition2(Permanent permanent) =>
                permanent.TopCard.ContainsCardName("Garurumon")
                && permanent.TopCard.JogressLevelsAgainst(card).Contains(6);

            cardEffects.Add(CardEffectFactory.GetJogressConditionClass(
                PermanentCondition1, "Lv.6 w/[Greymon] in name",
                PermanentCondition2, "Lv.6 w/[Garurumon] in name",
                card));
        }
        #endregion

        #region Raid (timing OnAllyAttack)
        if (timing == EffectTiming.OnAllyAttack)
        {
            cardEffects.Add(CardEffectFactory.RaidSelfEffect(isInheritedEffect: false, card: card, condition: null));
        }
        #endregion

        #region Blocker (timing None)
        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.BlockerSelfStaticEffect(isInheritedEffect: false, card: card, condition: null));
        }
        #endregion

        #region Partition (timing WhenRemoveField)
        if (timing == EffectTiming.WhenRemoveField)
        {
            var partitionConditions = new List<PartitionCondition>
            {
                new PartitionCondition("WarGreymon"),
                new PartitionCondition("MetalGarurumon"),
            };

            cardEffects.Add(CardEffectFactory.PartitionSelfEffect(
                isInheritedEffect: false, card: card, condition: null, cardSourceConditions: partitionConditions));
        }
        #endregion

        #region All Turns [Once Per Turn] (timing OnLeaveFieldAnyone)
        if (timing == EffectTiming.OnLeaveFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Trash 1 enemy option card in the battle area and 1 card from their security.", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDescription());
            activateClass.SetHashString("AD1-025_AT");
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return "[All Turns] [Once Per Turn] When any of your opponent's Digimon leave the battle area, trash 1 of their Option cards in the battle area and trash their top security card.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.CanTriggerOnPermanentLeave(hashtable, IsOpponentsDigimon);
            }

            bool IsEnemyOptionPermanent(HeadlessEntityId id)
            {
                return CardEffectCommons.IsOpponentBattleAreaOption(card, id);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.HasMatchConditionOpponentsPermanent(card, IsEnemyOptionPermanent))
                {
                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: IsEnemyOptionPermanent,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: 1,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: null,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Destroy,
                        cardEffect: activateClass);

                    selectPermanentEffect.SetUpCustomMessage("Select option to trash.", "The opponent is selecting 1 option to trash.");

                    await selectPermanentEffect.Activate();
                }

                await new IDestroySecurity(
                    card.Context,
                    CardEffectCommons.OpponentOf(card),
                    1,
                    activateClass,
                    fromTop: true).DestroySecurity();
            }
        }
        #endregion

        #region Assembly (timing None)
        if (timing == EffectTiming.None)
        {
            var addAssemblyConditionClass = new AddAssemblyConditionClass();
            addAssemblyConditionClass.SetUpICardEffect("Assembly", CanUseCondition, card);
            addAssemblyConditionClass.SetUpAddAssemblyConditionClass(GetAssembly);
            addAssemblyConditionClass.SetNotShowUI(true);
            cardEffects.Add(addAssemblyConditionClass);

            // AS-IS AD1_025.cs:223-226 — unconditional true (Hashtable-shaped CanUse gate).
            bool CanUseCondition(Hashtable hashtable)
            {
                return true;
            }

            AssemblyCondition? GetAssembly(CardSource cardSource)
            {
                if (cardSource != card)
                {
                    return null;
                }

                bool CanSelectCardCondition1(CardSource cs) =>
                    cs != null && cs.Owner == card.Owner && cs.IsDigimon && cs.EqualsCardName("WarGreymon");

                bool CanSelectCardCondition2(CardSource cs) =>
                    cs != null && cs.Owner == card.Owner && cs.IsDigimon && cs.EqualsCardName("MetalGarurumon");

                var element1 = new AssemblyConditionElement(CanSelectCardCondition1, selectMessage: "[WarGreymon]", elementCount: 1);
                var element2 = new AssemblyConditionElement(CanSelectCardCondition2, selectMessage: "[MetalGarurumon]", elementCount: 1);

                return new AssemblyCondition(
                    elements: new List<AssemblyConditionElement> { element1, element2 },
                    reduceCost: 6);
            }
        }
        #endregion

        return cardEffects;
    }
}
