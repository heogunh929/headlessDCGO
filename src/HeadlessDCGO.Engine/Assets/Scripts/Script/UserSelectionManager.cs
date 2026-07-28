// Source: DCGO/Assets/Scripts/Script/UserSelectionManager.cs
// (EFFECT-MODEL REBUILD / bridge W5) 1:1 mirror of the AS-IS `UserSelectionManager` — the GManager-attached
// component card effects use for a MANDATORY labeled mode/branch pick ("Suspend 1" vs "Suspend 2", BT1_111;
// "Which effect do you choose?"). AS-IS flow: `SetBoolSelection`/`SetIntSelection` opens the command panel for
// the select player (or the AI random branch), the pick travels `SendSelection` -> `SetBool_RPC`/`SetInt_RPC` ->
// `Set*ForPlayer` -> `player.QueuePlayerSelection(new ValueSelection(value))`, and `WaitForEndSelect` polls the
// queue, dequeues the value into the shared `_selectedIntValue` channel (bool rides the SAME int channel via
// getIntFromBool/getBoolFromInt), then the effect reads `SelectedBoolValue`/`SelectedIntValue`. `SetBool`/
// `SetInt` are the direct, no-choice setters (the "only one branch is live" path — sets `_endSelect`).
//
// Substrate translation (docs/audit/rebuild_bridge_w5_notes.md):
//   - (isAI restore) The RPC/queue plumbing (:19-30/:38-41/:43-54/:62-65) and the isYou/AI branches of
//     Set*Selection (:108-141/:162-195) are now ported 1:1, including the AI branch's canSelectValue build and
//     its random pick. Only the two UI ARMS are stripped: the owner's command panel and the non-select player's
//     waiting banner — their delivery is the `context.ChoiceProvider.ChooseAsync` request issued by
//     WaitForEndSelect where AS-IS blocks on the queue (the W4 SelectPermanentEffect/SelectCardEffect
//     precedent). Headless `Player.isYou` and `GManager.IsAI` are both false, so the AI branch never runs and
//     the request path is the only live one, as before.
//     The AI pick uses `EngineContext.RandomSource.NextInt` for AS-IS's `UnityEngine.Random.Range`; NOTE
//     (verified in Headless/Services/GameRandomSource.cs:6) that source is documented as the equivalent of
//     AS-IS `GameRandom.Range`, NOT of `UnityEngine.Random.Range` (:22-27 of the same header classes the AI's
//     non-synchronized selection draws as UnityEngine.Random). Same [0,count) contract, different stream; the
//     branch is unreachable headless, so no live draw is affected.
//   - `Set*Selection` stores the pending elements + select player verbatim-reset state (:104-107/:158-161);
//     the request itself is issued inside `WaitForEndSelect` (:77-100), where AS-IS blocks — the deferred point
//     is identical, no state is readable between the two in any AS-IS caller (RD-W5-1).
//   - The choice shape follows the established ModeChoice primitive (`ChoiceType.ModeChoice`, min 1 / max 1 /
//     no skip, synthetic labeled candidates — ModeChoiceEffect.BuildRequest, ActivatedEffects.cs; that enum
//     member's own doc names AS-IS UserSelectionManager SetBool/IntSelection as its original).
//   - `GManager.instance.commandText.CloseCommandText()` + activeSelf wait (:96-99) = UI (stripped).
//   - MonoBehaviourPunCallbacks base / [PunRPC] = Photon substrate (stripped); `_isLocal` kept as state, like
//     the select effects' SetIsLocal.
// Match-scoped ONE instance via `GManager.userSelectionManager` (context-cached service), mirroring the single
// component on the AS-IS GManager GameObject (GManager.cs:114).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.PlayerSelection;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public class UserSelectionManager
{
    // AS-IS :10-17.
    bool _endSelect = false;
    int _selectedIntValue = 0;
    bool _isLocal = false;
    bool _selectedBoolValue => getBoolFromInt(_selectedIntValue);
    public int SelectedIntValue => _selectedIntValue;
    public bool SelectedBoolValue => _selectedBoolValue;

    // AS-IS :17 `Player _selectPlayer` — null = no pending player choice (the SetBool/SetInt direct path).
    HeadlessPlayerId? _selectPlayer;

    // The pending labeled options of the LAST Set*Selection call, normalized onto the AS-IS int transport
    // channel (a bool element rides getIntFromBool exactly as the AS-IS RPC does). AS-IS holds these in the
    // command-panel button closures; the mirror carries them to the WaitForEndSelect request.
    List<(string Message, int Value, int SpriteIndex)>? _pendingElements;
    string _pendingMessage = string.Empty;

    private EngineContext? _context;

    internal void AttachContext(EngineContext context) => _context = context;

    // AS-IS :32-36.
    public void SetInt(int value)
    {
        _selectedIntValue = value;
        _endSelect = true;
    }

    // AS-IS :56-60.
    public void SetBool(bool value)
    {
        _selectedIntValue = getIntFromBool(value);
        _endSelect = true;
    }

    // AS-IS :67-70.
    internal int getIntFromBool(bool value)
    {
        return value ? 1 : 0;
    }

    // AS-IS :72-75.
    internal bool getBoolFromInt(int value)
    {
        return value != 0;
    }

    /// <summary>AS-IS <c>WaitForEndSelect</c> (:77-100): with a pending select player, wait for that player's
    /// queued ValueSelection and take its value onto the int channel (:79-89) — mirrored as the ONE
    /// ChoiceProvider ModeChoice request over the pending elements; otherwise the value was set directly by
    /// SetInt/SetBool and <c>_endSelect</c> already holds (:91-94). Then the AS-IS reset (:95-96). The AS-IS
    /// null-dequeue fallthrough (a null ValueSelection leaves <c>_selectedIntValue</c> untouched, :84-88) is
    /// kept for a skipped/empty result. CloseCommandText + activeSelf wait (:98-99) = UI (stripped).</summary>
    public async Task WaitForEndSelect()
    {
        // SUBSTRATE: SetIntForPlayer below reads the process-global `GManager.instance` — scope the match, as in
        // Set*Selection.
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(RequireContext());

        if (_selectPlayer is { } selectPlayer)
        {
            Player selectionPlayer = new Player(RequireContext(), selectPlayer);

            // AS-IS :79 `yield return new WaitUntil(() => _selectPlayer.HasPlayerSelection());` — blocks until a
            // Set*Selection branch delivered a ValueSelection (the restored AI branch queues one immediately; the
            // owner's panel delivers one by RPC). The mirror's ChoiceProvider request IS that panel/RPC delivery,
            // so it is issued exactly when nothing was queued.
            if (!selectionPlayer.HasPlayerSelection())
            {
                List<(string Message, int Value, int SpriteIndex)> elements =
                    _pendingElements ?? new List<(string, int, int)>();

                var candidates = new List<ChoiceCandidate>(elements.Count);
                for (int index = 0; index < elements.Count; index++)
                {
                    candidates.Add(new ChoiceCandidate(
                        new HeadlessEntityId($"userSelection#{index}"),
                        elements[index].Message, ChoiceZone.BattleArea, IsSelectable: true, ownerId: selectPlayer));
                }

                var request = new ChoiceRequest(
                    ChoiceType.ModeChoice, selectPlayer, _pendingMessage,
                    minCount: 1, maxCount: 1, canSkip: false, ChoiceZone.BattleArea, candidates);

                ChoiceResult result = await RequireContext().ChoiceProvider.ChooseAsync(request).ConfigureAwait(false);
                if (!result.IsSkipped && result.SelectedIds.Count > 0)
                {
                    // Parse the picked index off the synthetic id — the ModeChoiceEffect.BranchFor convention.
                    string[] parts = result.SelectedIds[0].Value.Split('#');
                    if (int.TryParse(parts.Length > 1 ? parts[1] : null, out int picked)
                        && picked >= 0 && picked < elements.Count)
                    {
                        // The AS-IS transport the button closure uses (SendSelection -> SetInt_RPC ->
                        // SetIntForPlayer): queue it on the seat, then take it off below as AS-IS does.
                        SetIntForPlayer(selectPlayer, elements[picked].Value);
                    }
                }
            }

            // AS-IS :81-88 `DequeuePlayerSelection<ValueSelection>()`; a null dequeue leaves _selectedIntValue
            // untouched. The HasPlayerSelection guard keeps the pre-existing skipped/empty-result fallthrough:
            // AS-IS's WaitUntil cannot fall through with an empty queue, headless a skipped choice queues nothing.
            if (selectionPlayer.HasPlayerSelection())
            {
                ValueSelection? valueSeletion = selectionPlayer.DequeuePlayerSelection<ValueSelection>();

                if (valueSeletion != null)
                {
                    _selectedIntValue = valueSeletion.ValueAsInt();
                }
            }
        }
        else if (!_endSelect)
        {
            // AS-IS :93 `yield return new WaitWhile(() => !_endSelect)` — with neither a pending selection nor
            // a direct SetInt/SetBool, the AS-IS coroutine polls forever. No headless caller may reach this
            // state; STOP loudly instead of hanging (design item RD-W5-3).
            throw new InvalidOperationException(
                "UserSelectionManager.WaitForEndSelect called with no pending selection and no direct " +
                "SetInt/SetBool value (the AS-IS path would poll forever) — design item RD-W5-3, " +
                "docs/audit/rebuild_bridge_w5_notes.md.");
        }

        // AS-IS :95-96.
        _endSelect = false;
        _selectPlayer = null;
        _pendingElements = null;

        // AS-IS :98-99 commandText.CloseCommandText() + activeSelf wait = UI (stripped).
    }

    /// <summary>(isAI restore) AS-IS <c>[PunRPC] public void SetIntForPlayer(int playerID, int value)</c>
    /// (:19-29), ported 1:1 including the AS-IS null-seat early return. SUBSTRATE: the first parameter carries
    /// the mirror's seat-identity type (AS-IS callers pass the int <c>Player.PlayerID</c>; the mirror has no int
    /// seat index — see <c>GManager.GetPlayerFromID</c>); name, order and count unchanged. <c>[PunRPC]</c> is
    /// Photon substrate (stripped).</summary>
    public void SetIntForPlayer(HeadlessPlayerId playerID, int value)
    {
        Player? selectionPlayer = GManager.instance!.GetPlayerFromID(playerID);

        if (selectionPlayer == null)
        {
            return;
        }

        selectionPlayer.QueuePlayerSelection(new ValueSelection(value));
    }

    /// <summary>(isAI restore) AS-IS <c>protected void SetInt_RPC(int playerID, int value)</c> (:38-41) —
    /// <c>photonView.RPC("SetIntForPlayer", RpcTarget.All, playerID, value)</c>, translated to the direct call
    /// with the same arguments (the sanctioned Photon-dispatch translation).</summary>
    protected void SetInt_RPC(HeadlessPlayerId playerID, int value)
    {
        SetIntForPlayer(playerID, value);
    }

    /// <summary>(isAI restore) AS-IS <c>[PunRPC] public void SetBoolForPlayer(int playerID, bool value)</c>
    /// (:43-54) — the bool sibling of <see cref="SetIntForPlayer"/>; AS-IS queues the bool through the same
    /// <see cref="ValueSelection"/> (its bool constructor).</summary>
    public void SetBoolForPlayer(HeadlessPlayerId playerID, bool value)
    {
        Player? selectionPlayer = GManager.instance!.GetPlayerFromID(playerID);

        if (selectionPlayer == null)
        {
            return;
        }

        selectionPlayer.QueuePlayerSelection(new ValueSelection(value));
    }

    /// <summary>(isAI restore) AS-IS <c>protected void SetBool_RPC(int playerID, bool value)</c> (:62-65).
    /// </summary>
    protected void SetBool_RPC(HeadlessPlayerId playerID, bool value)
    {
        SetBoolForPlayer(playerID, value);
    }

    /// <summary>AS-IS <c>SetIntSelection</c> (:102-152). The verbatim reset (:104-107), then the AS-IS
    /// <c>selectPlayer.isYou</c> / <c>GManager.IsAI</c> split (:108-141) with <c>SendSelection</c> (:143-151)
    /// — restored branch-for-branch. Only the two UI ARMS are stripped: the owner's command panel (:110-121)
    /// and the non-select player's waiting banner from <paramref name="notSelectPlayerMessage"/> (:126); their
    /// delivery is the WaitForEndSelect ChoiceProvider request (file header). Headless <c>isYou</c> and
    /// <c>IsAI</c> are both false, so nothing is queued here and WaitForEndSelect issues that request, exactly
    /// as before this restore.</summary>
    public void SetIntSelection(List<SelectionElement<int>> selectionElements, HeadlessPlayerId selectPlayer, string selectPlayerMessage, string notSelectPlayerMessage, bool IsLocal = false)
    {
        // SUBSTRATE: the AS-IS branches read the process-global `GManager.instance`; the mirror resolves that
        // from AmbientMatchContext, so scope the match here (a caller already in the same scope re-enters
        // harmlessly) — the CardSource.GetPayingCostWithBaseCost idiom.
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(RequireContext());

        _endSelect = false;
        _selectedIntValue = 0;
        _selectPlayer = selectPlayer;
        _isLocal = IsLocal;

        // Mirror-only: the labeled options AS-IS keeps in the command-panel button closures (:114-119) are
        // carried to the WaitForEndSelect request instead.
        _pendingMessage = selectPlayerMessage;
        _pendingElements = new List<(string, int, int)>();
        foreach (SelectionElement<int> selectionElement in selectionElements)
        {
            _pendingElements.Add((selectionElement.Message, selectionElement.Value, selectionElement.SpriteIndex));
        }

        if (new Player(RequireContext(), selectPlayer).isYou)
        {
            // AS-IS :110-121 commandText.OpenCommandText(selectPlayerMessage) + one Command_SelectCommand per
            // element, each firing SendSelection = UI (stripped).
        }

        else
        {
            // AS-IS :126 commandText.OpenCommandText(notSelectPlayerMessage) — waiting banner = UI (stripped).

            #region AIモード
            if (GManager.instance!.IsAI)
            {
                List<int> canSelectValue = new List<int>();

                foreach (SelectionElement<int> selectionElement in selectionElements)
                {
                    canSelectValue.Add(selectionElement.Value);
                }

                int value = canSelectValue.Count >= 1 ? canSelectValue[RequireContext().RandomSource.NextInt(0, canSelectValue.Count)] : 0;
                SendSelection(value);
            }
            #endregion
        }

        // AS-IS :143-151.
        void SendSelection(int value)
        {
            if (_isLocal)
            {
                SetIntForPlayer(selectPlayer, value);
            }
            else
            {
                SetInt_RPC(selectPlayer, value);
            }
        }
    }

    /// <summary>AS-IS <c>SetBoolSelection</c> (:156-206) — identical to SetIntSelection except the bool
    /// values ride the int channel via <c>getIntFromBool</c>, exactly as the AS-IS RPC transport does
    /// (SetBool :56-60 / SetBoolForPlayer :43-54 store ints).</summary>
    public void SetBoolSelection(List<SelectionElement<bool>> selectionElements, HeadlessPlayerId selectPlayer, string selectPlayerMessage, string notSelectPlayerMessage, bool IsLocal = false)
    {
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(RequireContext());

        _endSelect = false;
        _selectedIntValue = 0;
        _selectPlayer = selectPlayer;
        _isLocal = IsLocal;

        _pendingMessage = selectPlayerMessage;
        _pendingElements = new List<(string, int, int)>();
        foreach (SelectionElement<bool> selectionElement in selectionElements)
        {
            _pendingElements.Add((selectionElement.Message, getIntFromBool(selectionElement.Value), selectionElement.SpriteIndex));
        }

        if (new Player(RequireContext(), selectPlayer).isYou)
        {
            // AS-IS :164-175 command panel = UI (stripped).
        }

        else
        {
            // AS-IS :180 commandText.OpenCommandText(notSelectPlayerMessage) = UI (stripped).

            #region AIモード
            if (GManager.instance!.IsAI)
            {
                List<bool> canSelectValue = new List<bool>();

                foreach (SelectionElement<bool> selectionElement in selectionElements)
                {
                    canSelectValue.Add(selectionElement.Value);
                }

                bool value = canSelectValue.Count >= 1 ? canSelectValue[RequireContext().RandomSource.NextInt(0, canSelectValue.Count)] : false;
                SendSelection(value);
            }
            #endregion
        }

        // AS-IS :197-205.
        void SendSelection(bool value)
        {
            if (_isLocal)
            {
                SetBoolForPlayer(selectPlayer, value);
            }
            else
            {
                SetBool_RPC(selectPlayer, value);
            }
        }
    }

    private EngineContext RequireContext() =>
        _context
        ?? AmbientMatchContext.Current
        ?? throw new InvalidOperationException(
            "UserSelectionManager has no EngineContext — obtain the instance via " +
            "GManager.instance.userSelectionManager (bridge W5).");
}

// AS-IS :211-222 — the labeled-option data holder, verbatim (same file, namespace level).
public class SelectionElement<T>
{
    public SelectionElement(string message, T value, int spriteIndex)
    {
        this.Message = message;
        this.Value = value;
        this.SpriteIndex = spriteIndex;
    }
    public string Message { get; private set; }
    public T Value { get; private set; }
    public int SpriteIndex { get; private set; }
}
