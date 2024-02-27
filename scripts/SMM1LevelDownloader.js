const axios = require('axios');
const { app, BrowserWindow, dialog, ipcMain, shell, nativeImage } = require('electron');
const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');
const readline = require('readline');
const smmCourseViewer = require('smm-course-viewer');

const rl = readline.createInterface({
    input: process.stdin,
    output: process.stdout,
  });

const partNames = [
    "thumbnail0.tnl",
    "course_data.cdt",
    "course_data_sub.cdt",
    "thumbnail1.tnl"
];

const thisReleaseTag = "V2.0-Pre"

const iconPath = path.join(__dirname, '../SMMDownloader/Data');
const jsonDirectory = path.join(__dirname, '../SMMDownloader/Data');
if (!fs.existsSync(jsonDirectory)){
  fs.mkdirSync(jsonDirectory, { recursive: true });
  console.log(`Created directory: ${jsonDirectory}`);
}
const outputDirectory = path.join(__dirname, '../SMMDownloader/Data/DownloadCache');
if (!fs.existsSync(outputDirectory)){
    fs.mkdirSync(outputDirectory, { recursive: true });
    console.log(`Created directory: ${outputDirectory}`);
}

// Path to the ASH Extractor executable within the SMMDownloader directory
const ashextractorExecutable = path.join(__dirname, '../SMMDownloader', 'ashextractor.exe');

async function fetchArchiveUrl(originalUrl, levelObj) {
    const encodedUrl = encodeURIComponent(originalUrl);
    const apiUrl = `https://web.archive.org/__wb/sparkline?output=json&url=${encodedUrl}&collection=web`;
  
    const headers = {
        'Accept': '*/*',
        'Accept-Language': 'de,en-US;q=0.7,en;q=0.3',
        'Referer': `https://web.archive.org/web/20240000000000*/${encodedUrl}`,
        'Pragma': 'no-cache',
        'Cache-Control': 'no-cache',
    };
    try {
      const response = await axios.get(apiUrl, { headers });
      //console.log(`Fetched archive URL from Wayback Machine: ${JSON.stringify(response.data)}`);
      
      const archiveTimestamp = response.data.first_ts;
      if (!archiveTimestamp) {
          console.error('No archived version found.');
          return null;
      }
    
      const archiveUrl = `https://web.archive.org/web/${archiveTimestamp}if_/${originalUrl}`;
      return archiveUrl;
    } catch (error) {
      //mainWindow.webContents.send("fromMain", {action:"download-info",resultType:'ERROR',step:"fetchArchiveUrl",levelid:levelObj.levelid,info:"Wasn't able to fetch archive URL from Wayback Machine"});
    }
}

async function checkNewRelease() {
  const repo = 'HerobrineTV/SMM1-Level-Downloader'; // Replace with your GitHub repo
  const url = `https://api.github.com/repos/${repo}/releases/latest`;

  try {
    const response = await fetch(url);
    const data = await response.json();

    if (!response.ok) {
      throw new Error(`GitHub API error: ${data.message}`);
    }

    console.log('Latest release tag:', data.tag_name);
    //console.log(data)
    // You can further process the data as needed, for example, compare it with your local version
    return data;
  } catch (error) {
    console.error('Failed to fetch the latest release!');
  }
}

async function downloadFile(fileUrl, outputPath, levelObj) {
    const headers = {
      'Accept': 'text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8',
      'Accept-Language': 'de,en-US;q=0.7,en;q=0.3',
      'Accept-Encoding': 'gzip, deflate, br',
      'Connection': 'keep-alive',
      'Upgrade-Insecure-Requests': '1',
      'Sec-Fetch-Dest': 'document',
      'Sec-Fetch-Mode': 'navigate',
      'Sec-Fetch-Site': 'same-origin',
      'Sec-Fetch-User': '?1',
      'TE': 'trailers',
      'Pragma': 'no-cache',
      'Cache-Control': 'no-cache'
    };
  
    try {
      const response = await axios.get(fileUrl, { 
        headers: headers,
        responseType: 'arraybuffer',
        decompress: true 
      });
      fs.writeFileSync(outputPath, response.data);
      //console.log(`File downloaded at: ${outputPath}`);
      mainWindow.webContents.send("fromMain", {action:"download-info",resultType:'IN_PROGRESS',step:"downloadFile",levelid:levelObj.levelid,info:"Successfully downloaded file from Wayback Machine"});
    } catch (error) {
      mainWindow.webContents.send("fromMain", {action:"download-info",resultType:'ERROR',step:"downloadFile",levelid:levelObj.levelid,info:"Wasn't able to download file from Wayback Machine"});
      console.error('Error downloading file:', error.message);
    }
  }

  function splitFile(filePath, levelid, levelObj) {
    mainWindow.webContents.send("fromMain", {action:"download-info",resultType:'IN_PROGRESS',step:"splitFile",levelid:levelObj.levelid,info:"Starting to split file"});
    //console.log(`Splitting file: ${filePath}`);
    const data = fs.readFileSync(filePath);

    // ASH0 in hexadecimal byte representation
    const separator = Buffer.from([0x41, 0x53, 0x48, 0x30]); // ASCII for 'ASH0'
    let parts = [];
    let lastIndex = 0;
    let index = 0;

    const partNamesFirst = [
        "thumbnail0",
        "course_data",
        "course_data_sub",
        "thumbnail1"
    ];

    // Search through the file for each occurrence of the separator
    while ((index = data.indexOf(separator, lastIndex)) !== -1) {
        let endOfPart = data.indexOf(separator, index + separator.length);
        endOfPart = endOfPart === -1 ? data.length : endOfPart; // Handle last part

        const partData = data.slice(index, endOfPart);
        if (partData.length > 0) {
            parts.push(partData);
        }
        lastIndex = endOfPart;
    }

    // Ensure we have a directory to save the parts
    //const partsDirectory = path.join(outputDirectory, `${path.basename(filePath, path.extname(filePath))}_Extracted`);
    const partsDirectory = path.join(outputDirectory, `${levelid}`);
    if (!fs.existsSync(partsDirectory)) {
        fs.mkdirSync(partsDirectory, { recursive: true });
    }

    // Save each part with the predetermined names
    parts.forEach((part, i) => {
        if (i < partNamesFirst.length) { // Ensure we don't exceed the names array
            const partFilePath = path.join(partsDirectory, partNamesFirst[i]);
            fs.writeFileSync(partFilePath, part);
            //console.log(`Saved: ${partFilePath}`);
            mainWindow.webContents.send("fromMain", {action:"download-info",resultType:'IN_PROGRESS',step:"splitFile",levelid:levelObj.levelid,info:`Saved part ${partNamesFirst[i]}`});
        }
    });

    decompressAndRenameFiles(partsDirectory, parts.length, partNamesFirst, levelObj);
}

function containsSpecificFile(directory, fileName) {
    try {
      const files = fs.readdirSync(directory);
      return files.includes(fileName);
    } catch (error) {
      console.error(`Error reading directory ${directory}:`, error);
      return false;
    }
  }
  
  // Function to rename a file if a file with a certain name exists
  function renameFileIfConditionMet(directory, originalFileName, newFileName) {
    const filePath = path.join(directory, originalFileName);
    const newFilePath = path.join(directory, newFileName);
  
    if (fs.existsSync(filePath)) {
      fs.renameSync(filePath, newFilePath);
      console.log(`Renamed ${originalFileName} to ${newFileName}`);
    } else {
      console.log(`${originalFileName} does not exist and cannot be renamed.`);
    }
  }

// Function to decompress a file if a file using ASH Extractor http://wiibrew.org/wiki/ASH_Extractor
  async function decompressAndRenameFiles(partsDirectory, partCount, partNamesFirst, levelObj) {
    mainWindow.webContents.send("fromMain", {action:"download-info",resultType:'IN_PROGRESS',step:"decompressAndRenameFiles",levelid:levelObj.levelid,info:`Starting Decompressing and Renaming Files`});
    for (let i = 0; i < partCount; i++) {
        const partFilePath = path.join(partsDirectory, partNamesFirst[i]);

        try {
            //console.log(`Decompressing: ${partFilePath}`);
            console.log(ashextractorExecutable, partFilePath);
            execSync(`"${ashextractorExecutable}" ${partFilePath}`);
            if (containsSpecificFile(partsDirectory, partNamesFirst[i]+".arc")) {
              console.log(`${partFilePath} has sucessfully been compressed!`);
              renameFileIfConditionMet(partsDirectory, partNamesFirst[i]+".arc", partNames[i]);
              fs.unlink(partFilePath, (err) => {
                if (err) throw err;
                console.log('File deleted successfully!');
              });
            }
        } catch (error) {
            if (containsSpecificFile(partsDirectory, partNamesFirst[i]+".arc")) {
                console.log(`${partFilePath} has sucessfully been compressed!`);
                renameFileIfConditionMet(partsDirectory, partNamesFirst[i]+".arc", partNames[i]);
                fs.unlink(partFilePath, (err) => {
                  if (err) throw err;
                  console.log('File deleted successfully!');
                });
            }
        }
    }
    addLevelToJson(levelObj)
    mainWindow.webContents.send("fromMain", {action:"download-info",resultType:"SUCCESS",step:"decompressAndRenameFiles",levelid:levelObj.levelid,info:"All files have been decompressed and Downloaded!"});
    //console.log(`All files have been decompressed, u find them here ${partsDirectory}`);
}

async function addLevelToJson(levelObj){
      // Read the existing JSON data from the file
      const levelobjlvlid = levelObj.levelid;
      let jsonData = {};
      try {
          const data = fs.readFileSync(jsonDirectory+"/downloaded.json", 'utf8');
          jsonData = JSON.parse(data);
      } catch (error) {
          console.error('Error reading JSON file:', error);
          return;
      }
  
      // Append the object to the JSON Object
      jsonData[levelObj.levelid] = levelObj;

      // Write the updated JSON back to the file
      try {
          fs.writeFileSync(jsonDirectory+"/downloaded.json", JSON.stringify(jsonData, null, 2));
          //console.log('Object added to JSON file successfully.');
          mainWindow.webContents.send("fromMain", {action:"download-info",resultType:'IN_PROGRESS',step:"addLevelToJson",levelid:levelobjlvlid,info:"Finished adding object to JSON file."});
      } catch (error) {
          console.error('Error writing JSON file:', error);
          mainWindow.webContents.send("fromMain", {action:"download-info",resultType:'IN_PROGRESS',step:"addLevelToJson",levelid:levelobjlvlid,info:"Error adding object to JSON file."});
      }
}

function delay(ms) {
  return new Promise(resolve => setTimeout(resolve, ms));
}

async function fetchArchiveUrlWithRetries(originalUrl, levelObj) {
  let attempts = 0;
  while (attempts < 5) {
    try {
      const archiveUrl = await fetchArchiveUrl(originalUrl, levelObj);
      if (archiveUrl) {
        return archiveUrl; // If successful, return the URL
      }
      // If fetchArchiveUrl returns null or undefined, increase attempt count and retry after a delay
      attempts++;
      console.log(`Attempt ${attempts} failed. Retrying...`);
      await delay(1000); // Wait for 1 second before retrying
    } catch (error) {
      // If fetchArchiveUrl throws an error, log it, increase attempt count, and retry after a delay
      console.error(`Attempt ${attempts} failed with error: ${error}. Retrying...`);
      attempts++;
      await delay(1000); // Wait for 1 second before retrying
    }
  }
  // After 5 failed attempts, return null to indicate failure
  return null;
}


async function processUrl(originalUrl, levelid, levelObj) {
    const archiveUrl = await fetchArchiveUrlWithRetries(originalUrl, levelObj);
    if (!archiveUrl) {
      mainWindow.webContents.send("fromMain", {action:"download-info",resultType:'ERROR',step:"fetchArchiveUrl",levelid:levelObj.levelid,info:"Wasn't able to fetch archive URL from Wayback Machine."});
      return;
    }
    mainWindow.webContents.send("fromMain", {action:"download-info",resultType:'IN_PROGRESS',step:"fetchArchiveUrl",levelid:levelObj.levelid,info:"Fetched archive URL from Wayback Machine."});

    const fileName = path.basename(new URL(originalUrl).pathname);
    const outputPath = path.join(outputDirectory, fileName);

    await downloadFile(archiveUrl, outputPath, levelObj);
    splitFile(outputPath, levelid, levelObj);
}

  let mainWindow;

  function createWindow() {
    mainWindow = new BrowserWindow({
      width: 930,
      height: 800,
      icon:iconPath,
      webPreferences: {
        nodeIntegration: true,
        preload: path.join(__dirname, 'preload.js')
      },
      menu: false
    });

    //mainWindow.setMenu(null);
    const appIcon = nativeImage.createFromPath(iconPath+"/Icon.png");
    mainWindow.setIcon(appIcon);
    //mainWindow.loadFile(iconPath+"/Icon.png")
    mainWindow.loadFile('../pages/index.html');
    checkNewRelease();
  }

  app.whenReady().then(() => {
    createWindow();
});

  //app.whenReady().then(createWindow);

  function selectFolder() {
    dialog.showOpenDialog({ properties: ['openDirectory'] }).then(result => {
      const selectedFolderPath = result.filePaths[0];
      mainWindow.webContents.send("fromMain", {action:"selectedFolder",path:selectedFolderPath});
    }).catch(err => {
      console.log(err);
    });
  }

  function saveSettings(settings) {
    var jsonData;
    
    if (settings == "RESET") {
      jsonData = JSON.stringify({"useCemuDir":false,"BackupLevels":false,"CemuDirPath":"","selectedProfile":"","useAPILink":true,"APILink":"https://api.bobac-analytics.com/smm1","lastSearchPhrase":"","recentFoundLevels":[],"searchParams":{"LevelName":false,"LevelID":false,"CreatorName":false,"CreatorID":false, "SearchExact":false}});
    } else {
      jsonData = JSON.stringify(settings);
    }
    // Write data to the file
    fs.writeFile(path.join(jsonDirectory,"/data.json"), jsonData, 'utf8', (err) => {
        if (err) {
          mainWindow.webContents.send("fromMain", {action:"saveing",result:'Error saving settings:'+err});
          return;
        }
        if (settings == "RESET") {
          app.quit();
        }
        mainWindow.webContents.send("fromMain", {action:"saveing",result:'Settings saved successfully'});
    });
  }

  function folderExists(folderPath) {
    try {
        // Check if the folder exists
        fs.accessSync(folderPath, fs.constants.F_OK);
        return true;
    } catch (error) {
        // Folder does not exist or cannot be accessed
        return false;
    }
}

function loadExistingUserIDs(cemupath) {
  fs.readdir(path.join(cemupath, "mlc01","usr","save","00050000","1018dd00","user"), { withFileTypes: true }, (err, files) => {
    if (err) {
      console.error('Error reading the directory:', err);
      return;
    }

    const folders = files.filter(dirent => dirent.isDirectory() && dirent.name !== 'common').map(dirent => dirent.name);
    mainWindow.webContents.send("fromMain", {action:"currentUsersInSMM1Dir",users:folders});
  });
}

function courseViewerExtract(coursepath){
  fs.readdir(coursepath, { withFileTypes: true }, (err, files) => {
    if (err) {
      console.error('Error reading the directory:', err);
      return;
    }

    const folders = files.filter(dirent => dirent.isDirectory()).map(dirent => dirent.name);

    let levels = [];
    var counter = 0;

    if (folders.length == 0) {
      if (coursepath == outputDirectory) {

      } else if (coursepath == "BACKUP_DIR_PATH_HERE") {

      } else {
        mainWindow.webContents.send("fromMain", {action:"currentLevelsInSMM1ProfileDir",levels:null, problem:"No Levels Found!"});
        return;
      }
    }

    for (let i = 0; i < folders.length; i++) {
      smmCourseViewer.read(path.join(coursepath, folders[i], "course_data.cdt"), function(err, course, objects) {
        levelObj = {
          folder: folders[i],
          course: course,
          objects: objects,
          levelid: folders[i],
          html: smmCourseViewer.course.getHtml(),
          name: ""
        };
        if(!err) {
            levelObj.name = course['name'];
            levels.push(levelObj);
        } else {
          levels.push(levelObj);
        }
        counter++;
        if (counter == folders.length) {
          //console.log(levels[0].course)
          mainWindow.webContents.send("fromMain", {action:"currentLevelsInSMM1ProfileDir",levels:levels});
          for (let i = 0; i < folders.length; i++) {
            folders[i].fileName
            //const courseHTML = smmCourseViewer.course.getHtml();
            mainWindow.webContents.send("fromMain", {action:"displayCourse",coursehtml:levels[i].html,levelid:levels[i].levelid,course:levels[i].course,objects:levels[i].objects});	
            //mainWindow.webContents.send("fromMain", {action:"displayCourse",levelid:folders[i],course:levels[i].course,objects:levels[i].objects});	
          }
        }
        //let sizeBase = getSelectedSize();
        //new Draw('courseDisplay', course, objects, sizeBase);
      });
    }
  });
}

function loadDownloadedCourses(levels) {
  courseViewerExtract(outputDirectory);
}

function loadExistingCourses(cemupath, profileid) {
  //const profileFolder = cemupath;
  if (folderExists(cemupath) && folderExists(path.join(cemupath, "mlc01"))) {
    const profileFolder = path.join(cemupath, "mlc01","usr","save","00050000","1018dd00","user", profileid);
    courseViewerExtract(profileFolder);
  } else {
    mainWindow.webContents.send("fromMain", {action:"currentLevelsInSMM1ProfileDir",levels:null, problem:"mlc01 folder not Found in Cemu Path"});
  }
}

  ipcMain.on("toMain", (event, args) => {
    if (args.action === "select-folder") {
      selectFolder();
    } else if (args.action === "download-level") {
      if (folderExists(outputDirectory+"/"+args.levelID)) {
        mainWindow.webContents.send("fromMain", {action:"download-info",resultType:'ERROR',step:"initializing",levelid:args.levelID,info:"Level folder already exists."});
        return;
      }
      processUrl(args.url, args.levelID, args.levelObj);
    } else if (args.action === "save-settings") {
      saveSettings(args.settings);
    } else if (args.action === "search-level") {
      searchLevelInDB(args.searchTypes, args.searchPhrase);
    } else if (args.action === "exit-app") {
      app.quit();
    } else if (args.action === "openURL") {
      shell.openExternal(args.url);
    } else if (args.action === "checkIfAlreadyDownloaded") {
      if (folderExists(outputDirectory+"/"+args.levelID)) {
        mainWindow.webContents.send("fromMain", {action:"checkIfAlreadyDownloaded-info",answer:true, levelid:args.levelID});
      } else {
        mainWindow.webContents.send("fromMain", {action:"checkIfAlreadyDownloaded-info",answer:false, levelid:args.levelID});
      }
    } else if (args.action === "get-smm1-cached-downloads") {
      loadDownloadedCourses();
    } else if (args.action === "get-smm1-courses") {
      loadExistingCourses(args.path, args.selectedProfile);
    } else if (args.action === "get-smm1-profiles") {
      if (args.path != undefined && args.selectedProfile!== undefined && folderExists(args.path)) {
        loadExistingUserIDs(args.path)
      }
    }
  });

  //startProcess();

// Course URLS have to be retrieved from https://app.gigasheet.com/spreadsheet/courses-jsonl/1493e4c3_5fed_45cb_b189_2e1428df82d5 or a copy of this sheet

//const originalUrl = 'https://d2sno3mhmk1ekx.cloudfront.net/10.WUP_AMAJ_datastore/ds/1/data/00030699122-00001?Expires=1624406273&Signature=dvCQjz~tTf09havdMwRVryzz9MfRp16RF5NJWQa8k-wJiizAOlbmb9kMsT5Kv9j4-QJ1RzU1rwit4QFFTF8Jg7wRRwS0RVjENcuxy6wag-~v187HMsX3yMGRs8VxSx5Syem9ZxjTqGpBqRfm~71rQYKH~32vqDVTXR6IRtyOnKtAWfIikJK8Tk0jBQM~fFqv4OqqFCRhHRjFyp8hJPMaz8P5qIm~puSkJ0wUNvDKV0upwQw9RJiDABo1aRkcpW0QghK1xfQEEHCG4RVOn5Zng6rBNhSOLGcJe~K0bBffA~Y5kkgEbOl18c-BXXy3-z3hV-mnIcxRU9e6VNMo00M1Zw__&Key-Pair-Id=APKAJUYKVK3BE6ZPNZBQ'
//processUrl(originalUrl);


 // GitLens
 