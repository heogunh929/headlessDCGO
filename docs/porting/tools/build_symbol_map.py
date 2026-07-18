#!/usr/bin/env python3
"""
build_symbol_map.py — regenerate docs/porting/symbol_map.csv and the coverage report.

PURPOSE
  Weak porting models (Haiku / local LLM) fail card porting by *symbol resolution*:
  they declare a real mirror surface "absent" and STOP falsely. This table lets them
  look symbols up instead of searching. Every row is verified by a live mirror grep at
  generation time, so the table never claims a symbol exists when it does not.

DATA MODEL (all AS-IS, i.e. original `DCGO/` surface)
  - AS-IS symbol inventory + per-card frequency comes from the IR DB
    (docs/porting/card_ir.sqlite):
      * card.commons / card.keywords / card.actions  (JSON arrays: trigger gates,
        *Class factories, Select*Effect UI classes)
      * card_primitive.name                          (factory-level primitives)
    port_status in the DB is STALE and ignored.
  - The mirror surface is the live tree under MIRROR_ROOT (src/), scanned fresh.

OUTPUTS
  - docs/porting/symbol_map.csv      (machine-readable lookup table)
  - docs/porting/symbol_map_coverage.md (frequency-weighted coverage + gap list)

RE-RUN after porting more cards (adds verified pairs) or editing the curated seed:
  python3 docs/porting/tools/build_symbol_map.py

NO engine code is modified and NO dotnet/build is invoked.
"""
import csv
import json
import os
import re
import sqlite3
import sys
from collections import Counter, defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
PORTING_DIR = os.path.dirname(HERE)                      # docs/porting
REPO = os.path.dirname(os.path.dirname(PORTING_DIR))     # repo root
MIRROR_ROOT = os.path.join(REPO, "src")
IR_DB = os.path.join(PORTING_DIR, "card_ir.sqlite")
SEED = os.path.join(PORTING_DIR, "symbol_map_seed.json")
OUT_CSV = os.path.join(PORTING_DIR, "symbol_map.csv")
OUT_COV = os.path.join(PORTING_DIR, "symbol_map_coverage.md")

# Files that are not part of the portable mirror surface (tests, obj/bin).
SKIP_DIR = re.compile(r"(^|/)(bin|obj|Tests?|\.git)(/|$)", re.IGNORECASE)


def load_asis_inventory():
    """Return (freq: symbol->#cards, sources: symbol->set(column))."""
    conn = sqlite3.connect(IR_DB)
    freq = Counter()
    sources = defaultdict(set)
    rows = conn.execute("select card_id, commons, keywords, actions from card").fetchall()
    ncards = len(rows)
    for _cid, commons, keywords, actions in rows:
        seen = set()
        for col, label in ((commons, "commons"), (keywords, "keywords"), (actions, "actions")):
            if not col:
                continue
            try:
                arr = json.loads(col)
            except Exception:
                arr = []
            for s in arr:
                seen.add(s)
                sources[s].add(label)
        for s in seen:
            freq[s] += 1
    # factory-level primitives (counted per distinct card)
    prim_cards = defaultdict(set)
    for cid, name in conn.execute("select card_id, name from card_primitive"):
        prim_cards[name].add(cid)
        sources[name].add("primitive")
    for name, cids in prim_cards.items():
        # do not double count if already present via other columns; take max
        freq[name] = max(freq[name], len(cids))
    conn.close()
    return freq, sources, ncards


_BLOCK_COMMENT = re.compile(r"/\*.*?\*/", re.DOTALL)


def strip_comments(txt):
    """Blank out C# comments so mentions inside `// AS-IS ...` notes do NOT count
    as real callable surface. Preserves line count (comments -> blanks)."""
    txt = _BLOCK_COMMENT.sub(lambda m: "\n" * m.group(0).count("\n"), txt)
    out = []
    for line in txt.splitlines():
        idx = line.find("//")
        out.append(line if idx < 0 else line[:idx])
    return "\n".join(out)


def build_mirror_index():
    """Walk mirror .cs files once. Return (files, text_cache) where text_cache holds
    the COMMENT-STRIPPED source (so grep-verification only sees real code)."""
    files = []
    for dirpath, dirnames, filenames in os.walk(MIRROR_ROOT):
        if SKIP_DIR.search(dirpath):
            continue
        for fn in filenames:
            if fn.endswith(".cs"):
                files.append(os.path.join(dirpath, fn))
    text_cache = {}
    for f in files:
        try:
            with open(f, "r", encoding="utf-8", errors="replace") as fh:
                raw = fh.read()
        except Exception:
            raw = ""
        text_cache[f] = strip_comments(raw)
    return files, text_cache


def find_symbol(sym, files, text_cache):
    """Return (present_paths, defsite) for one symbol."""
    word = re.compile(r"\b" + re.escape(sym) + r"\b")
    # definition patterns (C#): type/class/enum decl, or method/property def
    def_pat = re.compile(
        r"\b(class|struct|interface|enum|record)\s+" + re.escape(sym) + r"\b"
        r"|(?:public|private|internal|protected|static|async|override|virtual|sealed)\b"
        r"[^\n=;]*\b" + re.escape(sym) + r"\s*[\(\{]"
    )
    present = []
    defsites = []  # (is_framework, rel, lineno)
    for f in files:
        txt = text_cache[f]
        if sym not in txt:
            continue
        if not word.search(txt):
            continue
        rel = os.path.relpath(f, REPO)
        present.append(rel)
        is_fw = "/CardEffect/" not in rel  # framework/substrate def preferred over a card usage
        for i, line in enumerate(txt.splitlines(), 1):
            if sym in line and def_pat.search(line):
                defsites.append((0 if is_fw else 1, rel, i))
                break
    present.sort()
    # best defsite: framework first, then card files
    defsite = None
    if defsites:
        defsites.sort(key=lambda t: (t[0], t[1]))
        defsite = (defsites[0][1], defsites[0][2])
    return present, defsite


def load_seed():
    if not os.path.exists(SEED):
        return {}
    with open(SEED, "r", encoding="utf-8") as fh:
        data = json.load(fh)
    return {row["asis_symbol"]: row for row in data.get("rows", [])}


def main():
    freq, sources, ncards = load_asis_inventory()
    files, text_cache = build_mirror_index()
    seed = load_seed()

    symbols = sorted(set(freq) | set(seed))
    rows = []
    covered_weight = 0
    total_weight = 0
    gaps = []  # (freq, symbol)
    for sym in symbols:
        f = freq.get(sym, 0)
        total_weight += f
        sd = seed.get(sym, {})
        # mirror symbol name: seed override, else identity
        mirror_symbol = sd.get("mirror_symbol", sym)
        # verify_token: the real single token to grep-verify (defaults to mirror_symbol).
        # Lets a row DISPLAY a human phrase (e.g. "SelectPermanentEffect.Mode.PutLibraryBottom
        # (fused select+bounce)") while still being existence-checked against a real symbol.
        verify_token = sd.get("verify_token", mirror_symbol)
        present, defsite = find_symbol(verify_token, files, text_cache)
        absent_marker = sd.get("absent")  # curated "no mirror surface" flag
        if absent_marker:
            status = "ABSENT"
        elif present:
            status = "OK"
            covered_weight += f
        else:
            status = "MISSING"  # not curated-absent, but grep found nothing → gap
        if status in ("MISSING", "ABSENT"):
            gaps.append((f, sym))
        # path hint: curated override, else framework def-site, else first framework usage, else any
        fw_present = [p for p in present if "/CardEffect/" not in p]
        mirror_path = sd.get("mirror_path") or (
            defsite[0] if defsite else (fw_present[0] if fw_present else (present[0] if present else "")))
        rows.append({
            "asis_symbol": sym,
            "freq_cards": f,
            "asis_source": "|".join(sorted(sources.get(sym, []))) or "seed",
            "status": status,
            "mirror_symbol": mirror_symbol if status != "ABSENT" else "",
            "mirror_path": mirror_path if status != "ABSENT" else "",
            "signature_delta": sd.get("signature_delta", ""),
            "example_card": sd.get("example_card", ""),
            "example_line": sd.get("example_line", ""),
            "notes": sd.get("notes", ""),
        })

    # order: highest frequency first (most useful lookups on top)
    rows.sort(key=lambda r: (-r["freq_cards"], r["asis_symbol"]))
    fieldnames = ["asis_symbol", "freq_cards", "asis_source", "status", "mirror_symbol",
                  "mirror_path", "signature_delta", "example_card", "example_line", "notes"]
    with open(OUT_CSV, "w", newline="", encoding="utf-8") as fh:
        w = csv.DictWriter(fh, fieldnames=fieldnames)
        w.writeheader()
        for r in rows:
            w.writerow(r)

    # coverage report
    n_ok = sum(1 for r in rows if r["status"] == "OK")
    n_absent = sum(1 for r in rows if r["status"] == "ABSENT")
    n_missing = sum(1 for r in rows if r["status"] == "MISSING")
    pct = (100.0 * covered_weight / total_weight) if total_weight else 0.0
    gaps.sort(reverse=True)
    with open(OUT_COV, "w", encoding="utf-8") as fh:
        fh.write("# symbol_map coverage (auto-generated by build_symbol_map.py)\n\n")
        fh.write(f"- AS-IS cards in IR DB: **{ncards}**\n")
        fh.write(f"- Distinct AS-IS symbols: **{len(symbols)}**\n")
        fh.write(f"- Rows OK (mirror surface grep-verified): **{n_ok}**\n")
        fh.write(f"- Rows ABSENT (curated: no mirror surface = infra gap): **{n_absent}**\n")
        fh.write(f"- Rows MISSING (grep found nothing, not yet curated): **{n_missing}**\n")
        fh.write(f"- **Frequency-weighted coverage: {pct:.1f}%** "
                 f"({covered_weight} / {total_weight} symbol-card uses resolve to a verified mirror surface)\n\n")
        fh.write("## Uncovered high-frequency symbols (= remaining infra/curation gaps)\n\n")
        fh.write("| freq (cards) | AS-IS symbol | status |\n|---:|---|---|\n")
        status_by_sym = {r["asis_symbol"]: r["status"] for r in rows}
        for f, sym in gaps[:60]:
            fh.write(f"| {f} | {sym} | {status_by_sym[sym]} |\n")

    print(f"wrote {OUT_CSV}  ({len(rows)} rows)")
    print(f"wrote {OUT_COV}")
    print(f"OK={n_ok} ABSENT={n_absent} MISSING={n_missing}  weighted-coverage={pct:.1f}%")


if __name__ == "__main__":
    main()
