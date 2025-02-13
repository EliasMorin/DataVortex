using Ionic.Zip;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SharpCompress.Readers;

namespace AuthServer.DataVortex
{
    internal class Files
    {
        public static class ProceedFiles
        {
            public static void ExtractArchive(string filename, string password, string output_path)
            {
                if (ZipFile.IsZipFile(filename))
                {
                    using (var zipFile = new ZipFile(filename))
                    {
                        zipFile.Password = password;
                        foreach (var entry in zipFile.Entries)
                        {
                            if (!entry.IsDirectory && entry.FileName.EndsWith("Passwords.txt"))
                            {
                                var relativePath = entry.FileName;
                                var outputPath = Path.GetFullPath(Path.Combine(output_path, relativePath));
                                var outputDir = Path.GetDirectoryName(outputPath);

                                // Si outputDir est vide, utilisez un répertoire par défaut
                                if (string.IsNullOrEmpty(outputDir))
                                {
                                    outputDir = Path.Combine(output_path, "default");
                                }

                                Directory.CreateDirectory(outputDir);
                                entry.Extract(outputDir, ExtractExistingFileAction.OverwriteSilently);
                            }
                        }
                    }
                }
                else if (RarArchive.IsRarFile(filename))
                {
                    using (var rarFile = RarArchive.Open(filename, new ReaderOptions { Password = password }))
                    {
                        foreach (var entry in rarFile.Entries)
                        {
                            if (!entry.IsDirectory && entry.Key.EndsWith("Passwords.txt"))
                            {
                                var relativePath = entry.Key;
                                var outputPath = Path.GetFullPath(Path.Combine(output_path, relativePath));
                                var outputDir = Path.GetDirectoryName(outputPath);

                                // Si outputDir est vide, utilisez un répertoire par défaut
                                if (string.IsNullOrEmpty(outputDir))
                                {
                                    outputDir = Path.Combine(output_path, "default");
                                }

                                Directory.CreateDirectory(outputDir);
                                entry.WriteToFile(outputPath, new ExtractionOptions { ExtractFullPath = true, Overwrite = true });
                            }
                        }
                    }
                }
                else
                {
                    Colorful.Console.WriteLine("Error: unsupported archive format", System.Drawing.Color.Red);
                    Environment.Exit(1);
                }
            }

            public static bool ExtractProtectedArchive(string filename, string password, string output_path)
            {
                try
                {
                    if (ZipFile.IsZipFile(filename))
                    {
                        using (var zipFile = new ZipFile(filename))
                        {
                            zipFile.Password = password;
                            foreach (var entry in zipFile.Entries)
                            {
                                if (!entry.IsDirectory)
                                {
                                    var relativePath = entry.FileName;
                                    var outputPath = Path.GetFullPath(Path.Combine(output_path, relativePath));
                                    var outputDir = Path.GetDirectoryName(outputPath);

                                    // Normaliser le chemin pour éviter les duplications
                                    outputPath = Path.GetFullPath(outputPath);
                                    outputDir = Path.GetDirectoryName(outputPath);

                                    // Debug messages
                                    Console.WriteLine($"Extracting: {entry.FileName}");
                                    Console.WriteLine($"Relative Path: {relativePath}");
                                    Console.WriteLine($"Output Path: {outputPath}");
                                    Console.WriteLine($"Output Directory: {outputDir}");

                                    // Créer le répertoire de sortie si nécessaire
                                    if (!Directory.Exists(outputDir))
                                    {
                                        Directory.CreateDirectory(outputDir);
                                    }

                                    entry.Extract(outputDir, ExtractExistingFileAction.OverwriteSilently);
                                }
                            }
                        }
                    }
                    else if (RarArchive.IsRarFile(filename))
                    {
                        using (var rarFile = RarArchive.Open(filename, new ReaderOptions { Password = password }))
                        {
                            foreach (var entry in rarFile.Entries)
                            {
                                if (!entry.IsDirectory)
                                {
                                    var relativePath = entry.Key;
                                    var outputPath = Path.GetFullPath(Path.Combine(output_path, relativePath));
                                    var outputDir = Path.GetDirectoryName(outputPath);

                                    // Normaliser le chemin pour éviter les duplications
                                    outputPath = Path.GetFullPath(outputPath);
                                    outputDir = Path.GetDirectoryName(outputPath);

                                    // Créer le répertoire de sortie si nécessaire
                                    if (!Directory.Exists(outputDir))
                                    {
                                        Directory.CreateDirectory(outputDir);
                                    }

                                    entry.WriteToFile(outputPath, new ExtractionOptions { ExtractFullPath = true, Overwrite = true });
                                }
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("Error: unsupported archive format");
                        return false;
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erreur lors de l'extraction de l'archive {filename} : {ex.Message}");
                    return false;
                }
            }

            public static bool IsPasswordProtected(string filename)
            {
                try
                {
                    using (var fileStream = File.OpenRead(filename))
                    {
                        if (filename.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            byte[] buffer = new byte[4];
                            fileStream.Read(buffer, 0, 4);
                            return BitConverter.ToUInt32(buffer, 0) == 0x04034b50; // ZIP file signature
                        }
                        else if (filename.EndsWith(".rar", StringComparison.OrdinalIgnoreCase))
                        {
                            byte[] signature = new byte[2];
                            fileStream.Read(signature, 0, signature.Length);
                            return signature[0] == 0x52 && signature[1] == 0x61; // RAR signature "Rar!"
                        }
                    }
                }
                catch
                {
                    // Ignorer les erreurs et considérer l'archive comme non protégée
                }
                return false;
            }

            public static Dictionary<string, List<(string url, string username, string password, string app)>> FindPasswords(Dictionary<string, string> accountsConfig)
            {
                var results = new Dictionary<string, List<(string url, string username, string password, string app)>>();

                var directoryPath = "dbdtemp";
                var files = Directory.GetFiles(directoryPath, "Passwords.txt", SearchOption.AllDirectories);

                foreach (var file in files)
                {
                    var contents = File.ReadAllText(file);
                    var matches = Regex.Matches(contents, @"URL:.*\nUsername:.*\nPassword:.*\nApplication:.*\n=*");

                    foreach (Match match in matches)
                    {
                        var urlMatch = Regex.Match(match.Value, @"URL:\s*(.*)\n");
                        if (urlMatch.Success)
                        {
                            var url = urlMatch.Groups[1].Value.Trim();
                            Console.WriteLine($"Extracted URL: {url}");

                            foreach (var keyword in accountsConfig.Keys)
                            {
                                Console.WriteLine($"Checking keyword: {keyword} against URL: {url}");
                                if (url.Contains(keyword))
                                {
                                    var usernameMatch = Regex.Match(match.Value, @"Username:\s*(.*)\n");
                                    var passwordMatch = Regex.Match(match.Value, @"Password:\s*(.*)\n");
                                    var appMatch = Regex.Match(match.Value, @"Application:\s*(.*)\n");

                                    if (usernameMatch.Success && passwordMatch.Success && appMatch.Success)
                                    {
                                        var username = usernameMatch.Groups[1].Value.Trim();
                                        var password = passwordMatch.Groups[1].Value.Trim();
                                        var app = appMatch.Groups[1].Value.Trim();

                                        var result = (url, username, password, app);

                                        if (!results.ContainsKey(keyword))
                                        {
                                            results[keyword] = new List<(string url, string username, string password, string app)>();
                                            Console.WriteLine($"New category created: {keyword}");
                                        }
                                        results[keyword].Add(result);
                                        Console.WriteLine($"Added {username}:{password} to category {keyword}");
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"Keyword {keyword} not found in URL: {url}");
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine($"URL match failed for: {match.Value}");
                        }
                    }
                }
                return results;
            }
        }
    }
}
