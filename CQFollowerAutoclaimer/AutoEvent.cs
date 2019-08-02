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

namespace CQFollowerAutoclaimer
{
    class AutoEvent
    {
        Form1 main;
        static int goodPicks = 0, badPicks = 0;
        public System.Timers.Timer EventTimer = new System.Timers.Timer();

        public AutoEvent(Form1 m)
        {
            main = m;
            EventTimer.Elapsed += EventTimer_Elapsed;
        }

        async void EventTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (!PlayFab.PlayFabClientAPI.IsClientLoggedIn())
            {
                await main.login();
            }
            PFStuff.getWebsiteData(main.KongregateId);

            /* status :
             * flash/eas OK (just a notice, no action)
             * keys OK (all automated, report on GUI ; add to log ?)
             * lf/pg IN PROGRESS
             * snake/lottery TODO (just a notice, no action)
            */
            if (PFStuff.FlashStatus != -1)
            {
                main.label73.setText("Flash : " + (PFStuff.FlashStatus == 1 ? "active" : "not active") + " today");
            }
            main.label109.setText("EAS : " + (PFStuff.EASDay == 1 ? "active" : "not active") + " today");
            main.label133.setText("Dungeon : " + (PFStuff.DungLevel != "-/-" ? "active" : "not active") + " today");
            if (PFStuff.LuckyFollowers != null)
            {
                main.label122.setText("Lucky Followers : " + PFStuff.LuckyFollowers.ToString()); // debug
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
            if (PFStuff.PGCards != null && (Int64)PFStuff.PGCards["attempts"] != 0)
            { // let's run PGCards
                void Work()
                {
                    // 1 : find 2 similar cards among non-picked ones
                    for (int i = 0; i < PFStuff.PGDeck.Length; i++)
                    {
                        if (PFStuff.PGPicked[i] == 0 && PFStuff.PGDeck[i] != -1)
                        {
                            for (int j = 0; j < PFStuff.PGDeck.Length; j++)
                            {
                                if (PFStuff.PGPicked[j] == 0 && PFStuff.PGDeck[i] == PFStuff.PGDeck[j])
                                {
                                    // found ! let's pick both
                                    // TODO
                                    return;
                                }
                            }
                        }
                    }
                    // 2a : pick one card
                    // 2b : check if similar card among non-picked ones
                    // 2c : pick another card
                }
                Work();
            }
            main.AEIndicator.BackColor = Color.Green;
        }
    }
}
