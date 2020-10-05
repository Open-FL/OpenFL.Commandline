using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using CommandlineSystem;

using PluginSystem.Core.Pointer;
using PluginSystem.FileSystem;
using PluginSystem.Loading.Plugins;
using PluginSystem.Utility;

using Utility.CommandRunner;
using Utility.CommandRunner.BuiltInCommands;
using Utility.IO.Callbacks;
using Utility.IO.VirtualFS;

namespace OpenFL.Commandline.Core.Systems
{
    public class AssemblyBrowser : ICommandlineSystem
    {

        private string[] FileList;
        private string[] FileUnpack;
        private string[] List;
        private string[] Unpack;

        private int Verbosity = 4;

        public string Name => "asm-browser";

        public void Run(string[] args)
        {
            CommandRunnerDebugConfig.Settings.MinSeverity = Utility.ADL.Verbosity.Level3;
            Runner r = new Runner();
            r._AddCommand(
                          new SetDataCommand(
                                             strings => Verbosity = int.Parse(strings.First()),
                                             new[] { "--verbosity", "-v" },
                                             "The Verbosity Level (lower = less logs)"
                                            )
                         );
            r._AddCommand(new DefaultHelpCommand(true));
            r._AddCommand(
                          new SetDataCommand(
                                             strings => Unpack = strings,
                                             new[] { "--unpack", "-unpack" },
                                             "Unpacks a plugins assembly data by name"
                                            )
                         );
            r._AddCommand(
                          new SetDataCommand(
                                             strings => List = strings,
                                             new[] { "--list", "-list" },
                                             "Lists All loaded files or All files inside an assembly if arguments are passed"
                                            )
                         );
            r._AddCommand(
                          new SetDataCommand(
                                             strings => FileUnpack = strings,
                                             new[] { "--unpack-file", "-unpack-file" },
                                             "Unpacks a plugins assembly data by file"
                                            )
                         );
            r._AddCommand(
                          new SetDataCommand(
                                             strings => FileList = strings,
                                             new[] { "--list-file", "-list-file" },
                                             "Lists All loaded files or All files inside an assembly if arguments are passed"
                                            )
                         );

            FLData.InitializePluginSystemOnly(true);

            r._RunCommands(args);

            IEnumerable<BasePluginPointer> global =
                ListHelper.LoadList(PluginPaths.GlobalPluginListFile).Select(x => new BasePluginPointer(x));

            Run(y => global.FirstOrDefault(x => x.PluginName == y)?.PluginFile, Unpack, List);

            Run(Path.GetFullPath, FileUnpack, FileList);
        }

        private void Run(Func<string, string> assemblyFileResolve, string[] unpack, string[] list)
        {
            if (unpack != null)
            {
                if (unpack.Length == 0)
                {
                    Console.WriteLine(
                                      "Invalid Unpack Argument Count.\nExpected: <plugin-name> or <plugin-name> <folder1> <folder2> ..."
                                     );
                }
                else
                {
                    string ptr = assemblyFileResolve(unpack[0]);
                    if (ptr == null)
                    {
                        Console.WriteLine("Can not find plugin with that name.");
                    }
                    else if (unpack.Length == 1) //Specific Plugin
                    {
                        Assembly asm = PluginLoader.SaveLoadFrom(ptr);

                        if (asm == null)
                        {
                            Console.WriteLine("Can not Load Plugin: " + ptr);
                        }

                        string[] files = ManifestReader.AllFilesFromAssembly(asm);
                        UnpackResources(files);
                    }
                    else if (unpack.Length > 1) //Folders from a specific plugin
                    {
                        Assembly asm = PluginLoader.SaveLoadFrom(ptr);

                        if (asm == null)
                        {
                            Console.WriteLine("Can not Load Plugin: " + ptr);
                        }

                        IEnumerable<string> files = ManifestReader.AllFilesFromAssembly(asm)
                                                                  .Where(x => unpack.Skip(1).Any(x.StartsWith));
                        UnpackResources(files);
                    }
                }
            }
            else if (list != null)
            {
                IEnumerable<string> files = ManifestReader.Files;
                if (list.Length != 0)
                {
                    string ptr = assemblyFileResolve(list[0]);
                    if (ptr == null)
                    {
                        files = ManifestReader.Files;
                    }
                    else if (list.Length == 1) //Specific Plugin
                    {
                        Assembly asm = PluginLoader.SaveLoadFrom(ptr);
                        files = ManifestReader.AllFilesFromAssembly(asm);
                    }
                    else if (list.Length > 1) //Folders from a specific plugin
                    {
                        Assembly asm = PluginLoader.SaveLoadFrom(ptr);
                        files = ManifestReader.AllFilesFromAssembly(asm).Where(x => list.Skip(1).Any(x.StartsWith));
                    }
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