using CommandLine;
using System.Collections.Generic;

namespace PolicyUtil
{
    public class Program
    {
        static int Main(string[] args)
        {
            return CommandLine.Parser.Default.ParseArguments<SingleOptions, BatchOptions>(args)
               .MapResult(
                 (SingleOptions opts) => RunSingleAndReturnExitCode(opts),
                 (BatchOptions opts) => RunBatchAndReturnExitCode(opts),
                 errs => 1);
        }

        static int RunSingleAndReturnExitCode(SingleOptions opts)
        {
            MergeHelper.ProcessSingle(opts.Xml, opts.Cs);
            return 0;
        }

        static int RunBatchAndReturnExitCode(BatchOptions opts)
        {
            MergeHelper.ProcessBatch(opts.Folder);
            return 0;
        }
    }

    [Verb("single", HelpText = "Process single")]
    class SingleOptions
    {
        [Option(HelpText = "Xml file.")]
        public string Xml { get; set; }

        [Option(HelpText = "CSharp file.")]
        public string Cs { get; set; }
    }

    [Verb("batch", HelpText = "Process batch")]
    class BatchOptions
    {
        [Option(HelpText = "Root folder name.")]
        public string Folder { get; set; }
    }
}