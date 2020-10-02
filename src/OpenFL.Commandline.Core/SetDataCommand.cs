using System;

using Utility.ADL.Streams;
using Utility.CommandRunner;
using Utility.CommandRunner.BuiltInCommands.SetSettings;

namespace OpenFL.Commandline.Core
{
    public class SetDataCommand : AbstractCommand
    {

        public SetDataCommand(Action<string[]> setData, string[] keys, string helpText = "No Help Text Available", bool defaultCommand = false) : base(keys, helpText, defaultCommand)
        {
            CommandAction = (info, strings) => setData?.Invoke(strings);
        }

    }
}
