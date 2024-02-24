namespace ChantsExtractor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Text.RegularExpressions;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TeamChants
    {
        public ushort teamId;
        public ushort fileId;
        public ushort afsId;
    }
    public class ExeFile
    {
        public string exeFilePath;
        public uint chantsOffset;
        public ushort totalTeams;

        public ExeFile(string exeFilePath, uint chantsOffset, ushort totalTeams)
        {
            this.exeFilePath = exeFilePath;
            this.chantsOffset = chantsOffset;
            this.totalTeams = totalTeams;
        }
    }
    public class KitserverFile
    {
        public string filePath;
        public ushort fileId;
        public KitserverFile(string filePath, ushort fileId)
        {
            this.filePath = filePath;
            this.fileId = fileId;
        }
    }
    public class Program
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct TOCEntry
        {
            public uint offset;
            public uint length;
        }

        public static List<TeamChants> ReadTeamChantsFromExe(ExeFile exeFile)
        {
            List<TeamChants> teamChantsList = new List<TeamChants>();
            using (BinaryReader reader = new BinaryReader(File.OpenRead(exeFile.exeFilePath)))
            {
                reader.BaseStream.Seek(exeFile.chantsOffset, SeekOrigin.Begin);

                for (int i = 0; i < exeFile.totalTeams; i++)
                {
                    ushort fileId = reader.ReadUInt16();
                    ushort afsId = reader.ReadUInt16();
                    teamChantsList.Add(new TeamChants { teamId = (ushort)i, fileId = fileId, afsId = afsId });
                }
            }

            return teamChantsList;
        }

        public static void ExtractAfsFile(TeamChants teamChant, string outputPath, string language)
        {
            string[] afsFilenames = { "0_sound.afs", "0_text.afs", $"{language}_sound.afs", $"{language}_text.afs" };
            string afsFilename = afsFilenames[teamChant.afsId];

            string datFolderPath = "./dat/";
            char[] afsMagicNumber = { 'A', 'F', 'S', '\0' }; ;
            int totalChants = 5;

            using (BinaryReader afsReader = new BinaryReader(File.OpenRead($"{datFolderPath}{afsFilename}")))
            {
                if (afsReader.ReadChars(4) != afsMagicNumber) new Exception("Not an AFS file");

                uint numFiles = afsReader.ReadUInt32();

                if (numFiles < teamChant.fileId) throw new Exception($"File ID {teamChant.fileId} out of range for AFS file {afsFilename}");

                afsReader.BaseStream.Seek(teamChant.fileId * 8 + 8, SeekOrigin.Begin);

                TOCEntry[] tocEntries = new TOCEntry[totalChants];
                for (int i = 0; i < totalChants; i++)
                {
                    tocEntries[i].offset = afsReader.ReadUInt32();
                    tocEntries[i].length = afsReader.ReadUInt32();
                }
                ushort counter = teamChant.fileId;
                foreach (TOCEntry tocEntry in tocEntries)
                {
                    if (tocEntry.offset != 0)
                    {
                        afsReader.BaseStream.Seek(tocEntry.offset, SeekOrigin.Begin);
                        byte[] fileData = afsReader.ReadBytes((int)tocEntry.length);
                        if (!Directory.Exists(outputPath + teamChant.teamId))
                        {
                            Directory.CreateDirectory(outputPath + teamChant.teamId);
                        }
                        File.WriteAllBytes($"{outputPath}/{teamChant.teamId}/chant_{counter}.adx", fileData);
                        counter++;
                    }
                    else
                    {
                        Console.WriteLine($"Couldn't find the fileId {teamChant.fileId} on AFS {afsFilename}");
                    }
                }
            }
        }
        public static List<KitserverFile>[] GetFilesFromKitserverAfs(string language) 
        {
            string BASE_PATH = "./kitserver/dat/";
            string[] afsFolders = { "0_sound.afs", "0_text.afs", $"{language}_sound.afs", $"{language}_text.afs" };
            List<KitserverFile>[] arrayDeListas = new List<KitserverFile>[afsFolders.Length];
            foreach (string folder in afsFolders)
            {
                try
                {
                    string[] filesInFolder = Directory.GetFiles($"{BASE_PATH}{folder}");
                    foreach (string file in filesInFolder) 
                    {
                        ushort fileNumber = GetNumberFromPath(file);
                        int folderIndex = Array.IndexOf(afsFolders, folder);
                        if (arrayDeListas[folderIndex] == null)
                            arrayDeListas[folderIndex] = new List<KitserverFile>();
                        arrayDeListas[folderIndex].Add(new KitserverFile(file, fileNumber));
                    }
                }
                catch (Exception ex)
                {

                    Console.WriteLine($"Error trying to create kitserver files array {ex.Message}");
                }
            }
            return arrayDeListas;

        }

        static ExeFile FindExecutable(List<ExeFile> exeFiles)
        {
            foreach (ExeFile exeFile in exeFiles)
            {
                if (File.Exists(exeFile.exeFilePath))
                {
                    return exeFile;
                }
            }

            return null;
        }
        static ushort GetNumberFromPath(string path)
        {
            Regex regex = new Regex(@"(\d+)(?=\.)");
            Match match = regex.Match(path);
            if (match.Success && ushort.TryParse(match.Value, out ushort number))
            {
                return number;
            }
            return 0xffff;
        }
        static KitserverFile GetKitserverFileFromList(List<KitserverFile> kitserverFiles, ushort fileID) 
        {
            if (kitserverFiles == null) return null;
            foreach (var kitserverFile in kitserverFiles)
            {
                if (kitserverFile.fileId  == fileID)
                {
                    return kitserverFile;
                }
            }
            return null;
        }
        static ushort GetNearestTeamId(List<TeamChants> teamChants, TeamChants teamChant)
        {
            foreach (TeamChants item in teamChants)
            {
                if (teamChant.fileId == item.fileId && item.afsId==teamChant.afsId)
                    return item.teamId;
            }
            return 0xffff;
        }
        public static void Main(string[] args)
        {
            string language = "";

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-l" || args[i] == "--language")
                {
                    if (i + 1 < args.Length)
                    {
                        language = args[i + 1];
                    }
                    else
                    {
                        Console.WriteLine("I was waiting a value after -l or --language, try again.");
                        return;
                    }
                }
            }

            if (string.IsNullOrEmpty(language))
            {
                Console.WriteLine(@"Usage: ChantsExtractor.exe.exe -l e

Options:
    -l, --language X        being X the language of the x_sound and x_text"
                );
                return;
            }

            List<ExeFile> exeFiles = new List<ExeFile> 
            {
                new ExeFile("pes5.exe", 0x6DF128, 221),
                new ExeFile("we9.exe", 0x6DF128, 221),
                new ExeFile("we9lek.exe", 0x6DCED8, 221),
                new ExeFile("pes6.exe", 0x7AEE20, 274),
            };

            ExeFile exeFile = FindExecutable(exeFiles);

            if (exeFile == null)
            {
                Console.WriteLine("Couldn't find any executable file.");
                return;
            }

            List<KitserverFile>[] kitserverAFS2FS = GetFilesFromKitserverAfs(language);
            List<TeamChants> teamChantsList = ReadTeamChantsFromExe(exeFile);

            string outputFolder = "./chants/";
            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }
            StreamWriter mapFile = new StreamWriter("./chants/map.txt");
            mapFile.WriteLine(@"# Made by PES5 Indie!
# This config maps team number into folder name
# Format: <team-num>,""<folder name>""
# Example: 21,""Russia""
# Note, if you put a # at the start of the line
# It means to be disable, just like any other kitserver module
# Always leave a blank line at the end"
            );
            List<ushort> proccesedFilesIds = new List<ushort>();
            foreach (TeamChants teamChant in teamChantsList)
            {
                bool writeLineOnMap = false;
                if (GetKitserverFileFromList(kitserverAFS2FS[teamChant.afsId], teamChant.fileId) != null && !proccesedFilesIds.Contains(teamChant.fileId))
                {
                    for (int i = 0; i < 5; i++)
                    {
                        KitserverFile ksFile = GetKitserverFileFromList(kitserverAFS2FS[teamChant.afsId], (ushort)(teamChant.fileId + i));
                        if (ksFile == null) continue;

                        string outputPath = $"{outputFolder}{teamChant.teamId}";
                        if (!Directory.Exists(outputPath))
                        {
                            Directory.CreateDirectory(outputPath);
                        }

                        string newFilePath = Path.Combine(outputPath, Path.GetFileName(ksFile.filePath));
                        try
                        {
                            File.Copy(ksFile.filePath, newFilePath, true);
                            writeLineOnMap = true;
                            proccesedFilesIds.Add(teamChant.fileId);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error when trying to copy file {ksFile.filePath} into {newFilePath}: {ex.Message}");
                        }
                    }
                }
                else if (!proccesedFilesIds.Contains(teamChant.fileId))
                {
                    try
                    {
                        ExtractAfsFile(teamChant, outputFolder, language);
                        writeLineOnMap = true;
                        proccesedFilesIds.Add(teamChant.fileId);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error trying to extract chants for teamId {teamChant.teamId} {ex.Message}");
                    }
                }
                if (writeLineOnMap || proccesedFilesIds.Contains(teamChant.fileId)) 
                {
                    ushort folderTeamId = GetNearestTeamId(teamChantsList, teamChant);
                    mapFile.WriteLine($"{teamChant.teamId},\"{folderTeamId}\"");
                }
            }
            mapFile.Close();
            Console.WriteLine("Extraction complete!");
        }
    }
}
