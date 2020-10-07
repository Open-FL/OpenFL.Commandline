using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
        private object lockObject = new object();
        private bool exitRequested = false;

        private bool warmBuffers;

        private ConcurrentQueue<(FLProgram, FLBuffer, string)> saveQueue = new ConcurrentQueue<(FLProgram, FLBuffer, string)>();


        public override bool ExpandInputDirectories => true;

        public override string Name => "run";

        public override string[] SupportedInputExtensions => new[] { "fl", "flc" };

        public override string[] SupportedOutputExtensions => new[] { "png", "bmp" };

        protected override void Run(string input, string output)
        {
            FLBuffer buffer = FLData.Container.CreateBuffer(resolutionX, resolutionY, 1, "Input", true);
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

                saveQueue.Enqueue((program, buffer, output));
                
            }
            catch (FLInvalidEntryPointException)
            {
                Logger.Log(LogType.Log, "No Entry Point Found. Skipping", 2);
            }
            
        }

        protected override void BeforeRun()
        {
            base.BeforeRun();
            Task saveTask = new Task(SaveThreadLoop);
            saveTask.Start();
        }


        protected override void AfterRun()
        {
            base.AfterRun();
            lock (lockObject) exitRequested = true;
        }

        private void SaveThreadLoop()
        {
            bool exit = false;
            do
            {
                if (saveQueue.IsEmpty)
                    Thread.Sleep(100);
                else if (saveQueue.TryDequeue(out (FLProgram, FLBuffer, string) result))
                {
                    Bitmap bmp = result.Item1.GetActiveBitmap();
                    Logger.Log(LogType.Log, "Saving: " + result.Item3, 2);
                    bmp.Save(result.Item3);
                    bmp.Dispose();
                    result.Item1.FreeResources();
                    result.Item2.Dispose();
                }

                lock (lockObject) exit = exitRequested;


            } while (!exit || !saveQueue.IsEmpty);
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