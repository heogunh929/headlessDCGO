using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// CardIr.Extract (pipeline-v3 stage 2) — Roslyn Source IR extractor.
//
// Parses a read-only DCGO card .cs (syntax tree only; DCGO does not compile without Unity,
// and we do not need a semantic model — symbol resolution is the lowering table's job) into a
// LOSSLESS Source IR JSON in DCGO vocabulary. It TRANSLATES NOTHING and JUDGES NOTHING:
// unrecognized syntax is preserved as {"opaque": "<text>"} so downstream lowering can decide tier.
//
// Usage:
//   CardIrExtract <card.cs> [<card.cs> ...]         # emits data/ir-src/<SET>.<COLOR>/<ID>.json
//   CardIrExtract --stdout <card.cs>                # prints one card's Source IR to stdout
//
// Output path is derived from the mirror/original layout: .../CardEffect/<SET>/<COLOR>/<ID>.cs

var jsonOpts = new JsonSerializerOptions { WriteIndented = true };

bool toStdout = false;
var files = new List<string>();
foreach (var a in args)
{
    if (a == "--stdout") toStdout = true;
    else files.Add(a);
}
if (files.Count == 0)
{
    Console.Error.WriteLine("usage: CardIrExtract [--stdout] <card.cs> [<card.cs> ...]");
    return 2;
}

string repoRoot = FindRepoRoot();
int emitted = 0, failed = 0;
foreach (var file in files)
{
    try
    {
        var (set, color, id) = PathParts(file);
        JsonObject ir = ExtractCard(file, set, color, id);
        if (toStdout)
        {
            Console.WriteLine(ir.ToJsonString(jsonOpts));
        }
        else
        {
            string outDir = Path.Combine(repoRoot, "porting", "data", "ir-src", $"{set}.{color}");
            Directory.CreateDirectory(outDir);
            string outPath = Path.Combine(outDir, id + ".json");
            File.WriteAllText(outPath, ir.ToJsonString(jsonOpts));
            Console.WriteLine($"{id}: {Path.GetRelativePath(repoRoot, outPath)}");
        }
        emitted++;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"FAIL {file}: {ex.Message}");
        failed++;
    }
}
Console.Error.WriteLine($"extracted {emitted}, failed {failed}");
return failed > 0 ? 1 : 0;

// ---------- extraction ----------

static JsonObject ExtractCard(string path, string set, string color, string id)
{
    string src = File.ReadAllText(path);
    SyntaxTree tree = CSharpSyntaxTree.ParseText(src);
    var root = tree.GetCompilationUnitRoot();

    var cls = root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault()
              ?? throw new InvalidOperationException("no class declaration (vanilla / skeleton?)");
    var method = cls.Members.OfType<MethodDeclarationSyntax>()
        .FirstOrDefault(m => m.Identifier.Text == "CardEffects")
        ?? throw new InvalidOperationException("no CardEffects method");

    var ir = new JsonObject
    {
        ["schema"] = "source-ir/1",
        ["card"] = id,
        ["set"] = set,
        ["color"] = color,
        ["className"] = cls.Identifier.Text,
    };

    var branches = new JsonArray();
    if (method.Body is not null)
        foreach (var stmt in method.Body.Statements)
            CollectBranches(stmt, branches);
    ir["branches"] = branches;
    return ir;
}

// Find `if (timing == EffectTiming.X) { ... }` branches (top-level of the method body).
static void CollectBranches(StatementSyntax stmt, JsonArray branches)
{
    if (stmt is IfStatementSyntax ifs && TryTiming(ifs.Condition, out string timing))
    {
        var branch = new JsonObject { ["timing"] = timing };
        var localFns = new JsonArray();
        var effects = new JsonArray();
        var body = ifs.Statement is BlockSyntax b ? b.Statements : new SyntaxList<StatementSyntax>().Add(ifs.Statement);
        foreach (var s in body)
            CollectBranchStatement(s, localFns, effects);
        branch["localFns"] = localFns;
        branch["effects"] = effects;
        branches.Add(branch);
    }
}

static void CollectBranchStatement(StatementSyntax s, JsonArray localFns, JsonArray effects)
{
    switch (s)
    {
        case LocalFunctionStatementSyntax fn:
            localFns.Add(new JsonObject
            {
                ["name"] = fn.Identifier.Text,
                ["returns"] = fn.ReturnType.ToString(),
                ["params"] = new JsonArray(fn.ParameterList.Parameters
                    .Select(p => (JsonNode)p.Identifier.Text!).ToArray()),
                ["body"] = fn.Body is not null ? BoolFromStatements(fn.Body.Statements)
                          : fn.ExpressionBody is not null ? Expr(fn.ExpressionBody.Expression)
                          : new JsonObject { ["opaque"] = "<empty>" },
            });
            break;

        // cardEffects.Add( <expr> );
        case ExpressionStatementSyntax es when es.Expression is InvocationExpressionSyntax inv
            && inv.Expression is MemberAccessExpressionSyntax ma && ma.Name.Identifier.Text == "Add":
            effects.Add(EffectFromAdd(inv.ArgumentList.Arguments.FirstOrDefault()?.Expression));
            break;

        default:
            effects.Add(new JsonObject { ["opaque"] = s.ToString().Trim() });
            break;
    }
}

// The argument to cardEffects.Add(...) — for Tier 1/2 this is a CardEffectFactory.X(...) call.
static JsonObject EffectFromAdd(ExpressionSyntax? added)
{
    if (added is InvocationExpressionSyntax inv && inv.Expression is MemberAccessExpressionSyntax ma)
    {
        string recv = ma.Expression.ToString();  // e.g. CardEffectFactory
        string name = ma.Name.Identifier.Text;
        var argsArr = new JsonArray();
        foreach (var arg in inv.ArgumentList.Arguments)
        {
            argsArr.Add(new JsonObject
            {
                ["name"] = arg.NameColon?.Name.Identifier.Text,  // null for positional
                ["value"] = Expr(arg.Expression),
            });
        }
        return new JsonObject
        {
            ["kind"] = "factoryAdd",
            ["receiver"] = recv,
            ["factory"] = name,
            ["args"] = argsArr,
        };
    }
    // ActivateClass-based triggers, identifiers, etc. — preserved for tier classification.
    return new JsonObject { ["kind"] = "opaque", ["syntax"] = added?.ToString().Trim() ?? "<null>" };
}

// ---------- boolean-AST for predicate bodies (lossless, no translation) ----------

// Convert a statement list of a bool-returning predicate into a boolean expression node.
// Recognized shape: nested `if (C) { ...return true... }` then `return false;` == And-chain;
// `return EXPR;` == EXPR. Anything else -> opaque.
static JsonNode BoolFromStatements(SyntaxList<StatementSyntax> stmts)
{
    JsonNode? acc = null;
    foreach (var s in stmts)
    {
        switch (s)
        {
            case IfStatementSyntax ifs when ifs.Else is null:
                var inner = ifs.Statement is BlockSyntax b
                    ? BoolFromStatements(b.Statements)
                    : BoolFromStatements(new SyntaxList<StatementSyntax>().Add(ifs.Statement));
                JsonNode cond = Expr(ifs.Condition);
                JsonNode combined = IsConstTrue(inner) ? cond : And(cond, inner);
                acc = acc is null ? combined : And(acc, combined);
                break;

            case ReturnStatementSyntax ret:
                if (ret.Expression is null) break;
                if (IsLiteral(ret.Expression, out bool bv))
                    return bv ? Const(true) : (acc ?? Const(false));
                return acc is null ? Expr(ret.Expression) : And(acc, Expr(ret.Expression));

            default:
                return new JsonObject { ["opaque"] = string.Join(" ", stmts.Select(x => x.ToString().Trim())) };
        }
    }
    return acc ?? Const(false);
}

// ---------- expression nodes ----------

static JsonNode Expr(ExpressionSyntax e)
{
    switch (e)
    {
        case ParenthesizedExpressionSyntax p:
            return Expr(p.Expression);

        case LiteralExpressionSyntax lit:
            if (lit.IsKind(SyntaxKind.TrueLiteralExpression)) return Const(true);
            if (lit.IsKind(SyntaxKind.FalseLiteralExpression)) return Const(false);
            if (lit.IsKind(SyntaxKind.NullLiteralExpression)) return new JsonObject { ["null"] = true };
            if (lit.IsKind(SyntaxKind.NumericLiteralExpression) && lit.Token.Value is int iv)
                return new JsonObject { ["lit"] = iv };
            if (lit.IsKind(SyntaxKind.StringLiteralExpression))
                return new JsonObject { ["lit"] = lit.Token.ValueText };
            return new JsonObject { ["lit"] = lit.Token.ValueText };

        case IdentifierNameSyntax idn:
            return new JsonObject { ["ref"] = idn.Identifier.Text };

        case MemberAccessExpressionSyntax ma:
            // Flatten dotted path of simple identifiers (e.g. permanent.TopCard.CardColors),
            // otherwise keep structured.
            if (TryDottedPath(ma, out string dotted))
                return new JsonObject { ["member"] = dotted };
            return new JsonObject { ["memberOf"] = Expr(ma.Expression), ["name"] = ma.Name.Identifier.Text };

        case InvocationExpressionSyntax inv:
        {
            string callName = inv.Expression is MemberAccessExpressionSyntax m
                ? m.Expression + "." + m.Name.Identifier.Text
                : inv.Expression.ToString();
            var argsArr = new JsonArray();
            foreach (var a in inv.ArgumentList.Arguments)
                argsArr.Add(Expr(a.Expression));
            return new JsonObject { ["call"] = callName, ["args"] = argsArr };
        }

        case PrefixUnaryExpressionSyntax pu when pu.IsKind(SyntaxKind.LogicalNotExpression):
            return new JsonObject { ["not"] = Expr(pu.Operand) };

        case BinaryExpressionSyntax bin:
            return new JsonObject
            {
                ["binop"] = bin.OperatorToken.Text,
                ["lhs"] = Expr(bin.Left),
                ["rhs"] = Expr(bin.Right),
            };

        case SimpleLambdaExpressionSyntax sl:
            return new JsonObject
            {
                ["lambda"] = new JsonObject
                {
                    ["params"] = new JsonArray((JsonNode)sl.Parameter.Identifier.Text!),
                    ["body"] = sl.Body is ExpressionSyntax le ? Expr(le)
                              : sl.Body is BlockSyntax lb ? BoolFromStatements(lb.Statements)
                              : new JsonObject { ["opaque"] = sl.Body.ToString() },
                },
            };

        case ParenthesizedLambdaExpressionSyntax pl:
            return new JsonObject
            {
                ["lambda"] = new JsonObject
                {
                    ["params"] = new JsonArray(pl.ParameterList.Parameters
                        .Select(p => (JsonNode)p.Identifier.Text!).ToArray()),
                    ["body"] = pl.Body is ExpressionSyntax le2 ? Expr(le2)
                              : pl.Body is BlockSyntax lb2 ? BoolFromStatements(lb2.Statements)
                              : new JsonObject { ["opaque"] = pl.Body.ToString() },
                },
            };

        default:
            return new JsonObject { ["opaque"] = e.ToString().Trim() };
    }
}

// ---------- helpers ----------

static JsonNode And(JsonNode a, JsonNode b) => new JsonObject { ["binop"] = "&&", ["lhs"] = a, ["rhs"] = b };
static JsonNode Const(bool v) => new JsonObject { ["const"] = v };
static bool IsConstTrue(JsonNode n) => n is JsonObject o && o.TryGetPropertyValue("const", out var c) && c is JsonValue jv && jv.TryGetValue(out bool b) && b;

static bool IsLiteral(ExpressionSyntax e, out bool value)
{
    if (e is LiteralExpressionSyntax l)
    {
        if (l.IsKind(SyntaxKind.TrueLiteralExpression)) { value = true; return true; }
        if (l.IsKind(SyntaxKind.FalseLiteralExpression)) { value = false; return true; }
    }
    value = false; return false;
}

static bool TryTiming(ExpressionSyntax cond, out string timing)
{
    timing = "";
    if (cond is BinaryExpressionSyntax bin && bin.IsKind(SyntaxKind.EqualsExpression))
    {
        foreach (var side in new[] { bin.Right, bin.Left })
            if (side is MemberAccessExpressionSyntax ma && ma.Expression.ToString() == "EffectTiming")
            {
                timing = ma.Name.Identifier.Text;
                return true;
            }
    }
    return false;
}

static bool TryDottedPath(ExpressionSyntax e, out string path)
{
    var parts = new List<string>();
    ExpressionSyntax cur = e;
    while (cur is MemberAccessExpressionSyntax ma)
    {
        parts.Add(ma.Name.Identifier.Text);
        cur = ma.Expression;
    }
    if (cur is IdentifierNameSyntax idn)
    {
        parts.Add(idn.Identifier.Text);
        parts.Reverse();
        path = string.Join(".", parts);
        return true;
    }
    path = "";
    return false;
}

static (string set, string color, string id) PathParts(string file)
{
    // .../CardEffect/<SET>/<COLOR>/<ID>.cs
    var full = Path.GetFullPath(file);
    var segs = full.Split(Path.DirectorySeparatorChar);
    int i = Array.LastIndexOf(segs, "CardEffect");
    if (i < 0 || i + 3 >= segs.Length)
        throw new InvalidOperationException($"path not under CardEffect/<SET>/<COLOR>/: {file}");
    return (segs[i + 1], segs[i + 2], Path.GetFileNameWithoutExtension(segs[i + 3]));
}

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "HeadlessDCGO.Engine")))
        dir = dir.Parent;
    return dir?.FullName ?? Directory.GetCurrentDirectory();
}
