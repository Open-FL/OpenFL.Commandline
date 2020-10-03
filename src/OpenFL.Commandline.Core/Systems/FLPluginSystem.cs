using System.Linq;

using CommandlineSystem;

using PluginSystem.StartupActions;

using Utility.CommandRunner;
using Utility.CommandRunner.BuiltInCommands;

namespace OpenFL.Commandline.Core.Systems
{
    public class FLPluginSystem : ICommandlineSystem
    {

        public string Name => "plugins";
        private string[] Adds = new string[0];
        private string[] Removes = new string[0];
        private int Verbosity = 2;

        public void Run(string[] args)
        {
            Runner r = new Runner();
            r._AddCommand(new DefaultHelpCommand());
            r._AddCommand(new SetDataCommand(strings => Adds = strings, new[] { "--add", "-a" }, "Adds a Plugin package"));
            r._AddCommand(new SetDataCommand(strings => Removes = strings, new[] { "--remove", "-r" }, "Removes a Plugin package"));
            r._AddCommand(new SetDataCommand(strings => Verbosity = int.Parse(strings.First()), new[] { "--verbosity", "-v" }, "The Verbosity Level (lower = less logs)"));

            r._RunCommands(args);

            FLData.InitializePluginSystemOnly(true,Verbosity);

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
}