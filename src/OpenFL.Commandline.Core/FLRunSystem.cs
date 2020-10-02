using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;

using CommandlineSystem;

using OpenCL.NET;

using OpenFL.Core;
using OpenFL.Core.Buffers;
using OpenFL.Core.DataObjects.ExecutableDataObjects;
using OpenFL.Core.DataObjects.SerializableDataObjects;
using OpenFL.Core.Exceptions;
using OpenFL.Core.Parsing.StageResults;

using Utility.CommandRunner;
using Utility.CommandRunner.BuiltInCommands;

namespace OpenFL.Commandline.Core
{
    public class FLRunSystem : FLCommandlineSystem
    {

        private string[] Input;
        private string[] Output;
        private string[] Defines = new string[0];
        private int ResolutionX = 256;
        private int ResolutionY = 256;

        private bool NoDialog;
        private bool WarmBuffers;


        public override bool ExpandInputDirectories => true;

        public override string Name => "run";

        public override string[] SupportedInputExtensions => new[] { "fl", "flc" };

        public override string[] SupportedOutputExtensions => new[] { "png", "bmp" };

        protected override void Run(string input, string output)
        {

            FLBuffer buffer = FLData.Container.CreateBuffer(ResolutionX, ResolutionY, 1, "Input");
            SerializableFLProgram prog;
            if (Path.GetExtension(input) == "flc")
            {
                FLData.Log($"[{Name}]", "Loading", 2);
                prog = Load(input);
            }
            else
            {
                FLData.Log($"[{Name}]", "Parsing", 2);
                prog = Parse(input, Defines);
            }
            FLData.Log($"[{Name}]", "Building", 2);
            FLProgram program = prog.Initialize(FLData.Container);


            try
            {
                FLData.Log($"[{Name}]", "Running", 2);
                program.Run(buffer, false, null, WarmBuffers);

                FLData.Log($"[{Name}]", "Saving", 2);
                Bitmap bmp = program.GetActiveBitmap();
                bmp.Save(output);
                bmp.Dispose();
            }
            catch (FLInvalidEntryPointException e)
            {
                FLData.Log($"[{Name}]", "No Entry Point Found. Skipping", 2);
            }
            
            program.FreeResources();
            buffer.Dispose();
        }


        protected override void AddCommands(Runner runner)
        {
            runner._AddCommand(new SetDataCommand(s => Defines = s, new[] { "--defines", "-d" }, "Set Define Tags"));
            runner._AddCommand(new SetDataCommand(s => WarmBuffers = true, new[] { "--warm", "-w" }, "Warm buffers before running"));
            runner._AddCommand(new SetDataCommand(s =>
                                             {
                                                 if (!int.TryParse(s.First(), out ResolutionX))
                                                 {
                                                     throw new InvalidCastException(
                                                                                    $"Can not Convert {s} into type int"
                                                                                   );
                                                 }
                                             }, new[] { "--x", "-x" }, "Set X Resolution"));
            runner._AddCommand(new SetDataCommand(s =>
                                             {
                                                 if (!int.TryParse(s.First(), out ResolutionX))
                                                 {
                                                     throw new InvalidCastException(
                                                                                    $"Can not Convert {s} into type int"
                                                                                   );
                                                 }
                                             }, new[] { "--y", "-y" }, "Set Y Resolution"));
        }

    }
}