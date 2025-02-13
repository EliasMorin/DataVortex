using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TL;
using WTelegram;

namespace AuthServer.DataVortex
{
    public static class GlobalPaths
    {
        public static string DatabasesDirectory { get; set; }
    }

    public class Telegram
    {
        public static async Task TelegramDownload(Session session, Client client)
        {
            try
            {
                var dialogs = await client.Messages_GetAllDialogs();
                var channelMap = new Dictionary<int, ChatBase>(); // Map pour stocker les canaux par numéro

                Console.WriteLine("Canaux auxquels le compte est abonné :");
                int channelNumber = 1;

                foreach (Dialog dialog in dialogs.dialogs)
                {
                    var chat = dialogs.UserOrChat(dialog) as ChatBase;
                    if (chat is Channel channel && chat.IsActive)
                    {
                        Console.WriteLine($"{channelNumber}. {chat}");
                        channelMap[channelNumber] = channel;
                        channelNumber++;
                    }
                }

                // Suivez tous les canaux
                foreach (var channelEntry in channelMap)
                {
                    var selectedChannel = channelEntry.Value;
                    Console.WriteLine($"Vous suivez maintenant : {selectedChannel}");

                    // Gérez les mises à jour en utilisant OnUpdate
                    client.OnUpdate += async (updates) =>
                    {
                        foreach (var update in updates.UpdateList)
                        {
                            if (update is UpdateNewMessage newMessage)
                            {
                                var message = newMessage.message as Message;
                                if (message != null && channelMap.Values.Any(channel => message.peer_id.ID == channel.ID))
                                {
                                    Console.WriteLine($"Nouveau message reçu du canal : {channelMap.Values.First(channel => message.peer_id.ID == channel.ID)}");
                                    Console.WriteLine($"{message.from_id}> {message.message} {message.media}");

                                    // Vérifiez si le message contient un mot de passe
                                    var password = ExtractPassword(message.message);
                                    if (password != null)
                                    {
                                        Console.WriteLine($"Mot de passe trouvé : {password}");
                                        SavePassword(password);
                                    }

                                    // Vérifiez si le message contient une pièce jointe et téléchargez-la si nécessaire
                                    if (message.media is MessageMediaDocument documentMedia)
                                    {
                                        var document = documentMedia.document as Document;
                                        if (document != null)
                                        {
                                            var fileExtension = document.mime_type.Split('/')[1];
                                            Console.WriteLine($"Extension du fichier : {fileExtension}");

                                            // Récupérez le nom du fichier à partir des attributs du document
                                            var fileNameAttribute = document.attributes.OfType<DocumentAttributeFilename>().FirstOrDefault();
                                            var fileName = fileNameAttribute != null ? fileNameAttribute.file_name : "downloaded";

                                            // Créez un objet InputDocumentFileLocation
                                            var fileLocation = new InputDocumentFileLocation
                                            {
                                                id = document.id,
                                                access_hash = document.access_hash,
                                                file_reference = document.file_reference,
                                                thumb_size = "" // laissez vide pour télécharger le fichier complet
                                            };

                                            // Obtenez le chemin du répertoire actuel
                                            string currentDirectory = Directory.GetCurrentDirectory();

                                            // Créez le chemin du dossier Databases dans le répertoire actuel
                                            string databasesDirectory = Path.Combine(currentDirectory, "Databases");

                                            // Créez le dossier Databases s'il n'existe pas
                                            if (!Directory.Exists(databasesDirectory))
                                            {
                                                Directory.CreateDirectory(databasesDirectory);
                                            }

                                            // Définissez la valeur de DatabasesDirectory dans GlobalPaths
                                            GlobalPaths.DatabasesDirectory = databasesDirectory;

                                            // Définissez le chemin du fichier local où le fichier sera téléchargé
                                            var localFilePath = Path.Combine(databasesDirectory, fileName);

                                            // Téléchargez le fichier
                                            using (var outputStream = System.IO.File.OpenWrite(localFilePath))
                                            {
                                                await client.DownloadFileAsync(fileLocation, outputStream);
                                            }

                                            // Appelez DBExplorer pour traiter le fichier
                                            DBExplorer.Run(session);
                                        }
                                    }
                                }
                            }
                        }
                    };

                    Console.WriteLine("Appuyez sur une touche pour arrêter le programme.");
                    Console.ReadKey();
                }
            }
            finally
            {
                if (client != null)
                    client.Dispose();
            }
        }

        private static string ExtractPassword(string message)
        {
            var passwordPattern = @"🔒Password:\s*(\S+)";
            var match = Regex.Match(message, passwordPattern);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
            return null;
        }

        private static void SavePassword(string password)
        {
            var filePath = "telegram_passwords.json";
            List<string> passwords;

            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                passwords = JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>();
            }
            else
            {
                passwords = new List<string>();
            }

            if (!passwords.Contains(password))
            {
                passwords.Add(password);
                var updatedJson = JsonConvert.SerializeObject(passwords, Formatting.Indented);
                File.WriteAllText(filePath, updatedJson);
            }
        }
    }
}
