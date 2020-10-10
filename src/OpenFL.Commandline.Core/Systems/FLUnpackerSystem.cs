using OpenFL.ResourceManagement;

using Utility.CommandRunner;

namespace OpenFL.Commandline.Core.Systems
{
    public class FLUnpackerSystem : FLProcessingCommandlineSystem
    {

        public override string Name => "unpacker";

        public override string[] SupportedInputExtensions => new[] { "flres" };

        public override string[] SupportedOutputExtensions => new[] { "" };

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
        

    }
}