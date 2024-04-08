const axios = require('axios');
const { app, BrowserWindow, dialog, ipcMain, shell, nativeImage } = require('electron');
const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');
const readline = require('readline');
const smmCourseViewer = require('smm-course-viewer');
const { EventEmitter } = require('events');
const { HttpsProxyAgent } = require('https-proxy-agent');

const queueEventEmitter = new EventEmitter();

const downloadQueue = [];
let tasksProcessedThisMinute = 0;
let lastProcessedTimestamp = Date.now();
let isProcessingQueue = false;
let useProxy = false;
let currentProxyIndex = 0;
let proxies = [];
let proxy = null;

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
  //console.log(`Created directory: ${jsonDirectory}`);
}
const outputDirectory = path.join(__dirname, '../SMMDownloader/Data/DownloadCache');
if (!fs.existsSync(outputDirectory)){
    fs.mkdirSync(outputDirectory, { recursive: true });
    //console.log(`Created directory: ${outputDirectory}`);
}

// Path to the ASH Extractor executable within the SMMDownloader directory
const ashextractorExecutable = path.join(__dirname, '../SMMDownloader', 'ashextractor.exe');

async function fetchArchiveUrl(originalUrl, levelObj) {
    //console.log(proxies);  
    //console.log(proxy);
    //console.log(useProxy && proxy ? { host: proxy.host, port: proxy.port } : null);
    //console.log("------------------------------------------");
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
      const agent = useProxy && proxy ? new HttpsProxyAgent(`http://${proxy.host}:${proxy.port}`) : undefined;

      const response = await axios.get(apiUrl, { headers, httpsAgent: agent });
      //console.log(`Fetched archive URL from Wayback Machine: ${JSON.stringify(response.data)}`);
      
      const archiveTimestamp = response.data.first_ts;
      if (!archiveTimestamp) {
          console.error('No archived version found.');
          writeToLog('[Error] '+'No archived Version on The Archive found!');
          return null;
      }
    
      const archiveUrl = `https://web.archive.org/web/${archiveTimestamp}if_/${originalUrl}`;
      return archiveUrl;
    } catch (error) {
      console.error(error.message);
      console.error(`http://${proxy.host}:${proxy.port}`)
      writeToLog('[Error] '+'Error fetching The Archive API! '+error.message);
      //console.error(error);
      //mainWindow.webContents.send("fromMain", {action:"download-info",resultType:'ERROR',step:"fetchArchiveUrl",levelid:levelObj.levelid,info:"Wasn't able to fetch archive URL from Wayback Machine"});
    }
}

async function processQueue() {
  while (true) {
      if (downloadQueue.length > 0) {
          const currentTime = Date.now();
          const timeSinceLastProcessed = currentTime - lastProcessedTimestamp;

          if (useProxy) {
            //await loadProxiesFromFile(path.join(jsonDirectory,"/proxies.txt"));
          }

          // Check if a new minute has started
          if (timeSinceLastProcessed >= 60000) {
              // Reset the counter and timestamp if a new minute has started
              tasksProcessedThisMinute = 0;
              lastProcessedTimestamp = currentTime;
          }

          if (tasksProcessedThisMinute == 10) {
              if (!useProxy) {
                  await new Promise(resolve => setTimeout(resolve, 10000));
              }
          }

          // Check if the rate limit is reached
          if (tasksProcessedThisMinute >= 20) {
              // Calculate wait time until the next minute
              if (!useProxy) {
                  const waitTime = 120000 - timeSinceLastProcessed;
                  await new Promise(resolve => setTimeout(resolve, waitTime));
              } else {
                  await switchProxy();
              }
              tasksProcessedThisMinute = 0; // Reset the counter after waiting
              lastProcessedTimestamp = Date.now(); // Update last processed timestamp
          }

          // Process the next task in the queue
          const task = downloadQueue.shift();
          //console.log(task.levelID);
          await processUrlWithDelay(task.url, task.levelID, task.levelObj);
          tasksProcessedThisMinute++;

      } else {
          // Wait for a new task event before continuing the loop
          await new Promise(resolve => queueEventEmitter.once('newTask', resolve));
      }
  }
}

async function switchProxy() {
  currentProxyIndex = (currentProxyIndex + 1) % proxies.length;
  // If proxies are exhausted, reset index
  proxy = proxies[currentProxyIndex];
  if (currentProxyIndex === 0) {
      console.log('All proxies exhausted. Resetting.');
  }
}

async function processUrlWithDelay(url, levelID, levelObj) {
  //await new Promise(resolve => setTimeout(resolve, 3000)); // Wait for 3 seconds
  processUrl(url, levelID, levelObj); // Then call processUrl
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

    //console.log('Latest release tag:', data.tag_name);
    //console.log(data)
    // You can further process the data as needed, for example, compare it with your local version
    return data;
  } catch (error) {
    writeToLog('[Error] '+'Failed to fetch the latest release!');
    console.error('Failed to fetch the latest release!');
  }
}

async function downloadFile(fileUrl, outputPath, levelObj) {
    mainWindow.webContents.send("fromMain", {action:"download-info",resultType:'IN_PROGRESS',step:"downloadFile",levelid:levelObj.levelid,info:"Trying to download file from Wayback Machine"});
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
      const agent = useProxy && proxy ? new HttpsProxyAgent(`http://${proxy.host}:${proxy.port}`) : undefined;

      const response = await axios.get(fileUrl, {
        headers: headers,
        responseType: 'arraybuffer',
        decompress: true, 
        httpsAgent: agent
      });
      fs.writeFileSync(outputPath, response.data);
      //console.log(`File downloaded at: ${outputPath}`);
      mainWindow.webContents.send("fromMain", {action:"download-info",resultType:'IN_PROGRESS',step:"downloadFile",levelid:levelObj.levelid,info:"Successfully downloaded file from Wayback Machine"});
    } catch (error) {
      mainWindow.webContents.send("fromMain", {action:"download-info",resultType:'ERROR',step:"downloadFile",levelid:levelObj.levelid,info:"Wasn't able to download file from Wayback Machine"});
      writeToLog('[Error] '+'Error downloading file: '+error.message);
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
      writeToLog('[Error] '+`Error reading directory ${directory}: `+error.message);
      return false;
    }
  }
  
  // Function to rename a file if a file with a certain name exists
  function renameFileIfConditionMet(directory, originalFileName, newFileName) {
    const filePath = path.join(directory, originalFileName);
    const newFilePath = path.join(directory, newFileName);
  
    if (fs.existsSync(filePath)) {
      fs.renameSync(filePath, newFilePath);
      //console.log(`Renamed ${originalFileName} to ${newFileName}`);
    } else {
      //console.log(`${originalFileName} does not exist and cannot be renamed.`);
    }
  }

// Function to decompress a file if a file using ASH Extractor http://wiibrew.org/wiki/ASH_Extractor
  async function decompressAndRenameFiles(partsDirectory, partCount, partNamesFirst, levelObj) {
    mainWindow.webContents.send("fromMain", {action:"download-info",resultType:'IN_PROGRESS',step:"decompressAndRenameFiles",levelid:levelObj.levelid,info:`Starting Decompressing and Renaming Files`});
    for (let i = 0; i < partCount; i++) {
        const partFilePath = path.join(partsDirectory, partNamesFirst[i]);

        try {
            //console.log(`Decompressing: ${partFilePath}`);
            //console.log(ashextractorExecutable, partFilePath);
            execSync(`"${ashextractorExecutable}" ${partFilePath}`);
            if (containsSpecificFile(partsDirectory, partNamesFirst[i]+".arc")) {
              //console.log(`${partFilePath} has sucessfully been compressed!`);
              renameFileIfConditionMet(partsDirectory, partNamesFirst[i]+".arc", partNames[i]);
              fs.unlink(partFilePath, (err) => {
                if (err) throw err;
                //console.log('File deleted successfully!');
              });
            }
        } catch (error) {
            if (containsSpecificFile(partsDirectory, partNamesFirst[i]+".arc")) {
                //console.log(`${partFilePath} has sucessfully been compressed!`);
                renameFileIfConditionMet(partsDirectory, partNamesFirst[i]+".arc", partNames[i]);
                fs.unlink(partFilePath, (err) => {
                  if (err) throw err;
                  //console.log('File deleted successfully!');
                });
            }
        }
    }
    const partFilePath = path.join(outputDirectory, levelObj.levelid+'-00001');
    fs.unlink(partFilePath, (err) => {
      if (err) throw err;
      //console.log('File deleted successfully!');
    });
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
          writeToLog('[Error] '+`Error reading JSON file: `+error.message);
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
          writeToLog('[Error] '+`Error writing JSON file: `+error.message);
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
      //console.log(`Attempt ${attempts} failed. Retrying...`);
      await delay(1000); // Wait for 1 second before retrying
    } catch (error) {
      // If fetchArchiveUrl throws an error, log it, increase attempt count, and retry after a delay
      console.error(`Attempt ${attempts} failed with error: ${error}. Retrying...`);
      attempts++;
      await delay(1000); // Wait for 1 second before retrying
    }
  }
  writeToLog('[Error] '+`Failed 5 Times to fetch the Archive URL!`);
  // After 5 failed attempts, return null to indicate failure
  return null;
}

async function loadProxiesFromFile(filePath) {
  return new Promise((resolve, reject) => {
      fs.readFile(filePath, (error, data) => {
          if (error) {
              reject(new Error(`Failed to read file: ${error.message}`));
              return;
          }
          
          const lines = data.toString().split('\n').map(line => line.trim()).filter(line => line); // Filter out empty lines
          const proxies = lines.map(line => {
              if (line.startsWith('#')) return null;
              const [host, port] = line.split(':');
              return { host, port };
          }).filter(proxy => proxy !== null);
          proxy = proxies[0];
          currentProxyIndex = 0;
          resolve(proxies);
      });
  });
}

async function processUrl(originalUrl, levelid, levelObj) {
    mainWindow.webContents.send("fromMain", {action:"download-info",resultType:'IN_PROGRESS',step:"fetchArchiveUrl",levelid:levelObj.levelid,info:"Trying to fetch archive URL from Wayback Machine."});
    const archiveUrl = await fetchArchiveUrlWithRetries(originalUrl, levelObj);
    if (!archiveUrl) {
      mainWindow.webContents.send("fromMain", {action:"download-info",resultType:'ERROR',step:"fetchArchiveUrl",levelid:levelObj.levelid,info:"Wasn't able to fetch archive URL from Wayback Machine."});
      return;
    }
    mainWindow.webContents.send("fromMain", {action:"download-info",resultType:'IN_PROGRESS',step:"fetchArchiveUrl",levelid:levelObj.levelid,info:"Fetched archive URL from Wayback Machine."});

    const fileName = path.basename(new URL(originalUrl).pathname);
    const outputPath = path.join(outputDirectory, fileName.replace(/^0+/, ''));

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

    app.commandLine.appendSwitch('js-flags', '--max-old-space-size=4096');
    //mainWindow.setMenu(null);
    const appIcon = nativeImage.createFromPath(iconPath+"/Icon.png");
    mainWindow.setIcon(appIcon);
    //mainWindow.loadFile(iconPath+"/Icon.png")
    if (folderExists(path.join(__dirname, "../../app"))) {
      mainWindow.loadFile('../app/pages/index.html');
    } else {
      mainWindow.loadFile('../pages/index.html');
    }
    checkNewRelease();
    loadProxiesFromFile(path.join(jsonDirectory,"/proxies.txt"));
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
      jsonData = JSON.stringify(
        {
          "useCemuDir":false,
          "BackupLevels":false,
          "CemuDirPath":"",
          "selectedProfile":"",
          "useAPILink":true,
          "APILink":"https://api.bobac-analytics.com/smm1",
          "lastSearchPhrase":"",
          "recentFoundLevels":[],
          "amounttrue":0,
          "useProxy":false,
          "searchParams":
          {
            "LevelName":false,
            "LevelID":false,
            "CreatorName":false,
            "CreatorID":false,
            "SearchExact":false
          },
        }
      );
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
      writeToLog('[Error] '+`Error reading the directory: `+error.message);
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
      writeToLog('[Error] '+`Error reading the directory: `+error.message);
      console.error('Error reading the directory:', err);
      return;
    }

    const folders = files.filter(dirent => dirent.isDirectory()).map(dirent => dirent.name);

    let levels = [];
    let counter = 0;

    if (folders.length == 0) {
      // Handle no levels found scenario
      mainWindow.webContents.send("fromMain", {action:"currentLevelsInSMM1ProfileDir",levels:null, problem:"No Levels Found!"});
      return;
    }

    const sendQueue = []; // Queue to hold the send operations

    for (let i = 0; i < folders.length; i++) {
      smmCourseViewer.read(path.join(coursepath, folders[i], "course_data.cdt"), function(err, course, objects) {
        const levelObj = {
          folder: folders[i],
          course: course,
          objects: objects,
          levelid: folders[i],
          html: smmCourseViewer.course.getHtml(),
          name: ""
        };
        if (!err) {
          levelObj.name = course['name'];
        }
        levels.push(levelObj);
        counter++;

        if (counter === folders.length) {
          // When all levels are processed, send summary
          mainWindow.webContents.send("fromMain", {action:"currentLevelsInSMM1ProfileDir",levels:levels});

          // Queue the send operations
          levels.forEach((level, index) => {
            sendQueue.push(() => mainWindow.webContents.send("fromMain", {
              action: "displayCourse",
              coursehtml: level.html,
              levelid: level.levelid,
              course: level.course,
              objects: level.objects,
              fileName: folders[index]
            }));
          });

          // Process the queue at a rate of 10 messages per second
          const sendInterval = setInterval(() => {
            if (sendQueue.length > 0) {
              const sendOperation = sendQueue.shift();
              sendOperation();
            } else {
              clearInterval(sendInterval); // Stop the interval when all messages are sent
            }
          }, 1); // 1ms interval
        }
      });
    }
  });
}
function loadDownloadedCourses() {
  courseViewerExtract(outputDirectory);
}

function openFolder(folderPath) {
  shell.openPath(folderPath);
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

let operationQueue = Promise.resolve();

function removeKeyFromJSONFileSafe(keyToRemove) {
  operationQueue = operationQueue.then(() => new Promise((resolve, reject) => {
    fs.readFile(jsonDirectory + "/downloaded.json", 'utf8', (err, data) => {
      if (err) {
        writeToLog('[Error] '+`Error reading file from disk: `+error.message);
        console.error(`Error reading file from disk: ${err}`);
        reject(err);
      } else {
        let jsonObj = JSON.parse(data);
        if (jsonObj.hasOwnProperty(keyToRemove)) {
          delete jsonObj[keyToRemove];
          const updatedJSON = JSON.stringify(jsonObj, null, 2);
          fs.writeFile(jsonDirectory + "/downloaded.json", updatedJSON, 'utf8', (err) => {
            if (err) {
              writeToLog('[Error] '+`Error writing file: `+error.message);
              console.error(`Error writing file: ${err}`);
              reject(err);
            } else {
              resolve();
            }
          });
        } else {
          resolve();
        }
      }
    });
  }));

  return operationQueue;
}


function writeToLog(message) {
  const today = new Date();
  const dateString = `${today.getFullYear()}-${(today.getMonth() + 1).toString().padStart(2, '0')}-${today.getDate().toString().padStart(2, '0')}`;
  const logFileName = `log_${dateString}.txt`;
  if (!fs.existsSync(path.join(jsonDirectory, "logs"))){
    fs.mkdirSync(path.join(jsonDirectory, "logs"), { recursive: true });
    //console.log(`Created directory: ${outputDirectory}`);
  }
  const logFilePath = path.join(jsonDirectory, "logs", logFileName);

  fs.appendFile(logFilePath, message + "\n", (err) => {
    if (err) {
      writeToLog('[Error] '+`Error writing to file: `+err.message);
      console.error(`Error writing to file: ${err}`);
    }
  });
}

function deleteCourseFile(levelid) {
  //console.log(outputDirectory, levelid)
  const partFilePath = path.join(outputDirectory, levelid+"");
  fs.rm(partFilePath, { recursive: true, force: true }, (err) => {
      if (err) throw err;
      removeKeyFromJSONFileSafe(levelid+"")
      mainWindow.webContents.send("fromMain", {action:"courseFileDeleted",levelid:levelid});
      //console.log('File deleted successfully!');
    });
}

  ipcMain.on("toMain", (event, args) => {
    if (args.action === "select-folder") {
      selectFolder();
    } else if (args.action === "open-folder") {
      openFolder(args.path);
    } else if (args.action === "download-level") {
      const outputDirectoryPath = outputDirectory + "/" + args.levelID;
      useProxy = args.useProxy;

      if (folderExists(outputDirectoryPath)) {
        mainWindow.webContents.send("fromMain", {
          action: "download-info",
          resultType: 'ERROR',
          step: "initializing",
          levelid: args.levelID,
          info: "Level folder already exists."
        });
        return;
      }
    
      mainWindow.webContents.send("fromMain", {
        action: "download-info",
        resultType: 'INIT',
        step: "startingProcess",
        info: "Starting download process for level: " + args.levelID
      });
    
      // Add the download task to the queue
      downloadQueue.push({ url: args.url, levelID: args.levelID, levelObj: args.levelObj });
      queueEventEmitter.emit('newTask'); // Signal that a new task has been added
    
      // Ensure the queue processor is running
      if (!isProcessingQueue) {
        isProcessingQueue = true;
        processQueue().catch(error => {
          writeToLog('[Error] '+`Error processing download queue: `+error.message);
          console.error('Error processing download queue:', error);
          isProcessingQueue = false; // Reset the processing flag in case of error
        });
      }
    } else if (args.action === "save-settings") {
      if (args.refreshProxies == true) {
        loadProxiesFromFile(path.join(jsonDirectory,"/proxies.txt"));
      }
      saveSettings(args.settings);
    } else if (args.action === "search-level") {
      searchLevelInDB(args.searchTypes, args.searchPhrase);
    } else if (args.action === "exit-app") {
      app.quit();
    } else if (args.action === "openURL") {
      shell.openExternal(args.url);
    } else if (args.action === "openProxies") {
      shell.openExternal(path.join(jsonDirectory,"/proxies.txt"));
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
      //console.log(args.path, args.selectedProfile, folderExists(args.path));
      if (args.path != undefined && folderExists(args.path)) {
        loadExistingUserIDs(args.path)
      }
    }else if (args.action === "write-to-log") {
      writeToLog(args.message);
    } else if (args.action === "delete-course-file") {
      deleteCourseFile(args.levelid);
    }
  });

  //startProcess();

// Course URLS have to be retrieved from https://app.gigasheet.com/spreadsheet/courses-jsonl/1493e4c3_5fed_45cb_b189_2e1428df82d5 or a copy of this sheet

//const originalUrl = 'https://d2sno3mhmk1ekx.cloudfront.net/10.WUP_AMAJ_datastore/ds/1/data/00030699122-00001?Expires=1624406273&Signature=dvCQjz~tTf09havdMwRVryzz9MfRp16RF5NJWQa8k-wJiizAOlbmb9kMsT5Kv9j4-QJ1RzU1rwit4QFFTF8Jg7wRRwS0RVjENcuxy6wag-~v187HMsX3yMGRs8VxSx5Syem9ZxjTqGpBqRfm~71rQYKH~32vqDVTXR6IRtyOnKtAWfIikJK8Tk0jBQM~fFqv4OqqFCRhHRjFyp8hJPMaz8P5qIm~puSkJ0wUNvDKV0upwQw9RJiDABo1aRkcpW0QghK1xfQEEHCG4RVOn5Zng6rBNhSOLGcJe~K0bBffA~Y5kkgEbOl18c-BXXy3-z3hV-mnIcxRU9e6VNMo00M1Zw__&Key-Pair-Id=APKAJUYKVK3BE6ZPNZBQ'
//processUrl(originalUrl);


 // GitLens
 