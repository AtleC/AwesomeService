using CommandLine;

namespace AwesomeService
{
    public class CommandLineOptions
    {
        [Value(index: 0, Required = false, HelpText = "Path to watch.")]
        public string Path { get; set; }

        [Option(shortName: 'e', longName: "extensions", Required = false, HelpText = "Valid extensions.", Default = new[] { "exe", "msi", "jpeg" })]
        public string[] Extensions { get; set; }

    }
}