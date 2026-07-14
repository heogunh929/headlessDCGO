// Source: DCGO/Assets/Scripts/CardEffect/BT1/Blue/BT1_101.cs — an Option.
// P8 CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass). 1:1 mirror of the AS-IS BT1_101.
//   [Main] (OptionSkill) "Trash all of the digivolution cards under all of your opponent's Digimon."
//     ActivateCoroutine: a plain `foreach (Permanent p in card.Owner.Enemy.GetBattleAreaDigimons())` — every
//     opponent battle-area Digimon, unconditionally, NO CanSelectPermanentCondition / NO player selection —
//     then per host TrashDigivolutionCardsFromTopOrBottom(trashCount: p.DigivolutionCards.Count, isFromTop:
//     true).
//   [Security] (use the Main effect) -> AddActivateMainOptionSecurityEffect (ReuseMainOptionEffect re-resolves
//     CardEffects(OptionSkill)); now that [Main] is registered it reuses it faithfully.
// AS-IS structure kept verbatim: inline `new ActivateClass()` + the literal foreach loop (no select body).
// Substrate translations only: IEnumerator->Task, StartCoroutine->await; `card.Owner.Enemy.
// GetBattleAreaDigimons()` (AS-IS live `Player.Enemy`) -> `new Player(card.Context, card.Owner).Enemy` (mirror
// `CardSource.Owner` is a bare `HeadlessPlayerId`, so the Player handle is reconstructed — the established
// BT1_104/BT2_023 idiom), null-conditional since `Player.Enemy` is nullable on the mirror.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Blue;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT1_101 : CEntity_Effect
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
                return "[Main] Trash all digivolution cards under all of your opponent's Digimon.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                foreach (Permanent selectedPermanent in new Player(card.Context, card.Owner).Enemy?.GetBattleAreaDigimons() ?? new List<Permanent>())
                {
                    await CardEffectCommons.TrashDigivolutionCardsFromTopOrBottom(targetPermanent: selectedPermanent, trashCount: selectedPermanent.DigivolutionCards.Count, isFromTop: true, activateClass: activateClass);
                }
            }
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            CardEffectCommons.AddActivateMainOptionSecurityEffect(
                card: card,
                cardEffects: ref cardEffects,
                effectName: "Trash digivolution cards");
        }

        return cardEffects;
    }
}
