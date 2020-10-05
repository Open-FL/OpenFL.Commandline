using System;
using System.Drawing;
using System.IO;
using System.Linq;

using OpenFL.Core;
using OpenFL.Core.Buffers;
using OpenFL.Core.DataObjects.ExecutableDataObjects;
using OpenFL.Core.DataObjects.SerializableDataObjects;
using OpenFL.Core.Exceptions;

using Utility.ADL;
using Utility.CommandRunner;

namespace OpenFL.Commandline.Core.Systems
{
    public class FLRunSystem : FLCommandlineSystem
    {

        private string[] defines = new string[0];
        private int resolutionX = 256;
        private int resolutionY = 256;

        private bool warmBuffers;


        public override bool ExpandInputDirectories => true;

        public override string Name => "run";

        public override string[] SupportedInputExtensions => new[] { "fl", "flc" };

        public override string[] SupportedOutputExtensions => new[] { "png", "bmp" };

        protected override void Run(string input, string output)
        {
            FLBuffer buffer = FLData.Container.CreateBuffer(resolutionX, resolutionY, 1, "Input");
            SerializableFLProgram prog;
            if (Path.GetExtension(input) == "flc")
            {
                Logger.Log(LogType.Log, "Loading", 2);
                prog = Load(input);
            }
            else
            {
                Logger.Log(LogType.Log, "Parsing", 2);
                prog = Parse(input, defines);
            }

            Logger.Log(LogType.Log, "Building", 2);
            FLProgram program = prog.Initialize(FLData.Container);


            try
            {
                Logger.Log(LogType.Log, "Running", 2);
                program.Run(buffer, false, null, warmBuffers);

                Logger.Log(LogType.Log, "Saving", 2);
                Bitmap bmp = program.GetActiveBitmap();
                bmp.Save(output);
                bmp.Dispose();
            }
            catch (FLInvalidEntryPointException)
            {
                Logger.Log(LogType.Log, "No Entry Point Found. Skipping", 2);
            }

            program.FreeResources();
            buffer.Dispose();
        }


        protected override void AddCommands(Runner runner)
        {
            runner._AddCommand(new SetDataCommand(s => defines = s, new[] { "--defines", "-d" }, "Set Define Tags"));
            runner._AddCommand(
                               new SetDataCommand(
                                                  s => warmBuffers = true,
                                                  new[] { "--warm", "-w" },
                                                  "Warm buffers before running"
                                                 )
                              );
            runner._AddCommand(
                               new SetDataCommand(
                                                  s =>
                                                  {
                                                      if (!int.TryParse(s.First(), out resolutionX))
                                                      {
                                                          throw new InvalidCastException(
                                                               $"Can not Convert {s} into type int"
                                                              );
                                                      }
                                                  },
                                                  new[] { "--x", "-x" },
                                                  "Set X Resolution"
                                                 )
                              );
            runner._AddCommand(
                               new SetDataCommand(
                                                  s =>
                                                  {
                                                      if (!int.TryParse(s.First(), out resolutionY))
                                                      {
                                                          throw new InvalidCastException(
                                                               $"Can not Convert {s} into type int"
                                                              );
                                                      }
                                                  },
                                                  new[] { "--y", "-y" },
                                                  "Set Y Resolution"
                                                 )
                              );
        }

    }
}