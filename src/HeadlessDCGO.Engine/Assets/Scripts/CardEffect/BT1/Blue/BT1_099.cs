// Source: Assets/Scripts/CardEffect/BT1/Blue/BT1_099.cs
// 1:1 headless mirror (Option).
//   [Main] Trash all digivolution cards under 1 of your opponent's Digimon.
//     -> SelectAndTrashDigivolutionEffect (OptionSkill, canTarget = IsOpponentBattleAreaDigimon (no further
//        narrowing, matching AS-IS CanSelectPermanentCondition which does NOT require any trashable
//        digivolution cards — unlike BT1_043's variant), maxCount 1, isFromTop:true -> fromBottom:false).
//        AS-IS calls TrashDigivolutionCardsFromTopOrBottom(trashCount: selectedPermanent.DigivolutionCards.Count,
//        isFromTop:true) i.e. "trash them ALL"; the headless factory takes a fixed int trashCount (no
//        Func<int> variant), but the underlying trash mechanic (DigivolutionStackHelpers.RemoveSourcesAsync)
//        clamps via Math.Min(count, sources.Count), so passing int.MaxValue is behaviorally identical to
//        "trash every digivolution card under the selected permanent" (not an approximation — the clamp makes
//        it exact for any stack size).
//   No [Security] branch — AS-IS defines only EffectTiming.OptionSkill for this card.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_099 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            bool CanTarget(HeadlessEntityId id) => CardEffectCommons.IsOpponentBattleAreaDigimon(card, id);

            cardEffects.Add(CardEffectFactory.SelectAndTrashDigivolutionEffect(
                card: card,
                canTarget: CanTarget,
                maxCount: 1,
                trashCount: int.MaxValue,
                fromBottom: false,
                description: "[Main] Trash all digivolution cards under 1 of your opponent's Digimon."));
        }

        return cardEffects;
    }
}
