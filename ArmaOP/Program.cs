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

        static void Main()
        {
            Console.Title = "Arma Packer";
            Console.WriteLine("Welcome to Arma Packer");

            try {
                if (Directory.Exists(Settings.GitDirectory))
                    Directory.Delete(Settings.GitDirectory, true);
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

            Thread.Sleep(2000);

            foreach (string s in Settings.ObfLocalVars) {
                string newVar = "_" + Util.RandomString(Settings.RandomVarsLength);
                _localVars[s] = newVar;
                File.AppendAllText("variables.log", $"{s} => {newVar}\r\n");
            }
             

            foreach (string s in Settings.ObfGlobalVars) {
                string newVar = Util.RandomString(Settings.RandomVarsLength);
                _globalVars[s] = newVar;
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

                RandomizeEverything(sm);
                FileEdit.Pack(sm);
            }

            foreach (ServerMod sm in Settings.Mods) {
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
            }

            if (Settings.UseArmaRemoteAdmin) {
                Console.WriteLine("All files moved.");
                Util.StartAra();
            } else {
                //string serverName = Settings.Use64BitServer ? "arma3server_x64.exe" : "arma3server.exe";
                //Process.Start(Settings.ServerDirectory + "/" + serverName);
            }

            Console.WriteLine("All done, goodbye!");
        }

        #region Functions
        static public void EndTask(string taskname)
        {
            string processName = taskname;
            string fixstring = taskname.Replace(".exe", "");

            if (taskname.Contains(".exe"))
            {
                foreach (Process process in Process.GetProcessesByName(fixstring))
                {
                    process.Kill();
                }
            }
            else if (!taskname.Contains(".exe"))
            {
                foreach (Process process in Process.GetProcessesByName(processName))
                {
                    process.Kill();
                }
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
            string folderPath = Path.Combine(Settings.GitDirectory, sm.GitPath);


            Console.WriteLine("Begining obfuscation");


            string[] files = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);

            foreach (string file in files) {

                if (file.EndsWith(".ogg") || file.EndsWith(".paa") || file.EndsWith(".jpg") || file.EndsWith(".png"))
                    continue;

                string outName = file;

                string contents = File.ReadAllText(file);

                if (sm.RenameGlobalVars) {
                    contents = Util.RenameVars(contents, _globalVars);
                }

                if (sm.RenameLocalVars) {
                    contents = Util.RenameVars(contents, _localVars);
                }

                if (sm.RenameFuncs) {
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
