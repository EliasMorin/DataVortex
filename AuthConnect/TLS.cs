using System;
using System.Net.Security;
using System.Net.Sockets;
using Newtonsoft.Json;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Net;
using System.Diagnostics;
using System.Runtime.InteropServices;
using static System.Console;
using Discord.WebSocket;
using Discord;

namespace AuthClient
{
    public class TLSClient
    {
        public class TelegramCredentials
        {
            public int ApiId { get; set; }
            public string ApiHash { get; set; }
            public string PhoneNumber { get; set; }
        }

        public class DiscordClient
        {
            private readonly DiscordSocketClient _client;
            private readonly string _token;
            private readonly ulong _channelId;
            public bool DownloadCompleted { get; private set; } = false;

            public DiscordClient(string token, ulong channelId)
            {
                var config = new DiscordSocketConfig()
                {
                    GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
                };

                _client = new DiscordSocketClient(config);
                _token = token;
                _channelId = channelId;
            }

            public async Task StartAsync()
            {
                _client.Ready += OnReady;

                await _client.LoginAsync(TokenType.Bot, _token);
                await _client.StartAsync();
                await Task.Delay(1000); // Attendre que le client se connecte
            }

            private async Task OnReady()
            {
                var channel = _client.GetChannel(_channelId) as IMessageChannel;
                if (channel != null)
                {
                    var messages = await channel.GetMessagesAsync(limit: 100).FlattenAsync();
                    var lastEmbedMessage = messages.FirstOrDefault(m => m.Embeds.Count > 0);

                    if (lastEmbedMessage != null)
                    {
                        var embed = lastEmbedMessage.Embeds.First();

                        // Cherchez un lien dans la description
                        var description = embed.Description;
                        var link = description.Split(' ').FirstOrDefault(word => Uri.IsWellFormedUriString(word, UriKind.Absolute));
                        if (link != null)
                        {
                            string publicIp = GetPublicIPAddress();
                            if (!string.IsNullOrEmpty(publicIp) && publicIp != "Unknown")
                            {
                                await DownloadFileAsync(link, $"{publicIp}.cer");
                            }
                            else
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine("[x] Unable to get public IP address. Check internet connection");
                                Console.ResetColor();
                            }
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("[x] Link error");
                            Console.ResetColor();
                        }
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("[x] Embed error");
                        Console.ResetColor();
                    }
                }
            }


            private async Task DownloadFileAsync(string fileUrl, string fileName)
            {
                using (var client = new HttpClient())
                {
                    var response = await client.GetAsync(fileUrl);
                    response.EnsureSuccessStatusCode();

                    var filePath = Path.Combine(Directory.GetCurrentDirectory(), fileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await response.Content.CopyToAsync(fileStream);
                    }

                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[-] SSL Downloaded");
                    Console.ResetColor();
                    DownloadCompleted = true; // Indiquer que le téléchargement est terminé
                }
            }
        }

        public void ImportCertificate(string certPath)
        {
            // Créer un nouveau certificat X509 à partir du fichier .pfx
            X509Certificate2 certificate = new X509Certificate2(certPath);

            // Ouvrir le magasin de certificats "Trusted Root" en lecture-écriture
            X509Store store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadWrite);

            // Rechercher le certificat dans le magasin
            X509Certificate2Collection existingCerts = store.Certificates.Find(X509FindType.FindByThumbprint, certificate.Thumbprint, false);

            // Ajouter le certificat au magasin s'il n'est pas déjà présent
            if (existingCerts.Count == 0)
            {
                store.Add(certificate);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[+] Certificate added to the store.");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[+] Certificate is already in the store.");
                Console.ResetColor();
            }

            // Fermer le magasin de certificats
            store.Close();
        }

        public void Connect(string server, int port, string licenceKey)
        {
            TcpClient client = null;
            SslStream sslStream = null;

            try
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[-] Connecting to the server");
                Console.ResetColor();

                // Essayer de créer et de se connecter au serveur
                client = new TcpClient(server, port);

                sslStream = new SslStream(
                    client.GetStream(),
                    false,
                    new RemoteCertificateValidationCallback(ValidateServerCertificate),
                    null
                );

                try
                {
                    sslStream.AuthenticateAsClient(server);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("[+] Authenticated by server");
                    Console.ResetColor();

                    byte[] message = Encoding.UTF8.GetBytes($"CHECK_LICENCE {licenceKey}");
                    sslStream.Write(message);

                    // Lire la réponse du serveur
                    byte[] buffer = new byte[4096];
                    int bytesRead = sslStream.Read(buffer, 0, buffer.Length);
                    string serverResponse = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    // Afficher la réponse du serveur
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("[-] Server response: {0}", serverResponse);
                    Console.ResetColor();

                    // Si la réponse du serveur est un message d'erreur, arrêter l'exécution
                    if (serverResponse.StartsWith("Too many requests"))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("[x] Error: {0}", serverResponse);
                        Console.ResetColor();
                        return;
                    }

                    // Si la clé de licence est valide, envoyer le message ACCOUNTS_CONFIG
                    if (serverResponse.Contains("Licence key valid"))
                    {
                        // Déclarez configFilePath ici
                        string configFilePath = "./config.json";

                        if (File.Exists(configFilePath))
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("[+] Config.json detected");
                            Console.ResetColor();

                            // Lire le fichier config.json
                            string configJson = File.ReadAllText(configFilePath);

                            // Désérialiser le JSON en un objet correspondant à la structure JSON
                            var config = JsonConvert.DeserializeObject<Dictionary<string, object>>(configJson);

                            // Extraire le sous-objet ACCOUNTS_CONFIG
                            var configAccounts = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(config["ACCOUNTS_CONFIG"].ToString());

                            // Créer le message ACCOUNTS_CONFIG
                            string accountsConfigMessage = "ACCOUNTS_CONFIG " + string.Join(",", configAccounts.Select(kv => kv.Key + ":" + kv.Value["webhook"]));

                            // Convertir le message en bytes et l'envoyer au serveur
                            byte[] accountsConfigMessageBytes = Encoding.UTF8.GetBytes(accountsConfigMessage);
                            sslStream.Write(accountsConfigMessageBytes);

                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("[+] ACCOUNTS_CONFIG sent");
                            Console.ResetColor();

                            // Extraire le sous-objet API_KEYS
                            var configApiKeys = JsonConvert.DeserializeObject<Dictionary<string, string>>(config["API_KEYS"].ToString());

                            // Créer le message API_KEYS
                            string apiKeysMessage = "API_KEYS " + string.Join(",", configApiKeys.Select(kv => kv.Key + ":" + kv.Value));

                            // Convertir le message en bytes et l'envoyer au serveur
                            byte[] apiKeysMessageBytes = Encoding.UTF8.GetBytes(apiKeysMessage);
                            sslStream.Write(apiKeysMessageBytes);

                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("[+] API_KEYS sent");
                            Console.ResetColor();

                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("[+] Config.json sent");
                            Console.ResetColor();
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("[x] Config.json not detected");
                            Console.ResetColor();
                        }

                        Console.ResetColor();
                        // Créer le message ID_HARDWARE
                        string hardwareIdMessage = GetHardwareIdMessage();

                        // Convertir le message en bytes et l'envoyer au serveur
                        byte[] hardwareIdMessageBytes = Encoding.UTF8.GetBytes(hardwareIdMessage);
                        sslStream.Write(hardwareIdMessageBytes);

                        // Recevoir la réponse du serveur
                        byte[] hardwareBuffer = new byte[4096];
                        int hardwareBytesRead = sslStream.Read(hardwareBuffer, 0, hardwareBuffer.Length);
                        string hardwareServerResponse = Encoding.UTF8.GetString(hardwareBuffer, 0, hardwareBytesRead);

                        // Vérifier la réponse du serveur
                        if (hardwareServerResponse.Contains("Le ID_HARDWARE ne correspond pas"))
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("[x] Error : Your hardware isn't the same. Please contact admin.");
                            Console.ResetColor();

                            // Déconnecter du serveur
                            sslStream.Close();
                            client.Close();
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("[x] Disconnected from server");
                            Console.ResetColor();
                            return; // Arrête l'exécution de la méthode pour éviter d'envoyer d'autres données après la déconnexion
                        }
                        else if (hardwareServerResponse.Contains("Le ID_HARDWARE est le même"))
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("[+] Client checked or already checked");
                            Console.ResetColor();
                        }
                        // Si le type de licence est Classic, demander à l'utilisateur d'entrer les informations nécessaires
                        if (serverResponse.Contains("Licence type: Classic"))
                        {
                            // Chemin du fichier JSON
                            string credsFilePath = Path.Combine(Environment.CurrentDirectory, "TelegramCreds.json");

                            // Charger les informations de connexion
                            TelegramCredentials creds = LoadCredentials(credsFilePath);

                            // Demander les informations manquantes
                            if (creds.ApiId == 0)
                            {
                                Console.ForegroundColor = ConsoleColor.Magenta;
                                Console.Write("Enter your api_id: ");
                                creds.ApiId = int.Parse(Console.ReadLine());
                            }

                            if (string.IsNullOrEmpty(creds.ApiHash))
                            {
                                Console.ForegroundColor = ConsoleColor.Magenta;
                                Console.Write("Enter your api_hash: ");
                                creds.ApiHash = Console.ReadLine();
                            }

                            if (string.IsNullOrEmpty(creds.PhoneNumber))
                            {
                                Console.ForegroundColor = ConsoleColor.Magenta;
                                Console.Write("Enter your phone_number: ");
                                creds.PhoneNumber = Console.ReadLine();
                            }

                            // Sauvegarder les informations mises à jour
                            SaveCredentials(credsFilePath, creds);

                            // Vérifiez si le fichier WTelegram.session existe déjà
                            string sessionFilePath = Path.Combine(Environment.CurrentDirectory, "WTelegram.session");
                            bool sessionExists = File.Exists(sessionFilePath);

                            if (sessionExists)
                            {
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine("[+] WTelegram.session already existing");
                                Console.ResetColor();
                            }
                            else
                            {
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine("[-] WTelegram.session not existing. Generating ...");
                                Console.ResetColor();
                                sessionFilePath = GenerateWTelegramSession(creds.ApiId, creds.ApiHash, creds.PhoneNumber);
                            }

                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("[-] Retrieving AuthCreds");
                            Console.ResetColor();

                            // Ajouter un délai pour s'assurer que le fichier est complètement fermé
                            System.Threading.Thread.Sleep(10000);

                            // Envoyer les informations ID_TELEGRAM au serveur
                            string telegramId = $"SESSION_TELEGRAM {creds.ApiId} {creds.ApiHash} {creds.PhoneNumber}";
                            sslStream.Write(Encoding.UTF8.GetBytes(telegramId + "\r\n"));

                            byte[] sessionFileBytes = null;

                            try
                            {
                                // Ouvrir le fichier en mode lecture seule avec un verrouillage partagé
                                using (FileStream stream = File.Open(sessionFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                                {
                                    // Lire le fichier
                                    using (BinaryReader reader = new BinaryReader(stream))
                                    {
                                        sessionFileBytes = reader.ReadBytes((int)stream.Length);
                                    }
                                }
                            }
                            catch (IOException ex)
                            {
                                Console.WriteLine($"An error occurred while reading the file: {ex.Message}");
                            }

                            if (sessionFileBytes != null)
                            {
                                // Envoyer le fichier au serveur en une seule fois
                                sslStream.Write(sessionFileBytes, 0, sessionFileBytes.Length);

                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine("[+] WTelegram file sent");
                                Console.ResetColor();
                            }
                            else
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine("[x] The file could not be read.");
                                Console.ResetColor();
                            }
                        }

                        string GenerateWTelegramSession(int api_id, string api_hash, string phone_number)
                        {
                            // Rediriger les logs vers une méthode vide
                            WTelegram.Helpers.Log = (lvl, str) => { };

                            // Créer un objet de configuration pour WTelegramClient
                            var client = new WTelegram.Client(Config);

                            // Fonction de configuration pour WTelegramClient
                            string Config(string what)
                            {
                                switch (what)
                                {
                                    case "api_id": return api_id.ToString();
                                    case "api_hash": return api_hash;
                                    case "phone_number": return phone_number;
                                    case "session_pathname": return Path.Combine(Environment.CurrentDirectory, "WTelegram.session");
                                    case "verification_code":
                                        Console.ForegroundColor = ConsoleColor.Yellow;
                                        Console.Write("[-] Enter Auth code : ");
                                        Console.ResetColor();
                                        return Console.ReadLine();
                                    case "password":
                                        Console.ForegroundColor = ConsoleColor.Yellow;
                                        Console.Write("[-] Enter your 2FA password: ");
                                        Console.ResetColor();
                                        return Console.ReadLine();
                                    default: return null;                  // let WTelegramClient decide the default config
                                }
                            }

                            // Connexion au client pour générer le fichier de session
                            try
                            {
                                client.LoginUserIfNeeded().Wait();
                            }
                            catch (Exception ex)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"[x] Telegram error : {ex.Message}");
                                Console.ResetColor();
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine("[+] Generating SessionFile");
                                Console.ResetColor();
                                File.Delete(Path.Combine(Environment.CurrentDirectory, "WTelegram.session"));
                                client = new WTelegram.Client(Config);
                                client.LoginUserIfNeeded().Wait();
                            }

                            // Fermer la connexion pour libérer le fichier
                            client.Dispose();

                            // Retourner le chemin du fichier de session généré
                            return Path.Combine(Environment.CurrentDirectory, "WTelegram.session");
                        }

                    }

                }
                catch (AuthenticationException e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("[x] Authentication failed - Check SSL/Admin rights");
                    Console.ResetColor();
                    client.Close();
                    return;
                }
                finally
                {
                    sslStream.Close();
                    client.Close();
                }
            }
            catch (SocketException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[x] Server Down : Check your internet connection or wait for server to be online");
                Console.ResetColor();
                return;
            }
        }

        // Méthodes pour charger et sauvegarder les informations de connexion
        public TelegramCredentials LoadCredentials(string filePath)
        {
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                return JsonConvert.DeserializeObject<TelegramCredentials>(json);
            }
            return new TelegramCredentials();
        }

        public void SaveCredentials(string filePath, TelegramCredentials creds)
        {
            var json = JsonConvert.SerializeObject(creds, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        public static bool ValidateServerCertificate(
              object sender,
              X509Certificate certificate,
              X509Chain chain,
              SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[x] Certificate error : SSl is not imported");
            Console.ResetColor();

            // Do not allow this client to communicate with unauthenticated servers.
            return false;
        }

        public static string GetHardwareIdMessage()
        {
            // Détecter l'OS
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[+] Detected OS: Linux");
                Console.ResetColor();

                // Récupérer l'adresse IP
                string ip = Dns.GetHostEntry(Dns.GetHostName()).AddressList[0].ToString();

                // Récupérer le nom d'utilisateur
                string username = Environment.UserName;

                // Récupérer l'architecture du CPU
                string architecture = ExecuteCommand("uname -m");

                // Récupérer les informations sur le processeur
                string cpuModel = ExecuteCommand("grep \"Hardware\" /proc/cpuinfo | uniq | awk -F: '{print $2}'");

                // Récupérer le modèle de l'appareil
                string deviceModel = ExecuteCommand("cat /proc/device-tree/model"); ;

                // Créer le message ID_HARDWARE
                string hardwareIdMessage = $"ID_HARDWARE ip:{ip},username:{username},architecture:{architecture},cpu:{cpuModel},device:{deviceModel}";

                return hardwareIdMessage;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[+] Detected OS: Windows");
                Console.ResetColor();

                // Récupérer l'adresse IP publique
                string ip = GetPublicIPAddress();

                // Récupérer le nom d'utilisateur
                string username = Environment.UserName;

                // Récupérer l'architecture du CPU
                string architecture = Environment.Is64BitOperatingSystem ? "x64" : "x86";

                // Récupérer les informations sur le processeur
                string cpuModel = ExecuteCommand("wmic cpu get name").Replace("Name", "").Trim();

                // Récupérer le modèle de l'appareil
                string deviceModel = ExecuteCommand("wmic computersystem get model").Replace("Model", "").Trim();

                // Créer l'objet ID_HARDWARE
                var hardwareInfo = new
                {
                    ip,
                    username,
                    architecture,
                    cpu = cpuModel,
                    device = deviceModel
                };

                // Sérialiser l'objet en JSON
                string hardwareIdMessage = "ID_HARDWARE " + JsonConvert.SerializeObject(hardwareInfo, Formatting.None);

                return hardwareIdMessage;
            }

            Console.WriteLine("OS: Unknown");
            return null;
        }

        public static string GetPublicIPAddress()
        {
            try
            {
                using (var client = new WebClient())
                {
                    string ip = client.DownloadString("https://api.ipify.org");
                    return ip.Trim();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while getting the public IP address: {ex.Message}");
                return "Unknown";
            }
        }

        private static string ExecuteCommand(string command)
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c " + command,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            var process = new Process { StartInfo = processStartInfo };
            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return output.Trim();
        }
    }
}