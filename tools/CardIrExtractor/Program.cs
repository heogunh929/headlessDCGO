using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// (P-DB1) 카드 포팅 IR 추출기 — docs/audit/card_porting_database_design.md §2.
// AS-IS 카드 .cs를 Roslyn으로 파싱해 카드당 IR 1레코드(JSONL)를 산출.
// 사용:  dotnet run --project tools/CardIrExtractor -- <asis-card-root> <out.jsonl>
//   기본: DCGO/Assets/Scripts/CardEffect  ->  docs/porting/card_ir.jsonl

string root = args.Length > 0 ? args[0] : Path.Combine("DCGO", "Assets", "Scripts", "CardEffect");
string outPath = args.Length > 1 ? args[1] : Path.Combine("docs", "porting", "card_ir.jsonl");

if (!Directory.Exists(root))
{
    Console.Error.WriteLine($"AS-IS card root not found: {root} (DCGO/ is local-only).");
    return 1;
}

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);

string[] files = Directory
    .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
    .OrderBy(p => p, StringComparer.Ordinal)
    .ToArray();

int written = 0, parseErrors = 0;
var primitiveUniverse = new SortedSet<string>(StringComparer.Ordinal);

using var writer = new StreamWriter(outPath, append: false);
foreach (string file in files)
{
    CardIr? ir = ExtractCard(file, root, out string? error);
    if (error is not null)
    {
        parseErrors++;
        Console.Error.WriteLine($"parse note: {Path.GetFileName(file)}: {error}");
    }

    if (ir is null)
    {
        continue;
    }

    foreach (string prim in ir.Primitives.Keys)
    {
        primitiveUniverse.Add(prim);
    }

    writer.WriteLine(ir.ToJson().ToJsonString());
    written++;
}

Console.WriteLine($"cards written: {written} / files scanned: {files.Length} / parse notes: {parseErrors}");
Console.WriteLine($"distinct primitives observed: {primitiveUniverse.Count}");
Console.WriteLine($"output: {outPath}");
return 0;

// ---------------------------------------------------------------------------

static CardIr? ExtractCard(string file, string root, out string? error)
{
    error = null;
    string source;
    try
    {
        source = File.ReadAllText(file);
    }
    catch (Exception ex)
    {
        error = ex.Message;
        return null;
    }

    SyntaxTree tree = CSharpSyntaxTree.ParseText(source);
    CompilationUnitSyntax rootNode = (CompilationUnitSyntax)tree.GetRoot();

    ClassDeclarationSyntax? cls = rootNode.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
    if (cls is null)
    {
        error = "no class declaration";
        return null;
    }

    string cardId = cls.Identifier.Text;
    string relative = Path.GetRelativePath(root, file).Replace('\\', '/');
    string[] segments = relative.Split('/');
    string setCode = segments.Length > 0 ? segments[0] : string.Empty;
    string color = segments.Length > 1 ? segments[1] : string.Empty;

    var ir = new CardIr(cardId, setCode, color, relative);

    // 타이밍: `timing == EffectTiming.X` 및 임의의 EffectTiming.X 멤버 접근.
    foreach (MemberAccessExpressionSyntax member in cls.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
    {
        string? owner = (member.Expression as IdentifierNameSyntax)?.Identifier.Text;
        string name = member.Name.Identifier.Text;
        switch (owner)
        {
            case "EffectTiming":
                ir.Timings.Add(name);
                break;
            case "CardEffectCommons":
                ir.Commons.Add(name);
                break;
        }
    }

    // 프리미티브: CardEffectFactory.Method(...) 호출 + 인자 종(種) 프로파일.
    foreach (InvocationExpressionSyntax invocation in cls.DescendantNodes().OfType<InvocationExpressionSyntax>())
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax access)
        {
            continue;
        }

        string? invOwner = (access.Expression as IdentifierNameSyntax)?.Identifier.Text;
        if (invOwner != "CardEffectFactory")
        {
            continue;
        }

        string method = access.Name.Identifier.Text;
        ir.AddPrimitive(method, ClassifyArgs(invocation.ArgumentList));
    }

    // 키워드/클래스 트리거 + 코루틴 액션 서브효과: `new X(` 중 *Class(트리거) / *Effect(액션).
    // (P-DB2) *Effect = 코루틴이 실제로 무엇을 하는가의 판별 신호 — inline/mixed 카드의 포트 타깃을
    // 가르는 핵심(예: SelectAndBuffDp vs SelectAndDestroy는 여기서 갈린다).
    foreach (ObjectCreationExpressionSyntax creation in cls.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
    {
        string typeName = creation.Type.ToString();
        if (typeName.EndsWith("Class", StringComparison.Ordinal))
        {
            ir.Keywords.Add(typeName);
        }
        else if (typeName.EndsWith("Effect", StringComparison.Ordinal))
        {
            ir.Actions.Add(typeName);
        }
    }

    // 코루틴이 GetComponent<XEffect>()로 끌어오는 서브효과 = 액션 동사(SelectPermanentEffect 등).
    foreach (GenericNameSyntax generic in cls.DescendantNodes().OfType<GenericNameSyntax>())
    {
        if (generic.Identifier.Text != "GetComponent")
        {
            continue;
        }

        foreach (TypeSyntax typeArg in generic.TypeArgumentList.Arguments)
        {
            string name = typeArg.ToString();
            if (name.EndsWith("Effect", StringComparison.Ordinal))
            {
                ir.Actions.Add(name);
            }
        }
    }

    // (P-DB2) 효과 설명문 — DCGO가 템플릿화한 룰텍스트. 코루틴 액션 동사가 정적 추출로 안 잡히는
    // 경우가 많은데, 설명문("Delete up to 2…" / "gets +3000 DP")이 가장 강건한 액션 신호다.
    foreach (LiteralExpressionSyntax literal in cls.DescendantNodes().OfType<LiteralExpressionSyntax>())
    {
        if (literal.Token.Value is string text && text.Length >= 12 && text.Contains('['))
        {
            ir.Descriptions.Add(text.Trim());
        }
    }

    ir.HasEffect = ir.Primitives.Count > 0 || ir.Keywords.Count > 0 || ir.Actions.Count > 0;
    return ir;
}

// 인자를 種으로 분류: 리터럴(int/string/bool) / 람다·로컬함수 / 열거·멤버 / 식별자 / 기타.
// "무엇이 카드마다 다른가"(=템플릿 슬롯)를 드러내는 것이 목적 — 값 자체가 아니라 種.
static JsonObject ClassifyArgs(ArgumentListSyntax? argList)
{
    var result = new JsonObject();
    if (argList is null)
    {
        return result;
    }

    foreach (ArgumentSyntax arg in argList.Arguments)
    {
        string name = arg.NameColon?.Name.Identifier.Text ?? "_positional";
        result[name] = ClassifyExpression(arg.Expression);
    }

    return result;
}

static string ClassifyExpression(ExpressionSyntax expr)
{
    return expr switch
    {
        LiteralExpressionSyntax literal => literal.Token.Value switch
        {
            int => "literal:int",
            string => "literal:string",
            bool => "literal:bool",
            _ => "literal"
        },
        PrefixUnaryExpressionSyntax => "literal:int",         // -1000 등
        MemberAccessExpressionSyntax => "member",             // EffectTiming.X, Color.Red 등
        SimpleLambdaExpressionSyntax or ParenthesizedLambdaExpressionSyntax => "lambda",
        IdentifierNameSyntax id => id.Identifier.Text == "card" ? "card" : "identifier",  // 로컬함수(Condition) 참조 포함
        InvocationExpressionSyntax => "invocation",
        _ => "other"
    };
}

// ---------------------------------------------------------------------------

sealed class CardIr(string cardId, string setCode, string color, string sourcePath)
{
    public string CardId { get; } = cardId;
    public string SetCode { get; } = setCode;
    public string Color { get; } = color;
    public string SourcePath { get; } = sourcePath;
    public bool HasEffect { get; set; }
    public SortedSet<string> Timings { get; } = new(StringComparer.Ordinal);
    public SortedSet<string> Commons { get; } = new(StringComparer.Ordinal);
    public SortedSet<string> Keywords { get; } = new(StringComparer.Ordinal);
    public SortedSet<string> Actions { get; } = new(StringComparer.Ordinal);
    public List<string> Descriptions { get; } = new();

    // 프리미티브명 -> 호출들(각 호출의 인자 種 프로파일). 같은 프리미티브 다중 호출 보존.
    public Dictionary<string, List<JsonObject>> Primitives { get; } = new(StringComparer.Ordinal);

    public void AddPrimitive(string name, JsonObject argProfile)
    {
        if (!Primitives.TryGetValue(name, out List<JsonObject>? calls))
        {
            calls = new List<JsonObject>();
            Primitives[name] = calls;
        }

        calls.Add(argProfile);
    }

    public JsonObject ToJson()
    {
        var prims = new JsonArray();
        foreach (KeyValuePair<string, List<JsonObject>> entry in Primitives.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            var calls = new JsonArray();
            foreach (JsonObject profile in entry.Value)
            {
                calls.Add(profile.DeepClone());
            }

            prims.Add(new JsonObject
            {
                ["name"] = entry.Key,
                ["count"] = entry.Value.Count,
                ["calls"] = calls
            });
        }

        return new JsonObject
        {
            ["card_id"] = CardId,
            ["set_code"] = SetCode,
            ["color"] = Color,
            ["has_effect"] = HasEffect,
            ["timings"] = ToArray(Timings),
            ["primitives"] = prims,
            ["commons"] = ToArray(Commons),
            ["keywords"] = ToArray(Keywords),
            ["actions"] = ToArray(Actions),
            ["descriptions"] = ToArray(Descriptions),
            ["source_path"] = SourcePath
        };
    }

    private static JsonArray ToArray(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (string value in values)
        {
            array.Add(value);
        }

        return array;
    }
}
