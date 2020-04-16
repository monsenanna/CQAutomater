using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.IO;

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
            int size = Math.Max(3, 2 * (int)Math.Max(main.playersAboveCount.Value, main.playersBelowCount.Value + 1));
            while (!await main.pf.getLeaderboard(size));
            main.pvpRankingSummary.setText("Ranking evolution : " + (PFStuff.initialRanking - PFStuff.currentRanking).ToString() + " (" + PFStuff.initialRanking + " -> " + PFStuff.currentRanking + ")");
            Random r = new Random();
            int index;
            // try finding easiest opponent
            index = await main.pf.getEasiestOpponent();
            if (pickBest || index == 0)
            {
                // random among neighbors
                do
                {
                    index = r.Next(0, PFStuff.nearbyPlayersIDs.Length);
                } while (index == PFStuff.userIndex ||
                        index > PFStuff.userIndex + (int)main.playersBelowCount.Value ||
                        index < PFStuff.userIndex - (int)main.playersAboveCount.Value);
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
                await main.getData();
                int fightsToDo = Int32.Parse(PFStuff.PVPCharges);
                if (fightsToDo > 0)
                {
                    int index = await pickOpponent(true);
                    main.taskQueue.Enqueue(() => sendFight(index), "PVP");
                    /*if (fightsToDo > 1)
                    {
                        await Task.Delay(10000);
                        index = await pickOpponent(false);
                        main.taskQueue.Enqueue(() => sendFight(index), "PVP");
                    }*/

                } else {
                    nextPVP = Form1.getTime(PFStuff.PVPTime);
                    if (nextPVP < DateTime.Now)
                        nextPVP = nextPVP.AddMilliseconds(3605000);
                    main.PvPTimeLabel.setText(nextPVP.ToString());
                    //PVPTimer.Interval = fightsToDo > 1 ? 30000 : Math.Max(8000, (nextPVP - DateTime.Now).TotalMilliseconds);
                    PVPTimer.Interval = fightsToDo > 0 ? 30000 : Math.Max(8000, Math.Min(600000, (nextPVP - DateTime.Now).TotalMilliseconds));
                    PVPTimer.Start();
                }
            }
        }

        internal async Task<bool> sendFight(int index)
        {
            bool b = await main.pf.sendPVPFight(index);
            nextPVP = Form1.getTime(PFStuff.PVPTime);
            if (nextPVP < DateTime.Now)
                nextPVP = nextPVP.AddMilliseconds(3605000);
            //PVPTimer.Interval = Math.Max(8000, (nextPVP - DateTime.Now).TotalMilliseconds);
            PVPTimer.Interval = 60000;
            main.PvPLog.SynchronizedInvoke(() => main.PvPLog.AppendText(PFStuff.battleResult));
            main.PvPTimeLabel.setText(nextPVP.ToString());
            PVPTimer.Start();
            return b;
        }
    }
}
