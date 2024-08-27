using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Remote;
using OpenQA.Selenium.Support.UI;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Scraper
{
    public class DirettaScraper
    {
        public IWebDriver Browser { get; set; }

        public DirettaScraper()
        {
            var options = new ChromeOptions()
            {
                BinaryLocation = "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe",
            };

            string seleniumUrl = Environment.GetEnvironmentVariable("SELENIUM_REMOTE_URL") ?? "http://localhost:4444";
            Log.Information("Utilizzo dell'URL Selenium: {SeleniumUrl}", seleniumUrl);

            options.AddArguments(new List<string>() { "disable-gpu", "--silent", "log-level=3", "--disable-search-engine-choice-screen" });
            //options.AddArguments(new List<string>() { "headless", "disable-gpu", "--silent", "log-level=3", "--disable-search-engine-choice-screen" });
            //Browser = new ChromeDriver(@"C:\Users\Samuele\Downloads\chromedriver-win64", options);

            var connected = false;
            while (!connected)
            {
                connected = TryToOpenBrowser(seleniumUrl, options);

                if (!connected)
                {
                    Wait();
                    Wait();
                    Wait();
                }
            }

            //Click sulle live
            var liveElement = Browser.FindElement(By.ClassName("filters__group")).FindElements(By.ClassName("filters__tab"))[1];
            Click(liveElement);
            Log.Information("Clicked on Live");
            //Browser.FindElement(By.Id("onetrust-accept-btn-handler"))?.Click();
        }

        public async Task<Dictionary<string, List<(string Home, int HomeRes, string Away, int AwayRes, int Minutes)>>> GetLiveMatchesFromDiretta()
        {
            try
            {
                //Click all arrows
                var arrows = Browser.FindElements(By.ClassName("event__header")).SelectMany(el => el.FindElements(By.ClassName("event__expanderBlock")));
                foreach (var arrow in arrows)
                {
                    try
                    {
                        if (arrow.GetAttribute("Title").Contains("Mostra"))
                            Click(arrow);
                    }
                    catch (Exception ex)
                    {
                        var el = Browser.FindElement(By.ClassName("adsclick"));
                        var html = Browser.PageSource;

                        Log.Error(ex.StackTrace);
                    }
                }

                var list = Browser.FindElement(By.ClassName("sportName")).FindElements(By.XPath("./*"));
                var res = new Dictionary<string, List<(string Home, int HomeRes, string Away, int AwayRes, int Minutes)>>();
                var currentCompetition = string.Empty;
                foreach (var el in list)
                {
                    if (el.GetAttribute("class").Contains("event__match"))
                    {
                        _ = int.TryParse(el.FindElement(By.ClassName("event__stage")).Text, out var min);
                        var scores = el.FindElements(By.ClassName("event__score")).Select(s => s.Text);
                        _ = int.TryParse(scores.FirstOrDefault(), out var homeRes);
                        _ = int.TryParse(scores.LastOrDefault(), out var awayRes);

                        res[currentCompetition].Add((el.FindElement(By.ClassName("event__participant--home")).Text.Replace("\r", "").Split('\n')[0], homeRes, el.FindElement(By.ClassName("event__participant--away")).Text.Replace("\r", "").Split('\n')[0], awayRes, min));
                    }
                    else if (el.GetAttribute("class").Contains("event__header"))
                    {
                        currentCompetition = el.Text.Replace("\r", "").Split('\n')[1];
                        if (!res.TryGetValue(currentCompetition, out _))
                            res.Add(currentCompetition, new List<(string Home, int HomeRes, string Away, int AwayRes, int Minutes)>());
                    }
                }

                return res;
            }
            catch (Exception ex)
            {
                Log.Error(ex.StackTrace);
                return null;
            }
        }

        public async Task<Dictionary<string, List<(string Home, int HomeRes, Dictionary<EStatsType, List<int>> HomeStats, string Away, int AwayRes, Dictionary<EStatsType, List<int>> AwayStats, DateTime Date)>>> GetStatByTeamName(string teamName, IEnumerable<EStatsType> statsTypes)
        {
            Log.Information($"Getting stats for {teamName}");
            try
            {
                var res = new Dictionary<string, List<(string Home, int HomeRes, Dictionary<EStatsType, List<int>> HomeStats, string Away, int AwayRes, Dictionary<EStatsType, List<int>> AwayStats, DateTime Date)>>();

                var searchElement = Browser.FindElement(By.ClassName("searchIcon"));
                Click(searchElement);
                Log.Information($"Clicked on search");

                var search = Browser.FindElement(By.ClassName("searchInput__input"));
                search.Clear();
                search.SendKeys(teamName);
                Log.Information($"Searched for {teamName}");

                Wait();
                var resultSearch = Browser.FindElements(By.ClassName("searchResult")).FirstOrDefault(r => r.FindElement(By.ClassName("searchResult__participantName")).Text.ToLower() == teamName.ToLower());
                var resultName = Browser.FindElement(By.ClassName("searchResult__participantName")).Text;
                if (resultSearch == null)
                {
                    Log.Information($"Non ho trovato: {teamName}");

                    var back = Browser.FindElements(By.ClassName("searchResult")).FirstOrDefault();
                    Click(back);

                    return null;
                }

                Click(resultSearch);
                Log.Information($"Clicked on team");

                var list = Browser.FindElements(By.ClassName("event--summary")).Where(tab => HasElement(tab, "tabs__ear")).FirstOrDefault(tab => tab.FindElement(By.ClassName("tabs__ear")).Text == "Ultimi risultati").FindElement(By.ClassName("sportName")).FindElements(By.XPath("./*"));

                Click(list.LastOrDefault());
                Log.Information($"Clicked on Ultimi Risultati");
                Wait();

                list = Browser.FindElements(By.XPath("//*[@id=\"live-table\"]/div[1]/div/div/*"));
                //list = Browser.FindElements(By.ClassName("event--summary")).Where(tab => HasElement(tab, "tabs__ear")).FirstOrDefault(tab => tab.FindElement(By.ClassName("tabs__ear")).Text == "Ultimi risultati").FindElement(By.ClassName("sportName")).FindElements(By.XPath("./*"));

                var currentCompetition = string.Empty;
                IJavaScriptExecutor js = (IJavaScriptExecutor)Browser;
                js.ExecuteScript("window.scrollTo(0, document.body.scrollHeight)");

                foreach (var el in list.Take(25))
                {
                    if (el.GetAttribute("class").Contains("event__match"))
                    {
                        var homeStats = new Dictionary<EStatsType, List<int>>();
                        var awayStats = new Dictionary<EStatsType, List<int>>();
                        foreach (var statsType in statsTypes)
                        {
                            homeStats.Add(statsType, new List<int>());
                            awayStats.Add(statsType, new List<int>());
                        }

                        var statsClicked = false;

                        try
                        {
                            var elClick = el.FindElement(By.ClassName("eventRowLink"));//.Click();

                            js.ExecuteScript("arguments[0].click();", elClick);
                            Log.Information($"Open match");

                            Browser.SwitchTo().Window(Browser.WindowHandles.LastOrDefault());

                            var homeTeam = Browser.FindElement(By.ClassName("duelParticipant__home"));
                            var homeTeamName = homeTeam.FindElement(By.ClassName("participant__participantName")).Text;

                            var awayTeam = Browser.FindElement(By.ClassName("duelParticipant__away"));
                            var awayTeamName = awayTeam.FindElement(By.ClassName("participant__participantName")).Text;

                            var scoreItems = Browser.FindElement(By.ClassName("detailScore__wrapper")).FindElements(By.XPath("./*")).Select(t => t.Text);
                            var scoreHome = scoreItems.FirstOrDefault();
                            var scoreAway = scoreItems.LastOrDefault();

                            //_ = DateTime.TryParse(Browser.FindElement(By.ClassName("duelParticipant__startTime")).Text, out var date);

                            string dateString = Browser.FindElement(By.ClassName("duelParticipant__startTime")).Text;
                            Log.Information($"Match found: {dateString} -- {homeTeamName} ({scoreHome}) - ({scoreAway}) {awayTeamName}");

                            string format = "dd.MM.yyyy HH:mm";
                            DateTime date;

                            if (!DateTime.TryParseExact(dateString, format, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out date))
                            {
                                Log.Information("Wrong format date: " + date.ToString());
                            }

                            if (statsTypes.Contains(EStatsType.GOAL))
                            {
                                try
                                {
                                    var homeEvent = Browser.FindElements(By.ClassName("smv__homeParticipant"));
                                    foreach (var _event in homeEvent)
                                        if (HasAttributeValue(_event, "data-testid", "wcl-icon-soccer"))
                                            homeStats[EStatsType.GOAL].Add(int.Parse(_event.FindElement(By.ClassName("smv__timeBox")).Text.Replace("'", "").Split('+')[0]));

                                    var awayEvent = Browser.FindElements(By.ClassName("smv__awayParticipant"));
                                    foreach (var _event in awayEvent)
                                        if (HasAttributeValue(_event, "data-testid", "wcl-icon-soccer"))
                                            awayStats[EStatsType.GOAL].Add(int.Parse(_event.FindElement(By.ClassName("smv__timeBox")).Text.Replace("'", "").Split('+')[0]));
                                }
                                catch (Exception ex)
                                {
                                    Log.Information("Problema con lettura minuti gol");
                                    //Log.Information(ex.ToString());
                                }
                            }

                            if (statsTypes.Contains(EStatsType.CORNER))
                            {
                                try
                                {
                                    var statsButton = Browser.FindElement(By.XPath("//*[@id=\"detail\"]/div[7]/div/a[2]/button"));
                                    if (statsButton.Text.ToLower() != "statistiche")
                                        statsButton = Browser.FindElement(By.XPath("//*[@id=\"detail\"]/div[8]/div/a[2]/button"));
                                    if (!statsClicked)
                                    {
                                        statsClicked = Click(statsButton);
                                        Wait();
                                    }

                                    //var cornerElement = Browser.FindElement(By.ClassName("section")).FindElement(By.XPath("./div[8]/div[1]"));
                                    var cornerElements = Browser.FindElement(By.ClassName("section")).FindElements(By.XPath("./*"));
                                    foreach (var cornerElementWrap in cornerElements)
                                    {
                                        var cornerElement = cornerElementWrap.FindElement(By.XPath("./div[1]"));
                                        var name = cornerElement.FindElement(By.XPath("./div[2]/strong")).Text;
                                        if (name.ToLower() == "calci d'angolo")
                                        {
                                            var cornerHome = cornerElement.FindElement(By.XPath("./div[1]/strong")).Text;
                                            var cornerAway = cornerElement.FindElement(By.XPath("./div[3]/strong")).Text;

                                            homeStats[EStatsType.CORNER].Add(int.Parse(cornerHome));
                                            awayStats[EStatsType.CORNER].Add(int.Parse(cornerAway));

                                            break;
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    //Log.Information(ex);
                                }
                            }

                            if (statsTypes.Contains(EStatsType.SHOT))
                            {
                                try
                                {
                                    var statsButton = Browser.FindElement(By.XPath("//*[@id=\"detail\"]/div[7]/div/a[2]/button"));
                                    if (statsButton.Text.ToLower() != "statistiche")
                                        statsButton = Browser.FindElement(By.XPath("//*[@id=\"detail\"]/div[8]/div/a[2]/button"));
                                    if (!statsClicked)
                                    {
                                        statsClicked = Click(statsButton);
                                        Wait();
                                    }

                                    //var cornerElement = Browser.FindElement(By.ClassName("section")).FindElement(By.XPath("./div[8]/div[1]"));
                                    var cornerElements = Browser.FindElement(By.ClassName("section")).FindElements(By.XPath("./*"));
                                    foreach (var cornerElementWrap in cornerElements)
                                    {
                                        var cornerElement = cornerElementWrap.FindElement(By.XPath("./div[1]"));
                                        var name = cornerElement.FindElement(By.XPath("./div[2]/strong")).Text;
                                        if (name.ToLower() == "tiri")
                                        {
                                            var shotHome = cornerElement.FindElement(By.XPath("./div[1]/strong")).Text;
                                            var shotAway = cornerElement.FindElement(By.XPath("./div[3]/strong")).Text;

                                            homeStats[EStatsType.SHOT].Add(int.Parse(shotHome));
                                            awayStats[EStatsType.SHOT].Add(int.Parse(shotAway));

                                            break;
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    //Log.Information(ex);
                                }
                            }

                            if (statsTypes.Contains(EStatsType.SHOT_ON_TARGET))
                            {
                                try
                                {
                                    var statsButton = Browser.FindElement(By.XPath("//*[@id=\"detail\"]/div[7]/div/a[2]/button"));
                                    if (statsButton.Text.ToLower() != "statistiche")
                                        statsButton = Browser.FindElement(By.XPath("//*[@id=\"detail\"]/div[8]/div/a[2]/button"));
                                    if (!statsClicked)
                                    {
                                        statsClicked = Click(statsButton);
                                        Wait();
                                    }

                                    //var cornerElement = Browser.FindElement(By.ClassName("section")).FindElement(By.XPath("./div[8]/div[1]"));
                                    var cornerElements = Browser.FindElement(By.ClassName("section")).FindElements(By.XPath("./*"));
                                    foreach (var cornerElementWrap in cornerElements)
                                    {
                                        var cornerElement = cornerElementWrap.FindElement(By.XPath("./div[1]"));
                                        var name = cornerElement.FindElement(By.XPath("./div[2]/strong")).Text;
                                        if (name.ToLower() == "tiri in porta")
                                        {
                                            var shotHome = cornerElement.FindElement(By.XPath("./div[1]/strong")).Text;
                                            var shotAway = cornerElement.FindElement(By.XPath("./div[3]/strong")).Text;

                                            homeStats[EStatsType.SHOT_ON_TARGET].Add(int.Parse(shotHome));
                                            awayStats[EStatsType.SHOT_ON_TARGET].Add(int.Parse(shotAway));

                                            break;
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    //Log.Information(ex);
                                }
                            }

                            if (statsTypes.Contains(EStatsType.SHOT_OUTSIDE))
                            {
                                try
                                {
                                    var statsButton = Browser.FindElement(By.XPath("//*[@id=\"detail\"]/div[7]/div/a[2]/button"));
                                    if (statsButton.Text.ToLower() != "statistiche")
                                        statsButton = Browser.FindElement(By.XPath("//*[@id=\"detail\"]/div[8]/div/a[2]/button"));
                                    if (!statsClicked)
                                    {
                                        statsClicked = Click(statsButton);
                                        Wait();
                                    }

                                    //var cornerElement = Browser.FindElement(By.ClassName("section")).FindElement(By.XPath("./div[8]/div[1]"));
                                    var cornerElements = Browser.FindElement(By.ClassName("section")).FindElements(By.XPath("./*"));
                                    foreach (var cornerElementWrap in cornerElements)
                                    {
                                        var cornerElement = cornerElementWrap.FindElement(By.XPath("./div[1]"));
                                        var name = cornerElement.FindElement(By.XPath("./div[2]/strong")).Text;
                                        if (name.ToLower() == "tiri")
                                        {
                                            var shotHome = cornerElement.FindElement(By.XPath("./div[1]/strong")).Text;
                                            var shotAway = cornerElement.FindElement(By.XPath("./div[3]/strong")).Text;

                                            homeStats[EStatsType.SHOT_OUTSIDE].Add(int.Parse(shotHome));
                                            awayStats[EStatsType.SHOT_OUTSIDE].Add(int.Parse(shotAway));

                                            break;
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    //Log.Information(ex);
                                }
                            }

                            res[currentCompetition].Add((homeTeamName, int.Parse(scoreHome), homeStats, awayTeamName, int.Parse(scoreAway), awayStats, date));
                            Browser.Close();
                            Browser.SwitchTo().Window(Browser.WindowHandles.First());
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex.StackTrace);
                            Log.Information("CLOSING");
                            Browser.Close();
                        }
                    }
                    else if (el.GetAttribute("class").Contains("wclLeagueHeader"))
                    {
                        currentCompetition = el.Text.Replace("\r", "").Split('\n')[2];
                        if (!res.TryGetValue(currentCompetition, out _))
                            res.Add(currentCompetition, new List<(string Home, int HomeRes, Dictionary<EStatsType, List<int>> HomeStats, string Away, int AwayRes, Dictionary<EStatsType, List<int>> AwayStats, DateTime Date)>());
                    }
                }

                return res;
            }
            catch (Exception ex)
            {
                Log.Information(ex.ToString());
                return null;
            }
        }

        public bool HasElement(IWebElement element, string className)
        {
            try
            {
                var el = element.FindElement(By.ClassName(className));
                return el != null;
            }
            catch (Exception ex)
            {
                //Log.Information(ex);
                return false;
            }
        }

        public bool HasAttributeValue(ISearchContext element, string attribute, string value)
        {
            try
            {
                var el = element.FindElement(By.XPath($".//*[@{attribute}='{value}']"));
                return el != null;
            }
            catch (Exception ex)
            {
                //Log.Information(ex);
                return false;
            }
        }

        public IWebElement GetElementByAttributeValue(ISearchContext element, string attribute, string value)
        {
            try
            {
                var el = element.FindElement(By.XPath($".//*[@{attribute}='{value}']"));
                return el;
            }
            catch (Exception ex)
            {
                //Log.Information(ex);
                return null;
            }
        }

        public IEnumerable<IWebElement> GetElementsByAttributeValue(ISearchContext element, string attribute, string value)
        {
            try
            {
                var el = element.FindElements(By.XPath($".//*[@{attribute}='{value}']"));
                return el;
            }
            catch (Exception ex)
            {
                //Log.Information(ex);
                return null;
            }
        }

        private bool TryToOpenBrowser(string seleniumUrl, ChromeOptions options)
        {
            try
            {
                Browser = new RemoteWebDriver(new Uri(seleniumUrl), options);

                string fullUrl = "https://www.diretta.it/";
                Browser.Navigate().GoToUrl(fullUrl);
                Wait();
                Log.Information("Browser opened");

                return true;
            }
            catch (Exception e)
            {
                Log.Error("Cannot connect to selenium");
                Log.Error(e.ToString());

                return false;
            }
        }

        public bool Click(IWebElement element)
        {
            try
            {
                IJavaScriptExecutor js = (IJavaScriptExecutor)Browser;
                js.ExecuteScript("arguments[0].click();", element);

                return true;
            }
            catch (Exception e)
            {
                Log.Information("Click fallito");
                Log.Error(e.StackTrace);
                return false;
            }
        }

        public void Wait()
        {
            var sss = new Stopwatch();
            sss.Start();
            while (sss.Elapsed < TimeSpan.FromMilliseconds(1500)) { }
            sss.Stop();
        }
    }
}