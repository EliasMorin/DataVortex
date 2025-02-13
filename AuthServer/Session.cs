using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System.Text;

namespace AuthServer
{
    public class Session
    {
        public string LicenceKey { get; private set; }
        public string LicenceType { get; private set; }
        public string ApiId { get; set; }
        public string ApiHash { get; set; }
        public string PhoneNumber { get; set; }
        public string VerificationCode { get; set; }
        public StreamWriter WTelegramLogs { get; set; } // Ajout d'un setter
        public string logDirectory; // Directory for the log file
        public int ConnectionCount { get; set; }
        public Dictionary<string, string> FirstHardwareId { get; set; } // Ajout d'un champ pour le premier ID_HARDWARE


        public Session(string licenceKey, string licenceType)
        {
            LicenceKey = licenceKey;
            LicenceType = licenceType;
            logDirectory = Path.Combine(Directory.GetCurrentDirectory(), licenceKey);
            Directory.CreateDirectory(logDirectory); // Create the directory if it does not exist
            Console.WriteLine(logDirectory);

            var jsonFilePath = Path.Combine(logDirectory, $"{licenceKey}_config.json");

        }

        public Dictionary<string, string> GetAccountsConfig()
        {
            var configFilePath = Path.Combine(logDirectory, $"{LicenceKey}_config.json");
            if (!File.Exists(configFilePath)) return null;

            var jsonContent = File.ReadAllText(configFilePath);
            var configData = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(jsonContent);

            if (configData.TryGetValue("ACCOUNTS_CONFIG", out var accountsConfig))
            {
                return accountsConfig;
            }

            return null;
        }

        public string Config(string what)
        {
            switch (what)
            {
                case "api_id": return ApiId;
                case "api_hash": return ApiHash;
                case "phone_number": return PhoneNumber;
                case "session_pathname": return Path.Combine(logDirectory, "WTelegram.session");
                default: return null;                  // let WTelegramClient decide the default config
            }
        }

        public void SavePasswords(Dictionary<string, List<string>> passwords)
        {
            var passwordsFilePath = Path.Combine(logDirectory, $"{LicenceKey}_passwords.json");

            Dictionary<string, List<string>> savedPasswords;
            if (File.Exists(passwordsFilePath))
            {
                var jsonContent = File.ReadAllText(passwordsFilePath);
                savedPasswords = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(jsonContent);
            }
            else
            {
                savedPasswords = new Dictionary<string, List<string>>();
            }

            foreach (var entry in passwords)
            {
                if (!savedPasswords.ContainsKey(entry.Key))
                {
                    savedPasswords[entry.Key] = new List<string>();
                }

                foreach (var credential in entry.Value)
                {
                    if (!savedPasswords[entry.Key].Contains(credential))
                    {
                        savedPasswords[entry.Key].Add(credential);
                        System.Console.WriteLine($"Ajouté {credential} à la catégorie {entry.Key} dans le fichier JSON");
                    }
                }
            }

            var updatedJsonContent = JsonConvert.SerializeObject(savedPasswords, Formatting.Indented);
            File.WriteAllText(passwordsFilePath, updatedJsonContent);
        }

        public void SaveProcessedArchives(Dictionary<string, List<string>> processedArchives)
        {
            string jsonFilePath = Path.Combine(logDirectory, $"{LicenceKey}_archives.json");
            string json = JsonConvert.SerializeObject(processedArchives, Formatting.Indented);
            File.WriteAllText(jsonFilePath, json);
        }

        public Dictionary<string, List<string>> LoadProcessedArchives()
        {
            string jsonFilePath = Path.Combine(logDirectory, $"{LicenceKey}_archives.json");
            if (File.Exists(jsonFilePath))
            {
                string json = File.ReadAllText(jsonFilePath);
                if (!string.IsNullOrEmpty(json))
                {
                    return JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(json);
                }
            }
            return new Dictionary<string, List<string>>();
        }

        public bool IsArchiveProcessed(string fileName, Dictionary<string, List<string>> processedArchives)
        {
            foreach (var category in processedArchives.Keys)
            {
                if (processedArchives[category].Contains(fileName))
                {
                    System.Console.WriteLine($"L'archive {fileName} a déjà été traitée dans la catégorie {category}.");
                    return true;
                }
            }
            return false;
        }

        public bool IsFirstConnection(string licenceKey)
        {
            // Vérifiez si le compteur de connexion pour cette clé de licence est nul
            // Vous pouvez stocker ces compteurs dans un fichier ou une base de données
            // Pour cet exemple, je vais supposer que vous avez une méthode GetConnectionCount qui renvoie le compteur de connexion pour une clé de licence donnée
            return GetConnectionCount(licenceKey) == 0;
        }

        public Dictionary<string, string> GetFirstHardwareId(string licenceKey)
        {
            var configFilePath = Path.Combine(logDirectory, $"{licenceKey}_config.json");
            if (!File.Exists(configFilePath)) return null;

            var jsonContent = File.ReadAllText(configFilePath);
            var configData = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(jsonContent);

            if (configData.TryGetValue("ID_HARDWARE", out var firstHardwareId))
            {
                return firstHardwareId;
            }

            return null;
        }

        public bool IsHardwareIdSame(Dictionary<string, string> currentHardwareId)
        {
            // Obtenez le premier ID_HARDWARE enregistré
            var firstHardwareId = GetFirstHardwareId(LicenceKey);

            // Vérifiez si FirstHardwareId est initialisé
            if (firstHardwareId == null)
            {
                Console.WriteLine("FirstHardwareId n'est pas initialisé");
                return true;  // Retournez true si c'est la première connexion
            }

            // Comparez le ID_HARDWARE actuel avec le premier ID_HARDWARE enregistré
            foreach (var key in firstHardwareId.Keys)
            {
                if (firstHardwareId[key] != currentHardwareId[key])
                {
                    return false;
                }
            }
            return true;
        }

        public int GetConnectionCount(string licenceKey)
        {
            // Construisez le chemin vers le fichier JSON qui stocke les compteurs de connexion
            string jsonFilePath = Path.Combine(logDirectory, $"{licenceKey}_connection_counts.json");

            // Vérifiez si le fichier existe
            if (!File.Exists(jsonFilePath))
            {
                // Si le fichier n'existe pas, cela signifie qu'il s'agit de la première connexion
                return 0;
            }

            // Lisez le fichier JSON
            var json = File.ReadAllText(jsonFilePath);
            var connectionCount = JsonConvert.DeserializeObject<int>(json);

            return connectionCount;
        }

        public void IncrementConnectionCount(string licenceKey)
        {
            // Construisez le chemin vers le fichier JSON qui stocke les compteurs de connexion
            string jsonFilePath = Path.Combine(logDirectory, $"{licenceKey}_connection_counts.json");

            // Lisez le compteur de connexion actuel
            int currentCount = GetConnectionCount(licenceKey);

            // Incrémentez le compteur de connexion
            currentCount++;

            // Écrivez le nouveau compteur de connexion dans le fichier
            File.WriteAllText(jsonFilePath, JsonConvert.SerializeObject(currentCount));
        }

        public static Dictionary<string, Session> InitializeSessions()
        {
            var sessions = new Dictionary<string, Session>();

            // Lisez le fichier licences.json
            var licencesJson = File.ReadAllText("licences.json");
            var licencesData = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(licencesJson);

            // Pour chaque clé de licence, créez une nouvelle session
            foreach (var licenceKey in licencesData["LICENCES"].Keys)
            {
                var licenceType = licencesData["LICENCES"][licenceKey];
                var session = new Session(licenceKey, licenceType);
                sessions.Add(licenceKey, session);
            }

            return sessions;
        }
    }
}