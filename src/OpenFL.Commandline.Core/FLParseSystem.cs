using System.IO;

using OpenFL.Core.DataObjects.SerializableDataObjects;
using OpenFL.Core.Parsing.StageResults;
using OpenFL.Serialization;

using Utility.CommandRunner;

namespace OpenFL.Commandline.Core
{
    public class FLParseSystem : FLCommandlineSystem
    {
        private string[] Defines= new string[0];
        private string[] ExtraSteps;

        private bool NoDialog;

        public override string Name => "serialize";

        public override string[] SupportedInputExtensions => new []{ "fl" };

        public override string[] SupportedOutputExtensions => new []{ "flc" };

        protected override void Run(string input, string output)
        {
            FLData.Log($"[{Name}]", "Parsing", 2);
            SerializableFLProgram prog = Parse(input, Defines);
            FLData.Log($"[{Name}]", "Serializing", 2);
            using (Stream s = File.Create(output))
            {
                FLSerializer.SaveProgram(s, prog, FLData.Container.InstructionSet, ExtraSteps);
            }
        }

        protected override void AddCommands(Runner runner)
        {
            runner._AddCommand(new SetDataCommand(s => Defines = s, new[] { "--defines", "-d" }, "Set Define Tags"));
            runner._AddCommand(new SetDataCommand(s => ExtraSteps = s, new[] { "--extra-steps", "-e" }, "Set Extra Serialization Steps"));
        }
    }
}