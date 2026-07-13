// Source: Assets/Scripts/CardEffect/BT2/BT2_062.cs
// Decision: STOP
// STOP: ChangeDigivolutionCostStaticEffect 헤드리스 시그니처에 cardCondition(Diaboromon·Digimon·in-hand 한정)
//       및 rootCondition(Hand 한정) 인자가 없어, AS-IS의 "Diaboromon 카드에 한해 진화 코스트 -1" 조건을
//       충실히 포팅할 수 없음. 해당 인자 없이 등록하면 모든 카드의 진화 코스트가 감소하므로 억지 매핑 금지.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Purple;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT2_062 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // STOP: ChangeDigivolutionCostStaticEffect에 cardCondition(Diaboromon Digimon in-hand 한정) 및
        //       rootCondition(Hand) 인자가 없어 None 타이밍 진화 코스트 -1 효과를 포팅할 수 없음

        return cardEffects;
    }
}