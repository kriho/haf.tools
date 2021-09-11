using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HAF.Tools {
  [Verb("icons-convert", HelpText = "convert svg files to XAML graphics")]
  public class IconsConvertVerb {
    [Value(0, MetaName = "source directory", HelpText = "path to the directory that contains all svg files", Required = true)]
    public string SourceDirectory { get; set; }

    [Value(1, MetaName = "output directory", HelpText = "path to the directory that is used to store all generated graphics", Required = true)]
    public string OutputDirectory { get; set; }
  }
}
