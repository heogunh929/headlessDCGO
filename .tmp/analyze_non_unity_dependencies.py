from __future__ import annotations

import csv
import json
import re
from collections import defaultdict
from pathlib import Path


ROOT = Path(r"E:\headlessDCGO_new")
PROJECT_ROOT = ROOT / "DCGO"
ASSETS_ROOT = PROJECT_ROOT / "Assets"
DOCS_ROOT = ROOT / "docs"

DETAIL_CSV = DOCS_ROOT / "dotnet_non_unity_dependency_details.csv"
SUMMARY_CSV = DOCS_ROOT / "dotnet_non_unity_dependency_summary.csv"
SUMMARY_MD = DOCS_ROOT / "dotnet_non_unity_dependency_summary.md"


SYSTEM_PREFIXES = (
    "System",
    "Microsoft",
)

UNITY_PREFIXES = (
    "UnityEngine",
    "UnityEditor",
    "Unity",
)


DEPENDENCY_RULES = [
    (
        "Photon",
        ["Photon", "Photon.Pun", "Photon.Realtime", "Photon.Chat", "ExitGames.Client.Photon"],
        ["PhotonNetwork", "PhotonView", "PunRPC", "RpcTarget", "IPunObservable", "RaiseEventOptions"],
        "Network",
        "REMOVE_OR_REPLACE",
        "Client network dependency. Headless/RL should replace it with deterministic local match/session events.",
    ),
    (
        "WebSocketSharp",
        ["WebSocketSharp"],
        ["WebSocketSharp"],
        "Network",
        "REMOVE_OR_REPLACE",
        "WebSocket transport dependency from Photon stack. Not needed for local headless simulation.",
    ),
    (
        "TextMeshPro",
        ["TMPro"],
        ["TMP_Text", "TextMeshPro", "TMP_InputField"],
        "UI",
        "IGNORE_FOR_HEADLESS",
        "Text rendering/UI dependency. Keep out of headless runtime.",
    ),
    (
        "DOTween",
        ["DG.Tweening"],
        ["DOTween", "Tweener", "Tween"],
        "Animation",
        "IGNORE_FOR_HEADLESS",
        "Tween/animation dependency. Strip from deterministic rules and action processing.",
    ),
    (
        "Coffee.UIExtensions",
        ["Coffee.UIExtensions"],
        ["UIParticle", "UIEffect", "Unmask"],
        "UIEffect",
        "IGNORE_FOR_HEADLESS",
        "UGUI visual effect add-on. Headless engine should not depend on it.",
    ),
    (
        "WebGLInput",
        ["WebGLInput"],
        ["WebGLInput"],
        "PlatformInput",
        "IGNORE_FOR_HEADLESS",
        "WebGL browser input helper. Not relevant to headless engine.",
    ),
    (
        "NetPyoung.WebP",
        ["NetPyoun", "unity.webp"],
        ["WebP"],
        "ImageCodec",
        "IGNORE_FOR_HEADLESS",
        "Image/WebP codec package. Card image display is outside headless runtime.",
    ),
    (
        "Cinemachine",
        ["Cinemachine"],
        ["Cinemachine"],
        "Camera",
        "IGNORE_FOR_HEADLESS",
        "Unity camera package. Headless runtime should not depend on it.",
    ),
    (
        "Shapes2D",
        ["Shapes2D"],
        ["Shapes2D"],
        "VisualAddOn",
        "IGNORE_FOR_HEADLESS",
        "2D shape rendering add-on. Not needed for headless simulation.",
    ),
    (
        "AutoLayout3D",
        ["AutoLayout3D"],
        ["AutoLayout3D"],
        "VisualAddOn",
        "IGNORE_FOR_HEADLESS",
        "3D layout/UI add-on. Not needed for headless simulation.",
    ),
    (
        "WindowsRuntimeApi",
        ["Windows.Foundation", "Windows.Networking", "Windows.Storage"],
        ["Windows.Foundation", "Windows.Networking", "Windows.Storage"],
        "PlatformAPI",
        "REMOVE_OR_REPLACE",
        "UWP/Windows API usage. Replace with standard .NET APIs only if transport code is kept.",
    ),
    (
        "JetBrains.Annotations",
        ["JetBrains.Annotations"],
        ["JetBrains.Annotations"],
        "Annotation",
        "IGNORE_FOR_HEADLESS",
        "Static-analysis annotations. Safe to remove or replace with nullable annotations.",
    ),
    (
        "ProfanityFilter",
        ["ProfanityFilter", "ProfanityFilter.Interfaces"],
        ["ProfanityFilter"],
        "ClientTextFiltering",
        "IGNORE_FOR_HEADLESS",
        "Chat/input text filtering. Not needed for local AI/RL simulation.",
    ),
    (
        "Newtonsoft.Json",
        ["Newtonsoft.Json"],
        ["JsonConvert", "JObject", "JArray", "JToken"],
        "Serialization",
        "KEEP_OR_REPLACE",
        "Can be replaced with System.Text.Json or added as a normal .NET NuGet dependency if needed.",
    ),
    (
        "System.Runtime.CompilerServices.Unsafe",
        ["System.Runtime.CompilerServices.Unsafe"],
        [],
        "NuGetRuntime",
        "KEEP_IF_REQUIRED",
        "Already a NuGet package in Unity manifest; only keep if a chosen .NET dependency requires it.",
    ),
]


def read_text(path: Path) -> str:
    try:
        return path.read_text(encoding="utf-8-sig")
    except UnicodeDecodeError:
        return path.read_text(encoding="cp949", errors="ignore")


def classify(name: str, line: str = "", path: str = "") -> tuple[str, str, str, str]:
    normalized = name.strip()
    haystack = f"{normalized} {line} {path}"
    for dependency, namespaces, tokens, category, action, notes in DEPENDENCY_RULES:
        if any(normalized == ns or normalized.startswith(ns + ".") for ns in namespaces):
            return dependency, category, action, notes
        if any(token and token in haystack for token in tokens):
            return dependency, category, action, notes

    if normalized.startswith("com.netpyoung"):
        return (
            "NetPyoung.WebP",
            "ImageCodec",
            "IGNORE_FOR_HEADLESS",
            "Unity WebP package. Card image rendering is outside headless runtime.",
        )

    if normalized.startswith("org.nuget."):
        return (
            normalized,
            "NuGetRuntime",
            "KEEP_IF_REQUIRED",
            "NuGet package from Unity manifest. Re-evaluate after final .NET dependency set is known.",
        )

    root = normalized.split(".", 1)[0]
    return (
        normalized if normalized else "Unknown",
        "UnknownNonSystem",
        "REVIEW",
        f"Non-System non-Unity namespace or package not covered by known rules. Root={root}",
    )


def is_system_or_unity_namespace(namespace: str) -> bool:
    return namespace.startswith(SYSTEM_PREFIXES) or namespace.startswith(UNITY_PREFIXES)


def normalize_using(raw: str) -> str:
    value = raw.strip()
    if "=" in value:
        value = value.split("=", 1)[1].strip()
    if value.startswith("static "):
        value = value[len("static ") :].strip()
    return value


def collect_using_details() -> list[dict[str, str]]:
    rows: list[dict[str, str]] = []
    using_pattern = re.compile(r"^\s*using\s+(?!\()([^;\n]+);", re.MULTILINE)

    for path in ASSETS_ROOT.rglob("*.cs"):
        if any(part in {"Library", "Temp", "Logs", "obj", "bin"} for part in path.parts):
            continue
        text = read_text(path)
        rel = str(path.relative_to(ROOT)).replace("\\", "/")
        for match in using_pattern.finditer(text):
            namespace = normalize_using(match.group(1))
            if not namespace or is_system_or_unity_namespace(namespace):
                continue
            if namespace.startswith("DCGO."):
                continue
            dependency, category, action, notes = classify(namespace, match.group(0), rel)
            rows.append(
                {
                    "source_kind": "using",
                    "dependency": dependency,
                    "category": category,
                    "dotnet_action": action,
                    "path": rel,
                    "line": str(text.count("\n", 0, match.start()) + 1),
                    "symbol": namespace,
                    "notes": notes,
                }
            )
    return rows


def collect_token_details() -> list[dict[str, str]]:
    rows: list[dict[str, str]] = []
    token_rules = []
    for dependency, _namespaces, tokens, category, action, notes in DEPENDENCY_RULES:
        for token in tokens:
            if token:
                token_rules.append((dependency, token, category, action, notes))

    for path in ASSETS_ROOT.rglob("*.cs"):
        if any(part in {"Library", "Temp", "Logs", "obj", "bin"} for part in path.parts):
            continue
        text = read_text(path)
        rel = str(path.relative_to(ROOT)).replace("\\", "/")
        for dependency, token, category, action, notes in token_rules:
            for match in re.finditer(rf"\b{re.escape(token)}\b", text):
                rows.append(
                    {
                        "source_kind": "symbol",
                        "dependency": dependency,
                        "category": category,
                        "dotnet_action": action,
                        "path": rel,
                        "line": str(text.count("\n", 0, match.start()) + 1),
                        "symbol": token,
                        "notes": notes,
                    }
                )
    return rows


def collect_manifest_details() -> list[dict[str, str]]:
    rows: list[dict[str, str]] = []
    manifest = PROJECT_ROOT / "Packages" / "manifest.json"
    if not manifest.exists():
        return rows
    data = json.loads(read_text(manifest))
    dependencies = data.get("dependencies", {})
    for package, version in dependencies.items():
        if package.startswith("com.unity."):
            continue
        dependency, category, action, notes = classify(package, str(version), "Packages/manifest.json")
        rows.append(
            {
                "source_kind": "manifest",
                "dependency": dependency,
                "category": category,
                "dotnet_action": action,
                "path": str(manifest.relative_to(ROOT)).replace("\\", "/"),
                "line": "",
                "symbol": f"{package}={version}",
                "notes": notes,
            }
        )
    for registry in data.get("scopedRegistries", []):
        rows.append(
            {
                "source_kind": "scoped_registry",
                "dependency": registry.get("name", "UnknownRegistry"),
                "category": "PackageRegistry",
                "dotnet_action": "REVIEW",
                "path": str(manifest.relative_to(ROOT)).replace("\\", "/"),
                "line": "",
                "symbol": registry.get("url", ""),
                "notes": "Unity package registry. Not a runtime dependency, but relevant if package restore is reproduced.",
            }
        )
    return rows


def collect_asmdef_details() -> list[dict[str, str]]:
    rows: list[dict[str, str]] = []
    for path in ASSETS_ROOT.rglob("*.asmdef"):
        rel = str(path.relative_to(ROOT)).replace("\\", "/")
        try:
            data = json.loads(read_text(path))
        except json.JSONDecodeError:
            data = {}
        name = data.get("name", path.stem)
        if name.startswith("Unity"):
            continue
        if name.startswith("DCGO") or name in {"SelectCardEffect"}:
            continue
        dependency, category, action, notes = classify(name, "", rel)
        if category == "UnknownNonSystem":
            if "Photon" in rel:
                dependency, category, action, notes = classify("Photon", "", rel)
            elif "UIEffect" in rel or "UIParticle" in rel or "Unmask" in rel:
                dependency, category, action, notes = classify("Coffee.UIExtensions", "", rel)
            elif "AutoLayout3D" in rel:
                dependency, category, action, notes = classify("AutoLayout3D", "", rel)
            elif "Shapes2D" in rel:
                dependency, category, action, notes = classify("Shapes2D", "", rel)
        rows.append(
            {
                "source_kind": "asmdef",
                "dependency": dependency,
                "category": category,
                "dotnet_action": action,
                "path": rel,
                "line": "",
                "symbol": name,
                "notes": notes,
            }
        )
    return rows


def collect_plugin_binary_details() -> list[dict[str, str]]:
    rows: list[dict[str, str]] = []
    for path in ASSETS_ROOT.rglob("*.dll"):
        rel = str(path.relative_to(ROOT)).replace("\\", "/")
        name = path.stem
        dependency, category, action, notes = classify(name, "", rel)
        if category == "UnknownNonSystem":
            if "DOTween" in rel:
                dependency, category, action, notes = classify("DG.Tweening", "", rel)
            elif "Photon" in rel:
                dependency, category, action, notes = classify("Photon", "", rel)
        rows.append(
            {
                "source_kind": "plugin_binary",
                "dependency": dependency,
                "category": category,
                "dotnet_action": action,
                "path": rel,
                "line": "",
                "symbol": path.name,
                "notes": notes,
            }
        )
    return rows


def aggregate(rows: list[dict[str, str]]) -> list[dict[str, str]]:
    grouped: dict[str, list[dict[str, str]]] = defaultdict(list)
    for row in rows:
        grouped[row["dependency"]].append(row)

    summary_rows = []
    action_rank = {
        "REMOVE_OR_REPLACE": 0,
        "REVIEW": 1,
        "KEEP_OR_REPLACE": 2,
        "KEEP_IF_REQUIRED": 3,
        "IGNORE_FOR_HEADLESS": 4,
    }

    for dependency, items in sorted(grouped.items()):
        categories = sorted(set(item["category"] for item in items))
        actions = sorted(set(item["dotnet_action"] for item in items), key=lambda item: action_rank.get(item, 99))
        files = sorted(set(item["path"] for item in items))
        symbols = sorted(set(item["symbol"] for item in items))[:20]
        notes = sorted(set(item["notes"] for item in items))[:5]
        summary_rows.append(
            {
                "dependency": dependency,
                "categories": "|".join(categories),
                "dotnet_action": actions[0] if actions else "REVIEW",
                "occurrence_count": str(len(items)),
                "file_count": str(len(files)),
                "source_kinds": "|".join(sorted(set(item["source_kind"] for item in items))),
                "sample_symbols": "|".join(symbols),
                "sample_paths": "|".join(files[:10]),
                "notes": " / ".join(notes),
            }
        )

    return sorted(
        summary_rows,
        key=lambda row: (-int(row["file_count"]), row["dependency"]),
    )


def write_csv(path: Path, rows: list[dict[str, str]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    fieldnames = list(rows[0].keys()) if rows else []
    with path.open("w", newline="", encoding="utf-8-sig") as f:
        writer = csv.DictWriter(f, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(rows)


def write_summary(summary_rows: list[dict[str, str]], detail_rows: list[dict[str, str]]) -> None:
    action_counts: dict[str, int] = defaultdict(int)
    category_counts: dict[str, int] = defaultdict(int)
    for row in summary_rows:
        action_counts[row["dotnet_action"]] += 1
        for category in row["categories"].split("|"):
            category_counts[category] += 1

    detail_action_counts: dict[str, int] = defaultdict(int)
    for row in detail_rows:
        detail_action_counts[row["dotnet_action"]] += 1

    lines = [
        "# .NET Non-Unity Dependency Inventory",
        "",
        "## Scope",
        "",
        "- Source root inspected: `DCGO/Assets`",
        "- Excluded from this report: `System.*`, `Microsoft.*`, `UnityEngine.*`, `UnityEditor.*`, and `com.unity.*` package names.",
        "- Included: third-party packages, client SDKs, networking libraries, UI/text/tween plugins, manifest non-Unity packages, asmdef modules, and plugin DLLs.",
        "",
        "## Summary",
        "",
        f"- Unique non-Unity dependency groups: {len(summary_rows)}",
        f"- Detailed dependency occurrences: {len(detail_rows)}",
        "",
        "## Action Counts By Dependency Group",
        "",
        "| action | dependency groups |",
        "|---|---:|",
    ]
    for action, count in sorted(action_counts.items()):
        lines.append(f"| {action} | {count} |")
    lines.extend([
        "",
        "## Action Counts By Occurrence",
        "",
        "| action | occurrences |",
        "|---|---:|",
    ])
    for action, count in sorted(detail_action_counts.items()):
        lines.append(f"| {action} | {count} |")
    lines.extend([
        "",
        "## Category Counts",
        "",
        "| category | dependency groups |",
        "|---|---:|",
    ])
    for category, count in sorted(category_counts.items()):
        lines.append(f"| {category} | {count} |")

    lines.extend([
        "",
        "## Top Dependencies By File Count",
        "",
        "| dependency | action | files | occurrences | categories |",
        "|---|---|---:|---:|---|",
    ])
    for row in summary_rows[:30]:
        lines.append(
            f"| {row['dependency']} | {row['dotnet_action']} | {row['file_count']} | {row['occurrence_count']} | {row['categories']} |"
        )
    lines.extend([
        "",
        "## Output",
        "",
        "- Summary CSV: `docs/dotnet_non_unity_dependency_summary.csv`",
        "- Detail CSV: `docs/dotnet_non_unity_dependency_details.csv`",
    ])
    SUMMARY_MD.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> None:
    rows = []
    rows.extend(collect_using_details())
    rows.extend(collect_token_details())
    rows.extend(collect_manifest_details())
    rows.extend(collect_asmdef_details())
    rows.extend(collect_plugin_binary_details())

    # Remove exact duplicates caused by symbol and using scans overlapping.
    seen = set()
    unique_rows = []
    for row in rows:
        key = tuple(row.items())
        if key in seen:
            continue
        seen.add(key)
        unique_rows.append(row)

    summary_rows = aggregate(unique_rows)
    write_csv(DETAIL_CSV, unique_rows)
    write_csv(SUMMARY_CSV, summary_rows)
    write_summary(summary_rows, unique_rows)

    print(f"detail={DETAIL_CSV}")
    print(f"summary={SUMMARY_CSV}")
    print(f"markdown={SUMMARY_MD}")
    print(f"dependency_groups={len(summary_rows)}")
    print(f"occurrences={len(unique_rows)}")
    for action in sorted(set(row["dotnet_action"] for row in summary_rows)):
        count = sum(1 for row in summary_rows if row["dotnet_action"] == action)
        print(f"{action}={count}")


if __name__ == "__main__":
    main()
