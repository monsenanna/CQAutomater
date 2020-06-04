using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Net;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;

namespace CQFollowerAutoclaimer
{
    class AutoEvent
    {
        Form1 main;
        static int goodPicks = 0, badPicks = 0;
        public System.Timers.Timer EventTimer = new System.Timers.Timer();
        public System.Timers.Timer CouponTimer = new System.Timers.Timer();
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
            main.doAutoDGCheckbox.Checked = ap.doAutoDG ?? false;
            main.doAutoLFCheckbox.Checked = ap.doAutoLF ?? false;
            main.doAutoKTCheckbox.Checked = ap.doAutoKT ?? false;
            main.doAutoCCCheckbox.Checked = ap.doAutoCC ?? false;
            main.doAutoPGCheckbox.Checked = ap.doAutoPG ?? false;
            main.doAutoADCheckbox.Checked = ap.doAutoAD ?? false;
            main.adventurePriority.SelectedIndex = ap.optAutoAD ?? 0;
            main.doAutoLOCheckbox.Checked = ap.doAutoLO ?? false;
            main.lotteryCount.Value = ap.optAutoLO ?? 0;
        }

        async void EventTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (main.autoEvCheckbox.Checked)
            {
                if (!PlayFab.PlayFabClientAPI.IsClientLoggedIn())
                {
                    await main.login();
                }
                PFStuff.getWebsiteData(main.KongregateId);
                await main.pf.GetGameData();
                EventTimer.Interval = 60 * 1000; // 60sec

                main.label73.setText("Flash : " + (PFStuff.FlashStatus == 1 ? "active today, sending history" : "not active today"));
                main.label109.setText("EAS : " + (PFStuff.EASDay == 1 ? "active" : "not active") + " today");
                if (PFStuff.DungStatus == -1)
                {
                    main.label133.setText("Dungeon : not active today");
                }
                if (main.doAutoDGCheckbox.Checked && PFStuff.DungStatus == 0 && PFStuff.DungRunning == 0)
                {
                    try
                    {
                        PFStuff.lastDungLevel = PFStuff.DungLevel;
                        PFStuff.DungRunning = 1; // prevent parallel calcs
                        main.label133.setText("Dungeon : working");
                        if (!File.Exists("CQMacroCreator.exe") || !File.Exists("CosmosQuest.exe"))
                        {
                            main.autoDQ.fightWithPresetLineup(AutoDQ.CalcMode.DUNG);
                        }
                        else
                        {
                            main.autoDQ.RunCalc(AutoDQ.CalcMode.DUNG);
                        }
                    }
                    catch (Exception)
                    {
                        PFStuff.DungStatus = 1;
                        main.label133.setText("Dungeon : error (are calc and GUI present ?");
                    }
                }
                main.label122.setText("Lucky Followers : not active today");
                if (main.doAutoLFCheckbox.Checked && PFStuff.LuckyFollowers != null)
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
                            /*
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
                            */
                            // new code without gsheet
                            int cellToPick;
                            int res = 0;
                            do
                            {
                                Random rnd = new Random();
                                cellToPick = rnd.Next(0, 11);
                            } while (cellToPick == c1 || cellToPick == c2);
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
                            /*
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
                            */
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
                main.label123.setText("Keys Tower : not active today");
                if (main.doAutoKTCheckbox.Checked && PFStuff.KeysTower != null)
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
                main.label124.setText("CC Catcher : not active today");
                if (main.doAutoCCCheckbox.Checked && PFStuff.CCDay != -1)
                {
                    main.label124.setText("CC Catcher : active today ; current score = " + PFStuff.CCDone);
                    if (PFStuff.CCDone == 0)
                    {
                        Random rnd = new Random();
                        int score = rnd.Next(65, 105) * 3;
                        await main.pf.sendCCScore(score);
                        main.label124.setText("CC Catcher : sent score " + score);
                    }
                }
                if (main.doAutoPGCheckbox.Checked && PFStuff.PGCards != null && PFStuff.PGCards != "no" && PFStuff.PGCards != "done")
                { // let's run PGCards
                    var nbCells = PFStuff.PGDeck.Length;
                    bool stop = false;
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
                                    /*using (StreamWriter sw = new StreamWriter("ActionLog.txt", true))
                                    {
                                        sw.WriteLine(DateTime.Now + "\n\t" + "Debug in PGCards, pick " + i + " and " + j);
                                    }*/
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
                            if (PFStuff.PGDeck[i] == -1)
                            {
                                firstCard = i;
                                await main.pf.sendPGPick(i);
                                /*using (StreamWriter sw = new StreamWriter("ActionLog.txt", true))
                                {
                                    sw.WriteLine(DateTime.Now + "\n\t" + "Debug in PGCards, case 2a");
                                }*/
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
                                /*using (StreamWriter sw = new StreamWriter("ActionLog.txt", true))
                                {
                                    sw.WriteLine(DateTime.Now + "\n\t" + "Debug in PGCards, case 2b");
                                }*/
                                stop = true;
                                break;
                            }
                        }
                        if (!stop)
                        {
                            // 2c : pick another card
                            for (int j = 0; j < nbCells; j++)
                            {
                                if (firstCard != j && PFStuff.PGDeck[j] == -1)
                                {
                                    await main.pf.sendPGPick(j);
                                    /*using (StreamWriter sw = new StreamWriter("ActionLog.txt", true))
                                    {
                                        sw.WriteLine(DateTime.Now + "\n\t" + "Debug in PGCards, case 2c");
                                    }*/
                                    stop = true;
                                    break;
                                }
                            }
                        }
                    }
                    //main.label125.setText("PG match cards : " + String.Join(",", PFStuff.PGDeck.Select(p => p.ToString()).ToArray()));
                    main.label125.setText("PG match cards : CQA is playing, please wait");
                }
                else
                {
                        main.label125.setText("PG match cards : not active today");
                }
                main.label145.setText("Adventure : not active today");
                if (main.doAutoADCheckbox.Checked && PFStuff.AdventureDay == 1 && PFStuff.AdventureStatus == 1 && (main.pf.cosmicCoins > 100 || main.pf.pranaGems > 100 || main.pf.ascensionSpheres > 100))
                {
                    await main.pf.getCurrencies();
                    switch (main.adventurePriority.SelectedIndex)
                    {
                        case 2: // CC
                            if (main.pf.cosmicCoins > 100)
                            {
                                await main.pf.sendAdventure(0, 100);
                            }
                            else if (main.pf.ascensionSpheres > 100)
                            {
                                await main.pf.sendAdventure(2, 100);
                            }
                            else
                            {
                                await main.pf.sendAdventure(1, 100);
                            }
                            break;
                        case 1: // PG
                            if (main.pf.pranaGems > 100)
                            {
                                await main.pf.sendAdventure(1, 100);
                            }
                            else if (main.pf.ascensionSpheres > 100)
                            {
                                await main.pf.sendAdventure(2, 100);
                            }
                            else
                            {
                                await main.pf.sendAdventure(0, 100);
                            }
                            break;
                        default: // AS
                            if (main.pf.ascensionSpheres > 100)
                            {
                                await main.pf.sendAdventure(2, 100);
                            }
                            else if (main.pf.pranaGems > 100)
                            {
                                await main.pf.sendAdventure(1, 100);
                            }
                            else
                            {
                                await main.pf.sendAdventure(0, 100);
                            }
                            break;
                    }
                }
                if (PFStuff.AdventureDay == 1)
                {
                    main.label145.setText("Adventure : active today, under progress");
                }
                main.label126.setText("Lottery : " + (PFStuff.LotteryDay == 1 ? "active" : "not active") + " today");
                if (main.doAutoLOCheckbox.Checked && PFStuff.LotteryDay == 1)
                {
                    if (PFStuff.LotteryCurrent < main.lotteryCount.Value)
                    {
                        await main.pf.sendLottery();
                    }
                }
                main.AEIndicator.BackColor = Color.Green;
                //main.pf.logError("Event", "debug " + PFStuff.SpaceStatus[0].ToString());
                try
                {
                    if (PFStuff.SpaceStatus[0] != -2)
                    {
                        var Timestamp = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
                        // temp debug
                        main.pf.logError("SJ", "we shouldn't be running SJ, wtf ? " + JsonConvert.SerializeObject(PFStuff.SpaceStatus));
                        //int toUpg = PFStuff.SpaceStatus[2] <= PFStuff.SpaceStatus[3] && PFStuff.SpaceStatus[2] <= PFStuff.SpaceStatus[4] ? 0 : PFStuff.SpaceStatus[3] <= PFStuff.SpaceStatus[4] ? 1 : 2;
                        //int toUpg = PFStuff.SpaceStatus[3] <= PFStuff.SpaceStatus[2] && PFStuff.SpaceStatus[3] <= PFStuff.SpaceStatus[4] ? 1 : PFStuff.SpaceStatus[2] <= PFStuff.SpaceStatus[4] ? 0 : 2;
                        int toUpg = 1;
                        int cost = (int)(250 * Math.Pow(2, PFStuff.SpaceStatus[toUpg + 2]));
                        if (PFStuff.SpaceStatus[0] > -1 && PFStuff.SpaceStatus[1] != -1 && PFStuff.SpaceStatus[1] >= (int)Timestamp)
                        {
                            main.weeklyEventLabel.Text = "SJ running";
                            if (PFStuff.SpaceStatus[1] < (int)Timestamp + 90)
                            {
                                EventTimer.Interval = (PFStuff.SpaceStatus[1] + 5 - (int)Timestamp) * 1000; // reduce timer
                            }
                        }
                        else if (PFStuff.SpaceStatus[0] > -1 && PFStuff.SpaceStatus[1] != -1 && PFStuff.SpaceStatus[1] <= (int)Timestamp)
                        {
                            main.weeklyEventLabel.Text = "Let's go claim SJ";
                            EventTimer.Interval = 10000;
                            await main.pf.sendSJClaim();
                        }
                        if (PFStuff.SpaceStatus[0] == -1 || PFStuff.SpaceStatus[1] == -1)
                        {
                            main.weeklyEventLabel.Text = "Ready to start SJ ; gears : " + PFStuff.SpaceStatus[5].ToString() + "/" + cost.ToString();
                            if (PFStuff.SpaceStatus[5] >= cost)
                                await main.pf.sendSJUpgrade(toUpg);
                            EventTimer.Interval = 60 * 1000;
                            await main.pf.sendSJStart(0);
                            main.weeklyEventLabel.Text = "SJ started";
                        }
                    }
                }
                catch (Exception ex)
                {
                    main.pf.logError("SJ exception", ex.Message);
                }
            }
        }
        async void CouponTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (!PlayFab.PlayFabClientAPI.IsClientLoggedIn())
            {
                await main.login();
            }
            CouponTimer.Interval = 8 * 60 * 60 * 1000; // 8h
            try
            {
                using (var client = new HttpClient())
                {
                    var values = new Dictionary<string, string> { { "cget", "" } };
                    var cont = new FormUrlEncodedContent(values);
                    var resp = await client.PostAsync("http://dcouv.fr/cq.php", cont);
                    var respString = await resp.Content.ReadAsStringAsync();
                    if (TweetCoupon != respString)
                    {
                        TweetCoupon = respString;
                        await main.pf.sendCoupon(TweetCoupon);
                        main.label141.setText("Last coupon detected : " + TweetCoupon);
                    }
                }
            }
            catch(Exception ex)
            {
                main.pf.logError("Coupon", "Catched error " + ex.Message);
                main.label141.setText("Unable to send coupon");
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

    }
}
