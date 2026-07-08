// 1:1 mirror of the original BT3_018 (BT3/Red).
//   [Security] This Digimon gets Piercing. -> PierceSelfEffect(isInheritedEffect: false) (OnDetermineDoSecurityCheck).
//   [When Digivolving] Trigger <De-Digivolve 2> on 1 of your opponent's Digimon. (Trash up to 2 cards from
//   the top of one of your opponent's Digimon. If it has no digivolution cards, or becomes a level 3
//   Digimon, you can't trash any more cards.)
//   -> SelectAndDeDigivolveEffect (WhenDigivolving, mandatory pick of 1, count 2) — the headless
//   ActivatedSelectAndDeDigivolveEffect wraps DeDigivolveHelpers.DeDigivolveAsync, which mirrors the AS-IS
//   IDegeneration.Degeneration loop verbatim: trash the top, promote the immediate under-source, stop early
//   when the stack has no more sources OR the level-3 floor is reached (DeDigivolveHelpers.LevelFloor).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT3_018 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDetermineDoSecurityCheck)
        {
            cardEffects.Add(CardEffectFactory.PierceSelfEffect(isInheritedEffect: false, card: card, condition: null));
        }

        if (timing == EffectTiming.WhenDigivolving)
        {
            bool CanSelectPermanentCondition(HeadlessEntityId id) =>
                CardEffectCommons.IsOpponentBattleAreaDigimon(card, id);

            cardEffects.Add(CardEffectFactory.SelectAndDeDigivolveEffect(
                card: card,
                canTarget: CanSelectPermanentCondition,
                maxCount: 1,
                count: 2,
                canEndNotMax: false,
                description: "[When Digivolving] Trigger <De-Digivolve 2> on 1 of your opponent's Digimon."));
        }

        return cardEffects;
    }
}
