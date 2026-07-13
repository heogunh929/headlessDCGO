// Source: DCGO/Assets/Scripts/Script/SelectDNACondition.cs (90 lines)
// (P6C1) 1:1 mirror of the AS-IS SelectDNACondition component — the "which DNA (jogress) condition do you
// want?" picker the play pipeline reads (`GManager.instance.GetComponent<SelectDNACondition>()._selectedCount`,
// CardController.cs:580-584) and the jogress flows drive via SetUp/Activate.
// ADAPTATIONS (substrate only, per the W4 Select* convention):
//   * `IEnumerator Activate()` -> `async Task Activate()`; `Func<int, IEnumerator>` callback -> `Func<int, Task>`;
//     `yield return ContinuousController.instance.StartCoroutine(X)` -> `await X`.
//   * The labelled int pick rides the ALREADY-MIRRORED UserSelectionManager
//     (SetIntSelection -> WaitForEndSelect -> SelectedIntValue), exactly as AS-IS (:75-79).
//   * AS-IS :41-44 photonWaitController sync is commented out IN AS-IS (kept as a comment); :82-83
//     commandText close/wait = UI (stripped).
//   * `_targetDNA.jogressCondition` (the AS-IS CardSource property) -> `_targetDNA.JogressConditionOf()`
//     (the P6C1 accessor bridge in PlayCardClass.cs — AS-IS home CardSource.cs is another remediation
//     cluster's file; relocation design item RD-P6C1-9).
// AS-IS quirk KEPT verbatim: the inner `if (jogressCondition.Count == 1)` arm is dead under the outer
// `Count > 1` guard (AS-IS :46-48).

namespace HeadlessDCGO.Engine.Assets.Scripts.Script;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class SelectDNACondition
{
    // AS-IS :11-23.
    public void SetUp(
        Player SelectPlayer,
        CardSource targetDNA,
        Func<int, Task> SelectDNACoroutine,
        bool IsLocal = false)
    {
        _selectPlayer = SelectPlayer;
        _targetDNA = targetDNA;
        _selectDNACoroutine = SelectDNACoroutine;
        _notDoSync = false;
        _isDigivolutionCost = false;
        _isLocal = IsLocal;
    }

    // AS-IS :25-32 (the unused-in-AS-IS `_candidates` list is kept 1:1).
    Player _selectPlayer = null;
    CardSource _targetDNA = null;
    Func<int, Task> _selectDNACoroutine = null;
    List<int> _candidates = new List<int>();
    public int _selectedCount = 0;
    bool _notDoSync = false;
    bool _isDigivolutionCost = false;
    bool _isLocal = false;

    // AS-IS :34-37.
    public void ResetSelectDNAConditionClass()
    {
        _selectedCount = 0;
    }

    // AS-IS :39-89, IEnumerator -> Task.
    public async Task Activate()
    {
        if (!_notDoSync)
        {
            //yield return GManager.instance.photonWaitController.StartWait("SelectDNACondition");
        }

        if (_targetDNA.JogressConditionOf().Count > 1)
        {
            if (_targetDNA.JogressConditionOf().Count == 1)
            {
                _selectedCount = 0;
            }
            else
            {
                List<SelectionElement<int>> selectionElements = new List<SelectionElement<int>>();

                for (int i = 0; i < _targetDNA.JogressConditionOf().Count; i++)
                {
                    string message = $"";

                    for (int e = 0; e < _targetDNA.JogressConditionOf()[i].elements.Length; e++)
                    {
                        message += $"{_targetDNA.JogressConditionOf()[i].elements[e].SelectMessage}";

                        if (e < _targetDNA.JogressConditionOf()[i].elements.Length - 1)
                            message += " + ";
                    }

                    selectionElements.Add(new(message: $"{message}", value: i, spriteIndex: 0));
                }

                string selectPlayerMessage = "Which DNA do you want?";
                string notSelectPlayerMessage = "The opponent is choosing from which DNA to do.";

                GManager.instance.userSelectionManager.SetIntSelection(selectionElements: selectionElements, selectPlayer: _targetDNA.Owner, selectPlayerMessage: selectPlayerMessage, notSelectPlayerMessage: notSelectPlayerMessage, _isLocal);

                await GManager.instance.userSelectionManager.WaitForEndSelect();

                _selectedCount = GManager.instance.userSelectionManager.SelectedIntValue;
            }
        }

        // AS-IS :82-83 commandText.CloseCommandText() + WaitWhile(activeSelf) = UI (stripped).

        if (_selectDNACoroutine != null)
        {
            await _selectDNACoroutine(_selectedCount);
        }
    }
}
