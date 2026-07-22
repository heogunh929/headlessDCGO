// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Coverage-exemplar card — BT16_033 (Digimon / Yellow)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT16/Yellow/BT16_033.cs (3 regions)
//    * timing==WhenPermanentWouldBeDeleted: ArmorPurgeEffect (PRIMARY covered element: ArmorPurge).
//    * timing==None                       : AddSelfDigivolutionRequirementStaticEffect ([Hawkmon] 위 cost 2).
//    * timing==OnSecurityCheck            : [Your Turn] 상대 시큐리티 체크 시 — sec≥3이면 메모리+1, sec≤2면 <Recovery+1>
//      (PRIMARY covered element: OnSecurityCheck timing). CanUseCondition은 AS-IS 그대로 CanTriggerOnAttack 게이트.
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return StartCoroutine(X)`→`await X`.
//    * `card.Owner.SecurityCards/LibraryCards.Count` → `new Player(card.Context, card.Owner).*`.
//    * `card.Owner.CanAddSecurity(activateClass)` → `new Player(card.Context, card.Owner)
//      .CanAddSecurity(activateClass.EffectSourceCard?.InstanceId)` (Player.CanAddSecurity 시그니처).
//    * `card.Owner.CanAddMemory(activateClass)` / `card.Owner.AddMemory(1, activateClass)` → HeadlessPlayerId W4
//      확장(PlayerIdAsIsExtensions).
//    * `new IRecovery(card.Owner, 1, activateClass).Recovery()` → `new IRecovery(card.Context, card.Owner, 1,
//      activateClass.EffectSourceCard?.InstanceId).Recovery()` (미러 ctor).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT16.Yellow;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT16_033 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Armor Purge

        if (timing == EffectTiming.WhenPermanentWouldBeDeleted)
        {
            cardEffects.Add(CardEffectFactory.ArmorPurgeEffect(card: card));
        }

        #endregion

        #region Digivolution Condition

        if (timing == EffectTiming.None)
        {
            static bool PermanentCondition(Permanent targetPermanent)
            {
                if (targetPermanent.TopCard.CardNames.Contains("Hawkmon"))
                {
                    return true;
                }
                return false;
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(permanentCondition: PermanentCondition, digivolutionCost: 2, ignoreDigivolutionRequirement: false, card: card, condition: null));
        }

        #endregion

        #region Your Turn

        if (timing == EffectTiming.OnSecurityCheck)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Gain memory or recover 1.", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Your Turn] When this Digimon checks an opponent's security, if you have 3 or more security cards, gain 1 memory. If you have 2 or fewer security cards, [Recovery +1].";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.CanTriggerOnAttack(hashtable, card))
                    {
                        if (CardEffectCommons.IsOwnerTurn(card))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (new Player(card.Context, card.Owner).SecurityCards.Count <= 2)
                    {
                        if (new Player(card.Context, card.Owner).LibraryCards.Count >= 1)
                        {
                            if (new Player(card.Context, card.Owner).CanAddSecurity(activateClass.EffectSourceCard?.InstanceId))
                            {
                                return true;
                            }
                        }
                    }

                    if (new Player(card.Context, card.Owner).SecurityCards.Count >= 3)
                    {
                        if (card.Owner.CanAddMemory(activateClass))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                if (new Player(card.Context, card.Owner).SecurityCards.Count <= 2)
                {
                    await new IRecovery(card.Context, card.Owner, 1, activateClass.EffectSourceCard?.InstanceId).Recovery();
                }

                if (new Player(card.Context, card.Owner).SecurityCards.Count >= 3)
                {
                    await card.Owner.AddMemory(1, activateClass);
                }
            }
        }

        #endregion

        return cardEffects;
    }
}
