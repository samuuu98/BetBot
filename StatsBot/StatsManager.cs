using Model;
using Scraper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StatsBot
{
    public class StatsManager
    {
        public Dictionary<string, Competition> Competitions { get; set; }
        private Dictionary<Competition, List<Match>> LiveMatch { get; set; }

        private Dictionary<Competition, List<Match>> HistoryMatch { get; set; }

        public StatsManager()
        {
            Competitions = new Dictionary<string, Competition>();
            LiveMatch = new Dictionary<Competition, List<Match>>();
            HistoryMatch = new Dictionary<Competition, List<Match>>();
        }

        public double GetProbabilityforGoalByLiveMatch(Match match, int minuteBefore = 90)
        {
            if (match.Result.Away == 0 && match.Result.Home == 0)
                return GetProbabilityforGoalByTeamBetweenMinutes(match.Home, match.Result.Minutes, minuteBefore) + GetProbabilityforGoalByTeamBetweenMinutes(match.Away, match.Result.Minutes, minuteBefore);

            return default;
        }

        public double GetProbabilityforGoalByTeamBetweenMinutes(string team, int minuteMin, int minuteMax)
        {
            return GetProbabilityforGoalByTeamBeforeMinute(team, minuteMax) - GetProbabilityforGoalByTeamBeforeMinute(team, minuteMin);
        }

        public double GetProbabilityforGoalByTeamBeforeMinute(string team, int minute)
        {
            return GetProbabilityThatTeamScoreGoalBeforeMinute(team, minute) + GetProbabilityThatTeamSubisceGoalBeforeMinute(team, minute);
        }

        public double GetProbabilityThatTeamScoreGoalBeforeMinute(string team, int minute)
        {
            var goalScoredHome = (float)HistoryMatch.SelectMany(m => m.Value).Where(m => m.Home.Contains(team) && m.Result.GoalHome.Any()).SelectMany(m => m.Result.GoalHome).Where(m => m <= minute).Sum();
            var goalScoredAway = (float)HistoryMatch.SelectMany(m => m.Value).Where(m => m.Away.Contains(team) && m.Result.GoalAway.Any()).SelectMany(m => m.Result.GoalAway).Where(m => m <= minute).Sum();

            return (goalScoredHome + goalScoredAway) / (float)HistoryMatch.SelectMany(m => m.Value).Where(m => m.Home.Contains(team) || m.Away.Contains(team)).Count();
        }

        public double GetProbabilityThatTeamSubisceGoalBeforeMinute(string team, int minute)
        {
            var goalSubitoHome = (float)HistoryMatch.SelectMany(m => m.Value).Where(m => m.Home.Contains(team) && m.Result.GoalAway.Any()).SelectMany(m => m.Result.GoalAway).Where(m => m <= minute).Sum();
            var goalSubitoAway = (float)HistoryMatch.SelectMany(m => m.Value).Where(m => m.Away.Contains(team) && m.Result.GoalHome.Any()).SelectMany(m => m.Result.GoalHome).Where(m => m <= minute).Sum();

            return (goalSubitoHome + goalSubitoAway) / (float)HistoryMatch.SelectMany(m => m.Value).Where(m => m.Home.Contains(team) || m.Away.Contains(team)).Count();
        }

        public (double For, double Against) GetAvaregeMinFirstGoalByTeam(string team, string competition = "")
        {
            var matches = HistoryMatch.SelectMany(m => m.Value);
            if (!string.IsNullOrEmpty(competition))
                matches = matches.Where(m => m.Competition.Name == competition);

            var sumMinForHome = (float)matches.Where(m => m.Home.Contains(team) && m.Result.GoalHome.Any()).Select(m => m.Result.GoalHome.Min()).Sum();
            var sumMinForAway = (float)matches.Where(m => m.Away.Contains(team) && m.Result.GoalAway.Any()).Select(m => m.Result.GoalAway.Min()).Sum();

            var sumMinAgHome = (float)matches.Where(m => m.Home.Contains(team) && m.Result.GoalAway.Any()).Select(m => m.Result.GoalAway.Min()).Sum();
            var sumMinAgAway = (float)matches.Where(m => m.Away.Contains(team) && m.Result.GoalHome.Any()).Select(m => m.Result.GoalHome.Min()).Sum();

            return (Math.Floor((float)(sumMinForHome + sumMinForAway) / ((float)matches.Where(m => m.Home.Contains(team) || m.Away.Contains(team)).Count())),
            Math.Floor((float)(sumMinAgHome + sumMinAgAway) / ((float)matches.Where(m => m.Home.Contains(team) || m.Away.Contains(team)).Count())));
        }

        public int GetHowManyResultByTeam(string team, int homeRes, int awayRes, int minMin = 0, int maxMin = 90, string competition = "")
        {
            var matches = HistoryMatch.SelectMany(m => m.Value);
            if (!string.IsNullOrEmpty(competition))
                matches = matches.Where(m => m.Competition.Name == competition);

            return matches.Where(m => m.Competition.Name == competition).Where(m => m.Home.Contains(team) || m.Away.Contains(team)).Where(m => m.Result.GoalHome.Count(g => g >= minMin && g <= maxMin) == homeRes && m.Result.GoalAway.Count(g => g >= minMin && g <= maxMin) == awayRes).Count();
        }

        public (int For, int Against, int Match) GetGoalPerMinutesByTeam(string team, int minMin, int maxMin, string competition = "")
        {
            var matches = HistoryMatch.SelectMany(m => m.Value);
            if (!string.IsNullOrEmpty(competition))
                matches = matches.Where(m => m.Competition.Name == competition);

            var goalFHome = matches.Where(m => m.Home.Contains(team)).Select(m => m.Result.GoalHome.Count(g => g >= minMin && g <= maxMin)).Sum();
            var goalFAway = matches.Where(m => m.Away.Contains(team)).Select(m => m.Result.GoalAway.Count(g => g >= minMin && g <= maxMin)).Sum();

            var goalAHome = matches.Where(m => m.Home.Contains(team)).Select(m => m.Result.GoalAway.Count(g => g >= minMin && g <= maxMin)).Sum();
            var goalAAway = matches.Where(m => m.Away.Contains(team)).Select(m => m.Result.GoalHome.Count(g => g >= minMin && g <= maxMin)).Sum();

            return (goalFHome + goalFAway, goalAHome + goalAAway, matches.Where(m => m.Home.Contains(team)).Count() + matches.Where(m => m.Away.Contains(team)).Count());
        }

        public IEnumerable<Match> GetLiveMatches(string competition = "")
        {
            if (Competitions.TryGetValue(competition, out var comp) && LiveMatch.TryGetValue(comp, out var matches))
                return matches;

            if (string.IsNullOrEmpty(competition))
                return LiveMatch.Values.SelectMany(el => el);

            return Array.Empty<Match>();
        }

        public IEnumerable<Match> GetHistoryMatchesByTeam(string team, string competition = "")
        {
            var matches = HistoryMatch.SelectMany(m => m.Value);
            if (!string.IsNullOrEmpty(competition))
                matches = matches.Where(m => m.Competition.Name == competition);

            return matches.Where(m => m.Home.Contains(team) || m.Away.Contains(team));
        }

        public void InsertUpdateMatches(Dictionary<string, List<(string Home, int HomeRes, string Away, int AwayRes, int Minutes)>> matches)
        {
            if (matches != null)
                foreach (var league in matches)
                    foreach (var match in league.Value)
                        InsertUpdateMatch(league.Key, match);
        }

        public void InsertUpdateMatch(string competition, (string Home, int HomeRes, string Away, int AwayRes, int Minutes) match)
        {
            if (Competitions.TryGetValue(competition, out var comp) && LiveMatch.TryGetValue(comp, out var liveMatches))
            {
                var m = liveMatches.FirstOrDefault(mm => mm.Home == match.Home && mm.Away == match.Away);
                if (m != null)
                    liveMatches.FirstOrDefault(mm => mm.Home == match.Home && mm.Away == match.Away).Update(match.HomeRes, match.AwayRes, match.Minutes);
                else
                    InsertMatch(competition, match);
            }
            else
                InsertMatch(competition, match);
        }

        public void InsertMatch(string competition, (string Home, int HomeRes, string Away, int AwayRes, int Minutes) match)
        {
            if (Competitions.TryGetValue(competition, out var comp) && LiveMatch.TryGetValue(comp, out var matches))
            {
                matches.Add(new Match() { Home = match.Home, Away = match.Away, Result = new Result() { Minutes = match.Minutes, Home = match.HomeRes, Away = match.AwayRes }, Competition = comp, Date = DateTime.Now });
            }
            else
            {
                InsertCompetition(competition);
                InsertMatch(competition, match);
            }
        }

        public void InsertCompetition(string name)
        {
            if (!Competitions.TryGetValue(name, out var matches))
            {
                var comp = new Competition() { Name = name };
                Competitions.Add(name, comp);
                LiveMatch.Add(comp, new List<Match>());
                HistoryMatch.Add(comp, new List<Match>());
            }
        }

        public void InsertHistoryMatches(Dictionary<string, List<(string Home, int HomeRes, Dictionary<EStatsType, List<int>> HomeStats, string Away, int AwayRes, Dictionary<EStatsType, List<int>> AwayStats, DateTime Date)>> matches)
        {
            if (matches != null)
                foreach (var competition in matches)
                    foreach (var match in competition.Value)
                        InsertHistoryMatch(competition.Key, match);
        }

        public void InsertHistoryMatch(string competition, (string Home, int HomeRes, Dictionary<EStatsType, List<int>> HomeStats, string Away, int AwayRes, Dictionary<EStatsType, List<int>> AwayStats, DateTime Date) match)
        {
            if (Competitions.TryGetValue(competition, out var comp) && HistoryMatch.TryGetValue(comp, out var matches))
            {
                var matchObj = new Match()
                {
                    Competition = comp,
                    Date = match.Date,
                    Home = match.Home,
                    Away = match.Away,
                    Result = new Result()
                    {
                        Minutes = -1,
                        Home = match.HomeRes,
                        Away = match.AwayRes,
                    }
                };

                if (match.HomeStats.ContainsKey(EStatsType.GOAL))
                {
                    matchObj.Result.GoalHome = match.HomeStats[EStatsType.GOAL];
                    matchObj.Result.GoalAway = match.AwayStats[EStatsType.GOAL];
                }

                if (match.HomeStats.ContainsKey(EStatsType.GOAL))
                {
                    matchObj.Result.HomeCorner = match.HomeStats[EStatsType.CORNER].FirstOrDefault();
                    matchObj.Result.AwayCorner = match.AwayStats[EStatsType.CORNER].FirstOrDefault();
                }

                if (match.HomeStats.ContainsKey(EStatsType.SHOT))
                {
                    matchObj.Result.HomeShot = match.HomeStats[EStatsType.SHOT].FirstOrDefault();
                    matchObj.Result.AwayShot = match.AwayStats[EStatsType.SHOT].FirstOrDefault();
                }

                if (match.HomeStats.ContainsKey(EStatsType.SHOT_ON_TARGET))
                {
                    matchObj.Result.HomeShotOnTarget = match.HomeStats[EStatsType.SHOT_ON_TARGET].FirstOrDefault();
                    matchObj.Result.AwayShotOnTarget = match.AwayStats[EStatsType.SHOT_ON_TARGET].FirstOrDefault();
                }

                if (match.HomeStats.ContainsKey(EStatsType.SHOT_OUTSIDE))
                {
                    matchObj.Result.HomeShotOutside = match.HomeStats[EStatsType.SHOT_OUTSIDE].FirstOrDefault();
                    matchObj.Result.AwayShotOutside = match.AwayStats[EStatsType.SHOT_OUTSIDE].FirstOrDefault();
                }

                matches.Add(matchObj);
            }
            else
            {
                InsertCompetition(competition);
                InsertHistoryMatch(competition, match);
            }
        }
    }
}