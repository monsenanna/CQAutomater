using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Net;
using System.IO;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using LinqToTwitter;

namespace CQFollowerAutoclaimer
{
    class AutoEvent
    {
        Form1 main;
        static int goodPicks = 0, badPicks = 0;
        public System.Timers.Timer EventTimer = new System.Timers.Timer();
        public System.Timers.Timer CouponTimer = new System.Timers.Timer();
        static ulong TweetID = 0;
        static string TweetCoupon = "";

        public AutoEvent(Form1 m)
        {
            main = m;
            EventTimer.Elapsed += EventTimer_Elapsed;
            CouponTimer.Elapsed += CouponTimer_Elapsed;
        }

        public void loadSettings()
        {
            AppSettings ap = AppSettings.loadSettings();
            main.autoEvCheckbox.Checked = ap.autoEvEnabled ?? false;
        }

        async void EventTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (!PlayFab.PlayFabClientAPI.IsClientLoggedIn())
            {
                await main.login();
            }
            if (main.autoEvCheckbox.Checked)
            {
                PFStuff.getWebsiteData(main.KongregateId);
                await main.pf.GetGameData();

                /* status :
                 * flash/eas/dung/lottery OK (just a notice, no action)
                 * keys/snake OK (all automated, report on GUI ; add to log ?)
                 * lf/pg IN PROGRESS (gotta test more)
                */
                main.label73.setText("Flash : " + (PFStuff.FlashStatus == 1 ? "active" : "not active") + " today");
                main.label109.setText("EAS : " + (PFStuff.EASDay == 1 ? "active" : "not active") + " today");
                main.label133.setText("Dungeon : " + (PFStuff.DungLevel != "-/-" ? "active" : "not active") + " today");
                main.label122.setText("Lucky Followers debug : not active today");
                if (PFStuff.LuckyFollowers != null)
                {
                    try
                    {
                        string requrl_base = "https://script.google.com/macros/s/AKfycbwhVd1nC3e70v-6wX3swoSUo1-mnaAiGgkoEQR9xcD6D4Z5l27M/exec?action=";
                        string requrl = requrl_base;
                        var l = PFStuff.LuckyFollowers["open"].ToArray().Length;
                        DateTime nextlf = Form1.getTime(PFStuff.LuckyFollowers["timeleft"].ToString());
                        int c1 = 0, c2 = 0;
                        main.label122.setText("Lucky Followers : waiting for timer");
                        if (nextlf < DateTime.Now) // find a solution if the time counter is over
                        {
                            EventTimer.Interval = 10 * 1000;
                            // ask gsheet
                            switch (l)
                            {
                                case 1: // let's ask for 2nd cell
                                    c1 = convertCellToLocal((int)PFStuff.LuckyFollowers["open"][0]);
                                    if (PFStuff.LuckyFollowersLocal[c1] == 0)
                                    {
                                        PFStuff.LuckyFollowersLocal[c1] = (int)PFStuff.LuckyFollowers["current"][(int)PFStuff.LuckyFollowers["open"][0]];
                                    }
                                    requrl += "autolf&p1=" + c1 + "&r1=" + PFStuff.LuckyFollowersLocal[c1] + "&p2=0";
                                    break;
                                case 2: // let's ask for 3rd cell
                                case 3:
                                    c1 = convertCellToLocal((int)PFStuff.LuckyFollowers["open"][0]);
                                    c2 = convertCellToLocal((int)PFStuff.LuckyFollowers["open"][1]);
                                    if (PFStuff.LuckyFollowersLocal[c1] == 0)
                                    {
                                        PFStuff.LuckyFollowersLocal[c1] = (int)PFStuff.LuckyFollowers["current"][(int)PFStuff.LuckyFollowers["open"][0]];
                                    }
                                    if (PFStuff.LuckyFollowersLocal[c2] == 0)
                                    {
                                        PFStuff.LuckyFollowersLocal[c2] = (int)PFStuff.LuckyFollowers["current"][(int)PFStuff.LuckyFollowers["open"][1]];
                                    }
                                    requrl += "autolf&p1=" + c1 + "&r1=" + PFStuff.LuckyFollowersLocal[c1] + "&p2=" + c2 + "&r2=" + PFStuff.LuckyFollowersLocal[c2] + "&p3=0";
                                    break;
                                case 0: // let's start
                                default:
                                    requrl += "autolf&p1=0";
                                    break;
                            }
                            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(@requrl);
                            request.MaximumAutomaticRedirections = 2;
                            request.AllowAutoRedirect = true;
                            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                            string content = new StreamReader(response.GetResponseStream()).ReadToEnd();
                            JObject json = JObject.Parse(content);
                            var c = json["data"];
                            int cellToPick;
                            // send pick
                            int res = 0;
                            try
                            {
                                cellToPick = (int)c;
                            }
                            catch // we received a "no" : pick something
                            {
                                do
                                {
                                    Random rnd = new Random();
                                    cellToPick = rnd.Next(0, 11);
                                } while (cellToPick == c1 || cellToPick == c2);
                            }
                            switch (l)
                            {
                                case 0: // we received first cell
                                    res = await main.pf.sendLFPick(convertCellFromLocal(cellToPick));
                                    PFStuff.LuckyFollowersLocal[cellToPick] = res;
                                    break;
                                case 1: // we received 2nd cell
                                    res = await main.pf.sendLFPick(convertCellFromLocal(cellToPick));
                                    PFStuff.LuckyFollowersLocal[cellToPick] = res;
                                    break;
                                case 2: // we received 3rd cell
                                    res = await main.pf.sendLFPick(convertCellFromLocal(cellToPick));
                                    PFStuff.LuckyFollowersLocal[cellToPick] = res;
                                    break;
                            }
                            main.label122.setText("Lucky Followers : pick #" + (l + 1).ToString() + " done, won " + res.ToString() + " followers");
                        }
                        if (l >= 2)
                        {
                            EventTimer.Interval = 30 * 1000;
                            // send new tpl
                            string values = "";
                            PFStuff.getWebsiteData(main.KongregateId);
                            for (int i = 0; i < 12; i++)
                            {
                                PFStuff.LuckyFollowersLocal[convertCellToLocal(i)] = (int)PFStuff.LuckyFollowers["current"][i];
                            }
                            for (int i = 1; i <= 12; i++)
                            {
                                if (i > 1)
                                    values += "-";
                                values += PFStuff.LuckyFollowersLocal[i];
                            }
                            if (!PFStuff.LuckyFollowersSent.Contains(values) && PFStuff.LuckyFollowersLocal[1] != -1)
                            {
                                requrl = requrl_base + "addtpl&v=" + values;
                                HttpWebRequest request2 = (HttpWebRequest)WebRequest.Create(@requrl);
                                HttpWebResponse response2 = (HttpWebResponse)request2.GetResponse();
                                string content2 = new StreamReader(response2.GetResponseStream()).ReadToEnd();
                                JObject json2 = JObject.Parse(content2);
                                string IsTplAdded = json2["data"].ToString();
                                if (IsTplAdded == "ok")
                                {
                                    using (StreamWriter sw = new StreamWriter("ActionLog.txt", true))
                                    {
                                        sw.WriteLine(DateTime.Now + "\n\tNew template added : " + values);
                                    }
                                    PFStuff.LuckyFollowersSent.Add(values);
                                    main.label122.setText("Lucky Followers : done, new template added (" + values + ")");
                                }
                            }
                        }
                    }
                    catch (Exception webex)
                    {
                        using (StreamWriter sw = new StreamWriter("ActionLog.txt", true))
                        {
                            sw.WriteLine(DateTime.Now + "\n\t" + webex.Message);
                        }
                    }
                }
                if (PFStuff.KeysTower != null)
                { // let's run KT
                    goodPicks = 0;
                    badPicks = 0;
                    foreach (JValue jo in PFStuff.KeysTower)
                    {
                        if (jo.ToString() == "0") goodPicks++;
                        else badPicks++;
                    }
                    main.label123.setText("Keys Tower : " + goodPicks + " good picks, " + badPicks + " bad picks today");
                    if (badPicks < 5)
                    {
                        await main.pf.sendKeysTowerPick();
                    }
                }
                if (PFStuff.CCDay != -1)
                {
                    main.label124.setText("CC Catcher : active today ; current score = " + PFStuff.CCDone);
                    if (PFStuff.CCDone == 0)
                    {
                        Random rnd = new Random();
                        int score = rnd.Next(180, 290);
                        await main.pf.sendCCScore(score);
                        main.label124.setText("CC Catcher : sent score " + score);
                    }
                }
                if (PFStuff.PGCards != null && PFStuff.PGCards != "no")
                { // let's run PGCards
                    var nbCells = PFStuff.PGDeck.Length;
                    bool stop = false;
                    using (StreamWriter sw = new StreamWriter("ActionLog.txt", true))
                    {
                        sw.WriteLine(DateTime.Now + "\n\t" + "Debug in PGCards, PGCards attempts remaining : " + PFStuff.PGCards);
                    }
                    // 1 : find 2 similar cards among non-picked ones
                    for (int i = 0; i < nbCells; i++)
                    {
                        if (!stop && PFStuff.PGPicked[i] == 0 && PFStuff.PGDeck[i] != -1)
                        {
                            for (int j = 0; j < nbCells; j++)
                            {
                                if (i != j && PFStuff.PGPicked[j] == 0 && PFStuff.PGDeck[j] != -1 && PFStuff.PGDeck[i] == PFStuff.PGDeck[j])
                                {
                                    // found ! let's pick both
                                    await main.pf.sendPGPick(i);
                                    await main.pf.sendPGPick(j);
                                    using (StreamWriter sw = new StreamWriter("ActionLog.txt", true))
                                    {
                                        sw.WriteLine(DateTime.Now + "\n\t" + "Debug in PGCards, pick " + i + " and " + j);
                                    }
                                    stop = true;
                                    break;
                                }
                            }
                        }
                    }
                    if (!stop)
                    {
                        // 2a : pick one card
                        int firstCard = 0;
                        for (int i = 0; i < nbCells; i++)
                        {
                            if (PFStuff.PGPicked[i] == 0)
                            {
                                firstCard = i;
                                await main.pf.sendPGPick(i);
                                break;
                            }
                        }
                        // 2b : check if similar card among non-picked ones
                        for (int j = 0; j < nbCells; j++)
                        {
                            if (firstCard != j && PFStuff.PGDeck[firstCard] == PFStuff.PGDeck[j])
                            {
                                // found ! let's pick 2nd card
                                await main.pf.sendPGPick(j);
                                stop = true;
                                break;
                            }
                        }
                        if (!stop)
                        {
                            // 2c : pick another card
                            for (int j = 0; j < nbCells; j++)
                            {
                                if (firstCard != j && PFStuff.PGPicked[j] == 0)
                                {
                                    await main.pf.sendPGPick(j);
                                    stop = true;
                                    break;
                                }
                            }
                        }
                    }
                    main.label125.setText("PG : " + String.Join(",", PFStuff.PGDeck.Select(p => p.ToString()).ToArray()));
                }
                else
                {
                    main.label125.setText("PG : not active today");
                }
                main.label126.setText("Lottery : " + (PFStuff.LotteryDay == 1 ? "active" : "not active") + " today");
                main.AEIndicator.BackColor = Color.Green;
            }
        }
        async void CouponTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (!PlayFab.PlayFabClientAPI.IsClientLoggedIn())
            {
                await main.login();
            }
            CouponTimer.Interval = 12 * 60 * 60 * 1000; // 12h
            var tweetList = GetTwitterFeeds();
            string firstTweet = tweetList.First().Text.ToString();
            main.label141.setText("id : " + tweetList.First().StatusID.ToString());
            if (tweetList.First().StatusID > TweetID) // there's a new tweet to parse !
            {
                TweetCoupon = firstTweet.Substring(firstTweet.Length - 10);
                TweetID = tweetList.First().StatusID;
                await main.pf.sendCoupon(TweetCoupon);
                main.label141.setText("Last coupon detected : " + TweetCoupon);
            }
        }

        public int convertCellToLocal(int cell)
        {
            switch (cell)
            {
                case 0: return 1;
                case 1: return 7;
                case 2: return 2;
                case 3: return 8;
                case 4: return 3;
                case 5: return 9;
                case 6: return 4;
                case 7: return 10;
                case 8: return 5;
                case 9: return 11;
                case 10: return 6;
                default: return 12;
            }
        }
        public int convertCellFromLocal(int cell)
        {
            switch (cell)
            {
                case 1: return 0;
                case 2: return 2;
                case 3: return 4;
                case 4: return 6;
                case 5: return 8;
                case 6: return 10;
                case 7: return 1;
                case 8: return 3;
                case 9: return 5;
                case 10: return 7;
                case 11: return 9;
                default: return 11;
            }
        }

        public static List<Status> GetTwitterFeeds()
        {
            string screenname = "Gaia_Byte";

            var auth = new SingleUserAuthorizer
            {
                CredentialStore = new InMemoryCredentialStore()
                {
                    ConsumerKey = Twitter.ConsumerKey,
                    ConsumerSecret = Twitter.ConsumerSecret,
                    OAuthToken = Twitter.OAuthToken,
                    OAuthTokenSecret = Twitter.OAuthTokenSecret
                }
            };
            var twitterCtx = new TwitterContext(auth);
            var ownTweets = new List<Status>();

            ulong maxId = 0;
            bool flag = true;
            var statusResponse = new List<Status>();
            statusResponse = (from tweet in twitterCtx.Status
                              where tweet.Type == StatusType.User
                              && tweet.ScreenName == screenname
                              && tweet.Count == 200
                              select tweet).ToList();

            if (statusResponse.Count > 0)
            {
                maxId = ulong.Parse(statusResponse.Last().StatusID.ToString()) - 1;
                ownTweets.AddRange(statusResponse);
            }
            do
            {
                int rateLimitStatus = twitterCtx.RateLimitRemaining;
                if (rateLimitStatus != 0)
                {
                    statusResponse = (from tweet in twitterCtx.Status
                                      where tweet.Type == StatusType.User
                                      && tweet.ScreenName == screenname
                                      && tweet.MaxID == maxId
                                      && tweet.Count == 200
                                      select tweet).ToList();

                    if (statusResponse.Count != 0)
                    {
                        maxId = ulong.Parse(statusResponse.Last().StatusID.ToString()) - 1;
                        ownTweets.AddRange(statusResponse);
                    }
                    else
                    {
                        flag = false;
                    }
                }
                else
                {
                    flag = false;
                }
            } while (flag);

            return ownTweets;
        }

    }
}
