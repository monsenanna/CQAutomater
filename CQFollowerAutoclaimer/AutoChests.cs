﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.IO;

namespace CQFollowerAutoclaimer
{
    public class AutoChests
    {
        internal DateTime nextFreeChest;
        internal Timer FreeChestTimer = new Timer();
        Form1 main;

        public AutoChests(Form1 m)
        {
            main = m;
            FreeChestTimer.Elapsed += FreeChestTimer_Elapsed;
            loadSettings();
        }

        void loadSettings()
        {
            main.freeChestBox.Checked = main.appSettings.autoChestEnabled ?? false;
            main.chestToOpenCount.Value = main.appSettings.chestsToOpen ?? 0;
        }

        async void FreeChestTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (!PlayFab.PlayFabClientAPI.IsClientLoggedIn())
            {
                await main.login();
            }
            await main.getCurr();
        }

        internal async Task<bool> openChest(string mode)
        {
            try
            {
                var b = await main.pf.sendOpen(mode);
                if (Constants.ERROR.ContainsKey(PFStuff.chestResult))
                {
                    main.ChestLog.SynchronizedInvoke(() => main.ChestLog.AppendText(Constants.ERROR[PFStuff.chestResult] + "\n"));
                }
                else
                {
                    string rew = "Got ";
                    if (-PFStuff.chestResult >= Constants.heroNames.Length)
                    {
                        rew += "Unknown Hero(ID: " + PFStuff.chestResult + ")";
                    }
                    else
                    {
                        rew += (PFStuff.chestResult < 0 ? Constants.heroNames[-PFStuff.chestResult] : Constants.rewardNames[PFStuff.chestResult]) + " from " + mode + " chest";
                    }
                    main.ChestLog.SynchronizedInvoke(() => main.ChestLog.AppendText(rew + "\n"));
                    using (StreamWriter sw = new StreamWriter("ActionLog.txt", true))
                    {
                        sw.WriteLine(DateTime.Now + "\t" + rew + "\n");
                    }
                }
                if (!main.taskQueue.Contains("chest"))
                {
                    main.openNormalButton.SynchronizedInvoke(() => main.openNormalButton.Enabled = true);
                    main.openHeroButton.SynchronizedInvoke(() => main.openHeroButton.Enabled = true);
                }
                return b;
            }
            catch (Exception webex)
            {
                using (StreamWriter sw = new StreamWriter("ActionLog.txt", true))
                {
                    sw.WriteLine(DateTime.Now + "\n\t" + webex.Message);
                }
                return false;
            }
        }       
    }
}
