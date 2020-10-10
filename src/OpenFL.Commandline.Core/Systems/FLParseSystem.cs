using System.IO;

using OpenFL.Core.DataObjects.SerializableDataObjects;
using OpenFL.Serialization;

using Utility.ADL;
using Utility.CommandRunner;

namespace OpenFL.Commandline.Core.Systems
{
    public class FLParseSystem : FLProcessingCommandlineSystem
    {

        private string[] Defines = new string[0];
        private string[] ExtraSteps;

        public override string Name => "serialize";

        public override string[] SupportedInputExtensions => new[] { "fl" };

        public override string[] SupportedOutputExtensions => new[] { "flc" };

        protected override void Run(string input, string output)
        {
            Logger.Log(LogType.Log, "Parsing", 2);
            SerializableFLProgram prog = Parse(input, Defines);
            Logger.Log(LogType.Log, "Serializing", 2);
            using (Stream s = File.Create(output))
            {
                FLSerializer.SaveProgram(s, prog, FLData.Container.InstructionSet, ExtraSteps);
            }
        }

        protected override void AddCommands(Runner runner)
        {
            base.AddCommands(runner);
            runner._AddCommand(new SetDataCommand(s => Defines = s, new[] { "--defines", "-d" }, "Set Define Tags"));
            runner._AddCommand(
                               new SetDataCommand(
                                                  s => ExtraSteps = s,
                                                  new[] { "--extra-steps", "-e" },
                                                  "Set Extra Serialization Steps"
                                                 )
                              );
        }

    }
}