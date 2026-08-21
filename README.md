# SMM1-Level-Downloader
A Super Mario Maker 1 Level Downloader designed for the Use of the WayBackMachines Level Collection

![License](https://img.shields.io/github/license/HerobrineTV/SMM1-Level-Downloader)
![Version](https://img.shields.io/github/v/release/HerobrineTV/SMM1-Level-Downloader)
![Contributors](https://img.shields.io/github/contributors/HerobrineTV/SMM1-Level-Downloader)

<img width="1177" height="789" alt="grafik" src="https://github.com/user-attachments/assets/cd2c1e91-e9e9-4da3-83a6-20e7968f05b2" />


# Features:
- Download every existing SMM1 Level from the Archive
- Download Multiple Levels at once
- Have a course Preview for saved Courses
- Have a WildCard Search for Levels (Takes sometimes a bit, but working)
- Use Proxies to download Levels even faster (needs to be enabled in Settings)
- Delete Levels (Currently Downloads only please, else it could break stuff!!!)
- Import Levels from other Archives as long as they are saved the same Way!
- Search by the Official Level IDs through the Database, but make sure u disabled all other searches (except fast search) else it wont work

<img width="1180" height="787" alt="grafik" src="https://github.com/user-attachments/assets/4716d0cc-d9e4-4a3c-a9dd-2e15f7a56915" />

<img width="1179" height="789" alt="grafik" src="https://github.com/user-attachments/assets/442c0f02-96ea-4d3c-99b6-c5dc41c49ba5" />

# Statistics
Now I added some Statistics the Total Searches are tracked since my very First Release.
But the Total Installations and Downloads are newly tracked.

![Total Searches](https://img.shields.io/badge/dynamic/json?label=Total%20Searches&query=%24.TotalLevelSearches&url=https%3A%2F%2Fapi.bobac-analytics.com%2Fsmm1%2Fget%2FTotalLevelSearches)
![Total Installations](https://img.shields.io/badge/dynamic/json?label=Total%20Installations&query=%24.DownloadsSMM1Downloader&url=https%3A%2F%2Fapi.bobac-analytics.com%2Fsmm1%2Fget%2FDownloadsSMM1Downloader)
![Total Level Downloads](https://img.shields.io/badge/dynamic/json?label=Total%20Level%20Downloads&query=%24.TotalDownloads&url=https%3A%2F%2Fapi.bobac-analytics.com%2Fsmm1%2Fget%2FTotalDownloads)

# Planned Features
- Level Uploads (maybe idk)
- Accountsystem (maybe idk, but would be needed for the uploads)
- Maybe a Most Downloaded Levels List (Daily/Monthly/Alltime)

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


# I want to build this myself!
Feel free to fork this repo and build it yourself!

If u use Windows that is the build command I used:

`dotnet publish AvaloniaApp\SMMDownloader.Avalonia.csproj -c Release -r win-x64 --self-contained true -o publish\windows-x64`

For Linux:

`dotnet publish AvaloniaApp\SMMDownloader.Avalonia.csproj -c Release -r linux-x64 --self-contained true -o publish\linux-x64`

To run it, just search the 

SMMDownloader.Avalonia.exe
or
SMMDownloader.Avalonia

and run it on your Machine!