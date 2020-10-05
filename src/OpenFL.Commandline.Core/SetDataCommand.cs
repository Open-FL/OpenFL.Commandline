using System;

using Utility.CommandRunner;

namespace OpenFL.Commandline.Core
{
    public class SetDataCommand : AbstractCommand
    {

        public SetDataCommand(
            Action<string[]> setData, string[] keys, string helpText = "No Help Text Available",
            bool defaultCommand = false) : base(keys, helpText, defaultCommand)
        {
            CommandAction = (info, strings) => setData?.Invoke(strings);
        }

    }
}