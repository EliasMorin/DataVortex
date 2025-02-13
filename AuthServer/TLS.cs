using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using Newtonsoft.Json;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using System.IO;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Prng;
using Org.BouncyCastle.Utilities;
using Org.BouncyCastle.Math;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Crypto.Parameters;
using System.Security.Cryptography;
using AuthServer.DataVortex;
using Discord.Webhook;
using Discord;

namespace AuthServer
{
    public static class GlobalConfig
    {
        public static string RiotApiKey { get; set; }
    }

    public class TLS
    {
        public static void GenerateCertificate(string subjectName)
        {
            var randomGenerator = new CryptoApiRandomGenerator();
            var random = new SecureRandom(randomGenerator);

            var certificateGenerator = new X509V3CertificateGenerator();

            var serialNumber = BigIntegers.CreateRandomInRange(BigInteger.One, BigInteger.ValueOf(long.MaxValue), random);
            certificateGenerator.SetSerialNumber(serialNumber);

            var dirName = new X509Name("CN=" + subjectName);
            certificateGenerator.SetIssuerDN(dirName);
            certificateGenerator.SetSubjectDN(dirName);

            certificateGenerator.SetNotBefore(DateTime.UtcNow.Date);
            certificateGenerator.SetNotAfter(DateTime.UtcNow.Date.AddYears(1));

            var keyGenerationParameters = new KeyGenerationParameters(random, 2048);

            var keyPairGenerator = new RsaKeyPairGenerator();
            keyPairGenerator.Init(keyGenerationParameters);
            var subjectKeyPair = keyPairGenerator.GenerateKeyPair();

            certificateGenerator.SetPublicKey(subjectKeyPair.Public);

            var issuerKeyPair = subjectKeyPair;
            var signatureFactory = new Asn1SignatureFactory("SHA512WITHRSA", issuerKeyPair.Private, random);

            // Créer un certificat X509 avec la clé privée
            var certificate = certificateGenerator.Generate(signatureFactory);
            var x509 = new System.Security.Cryptography.X509Certificates.X509Certificate2(certificate.GetEncoded());

            // Convertir la clé privée en format PKCS#8
            PrivateKeyInfo privateKeyInfo = PrivateKeyInfoFactory.CreatePrivateKeyInfo(subjectKeyPair.Private);
            byte[] privateKey = privateKeyInfo.ToAsn1Object().GetDerEncoded();

            // Créer une clé privée RSA à partir de la clé privée BouncyCastle
            var rsaParams = DotNetUtilities.ToRSAParameters((RsaPrivateCrtKeyParameters)subjectKeyPair.Private);
            var rsaPrivateKey = RSA.Create(rsaParams);

            // Créer un nouveau certificat X509 qui contient la clé privée
            var x509WithPrivateKey = new System.Security.Cryptography.X509Certificates.X509Certificate2(x509.CopyWithPrivateKey(rsaPrivateKey));

            // Enregistrer le certificat et la clé privée dans un fichier .pfx
            File.WriteAllBytes(subjectName + ".pfx", x509WithPrivateKey.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Pfx));

            // Enregistrer le certificat public dans un fichier .cer
            File.WriteAllBytes(subjectName + ".cer", x509.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Cert));

            Console.WriteLine($"Certificate Generated. Subject Name: {subjectName}");
        }

        public static async Task<string> SendCertificateToDiscord(string cerFilePath, string webhookUrl)
        {
            using (var client = new HttpClient())
            {
                using (var form = new MultipartFormDataContent())
                {
                    using (var fileStream = new FileStream(cerFilePath, FileMode.Open, FileAccess.Read))
                    {
                        using (var streamContent = new StreamContent(fileStream))
                        {
                            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-x509-ca-cert");
                            form.Add(streamContent, "file", Path.GetFileName(cerFilePath));

                            var response = await client.PostAsync(webhookUrl, form);
                            var responseContent = await response.Content.ReadAsStringAsync();

                            // Extraire l'URL du fichier de la réponse
                            var jsonResponse = Newtonsoft.Json.Linq.JObject.Parse(responseContent);
                            var fileUrl = jsonResponse["attachments"][0]["url"].ToString();

                            // Afficher l'URL du fichier dans la console
                            Console.WriteLine($"URL du fichier envoyé : {fileUrl}");

                            // Envoyer l'embed avec l'URL du fichier
                            await SendEmbedToDiscord(webhookUrl, fileUrl);

                            return fileUrl;
                        }
                    }
                }
            }
        }

        public static async Task SendEmbedToDiscord(string webhookUrl, string fileUrl)
        {
            using (var client = new DiscordWebhookClient(webhookUrl))
            {
                var embed = new EmbedBuilder();
                embed.WithTitle("SSL Certificate");
                embed.WithDescription($"Link of certificate file : {fileUrl}");
                embed.WithColor(new Discord.Color(5814783)); // Couleur de l'embed (optionnel)
                embed.WithTimestamp(DateTimeOffset.Now);
                embed.WithFooter("DataVortex SSL");

                await client.SendMessageAsync(embeds: new[] { embed.Build() });
            }
        }
    }

    public class TLSServer
    {
        private readonly System.Security.Cryptography.X509Certificates.X509Certificate _serverCertificate = null; // Spécifiez explicitement la classe X509Certificate
        private Dictionary<string, DateTime> lastRequestTimes = new Dictionary<string, DateTime>();

        public TLSServer(string certificatePath)
        {
            _serverCertificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(certificatePath, "", System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.Exportable);
        }

        public async Task Start(int port)
        {
            TcpListener listener = new TcpListener(IPAddress.Any, port); // Utilisez IPAddress.Any pour écouter sur toutes les interfaces réseau
            listener.Start();
            Console.WriteLine($"Server started on {port}");

            while (true)
            {
                Console.WriteLine("Waiting for a client to connect...");
                TcpClient client = await listener.AcceptTcpClientAsync();

                ProcessClient(client);
            }
        }

        private async void ProcessClient(TcpClient client)
        {
            var Sessions = Session.InitializeSessions();

            SslStream sslStream = new SslStream(client.GetStream(), false);
            try
            {
                await sslStream.AuthenticateAsServerAsync(_serverCertificate, false, SslProtocols.Tls12, true);

                // Lire le message du client
                byte[] buffer = new byte[4096];
                int bytesRead = sslStream.Read(buffer, 0, buffer.Length);
                string clientMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                // Obtenez l'adresse IP du client
                string clientIp = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();

                // Vérifiez si le client a déjà envoyé une requête
                if (lastRequestTimes.ContainsKey(clientIp))
                {
                    // Vérifiez si le temps écoulé depuis la dernière requête est inférieur à 1 minute
                    if ((DateTime.Now - lastRequestTimes[clientIp]).TotalMinutes < 1)
                    {
                        // Construire le message d'erreur
                        string errorMessage = "Too many requests. Please try again later. Spam can result in ban.";
                        byte[] errorResponse = Encoding.UTF8.GetBytes(errorMessage);
                        sslStream.Write(errorResponse);

                        return;
                    }
                }

                // Mettez à jour l'heure de la dernière requête de ce client
                lastRequestTimes[clientIp] = DateTime.Now;

                // Lire le fichier licences.json
                string licencesJson = File.ReadAllText("licences.json");

                // Désérialiser le JSON en un dictionnaire
                var licences = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(licencesJson);

                string licenceKey = null; // Ajoutez cette ligne au début de votre méthode ProcessClient

                if (clientMessage.StartsWith("CHECK_LICENCE"))
                {
                    // Extraire la clé de licence du message du client
                    licenceKey = clientMessage.Substring("CHECK_LICENCE".Length).Trim();

                    // Vérifier si la clé de licence est valide
                    string responseMessage;
                    if (licences["LICENCES"].ContainsKey(licenceKey))
                    {
                        // Récupérer le type de licence
                        string licenceType = licences["LICENCES"][licenceKey];

                        responseMessage = $"Licence key valid. Licence type: {licenceType}";
                    }
                    else
                    {
                        responseMessage = "Licence key not valid, please retry or buy one";
                    }

                    // Envoyer la réponse au client
                    byte[] response = Encoding.UTF8.GetBytes(responseMessage);
                    sslStream.Write(response);

                    // Si la clé de licence est valide, attendre le message ACCOUNTS_CONFIG
                    if (responseMessage.Contains("Licence key valid"))
                    {
                        var dataToSave = new Dictionary<string, Dictionary<string, string>>();
                        string configFilePath = Path.Combine(Directory.GetCurrentDirectory(), licenceKey, $"{licenceKey}_config.json");

                        // Lire le fichier existant s'il existe
                        if (File.Exists(configFilePath))
                        {
                            string existingJson = File.ReadAllText(configFilePath);
                            dataToSave = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(existingJson);
                        }

                        while (true) // Boucle infinie pour continuer à lire les messages
                        {
                            bytesRead = sslStream.Read(buffer, 0, buffer.Length);
                            clientMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                            if (clientMessage.StartsWith("ACCOUNTS_CONFIG"))
                            {
                                // Extraire les informations de ACCOUNTS_CONFIG
                                string accountsConfig = clientMessage.Substring("ACCOUNTS_CONFIG".Length).Trim();
                                Console.WriteLine($"Received ACCOUNTS_CONFIG: {accountsConfig}");

                                // Diviser les informations de ACCOUNTS_CONFIG en paires de mot-clé et de webhook
                                string[] accountsConfigPairs = accountsConfig.Split(',');

                                // Créer le dictionnaire à enregistrer
                                var accountsConfigData = new Dictionary<string, string>();

                                foreach (var pair in accountsConfigPairs)
                                {
                                    string[] keywordAndWebhook = pair.Split(new[] { ':' }, 2);
                                    if (keywordAndWebhook.Length == 2)
                                    {
                                        accountsConfigData.Add(keywordAndWebhook[0], keywordAndWebhook[1]);
                                    }
                                }

                                // Ajouter les données extraites au dictionnaire principal
                                dataToSave["ACCOUNTS_CONFIG"] = accountsConfigData;
                            }
                            else if (clientMessage.StartsWith("API_KEYS"))
                            {
                                // Extraire les informations de API_KEYS
                                string apiKeysConfig = clientMessage.Substring("API_KEYS".Length).Trim();
                                Console.WriteLine($"Received API_KEYS: {apiKeysConfig}");

                                // Diviser les informations de API_KEYS en paires de mot-clé et de clé API
                                string[] apiKeysConfigPairs = apiKeysConfig.Split(',');

                                // Créer le dictionnaire à enregistrer
                                var apiKeysConfigData = new Dictionary<string, string>();

                                foreach (var pair in apiKeysConfigPairs)
                                {
                                    string[] keywordAndApiKey = pair.Split(new[] { ':' }, 2);
                                    if (keywordAndApiKey.Length == 2)
                                    {
                                        apiKeysConfigData.Add(keywordAndApiKey[0], keywordAndApiKey[1]);

                                        // Stocker la clé API dans la classe statique
                                        if (keywordAndApiKey[0] == "riot_api_key")
                                        {
                                            GlobalConfig.RiotApiKey = keywordAndApiKey[1];
                                        }
                                    }
                                }

                                // Ajouter les données extraites au dictionnaire principal
                                dataToSave["API_KEYS"] = apiKeysConfigData;
                            }
                            else if (clientMessage.StartsWith("ID_HARDWARE"))
                            {
                                // Extraire les informations de ID_HARDWARE
                                string hardwareId = clientMessage.Substring("ID_HARDWARE".Length).Trim();
                                Console.WriteLine($"Received ID_HARDWARE: {hardwareId}");

                                // Diviser les informations de ID_HARDWARE en paires de clé-valeur
                                string[] hardwareIdPairs = hardwareId.Split(',');

                                // Créer le dictionnaire à enregistrer
                                var hardwareIdData = new Dictionary<string, string>();

                                foreach (var pair in hardwareIdPairs)
                                {
                                    string[] keyAndValue = pair.Split(':');
                                    if (keyAndValue.Length == 2)
                                    {
                                        hardwareIdData.Add(keyAndValue[0], keyAndValue[1]);
                                    }
                                }

                                // Vérifier si la clé de licence est valide
                                string licenceKeyTelegram = licenceKey; // Utilisez la clé de licence stockée ici
                                Console.WriteLine($"Licence Key: {licenceKeyTelegram}");  // Debug: Afficher la clé de licence

                                if (Sessions.ContainsKey(licenceKeyTelegram))
                                {
                                    // Obtenir la session pour cette clé de licence
                                    var session = Sessions[licenceKeyTelegram];

                                    // Incrémenter le compteur de connexion
                                    session.IncrementConnectionCount(licenceKeyTelegram);

                                    // Vérifier si c'est la première connexion
                                    if (session.GetConnectionCount(licenceKeyTelegram) == 0)
                                    {
                                        // Si c'est la première connexion, enregistrez ID_HARDWARE
                                        session.FirstHardwareId = hardwareIdData;

                                        // Ajouter les données extraites au dictionnaire principal
                                        dataToSave["ID_HARDWARE"] = hardwareIdData;
                                    }
                                    else
                                    {
                                        // Si ce n'est pas la première connexion, vérifiez ID_HARDWARE
                                        if (!session.IsHardwareIdSame(hardwareIdData))
                                        {
                                            // Prendre des mesures appropriées
                                            string hardwareResponseMessage = "Le ID_HARDWARE ne correspond pas";
                                            byte[] hardwareResponse = Encoding.UTF8.GetBytes(hardwareResponseMessage);
                                            sslStream.Write(hardwareResponse);
                                        }
                                        else
                                        {
                                            Console.WriteLine("Client vérifié");

                                            // Ajouter les données extraites au dictionnaire principal
                                            dataToSave["ID_HARDWARE"] = hardwareIdData;

                                            // Envoyer une réponse au client
                                            string hardwareResponseMessage = "Le ID_HARDWARE est le même";
                                            byte[] hardwareResponse = Encoding.UTF8.GetBytes(hardwareResponseMessage);
                                            sslStream.Write(hardwareResponse);
                                        }
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"Aucune session trouvée pour la clé de licence {licenceKeyTelegram}");
                                }
                            }
                            else if (clientMessage.StartsWith("SESSION_TELEGRAM"))
                            {
                                // Extraire les informations de SESSION_TELEGRAM
                                string telegramId = clientMessage.Substring("SESSION_TELEGRAM".Length).Trim();
                                Console.WriteLine($"Received SESSION_TELEGRAM: {telegramId}");

                                // Diviser les informations de SESSION_TELEGRAM en paires de clé-valeur
                                string[] telegramIdPairs = telegramId.Split(' ');

                                // Vérifier si la clé de licence est valide
                                string licenceKeyTelegram = licenceKey; // Utilisez la clé de licence stockée ici
                                Console.WriteLine($"Licence Key: {licenceKeyTelegram}");  // Debug: Afficher la clé de licence

                                if (Sessions.ContainsKey(licenceKeyTelegram))
                                {
                                    Console.WriteLine($"Licence Key found in Sessions");  // Debug: Confirmer que la clé de licence est dans Sessions

                                    // Mettre à jour les informations de la session
                                    var session = Sessions[licenceKeyTelegram];
                                    session.ApiId = telegramIdPairs[0];
                                    session.ApiHash = telegramIdPairs[1];
                                    session.PhoneNumber = telegramIdPairs[2];

                                    Console.WriteLine($"Updated Session: ApiId={session.ApiId}, ApiHash={session.ApiHash}, PhoneNumber={session.PhoneNumber}");  // Debug: Afficher les informations mises à jour de la session

                                    // Déplacer la logique de création du fichier de log ici
                                    var logFilePath = Path.Combine(session.logDirectory, "WTelegram.log");
                                    Console.WriteLine(logFilePath);
                                    session.WTelegramLogs = new StreamWriter(logFilePath, true, Encoding.UTF8) { AutoFlush = true };
                                    WTelegram.Helpers.Log = (lvl, str) => session.WTelegramLogs.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{"TDIWE!"[lvl]}] {str}");

                                    // Créer une liste pour stocker les données du fichier
                                    List<byte> sessionFileBytes = new List<byte>();

                                    // Lire le fichier du flux en une seule fois
                                    int bytesReadFile;
                                    do
                                    {
                                        bytesReadFile = sslStream.Read(buffer, 0, buffer.Length);
                                        for (int i = 0; i < bytesReadFile; i++)
                                        {
                                            sessionFileBytes.Add(buffer[i]);
                                        }
                                    }
                                    while (bytesReadFile > 0);  // Continuer à lire tant qu'il y a des données disponibles

                                    // Écrire les données de session dans le fichier WTelegram.session
                                    string sessionFilePath = Path.Combine(session.logDirectory, "WTelegram.session");
                                    File.WriteAllBytes(sessionFilePath, sessionFileBytes.ToArray());
                                    Console.WriteLine("WTelegram.session file received and saved");

                                    // Charger et vérifier la session
                                    using var Telegramclient = new WTelegram.Client(session.Config);
                                    await Telegramclient.LoginUserIfNeeded();
                                    Console.WriteLine($"Nous sommes connectés en tant que {Telegramclient.User} (id {Telegramclient.User.id})");
                                    Console.WriteLine("La session a été chargée et vérifiée.");

                                    await AuthServer.DataVortex.Telegram.TelegramDownload(session, Telegramclient);
                                }
                            }
                            else
                            {
                                break; // Sortir de la boucle si le message n'est ni ACCOUNTS_CONFIG ni ID_HARDWARE
                            }

                            // Convertir le dictionnaire en JSON
                            string json = JsonConvert.SerializeObject(dataToSave, Formatting.Indented);

                            // Enregistrer le JSON dans un fichier
                            File.WriteAllText(configFilePath, json);
                        }
                    }
                }
            }
            catch (AuthenticationException e)
            {
                Console.WriteLine($"Exception: {e.Message}");
                if (e.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {e.InnerException.Message}");
                }
                Console.WriteLine("Authentication failed - closing the connection.");
                sslStream.Close();
                client.Close();
            }
            finally
            {
                sslStream.Close();
                client.Close();
            }
        }
    }
}
