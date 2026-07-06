# SMM1-Level-Downloader
A Super Mario Maker 1 Level Downloader designed for the Use of the WayBackMachines Level Collection

![License](https://img.shields.io/github/license/HerobrineTV/SMM1-Level-Downloader)
![Version](https://img.shields.io/github/v/release/HerobrineTV/SMM1-Level-Downloader)
![Contributors](https://img.shields.io/github/contributors/HerobrineTV/SMM1-Level-Downloader)

![img.png](img.png)

# Features:
- Download every existing SMM1 Level from the Archive
- Download Multiple Levels at once
- Have a course Preview for saved Courses
- Have a WildCard Search for Levels (Takes sometimes a bit, but working)
- Use Proxies to download Levels even faster (needs to be enabled in Settings)
- Delete Levels (Currently Downloads only please, else it could break stuff!!!)
- Import Levels from other Archives as long as they are saved the same Way!
- Search by the Official Level IDs through the Database, but make sure u disabled all other searches (except fast search) else it wont work

![img_1.png](img_1.png)

![img_2.png](img_2.png)

# Statistics
![Total Searches](https://img.shields.io/badge/dynamic/json?label=Total%20Searches&query=%24.TotalLevelSearches&url=https%3A%2F%2Fapi.bobac-analytics.com%2Fsmm1%2Fget%2FTotalLevelSearches)
![Total Installations](https://img.shields.io/badge/dynamic/json?label=Total%20Installations&query=%24.DownloadsSMM1Downloader&url=https%3A%2F%2Fapi.bobac-analytics.com%2Fsmm1%2Fget%2FDownloadsSMM1Downloader)
![Total Level Downloads](https://img.shields.io/badge/dynamic/json?label=Total%20Level%20Downloads&query=%24.TotalDownloads&url=https%3A%2F%2Fapi.bobac-analytics.com%2Fsmm1%2Fget%2FTotalDownloads)

# Planned Features
- Download Levels directly into CEMU (Comming Soon, will also be toggleable in Settings)
- Overwrite CEMU Levels directly on Download (Comming Soon, Will add a Backup Toggle in Settings)

# Usage / Download
Just unzip the release archive and start the SMM1 Level Downloader executable.

# How does this work?
The app gathers the Archive.org download link for the searched level from my database.

Then it is downloading it from Archive.org
(Information here: https://archive.org/details/super_mario_maker_courses_202105)

After that u can View it in Saved Levels in the Programm.
There u will be able to see the Curse display. Which was only possible through Leo's Course Viewer, which he allowed me to implement!
(https://github.com/leomaurodesenv/smm-course-viewer)
Big thanks to him!

Now u can Press the "Open Folder" Button and u will see all your Saved Levels.

Just copy the inside of the folders in your desired Mario Maker 1 Savefile in one of the course files.

# How to get the Testing Levels?
Feel free to download New ones and add them to:
"SMMDownloader\Data\OfficialCourses\OriginalFiles"
And next time u press "Reset Official Courses", it will get loaded if it is a valid File (or if its a broken one it could crash xD)
- https://archive.org/details/smm1-game-dev
- https://archive.org/details/smm1staging

# Can I add more Levels if I only got the File without Definition?
Yes you can, just put it in this Folder: "SMMDownloader\Data\OfficialCourses\OriginalFiles"

# Important Note
U have to create the course Files ingame and not by creating a Folder, else they wont show up!
All Copyright of the Used Images goes to Nintendo

# Credits
- [HerobrineTV](https://github.com/HerobrineTV) - Creator of the App
- [Snoozbuster](https://github.com/snoozbuster) - Course Viewer for the Curse Display
- [LeoMauro](https://github.com/leomaurodesenv) - Course Viewer for the Curse Display

### Repo Views:
![Visitor Count](https://profile-counter.glitch.me/HerobrineTV_SMM1/count.svg)
