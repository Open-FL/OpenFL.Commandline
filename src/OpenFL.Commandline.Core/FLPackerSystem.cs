using System;
using System.IO;
using System.Linq;

using CommandlineSystem;

using OpenFL.Core.DataObjects.SerializableDataObjects;
using OpenFL.ResourceManagement;

using PluginSystem.StartupActions;

using Utility.CommandRunner;
using Utility.CommandRunner.BuiltInCommands;
using Utility.ProgressFeedback;

namespace OpenFL.Commandline.Core
{
    public class FLRepositorySystem : ICommandlineSystem
    {

        public string Name => "plugins";
        private string[] Adds = new string[0];
        private string[] Removes = new string[0];

        public void Run(string[] args)
        {
            Runner r = new Runner();
            r._AddCommand(new DefaultHelpCommand());
            r._AddCommand(new SetDataCommand(strings => Adds = strings, new[] { "--add", "-a" }, "Adds a Plugin package"));
            r._AddCommand(new SetDataCommand(strings => Removes = strings, new[] { "--remove", "-r" }, "Removes a Plugin package"));

            r._RunCommands(args);

            FLData.InitializePluginSystemOnly(true);

            foreach (string remove in Removes)
            {
                ActionRunner.AddActionToStartup($"{ActionRunner.REMOVE_PACKAGE_ACTION} {remove}");
            }

            foreach (string adds in Adds)
            {
                ActionRunner.AddActionToStartup($"{ActionRunner.ADD_ACTIVATE_PACKAGE_ACTION} {adds}");
            }

        }


    }

    public class FLUnpackerSystem : FLCommandlineSystem
    {

        public override string Name => "unpacker";

        public override string[] SupportedInputExtensions =>new [] { "flres" };

        public override string[] SupportedOutputExtensions => new []{""};

        protected override void Run(string input, string output)
        {
            string name = ResourceManager.Load(input);
            ResourceManager.Activate(name, null, output);
        }

        protected override void BeforeRun()
        {
            ResourceManager.AddUnpacker(new FL2FLCUnpacker(FLData.Container));
            ResourceManager.AddUnpacker(new FL2TexUnpacker(FLData.Container));
            ResourceManager.AddUnpacker(new FLC2TexUnpacker(FLData.Container));
            ResourceManager.AddUnpacker(new FLRESUnpacker());
        }

        protected override void AddCommands(Runner runner)
        {
        }

    }

    public class FLPackerSystem : FLCommandlineSystem
    {

        public override string[] SupportedInputExtensions => new[] { "" };

        public override string[] SupportedOutputExtensions => new[] { "flres" };

        public override string Name => "packer";

        private bool ExportFL;
        private bool KeepFL;
        private string PackageName = "NO_NAME";
        private string UnpackConfig = "default";
        private string[] Defines = new string[0];
        private string[] ExtraSteps;

        protected override void AddCommands(Runner runner)
        {
            runner._AddCommand(new SetDataCommand(s => Defines = s, new[] { "--defines", "-d" }, "Set Define Tags"));
            runner._AddCommand(new SetDataCommand(s => ExtraSteps = s, new[] { "--extra-steps", "-e" }, "Set Extra Serialization Steps"));
            runner._AddCommand(new SetDataCommand(s => KeepFL =true, new[] { "--keep-fl" }, "When Exporting FL the FL Scripts are not Deleted"));
            runner._AddCommand(new SetDataCommand(s => ExportFL=true, new[] { "--export-fl", "-export" }, "Export FL Scripts to FLC before packing"));
            runner._AddCommand(new SetDataCommand(s => PackageName = s.First(), new[] { "--name", "-n" }, "Set Package Name"));
            runner._AddCommand(new SetDataCommand(s => UnpackConfig = s.First(), new[] { "--unpack-config", "-u" }, "Set Unpack Config"));
        }


        protected override void Run(string input, string output)
        {
            int maxProgress = 2;
            int currentProgress = 1;
            string oldDir = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(input);
            if (ExportFL)
            {

                string dir = Path.Combine(
                                          oldDir,
                                          "temp_" +
                                          Path.GetFileNameWithoutExtension(Directory.GetCurrentDirectory())
                                         );
                CopyDirectory(Directory.GetCurrentDirectory(), dir);
                Directory.SetCurrentDirectory(dir);
                string[] files = Directory.GetFiles(dir, "*.fl", SearchOption.AllDirectories);

                maxProgress += files.Length;
                FLData.SetProgress($"[{Name}]", "Exporting FL Scripts..", 1, currentProgress, maxProgress);
                currentProgress++;
                for (int i = 0; i < files.Length; i++)
                {
                    string file = files[i];

                    FLData.SetProgress($"[{Name}]", "Exporting File:" + file, 2, currentProgress, maxProgress);
                    currentProgress++;

                    SerializableFLProgram prog = Parse(file, Defines);

                    string f = file + "c";
                    Save(f, prog, ExtraSteps);

                    if (!KeepFL)
                    {
                        File.Delete(file);
                    }
                }
            }

            FLData.SetProgress($"[{Name}]", "Creating Package.", 1, currentProgress, maxProgress);
            currentProgress++;

            Directory.SetCurrentDirectory(oldDir);
            ResourceManager.Create(
                                   input,
                                   output,
                                   PackageName,
                                   UnpackConfig,
                                   null
                                  );

            FLData.SetProgress($"[{Name}]", "Cleaning Up.", 1, currentProgress, maxProgress);


        }

        private void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);
            if (!Directory.Exists(source))
            {
                return;
            }

            string[] files = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                string dstFile = file.Replace(source, target);
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dstFile));
                    File.Copy(file, dstFile);
                }
                catch (Exception e)
                {
                }
            }
        }

    }
}