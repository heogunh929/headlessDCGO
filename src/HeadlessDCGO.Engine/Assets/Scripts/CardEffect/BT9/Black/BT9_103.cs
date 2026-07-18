// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// PILOT-S1 카드 — BT9_103 (Option / Black)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT9/Black/BT9_103.cs (95 lines, no region markers, 2 timing 블록)
//    * [Main]     :16-86 (timing == OptionSkill — GainCanNotAttackPlayerEffect + CannotAddSecurityClass)
//    * [Security] :88-91 (timing == SecuritySkill — AddActivateMainOptionSecurityEffect)
//
// ② 프리미티브 매핑:
//    * P:GiveEffectToPlayer(GainCanNotAttackPlayerEffect) — 코스트 7 이하 상대 디지몬 플레이어 공격 불가
//      (AS-IS :35-61)
//    * P:CannotAddSecurityClass — 상대 효과로 시큐리티 추가 불가, UntilOpponentTurnEndEffects 등재 (AS-IS :64-83)
//    * P:ReuseMainOptionEffect(AddActivateMainOptionSecurityEffect) — [Security] (AS-IS :90)
//
// ③ 배선 관례 근거:
//    * [Main] → OptionSkill + CanTriggerOptionMainEffect(hashtable, card) 게이트(AS-IS :29 그대로).
//    * [Security] → SecuritySkill + AddActivateMainOptionSecurityEffect(SecurityResolver가 해소).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`→`await X`
//      — GainCanNotAttackPlayerEffect는 AS-IS 시그니처 그대로인 Task 오버로드(CanNotAttack.cs bridge,
//      activateClass 인자)가 존재해 그대로 `await` 사용.
//    * `card.Owner.UntilOpponentTurnEndEffects.Add(...)` → `new Player(card.Context, card.Owner)
//      .UntilOpponentTurnEndEffects.Add(...)` (미러 Player 접근 idiom).
//    * `CardEffectCommons.IsOpponentEffect(cardEffect, card)`(AS-IS는 ICardEffect 인자) → 미러 오버로드는
//      CardSource? 인자(effect의 EffectSourceCard) — `CardEffectCommons.IsOpponentEffect(cardEffect?.EffectSourceCard, card)`.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT9.Black;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;

public sealed class BT9_103 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(card.BaseENGCardNameFromEntity, CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Main] Until the end of your opponent's turn, your opponent's Digimon with play costs of 7 or less can't attack players, and cards can't be added to security stacks by your opponent's effects.";
            }
            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                {
                    bool AttackerCondition(Permanent Attacker)
                    {
                        if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(Attacker, card))
                        {
                            if (Attacker.TopCard.GetCostItself <= 7)
                            {
                                if (Attacker.TopCard.HasPlayCost)
                                {
                                    return true;
                                }
                            }
                        }

                        return false;
                    }

                    bool DefenderCondition(Permanent Defender)
                    {
                        return Defender == null;
                    }

                    await CardEffectCommons.GainCanNotAttackPlayerEffect(
                        attackerCondition: AttackerCondition,
                        defenderCondition: DefenderCondition,
                        effectDuration: EffectDuration.UntilOpponentTurnEnd,
                        activateClass: activateClass,
                        effectName: "Can't Attack to player");
                }

                {
                    CannotAddSecurityClass cannotAddSecurityClass = new CannotAddSecurityClass();
                    cannotAddSecurityClass.SetUpICardEffect("Can't Add Security", CanUseCondition1, card);
                    cannotAddSecurityClass.SetUpCannotAddSecurityClass(PlayerCondition: PlayerCondition, CardEffectCondition: CardEffectCondition);
                    new Player(card.Context, card.Owner).UntilOpponentTurnEndEffects.Add((_timing) => cannotAddSecurityClass);

                    bool CanUseCondition1(Hashtable hashtable)
                    {
                        return true;
                    }

                    bool PlayerCondition(Player player)
                    {
                        return true;
                    }

                    bool CardEffectCondition(ICardEffect cardEffect)
                    {
                        return CardEffectCommons.IsOpponentEffect(cardEffect?.EffectSourceCard, card);
                    }
                }
            }
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            CardEffectCommons.AddActivateMainOptionSecurityEffect(card: card, cardEffects: ref cardEffects, effectName: $"Opponent's Digimon can't attack and players can't add security");
        }

        return cardEffects;
    }
}
