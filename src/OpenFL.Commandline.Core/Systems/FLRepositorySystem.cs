using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

using CommandlineSystem;

using PluginSystem.Core;
using PluginSystem.Core.Interfaces;
using PluginSystem.Core.Pointer;
using PluginSystem.FileSystem;
using PluginSystem.Loading.Plugins;
using PluginSystem.Repository;
using PluginSystem.StartupActions;
using PluginSystem.Utility;

using Utility.CommandRunner;
using Utility.CommandRunner.BuiltInCommands;

namespace OpenFL.Commandline.Core.Systems
{
    public class FLEditorDownloadSystem : ICommandlineSystem
    {

        public string Name => "download-fl-edit";

        public void Run(string[] args)
        {
            Bootstrap.InitiateBatchUpdate("fledit", "", null);
        }

    }


    public class FLRepositorySystem : ICommandlineSystem
    {


        private RepositoryPlugin repoPlugin;
        public string Name => "repo";
        private string[] PackageAdds = new string[0];
        private string[] PackageAddsActivates = new string[0];
        private string[] PackageActivates = new string[0];
        private string[] PackageRemoves = new string[0];
        private string[] PackageDeactivates = new string[0];
        private string[] OriginAdds = new string[0];
        private string[] OriginRemoves = new string[0];
        private bool InstallAll;
        private bool DefaultOrigin;
        private bool ListPackages;
        private int Verbosity;


        public void Run(string[] args)
        {
            Runner r = new Runner();

            r._AddCommand(new DefaultHelpCommand());
            r._AddCommand(new SetDataCommand(strings => PackageAdds = strings, new[] { "--add", "-a" }, "Adds a Plugin package by name"));
            r._AddCommand(new SetDataCommand(strings => PackageAdds = strings, new[] { "--add-activate", "-aa" }, "Adds and activates a Plugin package by name"));
            r._AddCommand(new SetDataCommand(strings => PackageRemoves = strings, new[] { "--remove", "-r" }, "Removes a Plugin package by name"));
            r._AddCommand(new SetDataCommand(strings => PackageActivates = strings, new[] { "--activate", "-active" }, "Activates a Plugin package by name"));
            r._AddCommand(new SetDataCommand(strings => PackageDeactivates = strings, new[] { "--deactivate", "-d" }, "Deactivates a Plugin package by name"));
            r._AddCommand(new SetDataCommand(strings => OriginRemoves = strings, new[] { "--remove-origin", "-ro" }, "Removes an Origin Url from the Origins File"));
            r._AddCommand(new SetDataCommand(strings => OriginAdds = strings, new[] { "--add-origin", "-ao" }, "Adds an Origin Url to the Origins File"));
            r._AddCommand(new SetDataCommand(strings => DefaultOrigin = true, new[] { "--default-origin", "-default" }, "Writes the Default Origin File to Disk, overwriting the current origin file"));
            r._AddCommand(new SetDataCommand(strings => InstallAll = true, new[] { "--all", "-all" }, "Installs and activates All packages from all repositories"));
            r._AddCommand(new SetDataCommand(strings => ListPackages = true, new[] { "--list-packages", "-list" }, "Lists All packages from all repositories"));
            r._AddCommand(new SetDataCommand(strings => Verbosity = int.Parse(strings.First()), new[] { "--verbosity", "-v" }, "The Verbosity Level (lower = less logs)"));



            FLData.InitializePluginSystemOnly(true, Verbosity);



            r._RunCommands(args);

            if (DefaultOrigin)
            {
                string file = RepositoryPlugin.GetOriginFilePath(GetDefaultRepoPluginPointer());
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                WriteDefaultOrigin(file);
            }
            else
            {
                CheckOriginsExists(GetDefaultRepoPluginPointer());
            }

            RepositoryPlugin repo = GetPlugin();

            List<Repository> repos = repo.GetPlugins();
            IEnumerable<BasePluginPointer> global =
                ListHelper.LoadList(PluginPaths.GlobalPluginListFile).Select(x => new BasePluginPointer(x));
            IEnumerable<BasePluginPointer> active =
                ListHelper.LoadList(PluginPaths.PluginListFile).Select(x => new BasePluginPointer(x));
            if (ListPackages)
            {
                Console.WriteLine("Available Packages: ");
                foreach (Repository repository in repos)
                {
                    Console.WriteLine($"\tRepository: {repository.RepositoryOrigin}");
                    foreach (BasePluginPointer basePluginPointer in repository.Plugins)
                    {
                        BasePluginPointer installedPtr = global.FirstOrDefault(x => x.PluginOrigin == basePluginPointer.PluginOrigin);
                        bool contained = installedPtr != null;
                        bool installed = contained && active.Any(x => x.PluginOrigin == basePluginPointer.PluginOrigin);
                        string tag = installed ? "[ACTIVE]" : contained ? "[INSTALLED]" : "[NOT INSTALLED]";
                        Console.WriteLine($"\t\t{tag} {basePluginPointer.PluginName}");
                        Console.WriteLine($"\t\t\tVersion(Origin): {basePluginPointer.PluginVersion}");
                        Console.WriteLine($"\t\t\tVersion(Installed): {installedPtr?.PluginVersion?.ToString() ?? "NOT INSTALLED"}");
                    }
                }
            }

            if (InstallAll)
            {
                PackageAddsActivates = repos.SelectMany(x => x.Plugins.Select(y => y.PluginName)).ToArray();
            }

            foreach (string originRemove in OriginRemoves)
            {
                repo.RemoveOrigin(originRemove);
            }

            foreach (string originAdd in OriginAdds)
            {
                repo.AddOrigin(originAdd);
            }

            foreach (string packageDeactivate in PackageDeactivates)
            {
                ActionRunner.AddActionToStartup($"{ActionRunner.DEACTIVATE_PACKAGE_ACTION} {packageDeactivate}");
            }

            foreach (string packageRemove in PackageRemoves)
            {
                ActionRunner.AddActionToStartup($"{ActionRunner.REMOVE_PACKAGE_ACTION} {packageRemove}");
            }

            foreach (string packageAdd in PackageAdds)
            {
                string package = GetPackage(repos, packageAdd);
                if (package == null)
                {
                    PluginManager.SendLog("Can not Add Package. Url does not exist.");
                    continue;
                }
                ActionRunner.AddActionToStartup($"{ActionRunner.ADD_PACKAGE_ACTION} {package}");
            }

            foreach (string packageAddActivate in PackageAddsActivates)
            {
                string package = GetPackage(repos, packageAddActivate);

                if (package == null)
                {
                    PluginManager.SendLog("Can not Add Package. Url does not exist.");
                    continue;
                }
                ActionRunner.AddActionToStartup($"{ActionRunner.ADD_ACTIVATE_PACKAGE_ACTION} {package}");
            }

            foreach (string packageActivate in PackageActivates)
            {
                ActionRunner.AddActionToStartup($"{ActionRunner.ACTIVATE_PACKAGE_ACTION} {packageActivate}");
            }

        }

        private string GetPackage(List<Repository> repos, string name)
        {
            return repos.FirstOrDefault(x => x.Plugins.Any(y => y.PluginName == name))?.Plugins
                        .First(x => x.PluginName == name).PluginOrigin;
        }

        private PluginAssemblyPointer GetDefaultRepoPluginPointer()
        {
            return new PluginAssemblyPointer(
                                             "repository-plugin",
                                             "",
                                             "",
                                             "0.0.0.0",
                                             PluginManager.PluginHost
                                            );
        }

        private RepositoryPlugin GetPlugin()
        {
            if (repoPlugin == null)
            {
                repoPlugin = PluginManager.GetPlugins<RepositoryPlugin>().FirstOrDefault();
                if (repoPlugin == null)
                {
                    repoPlugin = new RepositoryPlugin();
                    PluginAssemblyPointer ptr = GetDefaultRepoPluginPointer();

                    PluginManager.AddPlugin(
                                            repoPlugin,
                                            ptr
                                           );
                }
            }

            return repoPlugin;
        }

        private void CheckOriginsExists(PluginAssemblyPointer ptr)
        {
            string file = RepositoryPlugin.GetOriginFilePath(ptr);
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            if (!File.Exists(file))
            {
                bool res = FLData.ShowDialog(
                                             "[repo]",
                                             $"{ptr.PluginName}: First Startup.",
                                             "Do you want to create the default Origins File?"
                                            );
                if (res)
                {
                    WriteDefaultOrigin(file);
                }
            }

        }

        private void WriteDefaultOrigin(string originFile)
        {
            File.WriteAllText(originFile, GetDefaultOrigin());
        }

        private string GetDefaultOrigin()
        {
            using (WebClient wc = new WebClient())
            {
                return wc.DownloadString("https://open-fl.github.io/RepositoryOrigins/default-origin.txt");
            }
        }
    }
}