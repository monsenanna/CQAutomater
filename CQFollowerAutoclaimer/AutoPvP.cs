using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Timers;

namespace CQFollowerAutoclaimer
{
    class AutoPvP
    {
        internal DateTime nextPVP;
        internal Timer PVPTimer = new Timer();
        public Timer TourTimer = new Timer();
        Form1 main;

        public AutoPvP(Form1 m)
        {
            main = m;
            PVPTimer.Elapsed += PVPTimer_Elapsed;
            TourTimer.Elapsed += TourTimer_Elapsed;
        }

        internal void loadPVPSettings()
        {
            main.autoPvPCheckbox.Checked = main.appSettings.autoPvPEnabled ?? false;
            main.doPvPHistoryCheckbox.Checked = main.appSettings.doPVPHistory ?? false;
            main.autoT1Checkbox.Checked = main.appSettings.autoT1Enabled ?? false;
            main.autoT2Checkbox.Checked = main.appSettings.autoT2Enabled ?? false;
            main.playersBelowCount.Value = main.appSettings.pvpLowerLimit ?? 4;
            main.playersAboveCount.Value = main.appSettings.pvpUpperLimit ?? 5;
        }

        internal async Task<Int32> pickOpponent(bool pickBest)
        {
            try
            {
                int size = Math.Max(5, 2 * (int)Math.Max(main.playersAboveCount.Value, main.playersBelowCount.Value + 1));
                while (!await main.pf.getLeaderboard(size)) ;
                main.pvpRankingSummary.setText("Ranking evolution : " + (PFStuff.initialRanking - PFStuff.currentRanking).ToString() + " (" + (PFStuff.initialRanking + 1) + " -> " + (PFStuff.currentRanking + 1) + ")");
                Random r = new Random();
                int index;
                // try finding easiest opponent
                index = await main.pf.getEasiestOpponent();
                if (!pickBest || index == 0)
                {
                    int cnt = 0;
                    // random among neighbors
                    do
                    {
                        index = r.Next(0, PFStuff.nearbyPlayersIDs.Length);
                        cnt++;
                    } while (cnt < 500 && (index == PFStuff.userIndex ||
                            index > PFStuff.userIndex + (int)main.playersBelowCount.Value ||
                            index < PFStuff.userIndex - (int)main.playersAboveCount.Value));
                    if (cnt >= 500)
                        return 0;
                }
                return index;
            }
            catch
            {
                return 0;
            }
        }

        async void PVPTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (main.autoPvPCheckbox.Checked)
            {
                PVPTimer.Stop();
                await Task.Delay(1000);
                if (!PlayFab.PlayFabClientAPI.IsClientLoggedIn())
                {
                    await main.login();
                    await Task.Delay(4000);
                }
                await main.getData();
                int fightsToDo = Int32.Parse(PFStuff.PVPCharges);
                if (fightsToDo > 0)
                {
                    int index = await pickOpponent(true);
                    main.taskQueue.Enqueue(() => sendFight(index), "PVP");
                }
                else
                {
                    nextPVP = Form1.getTime(PFStuff.PVPTime);
                    if (nextPVP < DateTime.Now)
                        nextPVP = nextPVP.AddMilliseconds(3605000);
                    main.PvPTimeLabel.setText(nextPVP.ToString());
                    PVPTimer.Interval = fightsToDo > 0 ? 905000 : Math.Max(30000, Math.Min(905000, (nextPVP - DateTime.Now).TotalMilliseconds));
                    PVPTimer.Start();
                }
            }
        }

        internal async Task<bool> sendFight(int index)
        {
            try
            {
                bool b = await main.pf.sendPVPFight(index);
                if (!b)
                { // remove from possible opponents
                    PFStuff.logError("PvP", "Fight impossible vs index " + PFStuff.nearbyPlayersIDs[index] + " (nearbyPlayersIDs = " + JsonConvert.SerializeObject(PFStuff.nearbyPlayersIDs) + ")");
                    if (PFStuff.nearbyPlayersIDs.Length < 3)
                    {
                        // rebuild leaderboard
                        await Task.Delay(5000);
                        int size = Math.Max(5, 2 * (int)Math.Max(main.playersAboveCount.Value + 4, main.playersBelowCount.Value + 5));
                        await main.pf.getLeaderboard(size);
                        await Task.Delay(3000);
                        return true;
                    }
                    List<string> list = new List<string>(PFStuff.nearbyPlayersIDs);
                    list.RemoveAt(index);
                    PFStuff.nearbyPlayersIDs = list.ToArray();
                    list = new List<string>(PFStuff.nearbyPlayersNames);
                    list.RemoveAt(index);
                    PFStuff.nearbyPlayersNames = list.ToArray();
                    return b;
                }
                nextPVP = Form1.getTime(PFStuff.PVPTime);
                if (nextPVP < DateTime.Now)
                    nextPVP = nextPVP.AddMilliseconds(3605000);
                PVPTimer.Interval = 905000;
                main.PvPLog.SynchronizedInvoke(() => main.PvPLog.AppendText(PFStuff.battleResult));
                main.PvPTimeLabel.setText(nextPVP.ToString());
                PVPTimer.Start();
                return b;
            }
            catch (Exception ex)
            {
                PFStuff.logError("PvP", "Catched error " + ex.Message + " vs index " + index.ToString() + " (" + PFStuff.nearbyPlayersIDs[index] + ") (nearbyPlayersIDs = " + JsonConvert.SerializeObject(PFStuff.nearbyPlayersIDs) + ")");
                return true;
            }
        }

        async void TourTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (!PlayFab.PlayFabClientAPI.IsClientLoggedIn())
            {
                await main.login();
            }
            TourTimer.Stop();
            TourTimer.Interval = 10 * 60 * 1000;
            if (main.autoT1Checkbox.Checked && !PFStuff.T1joined)
            {
                await main.pf.sendT1Register();
                main.isT1Joined.Text = "building a grid...";
                TourTimer.Interval = 30000;
            }
            if (main.autoT2Checkbox.Checked && !PFStuff.T2joined)
            {
                await main.pf.sendT2Register();
                main.isT2Joined.Text = "building a grid...";
                TourTimer.Interval = 30000;
            }
            TourTimer.Start();
        }
    }
}
