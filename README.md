# Announcement
I am currently createing a whole UI System with Settings and Level Search using ElectronJS
If u got any feature requests Dm me on Discord: nintendo_switch

# SMM1-Level-Downloader
A Super Mario Maker 1 Level Downloader designed for the Use of the WayBackMachines Level Collection

# Decompilation
To Decompile the Levels I used ASH Extractor which is already In this Repository
http://wiibrew.org/wiki/ASH_Extractor

# First time setup
For the first time setup u need to install axios
"npm install axios"

# Usage
To use this Properly u need to use this Spreadsheet or a Copy of it
https://app.gigasheet.com/spreadsheet/courses-jsonl/1493e4c3_5fed_45cb_b189_2e1428df82d5
this was uploaded by a deleted Reddit user in this Post
https://www.reddit.com/r/MarioMaker/comments/wlkwp9/easily_searchable_database_of_super_mario_maker_1/?sort=new

After u opened this Spreadsheet u Can search for your level and then copy the URL tab of the wanted level
After that u need to run "node SMM1LevelDownloader.js"
And when it Prompts u to insert a link paste it there

It should then try to get that level and decompile it for you.
Once done your level is in a Folder given the Name the downloaded file was called + _Extracted
Now u just need to copy the contents of that folder and Replace them with one of your existing courses (can also be courses which u just created)

# How does this work?
It just downloads the Level via Archive.org since theres alot or almost every level stored mentioned in this Post
https://archive.org/details/super_mario_maker_courses_202105
