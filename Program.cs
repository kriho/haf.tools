using CommandLine;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace HAF.Tools {

  public class LocalizableTextInfo {
    public static readonly LocalizableTextInfo Invalid = new LocalizableTextInfo();

    public string FilePath { get; set; }
    public int Line { get; set; }
    public string ContextId { get; set; }
    public string Id { get; set; }
    public string PluralId { get; set; }
    public string Comment { get; set; }
  }

  class Program {

    public static int Main(string[] args) {
      var verb = Parser.Default.ParseArguments<LocalizeVerb>(args);
      try {
        _ = verb.WithParsed(Program.Localize);
        return 0;
      } catch (Exception e) {
        Console.WriteLine(e.Message);
        return 1;
      }
    }

    private static void Localize(LocalizeVerb options) {
      var directoyInfo = new System.IO.DirectoryInfo(options.SourceDirectory);
      var texts = new List<LocalizableTextInfo>();
      foreach (var fileInfo in directoyInfo.EnumerateFiles("*.cs", System.IO.SearchOption.AllDirectories)) {
        texts.AddRange(ExtractFromCSharp(fileInfo.FullName));
      }
      foreach(var fileInfo in directoyInfo.EnumerateFiles("*.xaml", System.IO.SearchOption.AllDirectories)) {
        texts.AddRange(ExtractFromXaml(fileInfo.FullName));
      }
      var content = new StringBuilder();
      content.AppendLine("msgid \"\"");
      content.AppendLine("msgstr \"\"");
      content.AppendLine("\"Language: en-US\\n\"");
      content.AppendLine("\"MIME-Version: 1.0\\n\"");
      content.AppendLine("\"Content-Type: text/plain; charset=UTF-8\\n\"");
      content.AppendLine("\"Plural-Forms: nplurals=2; plural=(n != 1);\\n\"");
      var ids = new List<string>();
      foreach(var text in texts) {
        if(!ids.Contains(text.Id)) {
          ids.Add(text.Id);
          content.AppendLine("");
          content.AppendLine($"#: {text.FilePath.Replace(options.SourceDirectory, "").Trim('\\', '/', ' ')}:{text.Line}");
          if(text.ContextId != null) {
            content.AppendLine($"msgctxt \"{text.ContextId}\"");
          }
          content.AppendLine($"msgid \"{text.Id}\"");
          if(text.PluralId == null) {
            content.AppendLine($"msgstr \"\"");
          } else {
            content.AppendLine($"msgid_plural  \"{text.PluralId}\"");
            content.AppendLine($"msgstr[0] \"\"");
            content.AppendLine($"msgstr[1] \"\"");
          }
        }
      }
      System.IO.File.WriteAllText(options.TargetFilePath, content.ToString());
    }

    private static int GetLine(SyntaxNode node) {
      var lineSpan = node.SyntaxTree.GetMappedLineSpan(node.Span);
      return lineSpan.StartLinePosition.Line + 1;
    }

    private static IEnumerable<LocalizableTextInfo> ExtractFromCSharp(string filePath) {
      var fileContent = System.IO.File.ReadAllText(filePath);
      var syntaxTree = CSharpSyntaxTree.ParseText(fileContent);
      if (syntaxTree.GetDiagnostics().Any(d => d.Severity >= DiagnosticSeverity.Error)) {
        throw new InvalidOperationException($"the file \"{filePath}\" contains errors");
      }
      var argumentList = syntaxTree.GetRoot().DescendantNodes()
        .OfType<InvocationExpressionSyntax>()
        .Where(e => e.Expression is MemberAccessExpressionSyntax m && m.Name.Identifier.Text == "GetText")
        .Select(e => e.ArgumentList)
        .ToList();
      argumentList.AddRange(syntaxTree.GetRoot().DescendantNodes()
        .OfType<ObjectCreationExpressionSyntax>()
        .Where(e => e.Type is IdentifierNameSyntax i && i.Identifier.ValueText == "LocalizedText")
        .Select(e => e.ArgumentList));
      return argumentList
        .Select(l => {
          var stringLiterals = l.Arguments
            .Select(a => a.Expression)
            .OfType<LiteralExpressionSyntax>()
            .Where(a => a.IsKind(SyntaxKind.StringLiteralExpression))
            .Select(a => a.Token.ValueText)
            .ToList();
          if(stringLiterals.Count == 1) {
            return new LocalizableTextInfo() {
              Id = stringLiterals[0],
              Line = GetLine(l.Parent),
              FilePath = filePath,
            };
          } else if(stringLiterals.Count == 2) {
            if(l.Arguments.Count == 3) {
              return new LocalizableTextInfo() {
                Id = stringLiterals[0],
                PluralId = stringLiterals[1],
                Line = GetLine(l.Parent),
                FilePath = filePath,
              };
            } else {
              return new LocalizableTextInfo() {
                ContextId = stringLiterals[0],
                Id = stringLiterals[1],
                Line = GetLine(l.Parent),
                FilePath = filePath,
              };
            }
          } else if(stringLiterals.Count == 3) {
            return new LocalizableTextInfo() {
              ContextId = stringLiterals[0],
              Id = stringLiterals[1],
              PluralId = stringLiterals[2],
              Line = GetLine(l.Parent),
              FilePath = filePath,
            };
          } else {
            return null;
          }
        })
        .Where(e => e != null);
    }

    private static IEnumerable<LocalizableTextInfo> ExtractFromXaml(string filePath) {
      var document = XDocument.Load(filePath, LoadOptions.SetLineInfo);
      return document
        .Descendants()
        .SelectMany(d => d.Attributes())
        .Where(a => a.Value.Contains("Localize"))
        .Select(a => {
          var match = Regex.Match(a.Value, @"\{\S+:Localize +'(?:(.*?)\/\/)?(.*?)'\}");
          if(match.Success) {
            return new LocalizableTextInfo() {
              ContextId = match.Groups[1].Success ? match.Groups[1].Value : null,
              Id = match.Groups[2].Success ? match.Groups[2].Value : null,
              FilePath = filePath,
              Line = (a.Parent as IXmlLineInfo)?.LineNumber ?? -1,
            };
          } else {
            return null;
          }
        })
        .Where(a => a != null);
    }
  }
}
