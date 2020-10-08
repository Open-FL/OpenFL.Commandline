using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using OpenCL.Wrapper;

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

        private int count;

        private string[] defines = new string[0];
        private bool exitRequested;
        private readonly object lockObject = new object();
        private int resolutionX = 256;
        private int resolutionY = 256;

        private readonly ConcurrentQueue<(FLBuffer, CLAPI, string, string)> saveQueue =
            new ConcurrentQueue<(FLBuffer, CLAPI, string, string)>();

        private Task[] saveThread;
        private int totalCount;
        private int useSaveThread;
        private bool warmBuffers;

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
                program.Run(buffer, true, null, warmBuffers);
                FLBuffer outBuffer = program.GetActiveBuffer(true);
                if (useSaveThread != 0)
                {
                    Interlocked.Increment(ref totalCount);
                    saveQueue.Enqueue((outBuffer, FLData.Container.Instance, input, output));
                }
                else
                {
                    Bitmap bmp = program.GetActiveBitmap();
                    bmp.Save(output);
                    bmp.Dispose();
                }

                program.FreeResources();
            }
            catch (FLInvalidEntryPointException)
            {
                Logger.Log(LogType.Log, "No Entry Point Found. Skipping", 2);
            }
        }

        protected override void BeforeRun()
        {
            base.BeforeRun();
            if (useSaveThread != 0)
            {
                saveThread = new Task[useSaveThread];
                for (int i = 0; i < useSaveThread; i++)
                {
                    int i1 = i;
                    saveThread[i] = Task.Run(() => SaveThreadLoop(i1));
                }
            }
        }

        protected override void AfterRun()
        {
            base.AfterRun();
            if (useSaveThread != 0)
            {
                lock (lockObject)
                {
                    exitRequested = true;
                }

                Task.WaitAll(saveThread);
            }
        }

        private void SaveThreadLoop(int id)
        {
            bool exit = false;
            while (!exit || !saveQueue.IsEmpty)
            {
                if (saveQueue.IsEmpty)
                {
                    Thread.Sleep(100);
                }
                else if (saveQueue.TryDequeue(out (FLBuffer, CLAPI, string, string) result))
                {
                    Interlocked.Increment(ref count);
                    if (result.Item1.Buffer.IsDisposed)
                    {
                        Logger.Log(LogType.Error, $"Buffer from file {Path.GetFileName(result.Item3)} is disposed.", 0);
                        continue;
                    }

                    Logger.Log(
                               LogType.Log,
                               $"[W:{id} {count}/{totalCount}]Saving File: {Path.GetFileName(result.Item3)} => {Path.GetFileName(result.Item4)}",
                               0
                              );
                    Bitmap bmp = new Bitmap(result.Item1.Width, result.Item1.Height);
                    CLAPI.UpdateBitmap(result.Item2, bmp, result.Item1.Buffer);
                    bmp.Save(result.Item4);
                    bmp.Dispose();
                    result.Item1.Dispose();
                }

                lock (lockObject)
                {
                    exit = exitRequested;
                }
            }
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
                                                      if (s.Length == 0 || !int.TryParse(s[0], out useSaveThread))
                                                      {
                                                          useSaveThread = 1;
                                                      }
                                                  },
                                                  new[] { "--use-save-thread", "-save-thread" },
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