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
using Utility.ADL.Configs;
using Utility.ExtPP.Base;
using Utility.IO.Callbacks;
using Utility.IO.VirtualFS;
using Utility.TypeManagement;

namespace OpenFL.Commandline.Core
{
    public static class FLData
    {

        private static readonly PluginHost Host = new PluginHost();

        private static readonly ADLLogger<LogType> pluginLogger = new ADLLogger<LogType>(
             new ProjectDebugConfig("FL-Cmd", -1, PrefixLookupSettings.AddPrefixIfAvailable)
            );

        private static bool NoDialogs;

        public static FLDataContainer Container { get; private set; }

        public static event Action CustomStartupActions;

        public static void InitializePluginSystemOnly(bool noDialogs, int verbosity)
        {
            NoDialogs = noDialogs;
            int maxTasks = 3;

            SetProgress("[Setup]", "Initializing Logging System", 1, 1, maxTasks);
            InitializeLogging((Verbosity) verbosity);

            SetProgress("[Setup]", "Initializing Resource System", 1, 2, maxTasks);
            InitializeResourceSystem();

            SetProgress("[Setup]", "Initializing Plugin System", 1, 3, maxTasks);
            InitializePluginSystem();
        }

        public static void InitializeFL(bool noDialogs, int verbosity, FLProgramCheckType checkType)
        {
            NoDialogs = noDialogs;
            int maxTasks = 6;

            Log("[Setup]", "Initializing FS", 1);
            PrepareFileSystem();

            SetProgress("[Setup]", "Initializing Logging System", 1, 1, maxTasks);
            InitializeLogging((Verbosity) verbosity);

            SetProgress("[Setup]", "Initializing Resource System", 1, 2, maxTasks);
            InitializeResourceSystem();

            SetProgress("[Setup]", "Initializing Plugin System", 1, 3, maxTasks);
            InitializePluginSystem();

            PluginManager.LoadPlugins(Host);

            SetProgress("[Setup]", "Running Custom Actions", 1, 4, maxTasks);
            CustomStartupActions?.Invoke();

            SetProgress("[Setup]", "Initializing FL", 1, 5, maxTasks);
            Container = InitializeCLKernels("resources/kernel");

            FLProgramCheckBuilder builder = FLProgramCheckBuilder.CreateDefaultCheckBuilder(Container.InstructionSet, Container.BufferCreator, checkType);
            Container.SetCheckBuilder(builder);

            SetProgress("[Setup]", "Finished", 1, 6, maxTasks);
        }

        private static void PrepareFileSystem()
        {
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
        }

        public static void Log(string tag, string message, int severity)
        {
            pluginLogger.Log(LogType.Log, $"{tag}{message}", severity);
        }

        public static bool ShowDialog(string tag, string title, string message)
        {
            if (NoDialogs)
            {
                return true;
            }

            Log($"{tag}[DIALOG]", $"{title} :\n\t{message} [Y/n]", 0);
            Console.Write(">");
            string s = Console.ReadLine();
            return s.ToLower() != "n";
        }

        private static void InitializeLogging(Verbosity verbosity)
        {
            OpenCLDebugConfig.Settings.MinSeverity = verbosity;
            OpenFLDebugConfig.Settings.MinSeverity = verbosity;
            InternalADLProjectDebugConfig.Settings.MinSeverity = verbosity;
            ManifestIODebugConfig.Settings.MinSeverity = verbosity;
            ExtPPDebugConfig.Settings.MinSeverity = verbosity;

            Debug.DefaultInitialization();
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

        public static void SetProgress(string tag, string status, int severity, int current, int max)
        {
            Log($"{tag}[Progress][{current}/{max}]", status, severity);
        }

        private static void InitializePluginSystem()
        {
            PluginManager.SetLogEventHandler(args => Log("[PM]", args.Message, 2));
            PluginManager.Initialize(
                                     Path.Combine(PluginPaths.EntryDirectory, "data"),
                                     "internal",
                                     "plugins",
                                     (title, msg) => ShowDialog("[PM]", title, msg),
                                     (status, current, max) => SetProgress("[PM]", status, 1, current, max),
                                     Path.Combine(PluginPaths.EntryDirectory, "static-data.sd")
                                    );
        }

        private static FLDataContainer InitializeCLKernels(string kernelPath)
        {
            {
                CLAPI instance = CLAPI.GetInstance();
                Log("[CL-KERNELS]", "Discovering Files in Path: " + kernelPath, 1);
                string[] files = IOManager.DirectoryExists(kernelPath)
                                     ? IOManager.GetFiles(kernelPath, "*.cl")
                                     : new string[0];

                if (files.Length == 0)
                {
                    Log("[CL-KERNELS]", "Error: No Files found at path: " + kernelPath, 1);
                }

                KernelDatabase dataBase = new KernelDatabase(DataVectorTypes.Uchar1);
                List<CLProgramBuildResult> results = new List<CLProgramBuildResult>();
                bool throwEx = false;
                int kernelCount = 0;
                int fileCount = 0;

                foreach (string file in files)
                {
                    Log(
                        "[CL-KERNELS]",
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
                        Log("[CL-KERNELS]", "ERROR: " + e.Message, 2);
                    }

                    fileCount++;
                }


                Log("[CL-KERNELS]", "Kernels Loaded: " + kernelCount, 1);


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