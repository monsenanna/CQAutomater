using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
using MySqlConnector;
using MySql.Data.MySqlClient;

namespace CQFollowerAutoclaimer
{
    class PFStuff
    {
        string token;
        static public string kongID;
        //static int requestsSent = 0;
        static public string miracleTimes;
        static public string followers;
        static public string DQTime;
        static public string DQLevel;
        static public bool DQResult;
        static public string PVPTime;
        static public string PVPCharges;
        static public int PVPLastUpdate = 0;
        static public JToken PVPGrid;
        static public int[] heroLevels;
        static public int emMultiplier;
        static public int FlashStatus;
        static public int EASDay;
        static public int DungStatus = 0;
        static public int DungRunning = 0;
        static public string lastDungLevel = "";
        static public string DungLevel;
        static public JToken LuckyFollowers;
        static public int[] LuckyFollowersLocal = new int[13];
        static public List<string> LuckyFollowersSent = new List<string>();
        static public JArray KeysTower;
        static public int CCDay;
        static public int CCDone;
        static public string PGCards;
        static public int[] PGDeck;
        static public int[] PGPicked;
        static public int PGWon;
        static public int AdventureDay;
        static public int AdventureStatus;
        static public int LotteryDay;
        static public int LotteryCurrent;

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

        TaskQueue logQueue = new TaskQueue();
        static public AppSettings ap = AppSettings.loadSettings();

        public PFStuff(string t, string kid)
        {
            token = t;
            kongID = kid;
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

        public Task<bool> addErrorToQueue(string err, string msg, DateTime dt)
        {
            try
            {
                using (StreamWriter sw = new StreamWriter(Constants.ErrorLog, true))
                {
                    sw.WriteLine(DateTime.Now);
                    sw.WriteLine("\tError " + err + " " + msg);
                    return Task.FromResult(true);
                }
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        public void logError(string err, string msg)
        {
            logQueue.Enqueue(() => addErrorToQueue(err, msg, DateTime.Now), "log");
        }

        private void logError(string err, PlayFabResult<ExecuteCloudScriptResult> result)
        {
            using (StreamWriter sw = new StreamWriter(Constants.ErrorLog, true))
            {
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
                return true;
            }
            return false;
        }

        public async Task<bool> GetGameData()
        {
            var request = new ExecuteCloudScriptRequest()
            {
                RevisionSelection = CloudScriptRevisionOption.Live,
                FunctionName = "status",
                FunctionParameter = new { token = token, kid = kongID }
            };
            var statusTask = await PlayFabClientAPI.ExecuteCloudScriptAsync(request);
            if (statusTask.Error != null)
            {
                logError(statusTask.Error.Error.ToString(), statusTask.Error.ErrorMessage);
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
                string hLevels = json["data"]["city"]["hero"].ToString();
                heroLevels = getArray(hLevels);
                miracleTimes = json["data"]["miracles"].ToString();
                followers = json["data"]["followers"].ToString();
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
                    if (int.Parse(json["data"]["city"]["cc"]["tid"].ToString()) < int.Parse(json["data"]["city"]["pge"]["tid"].ToString()) || CCDone < 180)
                        CCDone = 0;
                }
                catch
                {
                    CCDone = 0;
                }
                try
                {
                    PGCards = json["data"]["city"]["pge"]["attempts"].ToString();
                    if((bool)json["data"]["city"]["pge"]["done"] == true)
                        PGCards = "done";
                    PGDeck = getArray(json["data"]["city"]["pge"]["cards"].ToString());
                    PGPicked = getArray(json["data"]["city"]["pge"]["picks"].ToString());
                    PGWon = (int)json["data"]["city"]["pge"]["pg"];
                    if ((int)json["data"]["city"]["pge"]["tid"] < (int)json["data"]["city"]["tour"][0]["tid"]) // don't consider last week
                    {
                        PGCards = "8";
                        for (int i = 0; i < PGDeck.Length; i++)
                        {
                            PGDeck[i] = 0;
                            PGPicked[i] = -1;
                        }
                    }
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
                    if ((int)json["data"]["city"]["adventure"]["tid"] >= (int)json["data"]["city"]["tour"][0]["tid"] || json["data"]["city"]["adventure"]["time"] == null)
                    {
                        AdventureStatus = 1;
                    }
                }
                catch
                {
                }
                return true;
            }
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
                using (var connection = new MySqlConnection("Server=db.dcouv.fr;Port=22306;User ID=" + MySQLAuth.user + "; Password=" + MySQLAuth.pass + "; Database=cqdata"))
                {
                    connection.Open();
                    for (int i = json.Count() - 1; i >= 0; i--)
                    {
                        d = json[i]["date"].ToString();
                        d = d.Substring(0, d.Length - 3);
                        // write new data
                        int ownLane = 0;
                        for (int j = 0; j < 6; j++)
                        {
                            if (PVPGrid[j * 6].ToString() == json[i]["setup"][0].ToString())
                            {
                                ownLane = j + 1;
                                break;
                            }
                        }
                        // find player id
                        int enemyid = 0;
                        bool doInsert = false;
                        using (var command = new MySqlCommand("SELECT id FROM player WHERE name = '" + json[i]["enemy"].ToString() + "'", connection))
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.HasRows)
                                while (reader.Read())
                                {
                                    enemyid = (int)reader.GetValue(0);
                                }
                            else // unknown player
                            {
                                doInsert = true;
                            }
                        }
                        if (doInsert)
                        {
                            using (var command = new MySqlCommand("INSERT INTO player(name) VALUES ('" + json[i]["enemy"].ToString() + "');", connection))
                            {
                                command.ExecuteNonQuery();
                                enemyid = (int)command.LastInsertedId;
                            }
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
                            try
                            {
                                var values = new Dictionary<string, string> { { "ipvp", JsonConvert.SerializeObject(fightData) } };
                                var content = new FormUrlEncodedContent(values);
                                var response = await client.PostAsync("http://dcouv.fr/cq.php", content);
                                var responseString = await response.Content.ReadAsStringAsync();
                                //logError("debug - ", responseString);
                            }
                            catch
                            {
                                return true;
                            }
                        }
                        /*using (var command = new MySqlCommand("INSERT INTO pvp(date, pleft, pright, lleft, lright, result, data) VALUES ('" + d + "', " + userID + ", " + enemyid + ", " + ownLane.ToString() + ", 0, '" + json[i]["result"].ToString() + "', '" + fightData.ToString() + "');", connection))
                                command.ExecuteNonQuery();*/
                    }
                    connection.Close();
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

        public static int getHeroLevel(string heroName)
        {
            if (!string.IsNullOrEmpty(heroName))
            {
                int heroIndex = Array.IndexOf(Constants.heroNames, heroName) - 2;
                if (heroIndex != -1)
                {
                    return heroLevels[heroIndex];
                }
            }
            return 0;
        }

        public async Task<bool> getLeaderboard(int size)
        {
            await Task.Delay(500);
            nearbyPlayersIDs = new string[size];
            nearbyPlayersNames = new string[size];
            var request = new GetLeaderboardAroundPlayerRequest
            {
                StatisticName = "Ranking",
                MaxResultsCount = size
            };
            var leaderboardTask = await PlayFabClientAPI.GetLeaderboardAroundPlayerAsync(request);
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
                Dictionary<int, string> res = new Dictionary<int, string>();
                JArray json = JArray.Parse(hofTask.Result.Data["tour"]);
                for (int i = 0; i < json.Count(); i++)
                {
                    int tid = int.Parse(json[i]["tid"].ToString());
                    res.Add(tid, json[i]["top10"].ToString());
                }
                using (var client = new HttpClient())
                {
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
                freeChestAvailable = currenciesTask.Result.VirtualCurrency["BK"].ToString() == "1" ? true : false;
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

        internal static void getUsername(string id)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(@"http://api.kongregate.com/api/user_info.json?user_id=" + id);
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                string content = new StreamReader(response.GetResponseStream()).ReadToEnd();

                JObject json = JObject.Parse(content);
                username = json["username"].ToString();

                using (var connection = new MySqlConnection("Server=db.dcouv.fr;Port=22306;User ID=" + MySQLAuth.user + "; Password=" + MySQLAuth.pass + "; Database=cqdata"))
                {
                    connection.Open();
                    bool doInsert = false;
                    using (var command = new MySqlCommand("SELECT id FROM player WHERE name = '" + username + "'", connection))
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                            while (reader.Read())
                            {
                                userID = (int)reader.GetValue(0);
                            }
                        else // unknown player
                        {
                            doInsert = true;
                        }
                        if (doInsert)
                        {
                            using (var command2 = new MySqlCommand("INSERT INTO player(name) VALUES ('" + username + "');", connection))
                            {
                                command2.ExecuteNonQuery();
                                userID = (int)command2.LastInsertedId;
                            }
                        }
                    }
                    connection.Close();
                }
            }
            catch (Exception webex)
            {
                using (StreamWriter sw = new StreamWriter("ActionLog.txt", true))
                {
                    sw.WriteLine(DateTime.Now + "\n\t" + "Username Error : " + webex.Message);
                }
                username = null;
            }
        }

        internal static void getWebsiteData(string id)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(@"https://cosmosquest.net/public.php?kid=" + id);
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                string content = new StreamReader(response.GetResponseStream()).ReadToEnd();

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
                }
                else
                {
                    FlashStatus = -1;
                }
                if (json["super"] != null && Int64.Parse(json["super"].ToString()) == 1 && !WBName.Contains("SUPER"))
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
                if (json["adventure"] == null || (bool)json["adventure"] != true)
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
                //WBchanged = (WB_ID != int.Parse(a)) ? true : false;
                //if (requestsSent++ == 1)
                //{
                //    WBchanged = true;
                //}
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
                if (json["pge"] == null || (bool)json["pge"] != true)
                {
                    PGCards = "no";
                    PGDeck = null;
                    PGPicked = null;
                    PGWon = 0;
                }
            }
            // MB fix to prevent crashes
            catch (Exception webex)
            {
                using (StreamWriter sw = new StreamWriter("ActionLog.txt", true))
                {
                    sw.WriteLine(DateTime.Now + "\n\t" + "Error in PFStuff" + "\n\t" + webex.Message);
                }
            }
        }

        internal static async Task<int> getWBData(string id)
        {
            int retryCount = 4;
            while (retryCount > 0)
            {
                await Task.Delay(1000);
                try
                {
                    if (username == null)
                    {
                        getUsername(kongID);
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
            using (var client = new HttpClient())
            {
                try
                {
                    var values = new Dictionary<string, string> { { "eopp", userID.ToString() } };
                    var content = new FormUrlEncodedContent(values);
                    var response = await client.PostAsync("http://dcouv.fr/cq.php", content);
                    var responseString = await response.Content.ReadAsStringAsync();
                    logError("debug - ", responseString);
                    string[] result = responseString.Split(',').ToArray();
                    for (int i = 0; i < result.Length; i++)
                    {
                        if (nearbyPlayersNames.Contains(result[i]))
                        {
                            logError("debug i - ", i.ToString());
                            logError("debug resi - ", result[i]);
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
        }

        #endregion

        #region Sending requests
        public void sendBuyWC()
        {
            var request = new ExecuteCloudScriptRequest()
            {
                RevisionSelection = CloudScriptRevisionOption.Live,
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
                Thread.Sleep(1);
            }
            return;
        }

        public async Task<bool> sendClaimAll()
        {
            var request = new ExecuteCloudScriptRequest()
            {
                RevisionSelection = CloudScriptRevisionOption.Live,
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
                RevisionSelection = CloudScriptRevisionOption.Live,
                FunctionName = "fight",
                FunctionParameter = new { token = token, kid = kongID, id = nearbyPlayersIDs[index] }
            };
            var statusTask = await PlayFabClientAPI.ExecuteCloudScriptAsync(request);
            if (statusTask.Error != null)
            {
                battleResult = "";
                logError(statusTask.Error.Error.ToString(), statusTask.Error.ErrorMessage);
                return false;
            }
            if (statusTask == null || statusTask.Result.FunctionResult == null || !statusTask.Result.FunctionResult.ToString().Contains("true"))
            {
                battleResult = "";
                logError("Cloud Script Error: PvP Fight", statusTask);
                return true;
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
                RevisionSelection = CloudScriptRevisionOption.Live,
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
                JObject json = JObject.Parse(statusTask.Result.FunctionResult.ToString());
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
            var request = new ExecuteCloudScriptRequest()
            {
                RevisionSelection = CloudScriptRevisionOption.Live,
                FunctionName = "pved",
                FunctionParameter = new { setup = DQLineup, kid = kongID, max = true }
            };
            var statusTask = await PlayFabClientAPI.ExecuteCloudScriptAsync(request);
            if (statusTask.Error != null)
            {
                logError(statusTask.Error.Error.ToString(), statusTask.Error.ErrorMessage);
                return false;
            }
            if (statusTask == null || statusTask.Result.FunctionResult == null || !statusTask.Result.FunctionResult.ToString().Contains("true"))
            {
                logError("Cloud Script Error: Send DQ", statusTask);
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
            var request = new ExecuteCloudScriptRequest()
            {
                RevisionSelection = CloudScriptRevisionOption.Live,
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

        public async Task<int> sendLFPick(int cell)
        {
            var request = new ExecuteCloudScriptRequest()
            {
                RevisionSelection = CloudScriptRevisionOption.Live,
                FunctionName = "sfcell",
                FunctionParameter = new { cell = cell }
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
            int pick = 0; // todo : random ?
            var request = new ExecuteCloudScriptRequest()
            {
                RevisionSelection = CloudScriptRevisionOption.Live,
                FunctionName = "keyevent",
                FunctionParameter = new { pick = pick }
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
                //JObject json = JObject.Parse(statusTask.Result.FunctionResult.ToString());
                return true;
            }
        }

        public async Task<bool> sendCCScore(int score)
        {
            var request = new ExecuteCloudScriptRequest()
            {
                RevisionSelection = CloudScriptRevisionOption.Live,
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
                RevisionSelection = CloudScriptRevisionOption.Live,
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
                RevisionSelection = CloudScriptRevisionOption.Live,
                FunctionName = "adventure",
                FunctionParameter = new { kind = kind, percentage = pct }
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

        public async Task<bool> sendLottery()
        {
            var request = new ExecuteCloudScriptRequest()
            {
                RevisionSelection = CloudScriptRevisionOption.Live,
                FunctionName = "buylot",
                FunctionParameter = new { }
            };
            using (StreamWriter sw = new StreamWriter("ActionLog.txt", true))
            {
                sw.WriteLine(DateTime.Now + "\n\t Buying Lottery Ticket");
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
                RevisionSelection = CloudScriptRevisionOption.Live,
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
                logError("Cloud Script Error: Send coupon (maybe coupon already used)", statusTask);
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
                RevisionSelection = CloudScriptRevisionOption.Live,
                FunctionName = "auction",
                FunctionParameter = new { hid = bidHeroID, kid = kongID, name = username, bid = bidPrice }
            };
            var statusTask = await PlayFabClientAPI.ExecuteCloudScriptAsync(request);
            if (statusTask.Error != null)
            {
                logError(statusTask.Error.Error.ToString(), statusTask.Error.ErrorMessage);
                using (StreamWriter sw = new StreamWriter("ActionLog.txt", true))
                {
                    sw.WriteLine(DateTime.Now);
                    sw.WriteLine("\tFAILED Bid on hero " + (Constants.heroNames.Length > bidHeroID + 2 ? Constants.heroNames[bidHeroID + 2] : ("Unknown, ID: " + bidHeroID))
                        + " for: " + bidPrice + "UM.");
                    sw.WriteLine(statusTask.Error.ErrorMessage);
                }
                return false;
            }
            if (statusTask == null || statusTask.Result.FunctionResult == null || !statusTask.Result.FunctionResult.ToString().Contains("true"))
            {
                logError("Cloud Script Error: bid", statusTask);
                using (StreamWriter sw = new StreamWriter("ActionLog.txt", true))
                {
                    sw.WriteLine(DateTime.Now);
                    sw.WriteLine("\tFAILED Bid on hero " + (Constants.heroNames.Length > bidHeroID + 2 ? Constants.heroNames[bidHeroID + 2] : ("Unknown, ID: " + bidHeroID))
                        + " for: " + bidPrice + "UM.");
                    sw.WriteLine(statusTask.Result.FunctionResult.ToString());
                }
                return true;
            }
            else
            {
                using (StreamWriter sw = new StreamWriter("ActionLog.txt", true))
                {
                    sw.WriteLine(DateTime.Now);
                    sw.WriteLine("\tBid on hero " + (Constants.heroNames.Length > bidHeroID + 2 ? Constants.heroNames[bidHeroID + 2] : ("Unknown, ID: " + bidHeroID))
                        + " for: " + bidPrice + "UM.");
                }
                return true;
            }
        }

        public async Task<bool> sendWBFight(int[] WBLineup)
        {
            var request = new ExecuteCloudScriptRequest()
            {
                RevisionSelection = CloudScriptRevisionOption.Live,
                FunctionName = "fightWB",
                FunctionParameter = new { setup = WBLineup, kid = kongID }
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
                RevisionSelection = CloudScriptRevisionOption.Live,
                FunctionName = "levelUp",
                FunctionParameter = new { id = heroID, mode = mode }
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
                return true;
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
                RevisionSelection = CloudScriptRevisionOption.Live,
                FunctionName = "levelUp10",
                FunctionParameter = new { id = heroID, mode = mode }
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
                return true;
            }
            else
            {
                using (StreamWriter sw = new StreamWriter("ActionLog.txt", true))
                {
                    sw.WriteLine(DateTime.Now);
                    sw.WriteLine("\tLeveled up hero: " + (Constants.heroNames.Length > heroID + 2 ? Constants.heroNames[heroID + 2] : ("Unknown, ID: " + heroID))
                        + "10 times with: " + mode);
                }
                return true;
            }
        }

        public async Task<bool> sendLevelSuper(int heroID, string mode)
        {
            var request = new ExecuteCloudScriptRequest()
            {
                RevisionSelection = CloudScriptRevisionOption.Live,
                FunctionName = "levelSuper",
                FunctionParameter = new { id = heroID, mode = mode }
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
                return true;
            }
            else
            {
                using (StreamWriter sw = new StreamWriter("ActionLog.txt", true))
                {
                    sw.WriteLine(DateTime.Now);
                    sw.WriteLine("\tLeveled up hero: " + (Constants.heroNames.Length > heroID + 2 ? Constants.heroNames[heroID + 2] : ("Unknown, ID: " + heroID))
                        + "with:" + mode);
                }
                return true;
            }
        }

        public async Task<bool> sendAscendHero(int heroID)
        {
            var request = new ExecuteCloudScriptRequest()
            {
                RevisionSelection = CloudScriptRevisionOption.Live,
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

        public async Task<bool> sendConvert(bool mult10)
        {
            var request = new ExecuteCloudScriptRequest()
            {
                RevisionSelection = CloudScriptRevisionOption.Live,
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
        
        #endregion
    }
}
