// Source: DCGO/Assets/Scripts/Script/CardEffects/ActivateClass.cs
// (EFFECT-MODEL REBUILD / kind-class) 1:1 mirror of the original `ActivateClass` — the single most-used effect
// kind: an ACTIVATED (triggered / [On Play] / [When Digivolving] / …) effect whose body is a closure the card
// supplies. Every AS-IS card that inlines `new ActivateClass()` (e.g. BT1_001.cs) and every CardEffectFactory
// method that returns an activated effect ultimately builds one of these. Concrete implementation of the
// `ActivateICardEffect` contract (CardEffectCommons/ICardEffect.cs) over the abstract `ICardEffect` base.
//
// TRANSLATION (only substrate plumbing changes; the logic is verbatim):
//   * `Func<Hashtable, IEnumerator> _activateCoroutine` -> `Func<Hashtable, Task>` — the activation body a card
//     supplies is a coroutine in AS-IS, an `async Task` here (matching `ActivateICardEffect.Activate`'s
//     IEnumerator->Task adaptation in ICardEffect.cs).
//   * `ContinuousController.instance.StartCoroutine(_activateCoroutine(hashtable))` -> `await
//     _activateCoroutine(hashtable)` — the Unity coroutine-runner dependency the async/await model replaces.
//   * `DataBase.ReplaceToASCII(effectDiscription)` — full-width->ASCII description normaliser, ported to the
//     mirror `DataBase` (CardEffectCommons/DataBase.cs) verbatim; kept because `ICardEffect.IsOnPlay`/
//     `IsWhenDigivolving`/`IsOnDeletion`/`IsOnAttack` match on the ASCII-normalised `[On Play]`-style prefix.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

/// <summary>AS-IS <c>ActivateClass</c> (DCGO CardEffects/ActivateClass.cs) — <c>ICardEffect, ActivateICardEffect</c>.</summary>
public class ActivateClass : ICardEffect, ActivateICardEffect
{
    public Permanent PermanentWhenTriggered { get; set; } = null;
    public CardSource TopCardWhenTriggered { get; set; } = null;
    Func<Hashtable, Task> _activateCoroutine { get; set; } = null;

    public void SetUpActivateClass(Func<Hashtable, bool> canActivateCondition,
        Func<Hashtable, Task> activateCoroutine,
        int maxCountPerTurn,
        bool isOptional,
        string effectDiscription)
    {
        SetCanActivateCondition(canActivateCondition);
        SetMaxCountPerTurn(maxCountPerTurn);
        SetIsOptional(isOptional);
        SetEffectDiscription(DataBase.ReplaceToASCII(effectDiscription));
        _activateCoroutine = activateCoroutine;
    }

    public async Task Activate(Hashtable hashtable)
    {
        if (_activateCoroutine != null)
        {
            await _activateCoroutine(hashtable);
        }
    }
}
