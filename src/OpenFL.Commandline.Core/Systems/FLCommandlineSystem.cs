﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using CommandlineSystem;

using OpenFL.Core.DataObjects.SerializableDataObjects;
using OpenFL.Core.Parsing.StageResults;
using OpenFL.Serialization;

using Utility.ADL.Configs;
using Utility.CommandRunner;
using Utility.CommandRunner.BuiltInCommands;

namespace OpenFL.Commandline.Core.Systems
{
    public abstract class FLCommandlineSystem : ICommandlineSystem
    {

        private string[] Input = new string[0];
        private bool NoDialog;

        private string[] Output = new string[0];

        private int Verbosity = 1;


        public virtual bool ExpandInputDirectories => false;

        public abstract string[] SupportedInputExtensions { get; }

        public abstract string[] SupportedOutputExtensions { get; }

        public abstract string Name { get; }

        public void Run(string[] args)
        {
            ProjectDebugConfig.OnConfigCreate += ProjectDebugConfig_OnConfigCreate;

            Runner r = new Runner();
            r._AddCommand(new SetDataCommand(s => Input = s, new[] { "--input", "-i" }, "Set Input Files"));
            r._AddCommand(new SetDataCommand(s => Output = s, new[] { "--output", "-o" }, "Set Output Files"));
            r._AddCommand(
                          new SetDataCommand(
                                             s => NoDialog = true,
                                             new[] { "--yes", "-y" },
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
            r._AddCommand(new DefaultHelpCommand(true));
            AddCommands(r);
            r._RunCommands(args);

            FLData.InitializeFL(NoDialog, Verbosity);

            BeforeRun();

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
                                   $"[{Name}]",
                                   $"{Path.GetFileName(input)} => {Path.GetFileName(output)}",
                                   1,
                                   i,
                                   input.Length
                                  );
                if (!SupportedOutputExtensions.Select(x => string.IsNullOrEmpty(x) ? "" : "." + x)
                                              .Contains(Path.GetExtension(output)))
                {
                    throw new Exception("Extension is not supported: " + Path.GetExtension(output));
                }

                Run(input, output);
            }

            ProjectDebugConfig.OnConfigCreate -= ProjectDebugConfig_OnConfigCreate;
        }

        private void ProjectDebugConfig_OnConfigCreate(ProjectDebugConfig obj)
        {
            obj.MinSeverity = Verbosity;
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