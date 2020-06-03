using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.IO;
using Newtonsoft.Json;

namespace CQFollowerAutoclaimer
{
    class AutoPvP
    {
        internal DateTime nextPVP;
        internal System.Timers.Timer PVPTimer = new System.Timers.Timer();
        Form1 main;
        
        public AutoPvP(Form1 m)
        {
            main = m;
            PVPTimer.Elapsed += PVPTimer_Elapsed;
        }

        internal void loadPVPSettings()
        {
            main.autoPvPCheckbox.Checked = main.appSettings.autoPvPEnabled ?? false;
            main.doPvPHistoryCheckbox.Checked = main.appSettings.doPVPHistory ?? false;
            main.playersBelowCount.Value = main.appSettings.pvpLowerLimit ?? 4;
            main.playersAboveCount.Value = main.appSettings.pvpUpperLimit ?? 5;
        }

        internal async Task<Int32> pickOpponent(bool pickBest)
        {
            int size = Math.Max(5, 2 * (int)Math.Max(main.playersAboveCount.Value, main.playersBelowCount.Value + 1));
            while (!await main.pf.getLeaderboard(size));
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

        async void PVPTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (main.autoPvPCheckbox.Checked)
            {
                PVPTimer.Stop();
                if (!PlayFab.PlayFabClientAPI.IsClientLoggedIn())
                {
                    await main.login();
                }
                await Task.Delay(10000);
                await main.getData();
                int fightsToDo = Int32.Parse(PFStuff.PVPCharges);
                if (fightsToDo > 0)
                {
                    int index = await pickOpponent(true);
                    main.taskQueue.Enqueue(() => sendFight(index), "PVP");
                } else {
                    nextPVP = Form1.getTime(PFStuff.PVPTime);
                    if (nextPVP < DateTime.Now)
                        nextPVP = nextPVP.AddMilliseconds(3605000);
                    main.PvPTimeLabel.setText(nextPVP.ToString());
                    //PVPTimer.Interval = fightsToDo > 1 ? 30000 : Math.Max(8000, (nextPVP - DateTime.Now).TotalMilliseconds);
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
                    main.pf.logError("PvP", "Fight impossible vs index " + PFStuff.nearbyPlayersIDs[index]+ " (nearbyPlayersIDs = " + JsonConvert.SerializeObject(PFStuff.nearbyPlayersIDs) + ")");
                    if (PFStuff.nearbyPlayersIDs.Length < 3)
                    {
                        // rebuild leaderboard
                        await Task.Delay(5000);
                        int size = Math.Max(5, 2 * (int)Math.Max(main.playersAboveCount.Value + 4, main.playersBelowCount.Value + 5));
                        await main.pf.getLeaderboard(size);
                        await Task.Delay(5000);
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
                //PVPTimer.Interval = Math.Max(8000, (nextPVP - DateTime.Now).TotalMilliseconds);
                PVPTimer.Interval = 905000;
                main.PvPLog.SynchronizedInvoke(() => main.PvPLog.AppendText(PFStuff.battleResult));
                main.PvPTimeLabel.setText(nextPVP.ToString());
                PVPTimer.Start();
                return b;
            }
            catch (Exception ex)
            {
                main.pf.logError("PvP", "Catched error " + ex.Message + " vs index " + PFStuff.nearbyPlayersIDs[index] + " (nearbyPlayersIDs = " + JsonConvert.SerializeObject(PFStuff.nearbyPlayersIDs) + ")");
                /*using (StreamWriter sw = new StreamWriter("ActionLog.txt", true))
                {
                    sw.WriteLine(DateTime.Now + "\n\t" + "Error in AutoPvP" + "\n\t" + ex.Message);
                    sw.WriteLine(DateTime.Now + "\n\t" + "PFStuff.nearbyPlayersIDs" + "\n\t" + JsonConvert.SerializeObject(PFStuff.nearbyPlayersIDs));
                    sw.WriteLine(DateTime.Now + "\n\t" + "index" + "\n\t" + index.ToString());
                }*/
                return true;
            }
        }
    }
}
