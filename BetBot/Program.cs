using Microsoft.AspNetCore.Mvc;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using PuppeteerSharp;
using Scraper;
using Serilog;
using StatsBot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace BetBot
{
    public class Program
    {
        private static void Main(string[] args)
        {
            var statManager = new StatsManager();
            var direttaScraper = new DirettaScraper();

            var t = Task.Run(() =>
            {
                while (true)
                {
                    var res = direttaScraper.GetLiveMatchesFromDiretta().GetAwaiter().GetResult();
                    statManager.InsertUpdateMatches(res);
                    PrintLiveResult(statManager);

                    var match = statManager.GetLiveMatches().FirstOrDefault();
                    var homeStats = direttaScraper.GetStatByTeamName(match.Home, new List<Scraper.EStatsType>() { Scraper.EStatsType.GOAL, Scraper.EStatsType.CORNER }).GetAwaiter().GetResult();
                    var awayStats = direttaScraper.GetStatByTeamName(match.Away, new List<Scraper.EStatsType>() { Scraper.EStatsType.GOAL, Scraper.EStatsType.CORNER }).GetAwaiter().GetResult();
                    statManager.InsertHistoryMatches(homeStats);
                    statManager.InsertHistoryMatches(awayStats);
                    PrintGoalStats(statManager, match.Home, match.Away);

                    Task.Delay(10000);
                }
            });

            t.Wait();
        }

        private static void PrintGoalStats(StatsManager statManager, string home, string away)
        {
            var aHomeSection = statManager.GetGoalPerMinutesByTeam(home, 0, 10);
            var bHomeSection = statManager.GetGoalPerMinutesByTeam(home, 11, 20);
            var cHomeSection = statManager.GetGoalPerMinutesByTeam(home, 21, 30);
            var dHomeSection = statManager.GetGoalPerMinutesByTeam(home, 31, 40);
            var eHomeSection = statManager.GetGoalPerMinutesByTeam(home, 41, 50);
            var fHomeSection = statManager.GetGoalPerMinutesByTeam(home, 51, 60);
            var gHomeSection = statManager.GetGoalPerMinutesByTeam(home, 61, 70);
            var hHomeSection = statManager.GetGoalPerMinutesByTeam(home, 71, 90);
            var iHomeSection = statManager.GetGoalPerMinutesByTeam(home, 81, 90);

            var aAwaySection = statManager.GetGoalPerMinutesByTeam(away, 0, 10);
            var bAwaySection = statManager.GetGoalPerMinutesByTeam(away, 11, 20);
            var cAwaySection = statManager.GetGoalPerMinutesByTeam(away, 21, 30);
            var dAwaySection = statManager.GetGoalPerMinutesByTeam(away, 31, 40);
            var eAwaySection = statManager.GetGoalPerMinutesByTeam(away, 41, 50);
            var fAwaySection = statManager.GetGoalPerMinutesByTeam(away, 51, 60);
            var gAwaySection = statManager.GetGoalPerMinutesByTeam(away, 61, 70);
            var hAwaySection = statManager.GetGoalPerMinutesByTeam(away, 71, 90);
            var iAwaySection = statManager.GetGoalPerMinutesByTeam(away, 81, 90);

            var strBuilder = new StringBuilder();

            strBuilder.Append($"{home} Goal per minutes ({aHomeSection.Match} matches):\n\n");
            strBuilder.Append($"          | For | Against\n");
            strBuilder.Append($" 0'- 10'  |  {aHomeSection.For}  |  {aHomeSection.Against}\n");
            strBuilder.Append($" 10'- 20' |  {bHomeSection.For}  |  {bHomeSection.Against}\n");
            strBuilder.Append($" 20'- 30' |  {cHomeSection.For}  |  {cHomeSection.Against}\n");
            strBuilder.Append($" 30'- 40' |  {dHomeSection.For}  |  {dHomeSection.Against}\n");
            strBuilder.Append($" 40'- 50' |  {eHomeSection.For}  |  {eHomeSection.Against}\n");
            strBuilder.Append($" 50'- 60' |  {fHomeSection.For}  |  {fHomeSection.Against}\n");
            strBuilder.Append($" 60'- 70' |  {gHomeSection.For}  |  {gHomeSection.Against}\n");
            strBuilder.Append($" 70'- 80' |  {hHomeSection.For}  |  {hHomeSection.Against}\n");
            strBuilder.Append($" 80'- 90' |  {iHomeSection.For}  |  {iHomeSection.Against}\n");

            strBuilder.Append($"{away} Goal per minutes ({aAwaySection.Match} matches):\n\n");
            strBuilder.Append($"          | For | Against\n");
            strBuilder.Append($" 0'- 10'  |  {aAwaySection.For}  |  {aAwaySection.Against}\n");
            strBuilder.Append($" 10'- 20' |  {bAwaySection.For}  |  {bAwaySection.Against}\n");
            strBuilder.Append($" 20'- 30' |  {cAwaySection.For}  |  {cAwaySection.Against}\n");
            strBuilder.Append($" 30'- 40' |  {dAwaySection.For}  |  {dAwaySection.Against}\n");
            strBuilder.Append($" 40'- 50' |  {eAwaySection.For}  |  {eAwaySection.Against}\n");
            strBuilder.Append($" 50'- 60' |  {fAwaySection.For}  |  {fAwaySection.Against}\n");
            strBuilder.Append($" 60'- 70' |  {gAwaySection.For}  |  {gAwaySection.Against}\n");
            strBuilder.Append($" 70'- 80' |  {hAwaySection.For}  |  {hAwaySection.Against}\n");
            strBuilder.Append($" 80'- 90' |  {iAwaySection.For}  |  {iAwaySection.Against}\n");
        }

        public static void PrintLiveResult(StatsManager statsManager)
        {
            var strBuilder = new StringBuilder();
            foreach (var live in statsManager.GetLiveMatches())
                strBuilder.Append($"{live.Competition.Name} [{live.Result.Minutes}] - {live.Home} ({live.Result.Home}) : {live.Away} ({live.Result.Away})\n");

            //Console.Clear();
            Log.Information(strBuilder.ToString());
        }
    }
}