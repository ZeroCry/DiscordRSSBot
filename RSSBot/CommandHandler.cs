using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Serilog;

namespace RSSBot
{
    public class CommandHandler
    {
        public static List<RssObject> RssFeeds = new List<RssObject>();
        public static int Messages;
        public static int Commands;
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        public IServiceProvider Provider;

        public CommandHandler(IServiceProvider provider)
        {
            Provider = provider;
            _client = Provider.GetService<DiscordSocketClient>();
            _commands = new CommandService();

            _client.MessageReceived += DoCommand;
            _client.JoinedGuild += _client_JoinedGuild;
            _client.Ready += _client_Ready;
            _client.Ready += DoRss;
        }

        private async Task _client_Ready()
        {
            if (File.Exists(Path.Combine(AppContext.BaseDirectory, "setup/RSS.json")))
            {
                var feedobj = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "setup/RSS.json"));
                RssFeeds = JsonConvert.DeserializeObject<List<RssObject>>(feedobj);
            }
            if (RssFeeds != null)
            {
                var feeds = JsonConvert.SerializeObject(RssFeeds);
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "setup/RSS.json"), feeds);
            }

            var application = await _client.GetApplicationInfoAsync();
            Log.Information(
                $"Invite: https://discordapp.com/oauth2/authorize?client_id={application.Id}&scope=bot&permissions=2146958591");
        }

        public async Task<List<FeedItem>> Index(string url)
        {
            var articles = new List<FeedItem>();
            var feedUrl = url;
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(feedUrl);
                var responseMessage = await client.GetAsync(feedUrl);
                var responseString = await responseMessage.Content.ReadAsByteArrayAsync();
                var rs2 = Encoding.UTF8.GetString(responseString);

                //extract feed items
                var doc = XDocument.Parse(rs2);
                if (doc.Root != null)
                {
                    var feedItems = from item in doc.Root.Descendants().First(i => i.Name.LocalName == "channel")
                            .Elements().Where(i => i.Name.LocalName == "item")
                        select new FeedItem
                        {
                            Content = item.Elements().First(i => i.Name.LocalName == "description").Value,
                            Link = item.Elements().First(i => i.Name.LocalName == "link").Value,
                            PublishDate = ParseDate(item.Elements().First(i => i.Name.LocalName == "pubDate").Value),
                            Title = item.Elements().First(i => i.Name.LocalName == "title").Value,
                            RssRootUrl = $"{new Uri(feedUrl).Scheme}://{new Uri(feedUrl).Host}",
                            PostRootUrl =
                                $"{new Uri(item.Elements().First(i => i.Name.LocalName == "link").Value).Scheme}://{new Uri(item.Elements().First(i => i.Name.LocalName == "link").Value).Host}"
                        };
                    articles = feedItems.ToList();
                }
            }

            return articles;
        }

        private static DateTime ParseDate(string date)
        {
            return DateTime.TryParse(date, out var result) ? result : DateTime.MinValue;
        }

        private async Task DoRss()
        {
            while (true)
            {
                foreach (var feed in RssFeeds)
                    try
                    {
                        if (feed.Running)
                        {
                            var guild = _client.GetGuild(feed.GuildId);
                            var channel = guild.GetChannel(feed.ChannelId);

                            var feedItems = (await Index(feed.RssUrl)).Where(x =>
                                x.PublishDate.ToUniversalTime() + TimeSpan.FromMinutes(10) > DateTime.UtcNow);

                            if (feed.Embed)
                            {
                                var i = 0;
                                foreach (var item in feedItems)
                                {
                                    var desc = Regex.Replace(item.Content, "<.*?>", "");
                                    var d2 = feed.Formatting
                                        .Replace("$posttitle", item.Title)
                                        .Replace("$postlink", item.Link)
                                        .Replace("$postcontent", desc)
                                        .Replace("$postrssroot", item.RssRootUrl)
                                        .Replace("$postroot", item.PostRootUrl)
                                        .Replace("$rssurl", feed.RssUrl);

                                    await ((IMessageChannel) channel).SendMessageAsync("", false, new EmbedBuilder
                                    {
                                        Description = $"{d2}",
                                        Footer = new EmbedFooterBuilder
                                        {
                                            Text =
                                                $"[{i}] || Post Date: {item.PublishDate.ToString(CultureInfo.InvariantCulture)}UTC"
                                        }
                                    }.Build());

                                    await Task.Delay(1000);
                                    i++;
                                    if (i >= 10)
                                        return;
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }

                await Task.Delay(1000 * 60 * 10);
            }
        }

        private static async Task _client_JoinedGuild(SocketGuild guild)
        {
            var embed = new EmbedBuilder();
            embed.AddField($"{guild.CurrentUser.Username}",
                $"Hi there, I am {guild.CurrentUser.Username}. Type `{Config.Load().Prefix}help` to see a list of my commands");
            embed.WithColor(Color.Blue);
            embed.AddField("Bot Base By PassiveModding", $"Support Server: {Config.Load().HomeInvite} \n" +
                                                         "Patreon: https://www.patreon.com/passivebot");
            try
            {
                await guild.DefaultChannel.SendMessageAsync("", false, embed.Build());
            }
            catch
            {
                foreach (var channel in guild.Channels)
                    try
                    {
                        await ((ITextChannel) channel).SendMessageAsync("", false, embed.Build());
                        break;
                    }
                    catch
                    {
                        //
                    }
            }
        }


        public async Task DoCommand(SocketMessage parameterMessage)
        {
            Messages++;
            if (!(parameterMessage is SocketUserMessage message)) return;
            var argPos = 0;
            var context = new SocketCommandContext(_client, message);


            //Do not react to commands initiated by a bot
            if (context.User.IsBot)
                return;

            //Ensure that commands are only executed if thet start with the bot's prefix
            if (!(message.HasMentionPrefix(_client.CurrentUser, ref argPos) ||
                  message.HasStringPrefix(Config.Load().Prefix, ref argPos))) return;


            var result = await _commands.ExecuteAsync(context, argPos, Provider);

            var commandsuccess = result.IsSuccess;


            if (!commandsuccess)
            {
                var embed = new EmbedBuilder();

                foreach (var module in _commands.Modules)
                foreach (var command in module.Commands)
                    if (context.Message.Content.ToLower()
                        .StartsWith($"{Config.Load().Prefix}{command.Name} ".ToLower()))
                    {
                        embed.AddField("COMMAND INFO", $"Name: {command.Name}\n" +
                                                       $"Summary: {command.Summary}\n" +
                                                       $"Info: {command.Remarks}");
                        break;
                    }

                embed.AddField($"ERROR {result.Error.ToString().ToUpper()}", $"Command: {context.Message}\n" +
                                                                             $"Error: {result.ErrorReason}");


                embed.Color = Color.Red;
                await context.Channel.SendMessageAsync("", false, embed.Build());
                Logger.LogError($"{message.Content} || {message.Author}");
            }
            else
            {
                Logger.LogInfo($"{message.Content} || {message.Author}");
                Commands++;
            }
        }

        public async Task ConfigureAsync()
        {
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly());
        }


        public class FeedItem
        {
            public string Link { get; set; }
            public string Title { get; set; }
            public string Content { get; set; }
            public DateTime PublishDate { get; set; }
            public string RssRootUrl { get; set; }
            public string PostRootUrl { get; set; }
        }
    }
}