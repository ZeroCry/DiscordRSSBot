﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Newtonsoft.Json;
using static RSSBot.CommandHandler;

namespace RSSBot
{
    [RequireContext(ContextType.Guild)]
    public class Commands : ModuleBase
    {

        private readonly CommandService _service;

        public Commands(CommandService service)
        {
            _service = service;
        }

        [Command("help")]
        [Summary("help")]
        [Remarks("Lists all commands")]
        public async Task Help()
        {
            var embed = new EmbedBuilder
            {
                Color = new Color(114, 137, 218),
                Title = $"{Context.Client.CurrentUser.Username} | Commands | Prefix: {Config.Load().Prefix}"
            };

            foreach (var module in _service.Modules)
            {
                var list = module.Commands
                    .Select(command => $"`{Config.Load().Prefix}{command.Summary}` - {command.Remarks}").ToList();
                if (module.Commands.Count > 0)
                    embed.AddField(x =>
                    {
                        x.Name = module.Name;
                        x.Value = string.Join("\n", list);
                    });
            }
            embed.AddField("Support",
                "Also Please consider supporting this project on patreon: <https://www.patreon.com/passivebot>");

            await ReplyAsync("", false, embed.Build());
        }

        [Command("invite")]
        [Summary("invite")]
        [Remarks("Bot Invite Link")]
        public async Task Invite()
        {
            var embed = new EmbedBuilder
            {
                Color = Color.Blue
            };
            var application = await Context.Client.GetApplicationInfoAsync();
            embed.AddField($"{Context.Client.CurrentUser.Username} Invite",
                $"Invite: https://discordapp.com/oauth2/authorize?client_id={application.Id}&scope=bot&permissions=2146958591" +
                "\nAlso Please consider supporting this project on patreon: https://www.patreon.com/passivebot");

            await ReplyAsync("", false, embed.Build());
        }

        [Command("FeedInfo")]
        [Summary("FeedInfo")]
        [Remarks("Information about the current servers feed")]
        public async Task Info()
        {
            var server = RssFeeds.FirstOrDefault(x => x.GuildId == Context.Guild.Id);
            if (server == null)
            {
                await Initialise();
                return;
            }

            await ReplyAsync("", false, new EmbedBuilder
            {
                Description = $"**RSS URL**\n" +
                              $"{server.RssUrl}\n" +
                              $"**RSS Channel**\n" +
                              $"{(await Context.Guild.GetChannelAsync(server.ChannelId))?.Name}\n" +
                              $"**RSS Formatting**\n" +
                              $"```\n" +
                              $"{server.Formatting}\n" +
                              $"```\n" +
                              $"**RSS Feed Status (running)**\n" +
                              $"{server.Running}",
                Color = Color.Blue
            });
        }
        [RequireUserPermission(GuildPermission.Administrator)]
        [Command("SetRssURL")]
        [Summary("SetRssURL <url>")]
        [Remarks("Set the current rss feed URL")]
        public async Task Rss([Remainder] string url)
        {
            var server = RssFeeds.FirstOrDefault(x => x.GuildId == Context.Guild.Id);
            if (server == null)
            {
                await Initialise();
                return;
            }

            server.RssUrl = url;

            await ReplyAsync("", false, new EmbedBuilder
            {
                Description = $"RSS URL Will now be set to {url}",
                Color = Color.Blue
            });

            if (RssFeeds != null)
            {
                var feeds = JsonConvert.SerializeObject(RssFeeds, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "setup/RSS.json"), feeds);
            }
        }
        [RequireUserPermission(GuildPermission.Administrator)]
        [Command("SetRssChannel")]
        [Summary("SetRssChannel")]
        [Remarks("Set the current channel to post in")]
        public async Task RssChannel()
        {
            var server = RssFeeds.FirstOrDefault(x => x.GuildId == Context.Guild.Id);
            if (server == null)
            {
                await Initialise();
                return;
            }
            server.ChannelId = Context.Channel.Id;

            await ReplyAsync("", false, new EmbedBuilder
            {
                Description = $"RSS Updates will now be posted in {Context.Channel.Name}",
                Color = Color.Blue
            });

            if (RssFeeds != null)
            {
                var feeds = JsonConvert.SerializeObject(RssFeeds, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "setup/RSS.json"), feeds);
            }
        }
        [RequireUserPermission(GuildPermission.Administrator)]
        [Command("SetFormatting")]
        [Summary("SetFormatting <text>")]
        [Remarks("Set Rss Post Formatting")]
        public async Task SetFormatting([Remainder] string formatting)
        {
            var server = RssFeeds.FirstOrDefault(x => x.GuildId == Context.Guild.Id);
            if (server == null)
            {
                await Initialise();
                return;
            }
            server.Formatting = formatting;

            if (RssFeeds != null)
            {
                var feeds = JsonConvert.SerializeObject(RssFeeds, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "setup/RSS.json"), feeds);
            }

            await ReplyAsync("", false, new EmbedBuilder
            {
                Description = $"Formatting has been saved.",
                Color = Color.Blue
            });
        }
        [RequireUserPermission(GuildPermission.Administrator)]
        [Command("ToggleRss")]
        [Summary("ToggleRss")]
        [Remarks("Toggle The Rss Feed Poster On or Off")]
        public async Task Toggle()
        {
            var server = RssFeeds.FirstOrDefault(x => x.GuildId == Context.Guild.Id);
            if (server == null)
            {
                await Initialise();
                return;
            }
            server.Running = !server.Running;

            if (RssFeeds != null)
            {
                var feeds = JsonConvert.SerializeObject(RssFeeds, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "setup/RSS.json"), feeds);
            }

            await ReplyAsync("", false, new EmbedBuilder
            {
                Description = $"Feed Running Status is now: {server.Running}",
                Color = Color.Blue
            });
        }
        [RequireUserPermission(GuildPermission.Administrator)]
        [Command("Formatting")]
        [Summary("Formatting")]
        [Remarks("See Formatting Tags")]
        public async Task Formatting()
        {
            await ReplyAsync("", false, new EmbedBuilder
            {
                Title = "Formatting Strings",
                Description = "`$posttitle` - This is replaced with the posts title\n\n" +
                              "`$postlink` - This is replaced with the posts URL\n\n" +
                              "`$postcontent` - This is replaced with the post itself\n\n" +
                              "`$postrssroot` - Replaced with the rss website ie. https://passivenation.com/syndication.php => https://passivenation.com/ \n\n" +
                              "`$postroot` - Is Replaced with the posts root, ie. https://passivenation.com/User-PassiveModding => https://passivenation.com/ \n\n" +
                              "`$rssurl` - Replaced with the original RSS Url ie. https://passivenation.com/syndication.php \n\n" +
                              "NOTE: If you can also do `[Words]($postlink)` to convert a url into a hyperlink\n" +
                              "Also these must be lowercase to work"
            });
        }

        [RequireUserPermission(GuildPermission.Administrator)]
        [Command("BlacklistAdd")]
        [Summary("BlacklistAdd <word/phrase>")]
        [Remarks("Blacklist a specific word or phrase so that it doesn't get posted (Case Insensitive)")]
        public async Task Badd(string input)
        {
            var server = RssFeeds.FirstOrDefault(x => x.GuildId == Context.Guild.Id);
            if (server == null)
            {
                await Initialise();
                return;
            }

            server.BlacklistedWords.Add(input);

            if (RssFeeds != null)
            {
                var feeds = JsonConvert.SerializeObject(RssFeeds, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "setup/RSS.json"), feeds);
            }

            await ReplyAsync("", false, new EmbedBuilder
            {
                Description = $"**Blacklisted Words/Phrases:** \n" +
                              $"{string.Join(", ", server.BlacklistedWords)}",
                Color = Color.Blue
            });
        }

        [RequireUserPermission(GuildPermission.Administrator)]
        [Command("BlacklistDel")]
        [Summary("BlacklistDel <word/phrase>")]
        [Remarks("Remove a blacklisted word/phrase from the Blacklist (Case Sensitive)")]
        public async Task Bdel(string input)
        {
            var server = RssFeeds.FirstOrDefault(x => x.GuildId == Context.Guild.Id);
            if (server == null)
            {
                await Initialise();
                return;
            }

            server.BlacklistedWords.Remove(input);

            if (RssFeeds != null)
            {
                var feeds = JsonConvert.SerializeObject(RssFeeds, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "setup/RSS.json"), feeds);
            }

            await ReplyAsync("", false, new EmbedBuilder
            {
                Description = $"**Blacklisted Words/Phrases:** \n" +
                              $"{string.Join(", ", server.BlacklistedWords)}",
                Color = Color.Blue
            });
        }

        [RequireUserPermission(GuildPermission.Administrator)]
        [Command("ToggleDuplicates")]
        [Summary("ToggleDuplicates")]
        [Remarks("Toggle weather duplicate posts get removed or not. ()")]
        public async Task Dupe()
        {
            var server = RssFeeds.FirstOrDefault(x => x.GuildId == Context.Guild.Id);
            if (server == null)
            {
                await Initialise();
                return;
            }

            server.RemoveDuplicates = !server.RemoveDuplicates;

            if (RssFeeds != null)
            {
                var feeds = JsonConvert.SerializeObject(RssFeeds, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "setup/RSS.json"), feeds);
            }

            await ReplyAsync("", false, new EmbedBuilder
            {
                Description = $"Duplicates are removed: {server.RemoveDuplicates}",
                Color = Color.Blue
            });
        }

        [RequireUserPermission(GuildPermission.Administrator)]
        [Command("initialise")]
        [Summary("initialise")]
        [Remarks("Initialise the server for the RSS Reader")]
        public async Task Initialise()
        {
            if (RssFeeds.Any(x => x.GuildId == Context.Guild.Id))
            {
                await ReplyAsync("", false, new EmbedBuilder
                {
                    Description = $"Server Already Initialised",
                    Color = Color.Red
                });
                return;
            }
            var server = new RssObject
            {
                GuildId = Context.Guild.Id,
                ChannelId = Context.Channel.Id,
                Embed = true,
                Formatting = "New Post in $postroot\n" +
                             "[$posttitle]($postlink)\n\n" +
                             "$postcontent"
            };
            if (RssFeeds.Any(x => x.GuildId == Context.Guild.Id))
                RssFeeds.Remove(RssFeeds.First(x => x.GuildId == Context.Guild.Id));
            RssFeeds.Add(server);

            await ReplyAsync("", false, new EmbedBuilder
            {
                Description = $"Server Initialised",
                Color = Color.Blue
            });

            if (RssFeeds != null)
            {
                var feeds = JsonConvert.SerializeObject(RssFeeds, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "setup/RSS.json"), feeds);
            }
        }
    }

    public class RssObject
    {
        public bool Running { get; set; } = true;
        public ulong GuildId { get; set; }
        public ulong ChannelId { get; set; }

        public string Formatting { get; set; } = "New Post\n" +
                                                 "($posttitle)[$postlink]\n\n" +
                                                 "$postcontent";

        public string RssUrl { get; set; }
        public bool Embed { get; set; } = true;

        public bool RemoveDuplicates { get; set; } = true;
        public List<string> BlacklistedWords { get; set; } = new List<string>();
        public List<string> PostList { get; set; } = new List<string>();
    }
}