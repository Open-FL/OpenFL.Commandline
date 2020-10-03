using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Reflection;

using CommandlineSystem;

using PluginSystem.Core.Pointer;
using PluginSystem.FileSystem;
using PluginSystem.Loading.Plugins;
using PluginSystem.Utility;

using Utility.CommandRunner;
using Utility.IO.Callbacks;
using Utility.IO.VirtualFS;

namespace OpenFL.Commandline.Core.Systems
{
    public class AssemblyBrowser : ICommandlineSystem
    {

        public string Name => "asm-browser";

        private int Verbosity = 1;
        private string[] Unpack;


        public void Run(string[] args)
        {
            Runner r = new Runner();
            r._AddCommand(new SetDataCommand(strings => Verbosity = int.Parse(strings.First()), new[] { "--verbosity", "-v" }, "The Verbosity Level (lower = less logs)"));
            r._AddCommand(new SetDataCommand(strings => Unpack = strings, new[] { "--list-packages", "-list" }, "Lists All packages from all repositories"));

            FLData.InitializePluginSystemOnly(true, Verbosity);

            IEnumerable<BasePluginPointer> global =
                ListHelper.LoadList(PluginPaths.GlobalPluginListFile).Select(x => new BasePluginPointer(x));

            if (Unpack != null)
            {
                if (Unpack.Length == 0)
                {
                    Console.WriteLine("Invalid Unpack Argument Count.\nExpected: <plugin-name> or <plugin-name> <folder1> <folder2> ...");

                }
                else
                {
                    BasePluginPointer ptr = global.FirstOrDefault(x => x.PluginName == Unpack[0]);
                    if (ptr == null)
                    {
                        Console.WriteLine("Can not find plugin with that name.");
                    }
                    else if (Unpack.Length == 1) //Specific Plugin
                    {
                        Assembly asm = PluginLoader.SaveLoadFrom(PluginPaths.GetPluginAssemblyFile(ptr));
                        string[] files = ManifestReader.AllFilesFromAssembly(asm);
                        UnpackResources(files);


                    }
                    else if (Unpack.Length > 1) //Folders from a specific plugin
                    {
                        Assembly asm = PluginLoader.SaveLoadFrom(PluginPaths.GetPluginAssemblyFile(ptr));
                        IEnumerable<string> files = ManifestReader.AllFilesFromAssembly(asm).Where(x => Unpack.Skip(1).Any(x.StartsWith));
                        UnpackResources(files);
                    }
                }
            }
            else
            {
                IEnumerable<string> files = new string[0];
                BasePluginPointer ptr = global.FirstOrDefault(x => x.PluginName == Unpack[0]);
                if (ptr == null)
                {
                    files = ManifestReader.Files;
                }
                else if (Unpack.Length == 1) //Specific Plugin
                {
                    Assembly asm = PluginLoader.SaveLoadFrom(PluginPaths.GetPluginAssemblyFile(ptr));
                    files = ManifestReader.AllFilesFromAssembly(asm);
                }
                else if (Unpack.Length > 1) //Folders from a specific plugin
                {
                    Assembly asm = PluginLoader.SaveLoadFrom(PluginPaths.GetPluginAssemblyFile(ptr));
                    files = ManifestReader.AllFilesFromAssembly(asm).Where(x => Unpack.Skip(1).Any(x.StartsWith));
                }
                Console.WriteLine("Listing Assembly Files:");
                foreach (string file in files)
                {
                    Console.WriteLine("\t" + file);
                }
            }
        }


        private void UnpackResources(IEnumerable<string> files)
        {
            string workingDir = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(PluginPaths.EntryDirectory);

            foreach (string file in files)
            {
                string dir = Path.GetDirectoryName(file);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                if (!File.Exists(file))
                {
                    Stream s = IOManager.GetStream(file);
                    Stream dst = File.Create(file);
                    s.CopyTo(dst);
                    s.Dispose();
                    dst.Dispose();
                }
            }

            Directory.SetCurrentDirectory(workingDir);
        }

    }
}