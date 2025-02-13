using AuthClient;
using System;
using System.Threading.Tasks;
using Figgle;
using static System.Console;
using static AuthClient.TLSClient;
using System.Net.Sockets;

namespace ConnectAuthServer
{
    internal class Program
    {
        static string licenceKey;
        static TLSClient client = new TLSClient(); // Déplacez la création de l'objet client ici

        static async Task Main(string[] args)
        {
            var banner = FiggleFonts.Poison.Render("DATVOX"); 
            Console.ForegroundColor = ConsoleColor.Blue;
            // Obtenez la largeur de la console
            int consoleWidth = Console.WindowWidth;

            // Divisez le texte Figgle en lignes
            var lines = banner.Split('\n');

            // Pour chaque ligne, calculez l'espace nécessaire pour centrer la ligne
            foreach (var line in lines)
            {
                int padding = (consoleWidth - line.Length) / 2;
                if (padding > 0)
                {
                    Console.WriteLine(new string(' ', padding) + line);
                }
                else
                {
                    Console.WriteLine(line);
                }
            }
            Console.ResetColor();

            // Texte de l'auteur
            string authorText = "[Author : Zeblive]";
            int authorPadding = (consoleWidth - authorText.Length) / 2;
            if (authorPadding > 0)
            {
                Console.WriteLine(new string(' ', authorPadding) + authorText);
            }
            else
            {
                Console.WriteLine(authorText);
            }
            // Texte de la version
            string versionText = "[Version : 2.0]";
            int versionPadding = (consoleWidth - versionText.Length) / 2;
            if (versionPadding > 0)
            {
                Console.WriteLine(new string(' ', versionPadding) + versionText);
            }
            else
            {
                Console.WriteLine(versionText);
            }

            // Configuration du client Discord
            string discordToken = "";
            ulong channelId = 0; // Remplacez par l'ID de votre channel Discord
            var discordClient = new DiscordClient(discordToken, channelId);
            await discordClient.StartAsync();

            // Vérifiez que le client Discord est bien connecté
            if (discordClient == null)
            {
                Console.WriteLine("Impossible de se connecter à Discord.");
                return;
            }

            Console.WriteLine("");
            Console.WriteLine("");
            Console.WriteLine("");
            // Attendre la fin du téléchargement
            await WaitForDownloadToComplete(discordClient);

            string publicIp = GetPublicIPAddress();

            if (!string.IsNullOrEmpty(publicIp) && publicIp != "Unknown")
            {
                string certPath = $"{publicIp}.cer";
                client.ImportCertificate(certPath);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[x] Unable to get public IP address. Check internet connection");
                Console.ResetColor();
            }

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.Write("Enter your licence key : ");
            licenceKey = Console.ReadLine();
            Console.ResetColor();

            client.Connect("", 16391, licenceKey);

            Console.WriteLine("");
            Console.Write("ADVICE : Use sendConfig to resend your config.json data");
            Console.WriteLine("");
            InteractiveConsole();
        }

        private static async Task WaitForDownloadToComplete(DiscordClient discordClient)
        {
            // Attendre que le téléchargement soit terminé
            while (!discordClient.DownloadCompleted)
            {
                await Task.Delay(100); // Attendre 100 ms avant de vérifier à nouveau
            }
        }

        static void InteractiveConsole()
        {
            string command = "";
            while (command != "exit")
            {
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.Write("(DataVortex)");
                Console.ResetColor();
                Console.Write("> ");
                command = Console.ReadLine();

                switch (command)
                {
                    case "sendConfig":
                        client.Connect("", 16388, licenceKey); // Utilisez l'instance client ici
                        break;
                        // Ajoutez ici d'autres commandes si nécessaire
                }
            }
        }
    }
}
