using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

// (CS-01) TO-BE 주석 스트리퍼 — TO-BE(포팅 트리)에서 AS-IS(원본)에 존재하지 않는 주석을 제거한다.
// Roslyn trivia만 조작하며, 토큰 시퀀스 동등성을 파일 단위로 기계 검증한 뒤에만 기록한다.
// 사용: dotnet run --project tools/CommentStripper -- <scope.list> <asisRoot> <tobeRoot> [--apply] [--report out.tsv]

Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

if (args.Length < 3)
{
    Console.Error.WriteLine("usage: <scope.list> <asisRoot> <tobeRoot> [--apply] [--report out.tsv]");
    return 2;
}

string scopeListPath = args[0];
string asisRoot = args[1];
string tobeRoot = args[2];
bool apply = args.Contains("--apply");
string? reportPath = null;
for (int i = 3; i < args.Length - 1; i++)
{
    if (args[i] == "--report") reportPath = args[i + 1];
}

string[] rels = File.ReadAllLines(scopeListPath)
    .Select(l => l.TrimEnd('\r'))
    .Where(l => l.Length > 0)
    .ToArray();

var report = new List<string>
{
    "rel\tunits_total\tunits_kept\tunits_deleted\tcomment_lines_deleted\tcomment_lines_kept\tphysical_lines_removed\ttoken_gate\tnote"
};

int filesChanged = 0, filesSkipped = 0, gateFail = 0;
long totUnitsDel = 0, totUnitsKept = 0, totLinesDel = 0, totLinesKept = 0, totPhysical = 0;
var failures = new List<string>();

// --verify <origRoot>: 이미 적용된 tobeRoot를 origRoot(적용 전 사본)와 토큰 동등성만으로 재대조한다.
string? verifyRoot = null;
for (int i = 3; i < args.Length - 1; i++)
{
    if (args[i] == "--verify") verifyRoot = args[i + 1];
}
if (verifyRoot is not null)
{
    int okCount = 0;
    var bad = new List<string>();
    foreach (string rel in rels)
    {
        string a = File.ReadAllText(Path.Combine(verifyRoot, rel));
        string b = File.ReadAllText(Path.Combine(tobeRoot, rel));
        string r = TokenEquivalent(a, b);
        if (r == "OK") okCount++; else bad.Add($"{rel}: {r}");
    }
    Console.WriteLine($"VERIFY token-equivalence: {okCount}/{rels.Length} OK");
    foreach (var x in bad) Console.Error.WriteLine("!! " + x);
    return bad.Count == 0 ? 0 : 1;
}

// --emptydoc (스테이지 2): 내용이 하나도 없는 XML 문서주석 블록을 통째로 삭제한다.
// AS-IS 대응 파일에 동일한 빈 껍데기가 실제로 있으면 보존(1스테이지와 동일한 판정 규칙).
if (args.Contains("--emptydoc"))
{
    var rows2 = new List<string>
    {
        "rel\tdoc_blocks\tempty_blocks\tdeleted\tkept_because_asis\tlines_removed\ttoken_gate"
    };
    int f2 = 0, del2 = 0, keptAsis2 = 0, lines2 = 0, gf2 = 0, empty2 = 0;
    var bad2 = new List<string>();

    foreach (string rel in rels)
    {
        string tobeP = Path.Combine(tobeRoot, rel);
        string asisP = Path.Combine(asisRoot, rel);
        if (!File.Exists(tobeP)) { bad2.Add($"{rel}: tobe missing"); continue; }

        byte[] bs = File.ReadAllBytes(tobeP);
        bool bom2 = bs.Length >= 3 && bs[0] == 0xEF && bs[1] == 0xBB && bs[2] == 0xBF;
        string txt = new UTF8Encoding(false).GetString(bs, bom2 ? 3 : 0, bs.Length - (bom2 ? 3 : 0));

        var asisEmpty = new HashSet<string>(StringComparer.Ordinal);
        if (File.Exists(asisP))
        {
            foreach (var e in FindDocBlocks(ReadTolerant(asisP, out _)))
            {
                if (e.IsEmpty) asisEmpty.Add(e.Norm);
            }
        }

        var blocks = FindDocBlocks(txt);
        var empties = blocks.Where(b => b.IsEmpty).ToList();
        var doomed2 = empties.Where(b => !asisEmpty.Contains(b.Norm)).ToList();
        int kAsis = empties.Count - doomed2.Count;
        empty2 += empties.Count;
        keptAsis2 += kAsis;

        if (doomed2.Count == 0)
        {
            if (empties.Count > 0)
                rows2.Add($"{rel}\t{blocks.Count}\t{empties.Count}\t0\t{kAsis}\t0\tNOOP");
            continue;
        }

        var units2 = doomed2
            .Select(b => new Unit(b.Span, b.Norm, b.StartLine, b.EndLine))
            .ToList();
        string newTxt = Strip(SourceText.From(txt), units2, out int removed2);

        string gate2 = TokenEquivalent(txt, newTxt);
        if (gate2 != "OK")
        {
            gf2++;
            bad2.Add($"{rel}: TOKEN-GATE-FAIL {gate2}");
            rows2.Add($"{rel}\t{blocks.Count}\t{empties.Count}\t0\t{kAsis}\t0\tFAIL:{gate2}");
            continue;
        }

        if (apply) File.WriteAllText(tobeP, newTxt, new UTF8Encoding(bom2));

        f2++;
        del2 += doomed2.Count;
        lines2 += removed2;
        rows2.Add($"{rel}\t{blocks.Count}\t{empties.Count}\t{doomed2.Count}\t{kAsis}\t{removed2}\tPASS");
    }

    Console.WriteLine($"[emptydoc] mode            : {(apply ? "APPLY" : "DRY-RUN")}");
    Console.WriteLine($"[emptydoc] files touched   : {f2}");
    Console.WriteLine($"[emptydoc] empty blocks    : {empty2}");
    Console.WriteLine($"[emptydoc] blocks deleted  : {del2}");
    Console.WriteLine($"[emptydoc] kept (in AS-IS) : {keptAsis2}");
    Console.WriteLine($"[emptydoc] lines removed   : {lines2}");
    Console.WriteLine($"[emptydoc] gate failures   : {gf2}");
    foreach (var x in bad2) Console.Error.WriteLine("!! " + x);
    if (reportPath is not null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, rows2);
        Console.WriteLine($"[emptydoc] report -> {reportPath}");
    }
    return bad2.Count == 0 ? 0 : 1;
}

foreach (string rel in rels)
{
    string asisPath = Path.Combine(asisRoot, rel);
    string tobePath = Path.Combine(tobeRoot, rel);

    if (!File.Exists(asisPath) || !File.Exists(tobePath))
    {
        failures.Add($"{rel}\tMISSING (asis={File.Exists(asisPath)} tobe={File.Exists(tobePath)})");
        report.Add($"{rel}\t0\t0\t0\t0\t0\t0\tSKIP\tmissing-file");
        filesSkipped++;
        continue;
    }

    string asisText = ReadTolerant(asisPath, out _);
    byte[] tobeBytes = File.ReadAllBytes(tobePath);
    bool tobeBom = tobeBytes.Length >= 3 && tobeBytes[0] == 0xEF && tobeBytes[1] == 0xBB && tobeBytes[2] == 0xBF;
    string tobeText = new UTF8Encoding(false).GetString(tobeBytes, tobeBom ? 3 : 0, tobeBytes.Length - (tobeBom ? 3 : 0));

    var asisSet = new HashSet<string>(StringComparer.Ordinal);
    foreach (var u in CollectUnits(asisText)) asisSet.Add(u.Norm);

    var tobeSource = SourceText.From(tobeText);
    var units = CollectUnits(tobeText);
    var doomed = units.Where(u => !asisSet.Contains(u.Norm)).ToList();
    var kept = units.Count - doomed.Count;

    long linesDel = doomed.Sum(u => (long)(u.EndLine - u.StartLine + 1));
    long linesKept = units.Where(u => asisSet.Contains(u.Norm)).Sum(u => (long)(u.EndLine - u.StartLine + 1));

    if (doomed.Count == 0)
    {
        report.Add($"{rel}\t{units.Count}\t{kept}\t0\t0\t{linesKept}\t0\tNOOP\t");
        totUnitsKept += kept;
        totLinesKept += linesKept;
        continue;
    }

    string newText = Strip(tobeSource, doomed, out int physicalRemoved);

    // 게이트 1: 토큰 시퀀스 동등성 (+ 전처리기 지시문 동등성 + 신규 파스 오류 없음)
    string gateNote = TokenEquivalent(tobeText, newText);
    if (gateNote != "OK")
    {
        gateFail++;
        failures.Add($"{rel}\tTOKEN-GATE-FAIL: {gateNote}");
        report.Add($"{rel}\t{units.Count}\t{kept}\t{doomed.Count}\t0\t{linesKept}\t0\tFAIL\t{gateNote}");
        filesSkipped++;
        continue;
    }

    if (apply)
    {
        var enc = new UTF8Encoding(tobeBom);
        File.WriteAllText(tobePath, newText, enc);
    }

    filesChanged++;
    totUnitsDel += doomed.Count;
    totUnitsKept += kept;
    totLinesDel += linesDel;
    totLinesKept += linesKept;
    totPhysical += physicalRemoved;
    report.Add($"{rel}\t{units.Count}\t{kept}\t{doomed.Count}\t{linesDel}\t{linesKept}\t{physicalRemoved}\tPASS\t");
}

Console.WriteLine($"mode           : {(apply ? "APPLY" : "DRY-RUN")}");
Console.WriteLine($"files in scope : {rels.Length}");
Console.WriteLine($"files changed  : {filesChanged}");
Console.WriteLine($"files untouched: {rels.Length - filesChanged - filesSkipped} (no deletable comment)");
Console.WriteLine($"files skipped  : {filesSkipped} (gate fail {gateFail})");
Console.WriteLine($"units deleted  : {totUnitsDel}");
Console.WriteLine($"units kept     : {totUnitsKept}");
Console.WriteLine($"comment lines deleted: {totLinesDel}");
Console.WriteLine($"comment lines kept   : {totLinesKept}");
Console.WriteLine($"physical lines removed: {totPhysical}");
foreach (var f in failures) Console.Error.WriteLine("!! " + f);

if (reportPath is not null)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
    File.WriteAllLines(reportPath, report);
    Console.WriteLine($"report -> {reportPath}");
}

return failures.Count == 0 ? 0 : 1;

// ---------------------------------------------------------------- helpers

static string ReadTolerant(string path, out string encName)
{
    byte[] bytes = File.ReadAllBytes(path);
    int off = (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) ? 3 : 0;
    var strict = new UTF8Encoding(false, throwOnInvalidBytes: true);
    try
    {
        encName = "utf-8";
        return strict.GetString(bytes, off, bytes.Length - off);
    }
    catch (DecoderFallbackException)
    {
        try
        {
            encName = "shift_jis";
            return Encoding.GetEncoding(932).GetString(bytes, off, bytes.Length - off);
        }
        catch
        {
            encName = "latin1";
            return Encoding.Latin1.GetString(bytes, off, bytes.Length - off);
        }
    }
}

// 주석 1단위. 줄 주석/단일줄 XML 주석은 물리 줄 단위로 쪼갠다. 블록 주석은 통째로 1단위.
static List<Unit> CollectUnits(string text)
{
    var src = SourceText.From(text);
    var tree = CSharpSyntaxTree.ParseText(src);
    var root = tree.GetRoot();
    var units = new List<Unit>();

    // 지시문(#pragma/#region ...) 안에 붙은 후행 주석도 주석이다. 구조화 trivia 안까지 훑되,
    // XML 문서주석 내부(descendIntoTrivia가 토해내는 Xml* 노드)는 이미 상위 단위로 잡히므로 지시문에 한정한다.
    var all = new List<SyntaxTrivia>(root.DescendantTrivia(descendIntoTrivia: false));
    foreach (var d in root.DescendantTrivia(descendIntoTrivia: false).Where(t => t.IsDirective))
    {
        var st = d.GetStructure();
        if (st is null) continue;
        all.AddRange(st.DescendantTrivia(descendIntoTrivia: false));
    }

    foreach (var trivia in all)
    {
        var kind = trivia.Kind();
        bool lineLike = kind == SyntaxKind.SingleLineCommentTrivia
                     || kind == SyntaxKind.SingleLineDocumentationCommentTrivia;
        bool blockLike = kind == SyntaxKind.MultiLineCommentTrivia
                      || kind == SyntaxKind.MultiLineDocumentationCommentTrivia;
        if (!lineLike && !blockLike) continue;

        var span = trivia.FullSpan;
        if (span.Length == 0) continue;

        if (blockLike)
        {
            var s = TrimSpan(src, span);
            if (s.Length == 0) continue;
            units.Add(new Unit(s,
                NormalizeBlock(src.ToString(s)),
                src.Lines.GetLineFromPosition(s.Start).LineNumber,
                src.Lines.GetLineFromPosition(s.End - 1).LineNumber));
            continue;
        }

        int l0 = src.Lines.GetLineFromPosition(span.Start).LineNumber;
        int l1 = src.Lines.GetLineFromPosition(Math.Max(span.Start, span.End - 1)).LineNumber;
        for (int ln = l0; ln <= l1; ln++)
        {
            var line = src.Lines[ln];
            var inter = span.Intersection(line.Span);
            if (inter is null) continue;
            var s = TrimSpan(src, inter.Value);
            if (s.Length == 0) continue;
            units.Add(new Unit(s, NormalizeLine(src.ToString(s)), ln, ln));
        }
    }

    return units;
}

static TextSpan TrimSpan(SourceText src, TextSpan s)
{
    int a = s.Start, b = s.End;
    while (a < b && char.IsWhiteSpace(src[a])) a++;
    while (b > a && char.IsWhiteSpace(src[b - 1])) b--;
    return TextSpan.FromBounds(a, b);
}

static string Collapse(string s)
{
    var parts = s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
    return string.Join(' ', parts);
}

static string NormalizeLine(string raw)
{
    int i = 0;
    while (i < raw.Length && raw[i] == '/') i++;
    return Collapse(raw[i..]);
}

static string NormalizeBlock(string raw)
{
    string s = raw;
    if (s.StartsWith("/*")) s = s[2..];
    if (s.EndsWith("*/")) s = s[..^2];
    return Collapse(s);
}

// 삭제 스팬을 문자 단위로 마스킹한 뒤 줄 단위로 재조립.
// 잔여가 공백뿐인 줄은 줄바꿈까지 통째로 제거하고, 그 외 줄은 후행 공백만 정리한다.
static string Strip(SourceText src, List<Unit> doomed, out int physicalRemoved)
{
    var del = new bool[src.Length];
    foreach (var u in doomed)
        for (int i = u.Span.Start; i < u.Span.End; i++) del[i] = true;

    var sb = new StringBuilder(src.Length);
    physicalRemoved = 0;

    foreach (var line in src.Lines)
    {
        var ls = line.Span;
        bool touched = false;
        for (int i = ls.Start; i < ls.End; i++)
        {
            if (del[i]) { touched = true; break; }
        }

        string brk = src.ToString(TextSpan.FromBounds(line.End, line.EndIncludingLineBreak));

        if (!touched)
        {
            sb.Append(src.ToString(ls));
            sb.Append(brk);
            continue;
        }

        var lb = new StringBuilder(ls.Length);
        for (int i = ls.Start; i < ls.End; i++)
        {
            if (!del[i]) lb.Append(src[i]);
        }

        string res = lb.ToString();
        if (res.Trim().Length == 0)
        {
            physicalRemoved++;
            continue;
        }

        sb.Append(res.TrimEnd());
        sb.Append(brk);
    }

    return sb.ToString();
}

static string TokenEquivalent(string before, string after)
{
    var t1 = CSharpSyntaxTree.ParseText(before);
    var t2 = CSharpSyntaxTree.ParseText(after);

    var a = t1.GetRoot().DescendantTokens(descendIntoTrivia: false).ToList();
    var b = t2.GetRoot().DescendantTokens(descendIntoTrivia: false).ToList();
    if (a.Count != b.Count) return $"token count {a.Count} != {b.Count}";
    for (int i = 0; i < a.Count; i++)
    {
        if (a[i].RawKind != b[i].RawKind)
            return $"token[{i}] kind {a[i].Kind()} != {b[i].Kind()}";
        if (!string.Equals(a[i].Text, b[i].Text, StringComparison.Ordinal))
            return $"token[{i}] text '{a[i].Text}' != '{b[i].Text}'";
        if (!string.Equals(a[i].ValueText, b[i].ValueText, StringComparison.Ordinal))
            return $"token[{i}] value differs";
    }

    string d1 = DirectiveSignature(t1);
    string d2 = DirectiveSignature(t2);
    if (!string.Equals(d1, d2, StringComparison.Ordinal)) return "directive sequence differs";

    int e1 = t1.GetDiagnostics().Count(d => d.Severity == DiagnosticSeverity.Error);
    int e2 = t2.GetDiagnostics().Count(d => d.Severity == DiagnosticSeverity.Error);
    if (e2 > e1) return $"new parse errors {e1} -> {e2}";

    return "OK";
}

// 지시문 서명: 지시문 원문에서 주석 trivia만 걷어낸 뒤 공백 정규화. 지시문 자체의 불변을 증명한다.
static string DirectiveSignature(Microsoft.CodeAnalysis.SyntaxTree t)
{
    var parts = new List<string>();
    foreach (var d in t.GetRoot().DescendantTrivia(descendIntoTrivia: false).Where(x => x.IsDirective))
    {
        var st = d.GetStructure();
        if (st is null) { parts.Add(Collapse(d.ToFullString())); continue; }
        var sb = new StringBuilder();
        foreach (var tk in st.DescendantTokens())
        {
            foreach (var tv in tk.LeadingTrivia) if (!IsCommentTrivia(tv)) sb.Append(tv.ToFullString());
            sb.Append(tk.Text);
            foreach (var tv in tk.TrailingTrivia) if (!IsCommentTrivia(tv)) sb.Append(tv.ToFullString());
        }
        parts.Add(Collapse(sb.ToString()));
    }
    return string.Join(" | ", parts);
}

static bool IsCommentTrivia(SyntaxTrivia t) =>
    t.IsKind(SyntaxKind.SingleLineCommentTrivia)
    || t.IsKind(SyntaxKind.MultiLineCommentTrivia)
    || t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
    || t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia);

// XML 문서주석 블록 수집. IsEmpty = 실제 텍스트 내용이 하나도 없는 껍데기.
static List<DocBlock> FindDocBlocks(string text)
{
    var src = SourceText.From(text);
    var root = CSharpSyntaxTree.ParseText(src).GetRoot();
    var res = new List<DocBlock>();
    foreach (var trivia in root.DescendantTrivia(descendIntoTrivia: false))
    {
        if (!trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
            && !trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)) continue;
        var span = trivia.FullSpan;
        if (span.Length == 0) continue;
        string raw = src.ToString(span);
        int l0 = src.Lines.GetLineFromPosition(span.Start).LineNumber;
        int l1 = src.Lines.GetLineFromPosition(Math.Max(span.Start, span.End - 1)).LineNumber;
        res.Add(new DocBlock(span, NormalizeDocBlock(raw), !DocHasContent(trivia, raw), l0, l1));
    }
    return res;
}

// 내용 판정: (1) Roslyn 구조 검사 OR (2) 태그 밖 잔여문자 검사. 둘 중 하나라도 걸리면 "내용 있음"(보수적).
static bool DocHasContent(SyntaxTrivia trivia, string raw)
{
    if (trivia.GetStructure() is not Microsoft.CodeAnalysis.CSharp.Syntax.DocumentationCommentTriviaSyntax doc)
        return true;

    foreach (var n in doc.DescendantNodes())
    {
        if (n is Microsoft.CodeAnalysis.CSharp.Syntax.XmlEmptyElementSyntax
            || n is Microsoft.CodeAnalysis.CSharp.Syntax.XmlCDataSectionSyntax
            || n is Microsoft.CodeAnalysis.CSharp.Syntax.XmlProcessingInstructionSyntax
            || n is Microsoft.CodeAnalysis.CSharp.Syntax.XmlCommentSyntax)
            return true;
    }

    // XmlText 검사는 쓰지 않는다: 짝 없는 </summary> 를 Roslyn이 평문 XmlText로 토해내므로
    // 토큰 텍스트만 보면 "내용 있음"으로 오판한다. 태그 구간을 걷어낸 잔여문자로 판정한다.
    return HasResidueText(raw);
}

// '///' 와 '<...>' 구간과 공백을 모두 걷어낸 뒤에도 문자가 남으면 실제 내용이 있는 것.
// 닫히지 않은 '<' 가 있으면 태그 오인으로 본문을 삼킬 수 있으므로 보수적으로 "내용 있음".
static bool HasResidueText(string raw)
{
    bool inTag = false;
    foreach (char c in raw)
    {
        if (c == '<') { inTag = true; continue; }
        if (c == '>') { inTag = false; continue; }
        if (inTag) continue;
        if (c == '/' || char.IsWhiteSpace(c)) continue;
        return true;
    }
    return inTag;
}

static string NormalizeDocBlock(string raw)
{
    var sb = new StringBuilder();
    foreach (var line in raw.Split('\n'))
    {
        string t = line.Trim();
        int i = 0;
        while (i < t.Length && t[i] == '/') i++;
        sb.Append(t[i..]).Append(' ');
    }
    return Collapse(sb.ToString());
}

readonly record struct DocBlock(TextSpan Span, string Norm, bool IsEmpty, int StartLine, int EndLine);

readonly record struct Unit(TextSpan Span, string Norm, int StartLine, int EndLine);
