namespace HeadlessDCGO.Engine.Headless.Runtime;

public static class HeadlessActionTypes
{
    public const string NoOp = "NoOp";
    public const string Pass = "Pass";
    public const string Cheat = "Cheat";
    public const string PlayCard = "PlayCard";
    public const string Digivolve = "Digivolve";
    public const string SpecialPlay = "SpecialPlay";
    public const string ActivateOption = "ActivateOption";
    public const string SetTerminal = "SetTerminal";
    public const string ClearTerminal = "ClearTerminal";
    public const string MoveCard = "MoveCard";
    public const string AddToHand = "AddToHand";
    public const string AddToTrash = "AddToTrash";
    public const string AddToSecurity = "AddToSecurity";
    public const string MoveToDeckTop = "MoveToDeckTop";
    public const string MoveToDeckBottom = "MoveToDeckBottom";
    public const string DrawCards = "DrawCards";
    public const string AddSecurityFromLibrary = "AddSecurityFromLibrary";
    public const string TrashSecurity = "TrashSecurity";
    public const string HatchDigitama = "HatchDigitama";
    public const string MoveBreedingToBattle = "MoveBreedingToBattle";
    public const string DeclareAttack = "DeclareAttack";
    public const string ResolveAttack = "ResolveAttack";
    public const string ClearAttack = "ClearAttack";
    public const string RequestChoice = "RequestChoice";
    public const string ResolveChoice = "ResolveChoice";
    public const string ClearChoice = "ClearChoice";
    public const string ShuffleDeck = "ShuffleDeck";
    public const string EnqueueEffect = "EnqueueEffect";
    public const string AdvancePhase = "AdvancePhase";
    public const string EndTurn = "EndTurn";
    public const string SetMemory = "SetMemory";
    public const string AddMemory = "AddMemory";
    public const string PayMemory = "PayMemory";

    public const string NormalizedNoOp = "NOOP";
    public const string NormalizedPass = "PASS";
    public const string NormalizedCheat = "CHEAT";
    public const string NormalizedPlayCard = "PLAYCARD";
    public const string NormalizedDigivolve = "DIGIVOLVE";
    public const string NormalizedSpecialPlay = "SPECIALPLAY";
    public const string NormalizedActivateOption = "ACTIVATEOPTION";
    public const string NormalizedSetTerminal = "SETTERMINAL";
    public const string NormalizedClearTerminal = "CLEARTERMINAL";
    public const string NormalizedMoveCard = "MOVECARD";
    public const string NormalizedAddToHand = "ADDTOHAND";
    public const string NormalizedAddToTrash = "ADDTOTRASH";
    public const string NormalizedAddToSecurity = "ADDTOSECURITY";
    public const string NormalizedMoveToDeckTop = "MOVETODECKTOP";
    public const string NormalizedMoveToDeckBottom = "MOVETODECKBOTTOM";
    public const string NormalizedDrawCards = "DRAWCARDS";
    public const string NormalizedAddSecurityFromLibrary = "ADDSECURITYFROMLIBRARY";
    public const string NormalizedTrashSecurity = "TRASHSECURITY";
    public const string NormalizedHatchDigitama = "HATCHDIGITAMA";
    public const string NormalizedMoveBreedingToBattle = "MOVEBREEDINGTOBATTLE";
    public const string NormalizedDeclareAttack = "DECLAREATTACK";
    public const string NormalizedResolveAttack = "RESOLVEATTACK";
    public const string NormalizedClearAttack = "CLEARATTACK";
    public const string NormalizedRequestChoice = "REQUESTCHOICE";
    public const string NormalizedResolveChoice = "RESOLVECHOICE";
    public const string NormalizedClearChoice = "CLEARCHOICE";
    public const string NormalizedShuffleDeck = "SHUFFLEDECK";
    public const string NormalizedEnqueueEffect = "ENQUEUEEFFECT";
    public const string NormalizedAdvancePhase = "ADVANCEPHASE";
    public const string NormalizedEndTurn = "ENDTURN";
    public const string NormalizedSetMemory = "SETMEMORY";
    public const string NormalizedAddMemory = "ADDMEMORY";
    public const string NormalizedPayMemory = "PAYMEMORY";

    public static string Normalize(string actionType)
    {
        return actionType.Trim().ToUpperInvariant();
    }
}
