// Source: Assets/Scripts/CardEffect/BT1/Blue/BT1_036.cs
// 1:1 headless mirror.
//   [On Play] Unsuspend 1 of your Digimon. -> SelectAndUnsuspendEffect (OnEnterFieldAnyone, canTarget = owner's
//                                             battle-area Digimon, maxCount 1). AS-IS Min(1, matchCount) -> engine
//                                             clamps the choice to what exists, so maxCount 1 suffices.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_036 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            bool CanTarget(HeadlessEntityId id) => CardEffectCommons.IsOwnerBattleAreaDigimon(card, id);

            cardEffects.Add(CardEffectFactory.SelectAndUnsuspendEffect(
                card: card,
                canTarget: CanTarget,
                maxCount: 1,
                canEndNotMax: false,
                description: "[On Play] Unsuspend 1 of your Digimon."));
        }

        return cardEffects;
    }
}
