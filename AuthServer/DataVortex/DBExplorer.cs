using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Ionic.Zip;
using SharpCompress.Archives.Rar;
using Colorful;
using SharpCompress.Archives;
using SharpCompress.Common;
using System.Collections.Specialized;
using System.Drawing;
using System.Net;
using System.Text;
using Discord.Webhook;
using Discord;
using static System.Net.WebRequestMethods;
using static System.Collections.Specialized.BitVector32;
using Newtonsoft.Json;

namespace AuthServer.DataVortex
{
    public static class DBExplorer
    {
        public static void Run(Session session)
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            PrintBanner();

            string folderPath = GlobalPaths.DatabasesDirectory;
            string[] archives = Directory.GetFiles(folderPath, "*.rar")
                                           .Concat(Directory.GetFiles(folderPath, "*.zip"))
                                           .ToArray();

            if (archives.Length == 0)
            {
                System.Console.WriteLine("Aucune archive détectée dans le dossier spécifié.");
                return;
            }

            // Lire la liste des archives déjà traitées
            Dictionary<string, List<string>> processedArchives = session.LoadProcessedArchives();

            var keywords = session.GetAccountsConfig();
            if (keywords == null || keywords.Count == 0)
            {
                System.Console.WriteLine("Aucun mot clé trouvé dans ACCOUNTS_CONFIG.");
                return;
            }

            // Charger les mots de passe depuis le fichier JSON
            var passwords = LoadPasswords("telegram_passwords.json");

            foreach (string archivePath in archives)
            {
                string fileName = Path.GetFileName(archivePath);

                // Vérifiez si l'archive a déjà été traitée
                if (session.IsArchiveProcessed(fileName, processedArchives))
                {
                    continue; // Si l'archive a déjà été traitée, passez à la suivante
                }

                System.Console.WriteLine($"Archive détectée : {Path.GetFileName(archivePath)}");

                bool isProtected = Files.ProceedFiles.IsPasswordProtected(archivePath);
                System.Console.WriteLine($"L'archive {fileName} est protégée par mot de passe : {isProtected}");

                bool extracted = false;
                try
                {
                    if (isProtected)
                    {
                        System.Console.WriteLine($"Nombre de mots de passe : {passwords.Count}");
                        foreach (var password in passwords)
                        {
                            if (Files.ProceedFiles.ExtractProtectedArchive(archivePath, password, "dbdtemp"))
                            {
                                extracted = true;
                                break;
                            }
                        }
                    }
                    else
                    {
                        Files.ProceedFiles.ExtractArchive(archivePath, null, "dbdtemp");
                        extracted = true;
                    }

                    if (extracted)
                    {
                        // Appelez FindPasswords() une fois ici
                        var results = Files.ProceedFiles.FindPasswords(keywords);

                        System.Console.WriteLine("Résultats de FindPasswords:");
                        foreach (var key in results.Keys)
                        {
                            System.Console.WriteLine($"Clé trouvée: {key}, Nombre de résultats: {results[key].Count}");
                        }

                        var passwordsToSave = new Dictionary<string, List<string>>();

                        // Utilisez les mots clés de ACCOUNTS_CONFIG
                        foreach (var keyword in keywords.Keys)
                        {
                            if (results.ContainsKey(keyword) && results[keyword].Count > 0)
                            {
                                // Passer webhookUrl et fileName à HandleAccount
                                foreach (var result in results[keyword])
                                {
                                    DataVortex.Checker.HandleAccount(keyword, result, keywords[keyword], fileName);
                                }

                                if (!passwordsToSave.ContainsKey(keyword))
                                {
                                    passwordsToSave[keyword] = new List<string>();
                                    System.Console.WriteLine($"Nouvelle catégorie créée: {keyword}");
                                }

                                foreach (var result in results[keyword])
                                {
                                    var credential = $"{result.username}:{result.password}";
                                    passwordsToSave[keyword].Add(credential);
                                    System.Console.WriteLine($"Ajouté {credential} à la catégorie {keyword}");
                                }
                            }
                        }

                        // Sauvegarder les mots de passe trouvés
                        session.SavePasswords(passwordsToSave);

                        // Après avoir traité l'archive, ajoutez-la à la liste des archives traitées
                        if (!processedArchives.ContainsKey("Databases"))
                        {
                            processedArchives["Databases"] = new List<string>();
                        }
                        processedArchives["Databases"].Add(fileName);

                        // Sauvegarder les archives traitées
                        session.SaveProcessedArchives(processedArchives);

                        // Supprimez l'archive après l'extraction
                        System.IO.File.Delete(archivePath);
                    }
                    else
                    {
                        System.Console.WriteLine($"Impossible d'extraire l'archive {fileName} avec les mots de passe fournis.");
                    }
                }
                finally
                {
                    // Supprimez les fichiers extraits dans dbdtemp
                    CleanUpExtractedFiles("dbdtemp");

                    // Supprimez l'archive après l'extraction ou en cas d'échec
                    System.IO.File.Delete(archivePath);
                }
            }
        }

        private static void CleanUpExtractedFiles(string directoryPath)
        {
            try
            {
                if (Directory.Exists(directoryPath))
                {
                    Directory.Delete(directoryPath, true);
                    System.Console.WriteLine($"Le répertoire {directoryPath} a été supprimé.");
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Erreur lors de la suppression du répertoire {directoryPath} : {ex.Message}");
            }
        }

        private static void PrintBanner()
        {
            Colorful.Console.WriteLine(Figgle.FiggleFonts.Standard.Render("DBExplorer"));
        }

        public static List<string> LoadPasswords(string filePath)
        {
            if (!System.IO.File.Exists(filePath))
            {
                System.Console.WriteLine($"Le fichier {filePath} n'existe pas.");
                return new List<string>();
            }

            var json = System.IO.File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<List<string>>(json);
        }
    }
}