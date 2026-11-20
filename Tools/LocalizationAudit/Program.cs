using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

string root = Path.GetFullPath(args.Length > 0 ? args[0] : "PCHardwareMonitor");
string output = Path.GetFullPath(args.Length > 1 ? args[1] : "Tools/LocalizationAudit/bin/audit.json");
if (args.Contains("--fix-exceptions"))
{
    var map = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText("Tools/LocalizationAudit/ExceptionTranslations.json"))!;
    foreach (string path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories).Where(p => !p.Split(Path.DirectorySeparatorChar).Any(s => s is "bin" or "obj" or "Thermiqra_Folder_Organization")))
    {
        string original = File.ReadAllText(path);
        var syntax = CSharpSyntaxTree.ParseText(original).GetRoot();
        string changed = new ExceptionLocalizer(map).Visit(syntax)!.ToFullString();
        if (changed != original) File.WriteAllText(path, changed);
    }
}
bool Russian(string text) => Regex.IsMatch(text, "[А-Яа-яЁё]");
bool Source(string path) => !path.Split(Path.DirectorySeparatorChar).Any(p => p is "bin" or "obj" or "Thermiqra_Folder_Organization");
var settings = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, "Services/Settings/AppSettings.cs"))).GetRoot();
var translations = new Dictionary<string, string>();
foreach (var assignment in settings.DescendantNodes().OfType<AssignmentExpressionSyntax>())
    if (assignment.Left is ImplicitElementAccessSyntax index && index.ArgumentList.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax key &&
        assignment.Right is LiteralExpressionSyntax value) translations.TryAdd(key.Token.ValueText, value.Token.ValueText);
var findings = new List<Finding>();
var englishReview = new List<string>();
var pairIssues = new List<string>();
foreach (string path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories).Where(Source))
{
    var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(path));
    foreach (var call in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Where(i => i.Expression.ToString() is "L" or "SettingsService.L"))
    {
        if (call.ArgumentList.Arguments.Count != 2) continue;
        var english = call.ArgumentList.Arguments[1].Expression;
        string plainEnglish = english is LiteralExpressionSyntax literal ? literal.Token.ValueText :
            english is InterpolatedStringExpressionSyntax formatted ? string.Concat(formatted.Contents.OfType<InterpolatedStringTextSyntax>().Select(t => t.TextToken.ValueText)) : "";
        if (Russian(plainEnglish)) pairIssues.Add($"{Path.GetRelativePath(root, path)}:{call.GetLocation().GetLineSpan().StartLinePosition.Line + 1} | Russian in English branch: {plainEnglish}");
    }
    foreach (var expression in tree.GetRoot().DescendantNodes().Where(n => n is LiteralExpressionSyntax or InterpolatedStringExpressionSyntax))
    {
        string text = expression switch { LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression) => literal.Token.ValueText,
            InterpolatedStringExpressionSyntax formatted => string.Concat(formatted.Contents.OfType<InterpolatedStringTextSyntax>().Select(t => t.TextToken.ValueText)), _ => "" };
        if (!Russian(text))
        {
            if (Regex.IsMatch(text, "[A-Za-z]{4}") && !expression.Ancestors().OfType<InvocationExpressionSyntax>().Any(i => i.Expression.ToString() is "L" or "SettingsService.L") &&
                !expression.Ancestors().OfType<ConditionalExpressionSyntax>().Any(c => Regex.IsMatch(c.Condition.ToString(), "(?i)russian")))
            {
                var creation = expression.Ancestors().OfType<ObjectCreationExpressionSyntax>().FirstOrDefault();
                var assignment = expression.Ancestors().OfType<AssignmentExpressionSyntax>().FirstOrDefault();
                if (creation?.Type.ToString().EndsWith("Exception") == true || assignment != null && Regex.IsMatch(assignment.Left.ToString(), @"\.(Text|Title|Content|ToolTip)$"))
                    englishReview.Add($"{Path.GetRelativePath(root, path)}:{expression.GetLocation().GetLineSpan().StartLinePosition.Line + 1} | {text}");
            }
            continue;
        }
        string relative = Path.GetRelativePath(root, path);
        if (relative == Path.Combine("Services", "Settings", "AppSettings.cs") && expression.GetLocation().GetLineSpan().StartLinePosition.Line < 470) continue;
        bool localized = expression.Ancestors().OfType<InvocationExpressionSyntax>().Any(i => i.Expression.ToString() is "L" or "SettingsService.L") ||
            expression.Ancestors().OfType<ConditionalExpressionSyntax>().Any(c => Regex.IsMatch(c.Condition.ToString(), "(?i)russian")) ||
            expression.Ancestors().OfType<SwitchExpressionSyntax>().Any(c => c.GoverningExpression.ToString().Contains("IsRussian"));
        var argument = expression.AncestorsAndSelf().OfType<ArgumentSyntax>().FirstOrDefault();
        if (argument?.NameColon?.Name.ToString().EndsWith("Ru") == true) localized = true;
        if (argument?.Parent is ArgumentListSyntax list)
        {
            int at = list.Arguments.IndexOf(argument);
            if (at + 1 < list.Arguments.Count && list.Arguments[at + 1].Expression is LiteralExpressionSyntax next &&
                next.IsKind(SyntaxKind.StringLiteralExpression) && !Russian(next.Token.ValueText) && next.Token.ValueText.Length > 2) localized = true;
        }
        if (expression.Ancestors().OfType<AssignmentExpressionSyntax>().Any(a => a.Left.ToString().EndsWith("Ru"))) localized = true;
        if (localized) continue;
        string context = expression.Ancestors().FirstOrDefault(n => n is StatementSyntax or MemberDeclarationSyntax)?.ToString() ?? "";
        if (context.Contains("Debug.WriteLine") || context.Contains("DiagnosticLog.") || context.Contains("Console.Write")) continue;
        bool exact = translations.ContainsKey(text);
        string translated = text;
        foreach (var pair in translations.OrderByDescending(p => p.Key.Length)) translated = translated.Replace(pair.Key, pair.Value, StringComparison.Ordinal);
        findings.Add(new(relative, expression.GetLocation().GetLineSpan().StartLinePosition.Line + 1, text, exact || !Russian(translated), context[..Math.Min(context.Length, 240)]));
    }
}
foreach (string path in Directory.EnumerateFiles(root, "*.xaml", SearchOption.AllDirectories).Where(Source))
{
    var xml = XDocument.Load(path, LoadOptions.SetLineInfo);
    foreach (var attribute in xml.Descendants().Attributes().Where(a => a.Name.LocalName is "Text" or "Content" or "Header" or "Title" or "ToolTip" && Russian(a.Value)))
        findings.Add(new(Path.GetRelativePath(root, path), ((System.Xml.IXmlLineInfo)attribute).LineNumber, attribute.Value,
            translations.ContainsKey(attribute.Value), attribute.Parent?.Name.LocalName + "." + attribute.Name.LocalName));
}
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
File.WriteAllText(output, JsonSerializer.Serialize(findings, new JsonSerializerOptions { WriteIndented = true }));
File.WriteAllLines(Path.ChangeExtension(output, ".txt"), findings.Where(f => !f.Covered).Select(f => $"{f.File}:{f.Line} | {f.Text.Replace("\r", "").Replace("\n", "\\n")} | {f.Context.Replace("\r", "").Replace("\n", " ")}"));
File.WriteAllLines(Path.ChangeExtension(output, ".english.txt"), englishReview);
File.WriteAllLines(Path.ChangeExtension(output, ".pairs.txt"), pairIssues);
Console.WriteLine($"Translations: {translations.Count}; audited literals without direct language selection: {findings.Count}; review candidates: {findings.Count(f => !f.Covered)}");
Console.WriteLine($"Language-pair issues: {pairIssues.Count}; English-only messages to review: {englishReview.Count}");
if (pairIssues.Count > 0) Environment.ExitCode = 1;
record Finding(string File, int Line, string Text, bool Covered, string Context);

sealed class ExceptionLocalizer(Dictionary<string, string> translations) : CSharpSyntaxRewriter
{
    public override SyntaxNode? VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
    {
        if (node.Type.ToString().EndsWith("Exception") && node.ArgumentList?.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax literal &&
            translations.TryGetValue(literal.Token.ValueText, out string? russian))
        {
            var argument = node.ArgumentList.Arguments[0];
            var options = new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            var localized = SyntaxFactory.ParseExpression("SettingsService.L(" + JsonSerializer.Serialize(russian, options) + ", " + literal.WithoutTrivia() + ")").WithTriviaFrom(literal);
            return node.WithArgumentList(node.ArgumentList.WithArguments(node.ArgumentList.Arguments.Replace(argument, argument.WithExpression(localized))));
        }
        return base.VisitObjectCreationExpression(node);
    }
}
