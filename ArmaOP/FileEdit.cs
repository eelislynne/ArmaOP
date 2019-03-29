using ArmaOP.Arma3;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ArmaOP {
    class FileEdit {

        public static List<Process> ObfuProcs = new List<Process>();

        public static string RemoveComments(string source) {
            var blockComments = @"/\*(.*?)\*/";
            var lineComments = @"//(.*?)\r?\n";
            var strings = @"""((\\[^\n]|[^""\n])*)""";
            var verbatimStrings = @"@(""[^""]*"")+";

            string noComments = Regex.Replace(source,
            blockComments + "|" + lineComments + "|" + strings + "|" + verbatimStrings,
            me => {
                if (me.Value.StartsWith("/*") || me.Value.StartsWith("//"))
                    return me.Value.StartsWith("//") ? Environment.NewLine : "";
                // Keep the literal strings
                return me.Value;
            },
            RegexOptions.Singleline);

            return noComments;
        }

        public static string OneLine(string contents) {
            string newshit = RemoveComments(contents);
            newshit = newshit.Replace("\n", " ");
            newshit = newshit.Replace("\r", "");

            return newshit;
        }

        public static bool Move(ServerMod sm) {
            string folderPath = Path.Combine(Program.Settings.GitDirectory, sm.GitPath);
            string modPath = Path.Combine(Program.Settings.GitDirectory, sm.Name) + ".pbo";

            try {
                File.Copy(modPath, sm.ServerPath, true);

                Console.WriteLine($"Moved ({sm.Name}): {modPath} => {sm.ServerPath}");

                return true;
            } catch (Exception ex) {
                Console.WriteLine("Error moving files");
                Util.Assert(ex);
                return false;
            }
        }

        public static void Pack(ServerMod sm) {

            string folderPath = Path.Combine(Program.Settings.GitDirectory, sm.GitPath);
            string modPath = Path.Combine(Program.Settings.GitDirectory, sm.Name);

            if (sm.UseObfuSQF) {
                Console.WriteLine($"Running ObfuSQF for {sm.Name}\n");

                string obfuPath = Path.Combine(Program.Settings.ObfuSQFDirectory, "ObfuSQF_CMD.exe");
                string type = sm.ObfuSQFMission ? "Mission" : "Mod";

                ProcessStartInfo obfu = new ProcessStartInfo(obfuPath);
                obfu.Arguments = $"--token {Program.Settings.ObfuSQFToken} --input \"{folderPath}\" --type {type} --output \"{modPath}.pbo\"";
                obfu.UseShellExecute = false;
                obfu.CreateNoWindow = true;

                ObfuProcs.Add(Process.Start(obfu));
            } else {
                Console.WriteLine($"Packing {sm.Name}\n");

                PboFile pbo = new PboFile();

                foreach (string s in Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories)) {
                    string path = s.Replace(folderPath, "");

                    if (path.StartsWith(@"\"))
                        path = path.Substring(1);

                    string file = File.ReadAllText(s);

                    pbo.AddEntry(path, Encoding.UTF8.GetBytes(file));
                }

                pbo.Save($"{modPath}.pbo");
            }
        }
    }
}
