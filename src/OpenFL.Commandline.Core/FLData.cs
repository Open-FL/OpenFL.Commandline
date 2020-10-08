using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using OpenCL.Wrapper;
using OpenCL.Wrapper.TypeEnums;

using OpenFL.Core;
using OpenFL.Core.Buffers.BufferCreators;
using OpenFL.Core.Instructions.InstructionCreators;
using OpenFL.Core.ProgramChecks;
using OpenFL.Parsing;

using PluginSystem.Core;
using PluginSystem.Core.Interfaces;
using PluginSystem.Core.Pointer;
using PluginSystem.FileSystem;

using Utility.ADL;
using Utility.IO.Callbacks;
using Utility.IO.VirtualFS;
using Utility.TypeManagement;

namespace OpenFL.Commandline.Core
{
    public static class FLData
    {

        private static readonly PluginHost Host = new PluginHost();

        private static readonly ADLLogger<LogType> Logger = new ADLLogger<LogType>(OpenFLDebugConfig.Settings, "CLI");


        private static bool NoDialogs;

        public static FLDataContainer Container { get; private set; }

        public static event Action CustomStartupActions;

        public static void InitializePluginSystemOnly(bool noDialogs)
        {
            NoDialogs = noDialogs;
            int maxTasks = 3;

            SetProgress("Initializing Logging System", 0, 1, maxTasks);

            SetProgress("Initializing Resource System", 0, 2, maxTasks);
            InitializeResourceSystem();

            SetProgress("Initializing Plugin System", 0, 3, maxTasks);
            InitializePluginSystem();
        }

        public static void InitializeFL(bool noDialogs, FLProgramCheckType checkType)
        {
            NoDialogs = noDialogs;
            int maxTasks = 6;

            Logger.Log(LogType.Log, "Initializing FS", 1);
            PrepareFileSystem();

            SetProgress("Initializing Logging System", 0, 1, maxTasks);
            Debug.DefaultInitialization();

            SetProgress("Initializing Resource System", 0, 2, maxTasks);
            InitializeResourceSystem();

            SetProgress("Initializing Plugin System", 0, 3, maxTasks);
            InitializePluginSystem();

            PluginManager.LoadPlugins(Host);

            SetProgress("Running Custom Actions", 0, 4, maxTasks);
            CustomStartupActions?.Invoke();

            SetProgress("Initializing FL", 0, 5, maxTasks);
            Container = InitializeCLKernels("resources/kernel");

            FLProgramCheckBuilder builder =
                FLProgramCheckBuilder.CreateDefaultCheckBuilder(
                                                                Container.InstructionSet,
                                                                Container.BufferCreator,
                                                                checkType
                                                               );
            Container.SetCheckBuilder(builder);

            SetProgress("Finished", 0, 6, maxTasks);
        }

        private static void PrepareFileSystem()
        {
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
        }

        //public static void Log(string message, int severity)
        //{
        //    pluginLogger.Log(LogType.Log, message, severity);
        //}

        public static bool ShowDialog(string tag, string title, string message)
        {
            if (NoDialogs)
            {
                return true;
            }

            Logger.Log(LogType.Log, $"{title} :\n\t{message} [Y/n]", 0);
            Console.Write(">");
            string s = Console.ReadLine();
            return s.ToLower() != "n";
        }

        private static void InitializeResourceSystem()
        {
            TypeAccumulator.RegisterAssembly(typeof(OpenFLDebugConfig).Assembly);
            ManifestReader.RegisterAssembly(Assembly.GetExecutingAssembly());
            ManifestReader.RegisterAssembly(typeof(FLRunner).Assembly);
            ManifestReader.PrepareManifestFiles(false);
            ManifestReader.PrepareManifestFiles(true);
            EmbeddedFileIOManager.Initialize();
        }

        public static void SetProgress(string status, int severity, int current, int max)
        {
            Logger.Log(LogType.Log, $"[{current}/{max}] {status}", severity);
        }

        private static void InitializePluginSystem()
        {
            PluginManager.SetLogEventHandler(args => Logger.Log(LogType.Log, args.Message, 2));
            PluginManager.Initialize(
                                     Path.Combine(PluginPaths.EntryDirectory, "data"),
                                     "internal",
                                     "plugins",
                                     (title, msg) => ShowDialog("[PM]", title, msg),
                                     (status, current, max) => SetProgress(status, 1, current, max),
                                     Path.Combine(PluginPaths.EntryDirectory, "static-data.sd")
                                    );
        }

        private static FLDataContainer InitializeCLKernels(string kernelPath)
        {
            {
                CLAPI instance = CLAPI.GetInstance();
                Logger.Log(LogType.Log, "Discovering Files in Path: " + kernelPath, 1);
                string[] files = IOManager.DirectoryExists(kernelPath)
                                     ? IOManager.GetFiles(kernelPath, "*.cl")
                                     : new string[0];

                if (files.Length == 0)
                {
                    Logger.Log(LogType.Error, "Error: No Files found at path: " + kernelPath, 1);
                }

                KernelDatabase dataBase = new KernelDatabase(DataVectorTypes.Uchar1);
                List<CLProgramBuildResult> results = new List<CLProgramBuildResult>();
                bool throwEx = false;
                int kernelCount = 0;
                int fileCount = 0;

                foreach (string file in files)
                {
                    Logger.Log(
                               LogType.Log,
                               $"[{fileCount}/{files.Length}]Loading: {file} ({kernelCount})",
                               2
                              );
                    try
                    {
                        CLProgram prog = dataBase.AddProgram(instance, file, false, out CLProgramBuildResult res);
                        kernelCount += prog.ContainedKernels.Count;
                        throwEx |= !res;
                        results.Add(res);
                    }
                    catch (Exception e)
                    {
                        Logger.Log(LogType.Error, "ERROR: " + e.Message, 2);
                    }

                    fileCount++;
                }


                Logger.Log(LogType.Log, "Kernels Loaded: " + kernelCount, 1);


                FLInstructionSet iset = FLInstructionSet.CreateWithBuiltInTypes(dataBase);
                BufferCreator creator = BufferCreator.CreateWithBuiltInTypes();
                FLParser parser = new FLParser(iset, creator, new WorkItemRunnerSettings(true, 2));

                return new FLDataContainer(instance, iset, creator, parser);
            }
        }

        public class PluginHost : IPluginHost
        {

            public bool IsAllowedPlugin(IPlugin plugin)
            {
                return true;
            }

            public void OnPluginLoad(IPlugin plugin, BasePluginPointer ptr)
            {
            }

            public void OnPluginUnload(IPlugin plugin)
            {
            }

        }

    }
}