// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// EXEMPLAR-T2B 정본 카드 — Medusamon (BT21_029, Digimon / Red / Lv.5)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT21/Red/BT21_029.cs (259 lines, 6 regions)
//    * [None] Progress        :16-23  (ProgressSelfStaticEffect — CanNotAffected 상시)
//    * [None] Sec +1          :25-36  (ChangeSelfSAttackStaticEffect changeValue:1)
//    * shared WD/EoA delete   :40-75  (SelectPermanentEffect Mode.Destroy — 상대 최저DP 삭제)
//    * [When Digivolving]     :77-112 (OnEnterFieldAnyone + CanTriggerWhenDigivolving — 공유 delete)
//    * [End of Attack]        :114-149(OnEndAttack + CanTriggerOnEndAttack — 공유 delete)
//    * [All Turns] delete→tok :151-254(OnDestroyedAnyone·OnLoseSecurity — 공유 PlayPetrificationToken)
//
// ② 프리미티브 매핑 (감사 축 이름 — coverage_exemplar_audit_2026-07-18.md §4 #25):
//    * K:Progress             — [None] ProgressSelfStaticEffect (AS-IS :20; CardEffectFactory KeyWord)
//    * P:PlayPetrificationToken — [All Turns] 공유 몸통 (AS-IS :155; TokenSpecs["Petrification"], 상대 보드)
//    * (+P:ChangeSAttackClass(=ChangeSelfSAttack) (AS-IS :29-33), IsMinDP,
//       E:SelectPermanentEffect Mode.Destroy, T:OnEndAttack/OnDestroyedAnyone/OnLoseSecurity,
//       CanTriggerWhenLoseSecurity)
//
// ③ 배선 관례 근거 (trigger-wiring-porting-rules):
//    * [When Digivolving] → EffectTiming.WhenDigivolving 전용 키(미러 방언 rule 3; AS-IS OnEnterFieldAnyone +
//      CanTriggerWhenDigivolving 게이트). CanUseCondition의 CanTriggerWhenDigivolving는 AS-IS 그대로.
//    * [End of Attack] self-스코프 → OnEndAttack + CanTriggerOnEndAttack(AS-IS :127-136).
//    * [All Turns] 상대-삭제 트리거 → OnDestroyedAnyone + CanTriggerOnPermanentDeleted(PermanentCondition);
//      상대-시큐리티 감소 트리거 → OnLoseSecurity + CanTriggerWhenLoseSecurity(PlayerCondition).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, StartCoroutine(X)→await X (BT8_092 idiom).
//    * `card.Owner.Enemy` (Player) → `CardEffectCommons.OpponentOf(card)` (HeadlessPlayerId; BT24_018 idiom).
//      IsMinDP는 미러에서 HeadlessPlayerId owner를 받음 → OpponentOf(card) 전달.
//    * `player == card.Owner.Enemy` → `player.PlayerId == CardEffectCommons.OpponentOf(card)` (BT24_018 idiom).
//    * SelectPermanentEffect.canTargetCondition는 id-형 — 공유 Permanent-술어에 PermanentOf(id) 어댑터 덧댐.
//    * `MatchConditionPermanentCount(cond)` → `MatchConditionPermanentCount(card, cond)` (미러 card-arg).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT21.Red;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT21_029 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Static Effects

        #region Progress
        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.ProgressSelfStaticEffect(false, card, null));
        }
        #endregion

        #region Sec +1
        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.ChangeSelfSAttackStaticEffect(
                changeValue: 1,
                isInheritedEffect: false,
                card: card,
                condition: null));
        }
        #endregion

        #endregion

        #region When Digivolving/End of Attack Shared

        bool CanSelectPermanentCondition(Permanent permanent)
        {
            if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
            {
                if (CardEffectCommons.IsMinDP(permanent, CardEffectCommons.OpponentOf(card)))
                {
                    return true;
                }
            }
            return false;
        }

        // (BT17_026 관례) SelectPermanentEffect canTargetCondition는 id-형 — Permanent-술어에 id 어댑터를 덧댐.
        Permanent? PermanentOf(HeadlessEntityId id) =>
            card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
                ? new Permanent(card.Context, id, rec.OwnerId)
                : null;

        bool CanSelectPermanentById(HeadlessEntityId id)
        {
            Permanent? permanent = PermanentOf(id);
            return permanent is not null && CanSelectPermanentCondition(permanent);
        }

        async Task WDAndEoAActivateCoroutine(Hashtable hashtable, ActivateClass activateClass)
        {
            int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition));
            SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

            selectPermanentEffect.SetUp(
                selectPlayer: card.Owner,
                canTargetCondition: CanSelectPermanentById,
                canTargetCondition_ByPreSelecetedList: null,
                canEndSelectCondition: null,
                maxCount: maxCount,
                canNoSelect: false,
                canEndNotMax: false,
                selectPermanentCoroutine: null,
                afterSelectPermanentCoroutine: null,
                mode: SelectPermanentEffect.Mode.Destroy,
                cardEffect: activateClass);

            await selectPermanentEffect.Activate();
        }

        #endregion

        #region When Digivolving
        // ③ 미러 방언: AS-IS OnEnterFieldAnyone(:79) + CanTriggerWhenDigivolving → WhenDigivolving 전용 키.
        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Delete lowest DP Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, hashtable => WDAndEoAActivateCoroutine(hashtable, activateClass), 1, true, EffectDiscription());
            activateClass.SetHashString("Delete_BT21_029");
            cardEffects.Add(activateClass);

            string EffectDiscription()
                => "[When Digivolving] [Once Per Turn] You may delete 1 of your opponent's lowest DP Digimon.";

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                {
                    if (CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card))
                    {
                        return true;
                    }
                }
                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                {
                    return true;
                }
                return false;
            }
        }
        #endregion

        #region End of Attack
        if (timing == EffectTiming.OnEndAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Delete lowest DP Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, hashtable => WDAndEoAActivateCoroutine(hashtable, activateClass), 1, true, EffectDiscription());
            activateClass.SetHashString("Delete_BT21_029");
            cardEffects.Add(activateClass);

            string EffectDiscription()
                => "[End of Attack] [Once Per Turn] You may delete 1 of your opponent's lowest DP Digimon.";

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                {
                    if (CardEffectCommons.CanTriggerOnEndAttack(hashtable, card))
                    {
                        return true;
                    }
                }
                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                {
                    return true;
                }
                return false;
            }
        }
        #endregion

        #region All Turns Shared

        async Task AllTurnsSharedActivateCoroutine(Hashtable hashtable, ActivateClass activateClass)
        {
            await CardEffectCommons.PlayPetrificationToken(activateClass);
        }

        #endregion

        #region All Turns - When Opponent Digimon Deleted
        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play 1 [Petrification Token]", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, hashtable => AllTurnsSharedActivateCoroutine(hashtable, activateClass), 1, false, EffectDiscription());
            activateClass.SetHashString("PlayToken_BT21_029");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[All Turns] [Once Per Turn] When any of your opponent's Digimon are deleted, they play 1 [Petrification] Token";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                {
                    if (CardEffectCommons.CanTriggerOnPermanentDeleted(hashtable, PermanentCondition))
                    {
                        return true;
                    }
                }
                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                {
                    return true;
                }
                return false;
            }

            bool PermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
                {
                    return true;
                }
                return false;
            }
        }
        #endregion

        #region All Turns - When Opponent Security Stack Removed
        if (timing == EffectTiming.OnLoseSecurity)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play 1 [Petrification Token]", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, hashtable => AllTurnsSharedActivateCoroutine(hashtable, activateClass), 1, false, EffectDiscription());
            activateClass.SetHashString("PlayToken_BT21_029");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[All Turns] [Once Per Turn] When opponents security stack is removed from, they play 1 [Petrification] Token";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                {
                    if (CardEffectCommons.CanTriggerWhenLoseSecurity(hashtable, PlayerCondition))
                    {
                        return true;
                    }
                }
                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                {
                    return true;
                }
                return false;
            }

            bool PlayerCondition(Player player)
            {
                if (player.PlayerId == CardEffectCommons.OpponentOf(card))
                {
                    return true;
                }
                return false;
            }
        }
        #endregion

        return cardEffects;
    }
}
