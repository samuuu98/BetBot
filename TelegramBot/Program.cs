using Model;
using Scraper;
using Serilog;
using StatsBot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBot;

namespace TelegramBot
{
    internal class Program
    {
        private static StatsManager StatManager;
        private static DirettaScraper DirettaScraper;

        private static void Main(string[] args)
        {
            ConfigureLogging();

            string asciiArt = @"
 ____       _   ____        _
|  _ \     | | |  _ \      | |
| |_) | ___| |_| |_) | ___ | |_
|  _ < / _ \ __|  _ < / _ \| __|
| |_) |  __/ |_| |_) | (_) | |_
|____/ \___|\__|____/ \___/ \__|

       v1.0 - Game On!
";

            Log.Information(asciiArt);
            Log.Information("BetBot loading...");

            StatManager = new StatsManager();
            DirettaScraper = new DirettaScraper();

            TelegramBotClient botClient = new TelegramBotClient("7341383519:AAF6_wA3209r97yKSRwE3QRzdwLi0WAS0fg");

            using var cts = new CancellationTokenSource();

            // StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>() // receive all update types
            };
            botClient.StartReceiving(
                updateHandler: HandleUpdateAsync,
                pollingErrorHandler: HandlePollingErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: cts.Token
            );

            var t = Task.Run(() => botClient.GetMeAsync());
            var me = t.GetAwaiter().GetResult();

            Log.Information($"Start listening for @{me.Username}");

            while (true)
            { }

            Log.Information($"Stopping");
            // Send cancellation request to stop bot
            cts.Cancel();

            DirettaScraper.Browser.Close();
        }

        public static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            // Only process Message updates: https://core.telegram.org/bots/api#message
            if (update.Message is not { } message)
                return;
            // Only process text messages
            if (message.Text is not { } messageText)
                return;

            var chatId = message.Chat.Id;
            var response = string.Empty;
            Log.Information($"Received a '{messageText}' message in chat {chatId}.");
            if (messageText.Split(" ")[0] == "/start")
            {
                response = "Benvenuto";
            }
            else if (messageText.Split(" ")[0] == "/live_matches")
            {
                var res = DirettaScraper.GetLiveMatchesFromDiretta().GetAwaiter().GetResult();
                StatManager.InsertUpdateMatches(res);
                response = PrintLiveResult(StatManager);
            }
            else if (messageText.Split(" ")[0] == "/get_stats")
            {
                if (!StatManager.GetHistoryMatchesByTeam(messageText).Any())
                {
                    var stats = DirettaScraper.GetStatByTeamName(message.Text, new List<EStatsType>() { EStatsType.GOAL, EStatsType.CORNER, EStatsType.SHOT, EStatsType.SHOT_ON_TARGET, EStatsType.SHOT_OUTSIDE }).GetAwaiter().GetResult();
                    StatManager.InsertHistoryMatches(stats);
                }
                response = PrintGoalStats(message.Text);
            }
            else
            {
                //if (!StatManager.GetHistoryMatchesByTeam(messageText).Any())
                {
                    var stats = DirettaScraper.GetStatByTeamName(message.Text, new List<EStatsType>() { EStatsType.GOAL, EStatsType.CORNER, EStatsType.SHOT, EStatsType.SHOT_ON_TARGET, EStatsType.SHOT_OUTSIDE }).GetAwaiter().GetResult();
                    StatManager.InsertHistoryMatches(stats);
                }
                response = PrintGoalStats(message.Text);
            }

            var messageParts = SplitMessage(response);

            foreach (var part in messageParts)
            {
                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: part,
                    parseMode: ParseMode.Markdown
                );
            }
        }

        public static Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var ErrorMessage = exception switch
            {
                ApiRequestException apiRequestException
                    => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            Log.Information(ErrorMessage);
            return Task.CompletedTask;
        }

        private static string PrintGoalStats(string home)
        {
            var strBuilder = new StringBuilder();

            foreach (var comp in StatManager.Competitions.Keys)
            {
                var sections = new[] {
                    StatManager.GetGoalPerMinutesByTeam(home, 0, 10, comp),
                    StatManager.GetGoalPerMinutesByTeam(home, 11, 20, comp),
                    StatManager.GetGoalPerMinutesByTeam(home, 21, 30, comp),
                    StatManager.GetGoalPerMinutesByTeam(home, 31, 40, comp),
                    StatManager.GetGoalPerMinutesByTeam(home, 41, 50, comp),
                    StatManager.GetGoalPerMinutesByTeam(home, 51, 60, comp),
                    StatManager.GetGoalPerMinutesByTeam(home, 61, 70, comp),
                    StatManager.GetGoalPerMinutesByTeam(home, 71, 80, comp),
                    StatManager.GetGoalPerMinutesByTeam(home, 81, 90, comp)
                };

                int totalMatches = sections[0].Match;
                int totalGoalsFor = sections.Sum(s => s.For);
                int totalGoalsAgainst = sections.Sum(s => s.Against);

                if (totalMatches > 0)
                {
                    strBuilder.AppendLine($"🏆 *{home} - {comp}*");
                    strBuilder.AppendLine($"📊 Statistiche basate su {totalMatches} partite\n");

                    // Statistiche Generali
                    strBuilder.AppendLine("*📈 STATISTICHE GENERALI*");
                    strBuilder.AppendLine($"• Media gol fatti: `{(float)totalGoalsFor / totalMatches:F2}`");
                    strBuilder.AppendLine($"• Media gol subiti: `{(float)totalGoalsAgainst / totalMatches:F2}`");
                    strBuilder.AppendLine($"• Partite 0-0: `{StatManager.GetHowManyResultByTeam(home, 0, 0, competition: comp)}`\n");

                    // Statistiche Primo Tempo
                    var firstHT = StatManager.GetGoalPerMinutesByTeam(home, 0, 45, comp);
                    strBuilder.AppendLine("*🕐 PRIMO TEMPO*");
                    strBuilder.AppendLine($"• % gol fatti: `{(float)firstHT.For / firstHT.Match * 100:F1}%`");
                    strBuilder.AppendLine($"• % gol subiti: `{(float)firstHT.Against / firstHT.Match * 100:F1}%`");
                    strBuilder.AppendLine($"• 0-0 all'intervallo: `{StatManager.GetHowManyResultByTeam(home, 0, 0, 0, 45, comp)}`\n");

                    // Minuto Medio Primo Gol
                    var avgMin = StatManager.GetAvaregeMinFirstGoalByTeam(home, comp);
                    strBuilder.AppendLine("*⏱ MINUTO MEDIO PRIMO GOL*");
                    strBuilder.AppendLine($"• Fatto: `{avgMin.For}'`");
                    strBuilder.AppendLine($"• Subito: `{avgMin.Against}'`\n");

                    // Gol per intervallo di tempo
                    strBuilder.AppendLine("*⚽ GOL PER INTERVALLO*");
                    strBuilder.AppendLine("`Minuti   | Fatti | Subiti`");
                    for (int i = 0; i < sections.Length; i++)
                    {
                        strBuilder.AppendLine($"`{i * 10:D2}'-{(i + 1) * 10:D2}'  |   {sections[i].For,2}   |   {sections[i].Against,2}`");
                    }
                    strBuilder.AppendLine();

                    // Statistiche Avanzate
                    int totalCornersFor = 0, totalCornersAgainst = 0, totalShotsFor = 0, totalShotsOnTargetFor = 0, totalShotsOutsideFor = 0, totalShotsAgainst = 0, totalShotsOnTargetAgainst = 0, totalShotsOutsideAgainst = 0;
                    foreach (var match in StatManager.GetHistoryMatchesByTeam(home, comp))
                    {
                        if (match.Home == home)
                        {
                            totalCornersFor += match.Result.HomeCorner;
                            totalShotsFor += match.Result.HomeShot;
                            totalShotsOnTargetFor += match.Result.HomeShotOnTarget;
                            totalShotsOutsideFor += match.Result.HomeShotOutside;

                            totalCornersAgainst += match.Result.AwayCorner;
                            totalShotsAgainst += match.Result.AwayShot;
                            totalShotsOnTargetAgainst += match.Result.AwayShotOnTarget;
                            totalShotsOutsideAgainst += match.Result.AwayShotOutside;
                        }
                        else
                        {
                            totalCornersFor += match.Result.AwayCorner;
                            totalShotsFor += match.Result.AwayShot;
                            totalShotsOnTargetFor += match.Result.AwayShotOnTarget;
                            totalShotsOutsideFor += match.Result.AwayShotOutside;

                            totalCornersAgainst += match.Result.HomeCorner;
                            totalShotsAgainst += match.Result.HomeShot;
                            totalShotsOnTargetAgainst += match.Result.HomeShotOnTarget;
                            totalShotsOutsideAgainst += match.Result.HomeShotOutside;
                        }
                    }

                    strBuilder.AppendLine("*🔍 STATISTICHE AVANZATE*");
                    strBuilder.AppendLine("`Statistica      |    Pro   | Contro `");
                    strBuilder.AppendLine("`----------------|----------|--------`");
                    strBuilder.AppendLine($"`Tiri            | {(float)totalShotsFor / totalMatches:F2}     | {(float)totalShotsAgainst / totalMatches:F2}`");
                    strBuilder.AppendLine($"`Tiri in porta   | {(float)totalShotsOnTargetFor / totalMatches:F2}     | {(float)totalShotsOnTargetAgainst / totalMatches:F2}`");
                    strBuilder.AppendLine($"`Tiri fuori      | {(float)totalShotsOutsideFor / totalMatches:F2}     | {(float)totalShotsOutsideAgainst / totalMatches:F2}`");
                    strBuilder.AppendLine($"`Precisione tiri | {(float)totalShotsOnTargetFor / totalShotsFor * 100:F1}%    | {(float)totalShotsOnTargetAgainst / totalShotsAgainst * 100:F1}%`");
                    strBuilder.AppendLine($"`Conversione tiri| {(float)totalGoalsFor / totalShotsFor * 100:F1}%    | {(float)totalGoalsAgainst / totalShotsAgainst * 100:F1}%`");
                    strBuilder.AppendLine($"`Corner          | {(float)totalCornersFor / totalMatches:F2}     | {(float)totalCornersAgainst / totalMatches:F2}`");
                    strBuilder.AppendLine($"`Tiri per corner | {(float)totalShotsFor / totalCornersFor:F2}     | {(float)totalShotsAgainst / totalCornersAgainst:F2}`\n");

                    // Ultime 5 partite
                    strBuilder.AppendLine("*🗓 ULTIME 7 PARTITE*");
                    foreach (var match in StatManager.GetHistoryMatchesByTeam(home, comp).Take(7))
                    {
                        strBuilder.AppendLine($"{match.Date.ToShortDateString()} - {match.Competition.Name}");
                        strBuilder.AppendLine($"{GetTeamStr(home, match.Home)} {GetTeamStr(home, match.Home, match.Result.Home)} - {GetTeamStr(home, match.Away, match.Result.Away)} {GetTeamStr(home, match.Away)} ");
                        strBuilder.AppendLine($"Tiri:  {GetTeamStr(home, match.Home, match.Result.HomeShot)}-{GetTeamStr(home, match.Away, match.Result.AwayShot)}, In porta:  {GetTeamStr(home, match.Home, match.Result.HomeShotOnTarget)}-{GetTeamStr(home, match.Away, match.Result.AwayShotOnTarget)}, Corner:  {GetTeamStr(home, match.Home, match.Result.HomeCorner)}- {GetTeamStr(home, match.Away, match.Result.AwayCorner)}\n");
                    }
                }
            }

            Log.Information($"Stats readed for {home}");

            return strBuilder.ToString();
        }

        public static string PrintLiveResult(StatsManager statsManager)
        {
            var strBuilder = new StringBuilder();
            foreach (var live in statsManager.GetLiveMatches())
                strBuilder.Append($"{live.Competition.Name} [{live.Result.Minutes}] - {live.Home} ({live.Result.Home}) : {live.Away} ({live.Result.Away})\n");

            return strBuilder.ToString();
        }

        private static List<string> SplitMessage(string message, int maxLength = 4000)
        {
            //var parts = new List<string>();
            //for (int i = 0; i < message.Length; i += maxLength)
            //{
            //    parts.Add(message.Substring(i, Math.Min(maxLength, message.Length - i)));
            //}
            //return parts;

            return message.Split("🏆").Skip(1).ToList();
        }

        private static void ConfigureLogging()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File("logs/betBot-.log", rollingInterval: RollingInterval.Day)
                .CreateLogger();
        }

        private static string GetTeamStr(string team, string teamToValue, int result = -1)
        {
            return result == -1 ? team == teamToValue ? $"*{teamToValue}*" : teamToValue : team == teamToValue ? $"*{result}*" : result.ToString();
        }
    }
}