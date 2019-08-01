using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

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
            main.playersBelowCount.Value = main.appSettings.pvpLowerLimit ?? 4;
            main.playersAboveCount.Value = main.appSettings.pvpUpperLimit ?? 5;
        }

        internal async Task<Int32> pickOpponent()
        {
            int size = Math.Max(3, 2 * (int)Math.Max(main.playersAboveCount.Value, main.playersBelowCount.Value + 1));
            while (!await main.pf.getLeaderboard(size)) ;
            main.pvpRankingSummary.setText("Ranking evolution : " + (PFStuff.initialRanking - PFStuff.currentRanking).ToString() + " (" + PFStuff.initialRanking + " -> " + PFStuff.currentRanking + ")");
            Random r = new Random();
            int index;
            do
            {
                index = r.Next(0, PFStuff.nearbyPlayersIDs.Length);
            } while (index == PFStuff.userIndex ||
                    index > PFStuff.userIndex + (int)main.playersBelowCount.Value ||
                    index < PFStuff.userIndex - (int)main.playersAboveCount.Value);
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
                if (Int32.Parse(PFStuff.PVPCharges) > 0)
                {
                    int index = await pickOpponent();
                    main.taskQueue.Enqueue(() => sendFight(index), "PVP");
                } else {
                    nextPVP = Form1.getTime(PFStuff.PVPTime);
                    if (nextPVP < DateTime.Now)
                        nextPVP = nextPVP.AddMilliseconds(3605000);
                    main.PvPTimeLabel.setText(nextPVP.ToString());
                    PVPTimer.Interval = Math.Max(8000, (nextPVP - DateTime.Now).TotalMilliseconds);
                    PVPTimer.Start();
                }
                /*int index = await pickOpponent();
                main.taskQueue.Enqueue(() => sendFight(index), "PVP");*/
                /*nextPVP = Form1.getTime(PFStuff.PVPTime);
                if (nextPVP < DateTime.Now)
                    nextPVP = nextPVP.AddMilliseconds(3605000);
                main.PvPTimeLabel.setText(nextPVP.ToString());
                PVPTimer.Interval = Math.Max(8000, (nextPVP - DateTime.Now).TotalMilliseconds);
                main.PvPLog.SynchronizedInvoke(() => main.PvPLog.AppendText("time debug A : " + DateTime.Now + " --- " + nextPVP + " --- " + PVPTimer.Interval + "\n"));*/
            }
        }

        internal async Task<bool> sendFight(int index)
        {
            bool b = await main.pf.sendPVPFight(index);
            nextPVP = Form1.getTime(PFStuff.PVPTime);
            if (nextPVP < DateTime.Now)
                nextPVP = nextPVP.AddMilliseconds(3605000);
            //PVPTimer.Interval = Math.Min(3660000, Math.Max(5000, (nextPVP - DateTime.Now).TotalMilliseconds + 3600000));
            PVPTimer.Interval = Math.Max(8000, (nextPVP - DateTime.Now).TotalMilliseconds);
            /*if (PVPTimer.Interval < 0)
                PVPTimer.Interval += 3605000;*/
            //main.PvPLog.SynchronizedInvoke(() => main.PvPLog.AppendText("time debug B : " + DateTime.Now + " --- " + nextPVP +" --- "+ PVPTimer.Interval + "\n"));
            main.PvPLog.SynchronizedInvoke(() => main.PvPLog.AppendText(PFStuff.battleResult));
            main.PvPTimeLabel.setText(nextPVP.ToString());
            PVPTimer.Start();
            return b;
        }
    }
}
