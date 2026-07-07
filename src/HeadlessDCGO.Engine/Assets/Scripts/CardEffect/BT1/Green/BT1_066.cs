// Source: Assets/Scripts/CardEffect/BT1/Green/BT1_066.cs
// 1:1 headless mirror via the uniform ActivatedEffect (= AS-IS ActivateClass): a [When Attacking]
// select-and-suspend targeting an opponent's low-DP Digimon.
//   [When Attacking] Suspend 1 of your opponent's Digimon with 3000 DP or less.
//   -> ActivatedEffect(OnAllyAttack, CanUse=CanTriggerOnAttack [self-scope],
//      CanActivate=on battle area && a matching opponent Digimon (<=3000 DP) exists,
//      body=SelectBody(maxCount=1, canNoSelect=false, canEndNotMax=false, Mode.Tap) [AS-IS
//      SelectPermanentEffect.SetUp(.., canNoSelect:false, canEndNotMax:false, mode:Tap)],
//      maxCountPerTurn=null [AS-IS ORDER=-1], isOptional=false [AS-IS ISOPTIONAL=false]).
// NOTE: AS-IS computes maxCount = Math.Min(1, MatchConditionPermanentCount(..)); the headless factory
// takes the fixed cap (1) directly — SelectPermanentEffect.BuildRequest clamps to the live candidate
// count (same simplification as ST4_15 / ST1_15 siblings).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;
using SelectPermanentEffect = HeadlessDCGO.Engine.Assets.Scripts.Script.SelectPermanentEffect;

public sealed class BT1_066 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                if (CardEffectCommons.IsOpponentBattleAreaDigimon(card, id))
                {
                    if (CardEffectCommons.CurrentDp(card, id) <= 3000)
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanActivate()
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                    {
                        return true;
                    }
                }

                return false;
            }

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnAllyAttack,
                canUse: ctx => CardEffectCommons.CanTriggerOnAttack(ctx, card),
                canActivate: CanActivate,
                body: new SelectBody(
                    card: card,
                    canTarget: CanSelectPermanentCondition,
                    maxCount: 1,
                    canNoSelect: false,
                    canEndNotMax: false,
                    mode: SelectPermanentEffect.Mode.Tap,
                    description: "Suspend 1 Digimon"),
                maxCountPerTurn: null,
                isOptional: false,
                description: "[When Attacking] Suspend 1 of your opponent's Digimon with 3000 DP or less."));
        }

        return cardEffects;
    }
}
