using System;
using System.Threading.Tasks;

namespace AuthServer
{
    class Program
    {
        // Variable globale pour stocker le code de vérification
        static string verificationCode;

        static void Main(string[] args)
        {
            string subjectName = "";
            string certificatePath = subjectName + ".pfx"; // Utilisez .pfx au lieu de .cer
            string cerFilePath = subjectName + ".cer";

            // Générer le certificat
            TLS.GenerateCertificate(subjectName);
            Console.WriteLine("Le certificat a été généré avec succès.");

            // Envoyer le certificat via un webhook Discord
            TLS.SendCertificateToDiscord(cerFilePath, "").Wait();

            // Démarrer le serveur
            var server = new TLSServer(certificatePath);
            server.Start(16391).Wait(); // Utilisez IPAddress.Any pour écouter sur toutes les interfaces réseau

            // Garder l'application en cours d'exécution
            while (true)
            {
                var key = Console.ReadKey();
                if (key.KeyChar == 'q' || key.KeyChar == 'Q')
                {
                    break;
                }
            }
        }
    }
}
