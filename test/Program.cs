using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

public class IniFile
{
    private readonly Dictionary<string, Dictionary<string, string>> sections;

    public IniFile()
    {
        sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
    }

    public void Load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"INI file not found: {filePath}");
        }

        sections.Clear();

        string[] lines = File.ReadAllLines(filePath);

        string currentSection = null;
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
            {
                currentSection = trimmedLine.Substring(1, trimmedLine.Length - 2);
                sections[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                var keyValue = trimmedLine.Split(new[] { '=' }, 2);
                if (keyValue.Length == 2 && currentSection != null)
                {
                    sections[currentSection][keyValue[0].Trim()] = keyValue[1].Trim();
                }
            }
        }
    }

    public void Save(string filePath)
    {
        using (var writer = new StreamWriter(filePath))
        {
            foreach (var section in sections)
            {
                writer.WriteLine($"[{section.Key}]");

                foreach (var keyValue in section.Value)
                {
                    writer.WriteLine($"{keyValue.Key}={keyValue.Value}");
                }

                writer.WriteLine();
            }
        }
    }

    public string GetValue(string section, string key, string defaultValue = null)
    {
        if (sections.TryGetValue(section, out var values) && values.TryGetValue(key, out var value))
        {
            return value;
        }

        return defaultValue;
    }

    public void SetValue(string section, string key, string value)
    {
        if (!sections.TryGetValue(section, out var values))
        {
            values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            sections[section] = values;
        }

        values[key] = value;
    }
}

class Program
{
    static async Task Main()
    {
        List<RepositoryConfig> repositories = GetRepositoriesFromConfig();

        foreach (var repoConfig in repositories)
        {
            string owner = repoConfig.Owner;
            string repo = repoConfig.Repository;

            string currentVersion = GetCurrentVersion(repoConfig);

            if (currentVersion == null)
            {
                Console.WriteLine($"Failed to retrieve current version for {repoConfig.Repository}. Aborting update check.");
                continue;
            }

            string updateMethod = GetUpdateMethod(repoConfig);

            if (updateMethod == "Version")
            {
                string latestReleaseVersion = await GetLatestReleaseVersion(owner, repo);
                if (ShouldUpdate(currentVersion, latestReleaseVersion))
                {
                    Console.WriteLine($"Repository: {repoConfig.Repository}");
                    Console.WriteLine($"Current version: {currentVersion}");
                    Console.WriteLine($"Latest version available: {latestReleaseVersion}");

                    // Perform the download and update logic based on the latest release
                    await DownloadAndInstallUpdate(repoConfig, latestReleaseVersion);

                    // Update the current version and SHA in the INI file
                    UpdateCurrentVersion(repoConfig, latestReleaseVersion);
                }
                else
                {
                    Console.WriteLine($"Repository {repoConfig.Repository} is up to date.");
                }
            }
            else if (updateMethod == "CommitSHA")
            {
                string latestCommitSha = await GetLatestCommitSha(owner, repo);
                if (ShouldUpdate(currentVersion, latestCommitSha))
                {
                    Console.WriteLine($"Repository: {repoConfig.Repository}");
                    Console.WriteLine($"Current version: {currentVersion}");
                    Console.WriteLine($"Latest commit SHA: {latestCommitSha}");

                    // Perform the download and update logic based on the latest commit
                    await DownloadAndInstallUpdate(repoConfig, latestCommitSha);

                    // Update the current version and SHA in the INI file
                    UpdateCurrentVersion(repoConfig, latestCommitSha);
                }
                else
                {
                    Console.WriteLine($"Repository {repoConfig.Repository} is up to date.");
                }
            }
            else
            {
                Console.WriteLine($"Invalid update method specified for {repoConfig.Repository}. Skipping repository.");
            }
        }
    }

    static bool ShouldUpdate(string latest, string current)
    {
        // Compare versions to determine if an update is needed
        return latest != null &&
            current != null &&
            current != latest;
    }

    static async Task<string> GetLatestReleaseVersion(string owner, string repo)
    {
        using (HttpClient client = new HttpClient())
        {
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

            HttpResponseMessage response = await client.GetAsync($"https://api.github.com/repos/{owner}/{repo}/releases/latest");

            if (response.IsSuccessStatusCode)
            {
                string jsonResponse = await response.Content.ReadAsStringAsync();
                int versionIndex = jsonResponse.IndexOf("\"tag_name\":", StringComparison.OrdinalIgnoreCase);
                if (versionIndex != -1)
                {
                    int startIndex = jsonResponse.IndexOf('"', versionIndex) + 1;
                    int endIndex = jsonResponse.IndexOf('"', startIndex);
                    return jsonResponse.Substring(startIndex, endIndex - startIndex);
                }
            }

            Console.WriteLine($"Failed to fetch latest release for {repo}. Status code: {response.StatusCode}");
            return null;
        }
    }

    static async Task<string> GetLatestCommitSha(string owner, string repo)
    {
        using (HttpClient client = new HttpClient())
        {
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

            HttpResponseMessage response = await client.GetAsync($"https://api.github.com/repos/{owner}/{repo}/commits/main");

            if (response.IsSuccessStatusCode)
            {
                string jsonResponse = await response.Content.ReadAsStringAsync();
                int shaIndex = jsonResponse.IndexOf("\"sha\":", StringComparison.OrdinalIgnoreCase);
                if (shaIndex != -1)
                {
                    int startIndex = jsonResponse.IndexOf('"', shaIndex) + 1;
                    int endIndex = jsonResponse.IndexOf('"', startIndex);
                    return jsonResponse.Substring(startIndex, endIndex - startIndex);
                }
            }

            Console.WriteLine($"Failed to fetch latest commit for {repo}. Status code: {response.StatusCode}");
            return null;
        }
    }

    static string GetCurrentVersion(RepositoryConfig repoConfig)
    {
        try
        {
            string configFilePath = "path/to/versions.ini"; // Replace with the actual path to your INI file
            var parser = new FileIniDataParser();
            IniData data = parser.ReadFile(configFilePath);

            // Check if the repository section exists
            if (data.Sections.ContainsSection(repoConfig.Repository))
            {
                return data[repoConfig.Repository]["CurrentVersion"];
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to read versions file: {ex.Message}");
            return null;
        }
    }

    static void UpdateCurrentVersion(RepositoryConfig repoConfig, string latestVersion)
    {
        try
        {
            string configFilePath = "path/to/versions.ini"; // Replace with the actual path to your INI file
            var parser = new FileIniDataParser();
            IniData data = parser.ReadFile(configFilePath);

            // Check if the repository section exists, create if not
            if (!data.Sections.ContainsSection(repoConfig.Repository))
            {
                data.Sections.AddSection(repoConfig.Repository);
            }

            // Update the current version in the INI file
            data[repoConfig.Repository]["CurrentVersion"] = latestVersion;

            // Save the updated INI file
            parser.WriteFile(configFilePath, data);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to update versions file: {ex.Message}");
        }
    }

    static async Task DownloadAndInstallUpdate(RepositoryConfig repoConfig, string version)
    {
        try
        {
            string downloadUrl = $"https://github.com/{repoConfig.Owner}/{repoConfig.Repository}/releases/download/{version}/{repoConfig.Repository}.zip";
            string downloadPath = $"path/to/downloads/{repoConfig.Repository}-{version}.zip"; // Replace with the actual path to your download folder

            using (HttpClient client = new HttpClient())
            {
                Console.WriteLine($"Downloading {repoConfig.Repository} version {version}...");

                HttpResponseMessage response = await client.GetAsync(downloadUrl);
                if (response.IsSuccessStatusCode)
                {
                    // Save the downloaded ZIP file
                    await File.WriteAllBytesAsync(downloadPath, await response.Content.ReadAsByteArrayAsync());
                    Console.WriteLine($"Download complete.");

                    // Extract the downloaded ZIP file
                    ExtractZipFile(downloadPath); // Replace with the actual path to your installation folder

                    Console.WriteLine($"Installation complete.");

                    // Execute the Bash script
                    string installScriptPath = repoConfig.InstallScript;
                    if (!string.IsNullOrEmpty(installScriptPath) && File.Exists(installScriptPath))
                    {
                        Console.WriteLine($"Executing install script: {installScriptPath}");

                        // Use a process to execute the Bash script
                        using (System.Diagnostics.Process process = new System.Diagnostics.Process())
                        {
                            process.StartInfo.FileName = "bash";
                            process.StartInfo.Arguments = $"{installScriptPath} {repoConfig.Repository} {version}";
                            process.StartInfo.RedirectStandardOutput = true;
                            process.StartInfo.RedirectStandardError = true;
                            process.StartInfo.UseShellExecute = false;
                            process.StartInfo.CreateNoWindow = true;

                            process.Start();
                            process.WaitForExit();

                            string output = process.StandardOutput.ReadToEnd();
                            string error = process.StandardError.ReadToEnd();

                            Console.WriteLine($"Install script output:\n{output}");
                            Console.WriteLine($"Install script error:\n{error}");
                        }

                        Console.WriteLine($"Install script execution complete.");
                    }
                    else
                    {
                        Console.WriteLine($"Install script path not provided or does not exist.");
                    }
                }
                else
                {
                    Console.WriteLine($"Failed to download {repoConfig.Repository}. Status code: {response.StatusCode}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to download and install update for {repoConfig.Repository}: {ex.Message}");
        }
    }

    static string ExtractZipFile(string zipFilePath)
    {
        try
        {
            // Create a temporary directory for extraction
            string extractPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            // Ensure the target directory exists
            Directory.CreateDirectory(extractPath);

            // Extract the ZIP file
            ZipFile.ExtractToDirectory(zipFilePath, extractPath);

            return extractPath;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to extract ZIP file: {ex.Message}");
            return null;
        }
    }

    static List<RepositoryConfig> GetRepositoriesFromConfig()
    {
        try
        {
            string configFilePath = "path/to/repositories.ini"; // Replace with the actual path to your INI file
            var parser = new FileIniDataParser();
            IniData data = parser.ReadFile(configFilePath);

            List<RepositoryConfig> repositories = new List<RepositoryConfig>();

            foreach (var section in data.Sections)
            {
                string owner = data[section.SectionName]["Owner"];
                string repository = data[section.SectionName]["Repository"];

                repositories.Add(new RepositoryConfig
                {
                    Owner = owner,
                    Repository = repository
                });
            }

            return repositories;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to read repositories from config file: {ex.Message}");
            return new List<RepositoryConfig>();
        }
    }

    static string GetUpdateMethod(RepositoryConfig repoConfig)
    {
        try
        {
            string configFilePath = "path/to/repositories.ini"; // Replace with the actual path to your INI file
            var parser = new FileIniDataParser();
            IniData data = parser.ReadFile(configFilePath);

            // Check if the repository section exists
            if (data.Sections.ContainsSection(repoConfig.Repository))
            {
                return data[repoConfig.Repository]["UpdateMethod"];
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to read update method from config file: {ex.Message}");
            return null;
        }
    }

    class RepositoryConfig
    {
        public string Owner { get; set; }
        public string Repository { get; set; }
        public string CommitSha { get; set; }
        public string InstallScript { get; set; }
    }
}