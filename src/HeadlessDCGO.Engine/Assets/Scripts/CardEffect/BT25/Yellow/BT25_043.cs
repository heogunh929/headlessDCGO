// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Coverage-exemplar card — BT25_043 "Habakirimon/Habakiri" (Digimon+Option / Yellow)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT25/Yellow/BT25_043.cs
//    * None            : AddSelfDigivolutionRequirementStaticEffect([Glowing Dawn] 위 cost 3, level 5).
//    * Shared WD/WA    : ActivateClassesForSharedEffects — <Recovery+1> 후 최다 시큐리티 플레이어 top sec 트래시로
//                        언서스펜드 (TrashSecurityAndProcessAccordingToResult — PRIMARY covered element).
//    * WhenRemoveField : [All Turns] [Glowing Dawn] 디지몬 이탈 시 top sec 트래시로 이탈 방지.
//    * None            : UseRequirements([Glowing Dawn] 색 요구 무시).
//    * OptionSkill     : [Main] 상대 1체 -8K, top sec 트래시로 전체 -5K.
//    * None            : ArtsDigivolveEffect (PRIMARY covered element: ArtsDigivolve).
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`→`await X`.
//    * AS-IS `CardSource.HasGlowingDawnTraits`(미러 미존재) → `EqualsTraits("Glowing Dawn")` 인라인
//      (편의-프로퍼티 인라인 판례, BT25_039/BT9_111). 로컬 헬퍼 HasGlowingDawn로 조립.
//    * `new IRecovery(card.Owner,1,activateClass).Recovery()` → `new IRecovery(card.Context, card.Owner, 1,
//      activateClass.EffectSourceCard?.InstanceId).Recovery()`.
//    * `new IUnsuspendPermanents(list, activateClass).Unsuspend()` → 미러 1:1.
//    * `new IDestroySecurity(player:card.Owner, 1, activateClass, fromTop:true).DestroySecurity()` →
//      `new IDestroySecurity(card.Context, card.Owner, 1, activateClass, true).DestroySecurity()`.
//    * `TrashSecurityAndProcessAccordingToResult(player: card.Owner|card.Owner.Enemy, ...)` — 미러 player 인자는
//      Player 객체 → `new Player(card.Context, card.Owner|OpponentOf(card))`.
//    * `card.Owner.SecurityCards.Count`/`card.Owner.Enemy...` → `new Player(card.Context, card.Owner|OpponentOf)`.
//    * SelectPermanentEffect.canTargetCondition은 정본 Func<Permanent,bool> — Permanent 술어 직결. AS-IS Hide* 4종
//      = UI 연출(스트립, BT25_039 판례);
//      `willBeRemoveField = false`만 실질 상태 변경 유지.
//    * UserSelectionManager.SetIntSelection/SetBoolSelection/WaitForEndSelect/SelectedIntValue/SelectedBoolValue 1:1.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT25.Yellow;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT25_043 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // AS-IS CardSource.HasGlowingDawnTraits (CardSource.cs:4147 => EqualsTraits("Glowing Dawn")) 인라인.
        bool HasGlowingDawn(CardSource cardSource) => cardSource.EqualsTraits("Glowing Dawn");

        #region Digimon Effects
        #region Alt Digivolution
        if (timing == EffectTiming.None)
        {
            bool PermanentCondition(Permanent permanent)
            {
                return HasGlowingDawn(permanent.TopCard);
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(PermanentCondition, 3, false, card, null, level: 5));
        }
        #endregion

        #region Shared WD/WA
        string SharedHashString = "BT25_043_WD_WA";

        string SharedEffectName = "<Recovery +1>, by trashing top sec of player with most sec cards, unsuspend";

        CardEffectFactory.ActivateClassesForSharedEffects
            (ref cardEffects, timing, card,
                SharedEffectName,
                SharedActivateCoroutine,
                SharedEffectDescription,
                optional: false,
                maxCountPerTurn: 1,
                hashValue: SharedHashString,
                whenDigivolving: true,
                whenAttacking: true);

        string SharedEffectDescription(string tag) => $"[{tag}] [Once Per Turn] <Recovery +1>. Then, by trashing the top security card of 1 player with the most security cards, this Digimon unsuspends.";

        async Task SharedActivateCoroutine(Hashtable hashtable, ActivateClass activateClass)
        {
            await new IRecovery(card.Context, card.Owner, 1, activateClass.EffectSourceCard?.InstanceId).Recovery();

            bool validOwnerSecurity = new Player(card.Context, card.Owner).SecurityCards.Count > 0 && new Player(card.Context, card.Owner).SecurityCards.Count >= new Player(card.Context, CardEffectCommons.OpponentOf(card)).SecurityCards.Count;
            bool validEnemySecurity = new Player(card.Context, CardEffectCommons.OpponentOf(card)).SecurityCards.Count > 0 && new Player(card.Context, CardEffectCommons.OpponentOf(card)).SecurityCards.Count >= new Player(card.Context, card.Owner).SecurityCards.Count;

            if (validOwnerSecurity || validEnemySecurity)
            {
                List<SelectionElement<int>> selectionElements = new List<SelectionElement<int>>();
                if (validOwnerSecurity)
                {
                    selectionElements.Add(new(message: $"Trash your own top security card", value: 1, spriteIndex: 0));
                }
                if (validEnemySecurity)
                {
                    selectionElements.Add(new(message: $"Trash your opponent's top security card", value: 2, spriteIndex: 0));
                }
                selectionElements.Add(new(message: $"Don't trash security", value: 3, spriteIndex: 1));

                string selectPlayerMessage = "Will you trash 1 player's security to unsuspend?";
                string notSelectPlayerMessage = "The opponent is choosing if they will trash a security.";

                GManager.instance.userSelectionManager.SetIntSelection(selectionElements: selectionElements, selectPlayer: card.Owner, selectPlayerMessage: selectPlayerMessage, notSelectPlayerMessage: notSelectPlayerMessage);

                await GManager.instance.userSelectionManager.WaitForEndSelect();

                bool doTrash = GManager.instance.userSelectionManager.SelectedIntValue != 3;
                bool ownSecurity = GManager.instance.userSelectionManager.SelectedIntValue == 1;
                if (doTrash)
                {
                    await CardEffectCommons.TrashSecurityAndProcessAccordingToResult(
                        player: ownSecurity ? new Player(card.Context, card.Owner) : new Player(card.Context, CardEffectCommons.OpponentOf(card)),
                        trashAmount: 1,
                        activateClass: activateClass,
                        fromTop: true,
                        successProcess: SuccessProcess,
                        failureProcess: null
                    );

                    async Task SuccessProcess(List<CardSource> cardSources)
                    {
                        await new IUnsuspendPermanents(new List<Permanent>() { ICardEffect.ResolvePermanentOfThisCard(card) }, activateClass).Unsuspend();
                    }
                }
            }
        }
        #endregion

        #region All Turns
        if (timing == EffectTiming.WhenRemoveField)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Trash your top sec to prevent them from leaving", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, true, EffectDescription());
            activateClass.SetHashString("BT25_043_AT");
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return
                    "[All Turns][Once Per Turn] When any of your [Glowing Dawn] trait Digimon would leave the battle area, by trashing your top security card, they don't leave.";
            }

            bool PermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card)
                    && HasGlowingDawn(permanent.TopCard);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card)
                    && CardEffectCommons.CanTriggerWhenPermanentRemoveField(hashtable, PermanentCondition);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card)
                    && new Player(card.Context, card.Owner).SecurityCards.Count >= 1;
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                List<Permanent> removedPermanents = CardEffectCommons.GetPermanentsFromHashtable(hashtable);

                removedPermanents = removedPermanents.Filter(PermanentCondition);
                await new IDestroySecurity(
                        card.Context,
                        card.Owner,
                        1,
                        activateClass,
                        fromTop: true).DestroySecurity();

                foreach (Permanent permanent in removedPermanents)
                {
                    permanent.willBeRemoveField = false;
                    // AS-IS HideDeleteEffect/HideHandBounceEffect/HideDeckBounceEffect/HideWillRemoveFieldEffect
                    // = UI 연출(스트립, BT25_039/Barrier.cs 판례).
                }
            }
        }
        #endregion
        #endregion

        #region Option Effects
        #region Ignore Colour Requirement
        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.UseRequirements(card, CardCondition));

            bool CardCondition(CardSource cardSource)
            {
                return HasGlowingDawn(cardSource);
            }
        }
        #endregion

        #region Main
        if (timing == EffectTiming.OptionSkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("1 enemy Digimon gets -8K DP for turn, then by trashing your top sec, all enemy digimon get -5K DP for turn", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription()
                => "[Main] 1 of your opponent's Digimon gets -8000 DP for the turn. Then, by trashing your top security card, all of your opponent's Digimon get -5000 DP for the turn.";

            bool CanUseCondition(Hashtable hashtable)
                => CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                #region DP minus single target
                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                {
                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanentCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: 1,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: SelectPermanentCoroutine,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Custom,
                        cardEffect: activateClass);

                    selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to -8K DP", "The opponent is selecting 1 Digimon to -8K DP");
                    await selectPermanentEffect.Activate();

                    async Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        if (permanent != null) await CardEffectCommons.ChangeDigimonDP(permanent, -8000, EffectDuration.UntilEachTurnEnd, activateClass);
                    }
                }
                #endregion

                #region DP minus enemy board
                if (new Player(card.Context, card.Owner).SecurityCards.Count >= 1)
                {
                    List<SelectionElement<bool>> selectionElements = new List<SelectionElement<bool>>()
                    {
                        new SelectionElement<bool>(message: $"Yes", value : true, spriteIndex: 0),
                        new SelectionElement<bool>(message: $"No", value : false, spriteIndex: 1),
                    };

                    string selectPlayerMessage = "Will you trash your top security card to give all enemy Digimon -5K DP for turn?";
                    string notSelectPlayerMessage = "The opponent is choosing whether or not to trash their top security card to give all your Digimon -5K DP for turn.";

                    GManager.instance.userSelectionManager.SetBoolSelection(selectionElements: selectionElements, selectPlayer: card.Owner, selectPlayerMessage: selectPlayerMessage, notSelectPlayerMessage: notSelectPlayerMessage);

                    await GManager.instance.userSelectionManager.WaitForEndSelect();

                    bool trash = GManager.instance.userSelectionManager.SelectedBoolValue;

                    if (trash)
                    {
                        await new IDestroySecurity(
                            card.Context,
                            card.Owner,
                            1,
                            activateClass,
                            fromTop: true).DestroySecurity();

                        await CardEffectCommons.ChangeDigimonDPPlayerEffect(
                            permanentCondition: CanSelectPermanentCondition,
                            changeValue: -5000,
                            effectDuration: EffectDuration.UntilEachTurnEnd,
                            activateClass: activateClass);
                    }
                }
                #endregion
            }
        }
        #endregion

        #region Arts Digivolution
        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.ArtsDigivolveEffect(card));
        }
        #endregion
        #endregion

        return cardEffects;
    }
}
