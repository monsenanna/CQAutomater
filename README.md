# CQAutomater

CQAutomater is a tool that runs in the background and automatically claims your miracles as soon as they are ready. It can also open the daily free chest, start battles with random person when your hourly battle is ready, automatically send a predefined lineup to beat DQ or run the calc to solve it and finally fight World Bosses.

# v 4.5.0.7
Feeding a shared sql database with PvP history, tournament grids, and userid-username connection. Ever wanted to know your neighbour's grid ? :)

# v 4.5.0.0
Events ok, subatomic heroes, personal PvP history (work in progress, more info to come).

# v 4.3.3.0
Added Bornag WB.
Events in progress.

# v 4.3.2.0
Bugfixes and improvements.

# v 4.3.1.0
Added cube and candy heroes, as well as Smith and Lili.

# v 4.3.0.0
Added S8 heroes & T31+ monsters.
Fixed PvP timer issue.

# v 4.2.1.0
Added Cupid, Mother...

# v 4.2.0.0
Added Easter2019 & aquatic Heroes.
Fixed crashes when server returns incomplete data.

# v 4.1.4.0
Added Ascended Djinn Heroes.

Last versions before this fork :

# v 4.1.3.0
Added 2nd Anniversary and Dragon AH Heroes.

# v 4.1.2.1
Added Drifter heroes.
Cleaned up some code formatting.

# v 4.1.1.0
Added Valentines LTO.
Updated Github links.

# v 4.1.0.4
Added Season 7 Heroes,
Fixed typo causing Super MOAK to show up as Unknown in AutoWB tab,
Fixed issue with automater crashing due to AutoPVP fighting an "undefined" enemy.

# v 4.0.1.1
Added S6, Christmas 2018, and Destructor heroes. Fixed an issue with Doyenne lineups not saving.

# v 3.2.1
Added Doyenne

# WARNING:
Auto-WB will work correctly only if you've enabled your username on website: https://cosmosquest.net/enable.php
I really don't recommend using auto-WB feature without username enabled as this will probably cause you attack way too many times.

# How to get Authentication Ticket and Kong ID:

You can follow instructions from that thread: https://www.kongregate.com/forums/910715-cosmos-quest/topics/965457-cq-macro-creator-for-diceycles-calc
or
you can use the "new" method.
Add a new bookmark in your browser. In most browser you do that by right clicking on your bookmarks bar(CTRL+SHIFT+B if you can't see it) and choosing "Add Page". You can write anything in the "Name" field. In the "URL" field paste this:
>javascript:prompt('UserID:\n'+active_user.id()+'\n\nGameAuthToken:\n'+"Copy to clipboard: Ctrl+C, Enter", active_user.gameAuthToken());

Now make sure that currently selected tab in your browser is Kongregate with Cosmos Quest game and you are logged in. Click on the created bookmark and the windows should open with your KongID and Auth Ticket.

#### Don't share Authentication Ticket with anyone!

Now when you start a program for the first time and you don't have valid MacroSettings file, the program will ask if you need help with creating one. Just provide it with necessary info(for CQAutomater you only need KongID and AuthTicket, other settings are used only in CQMacroCreator), save them to file and restart the program.
