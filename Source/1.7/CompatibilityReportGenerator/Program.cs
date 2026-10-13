using System.CommandLine;
using System.Text.Json;
using CompatibilityReportGenerator.Properties;

namespace CompatibilityReportGenerator;

public static class Program
{
    public static void Main(string[] args)
    {
        var rootCmd = new RootCommand("Generates a compatibility report (in markdown format) for tweak data.");

        var dirOption = new Option<DirectoryInfo>("--directory")
        {
            Description = "The directory that the tweak .json files are located in.",
            IsRequired = true,
            AllowMultipleArgumentsPerToken = false,
            Arity = ArgumentArity.ExactlyOne
        };

        var outputOption = new Option<FileInfo>("--output")
        {
            Description = "The file path to output the markdown file to.",
            IsRequired = true,
            AllowMultipleArgumentsPerToken = false,
            Arity = ArgumentArity.ExactlyOne
        };

        rootCmd.AddOption(dirOption);
        rootCmd.AddOption(outputOption);

        rootCmd.SetHandler(Run, dirOption, outputOption);

        rootCmd.Invoke(args);
    }

    private static TweakDataModel LoadTweakData(string filePath)
    {
        string json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<TweakDataModel>(json);
    }

    private static void Run(DirectoryInfo input, FileInfo output)
    {
        if (!input.Exists)
            throw new DirectoryNotFoundException(input.FullName);

        Dictionary<string, OutputRow> table = new Dictionary<string, OutputRow>(512);
        int totalWeaponCount = 0;

        var idToName = (from raw in Directory.EnumerateFiles(input.FullName, "*.txt", SearchOption.AllDirectories)
                        let name = new FileInfo(raw).Name.Replace(".txt", "")
                        let contents = File.ReadAllText(raw)
                        select (name, contents)).ToDictionary(pair => pair.name, pair => pair.contents);

        string[] files = Directory.GetFiles(input.FullName, "*.json", SearchOption.AllDirectories);
        foreach (string jsonFile in files)
        {
            TweakDataModel tweak;
            try
            {
                tweak = LoadTweakData(jsonFile);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception reading/parsing file '{jsonFile}':");
                Console.WriteLine(e);
                continue;
            }

            string Escape(string i)
            {
                return i.Replace("|", "&#124;");
            }

            string modID = tweak.TextureModID;
            if (!table.TryGetValue(modID, out var row))
            {
                if (!idToName.TryGetValue(modID, out string name))
                {
                    Console.WriteLine($"Failed to find ID -> name mapping for {modID}");
                    name = "???";
                }
                row = new OutputRow
                {
                    WeaponCount = 0,
                    ModName = Escape(name),
                    ModID = Escape(modID)
                };
                table.Add(modID, row);
            }

            row.WeaponCount++;
            totalWeaponCount++;
        }

        string template = Resources.Template;

        string time = DateTime.UtcNow.ToString("d MMM yyyy, h:mm tt");
        int modCount = table.Count;
        var lines = from row in table.Values
                    orderby row.ModName
                    select $"| **{row.ModName}** | {row.ModID} | {row.WeaponCount}";

        string finalTxt = string.Format(template, totalWeaponCount, modCount, string.Join("\n", lines), time);

        File.WriteAllText(output.FullName, finalTxt);
    }

    private class OutputRow
    {
        public string ModName { get; set; }
        public string ModID { get; set; }
        public int WeaponCount { get; set; }
    }
}