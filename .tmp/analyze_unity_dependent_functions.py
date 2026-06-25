from __future__ import annotations

import csv
import re
from dataclasses import dataclass
from pathlib import Path


ROOT = Path(r"E:\headlessDCGO_new")
SOURCE_ROOT = ROOT / "DCGO" / "Assets"
HEADLESS_ROOT = ROOT / "src" / "HeadlessDCGO.Engine" / "Headless"
DOCS_ROOT = ROOT / "docs"


UNITY_BASES = (
    "MonoBehaviour",
    "ScriptableObject",
    "Editor",
    "EditorWindow",
    "PropertyDrawer",
    "StateMachineBehaviour",
)

LIFECYCLE_NAMES = {
    "Awake",
    "Start",
    "Update",
    "LateUpdate",
    "FixedUpdate",
    "OnEnable",
    "OnDisable",
    "OnDestroy",
    "OnValidate",
    "Reset",
    "OnApplicationQuit",
    "OnApplicationPause",
    "OnApplicationFocus",
    "OnGUI",
}

CATEGORY_PATTERNS = {
    "Coroutine": [
        r"\bStartCoroutine\b",
        r"\bStopCoroutine\b",
        r"\bCoroutine\b",
        r"\bIEnumerator\b",
        r"\byield\s+return\b",
        r"\bWaitForSeconds\b",
        r"\bWaitWhile\b",
        r"\bWaitUntil\b",
    ],
    "ChoiceInput": [
        r"\bSelectCardEffect\b",
        r"\bSelectPermanentEffect\b",
        r"\bSelectCountEffect\b",
        r"\bSelectHandEffect\b",
        r"\bInput\.",
        r"\bInput\b",
        r"\bButton\b",
        r"\bToggle\b",
        r"\bDropdown\b",
        r"\bEventSystem\b",
        r"\bPointerEventData\b",
    ],
    "GameObjectTransform": [
        r"\bGameObject\b",
        r"\bTransform\b",
        r"\bRectTransform\b",
        r"\bInstantiate\b",
        r"\bDestroy\b",
        r"\bGetComponent\b",
        r"\bGetComponentsInChildren\b",
        r"\bAddComponent\b",
        r"\bFindObjectOfType\b",
        r"\bFindObjectsOfType\b",
        r"\bObject\.",
    ],
    "SceneLifecycle": [
        r"\bMonoBehaviour\b",
        r"\bSceneManager\b",
        r"\bDontDestroyOnLoad\b",
    ],
    "RenderingUI": [
        r"\bUnityEngine\.UI\b",
        r"\bTextMeshPro\b",
        r"\bTMP_",
        r"\bText\b",
        r"\bImage\b",
        r"\bRawImage\b",
        r"\bCanvas\b",
        r"\bCanvasGroup\b",
        r"\bSprite\b",
        r"\bTexture\b",
        r"\bMaterial\b",
        r"\bRenderer\b",
        r"\bCamera\b",
        r"\bAnimator\b",
        r"\bAnimation\b",
        r"\bParticleSystem\b",
        r"\bAudioSource\b",
        r"\bAudioClip\b",
        r"\bColor\b",
        r"\bVector2\b",
        r"\bVector3\b",
        r"\bVector4\b",
        r"\bQuaternion\b",
    ],
    "UnityDataLoading": [
        r"\bResources\.",
        r"\bAddressables\b",
        r"\bTextAsset\b",
        r"\bAssetDatabase\b",
        r"\bSerializeField\b",
        r"\bCreateAssetMenu\b",
        r"\bScriptableObject\b",
    ],
    "DebugTimePrefs": [
        r"\bDebug\.",
        r"\bTime\.",
        r"\bPlayerPrefs\b",
        r"\bApplication\.",
    ],
    "NetworkClient": [
        r"\bPhoton\b",
        r"\bPun\b",
        r"\bPhotonNetwork\b",
        r"\bRpcTarget\b",
    ],
}

CATEGORY_STATUS = {
    "Coroutine": ("DONE", "EngineTaskRunner / CoroutineAdapter / EngineWaitCondition"),
    "ChoiceInput": ("DONE", "IChoiceProvider / ScriptedChoiceProvider / PolicyChoiceProvider"),
    "GameObjectTransform": ("PARTIAL", "UnityNullObjectPolicy + in-memory zone/card services cover card-state cases only"),
    "SceneLifecycle": ("PARTIAL", "DcgoMatch / HeadlessGameLoop replace match lifecycle, not arbitrary MonoBehaviour lifecycle"),
    "RenderingUI": ("OUT_OF_SCOPE", "Visual UI/rendering/audio/camera are excluded from headless engine"),
    "UnityDataLoading": ("PARTIAL", "Card/deck/banlist loaders exist, Resources/Addressables replacement is not complete"),
    "DebugTimePrefs": ("PARTIAL", "ILogSink/trace/random/wait cover some cases; Application/PlayerPrefs are not fully modeled"),
    "NetworkClient": ("OUT_OF_SCOPE", "Client network play is excluded from current headless scope"),
    "ClassUnityBase": ("PARTIAL", "Headless runtime has lifecycle replacement only for match execution"),
    "UnityAttribute": ("OUT_OF_SCOPE", "Serialization/editor attributes do not need runtime headless replacement"),
    "OtherUnity": ("PENDING", "Needs manual review for specific replacement path"),
}

HEADLESS_EVIDENCE = {
    "Coroutine": [
        "Coroutines/EngineTaskRunner.cs",
        "Coroutines/CoroutineAdapter.cs",
        "Coroutines/EngineWaitCondition.cs",
    ],
    "ChoiceInput": [
        "Choices/IChoiceProvider.cs",
        "Choices/ScriptedChoiceProvider.cs",
        "Choices/PolicyChoiceProvider.cs",
    ],
    "GameObjectTransform": [
        "Bridge/UnityNullObjectPolicy.cs",
        "Services/InMemoryZoneMover.cs",
        "Services/InMemoryCardInstanceRepository.cs",
    ],
    "SceneLifecycle": [
        "Runtime/DcgoMatch.cs",
        "Runtime/HeadlessGameLoop.cs",
        "Bridge/EngineContext.cs",
    ],
    "UnityDataLoading": [
        "DataLoading/CardAssetJsonLoader.cs",
        "DataLoading/DeckListLoader.cs",
        "DataLoading/BanlistLoader.cs",
    ],
    "DebugTimePrefs": [
        "Services/ILogSink.cs",
        "Diagnostics/EngineTrace.cs",
        "Services/GameRandomSource.cs",
        "Coroutines/EngineWaitCondition.cs",
    ],
    "ClassUnityBase": [
        "Runtime/DcgoMatch.cs",
        "Runtime/HeadlessGameLoop.cs",
        "Bridge/EngineContext.cs",
    ],
}


@dataclass
class MethodInfo:
    path: str
    class_name: str
    class_bases: str
    function_name: str
    signature: str
    line: int
    body: str


def strip_comments_and_strings(text: str) -> str:
    result = []
    i = 0
    n = len(text)
    state = "code"
    while i < n:
        ch = text[i]
        nxt = text[i + 1] if i + 1 < n else ""
        if state == "code":
            if ch == "/" and nxt == "/":
                state = "line_comment"
                result.append(" ")
                result.append(" ")
                i += 2
                continue
            if ch == "/" and nxt == "*":
                state = "block_comment"
                result.append(" ")
                result.append(" ")
                i += 2
                continue
            if ch == '"':
                state = "string"
                result.append('"')
                i += 1
                continue
            if ch == "'":
                state = "char"
                result.append("'")
                i += 1
                continue
            result.append(ch)
            i += 1
            continue
        if state == "line_comment":
            if ch == "\n":
                state = "code"
                result.append("\n")
            else:
                result.append(" ")
            i += 1
            continue
        if state == "block_comment":
            if ch == "*" and nxt == "/":
                state = "code"
                result.append(" ")
                result.append(" ")
                i += 2
            else:
                result.append("\n" if ch == "\n" else " ")
                i += 1
            continue
        if state == "string":
            if ch == "\\":
                result.append(" ")
                result.append(" ")
                i += 2
                continue
            if ch == '"':
                state = "code"
                result.append('"')
            else:
                result.append("\n" if ch == "\n" else " ")
            i += 1
            continue
        if state == "char":
            if ch == "\\":
                result.append(" ")
                result.append(" ")
                i += 2
                continue
            if ch == "'":
                state = "code"
                result.append("'")
            else:
                result.append("\n" if ch == "\n" else " ")
            i += 1
            continue
    return "".join(result)


def line_number_at(text: str, pos: int) -> int:
    return text.count("\n", 0, pos) + 1


def find_matching_brace(text: str, open_index: int) -> int:
    depth = 0
    for i in range(open_index, len(text)):
        if text[i] == "{":
            depth += 1
        elif text[i] == "}":
            depth -= 1
            if depth == 0:
                return i
    return len(text) - 1


def previous_statement(text: str, open_index: int) -> str:
    i = open_index - 1
    depth_paren = 0
    while i >= 0:
        ch = text[i]
        if ch == ")":
            depth_paren += 1
        elif ch == "(":
            depth_paren -= 1
        elif depth_paren <= 0 and ch in "{};":
            return text[i + 1:open_index].strip()
        i -= 1
    return text[:open_index].strip()


CLASS_RE = re.compile(
    r"\b(?:public|private|protected|internal|sealed|abstract|static|partial|\s)*"
    r"(?:class|struct)\s+(?P<name>[A-Za-z_][A-Za-z0-9_]*)"
    r"(?:\s*:\s*(?P<bases>[^{]+))?$",
    re.S,
)

METHOD_RE = re.compile(
    r"\b(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*\([^;{}]*\)\s*(?:where\s+[^{]+)?$",
    re.S,
)

CONTROL_KEYWORDS = {
    "if",
    "for",
    "foreach",
    "while",
    "switch",
    "catch",
    "using",
    "lock",
    "fixed",
}


def parse_methods(path: Path) -> list[MethodInfo]:
    try:
        raw = path.read_text(encoding="utf-8-sig")
    except UnicodeDecodeError:
        raw = path.read_text(encoding="cp949", errors="ignore")
    clean = strip_comments_and_strings(raw)
    methods: list[MethodInfo] = []
    class_stack: list[tuple[int, str, str]] = []
    method_ranges: list[tuple[int, int]] = []
    brace_positions = [m.start() for m in re.finditer(r"[{}]", clean)]
    for pos in brace_positions:
        if any(start < pos < end for start, end in method_ranges):
            continue
        ch = clean[pos]
        if ch != "{":
            class_stack = [(end, name, bases) for end, name, bases in class_stack if end > pos]
            continue
        sig = previous_statement(clean, pos)
        sig_one_line = " ".join(sig.split())
        class_match = CLASS_RE.search(sig_one_line)
        close_pos = find_matching_brace(clean, pos)
        if class_match:
            class_stack.append((close_pos, class_match.group("name"), class_match.group("bases") or ""))
            continue
        method_match = METHOD_RE.search(sig_one_line)
        if not method_match:
            continue
        name = method_match.group("name")
        if name in CONTROL_KEYWORDS:
            continue
        if "." in sig_one_line.split("(", 1)[0]:
            continue
        if "=>" in sig_one_line or "=" in sig_one_line and "(" not in sig_one_line.split("=")[0]:
            continue
        current_class = class_stack[-1] if class_stack else (0, "", "")
        if not current_class[1]:
            continue
        prefix = sig_one_line[:method_match.start("name")].strip()
        if not prefix and name != current_class[1]:
            continue
        if prefix.startswith(("return ", "new ", "await ", "yield ")):
            continue
        close_method_pos = find_matching_brace(clean, pos)
        body = raw[pos + 1:find_matching_brace(clean, pos)]
        method_ranges.append((pos, close_method_pos))
        methods.append(
            MethodInfo(
                path=str(path.relative_to(ROOT)).replace("\\", "/"),
                class_name=current_class[1],
                class_bases=" ".join(current_class[2].split()),
                function_name=name,
                signature=sig_one_line,
                line=line_number_at(raw, pos),
                body=body,
            )
        )
    return methods


def categorize(method: MethodInfo, file_text: str) -> tuple[list[str], list[str]]:
    haystack = "\n".join([method.signature, method.body, file_text[:3000], method.class_bases])
    categories: set[str] = set()
    signals: list[str] = []
    for category, patterns in CATEGORY_PATTERNS.items():
        for pattern in patterns:
            if re.search(pattern, haystack):
                categories.add(category)
                signals.append(pattern)
                break
    if method.function_name in LIFECYCLE_NAMES and any(base in method.class_bases for base in UNITY_BASES):
        categories.add("SceneLifecycle")
        signals.append(f"lifecycle:{method.function_name}")
    if any(base in method.class_bases for base in UNITY_BASES):
        categories.add("ClassUnityBase")
        signals.append(f"class_base:{method.class_bases}")
    if re.search(r"\[(SerializeField|Header|HideInInspector|CreateAssetMenu|ContextMenu|MenuItem)\b", method.signature):
        categories.add("UnityAttribute")
        signals.append("unity_attribute")
    if "using UnityEngine" in file_text and not categories:
        categories.add("OtherUnity")
        signals.append("file_using_unityengine")
    return sorted(categories), sorted(set(signals))


def status_for(categories: list[str]) -> tuple[str, str, str]:
    if not categories:
        return "", "", ""
    statuses = [CATEGORY_STATUS.get(category, ("PENDING", "", ""))[0] for category in categories]
    if all(status == "OUT_OF_SCOPE" for status in statuses):
        status = "OUT_OF_SCOPE"
    elif "PENDING" in statuses:
        status = "PENDING"
    elif "PARTIAL" in statuses:
        status = "PARTIAL"
    elif all(status == "DONE" for status in statuses):
        status = "DONE"
    else:
        status = "PARTIAL"
    replacement = "; ".join(
        f"{category}: {CATEGORY_STATUS.get(category, ('PENDING', 'Needs manual review'))[1]}"
        for category in categories
    )
    evidence = "; ".join(
        f"{category}: "
        + ", ".join(
            str((HEADLESS_ROOT / rel).relative_to(ROOT)).replace("\\", "/")
            for rel in HEADLESS_EVIDENCE.get(category, [])
            if (HEADLESS_ROOT / rel).exists()
        )
        for category in categories
        if category in HEADLESS_EVIDENCE
    )
    return status, replacement, evidence


def main() -> None:
    rows = []
    file_count = 0
    method_count = 0
    for path in SOURCE_ROOT.rglob("*.cs"):
        if any(part in {"Library", "Temp", "obj", "bin"} for part in path.parts):
            continue
        file_count += 1
        try:
            file_text = path.read_text(encoding="utf-8-sig")
        except UnicodeDecodeError:
            file_text = path.read_text(encoding="cp949", errors="ignore")
        methods = parse_methods(path)
        method_count += len(methods)
        for method in methods:
            categories, signals = categorize(method, file_text)
            if not categories:
                continue
            status, replacement, evidence = status_for(categories)
            rows.append(
                {
                    "path": method.path,
                    "line": method.line,
                    "class": method.class_name,
                    "class_bases": method.class_bases,
                    "function": method.function_name,
                    "signature": method.signature,
                    "unity_dependency_categories": "|".join(categories),
                    "signals": "|".join(signals),
                    "headless_status": status,
                    "headless_replacement": replacement,
                    "headless_evidence": evidence,
                }
            )

    DOCS_ROOT.mkdir(parents=True, exist_ok=True)
    csv_path = DOCS_ROOT / "headless_unity_dependent_functions.csv"
    with csv_path.open("w", newline="", encoding="utf-8-sig") as f:
        writer = csv.DictWriter(f, fieldnames=list(rows[0].keys()) if rows else [])
        writer.writeheader()
        writer.writerows(rows)

    status_counts: dict[str, int] = {}
    category_counts: dict[str, int] = {}
    for row in rows:
        status_counts[row["headless_status"]] = status_counts.get(row["headless_status"], 0) + 1
        for category in row["unity_dependency_categories"].split("|"):
            category_counts[category] = category_counts.get(category, 0) + 1

    in_scope_total = sum(
        count for status, count in status_counts.items() if status != "OUT_OF_SCOPE"
    )
    done = status_counts.get("DONE", 0)
    partial = status_counts.get("PARTIAL", 0)
    pending = status_counts.get("PENDING", 0)
    out_of_scope = status_counts.get("OUT_OF_SCOPE", 0)

    summary_path = DOCS_ROOT / "headless_unity_dependency_progress.md"
    lines = [
        "# Headless Unity Dependency Progress",
        "",
        "## Scope",
        "",
        "- Source root inspected: `DCGO/Assets`",
        "- This counts Unity-dependent original C# functions/methods, then compares them with current `src/HeadlessDCGO.Engine/Headless` replacement infrastructure.",
        "- `DONE` means the Unity dependency category has a concrete Headless v0 replacement API that builds.",
        "- `PARTIAL` means a Headless replacement exists, but does not fully cover all original Unity semantics.",
        "- `OUT_OF_SCOPE` means visual/client-only Unity behavior is intentionally excluded from the headless runtime.",
        "",
        "## Counts",
        "",
        f"- C# files inspected: {file_count}",
        f"- Methods parsed: {method_count}",
        f"- Unity-dependent methods documented: {len(rows)}",
        f"- In-scope replacement target: {in_scope_total}",
        f"- DONE: {done}",
        f"- PARTIAL: {partial}",
        f"- PENDING: {pending}",
        f"- OUT_OF_SCOPE: {out_of_scope}",
        "",
        "## Progress",
        "",
        f"- Strict completed progress: **{done} / {in_scope_total}** in-scope Unity-dependent functions",
        f"- Implemented or partially covered: **{done + partial} / {in_scope_total}** in-scope Unity-dependent functions",
        f"- Whole detected set, including out-of-scope UI/client functions: **{done} / {len(rows)}** DONE",
        "",
        "## Status Counts",
        "",
        "| status | count |",
        "|---|---:|",
    ]
    for status in sorted(status_counts):
        lines.append(f"| {status} | {status_counts[status]} |")
    lines.extend([
        "",
        "## Category Counts",
        "",
        "| category | method count |",
        "|---|---:|",
    ])
    for category, count in sorted(category_counts.items()):
        lines.append(f"| {category} | {count} |")
    lines.extend([
        "",
        "## Output",
        "",
        "- Detailed function list: `docs/headless_unity_dependent_functions.csv`",
    ])
    summary_path.write_text("\n".join(lines) + "\n", encoding="utf-8")

    print(f"csv={csv_path}")
    print(f"summary={summary_path}")
    print(f"files={file_count}")
    print(f"methods={method_count}")
    print(f"unity_dependent={len(rows)}")
    print(f"in_scope={in_scope_total}")
    print(f"done={done}")
    print(f"partial={partial}")
    print(f"pending={pending}")
    print(f"out_of_scope={out_of_scope}")


if __name__ == "__main__":
    main()
