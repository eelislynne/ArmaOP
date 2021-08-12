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

            newshit = newshit.Replace("\t", "");
            string[] lines = newshit.Split(
                new[] { "\r\n", "\r", "\n" },
                StringSplitOptions.None
            );

            StringBuilder built = new StringBuilder();

            bool did = false;
            foreach (string line in lines)
            {
                
                if (Regex.IsMatch(line, @"^\s*#"))
                {
                    built.AppendLine();
                    built.AppendLine(line);
                    did = true;
                    continue;
                }

                built.Append(line);
                if (!did)
                    built.Append(" ");
                did = false;
            }


            return built.ToString();
        }

        public static bool Move(ServerMod sm) {
            string gitName = Path.GetFileNameWithoutExtension(sm.GitUrl);
            string folderPath = Path.Combine(Program.Settings.GitDirectory, gitName);
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

        public static bool MoveFile(ServerMod sm)
        {
            string modPath = Path.Combine(Program.Settings.GitDirectory, sm.Name);

            try
            {
                File.Copy(modPath, sm.ServerPath, true);

                Console.WriteLine($"Moved ({sm.Name}): {modPath} => {sm.ServerPath}");

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error moving files");
                Util.Assert(ex);
                return false;
            }
        }

        public static void Pack(ServerMod sm) {
            string gitName = Path.GetFileNameWithoutExtension(sm.GitUrl);

            string gitPath = Path.Combine(Program.Settings.GitDirectory, gitName);
            string folderPath = Path.Combine(gitPath, sm.Name);
            string modPath = Path.Combine(Program.Settings.GitDirectory, sm.Name);

            Console.WriteLine($"{folderPath} : {modPath}");



            if (sm.PackingMethod == PackingMethod.ObfuSQF) {
                Console.WriteLine($"Running ObfuSQF for {sm.Name}\n");

                string obfuPath = Path.Combine(Program.Settings.ObfuSQFDirectory, "ObfuSQF_CMD.exe");
                string type = sm.ObfuSQFMission ? "Mission" : "Mod";

                ProcessStartInfo obfu = new ProcessStartInfo(obfuPath)
                {
                    Arguments = $"--token {Program.Settings.ObfuSQFToken} --input \"{folderPath}\" --type {type} --output \"{modPath}.pbo\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                ObfuProcs.Add(Process.Start(obfu));
            } else if (sm.PackingMethod == PackingMethod.Normal) {
                Console.WriteLine($"Packing {sm.Name}\n");

                PboFile pbo = new PboFile();
                foreach (string s in Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories)) {
                    string filename = Path.GetFileName(s);
                    if (filename == "mission.sqm")
                    {
                        string Rapify = Path.Combine(Program.Settings.ToolsDirectory, "CfgConvert" , "CfgConvert.exe");                      

                        if (File.Exists(Rapify) /*&& false*/)
                        {
                            Console.WriteLine("Binarising SQM");
                            Process.Start(new ProcessStartInfo
                            {
                                WorkingDirectory = folderPath,
                                FileName = Rapify,
                                Arguments = $"-bin {s}",
                                UseShellExecute = false,
                                CreateNoWindow = true

                            }).WaitForExit();
                        }
                    }

                    string path = s.Replace(folderPath, "");

                    if (path.StartsWith(@"\"))
                        path = path.Substring(1);

                    //string file = File.ReadAllText(s);

                    //pbo.AddEntry(path, Encoding.UTF8.GetBytes(file));

                    byte[] file = File.ReadAllBytes(s);

                    pbo.AddEntry(path, file);
                }

                pbo.Save($"{modPath}.pbo");
            } else if (sm.PackingMethod == PackingMethod.SqfSafe)
            {
                Console.WriteLine($"Running SqfSafe for {sm.Name}\n");

                string obfuPath = Path.Combine(Program.Settings.SqfSafeDirectory, "SqfSafe-CLI.exe");

                ProcessStartInfo obfu = new ProcessStartInfo(obfuPath)
                {
                    Arguments = $"--token {Program.Settings.SqfSafeToken} -f \"{folderPath}\" -p \"{sm.SqfSafeProfile}\" -o \"{modPath}.pbo\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                ObfuProcs.Add(Process.Start(obfu));
            } else
            {
                Console.WriteLine($"Packing Method not Supported.");
                Util.Assert(false);
            }
        }
    }
}
