using CommandlineSystem;

namespace OpenFL.Commandline
{
    internal class Program
    {

        private static void Main(string[] args)
        {
            CommandlineCore.Run(
                                args,
                                "https://open-fl.github.io/OpenFL.Commandline/latest/fl.zip",
                                "https://open-fl.github.io/OpenFL.Commandline/latest/fl-systems.zip"
                               );
        }

    }
}