// TEST FIXTURE (not a real card). [Main] (OptionSkill) computes the list of the opponent's battle-area
// Digimon and DIRECTLY deletes them — mirrors the original DestroyPermanentsClass (a pre-computed target
// list). Used by tests/BT-PRE-A3 (G9-017). Inert in actual play (no real card numbered "TfxDestroy").

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class TfxDestroy : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill && card.Context.ZoneMover is IZoneStateReader zones)
        {
            var targets = new List<HeadlessEntityId>();
            foreach (HeadlessPlayerId player in card.Context.TurnController.Current.PlayerOrder)
            {
                foreach (HeadlessEntityId id in zones.GetCards(player, ChoiceZone.BattleArea))
                {
                    if (CardEffectCommons.IsOpponentBattleAreaDigimon(card, id))
                    {
                        targets.Add(id);
                    }
                }
            }

            cardEffects.Add(new DestroyPermanentsEffect(card, targets, "Delete all of your opponent's Digimon."));
        }

        return cardEffects;
    }
}
