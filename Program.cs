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
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;

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
      var verb = Parser.Default.ParseArguments<LocalizeVerb, IconsConvertVerb, IconsMergeVerb>(args);
      try {
        _ = verb.WithParsed<LocalizeVerb>(Program.Localize);
        _ = verb.WithParsed<IconsConvertVerb>(Program.ConvertIcons);
        _ = verb.WithParsed<IconsMergeVerb>(Program.MergeIcons);
        return 0;
      } catch(Exception e) {
        Console.WriteLine(e.Message);
        return 1;
      }
    }

    private static void ConvertIcons(IconsConvertVerb options) {
      var outputDirectoryInfo = new System.IO.DirectoryInfo(options.OutputDirectory);
      var sourceDirectoryInfo = new System.IO.DirectoryInfo(options.SourceDirectory);
      // create conversion options
      var settings = new WpfDrawingSettings {
        IncludeRuntime = false,
        TextAsGeometry = false,
        OptimizePath = true,
      };
      foreach(var fileInfo in outputDirectoryInfo.GetFiles("*.*", System.IO.SearchOption.AllDirectories)) {
        fileInfo.Delete();
      }
      // create a directory converter
      var converter = new FileSvgConverter(settings) {
        SaveXaml = true
      };
      // perform the conversion to XAML
      void convertSubdirectories(System.IO.DirectoryInfo directoryInfo) {
        foreach(var fileInfo in directoryInfo.EnumerateFiles()) {
          var targetFilePath = fileInfo.FullName.Replace(sourceDirectoryInfo.FullName, outputDirectoryInfo.FullName).Replace(".svg", ".xaml");
          var document = XDocument.Load(fileInfo.FullName);
          var modified = false;
          foreach(var p in document.Descendants().Where(d => d.Attribute("fill")?.Value == "none" && d.Attribute("stroke") == null)) {
            p.SetAttributeValue("stroke", "currentColor");
            modified = true;
          }
          foreach(var p in document.Descendants().Where(d => d.Attribute("fill") == null && d.Attribute("stroke") == null && d.Attribute("stroke-linecap") != null && d.Attribute("stroke-width")?.Value != "0")) {
            p.SetAttributeValue("stroke", "currentColor");
            p.SetAttributeValue("fill", "none");
            modified = true;
          }
          var success = false;
          if(modified) {
            var temporaryFilePath = System.IO.Path.GetTempFileName();
            document.Save(temporaryFilePath);
            success = converter.Convert(temporaryFilePath, targetFilePath);
          } else {
            success = converter.Convert(fileInfo.FullName, targetFilePath);
          }
          if(!success || converter.WriterErrorOccurred) {
            throw new Exception("writer error ocurred");
          }
        }
        foreach(var subDirectoryInfo in directoryInfo.EnumerateDirectories()) {
          convertSubdirectories(subDirectoryInfo);
        }
      }
      convertSubdirectories(sourceDirectoryInfo);
    }

    private static void MergeIcons(IconsMergeVerb options) {
      var sourceDirectoryInfo = new System.IO.DirectoryInfo(options.SourceDirectory);
      // add drawings to dictionary
      var dictionaryDocument = XDocument.Load(options.ResourceDictionaryFilePath);
      dictionaryDocument.Root.RemoveNodes();
      var keys = new List<string>();
      XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
      XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
      foreach(var fileInfo in sourceDirectoryInfo.GetFiles("*.xaml", System.IO.SearchOption.AllDirectories)) {
        var document = XDocument.Load(fileInfo.FullName);
        // do not use root
        var root = document.Root.Element(xaml + "DrawingGroup");
        root.RemoveAttributes();
        var entry = string.Join("", fileInfo.Name.Replace(".xaml", "").Replace("'", "").Replace(",", "").Replace("+", "Plus").Replace(".", "Dot").Split('-').Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Substring(0, 1).ToUpper() + s.Substring(1)));
        var category = string.Join("", fileInfo.DirectoryName.Replace(sourceDirectoryInfo.FullName, "").Replace("'", "").Replace(",", "").Replace("+", "Plus").Replace(".", "Dot").Split('/', '\\', '-').Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Substring(0, 1).ToUpper() + s.Substring(1)));
        var key = category + "_" + entry;
        root.Add(new XAttribute(x + "Key", "Icon" + key));
        dictionaryDocument.Root.Add(root);
        keys.Add(key + ",");
      }
      if(keys.Count != keys.Distinct().Count()) {
        throw new Exception("keys are not unique");
      }
      dictionaryDocument.Save(options.ResourceDictionaryFilePath);
      System.IO.File.WriteAllText(options.KeysFilePath, string.Join("\r\n", keys));
    }

    private static void Localize(LocalizeVerb options) {
      var directoyInfo = new System.IO.DirectoryInfo(options.SourceDirectory);
      var texts = new List<LocalizableTextInfo>();
      foreach(var fileInfo in directoyInfo.EnumerateFiles("*.cs", System.IO.SearchOption.AllDirectories)) {
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
      if(syntaxTree.GetDiagnostics().Any(d => d.Severity >= DiagnosticSeverity.Error)) {
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
