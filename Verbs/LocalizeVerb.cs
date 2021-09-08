using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HAF.Tools {
  [Verb("localize", HelpText = "extract localized text from a source directory")]
  public class LocalizeVerb {
    [Option('q', "quiet", HelpText = "suppress verbose output")]
    public bool Quiet { get; set; }

    [Value(0, MetaName = "source directory", HelpText = "path to the directory that contains all sources", Required = true)]
    public string SourceDirectory { get; set; }

    [Value(1, MetaName = "target file path", HelpText = "path to the file that is generated", Required = true)]
    public string TargetFilePath { get; set; }
  }
}
