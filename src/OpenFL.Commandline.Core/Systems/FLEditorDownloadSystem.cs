using CommandlineSystem;

namespace OpenFL.Commandline.Core.Systems
{
    public class FLEditorDownloadSystem : ICommandlineSystem
    {

        public string Name => "download-fl-edit";

        public void Run(string[] args)
        {
            Bootstrap.InitiateBatchUpdate("fledit", "https://open-fl.github.io/OpenFL.Editor/latest/fledit.zip", null);
        }

    }
}