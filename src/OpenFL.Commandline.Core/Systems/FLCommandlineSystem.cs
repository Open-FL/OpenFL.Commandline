using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using CommandlineSystem;

using OpenFL.Core;
using OpenFL.Core.DataObjects.SerializableDataObjects;
using OpenFL.Core.Parsing.StageResults;
using OpenFL.Core.ProgramChecks;
using OpenFL.Serialization;

using Utility.ADL;
using Utility.ADL.Configs;
using Utility.CommandRunner;
using Utility.CommandRunner.BuiltInCommands;
using Utility.FastString;
using Utility.ObjectPipeline;

namespace OpenFL.Commandline.Core.Systems
{
    public abstract class FLProcessingCommandlineSystem : FLCommandlineSystem
    {

        private string[] Input = new string[0];
        private string[] Output = new string[0];

        public virtual bool ExpandInputDirectories => false;

        public abstract string[] SupportedInputExtensions { get; }

        public abstract string[] SupportedOutputExtensions { get; }

        protected override void AddCommands(Runner runner)
        {
            runner._AddCommand(new SetDataCommand(s => Input = s, new[] { "--input", "-i" }, "Set Input Files"));
            runner._AddCommand(new SetDataCommand(s => Output = s, new[] { "--output", "-o" }, "Set Output Files"));
        }

        protected abstract void Run(string input, string output);

        protected override void DoRun(string[] args)
        {
            if (ExpandInputDirectories)
            {
                List<string> newInput = new List<string>();
                foreach (string s in Input)
                {
                    if (Path.GetExtension(s) == "")
                    {
                        newInput.AddRange(Directory.GetFiles(s, "*.flc", SearchOption.AllDirectories));
                        newInput.AddRange(Directory.GetFiles(s, "*.fl", SearchOption.AllDirectories));
                    }
                    else
                    {
                        newInput.Add(s);
                    }
                }

                Input = newInput.ToArray();
            }

            for (int i = 0; i < Input.Length; i++)
            {
                string input = Path.GetFullPath(Input[i]);
                if (!SupportedInputExtensions.Select(x => string.IsNullOrEmpty(x) ? "" : "." + x)
                                             .Contains(Path.GetExtension(input)))
                {
                    throw new Exception("Extension is not supported: " + Path.GetExtension(input));
                }

                string output = Output.Length <= i
                                    ? Path.Combine(
                                                   Path.GetDirectoryName(input),
                                                   Path.GetFileNameWithoutExtension(input) +
                                                   "." +
                                                   SupportedOutputExtensions.First()
                                                  )
                                    : Output[i];
                FLData.SetProgress(
                                   $"{Path.GetFileName(input)} => {Path.GetFileName(output)}",
                                   0,
                                   i + 1,
                                   Input.Length
                                  );
                if (!SupportedOutputExtensions.Select(x => string.IsNullOrEmpty(x) ? "" : "." + x)
                                              .Contains(Path.GetExtension(output)))
                {
                    throw new Exception("Extension is not supported: " + Path.GetExtension(output));
                }

                Run(input, output);
            }
        }

    }


    public abstract class FLCommandlineSystem : ALoggable<LogType>, ICommandlineSystem
    {

        protected bool AbortRun = false;

        private FLProgramCheckType CheckTypes = FLProgramCheckType.InputValidation;

        private bool NoDialog;

        private int Verbosity = 1;


        protected FLCommandlineSystem() : base(OpenFLDebugConfig.Settings)
        {
            Logger.SetSubProjectName(Name);
        }

        public abstract string Name { get; }


        public void Run(string[] args)
        {
            Debug.OnConfigCreate += ProjectDebugConfig_OnConfigCreate;

            AbstractCommand.MIN_COMMAND_SEVERITY = 0;

            Runner r = new Runner();

            r._AddCommand(
                          new SetDataCommand(
                                             s => NoDialog = true,
                                             new[] { "--yes" },
                                             "Answer all dialogs with Yes"
                                            )
                         );
            r._AddCommand(
                          new SetDataCommand(
                                             strings => Verbosity = int.Parse(strings.First()),
                                             new[] { "--verbosity", "-v" },
                                             "The Verbosity Level (lower = less logs)"
                                            )
                         );
            r._AddCommand(
                          new SetDataCommand(
                                             strings => CheckTypes =
                                                            (FLProgramCheckType) Enum.Parse(
                                                                 typeof(FLProgramCheckType),
                                                                 strings.First(),
                                                                 true
                                                                ),
                                             new[] { "--checks", "-checks" },
                                             $"Program Check Profile. (Available: {Enum.GetNames(typeof(FLProgramCheckType)).Unpack(", ")})"
                                            )
                         );
            r._AddCommand(new DefaultHelpCommand(true));
            AddCommands(r);

            Debug.DefaultInitialization();
            r._RunCommands(args);
            if (AbortRun)
            {
                Console.ReadLine();
                return;
            }

            foreach (KeyValuePair<IProjectDebugConfig, List<ADLLogger>> keyValuePair in ADLLogger.GetReadOnlyLoggerMap()
            )
            {
                keyValuePair.Key.SetMinSeverity(Verbosity);
            }

            OpenFLDebugConfig.Settings.SetMinSeverity(Verbosity);

            FLData.InitializeFL(NoDialog, CheckTypes);

            BeforeRun();

            DoRun(args);

            AfterRun();

            Debug.OnConfigCreate -= ProjectDebugConfig_OnConfigCreate;
        }

        private void ProjectDebugConfig_OnConfigCreate(IProjectDebugConfig obj)
        {
            obj.SetMinSeverity(Verbosity);
        }

        protected abstract void DoRun(string[] args);

        protected virtual void BeforeRun()
        {
        }

        protected virtual void AfterRun()
        {
        }


        protected abstract void AddCommands(Runner runner);

        protected SerializableFLProgram Parse(string input, string[] defines)
        {
            if (FLData.Container.CheckBuilder != null && !FLData.Container.CheckBuilder.IsAttached)
            {
                if (!FLData.Container.CheckBuilder.Attach(FLData.Container.Parser, true))
                {
                    throw new PipelineNotValidException(
                                                        FLData.Container.Parser,
                                                        "Check builder contains invalid Checks."
                                                       );
                }
            }

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