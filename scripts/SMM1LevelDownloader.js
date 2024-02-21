const axios = require('axios');
const { app, BrowserWindow, dialog, ipcMain } = require('electron');
const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');
const readline = require('readline'); 

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

// Ensure SMMDownloader directory exists
const outputDirectory = path.join(__dirname, '../SMMDownloader/Data/DownloadCache');
const jsonDirectory = path.join(__dirname, '../SMMDownloader/Data');
if (!fs.existsSync(outputDirectory)){
    fs.mkdirSync(outputDirectory, { recursive: true });
    console.log(`Created directory: ${outputDirectory}`);
}

// Path to the ASH Extractor executable within the SMMDownloader directory
const ashextractorExecutable = path.join(outputDirectory, 'ashextractor.exe');

async function fetchArchiveUrl(originalUrl) {
    const encodedUrl = encodeURIComponent(originalUrl);
    const apiUrl = `https://web.archive.org/__wb/sparkline?output=json&url=${encodedUrl}&collection=web`;
  
    const headers = {
        'Accept': '*/*',
        'Accept-Language': 'de,en-US;q=0.7,en;q=0.3',
        'Referer': `https://web.archive.org/web/20240000000000*/${encodedUrl}`,
        'Pragma': 'no-cache',
        'Cache-Control': 'no-cache',
    };
  
    const response = await axios.get(apiUrl, { headers });
    console.log(`Fetched archive URL from Wayback Machine: ${JSON.stringify(response.data)}`);
    
    const archiveTimestamp = response.data.first_ts;
    if (!archiveTimestamp) {
        console.error('No archived version found.');
        return null;
    }
  
    const archiveUrl = `https://web.archive.org/web/${archiveTimestamp}if_/${originalUrl}`;
    return archiveUrl;
}

async function downloadFile(fileUrl, outputPath) {
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
      console.log(`File downloaded at: ${outputPath}`);
    } catch (error) {
      console.error('Error downloading file:', error.message);
    }
  }

  function splitFile(filePath, levelid, levelObj) {
    console.log(`Splitting file: ${filePath}`);
    const data = fs.readFileSync(filePath);

    // ASH0 in hexadecimal byte representation
    const separator = Buffer.from([0x41, 0x53, 0x48, 0x30]); // ASCII for 'ASH0'
    let parts = [];
    let lastIndex = 0;
    let index = 0;

    const partNamesFirst = [
        "thumbnail0.tnl",
        "course_data.cdt",
        "course_data_sub.cdt",
        "thumbnail1.tnl"
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
            console.log(`Saved: ${partFilePath}`);
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
    for (let i = 0; i < partCount; i++) {
        const partFilePath = path.join(partsDirectory, partNamesFirst[i]);

        try {
            console.log(`Decompressing: ${partFilePath}`);
            execSync(`"${ashextractorExecutable}" "${partFilePath}"`);
            console.log(`Decompressed: ${partFilePath}`);
        } catch (error) {
            if (containsSpecificFile(partsDirectory, partNamesFirst[i]+".arc")) {
                console.log(`${partFilePath} has sucessfully been compressed!`);
                renameFileIfConditionMet(partsDirectory, partNamesFirst[i]+".arc", partNames[i]);
            }
        }
    }
    addLevelToJson(levelObj)
    mainWindow.webContents.send("fromMain", {action:"download-info",resultType:"SUCCESS",resultMessage:"All files have been decompressed and Downloaded!"});
    console.log(`All files have been decompressed, u find them here ${partsDirectory}`);
}

async function addLevelToJson(levelObj){
      // Read the existing JSON data from the file
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
          console.log('Object added to JSON file successfully.');
      } catch (error) {
          console.error('Error writing JSON file:', error);
      }
}

async function processUrl(originalUrl, levelid, levelObj) {
    const archiveUrl = await fetchArchiveUrl(originalUrl);
    if (!archiveUrl) return;

    const fileName = path.basename(new URL(originalUrl).pathname);
    const outputPath = path.join(outputDirectory, fileName);

    await downloadFile(archiveUrl, outputPath);
    splitFile(outputPath, levelid, levelObj);
}

function startProcess() {
    rl.question('Enter the SMM1 Level URL you want to Download \n \n[URLS can be obtained here: https://app.gigasheet.com/spreadsheet/courses-jsonl/1493e4c3_5fed_45cb_b189_2e1428df82d5]: \n\n', (url) => {
      if (url.trim() === '') {
        console.error('URL cannot be empty. Please enter a valid URL.');
        startProcess(); // Ask for URL again
      } else {
        processUrl(url);
        rl.close();
      }
    });
  }

  let mainWindow;

  function createWindow() {
    mainWindow = new BrowserWindow({
      width: 800,
      height: 800,
      webPreferences: {
        nodeIntegration: true,
        preload: path.join(__dirname, 'preload.js')
      },
      menu: false
    });

    //mainWindow.setMenu(null);
  
    mainWindow.loadFile('../pages/index.html');
  }

  app.whenReady().then(createWindow);

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
      jsonData = JSON.stringify({"useCemuDir":false,"CemuDirPath":"","useAPILink":true,"APILink":"https://api.bobac-analytics.com/smm1","lastSearchPhrase":"","recentFoundLevels":[],"searchParams":{"LevelName":false,"LevelID":false,"CreatorName":false,"CreatorID":false}});
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

  function searchLevelInDB(searchTypes, searchPhrase) {
    mainWindow.webContents.send("fromMain", {action:"found-levels", resultType: 'SUCCESS', result:'Levels found', levels: [{"url":"exampleurl1", "name":"ExampleName", "creator":"ExampleCreator", "levelid":"1efe", "creatorid":"dsd", "clears":0, "failures":0, "total_attempts":0, "clearrate":0.00, "uploadTime":0, "world_record_ms":0, "world_record_holder_nnid":"gfh", "stars":0}]});
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

  ipcMain.on("toMain", (event, args) => {
    if (args.action === "select-folder") {
      selectFolder();
    } else if (args.action === "download-level") {
      addLevelToJson(args.levelObj);
      if (folderExists(outputDirectory+"/"+args.levelID)) {
        mainWindow.webContents.send("fromMain", {action:"download-info",resultType:"ERROR",resultMessage:"Already downloaded this level!"});
        return;
      }
      processUrl(args.url, args.levelID, args.levelObj);
    } else if (args.action === "save-settings") {
      saveSettings(args.settings);
    } else if (args.action === "search-level") {
      searchLevelInDB(args.searchTypes, args.searchPhrase);
    } else if (args.action === "exit-app") {
      app.quit();
    }
  });

  //startProcess();

// Course URLS have to be retrieved from https://app.gigasheet.com/spreadsheet/courses-jsonl/1493e4c3_5fed_45cb_b189_2e1428df82d5 or a copy of this sheet

//const originalUrl = 'https://d2sno3mhmk1ekx.cloudfront.net/10.WUP_AMAJ_datastore/ds/1/data/00030699122-00001?Expires=1624406273&Signature=dvCQjz~tTf09havdMwRVryzz9MfRp16RF5NJWQa8k-wJiizAOlbmb9kMsT5Kv9j4-QJ1RzU1rwit4QFFTF8Jg7wRRwS0RVjENcuxy6wag-~v187HMsX3yMGRs8VxSx5Syem9ZxjTqGpBqRfm~71rQYKH~32vqDVTXR6IRtyOnKtAWfIikJK8Tk0jBQM~fFqv4OqqFCRhHRjFyp8hJPMaz8P5qIm~puSkJ0wUNvDKV0upwQw9RJiDABo1aRkcpW0QghK1xfQEEHCG4RVOn5Zng6rBNhSOLGcJe~K0bBffA~Y5kkgEbOl18c-BXXy3-z3hV-mnIcxRU9e6VNMo00M1Zw__&Key-Pair-Id=APKAJUYKVK3BE6ZPNZBQ'
//processUrl(originalUrl);
