using System;
using System.IO;
using System.Linq;

using CommandlineSystem;

using OpenFL.Core.DataObjects.SerializableDataObjects;
using OpenFL.Core.Parsing.StageResults;
using OpenFL.Serialization;

using Utility.CommandRunner;
using Utility.CommandRunner.BuiltInCommands;

namespace OpenFL.Commandline.Core
{
    public abstract class FLCommandlineSystem : ICommandlineSystem
    {

        private string[] Output = new string[0];
        private string[] Input = new string[0];
        private bool NoDialog;

        public abstract string Name { get; }
        public abstract string[] SupportedInputExtensions { get; }
        public abstract string[] SupportedOutputExtensions { get; }

        public void Run(string[] args)
        {
            Runner r = new Runner();
            r._AddCommand(new DefaultHelpCommand(true));
            r._AddCommand(new SetDataCommand(s => Input = s, new[] { "--input", "-i" }, "Set Input Files"));
            r._AddCommand(new SetDataCommand(s => Output = s, new[] { "--output", "-o" }, "Set Output Files"));
            r._AddCommand(new SetDataCommand(s => NoDialog = true, new[] { "--yes", "-y" }, "Answer all dialogs with Yes"));
            AddCommands(r);
            r._RunCommands(args);

            FLData.InitializeFL(NoDialog);

            BeforeRun();

            for (int i = 0; i < Input.Length; i++)
            {
                string input = Path.GetFullPath(Input[i]);
                if (!SupportedInputExtensions.Select(x=> "." + x).Contains(Path.GetExtension(input)))
                {
                    throw new Exception("Extension is not supported: " + Path.GetExtension(input));
                }
                string output = Output.Length <= i
                                    ? Path.Combine(
                                                   Path.GetDirectoryName(input),
                                                   Path.GetFileNameWithoutExtension(input) + "." + SupportedOutputExtensions.First()
                                                  )
                                    : Output[i];
                FLData.SetProgress($"[{Name}]", $"{Path.GetFileName(input)} => {Path.GetFileName(output)}", 1, i, input.Length);
                if (!SupportedOutputExtensions.Select(x => "." + x).Contains(Path.GetExtension(output)))
                {
                    throw new Exception("Extension is not supported: " + Path.GetExtension(output));
                }
                Run(input, output);
            }

        }

        protected virtual void BeforeRun()
        {

        }
        protected abstract void Run(string input, string output);

        protected abstract void AddCommands(Runner runner);

        protected SerializableFLProgram Parse(string input, string[] defines)
        {
            return FLData.Container.Parser.Process(new FLParserInput(input, true, defines));
        }

        protected void Save(string path, SerializableFLProgram prog, string[] extraSteps)
        {
            using (Stream s = File.Create(path))
            {
                FLSerializer.SaveProgram(s, prog, FLData.Container.InstructionSet, extraSteps);
            }
        }

        protected SerializableFLProgram Load(string input)
        {
            using (Stream s = File.OpenRead(input))
            {
                return FLSerializer.LoadProgram(s, FLData.Container.InstructionSet);
            }
        }
    }
}