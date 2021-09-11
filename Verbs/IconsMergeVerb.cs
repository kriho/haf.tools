using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HAF.Tools {
  [Verb("icons-merge", HelpText = "merge XAML graphics from multiple files into a resource dictionary and generate keys")]
  public class IconsMergeVerb {
    [Value(0, MetaName = "source directory", HelpText = "path to the directory that contains all XAML graphics", Required = true)]
    public string SourceDirectory { get; set; }

    [Value(1, MetaName = "keys file path", HelpText = "path to the generated file that contains all icon keys", Required = true)]
    public string KeysFilePath { get; set; }

    [Value(2, MetaName = "resource dictionary file path", HelpText = "path to the generated file that contains all icon graphics", Required = true)]
    public string ResourceDictionaryFilePath { get; set; }
  }
}
