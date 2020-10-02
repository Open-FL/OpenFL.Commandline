using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;

using CommandlineSystem;

using PluginSystem.Core;
using PluginSystem.Core.Interfaces;
using PluginSystem.Core.Pointer;
using PluginSystem.Repository;
using PluginSystem.StartupActions;

using Utility.CommandRunner;
using Utility.CommandRunner.BuiltInCommands;

namespace OpenFL.Commandline.Core.Systems
{
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


        public void Run(string[] args)
        {
            Runner r = new Runner();

            RepositoryPlugin repo = null;
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


            FLData.InitializePluginSystemOnly(true);

            repo = GetPlugin(DefaultOrigin);
            

            r._RunCommands(args);

            if (DefaultOrigin)
            {
                WriteDefaultOrigin(repo);
            }

            List<Repository> repos = repo.GetPlugins();

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

        private RepositoryPlugin GetPlugin(bool noOriginsCheck)
        {
            if (repoPlugin == null)
            {
                repoPlugin = PluginManager.GetPlugins<RepositoryPlugin>().FirstOrDefault();
                if (repoPlugin == null)
                {
                    repoPlugin = new RepositoryPlugin();
                    PluginAssemblyPointer ptr = new PluginAssemblyPointer(
                                                                          "repository-plugin",
                                                                          "",
                                                                          "",
                                                                          "0.0.0.0",
                                                                          PluginManager.PluginHost
                                                                         );

                    PluginManager.AddPlugin(
                                            repoPlugin,
                                            ptr
                                           );
                }
            }
            if (!noOriginsCheck && !File.Exists(repoPlugin.OriginFile))
            {
                bool res = FLData.ShowDialog(
                                             "[repo]",
                                             $"{repoPlugin.PluginAssemblyData.PluginName}: First Startup.",
                                             "Do you want to create the default Origins File?"
                                            );
                if (res)
                {
                    WriteDefaultOrigin(repoPlugin);
                }
            }

            return repoPlugin;
        }

        private void WriteDefaultOrigin(RepositoryPlugin repo)
        {
            File.WriteAllText(repo.OriginFile, GetDefaultOrigin());
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