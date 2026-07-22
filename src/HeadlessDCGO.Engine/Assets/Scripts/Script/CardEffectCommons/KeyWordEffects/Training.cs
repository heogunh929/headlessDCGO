// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Training.cs
// (SKEL-Exhaust) 1:1 mirror of the AS-IS TrainingClass keyword-body wrapper (latent in AS-IS too — 0 callers;
// the live [Training] path is CardEffectFactory/KeyWordEffects/Training.cs which inlines the same body). Ported
// for freeze-contract completeness. Substrate translation: coroutine IEnumerator -> async Task;
// yield return ContinuousController.StartCoroutine(X) -> await X; card.PermanentOfThisCard() ->
// ICardEffect.ResolvePermanentOfThisCard(card); CardEffectCommons.CardEffectHashtable(activateClass) ->
// pass the ICardEffect directly (mirror SuspendPermanentsClass takes ICardEffect, not a Hashtable); the empty
// AS-IS library-count guard mirrors the live factory's `libraryCards.Count > 0` snapshot adaptation.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;

using System.Collections.Generic;
using System.Threading.Tasks;

public class TrainingClass
{
    public TrainingClass(ICardEffect activateClass)
    {
        _activateClass = activateClass;
    }

    private readonly ICardEffect _activateClass;

    /// <summary>AS-IS <c>TrainingClass.Training</c> (KeyWordEffects/Training.cs): the [Training] process —
    /// suspend this Digimon, then place the top card of the owner's library face-down at the bottom of its
    /// digivolution cards.</summary>
    public async Task Training()
    {
        if (_activateClass is null)
        {
            return;
        }

        if (_activateClass.EffectSourceCard is null)
        {
            return;
        }

        CardSource card = _activateClass.EffectSourceCard;
        Permanent thisPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

        if (thisPermanent.IsSuspended || !thisPermanent.CanSuspend)
        {
            return;
        }

        await new SuspendPermanentsClass(
            new List<Permanent>() { thisPermanent },
            _activateClass, isBlock: false).Tap();

        // AS-IS reads card.Owner.LibraryCards after the Tap; the mirror property materializes a snapshot per
        // read, so the capture stays on the post-Tap side (matches the live factory path).
        List<CardSource> libraryCards = new Player(card.Context, card.Owner).LibraryCards;
        if (libraryCards.Count > 0)
        {
            await thisPermanent.AddDigivolutionCardsBottom(
                new List<CardSource> { libraryCards[0] }, card.InstanceId, isFacedown: true);
        }
    }
}
