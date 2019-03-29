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

                string gitPath = Path.Combine(Program.Settings.GitDirectory + "/git.zip");
                
                if (!Directory.Exists(Program.Settings.GitDirectory)) {
                    Directory.CreateDirectory(Program.Settings.GitDirectory);
                }
                

                using (WebClient wc = new WebClient()) {
                    if (sm.GitType == 1) {
                        wc.Headers.Add("Authorization", "token " + token);
                        Console.WriteLine($"{sm.Name} using GitHub");
                    } else if (sm.GitType == 2) {
                        wc.Headers.Add("Private-Token", token);
                        Console.WriteLine($"{sm.Name} using GitLab");
                    } else {
                        throw new Exception("Unknown git type! 1 = GitHub, 2 = GitLab");
                    }
                    wc.DownloadFile(sm.GitUrl, gitPath);
                }

                if (Directory.Exists(Path.Combine(Program.Settings.GitDirectory, sm.GitPath))) {
                    Directory.Delete(Program.Settings.GitDirectory, true);
                }
                Directory.CreateDirectory(Program.Settings.GitDirectory);

                // very dirty hack for gitlab stupid file bullshit
                if (sm.GitType == 2) {

                    using (ZipArchive archive = ZipFile.OpenRead(gitPath)) {

                        string folderName = archive.Entries[0].FullName;
                        
                        foreach (ZipArchiveEntry entry in archive.Entries.Skip(1)) {

                            string name = Program.Settings.GitDirectory + "/" + entry.FullName.Replace(folderName, "");
                            if (entry.FullName.EndsWith("/")) {
                                Directory.CreateDirectory(name);
                            } else {
                                entry.ExtractToFile(name);
                            }

                        }
                    }

                } else {
                    ZipFile.ExtractToDirectory(gitPath, Program.Settings.GitDirectory); // github :)
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
