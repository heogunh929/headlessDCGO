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
//   - The panel/RPC/queue plumbing (:19-30/:38-41/:43-54/:62-65 and the isYou/AI branches of Set*Selection,
//     :108-141/:162-195) is the SELECTION TRANSPORT — the mirror's `context.ChoiceProvider.ChooseAsync` request
//     IS the transport (the W4 SelectPermanentEffect/SelectCardEffect precedent). Both the human panel and the
//     AI random branch collapse to the ONE ChoiceProvider request (RD-W5-2: the provider policy owns the
//     opponent/AI decision; the AS-IS UnityEngine.Random pick is not reproduced).
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
        if (_selectPlayer is { } selectPlayer)
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
                    _selectedIntValue = elements[picked].Value;
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

    /// <summary>AS-IS <c>SetIntSelection</c> (:102-152). The verbatim reset (:104-107); the panel/AI/RPC
    /// branches (:108-141, incl. <c>SendSelection</c> :143-151) are the transport — carried by the
    /// WaitForEndSelect ChoiceProvider request (file header). <c>notSelectPlayerMessage</c> is the OTHER
    /// player's waiting-banner text (:126) = UI (stripped, parameter kept at the AS-IS signature).</summary>
    public void SetIntSelection(List<SelectionElement<int>> selectionElements, HeadlessPlayerId selectPlayer, string selectPlayerMessage, string notSelectPlayerMessage, bool IsLocal = false)
    {
        _endSelect = false;
        _selectedIntValue = 0;
        _selectPlayer = selectPlayer;
        _isLocal = IsLocal;

        _ = notSelectPlayerMessage; // UI-only (:126).
        _pendingMessage = selectPlayerMessage;
        _pendingElements = new List<(string, int, int)>();
        foreach (SelectionElement<int> selectionElement in selectionElements)
        {
            _pendingElements.Add((selectionElement.Message, selectionElement.Value, selectionElement.SpriteIndex));
        }
    }

    /// <summary>AS-IS <c>SetBoolSelection</c> (:156-206) — identical to SetIntSelection except the bool
    /// values ride the int channel via <c>getIntFromBool</c>, exactly as the AS-IS RPC transport does
    /// (SetBool :56-60 / SetBoolForPlayer :43-54 store ints).</summary>
    public void SetBoolSelection(List<SelectionElement<bool>> selectionElements, HeadlessPlayerId selectPlayer, string selectPlayerMessage, string notSelectPlayerMessage, bool IsLocal = false)
    {
        _endSelect = false;
        _selectedIntValue = 0;
        _selectPlayer = selectPlayer;
        _isLocal = IsLocal;

        _ = notSelectPlayerMessage; // UI-only (:180).
        _pendingMessage = selectPlayerMessage;
        _pendingElements = new List<(string, int, int)>();
        foreach (SelectionElement<bool> selectionElement in selectionElements)
        {
            _pendingElements.Add((selectionElement.Message, getIntFromBool(selectionElement.Value), selectionElement.SpriteIndex));
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
