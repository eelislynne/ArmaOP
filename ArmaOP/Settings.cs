using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ArmaOP {

    class ServerMod {
        public string Name;
        public string GitPath;
        public string GitUrl;
        public string GitToken;
        public int GitType; // 1 = GitHub, 2 = GitLab, implement ur own if u need
        public string ServerPath;
        public bool UseObfuSQF;
        public bool ObfuSQFMission;
        public bool OneLine;
        public bool RenameFuncs;
        public bool RenameGlobalVars;
        public bool RenameLocalVars;
    }

    class Settings {

        public string ServerDirectory;
        public string GitDirectory;
        public string MaverickDirectory;
        public string ObfuSQFDirectory;
        public string FunctionsTag;
        public int RandomFuncsLength;
        public int RandomVarsLength;
        public string ObfuSQFToken;
        public bool UseArmaRemoteAdmin;
        public bool StartARAIfFail;
        public bool KillArmaServer;
        public bool Use64BitServer;
        public List<string> ObfLocalVars;
        public List<string> ObfGlobalVars;
        public List<string> ObfFunctions;
        public List<ServerMod> Mods;

    }

    class SettingsManager {
        private static string _configPath = "settings.json";

        private static Settings WriteDefaults() {
            Settings s = new Settings();
            s.ServerDirectory = "C:\\Arma3";
            s.GitDirectory = "C:\\Github";
            s.MaverickDirectory = "C:\\ArmaRemoteAdmin";
            s.ObfuSQFDirectory = "C:\\ObfuSQFCMD";
            s.FunctionsTag = "life";
            s.RandomFuncsLength = 8;
            s.RandomVarsLength = 8;
            s.ObfuSQFToken = "0000-0000-0000-0000";
            s.UseArmaRemoteAdmin = false;
            s.StartARAIfFail = false;
            s.KillArmaServer = false;
            s.Use64BitServer = true;
            s.ObfLocalVars = new List<string>();
            s.ObfGlobalVars = new List<string>();
            s.ObfFunctions = new List<string>();


            List<ServerMod> mods = new List<ServerMod>();
            mods.Add(new ServerMod {
                Name = "Mission.Altis",
                GitPath = "repo-master",
                GitUrl = "https://github.com/user/repo/archive/master.zip",
                GitToken = "xxxxx",
                GitType = 1,
                ServerPath = "C:\\Arma3\\@life_server\\addons\\server.pbo",
                UseObfuSQF = false,
                ObfuSQFMission = true,
                OneLine = false,
                RenameFuncs = false,
                RenameGlobalVars = false,
                RenameLocalVars = false
            });

            s.Mods = mods;

            WriteConfig(s);

            return s;
        }

        public static void WriteConfig(Settings s) {
            try {
                string json = JsonConvert.SerializeObject(s, Formatting.Indented);
                File.WriteAllText(_configPath, json);
            } catch (Exception ex) {
                Console.WriteLine("Error while writing config. Invalid permissions?");
                Util.Assert(ex);
            }
        }

        public static Settings ReadConfig() {
            if (!File.Exists(_configPath)) {
                Console.WriteLine("Config file not found, writing a new one!");
                return WriteDefaults();
            }

            try {
                return JsonConvert.DeserializeObject<Settings>(File.ReadAllText(_configPath));
            } catch(Exception ex) {
                if (Program.Settings.StartARAIfFail) {
                    Util.StartAra();
                }

                Console.WriteLine("Oops! An error occured while reading the config file: " + ex);
                Console.WriteLine("Would you like to rewrite the default config? Y/N");
                string res = Console.ReadLine();
                if (res.ToLower() == "y")
                    WriteDefaults();

                Environment.Exit(0);
                return null;
            }
        }



    }
}
