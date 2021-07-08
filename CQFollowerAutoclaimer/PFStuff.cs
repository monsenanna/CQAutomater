using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PlayFab;
using PlayFab.ClientModels;
using System.Threading;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using System.IO;
using System.Net.Http;
using System.Text;

namespace CQFollowerAutoclaimer
{
    class PFStuff
    {
        readonly string token;
        static public string kongID;
        static public bool isAdmin = false; // don't edit that without an admin password or it'll crash
        static public string adminPassword;
        //static int requestsSent = 0;
        static public string GetDataTime;
        static public string miracleTimes;
        static public bool hasLucy = false;
        static public string followers;
        static public string DQTime;
        static public string DQLevel;
        static public bool DQResult;
        static public string PVPTime;
        static public string PVPCharges;
        static public int PVPLastUpdate = 0;
        static public JToken PVPGrid;
        static public int[] heroLevels;
        static public int[] heroProms;
        static public int emMultiplier;
        static public int FlashStatus;
        static public JToken FlashCurrent;
        static public int FlashLastUpdate = 0;
        static public int EASDay;
        static public int DungStatus = 0;
        static public int DungRunning = 0;
        static public string lastDungLevel = "";
        static public string DungLevel;
        static public JToken LuckyFollowers;
        static public int[] LuckyFollowersLocal = new int[13];
        static public List<string> LuckyFollowersSent = new List<string>();
        static public JArray KeysTower;
        static public int CCDay = -1;
        static public int CCDone;
        static public string PGCards = "no";
        static public int[] PGDeck;
        static public int[] PGPicked;
        static public int PGWon;
        static public int AdventureDay;
        static public int AdventureStatus;
        static public int LotteryDay;
        static public int LotteryCurrent;
        static public int TrainingStatus;
        static public int currentWeeklyEvent;
        static public int[] SpaceStatus = new int[9];
        static public string NextRecycle;
        static public string LastCaptcha;
        static public JToken GamesData;
        static public Dictionary<int, string> eventLoop = new Dictionary<int, string>
            {
                { 0, "No event" },
                { 1, "G.A.M.E.S" },
                { 2, "No event" },
                { 3, "Space Journey" },
                { 4, "No event" },
                { 5, "No event" }
            };

        static public string[] nearbyPlayersIDs;
        static public string[] nearbyPlayersNames;
        static public string username;
        static public int userID = 0;
        static public int userIndex;
        static public int initialRanking = 0;
        static public int currentRanking = 0;
        static public int freeChestRecharge;

        static public string battleResult;
        static public int chestResult;

        static public int normalChests;
        static public int heroChests;
        public int ascensionSpheres = 0;
        public int pranaGems = 0;
        public int cosmicCoins = 0;
        public int universeMarbles = 0;

        static public bool freeChestAvailable = false;

        static public Int64 wbDamageDealt;
        static public int wbMode;
        static public int wbAttacksAvailable;
        static public DateTime wbAttackNext;
        static public int WB_ID = 1;
        static public string WBName;
        static public int attacksLeft = 0;

        static public bool WBchanged = false;
        static public JArray auctionData;
        static public bool T1joined = false;
        static public bool T2joined = false;

        static public TaskQueue logQueue = new TaskQueue();
        static public AppSettings ap = AppSettings.loadSettings();

        public PFStuff(string t, string kid)
        {
            token = t;
            kongID = kid;
            logQueue.queueTimer.Interval = 1000;
        }

        private static int[] getArray(string s)
        {
            s = Regex.Replace(s, @"\s+", "");
            s = s.Substring(1, s.Length - 2);
            s = Regex.Replace(s, "true", "1");
            s = Regex.Replace(s, "false", "0");
            int[] result = s.Split(',').Select(n => Convert.ToInt32(n)).ToArray();
            return result;
        }

        public static Task<bool> addErrorToQueue(string err, string msg, DateTime _dt)
        {
            if(err.Length < 2)
                return Task.FromResult(true);
            logQueue.Enqueue(() => PFStuff.sendLog(err + " " + msg), "ierr");
            try
            {
                using StreamWriter sw = new StreamWriter(Constants.ErrorLog, true);
                sw.WriteLine(DateTime.Now);
                sw.WriteLine("\tError " + err + " " + msg);
                return Task.FromResult(true);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        public static void logError(string err, string msg)
        {
            logQueue.Enqueue(() => addErrorToQueue(err, msg, DateTime.Now), "log");
        }

        private void logError(string err, PlayFabResult<ExecuteCloudScriptResult> result)
        {
            using StreamWriter sw = new StreamWriter(Constants.ErrorLog, true);
            sw.WriteLine(DateTime.Now);
            string msg = "";
            if (result == null)
            {
                msg = "Unknown error";
            }
            else if (result.Result != null)
            {
                msg = result.Result.ToString();
                if (result.Result.FunctionResult != null)
                {
                    msg = result.Result.FunctionResult.ToString();
                }
            }
            logQueue.Enqueue(() => addErrorToQueue(err, msg, DateTime.Now), "log");
        }

        #region Getting Data
        public async Task<bool> LoginKong()
        {
            PlayFabSettings.TitleId = "E3FA";
            var request = new LoginWithKongregateRequest
            {
                AuthTicket = token,
                CreateAccount = false,
                KongregateId = kongID,
            };
            var loginTask = await PlayFabClientAPI.LoginWithKongregateAsync(request);
            var apiError = loginTask.Error;
            var apiResult = loginTask.Result;
            if (apiError != null)
            {
                MessageBox.Show("Failed to log in. Error: " + apiError.ErrorMessage);
                logError("Cloud Script Error", apiError.ErrorMessage);
                return false;
            }
            else if (apiResult != null)
            {
                await getHof();
                await getLeaderboard(11);
                return true;
            }
            return false;
        }

        public async Task<bool> GetGameData()
        {
            var request = new ExecuteCloudScriptRequest()
            {
                RevisionSelection = CloudScriptRevisionOption.Latest,
                FunctionName = "status",
                FunctionParameter = new { token, kid = kongID }
            };
            var statusTask = await PlayFabClientAPI.ExecuteCloudScriptAsync(request);
            if (statusTask.Error != null)
            {
                logError("GetGameData error ", statusTask.Error.ErrorMessage + " --- " + statusTask.Error.ErrorDetails);
                return false;
            }
            if (statusTask.Result == null || statusTask.Result.FunctionResult == null || !statusTask.Result.FunctionResult.ToString().Contains("true"))
            {
                //logError("Cloud Script Error: status", statusTask);
                return false;
            }
            else
            {
                JObject json = JObject.Parse(statusTask.Result.FunctionResult.ToString());
                heroLevels = getArray(json["data"]["city"]["hero"].ToString());
                heroProms = getArray(json["data"]["city"]["promo"].ToString());
                miracleTimes = json["data"]["miracles"].ToString();
                hasLucy = json["data"]["tm"].ToString() == "-1";
                followers = json["data"]["followers"].ToString();
                GetDataTime = json["data"]["now"].ToString();
                DQTime = json["data"]["city"]["daily"]["timer2"].ToString();
                DQLevel = json["data"]["city"]["daily"]["lvl"].ToString();
                //PVPTime = json["data"]["city"]["nextfight"].ToString();
                PVPTime = json["data"]["city"]["pvp"]["next"].ToString();
                PVPCharges = json["data"]["city"]["pvp"]["attacks"].ToString();
                PVPGrid = json["data"]["city"]["setup"];
                bool tmp = ap.doPVPHistory ?? false;
                if (tmp)
                    await updatePVPHistory(json["data"]["city"]["log"]);
                wbAttacksAvailable = int.Parse(json["data"]["city"]["WB"]["atks"].ToString());
                wbAttackNext = Form1.getTime(json["data"]["city"]["WB"]["next"].ToString());
                try
                {
                    CCDone = int.Parse(json["data"]["city"]["cc"]["coins"].ToString());
                    if (int.Parse(json["data"]["city"]["cc"]["tid"].ToString()) < int.Parse(json["data"]["city"]["pge"]["tid"].ToString()) || CCDone < 200)
                        CCDone = 0;
                }
                catch
                {
                    CCDone = 0;
                }
                try
                {
                    if (PGCards != "no")
                        PGCards = json["data"]["city"]["pge"]["attempts"].ToString();
                    PGDeck = getArray(json["data"]["city"]["pge"]["cards"].ToString());
                    PGPicked = getArray(json["data"]["city"]["pge"]["picks"].ToString());
                    PGWon = (int)json["data"]["city"]["pge"]["pg"];
                    if (PGCards != "no" && int.Parse(json["data"]["city"]["pge"]["tid"].ToString()) < int.Parse(json["data"]["city"]["tour"][0]["tid"].ToString())) // don't consider last week
                    {
                        PGCards = "8";
                        for (int i = 0; i < PGDeck.Length; i++)
                        {
                            PGDeck[i] = 0;
                            PGPicked[i] = -1;
                        }
                    }
                    if (PGCards == "0" && json["data"]["city"]["pge"]["done"].ToString() == "True")
                        PGCards = "done";
                }
                catch
                {
                    PGCards = "no";
                    PGDeck = null;
                    PGPicked = null;
                    PGWon = 0;
                }
                AdventureStatus = 0;
                try
                {
                    if (json["data"]["city"]["adventure"]["time"] == null)
                    {
                        AdventureStatus = 1;
                    }
                }
                catch
                {
                }
                try
                {
                    currentWeeklyEvent = getWeeklyEvent();
                    GamesData = json["data"]["city"]["games"];
                    LastCaptcha = json["data"]["city"]["captchats"].ToString();
                    switch (currentWeeklyEvent)
                    {
                        case 3: // Space Journey
                            if (json["data"]["city"]["space"]["current"] != null && json["data"]["city"]["space"]["start"] != null && ((double)json["data"]["city"]["space"]["start"] / 1000) > Form1.getTimestamp(DateTime.UtcNow) - 432000)
                            {
                                SpaceStatus[0] = (int)json["data"]["city"]["space"]["current"]["mission"];
                                SpaceStatus[1] = (int)((double)json["data"]["city"]["space"]["current"]["timer"] / 1000);
                            }
                            else
                            {
                                SpaceStatus[0] = -2;
                            }
                            SpaceStatus[2] = (int)json["data"]["city"]["space"]["upgrades"][0];
                            SpaceStatus[3] = (int)json["data"]["city"]["space"]["upgrades"][1];
                            SpaceStatus[4] = (int)json["data"]["city"]["space"]["upgrades"][2];
                            SpaceStatus[5] = (int)json["data"]["city"]["space"]["upgrades"][3];
                            SpaceStatus[6] = (int)json["data"]["city"]["space"]["upgrades"][4];
                            SpaceStatus[7] = (int)json["data"]["city"]["space"]["gears"];
                            SpaceStatus[8] = (int)json["data"]["city"]["space"]["hyperloop"];
                            break;
                        case 5: // G.A.M.E.S
                            break;
                        default:
                            break;
                    }
                }
                catch(Exception ex)
                {
                    logError("sjerr", ex.Message + " --- " + ex.StackTrace);
                    SpaceStatus[0] = -2; // no SJ
                }
                TrainingStatus = -1;
                try
                {
                    if (json["data"]["city"]["training"]["time"] != null && (int)json["data"]["city"]["training"]["hid"] >= 0)
                    {
                        TrainingStatus = (int)json["data"]["city"]["training"]["hid"];
                    }
                }
                catch
                {
                }
                try
                {
                    NextRecycle = json["data"]["city"]["recycle"]["next"].ToString();
                }
                catch
                {
                    NextRecycle = "-1";
                }
                T1joined = false;
                long Timestamp = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
                int tid = (int)Math.Floor(Timestamp / 864e2);
                for (int i = json["data"]["city"]["tour"].Count() - 1; i >= 0; i--)
                {
                    try
                    {
                        if ((int)json["data"]["city"]["tour"][i]["tid"] == tid)
                            T1joined = true;
                    }
                    catch
                    {
                    }
                }
                if (doSometimes(5))
                    await updateHeroPool();
                return true;
            }
        }

        public static bool doSometimes(int pct)
        {
            Random rnd = new Random();
            int doit = rnd.Next(1, 100);
            if (doit < pct)
                return true;
            return false;
        }

        public static int getWeeklyEvent()
        {
            double day = Math.Floor(Form1.getTimestamp(DateTime.UtcNow) / 86400);
            //logError("getWeeklyEvent day ", day.ToString());
            if ((day - 3) % 7 == 1) // nothing on monday
                return 0;
            return (int)((Math.Ceiling((day - 18379 + 1) / 7) - 1) % PFStuff.eventLoop.Count);
        }

        public async Task<bool> updatePVPHistory(JToken json)
        {
            try
            {
                var d = json[0]["date"].ToString();
                d = d.Substring(0, d.Length - 3);
                if (int.Parse(d) <= PVPLastUpdate)
                    return true;
                PVPLastUpdate = int.Parse(d);
                for (int i = json.Count() - 1; i >= 0; i--)
                {
                    d = json[i]["date"].ToString();
                    d = d.Substring(0, d.Length - 3);
                    // write new data
                    int ownLane = 0;
                    for (int j = 0; j < 6; j++)
                    {
                        if (PVPGrid[j * 6].ToString() == json[i]["setup"][0].ToString() && PVPGrid[1 + j * 6].ToString() == json[i]["setup"][1].ToString())
                        {
                            ownLane = j + 1;
                            break;
                        }
                    }
                    // find player id
                    int enemyid = 0;
                    using (var client = new HttpClient()) // todo : optimize
                    {
                        var values = new Dictionary<string, string> { { "uget", json[i]["enemy"].ToString() } };
                        var cont = new FormUrlEncodedContent(values);
                        var resp = await client.PostAsync("http://dcouv.fr/cq.php", cont);
                        var respString = await resp.Content.ReadAsStringAsync();
                        enemyid = int.Parse(respString);
                    }
                    JObject fightData = JObject.FromObject(new
                    {
                        date = d,
                        pleft = userID,
                        pright = enemyid,
                        lleft = ownLane.ToString(),
                        result = json[i]["result"].ToString(),
                        p1lane = json[i]["setup"],
                        p1levels = json[i]["shero"],
                        p1proms = json[i]["spromo"],
                        p2lane = json[i]["player"],
                        p2levels = json[i]["phero"],
                        p2proms = json[i]["ppromo"]
                    });
                    using (var client = new HttpClient())
                    {
                        var values = new Dictionary<string, string> { { "ipvp", JsonConvert.SerializeObject(fightData) } };
                        var content = new FormUrlEncodedContent(values);
                        var response = await client.PostAsync("http://dcouv.fr/cq.php", content);
                        var responseString = await response.Content.ReadAsStringAsync();
                    }
                }
            }
            catch (Exception webex)
            {
                using (StreamWriter sw = new StreamWriter("ActionLog.txt", true))
                {
                    sw.WriteLine(DateTime.Now + "\n\t" + webex.Message);
                }
                return false;
            }
            return true;
        }

        public async Task<bool> updateHeroPool()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    Dictionary<int, Array> a = new Dictionary<int, Array>();
                    int[] u = new int[1];
                    string[] infos = new string[4];
                    u[0] = userID;
                    a.Add(0, u);
                    a.Add(1, heroLevels);
                    a.Add(2, heroProms);
                    infos[0] = hasLucy.ToString();
                    infos[1] = followers.ToString();
                    infos[2] = emMultiplier.ToString();
                    infos[3] = DQLevel.ToString();
                    a.Add(3, infos);
                    var values = new Dictionary<string, string> { { "uhpl", JsonConvert.SerializeObject(a) } };
                    var content = new FormUrlEncodedContent(values);
                    var response = await client.PostAsync("http://dcouv.fr/cq.php", content);
                    var responseString = await response.Content.ReadAsStringAsync();
                }
                return true;
            }
            catch (Exception ex)
            {
                logError("updateHeroPool ", ex.Message + " --- " + ex.StackTrace);
                return false;
            }
        }

        public static int getHeroLevel(string heroName)
        {
            try
            {
                if (!string.IsNullOrEmpty(heroName))
                {
                    int heroIndex = Array.IndexOf(Constants.heroNames, heroName) - 2;
                    if (heroIndex != -1)
                    {
                        return heroLevels[heroIndex];
                    }
                }
            }
            catch
            {
            }
            return 0;
        }

        public async Task<bool> getLeaderboard(int size)
        {
            await Task.Delay(100);
            try
            {
                var req = new GetLeaderboardRequest
                {
                    StatisticName = "Ranking",
                    MaxResultsCount = 100
                };
                var lbTask = await PlayFabClientAPI.GetLeaderboardAsync(req);
                await Task.Delay(200);
                nearbyPlayersIDs = new string[size];
                nearbyPlayersNames = new string[size];
                var request = new GetLeaderboardAroundPlayerRequest
                {
                    StatisticName = "Ranking",
                    MaxResultsCount = size
                };
                var leaderboardTask = await PlayFabClientAPI.GetLeaderboardAroundPlayerAsync(request);
                using (var client = new HttpClient())
                {
                    var values = new Dictionary<string, string> { { "ulbd", JsonConvert.SerializeObject(lbTask) }, { "ulbd2", JsonConvert.SerializeObject(leaderboardTask) }, { "ulbdp", userID.ToString() } };
                    var content = new FormUrlEncodedContent(values);
                    var response = await client.PostAsync("http://dcouv.fr/cq.php", content);
                    var responseString = await response.Content.ReadAsStringAsync();
                }
                if (leaderboardTask.Error != null)
                {
                    logError(leaderboardTask.Error.Error.ToString(), leaderboardTask.Error.ErrorMessage);
                    await Task.Delay(1500);
                    return false;
                }
                if (leaderboardTask == null || leaderboardTask.Result == null)
                {
                    logError("Leaderboard Error", leaderboardTask.Result.ToString());
                    await Task.Delay(1500);
                    return false;
                }
                else
                {
                    for (int i = 0; i < size; i++)
                    {
                        nearbyPlayersIDs[i] = leaderboardTask.Result.Leaderboard[i].PlayFabId;
                        nearbyPlayersNames[i] = leaderboardTask.Result.Leaderboard[i].DisplayName;
                        if (leaderboardTask.Result.Leaderboard[i].DisplayName == username)
                        {
                            userIndex = i;
                            currentRanking = leaderboardTask.Result.Leaderboard[i].Position;
                            if (initialRanking == 0)
                                initialRanking = currentRanking;
                        }
                    }
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> getHof()
        {
            await Task.Delay(500);
            var request = new GetTitleDataRequest
            {
                Keys = new List<string> { "hof", "tour" }
            };
            var hofTask = await PlayFabClientAPI.GetTitleDataAsync(request);
            if (hofTask.Error != null)
            {
                logError(hofTask.Error.Error.ToString(), hofTask.Error.ErrorMessage);
                await Task.Delay(1500);
                return false;
            }
            if (hofTask == null || hofTask.Result == null)
            {
                logError("Hof Error", hofTask.Result.ToString());
                await Task.Delay(1500);
                return false;
            }
            else
            {
                if (doSometimes(5))
                {
                    Dictionary<int, string> res = new Dictionary<int, string>();
                    JArray json = JArray.Parse(hofTask.Result.Data["tour"]);
                    for (int i = 0; i < json.Count(); i++)
                    {
                        int tid = int.Parse(json[i]["tid"].ToString());
                        try
                        {
                            res.Add(tid, json[i]["top10"].ToString());
                        }
                        catch
                        {
                        }
                    }
                    using var client = new HttpClient();
                    try
                    {
                        var values = new Dictionary<string, string> { { "uupd", JsonConvert.SerializeObject(res) } };
                        var content = new FormUrlEncodedContent(values);
                        var response = await client.PostAsync("http://dcouv.fr/cq.php", content);
                        var responseString = await response.Content.ReadAsStringAsync();
                    }
                    catch
                    {
                        return true;
                    }
                }
                return true;
            }
        }

        public async Task<bool> getCurrencies()
        {
            var request = new GetUserInventoryRequest();
            var currenciesTask = await PlayFabClientAPI.GetUserInventoryAsync(request);

            if (currenciesTask.Error != null)
            {
                logError(currenciesTask.Error.Error.ToString(), currenciesTask.Error.ErrorMessage);
                await Task.Delay(1500);
                return false;
            }
            else if (currenciesTask.Result != null)
            {
                freeChestRecharge = currenciesTask.Result.VirtualCurrencyRechargeTimes["BK"].SecondsToRecharge;
                normalChests = int.Parse(currenciesTask.Result.VirtualCurrency["PK"].ToString());
                pranaGems = int.Parse(currenciesTask.Result.VirtualCurrency["PG"].ToString());
                cosmicCoins = int.Parse(currenciesTask.Result.VirtualCurrency["CC"].ToString());
                ascensionSpheres = int.Parse(currenciesTask.Result.VirtualCurrency["AS"].ToString());
                universeMarbles = int.Parse(currenciesTask.Result.VirtualCurrency["UM"].ToString());
                heroChests = int.Parse(currenciesTask.Result.VirtualCurrency["KU"].ToString()) / 10;
                freeChestAvailable = currenciesTask.Result.VirtualCurrency["BK"].ToString() == "1";
                emMultiplier = 1;
                for (int i = 0; i < currenciesTask.Result.Inventory.Count; i++)
                {
                    if(currenciesTask.Result.Inventory[i].ItemId == "FOL")
                    {
                        emMultiplier = (int)currenciesTask.Result.Inventory[i].RemainingUses;
                    }
                }
                return true;
            }
            await Task.Delay(1500);
            return false;
        }

        public async void getUsername(string id)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(@"http://api.kongregate.com/api/user_info.json?user_id=" + id);
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                string content = new StreamReader(response.GetResponseStream()).ReadToEnd();
                JObject json = JObject.Parse(content);
                username = json["username"].ToString();

                using var client = new HttpClient();
                var values = new Dictionary<string, string> { { "uget", username } };
                var cont = new FormUrlEncodedContent(values);
                var resp = await client.PostAsync("http://dcouv.fr/cq.php", cont);
                var respString = await resp.Content.ReadAsStringAsync();
                userID = int.Parse(respString);
            }
            catch
            {
                username = null;
            }
        }

        internal static void getWebsiteData(string id)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(@"https://cosmosquest.net/public.php?kid=" + id);
                string content = "";
                try
                {
                    HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                    content = new StreamReader(response.GetResponseStream()).ReadToEnd();
                }
                catch (Exception ex) // try again
                {
                    logError("getWebsiteData ","http failed, retrying --- " + ex.Message);
                    Thread.Sleep(1000);
                    HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                    content = new StreamReader(response.GetResponseStream()).ReadToEnd();
                    logError("getWebsiteData ", "http retry succeeded");
                }

                JObject json = JObject.Parse(content);
                var WBData = json["WB"];
                auctionData = (JArray)json["auction"];
                wbDamageDealt = Int64.Parse(WBData["dealt"].ToString());
                wbMode = int.Parse(WBData["mode"].ToString());
                WBName = WBData["name"].ToString();
                WBchanged = (int.Parse(WBData["atk"].ToString()) > attacksLeft);               
                attacksLeft = int.Parse(WBData["atk"].ToString());
                if (json["flash"] != null)
                {
                    FlashStatus = 1;
                    FlashCurrent = json["flash"]["current"];
                    if (doSometimes(30))
                        updateFlashHistory(json["flash"]);
                }
                else
                {
                    FlashStatus = -1;
                }
                if (json["super"] != null && Int64.Parse(json["super"].ToString()) == 1 && !WBName.Contains("SUPER")) // todo : improve
                {
                    EASDay = 1;
                }
                else
                {
                    EASDay = -1;
                }
                if (json["dungeon"] != null)
                {
                    DungLevel = json["dungeon"]["lvl"].ToString();
                    if (DungStatus == -1)
                        DungStatus = 0;
                }
                else
                {
                    DungStatus = -1;
                    DungLevel = "-/-";
                }
                if (json["cc"] != null)
                {
                    CCDay = 1;
                }
                else
                {
                    CCDay = -1;
                }
                if (json["adventure"] == null || json["adventure"].ToString() != "True")
                {
                    AdventureDay = -1;
                }
                else
                {
                    AdventureDay = 1;
                }
                if (json["lottery"] != null)
                {
                    LotteryDay = 1;
                    LotteryCurrent = json["lottery"]["numbers"].Count();
                }
                else
                {
                    LotteryDay = -1;
                    LotteryCurrent = 0;
                }
                if (WBchanged)
                {
                    HttpWebRequest request2 = (HttpWebRequest)WebRequest.Create(@"https://cosmosquest.net/wb.php");
                    HttpWebResponse response2 = (HttpWebResponse)request2.GetResponse();
                    string content2 = new StreamReader(response2.GetResponseStream()).ReadToEnd();
                    string a = Regex.Match(content2, "(?<=<a href.*>).*?(?=</a>)").ToString();
                    WB_ID = int.Parse(a);
                }
                if (json["followers"] != null)
                {
                    LuckyFollowers = json["followers"];
                }
                else
                {
                    LuckyFollowers = null;
                }
                try
                {
                    KeysTower = (JArray)json["keys"];
                }
                catch
                {
                    KeysTower = null;
                }
                if (PGCards == "no")
                    PGCards = "8";
                if (json["pge"] == null || json["pge"].ToString() != "True")
                {
                    PGCards = "no";
                    PGDeck = null;
                    PGPicked = null;
                    PGWon = 0;
                }
                T2joined = false;
                if (json["tour"]["current"]["joined"].ToString() == "True")
                    T2joined = true;
            }
            catch (Exception ex)
            {
                logError("getWebsiteData ", ex.Message + " --- " + ex.StackTrace);
            }
        }

        internal static bool updateFlashHistory(JToken json)
        {
            try
            {
                var d = json["history"][json["history"].Count() - 1]["date"].ToString();
                d = d.Substring(0, d.Length - 3);
                if (long.Parse(d) <= FlashLastUpdate)
                    return true;
                using var client = new HttpClient();
                string jc = LZString.compressToEncodedURIComponent(json.ToString());
                var values = new Dictionary<string, string> { { "uflc", jc }, { "uflcp", userID.ToString() } };
                var encodedItems = values.Select(i => WebUtility.UrlEncode(i.Key) + "=" + WebUtility.UrlEncode(i.Value));
                var content = new StringContent(String.Join("&", encodedItems), Encoding.UTF8, "application/x-www-form-urlencoded");
                var r = Task.Run(() => client.PostAsync("http://dcouv.fr/cq.php", content));
                r.Wait();
                if(r.IsCompleted)
                    FlashLastUpdate = int.Parse(d) - 60 * 60 * 8; // 8h before
            }
            catch (Exception ex)
            {
                logError("updateFlashHistory ", ex.Message + " --- " + ex.StackTrace);
                FlashLastUpdate = 0;
                return false;
            }
            return true;
        }

        internal async Task<int> getWBData(string id)
        {
            int retryCount = 4;
            while (retryCount > 0)
            {
                await Task.Delay(1000);
                try
                {
                    if (username == null)
                    {
                        this.getUsername(kongID);
                    }
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(@"https://cosmosquest.net/wb.php?id=" + id);
                    var response = await request.GetResponseAsync();

                    string content = new StreamReader(response.GetResponseStream()).ReadToEnd();
                    string a = Regex.Match(content, username + ".*?</tr>").ToString();
                    var b = Regex.Matches(a, "(?<=\"small\">).*?(?=</td>)");
                    if (string.IsNullOrEmpty(a) || b.Count == 0)
                    {
                        retryCount--;
                    }
                    else
                    {
                        return int.Parse(b[1].ToString().Replace(".", ""));
                    }
                }
                catch (WebException wbDataException)
                {
                    retryCount--;
                    Console.Write(wbDataException.Message);
                }
            }
            return 0;
        }

        public async Task<int> getEasiestOpponent()
        {
            using var client = new HttpClient();
            try
            {
                var values = new Dictionary<string, string> { { "eopp", userID.ToString() } };
                var content = new FormUrlEncodedContent(values);
                var response = await client.PostAsync("http://dcouv.fr/cq.php", content);
                var responseString = await response.Content.ReadAsStringAsync();
                string[] result = responseString.Split(',').ToArray();
                for (int i = 0; i < result.Length; i++)
                {
                    if (nearbyPlayersNames.Contains(result[i]))
                    {
                        return int.Parse(nearbyPlayersIDs[Array.IndexOf(nearbyPlayersNames, result[i])]);
                    }
                }
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        public async Task<bool> getCQAVersion(Form1 f, bool force)
        {

            if (force || doSometimes(20))
                using (var client = new HttpClient())
                {
                    try
                    {
                        var values = new Dictionary<string, string> { { "cqav", Constants.version } };
                        var content = new FormUrlEncodedContent(values);
                        var response = await client.PostAsync("http://dcouv.fr/cq.php", content);
                        var responseString = await response.Content.ReadAsStringAsync();
                        if (responseString == "1")
                            f.versionLabel.ForeColor = System.Drawing.Color.Red;
                        else
                            f.versionLabel.ForeColor = System.Drawing.Color.DarkGreen;
                        return responseString == "0";
                    }
                    catch
                    {
                        return true;
                    }
                }
            return true;
        }

        #endregion

        #region Sending requests
        public void sendBuyWC()
        {
            var request = new ExecuteCloudScriptRequest()
            {
                RevisionSelection = CloudScriptRevisionOption.Latest,
                FunctionName = "buywc",
                FunctionParameter = new { wc = 0 }
            };
            var statusTask = PlayFabClientAPI.ExecuteCloudScriptAsync(request);
            bool _running = true;
            while (_running)
            {
                if (statusTask.IsCompleted)
                {
                    var apiError = statusTask.Result.Error;
                    var apiResult = statusTask.Result.Result;

                    if (apiError != null)
                    {
                        return;
                    }
                    else if (apiResult != null)
                    {
                        return;
                    }
                    _running = false;
                }
                Thread.Sleep(500);
            }
            return;
        }

        public async Task<bool> sendClaimAll()
        {
            var request = new ExecuteCloudScriptRequest()
            {
                RevisionSelection = CloudScriptRevisionOption.Latest,
                FunctionName = "claimall",
                FunctionParameter = new { kid = kongID }
            };
            var statusTask = await PlayFabClientAPI.ExecuteCloudScriptAsync(request);
            if (statusTask.Error != null)
            {
                logError(statusTask.Error.Error.ToString(), statusTask.Error.ErrorMessage);
                return false;
            }
            if (statusTask == null || statusTask.Result.FunctionResult == null || !statusTask.Result.FunctionResult.ToString().Contains("true"))
            {
                //logError("Cloud Script Error: Claim All", statusTask);
                return false;
            }
            else
            {
                JObject json = JObject.Parse(statusTask.Result.FunctionResult.ToString());
                miracleTimes = json["data"]["miracles"].ToString();
                followers = json["data"]["followers"].ToString();
                return true;
            }
        }

        public async Task<bool> sendPVPFight(int index)
        {
            battleResult = "";
            var request = new ExecuteCloudScriptRequest()
            {
                RevisionSelection = CloudScriptRevisionOption.Latest,
                FunctionName = "fight",
                FunctionParameter = new { token, kid = kongID, id = nearbyPlayersIDs[index] }
            };
            var statusTask = await PlayFabClientAPI.ExecuteCloudScriptAsync(request);
            if (statusTask.Error != null)
            {
                battleResult = "";
                logError(statusTask.Error.Error.ToString(), statusTask.Error.ErrorMessage + " vs player " + nearbyPlayersIDs[index]);
                return false;
            }
            if (statusTask == null || statusTask.Result.FunctionResult == null || !statusTask.Result.FunctionResult.ToString().Contains("true"))
            {
                battleResult = "";
                logError("Cloud Script Error: PvP Fight", statusTask.Result.FunctionResult.ToString() + " vs player " + nearbyPlayersIDs[index]);
                return false;
            }
            else
            {
                JObject json = JObject.Parse(statusTask.Result.FunctionResult.ToString());
                PVPTime = json["data"]["city"]["pvp"]["next"].ToString();
                switch (json["data"]["city"]["log"][0]["result"].ToString())
                {
                    case ("-1"):
                        battleResult = "Lose";
                        break;
                    case ("0"):
                        battleResult = "Draw";
                        break;
                    case ("1"):
                        battleResult = "Win";
                        break;
                }
                if (json["data"]["city"]["log"][0]["enemy"] != null)
                {
                    battleResult += " vs " + json["data"]["city"]["log"][0]["enemy"].ToString();
                }
                else
                {
                    battleResult += " vs undefined";
                }
                battleResult += ", ELO " + int.Parse(json["data"]["city"]["log"][0]["rankd"].ToString()).ToString("+0;-#") + ", Star Dust +" + json["data"]["city"]["log"][0]["earn"].ToString() + "\n";
                return true;
            }
        }

        public async Task<bool> sendOpen(string chestMode)
        {
            if (chestMode != "normal" && chestMode != "hero")
            {
                throw new ArgumentException("Wrong chest mode");
            }
            var request = new ExecuteCloudScriptRequest()
            {
                RevisionSelection = CloudScriptRevisionOption.Latest,
                FunctionName = "open",
                FunctionParameter = new { kid = kongID, mode = chestMode }
            };
            var statusTask = await PlayFabClientAPI.ExecuteCloudScriptAsync(request);
            if (statusTask.Error != null)
            {
                logError(statusTask.Error.Error.ToString(), statusTask.Error.ErrorMessage);
                chestResult = 801;
                return true;
            }
            if (statusTask == null || statusTask.Result.FunctionResult == null || !statusTask.Result.FunctionResult.ToString().Contains("true"))
            {
                logError("Cloud Script Error: Open chest", statusTask);
                _ = JObject.Parse(statusTask.Result.FunctionResult.ToString());
                chestResult = 800;
                return true;
            }
            else
            {
                JObject json = JObject.Parse(statusTask.Result.FunctionResult.ToString());
                chestResult = int.Parse(json["result"].ToString());
                freeChestAvailable = false;
                return true;
            }
        }

        public async Task<bool> sendDQSolution(int[] DQLineup)
        {
            await Task.Delay(500);
            var request = new ExecuteCloudScriptRequest()
            {
                RevisionSelection = CloudScriptRevisionOption.Latest,
                FunctionName = "pved",
                FunctionParameter = new { setup = DQLineup, kid = kongID, max = true }
            };
            var statusTask = await PlayFabClientAPI.ExecuteCloudScriptAsync(request);
            await Task.Delay(2000);
            if (statusTask.Error != null)
            {
                logError(statusTask.Error.Error.ToString(), statusTask.Error.ErrorMessage);
                return false;
            }
            if (statusTask.Result == null || statusTask.Result.FunctionResult == null || !statusTask.Result.FunctionResult.ToString().Contains("true"))
            {
                logError("Cloud Script Error: Send DQ", JsonConvert.SerializeObject(statusTask));
                return false;
            }
            else
            {
                JObject json = JObject.Parse(statusTask.Result.FunctionResult.ToString());
                DQLevel = json["data"]["city"]["daily"]["lvl"].ToString();
                DQResult = true;
                return true;
            }
        }

        public async Task<bool> sendDungSolution(int[] DungLineup)
        {
            await Task.Delay(1000);
            var request = new ExecuteCloudScriptRequest()
            {
                RevisionSelection = CloudScriptRevisionOption.Latest,
                FunctionName = "dungeon",
                FunctionParameter = new { setup = DungLineup, max = true }
            };
            var statusTask = await PlayFabClientAPI.ExecuteCloudScriptAsync(request);
            if (statusTask.Error != null)
            {
                logError(statusTask.Error.Error.ToString(), statusTask.Error.ErrorMessage);
                return false;
            }
            if (statusTask == null || statusTask.Result.FunctionResult == null || !statusTask.Result.FunctionResult.ToString().Contains("true"))
            {
                logError("Cloud Script Error: Send Dung", statusTask);
                return false;
            }
            else
            {
                //JObject json = JObject.Parse(statusTask.Result.FunctionResult.ToString());
                //DQLevel = json["data"]["city"]["daily"]["lvl"].ToString();
                //DQResult = true;
                return true;
            }
        }

        public async Task<bool> sendT1Register()
        {
            await Task.Delay(1000);
            long Timestamp = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
            int tid = (int)Math.Floor(Timestamp / 864e2);
            var responseString = "";
            using (var client = new HttpClient())
            {
                Dictionary<int, int> a = new Dictionary<int, int>
                {
                    { 0, userID },
                    { 1, tid }
                };
                var values = new Dictionary<string, string> { { "gtn1", JsonConvert.SerializeObject(a) } };
                var content = new FormUrlEncodedContent(values);
                var response = await client.PostAsync("http://dcouv.fr/cq.php", content);
                responseString = await response.Content.ReadAsStringAsync();
                if (responseString.Length < 5 || responseString.Length > 400)
                    return false;
            }
            //logError("sendT1Register for user " + userID.ToString(), responseString);
            var request = new ExecuteCloudScriptRequest()
            {
                RevisionSelection = CloudScriptRevisionOption.Latest,
                FunctionName = "register",
                FunctionParameter = new { setup = getArray(responseString), kid = kongID, tid }
            };
            var statusTask = await PlayFabClientAPI.ExecuteCloudScriptAsync(request);
            if (statusTask.Error != null)
            {
                logError(statusTask.Error.Error.ToString(), statusTask.Error.ErrorMessage);
                return false;
            }
            if (statusTask == null || statusTask.Result.FunctionResult == null)
            {
                return false;
            }
            else
            {
                await Task.Delay(3000);
                return true;
            }
        }

        public async Task<bool> sendT2Register()
        {
            await Task.Delay(1000);
            var responseString = "";
            using (var client = new HttpClient())
            {
                Dictionary<int, int> a = new Dictionary<int, int>
                {
                    { 0, userID }
                };
                var values = new Dictionary<string, string> { { "gtn2", JsonConvert.SerializeObject(a) } };
                var content = new FormUrlEncodedContent(values);
                var response = await client.PostAsync("http://dcouv.fr/cq.php", content);
                responseString = await response.Content.ReadAsStringAsync();
                if (responseString.Length < 5 || responseString.Length > 400)
                    return false;
            }
            //logError("sendT2Register for user " + userID.ToString(), responseString);
            var request = new ExecuteCloudScriptRequest()
            {
                RevisionSelection = CloudScriptRevisionOption.Latest,
                FunctionName = "etregister",
                FunctionParameter = new { setup = getArray(responseString), kid = kongID }
            };
            var statusTask = await PlayFabClientAPI.ExecuteCloudScriptAsync(request);
            if (statusTask.Error != null)
            {
                logError(statusTask.Error.Error.ToString(), statusTask.Error.ErrorMessage);
                return false;
            }
            if (statusTask == null || statusTask.Result.FunctionResult == null)
            {
                return false;
            }
            else
            {
                await Task.Delay(3000);
                return true;
            }
        }

        public async Task<bool> sendFlashRegister(int tid)
        {
            await Task.Delay(1000);
            var responseString = "";
            using (var client = new HttpClient())
            {
                Dictionary<int, int> a = new Dictionary<int, int>
                {
                    { 0, userID },
                    { 1, tid },
                    { 2, int.Parse(PFStuff.FlashCurrent["players"].ToString()) }
                };
                var values = new Dictionary<string, string> { { "gfla", JsonConvert.SerializeObject(a) } };
                var content = new FormUrlEncodedContent(values);
                var response = await client.PostAsync("http://dcouv.fr/cq.php", content);
                responseString = await response.Content.ReadAsStringAsync();
                if (responseString.Length < 5 || responseString.Length > 400)
                    return false;
            }
            //logError("Sending Flash lineup", responseString + " --- " + JsonConvert.SerializeObject(FlashCurrent));
            var request = new ExecuteCloudScriptRequest()
            {
                RevisionSelection = CloudScriptRevisionOption.Latest,
                FunctionName = "fregister",
                FunctionParameter = new { setup = getArray(responseString), kid = kongID, tid }
            };
            var statusTask = await PlayFabClientAPI.ExecuteCloudScriptAsync(request);
            if (statusTask.Error != null)
            {
                logError(statusTask.Error.Error.ToString(), statusTask.Error.ErrorMessage);
                return false;
            }
            if (statusTask == null || statusTask.Result.FunctionResult == null)
            {
                return false;
            }
            else
            {
                await Task.Delay(4000);
                return true;
            }
        }

        public async Task<int> sendLFPick(int cell)
        {
            var request = new ExecuteCloudScriptRequest()
            {
                RevisionSelection = CloudScriptRevisionOption.Latest,
                FunctionName = "sfcell",
                FunctionParameter = new { cell }
            };
            using (StreamWriter sw = new StreamWriter("ActionLog.txt", true))
            {
                sw.WriteLine(DateTime.Now + "\n\t Picking LF cell " + cell.ToString());
            }
            var statusTask = await PlayFabClientAPI.ExecuteCloudScriptAsync(request);
            if (statusTask.Error != null)
            {
                logError(statusTask.Error.Error.ToString(), statusTask.Error.ErrorMessage);
                return 0;
            }
            if (statusTask == null || statusTask.Result.FunctionResult == null || !statusTask.Result.FunctionResult.ToString().Contains("true"))
            {
                logError("Cloud Script Error: Send LF", statusTask);
                return 0;
            }
            else
            {
                JObject json = JObject.Parse(statusTask.Result.FunctionResult.ToString());
                if ((bool)json["ok"] == true)
                {
                    return (int)json["followers"];
                }
                return 0;
            }
        }

        public async Task<bool> sendKeysTowerPick()
        {
            int pick = 0;
            var request = new ExecuteCloudScriptRequest()
            {
                RevisionSelection = CloudScriptRevisionOption.Latest,
                FunctionName = "keyevent",
                FunctionParameter = new { pick }
            };
            var statusTask = await PlayFabClientAPI.ExecuteCloudScriptAsync(request);
            if (statusTask.Error != null)
            {
                logError(statusTask.Error.Error.ToString(), statusTask.Error.ErrorMessage);
                return false;
            }
            if (statusTask == null || statusTask.Result.FunctionResult == null || !statusTask.Result.FunctionResult.ToString().Contains("true"))
            {
                logError("Cloud Script Error: Send KeysTower", statusTask);
                return false;
            }
            else
            {
                return true;
            }
        }

        public async Task<bool> sendCCScore(int score)
        {
            var request = new ExecuteCloudScriptRequest()
            {
                RevisionSelection = CloudScriptRevisionOption.Latest,
                FunctionName = "cc3v3nt",
                FunctionParameter = new { coins = score, kid = kongID }
            };
            using (StreamWriter sw = new StreamWriter("ActionLog.txt", true))
            {
                sw.WriteLine(DateTime.Now + "\n\t Sending CC catcher score " + score.ToString());
            }
            var statusTask = await PlayFabClientAPI.ExecuteCloudScriptAsync(request);
            if (statusTask.Error != null)
            {
                logError(statusTask.Error.Error.ToString(), statusTask.Error.ErrorMessage);
                return false;
            }
            if (statusTask == null || statusTask.Result.FunctionResult == null)// || !statusTask.Result.FunctionResult.ToString().Contains("true"))
            {
                //logError("Cloud Script Error: Send CC catcher score", statusTask);
                return false;
            }
            else
            {
                return true;
            }
        }

        public async Task<bool> sendPGPick(int cell)
        {
            var request = new ExecuteCloudScriptRequest()
            {
                RevisionSelection = CloudScriptRevisionOption.Latest,
                FunctionName = "pge",
                FunctionParameter = new { card = cell, kid = kongID }
            };
            using (StreamWriter sw = new StreamWriter("ActionLog.txt", true))
            {
                sw.WriteLine(DateTime.Now + "\n\t Picking PG cell " + cell.ToString());
            }
            var statusTask = await PlayFabClientAPI.ExecuteCloudScriptAsync(request);
            if (statusTask.Error != null)
            {
                logError(statusTask.Error.Error.ToString(), statusTask.Error.ErrorMessage);
                return false;
            }
            if (statusTask == null || statusTask.Result.FunctionResult == null || !statusTask.Result.FunctionResult.ToString().Contains("true"))
            {
                //logError("Cloud Script Error: Send PGEvent", statusTask);
                return false;
            }
            else
            {
                JObject json = JObject.Parse(statusTask.Result.FunctionResult.ToString());
                PGDeck = getArray(json["data"]["city"]["pge"]["cards"].ToString());
                PGPicked = getArray(json["data"]["city"]["pge"]["picks"].ToString());
                return true;
            }
        }

        public async Task<bool> sendAdventure(int kind, int pct)
        {
            var request = new ExecuteCloudScriptRequest()
            {
                RevisionSelection = CloudScriptRevisionOption.Latest,
                FunctionName = "adventure",
                FunctionParameter = new { kind, percentage = pct }
            };
            using (StreamWriter sw = new StreamWriter("ActionLog.txt", true))
            {
                sw.WriteLine(DateTime.Now + "\n\t Sending Adventure (" + kind.ToString() + ", " + pct.ToString() + "%)");
            }
            var statusTask = await PlayFabClientAPI.ExecuteCloudScriptAsync(request);
            if (statusTask.Error != null)
            {
                logError(statusTask.Error.Error.ToString(), statusTask.Error.ErrorMessage);
                return false;
            }
            if (statusTask == null || statusTask.Result.FunctionResult == null)// || !statusTask.Result.FunctionResult.ToString().Contains("true"))
            {
                //logError("Cloud Script Error: Send Adventure", statusTask);
                return false;
            }
            else
            {
                return true;
            }
        }

        public async Task<bool> sendLottery(int qty)
        {
            var request = new ExecuteCloudScriptRequest()
            {
                RevisionSelection = CloudScriptRevisionOption.Latest,
                FunctionName = "buylot",
                FunctionParameter = new { qty }
            };
            using (StreamWriter sw = new StreamWriter("ActionLog.txt", true))
            {
                sw.WriteLine(DateTime.Now + "\n\t Buying Lottery Ticket, qty = " + qty.ToString());
            }
            var statusTask = await PlayFabClientAPI.ExecuteCloudScriptAsync(request);
            if (statusTask.Error != null)
            {
                logError(statusTask.Error.Error.ToString(), statusTask.Error.ErrorMessage);
                return false;
            }
            if (statusTask == null || statusTask.Result.FunctionResult == null)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        public async Task<bool> sendCoupon(string coupon)
        {
            var request = new ExecuteCloudScriptRequest()
            {
                RevisionSelection = CloudScriptRevisionOption.Latest,
                FunctionName = "coupon",
                FunctionParameter = new { code = coupon }
            };
            using (StreamWriter sw = new StreamWriter("ActionLog.txt", true))
            {
                sw.WriteLine(DateTime.Now + "\n\t Sending coupon " + coupon);
            }
            var statusTask = await PlayFabClientAPI.ExecuteCloudScriptAsync(request);
            if (statusTask.Error != null)
            {
                logError(statusTask.Error.Error.ToString(), statusTask.Error.ErrorMessage);
                return false;
            }
            if (statusTask == null || statusTask.Result.FunctionResult == null || !statusTask.Result.FunctionResult.ToString().Contains("true"))
            {
                //logError("Cloud Script Error: Send coupon (maybe coupon already used)", statusTask);
                return false;
            }
            else
            {
                JObject json = JObject.Parse(statusTask.Result.FunctionResult.ToString());
                using (StreamWriter sw = new StreamWriter("ActionLog.txt", true))
                {
                    sw.WriteLine(DateTime.Now + "\n\t Coupon gave " + json["prize"].ToString());
                }
                return true;
            }
        }

        public async Task<bool> sendBid(int bidHeroID, int bidPrice)
        {
            var request = new ExecuteCloudScriptRequest()
            {
                RevisionSelection = CloudScriptRevisionOption.Latest,
                FunctionName = "auction",
                FunctionParameter = new { hid = bidHeroID, kid = kongID, name = username, bid = bidPrice }
            };
            var statusTask = await PlayFabClientAPI.ExecuteCloudScriptAsync(request);
            if (statusTask.Error != null)
            {
                logError("sendBid --- " + statusTask.Error.Error.ToString(), statusTask.Error.ErrorMessage);
                /*using (StreamWriter sw = new StreamWriter("ActionLog.txt", true))
                {
                    sw.WriteLine(DateTime.Now);
                    sw.WriteLine("\tFAILED Bid on hero " + (Constants.heroNames.Length > bidHeroID + 2 ? Constants.heroNames[bidHeroID + 2] : ("Unknown, ID: " + bidHeroID))
                        + " for: " + bidPrice + "UM.");
                    sw.WriteLine(statusTask.Error.ErrorMessage);
                }*/
                return false;
            }
            if (statusTask == null || statusTask.Result.FunctionResult == null || !statusTask.Result.FunctionResult.ToString().Contains("true"))
            {
                //logError("Cloud Script Error: bid", JsonConvert.SerializeObject(statusTask));
                /*using (StreamWriter sw = new StreamWriter("ActionLog.txt", true))
                {
                    sw.WriteLine(DateTime.Now);
                    sw.WriteLine("\tFAILED Bid on hero " + (Constants.heroNames.Length > bidHeroID + 2 ? Constants.heroNames[bidHeroID + 2] : ("Unknown, ID: " + bidHeroID))
                        + " for: " + bidPrice + "UM.");
                    sw.WriteLine(statusTask.Result.FunctionResult.ToString());
                }*/
                return true;
            }
            else
            {
                using (StreamWriter sw = new StreamWriter("ActionLog.txt", true))
                {
                    sw.WriteLine(DateTime.Now + "\tBid on hero " + (Constants.heroNames.Length > bidHeroID + 2 ? Constants.heroNames[bidHeroID + 2] : ("Unknown, ID: " + bidHeroID)) + " for: " + bidPrice + "UM.");
                }
                await Task.Delay(2000);
                await getCurrencies();
                return true;
            }
        }

        public async Task<bool> sendBuyLTO(int heroID, int maxPrice)
        {
            using var client = new HttpClient();
            int ltoID = 0;
            try
            {
                var values = new Dictionary<string, string> { { "oget", heroID.ToString() }, { "oget2", maxPrice.ToString() } };
                var cont = new FormUrlEncodedContent(values);
                var resp = await client.PostAsync("http://dcouv.fr/cq.php", cont);
                var respString = await resp.Content.ReadAsStringAsync();
                ltoID = int.Parse(respString);
            }
            catch
            {
            }
            if (ltoID > 0)
            {
                var request = new ExecuteCloudScriptRequest()
                {
                    RevisionSelection = CloudScriptRevisionOption.Latest,
                    FunctionName = "lto",
                    FunctionParameter = new { offer = ltoID }
                };
                var statusTask = await PlayFabClientAPI.ExecuteCloudScriptAsync(request);
                if (statusTask.Error != null)
                {
                    logError(statusTask.Error.Error.ToString(), statusTask.Error.ErrorMessage);
                    using (StreamWriter sw = new StreamWriter("ActionLog.txt", true))
                    {
                        sw.WriteLine(DateTime.Now);
                        sw.WriteLine("\tFAILED LTO purchase");
                        sw.WriteLine(statusTask.Error.ErrorMessage);
                    }
                    return false;
                }
                if (statusTask == null || statusTask.Result.FunctionResult == null || !statusTask.Result.FunctionResult.ToString().Contains("true"))
                {
                    logError("Cloud Script Error: LTO purchase", statusTask);
                    using (StreamWriter sw = new StreamWriter("ActionLog.txt", true))
                    {
                        sw.WriteLine(DateTime.Now);
                        sw.WriteLine("\tFAILED LTO purchase");
                        sw.WriteLine(statusTask.Result.FunctionResult.ToString());
                    }
                    return true;
                }
                else
                {
                    using (StreamWriter sw = new StreamWriter("ActionLog.txt", true))
                    {
                        sw.WriteLine(DateTime.Now);
                        sw.WriteLine("\tLTO purchase OK");
                    }
                    return true;
                }
            }
            return false;
        }

        public async Task<bool> sendWBFight(int[] WBLineup)
        {
            var request = new ExecuteCloudScriptRequest()
            {
                RevisionSelection = CloudScriptRevisionOption.Latest,
                FunctionName = "fightWB",
                FunctionParameter = new { setup = WBLineup, kid = kongID, wbid = WB_ID }
            };
            var statusTask = await PlayFabClientAPI.ExecuteCloudScriptAsync(request);
            if (statusTask.Error != null)
            {
                logError(statusTask.Error.Error.ToString(), statusTask.Error.ErrorMessage);
                return false;
            }
            if (statusTask == null || statusTask.Result.FunctionResult == null || !statusTask.Result.FunctionResult.ToString().Contains("true"))
            {
                logError("Cloud Script Error: send WB", statusTask);
                JObject json = JObject.Parse(statusTask.Result.FunctionResult.ToString());
                if (json["err"].ToString() == "No attacks left")
                    PFStuff.wbAttacksAvailable = 0;
                return true;
            }
            else
            {
                return true;
            }
        }

        public async Task<bool> sendLevelUp(int heroID, string mode)
        {
            var request = new ExecuteCloudScriptRequest()
            {
                RevisionSelection = CloudScriptRevisionOption.Latest,
                FunctionName = "levelUp",
                FunctionParameter = new { id = heroID, mode }
            };
            var statusTask = await PlayFabClientAPI.ExecuteCloudScriptAsync(request);
            if (statusTask.Error != null)
            {
                logError(statusTask.Error.Error.ToString(), statusTask.Error.ErrorMessage);
                return false;
            }
            if (statusTask == null || statusTask.Result.FunctionResult == null || !statusTask.Result.FunctionResult.ToString().Contains("true"))
            {
                logError("Cloud Script Error: level up", statusTask);
                return false;
            }
            else
            {
                using (StreamWriter sw = new StreamWriter("ActionLog.txt", true))
                {
                    sw.WriteLine(DateTime.Now);
                    sw.WriteLine("\tLeveled up hero: " + (Constants.heroNames.Length > heroID + 2 ? Constants.heroNames[heroID + 2] : ("Unknown, ID: " + heroID))
                        + " with: " + mode);
                }
                return true;
            }
        }
        
        public async Task<bool> sendLevelUp10(int heroID, string mode)
        {
            var request = new ExecuteCloudScriptRequest()
            {
                RevisionSelection = CloudScriptRevisionOption.Latest,
                FunctionName = "levelUp10",
                FunctionParameter = new { id = heroID, mode }
            };
            var statusTask = await PlayFabClientAPI.ExecuteCloudScriptAsync(request);
            if (statusTask.Error != null)
            {
                logError(statusTask.Error.Error.ToString(), statusTask.Error.ErrorMessage);
                return false;
            }
            if (statusTask == null || statusTask.Result.FunctionResult == null || !statusTask.Result.FunctionResult.ToString().Contains("true"))
            {
                logError("Cloud Script Error: level up10", statusTask);
                return false;
            }
            else
            {
                using (StreamWriter sw = new StreamWriter("ActionLog.txt", true))
                {
                    sw.WriteLine(DateTime.Now);
                    sw.WriteLine("\tLeveled up hero: " + (Constants.heroNames.Length > heroID + 2 ? Constants.heroNames[heroID + 2] : ("Unknown, ID: " + heroID))
                        + " 10 times with: " + mode);
                }
                return true;
            }
        }

        public async Task<bool> sendLevelSuper(int heroID, string mode)
        {
            var request = new ExecuteCloudScriptRequest()
            {
                RevisionSelection = CloudScriptRevisionOption.Latest,
                FunctionName = "levelSuper",
                FunctionParameter = new { id = heroID, mode }
            };
            var statusTask = await PlayFabClientAPI.ExecuteCloudScriptAsync(request);
            if (statusTask.Error != null)
            {
                logError(statusTask.Error.Error.ToString(), statusTask.Error.ErrorMessage);
                return false;
            }
            if (statusTask == null || statusTask.Result.FunctionResult == null || !statusTask.Result.FunctionResult.ToString().Contains("true"))
            {
                logError("Cloud Script Error: level super", statusTask);
                return false;
            }
            else
            {
                using (StreamWriter sw = new StreamWriter("ActionLog.txt", true))
                {
                    sw.WriteLine(DateTime.Now);
                    sw.WriteLine("\tLeveled up hero: " + (Constants.heroNames.Length > heroID + 2 ? Constants.heroNames[heroID + 2] : ("Unknown, ID: " + heroID))
                        + " with: " + mode);
                }
                return true;
            }
        }

        public async Task<bool> sendAscendHero(int heroID)
        {
            var request = new ExecuteCloudScriptRequest()
            {
                RevisionSelection = CloudScriptRevisionOption.Latest,
                FunctionName = "ascendHero",
                FunctionParameter = new { id = heroID }
            };
            var statusTask = await PlayFabClientAPI.ExecuteCloudScriptAsync(request);
            if (statusTask.Error != null)
            {
                logError(statusTask.Error.Error.ToString(), statusTask.Error.ErrorMessage);
                return false;
            }
            if (statusTask == null || statusTask.Result.FunctionResult == null || !statusTask.Result.FunctionResult.ToString().Contains("true"))
            {
                logError("Cloud Script Error: ascend hero", statusTask);
                return true;
            }
            else
            {
                using (StreamWriter sw = new StreamWriter("ActionLog.txt", true))
                {
                    sw.WriteLine(DateTime.Now);
                    sw.WriteLine("\tAscended hero: " + (Constants.heroNames.Length > heroID + 2 ? Constants.heroNames[heroID + 2] : ("Unknown, ID: " + heroID)));
                }
                return true;
            }
        }

        public async Task<bool> sendTrainHero(int heroID)
        {
            var request = new ExecuteCloudScriptRequest()
            {
                RevisionSelection = CloudScriptRevisionOption.Latest,
                FunctionName = "training",
                FunctionParameter = new { hid = heroID, um = false }
            };
            var statusTask = await PlayFabClientAPI.ExecuteCloudScriptAsync(request);
            if (statusTask.Error != null)
            {
                logError(statusTask.Error.Error.ToString(), statusTask.Error.ErrorMessage);
                return false;
            }
            if (statusTask == null || statusTask.Result.FunctionResult == null || !statusTask.Result.FunctionResult.ToString().Contains("true"))
            {
                logError("Cloud Script Error: train hero " + (Constants.heroNames.Length > heroID + 2 ? heroID.ToString()+" "+Constants.heroNames[heroID + 2] : ("Unknown, ID: " + heroID)), statusTask);
                return true;
            }
            else
            {
                using (StreamWriter sw = new StreamWriter("ActionLog.txt", true))
                {
                    sw.WriteLine(DateTime.Now);
                    sw.WriteLine("\tTrain hero: " + (Constants.heroNames.Length > heroID + 2 ? Constants.heroNames[heroID + 2] : ("Unknown, ID: " + heroID)));
                }
                return true;
            }
        }

        public async Task<bool> sendConvert(bool mult10)
        {
            var request = new ExecuteCloudScriptRequest()
            {
                RevisionSelection = CloudScriptRevisionOption.Latest,
                FunctionName = "toPG",
                FunctionParameter = new { multiple = mult10 }
            };
            var statusTask = await PlayFabClientAPI.ExecuteCloudScriptAsync(request);
            if (statusTask.Error != null)
            {
                logError(statusTask.Error.Error.ToString(), statusTask.Error.ErrorMessage);
                return false;
            }
            if (statusTask == null || statusTask.Result.FunctionResult == null || !statusTask.Result.FunctionResult.ToString().Contains("true"))
            {
                logError("Cloud Script Error: convert", statusTask);
                return true;
            }
            else
            {
                using (StreamWriter sw = new StreamWriter("ActionLog.txt", true))
                {
                    sw.WriteLine(DateTime.Now);
                    sw.WriteLine("\tConverted " + (mult10 ? 1 : 10) + " AS to Prana");
                }
                pranaGems += mult10 ? 1 : 10;
                return true;
            }
        }

        public static async Task<bool> sendLog(string e)
        {
            try
            {
                if (userID == 0)
                    return true;
                var d = new Dictionary<string, string>
                {
                    { "p", userID.ToString() },
                    { "e", e },
                    { "v", Constants.version }
                };
                using var client = new HttpClient();
                var values = new Dictionary<string, string> { { "ierr", JsonConvert.SerializeObject(d) } };
                var content = new FormUrlEncodedContent(values);
                var response = await client.PostAsync("http://dcouv.fr/cq.php", content);
                var responseString = await response.Content.ReadAsStringAsync();
            }
            catch (Exception)
            {
            }
            return true;
        }
        #endregion

        #region Weekly Events
        public async Task<bool> sendSJClaim()
        {
            if (!isAdmin)
                return true;
            try
            {
                if (!await checkCaptcha())
                    return false;
                var request = new ExecuteCloudScriptRequest()
                {
                    RevisionSelection = CloudScriptRevisionOption.Latest,
                    FunctionName = "sjclaim"
                };
                var statusTask = await PlayFabClientAPI.ExecuteCloudScriptAsync(request);
                if (statusTask.Error != null)
                {
                    logError(statusTask.Error.Error.ToString(), statusTask.Error.ErrorMessage);
                    return false;
                }
                if (statusTask == null || statusTask.Result.FunctionResult == null)
                {
                    return false;
                }
                else
                {
                    JObject json = JObject.Parse(statusTask.Result.FunctionResult.ToString());
                    if (json["err"].ToString() != "")
                    {
                        LastCaptcha = "0";
                        await sendSJClaim();
                        return true;
                    }
                    SpaceStatus[0] = (int)json["data"]["city"]["space"]["current"]["mission"];
                    SpaceStatus[1] = (int)((double)json["data"]["city"]["space"]["current"]["timer"] / 1000);
                    SpaceStatus[7] = (int)json["data"]["city"]["space"]["gears"];
                    Thread.Sleep(500);
                    return true;
                }
            }
            catch
            {
                await Task.Delay(1000);
                return false;
            }
        }

        public async Task<bool> sendSJStart(int mission)
        {
            if (!isAdmin)
                return true;
            if (!await checkCaptcha())
                return false;
            var request = new ExecuteCloudScriptRequest()
            {
                RevisionSelection = CloudScriptRevisionOption.Latest,
                FunctionName = "sjmission",
                FunctionParameter = new { mission }
            };
            var statusTask = await PlayFabClientAPI.ExecuteCloudScriptAsync(request);
            if (statusTask.Error != null)
            {
                logError(statusTask.Error.Error.ToString(), statusTask.Error.ErrorMessage);
                return false;
            }
            if (statusTask == null || statusTask.Result.FunctionResult == null)
            {
                return false;
            }
            else
            {
                await GetGameData();
                return true;
            }
        }

        public async Task<bool> sendSJUpgrade(int upgrade)
        {
            if (!isAdmin)
                return true;
            if (!await checkCaptcha())
                return false;
            var request = new ExecuteCloudScriptRequest()
            {
                RevisionSelection = CloudScriptRevisionOption.Latest,
                FunctionName = "sjupgrade",
                FunctionParameter = new { upgrade }
            };
            var statusTask = await PlayFabClientAPI.ExecuteCloudScriptAsync(request);
            if (statusTask.Error != null)
            {
                logError(statusTask.Error.Error.ToString(), statusTask.Error.ErrorMessage);
                return false;
            }
            if (statusTask == null || statusTask.Result.FunctionResult == null)
            {
                return false;
            }
            else
            {
                await GetGameData();
                await Task.Delay(1000);
                return true;
            }
        }

        public async Task<bool> sendSJHloop()
        {
            if (!isAdmin)
                return true;
            try
            {
                var request = new ExecuteCloudScriptRequest()
                {
                    RevisionSelection = CloudScriptRevisionOption.Latest,
                    FunctionName = "sjHyperloop"
                };
                var statusTask = await PlayFabClientAPI.ExecuteCloudScriptAsync(request);
                if (statusTask.Error != null)
                {
                    logError(statusTask.Error.Error.ToString(), statusTask.Error.ErrorMessage);
                    return false;
                }
                if (statusTask == null || statusTask.Result.FunctionResult == null)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                logError("sendSJHloop ", ex.Message + " --- " + ex.StackTrace);
                return false;
            }
        }

        public async Task<bool> sendSJLeaderboard()
        {
            if (!isAdmin)
                return true;
            await Task.Delay(100);
            try
            {
                string[] res = await getWELeaderboard("spacejourney", 5);
                using (var client = new HttpClient())
                {
                    var values = new Dictionary<string, string> { { "ulsj", LZString.compressToEncodedURIComponent(JsonConvert.SerializeObject(res)) }, { "ulsj2", adminPassword } };
                    var encodedItems = values.Select(i => WebUtility.UrlEncode(i.Key) + "=" + WebUtility.UrlEncode(i.Value));
                    var content = new StringContent(String.Join("&", encodedItems), Encoding.UTF8, "application/x-www-form-urlencoded");
                    var response = await client.PostAsync("http://dcouv.fr/cq.php", content);
                    var responseString = await response.Content.ReadAsStringAsync();
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<string[]> getWELeaderboard(string ev, int size)
        {
            string[] res = new string[size];
            try
            {
                for (int i = 0; i < size; i++)
                {
                    var req = new GetLeaderboardRequest
                    {
                        StatisticName = ev,
                        StartPosition = 100*i,
                        MaxResultsCount = 100
                    };
                    var lbTask = await PlayFabClientAPI.GetLeaderboardAsync(req);
                    res[i] = JsonConvert.SerializeObject(lbTask);
                }
            }
            catch (Exception)
            {
            }
            return res;
        }

        public async Task<bool> sendGGAutoActivity()
        {
            try
            {
                var request = new ExecuteCloudScriptRequest()
                {
                    RevisionSelection = CloudScriptRevisionOption.Latest,
                    FunctionName = "ggautoclaim"
                };
                var statusTask = await PlayFabClientAPI.ExecuteCloudScriptAsync(request);
                if (statusTask.Error != null)
                {
                    logError(statusTask.Error.Error.ToString(), statusTask.Error.ErrorMessage);
                    return false;
                }
                if (statusTask == null || statusTask.Result.FunctionResult == null)
                {
                    return false;
                }
                else
                {
                    JObject json = JObject.Parse(statusTask.Result.FunctionResult.ToString());
                    GamesData = json["data"]["city"]["games"];
                    //logError("sendGGAutoActivity", JsonConvert.SerializeObject(GamesData));
                    Thread.Sleep(500);
                    return true;
                }
            }
            catch
            {
                await Task.Delay(500);
                return false;
            }
        }

        public async Task<bool> sendGGClaimActivity()
        {
            try
            {
                if (!await checkCaptcha())
                    return false;
                var request = new ExecuteCloudScriptRequest()
                {
                    RevisionSelection = CloudScriptRevisionOption.Latest,
                    FunctionName = "ggclaim"
                };
                var statusTask = await PlayFabClientAPI.ExecuteCloudScriptAsync(request);
                if (statusTask.Error != null)
                {
                    logError(statusTask.Error.Error.ToString(), statusTask.Error.ErrorMessage);
                    return false;
                }
                if (statusTask == null || statusTask.Result.FunctionResult == null)
                {
                    return true;
                }
                else
                {
                    JObject json = JObject.Parse(statusTask.Result.FunctionResult.ToString());
                    GamesData = json["data"]["city"]["games"];
                    Thread.Sleep(500);
                    return true;
                }
            }
            catch
            {
                await Task.Delay(1000);
                return false;
            }
        }

        public async Task<bool> sendGGUpgrade(int upgrade)
        {
            if (!isAdmin)
                return true;
            if (!await checkCaptcha())
                return false;
            var request = new ExecuteCloudScriptRequest()
            {
                RevisionSelection = CloudScriptRevisionOption.Latest,
                FunctionName = "ggupgrade",
                FunctionParameter = new { upgrade }
            };
            var statusTask = await PlayFabClientAPI.ExecuteCloudScriptAsync(request);
            if (statusTask.Error != null)
            {
                logError(statusTask.Error.Error.ToString(), statusTask.Error.ErrorMessage);
                return false;
            }
            if (statusTask == null || statusTask.Result.FunctionResult == null)
            {
                return false;
            }
            else
            {
                await GetGameData();
                await Task.Delay(1000);
                return true;
            }
        }

        public async Task<bool> sendGGStartActivity(int activity)
        {
            try
            {
                if(!await checkCaptcha())
                    return false;
                var request = new ExecuteCloudScriptRequest()
                {
                    RevisionSelection = CloudScriptRevisionOption.Latest,
                    FunctionName = "ggactivity",
                    FunctionParameter = new { activity }
                };
                var statusTask = await PlayFabClientAPI.ExecuteCloudScriptAsync(request);
                if (statusTask.Error != null)
                {
                    logError(statusTask.Error.Error.ToString(), statusTask.Error.ErrorMessage);
                    return false;
                }
                if (statusTask == null || statusTask.Result.FunctionResult == null)
                {
                    logError("ggactivity null", activity.ToString());
                    return false;
                }
                else
                {
                    JObject json = JObject.Parse(statusTask.Result.FunctionResult.ToString());
                    GamesData = json["data"]["city"]["games"];
                    //logError("sendGGStartActivity", "ok");
                    Thread.Sleep(1000);
                    return true;
                }
            }
            catch (Exception ex)
            {
                logError("gg start activity error ", ex.Message + " --- " + ex.StackTrace + " --- " + ex.Data);
                await Task.Delay(5000);
                return false;
            }
        }

        public async Task<bool> sendGGLeaderboard()
        {
            if (!isAdmin || doSometimes(90))
                return true;
            await Task.Delay(100);
            try
            {
                string[] res = await getWELeaderboard("games", 5);
                using (var client = new HttpClient())
                {
                    var values = new Dictionary<string, string> { { "ulgg", LZString.compressToEncodedURIComponent(JsonConvert.SerializeObject(res)) }, { "ulgg2", adminPassword } };
                    var encodedItems = values.Select(i => WebUtility.UrlEncode(i.Key) + "=" + WebUtility.UrlEncode(i.Value));
                    var content = new StringContent(String.Join("&", encodedItems), Encoding.UTF8, "application/x-www-form-urlencoded");
                    var response = await client.PostAsync("http://dcouv.fr/cq.php", content);
                    var responseString = await response.Content.ReadAsStringAsync();
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> checkCaptcha()
        {
            try
            {
                if (!isAdmin)
                    return false;
                long TimestampMilli = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
                if (TimestampMilli >= (long)Int64.Parse(LastCaptcha))
                {
                    int[] captchaRes = getArray(await createCaptcha());
                    return await validateCaptcha(captchaRes);
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<string> createCaptcha()
        {
            if (!isAdmin)
                return "";
            try
            {
                var request = new ExecuteCloudScriptRequest()
                {
                    RevisionSelection = CloudScriptRevisionOption.Latest,
                    FunctionName = "createCaptcha"
                };
                var statusTask = await PlayFabClientAPI.ExecuteCloudScriptAsync(request);
                if (statusTask.Error != null)
                {
                    logError(statusTask.Error.Error.ToString(), statusTask.Error.ErrorMessage);
                    return "";
                }
                if (statusTask == null || statusTask.Result.FunctionResult == null)
                {
                    return "";
                }
                else
                {
                    JObject json = JObject.Parse(statusTask.Result.FunctionResult.ToString());
                    int captcha = (int)json["id"];
                    var responseString = "";
                    using var client = new HttpClient();
                    var values = new Dictionary<string, string> { { "scap", captcha.ToString() }, { "scap2", adminPassword } };
                    var content = new FormUrlEncodedContent(values);
                    HttpResponseMessage httpResponseMessage = await client.PostAsync("http://dcouv.fr/cq.php", content);
                    responseString = await httpResponseMessage.Content.ReadAsStringAsync();
                    Random rnd = new Random();
                    await Task.Delay(700 + rnd.Next(100, 2000));
                    return responseString;
                }
            }
            catch
            {
                await Task.Delay(3000);
                return "";
            }
        }

        public async Task<bool> validateCaptcha(int[] solution)
        {
            if (!isAdmin)
                return true;
            try
            {
                var request = new ExecuteCloudScriptRequest()
                {
                    RevisionSelection = CloudScriptRevisionOption.Latest,
                    FunctionName = "validateCaptcha",
                    FunctionParameter = new { solution }
                };
                var statusTask = await PlayFabClientAPI.ExecuteCloudScriptAsync(request);
                if (statusTask.Error != null)
                {
                    logError(statusTask.Error.Error.ToString(), statusTask.Error.ErrorMessage);
                    return false;
                }
                if (statusTask == null || statusTask.Result.FunctionResult == null)
                {
                    return false;
                }
                else
                {
                    //logError("validateCaptcha ok ", "");
                    Random rnd = new Random();
                    await Task.Delay(100 + rnd.Next(50, 400));
                    return true;
                }
            }
            catch
            {
                await Task.Delay(3000);
                return false;
            }
        }
        #endregion
    }
}
