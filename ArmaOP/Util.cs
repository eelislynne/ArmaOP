using LibGit2Sharp;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;



namespace ArmaOP {
    class Util {
        private static Random random = new Random();
        private static List<string> pulledGits = new List<string>();

        public static string RandomString(int length) {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstvwxyz";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        public static void Assert(bool val, string msg = "") {
            if (!val) {
                Console.WriteLine("Fatal error occured!");
                if (msg != "")
                    Console.WriteLine("Error: " + msg);

                if (Program.Settings.StartARAIfFail) {
                    StartAra();
                }
                Console.WriteLine("Press enter to exit...");
                Console.ReadLine();
                Environment.Exit(1);
            }
        }

        public static void Assert(Exception ex) {
            if (ex != null) {
                Console.WriteLine("Fatal error occured!");
                Console.WriteLine("Error: " + ex);

                if (Program.Settings.StartARAIfFail) {
                    StartAra();
                }
                Console.WriteLine("Press enter to exit...");
                Console.ReadLine();
                Environment.Exit(1);
            }
        }

        public static void StartAra() {
            Console.WriteLine("Starting ArmaRemoteAdmin...");
            Process.Start(Program.Settings.MaverickDirectory + "/Maverick-ArmARemote.exe");
        }

        public static bool GitDownload(ServerMod sm) {

            if (pulledGits.Contains(sm.GitUrl.ToLower())) {
                return true;
            }

            pulledGits.Add(sm.GitUrl.ToLower());

            Console.WriteLine("");
            Console.WriteLine("Pulling from git...");

            try {

                string token = sm.GitToken.Replace("token ", "");

                string gitName = Path.GetFileNameWithoutExtension(sm.GitUrl);
                string gitPath = Path.Combine(Program.Settings.GitDirectory, gitName);
                
                if (!Directory.Exists(Program.Settings.GitDirectory)) {
                    Directory.CreateDirectory(Program.Settings.GitDirectory);
                }

                // This options allows you to use the SSH option. (Allows for Repo specific auth)
                if (sm.GitType == 3)
                {
                    if (Directory.Exists(gitPath))
                    {
                        return true;
                    }

                    string branch = "";

                    if (sm.GitPath != "")
                    {
                        branch = $" -b {sm.GitPath}";
                    }

                    Process.Start(new ProcessStartInfo
                    {
                        WorkingDirectory = Program.Settings.GitDirectory,
                        FileName = "git",
                        Arguments = $"clone --depth=1{branch} {sm.GitUrl}",
                        UseShellExecute = false,
                        CreateNoWindow = true

                    }).WaitForExit();

                    Program.DeleteDirectory(Path.Combine(Program.Settings.GitDirectory, gitName, ".git"));

                    return true;
                }


                if (Directory.Exists(Path.Combine(Program.Settings.GitDirectory, sm.GitPath))) {
                    Directory.Delete(Program.Settings.GitDirectory, true);
                }
                Directory.CreateDirectory(Program.Settings.GitDirectory);

                if (sm.GitType == 2) {


                    CloneOptions co = new CloneOptions
                    {
                        CredentialsProvider = (_url, _user, _cred) =>
                             new UsernamePasswordCredentials
                             {
                                 Username = "User",
                                 Password = sm.GitToken
                             }
                    };
                    if (sm.GitPath != "")
                    {
                        co.BranchName = sm.GitPath;
                    }
                    Repository.Clone(sm.GitUrl, gitPath, co);

                } else {
                    CloneOptions co = new CloneOptions
                    {
                        CredentialsProvider = (_url, _user, _cred) =>
                             new UsernamePasswordCredentials
                             {
                                 Username = sm.GitToken,
                                 Password = string.Empty
                             }
                    };
                    if (sm.GitPath != "")
                    {
                        co.BranchName = sm.GitPath;
                    }
                    Repository.Clone(sm.GitUrl, gitPath, co);
                }

                return true;
            } catch (Exception ex) {
                Console.WriteLine("Error in download: " + ex);
                return false;
            }
        }

        
        public static string RenameVars(string contents, Dictionary<string, string> vars) {

            StringBuilder sb = new StringBuilder(contents);

            foreach (KeyValuePair<string, string> kv in vars) {

                string pattern = string.Format(@"\b{0}\b", Regex.Escape(kv.Key));
                contents = Regex.Replace(contents, pattern, kv.Value, RegexOptions.Multiline | RegexOptions.IgnoreCase);

            }
            return contents;
        }
    }
}
