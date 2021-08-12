using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;
using System.Configuration;
using System.Diagnostics;
using System.Net;
using System.IO.Compression;
using ArmaOP.Arma3;
using System.Threading;

namespace ArmaOP
{
    class Program
    {

        private static Settings _settings = SettingsManager.ReadConfig();

        public static Settings Settings {
            get {
                return _settings;
            }
            set {
                SettingsManager.WriteConfig(value);
            }
        }

        private static Dictionary<string, string> _localVars = new Dictionary<string, string>();
        private static Dictionary<string, string> _globalVars = new Dictionary<string, string>();
        private static Dictionary<string, string> _scriptFuncs = new Dictionary<string, string>();


        public static void DeleteDirectory(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                return;
            }

            var files = Directory.GetFiles(directoryPath);
            var directories = Directory.GetDirectories(directoryPath);

            foreach (var file in files)
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            foreach (var dir in directories)
            {
                DeleteDirectory(dir);
            }

            File.SetAttributes(directoryPath, FileAttributes.Normal);

            Directory.Delete(directoryPath, false);
        }

        static void Main()
        {
            Console.Title = "Arma Packer";
            Console.WriteLine("Welcome to Arma Packer");

            try {
                DeleteDirectory(Settings.GitDirectory);
                Directory.CreateDirectory(Settings.GitDirectory);
            } catch(Exception ex) {
                Console.WriteLine("Invalid git directory provided! Config is invalid?");
                Util.Assert(ex);
            }

            if (Settings.KillArmaServer) {
                Console.WriteLine("Ending Arma3Server processes");

                if (Settings.Use64BitServer)
                    EndTask("arma3server_x64.exe");
                else
                    EndTask("arma3server.exe");
            }

            if (Settings.UseArmaRemoteAdmin) {
                Console.WriteLine("Ending ArmaRemoteAdmin processes");
                EndTask("Maverick-ArmARemote.exe");
            }

            //Thread.Sleep(2000);
            Console.WriteLine("Generating Random Vars");
            foreach (string s in Settings.ObfLocalVars) {
                string newVar = "_" + Util.RandomString(Settings.RandomVarsLength);
                _localVars[s] = newVar;
                File.AppendAllText("variables.log", $"{s} => {newVar}\r\n");
            }
             

            foreach (string s in Settings.ObfGlobalVars) {
                string newVar = Util.RandomString(Settings.RandomVarsLength);
                _globalVars[s] = newVar;
                _globalVars[$"{s}_HC"] = $"{newVar}_HC";
                File.AppendAllText("variables.log", $"{s} => {newVar}\r\n");
            }
                

            foreach (string s in Settings.ObfFunctions) {
                string newFnc = Util.RandomString(Settings.RandomFuncsLength);
                string fnc = s.Replace(Settings.FunctionsTag + "_fnc_", "");
                _scriptFuncs[fnc] = newFnc;
                File.AppendAllText("variables.log", $"{s} => {newFnc}\r\n");
            }


            foreach (ServerMod sm in Settings.Mods) {
                Util.Assert(Util.GitDownload(sm), $"Downloading from git: {sm.Name}/{sm.GitUrl}");
                if (sm.ServerPath.EndsWith(".pbo"))
                {
                    RandomizeEverything(sm);
                    FileEdit.Pack(sm);
                } else
                {
                    string gitName = Path.GetFileNameWithoutExtension(sm.GitUrl);
                    string filePath = Path.Combine(Settings.GitDirectory, gitName, sm.Name);
                    string modPath = Path.Combine(Settings.GitDirectory, Path.GetFileName(sm.Name));

                    if (!File.Exists(modPath)) {
                        File.Copy(filePath, modPath);
                    }

                }
            }

            foreach (ServerMod sm in Settings.Mods) {
                if (sm.ServerPath.EndsWith(".pbo"))
                {
                    if (FileEdit.ObfuProcs.Count > 0) {
                        Console.WriteLine("Waiting for ObfuSQF to finish...");

                        while (FileEdit.ObfuProcs.Count > 0) {
                            List<Process> ps = new List<Process>(FileEdit.ObfuProcs);

                            foreach (Process p in ps) {
                                if (p.HasExited && FileEdit.ObfuProcs.Contains(p))
                                    FileEdit.ObfuProcs.Remove(p);
                            }
                            Thread.Sleep(100);
                        }

                        Console.WriteLine("ObfuSQF finished.");
                    }

                    Util.Assert(FileEdit.Move(sm), "Failed to move files");
                } else
                {
                    Util.Assert(FileEdit.MoveFile(sm), "Failed to move files");
                }
            }

            Console.WriteLine("All files moved.");
            if (Settings.UseArmaRemoteAdmin) {
                Util.StartAra();
            } else if (Settings.StartArma) {

                string serverName = Settings.Use64BitServer ? "arma3server_x64.exe" : "arma3server.exe";
                string armaServer = Path.Combine(Settings.ServerDirectory, serverName);
                //Process.Start(Settings.ServerDirectory + "/" + serverName);


                try {
                    Process.Start(new ProcessStartInfo
                    {
                        WorkingDirectory = Program.Settings.GitDirectory,
                        FileName = armaServer,
                        Arguments = Settings.ServerParams

                    });
                    Console.WriteLine($"Starting {serverName} with params\n {Settings.ServerParams}");

                    Thread.Sleep(2000);
                } catch (Exception ex) {
                    Console.WriteLine($"Failed to start Arma Server. {ex.Message}");
                    Util.Assert(ex);
                };
            }

            Console.WriteLine("All done, goodbye!");

        }

        #region Functions
        static public void EndTask(string taskname)
        {
            string processName = taskname;
            string fixstring = taskname.Replace(".exe", "");

            Process[] processes = Process.GetProcessesByName(taskname.Contains(".exe") ? fixstring : processName);

            foreach (Process process in processes)
            {
                process.Kill();
            }
        }

        static public void CopyFolder(string sourceFolder, string destFolder)
        {
            if (!Directory.Exists(destFolder))
                Directory.CreateDirectory(destFolder);
            string[] files = Directory.GetFiles(sourceFolder);
            foreach (string file in files)
            {
                string name = Path.GetFileName(file);
                string dest = Path.Combine(destFolder, name);
                File.Copy(file, dest);
            }
            string[] folders = Directory.GetDirectories(sourceFolder);
            foreach (string folder in folders)
            {
                string name = Path.GetFileName(folder);
                string dest = Path.Combine(destFolder, name);
                CopyFolder(folder, dest);
            }
        }

        static void RandomizeEverything(ServerMod sm)
        {
            string gitName = Path.GetFileNameWithoutExtension(sm.GitUrl);

            string folderPath = Path.Combine(Settings.GitDirectory, gitName, sm.Name);


            Console.WriteLine("Begining obfuscation");


            string[] files = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);

            foreach (string file in files) {

                if (file.EndsWith(".ogg") || file.EndsWith(".paa") || file.EndsWith(".jpg") || file.EndsWith(".png"))
                    continue;

                string outName = file;

                string contents = File.ReadAllText(file);

                if (Settings.RenameGlobalVars) {
                    contents = Util.RenameVars(contents, _globalVars);
                }

                if (Settings.RenameLocalVars) {
                    contents = Util.RenameVars(contents, _localVars);
                }

                if (Settings.RenameFuncs) {
                    foreach (KeyValuePair<string, string> kv in _scriptFuncs) {
                        contents = contents.Replace(kv.Key, kv.Value);

                        if (file.Contains(kv.Key)) {
                            outName = file.Replace(kv.Key, kv.Value);
                        }
                    }

                }

                if (sm.OneLine) {
                    if (file.EndsWith(".sqf")) {
                        contents = FileEdit.OneLine(contents);
                    }
                    contents = contents.Replace("\t", "");
                }

                File.WriteAllText(file, contents);
                if (file != outName) {
                    Console.WriteLine($"Filename: {file} => {outName}");
                    File.Move(file, outName);
                }
            }

            Console.WriteLine($"{sm.Name} obfuscated.");
        }

        
        #endregion
    }
}
