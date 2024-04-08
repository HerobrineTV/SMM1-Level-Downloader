const { ipcRenderer } = window; // Use provided ipcRenderer in Electron apps
const settingsFile = ('../SMMDownloader/Data/data.json');
const downloadCache = ('../SMMDownloader/Data/downloaded.json');
const backupCache = ('../SMMDownloader/Data/backupped.json');
let SettingsData;
let lastLoadedDownloads = {};
const totalSteps = 12;
var currentStep = [];
var currentHTMLPage = "";
const drawQueue = [];
let currentLoadedLevels = [];
var isDeleteMode = false;
var isAllSelectedForDel = false;
let deleteArray = [];
var isloadingLevels = false;
var connerrorCooldown = false;
var imageUrls = [
    "../pictures/smmdownloader/WU-groundblock.png", 
    "../pictures/smmdownloader/WU-questionmarkblock.png", 
    "../pictures/smmdownloader/WU-noteblock.png",
    "../pictures/smmdownloader/WU-brickblock.png",
    "../pictures/smmdownloader/M1-groundblock.png", 
    "../pictures/smmdownloader/M1-questionmarkblock.png", 
    "../pictures/smmdownloader/M1-noteblock.png",
    "../pictures/smmdownloader/M1-brickblock.png",
];

// Function to change image source after each bounce
function changeImageSrc() {
    var images = document.querySelectorAll('.loading-container img');
    for (var i = 0; i < images.length; i++) {
      images[i].src = imageUrls[Math.random() * imageUrls.length | 0];
    }
}

var downloadingBar = {
    maxSteps: totalSteps, // Default max steps, can be overridden in initialize if needed
    
    initialize: function(maxSteps, levelid) {
      this.maxSteps = maxSteps;
      currentStep[levelid] = 0; // Initialize steps for this levelid
      this.updateBar(levelid); // Initialize the bar's state
    },

    addBar: function(levelid) {
        const barcontainer = document.getElementById(`downloadingBarContainer-${levelid}`);

        if (barcontainer) {
            barcontainer.style.display = 'block';
            if (barcontainer.children[0]) {
                barcontainer.children[0].style.width = 0 + '%';
            }
        }
    },
    
    incrementStep: function(levelid) {
      if (currentStep[levelid] < this.maxSteps) {
        currentStep[levelid]++;
        this.updateBar(levelid);
      }
    },

    updateWindow: function(levelid) {
        if (currentStep[levelid] && document.getElementById('levelID') && document.getElementById('levelID').innerHTML.includes(levelid)) {
            var progressPercentage = (currentStep[levelid] / this.maxSteps) * 100;
            const levelDisplayObjDownloadActions = document.getElementById(`download-actions`);
            var downloadingBarContainerWindow = document.getElementById(`downloadingBarContainerWindow`);
    
            if (!downloadingBarContainerWindow) {
                levelDisplayObjDownloadActions.innerHTML = `<div id="downloadingBarContainerWindow" class="downloadingBarContainerClass" style=""><div id="downloadingBarProgress"></div></div>`
                downloadingBarContainerWindow = document.getElementById(`downloadingBarContainerWindow`);
            } else {
                downloadingBarContainerWindow.style.display = 'block';
                downloadingBarContainerWindow.children[0].style.width = progressPercentage + '%';
        
                if (currentStep[levelid] == this.maxSteps) {
                    delay(1000).then(() => {
                        downloadingBarContainerWindow.innerHTML = `<p2>Download Complete</p2>`;
                        downloadingBarContainerWindow.style.display = 'contents';
                    })
                  }
            }
        }
    },
    
    updateBar: function(levelid) {
        if (currentStep[levelid]) {
            this.updateWindow(levelid)
            var progressPercentage = (currentStep[levelid] / this.maxSteps) * 100;
            const barcontainer = document.getElementById(`downloadingBarContainer-${levelid}`);
            
            if (barcontainer) {
                barcontainer.style.display = 'block';
                if (barcontainer.children[0]) {
                    barcontainer.children[0].style.width = progressPercentage + '%';
                }
          
                if (currentStep[levelid] == this.maxSteps) {
                  delay(1000).then(() => {
                      barcontainer.innerHTML = `<p2>Download Complete</p2>`;
                      barcontainer.style.display = 'contents';
                  })
                }
            }
        }
    },

    resetBar: function(levelid) {
        const barcontainer = document.getElementById(`downloadingBarContainer-${levelid}`);
            if (barcontainer) {
                barcontainer.innerHTML = ``;
                barcontainer.style.display = 'none';
            }
    },

    showError: function(levelid, error) {
        const barcontainer = document.getElementById(`downloadingBarContainer-${levelid}`);
        //console.log("Resetting bar for " + levelid + " because it of " + error);
            if (barcontainer) {
                barcontainer.innerHTML = `<p2 style="color: crimson">Failed to Download Level!</p2><br><p style="display: contents; color: crimson">Error: ${error}</p>`;
                barcontainer.style.display = 'contents';
                barcontainer.style.color = 'crimson';
            }
            //delay(60000).then(() => {
            //    this.resetBar(levelid);
            //})
    }
};

async function delay(ms) {
    return new Promise(resolve => setTimeout(resolve, ms));
  }

function CEMUcheckBoxChanged() {
    if (SettingsData.useCemuDir == false) {
        document.getElementById('optionalCEMU').innerHTML = `<h2>Select Cemu Folder:</h2><button onclick="selectFolder()">Select Folder</button><br><br>`
        setSetting("useCemuDir", true)
    } else if (SettingsData.useCemuDir == true) {
        document.getElementById('optionalCEMU').innerHTML = ``
        setSetting("useCemuDir", false)
    } else {
        document.getElementById('optionalCEMU').innerHTML = ``
        setSetting("useCemuDir", false)
    }
}

function proxyCheckBoxChanged() {
    if (SettingsData.useProxy == false) {
        document.getElementById('optionalProxy').innerHTML = `<h2>Open Proxy File</h2><button onclick="openProxiesFile()">Open</button><br><br>`
        setSetting("useProxy", true)
    } else if (SettingsData.useProxy == true) {
        document.getElementById('optionalProxy').innerHTML = ``
        setSetting("useProxy", false)
    } else {
        document.getElementById('optionalProxy').innerHTML = ``
        setSetting("useProxy", false)
    }
}

function overwriteCheckBoxChanged() {
    if (SettingsData.BackupLevels == false) {
        setSetting("BackupLevels", true)
    } else if (SettingsData.BackupLevels == true) {
        setSetting("BackupLevels", false)
    } else {
        setSetting("BackupLevels", false)
    }
}

function searchTextInputChanged() {
    if (document.getElementById('searchLevelText').value.trim() != "") {
        document.getElementById('searchLevel-button').style.display = ""
    } else {
        document.getElementById('searchLevel-button').style.display = "none"
    }
}

function formatTime(milliseconds) {
    const hours = Math.floor(milliseconds / 3600000);
    const minutes = Math.floor((milliseconds % 3600000) / 60000);
    const seconds = Math.floor((milliseconds % 60000) / 1000);
    const ms = milliseconds % 1000;

    return `${padZero(hours)}:${padZero(minutes)}:${padZero(seconds)}:${padZero(ms, 3)}`;
}

function padZero(num, length = 2) {
    return String(num).padStart(length, '0');
}

function loadFileInWindow(levelObj) {
    const modal = document.getElementById("myModal");
    const modalContent = document.querySelector(".modal-content");
    const levelInfo = document.getElementById("levelInfo");
    //console.log(levelObj);
    checkIfLevelisAlreadyDownloaded(levelObj.levelid);

    // Modified part to include the custom dropdown instead of <select>
    levelInfo.innerHTML = `
    <div class="level-info">
        <h2 id="levelName">${levelObj.name}</h2>
    </div>
    <div class="level-details">
        <p><strong>Uploader:</strong> <span id="uploader">${levelObj.creator || null}</span></p>
        <p><strong>Upload Time:</strong> <span id="uploadTime">${levelObj.uploadTime || null}</span></p>
        <p><strong>Level ID:</strong> <span id="levelID">${levelObj.levelid}</span></p>
    </div>
    <div class="level-stats">
        <p><strong>Clear Rate:</strong> <span id="clearRate">${(levelObj.clearrate*100).toFixed(2).replace(/(\.0+|(\.\d+?)0+)$/, '$2') || "0.00%"}%</span></p>
        <p><strong>Total Attempts:</strong> <span id="totalAttempts">${levelObj.total_attempts || 0}</span></p>
        <p><strong>Completions:</strong> <span id="completions">${levelObj.clears || 0}</span></p>
        <p><strong>Record Time:</strong> <span id="recordTime">${formatTime(levelObj.world_record_ms) || NaN}</span></p>
        <p><strong>Record Holder:</strong> <span id="recordHolder">${levelObj.world_record_holder_nnid || null}</span></p>
    </div>
    <div class="actions">
        <div id="download-actions" class="custom-select">
            <div class="selected-option">Load_to_Course</div>
            <div class="options-container">
                <div class="option">course000</div>
                <div class="option">course001</div>
                <div class="option">course002</div>
                <div class="option">course003</div>
                <div class="option">course004</div>
                <div class="option">course005</div>
            </div>
            <div id="download-actions-button-1">
                <button class="searchdownload-btn">Download</button>
            </div>
        </div>
    </div>
    `;

    // Add event listeners for the custom dropdown
    setupCustomDropdown();

    document.getElementsByClassName("searchdownload-btn")[0].addEventListener("click", () => {runLevelDownloader(levelObj)})
    downloadingBar.updateWindow(levelObj.levelid);

    if (SettingsData.useCemuDir == false || SettingsData.CemuDirPath == "") {
        if (document.getElementsByClassName("selected-option")[0]) {
            document.getElementsByClassName("selected-option")[0].style.display = "none";
        }
    } else {
        if (document.getElementsByClassName("selected-option")[0]) {
            document.getElementsByClassName("selected-option")[0].style.display = "block";   
        }
    }

    modal.style.display = "block";

    const closeModal = () => {
        modal.style.display = "none";
    };

    const span = document.getElementsByClassName("close")[0];

    span.onclick = closeModal;

    window.onclick = function (event) {
        if (event.target == modal) {
            closeModal();
        }
    };
}

function setupCustomDropdown() {
    const selected = document.querySelector('.selected-option');
    const optionsContainer = document.querySelector('.options-container');
    const optionsList = document.querySelectorAll('.option');

    selected.addEventListener('click', () => {
        optionsContainer.style.display = optionsContainer.style.display === 'block' ? 'none' : 'block';
    });

    optionsList.forEach(option => {
        option.addEventListener('click', (e) => {
            selected.innerHTML = e.target.innerHTML;
            optionsContainer.style.display = 'none'; // Hide options after selection
        });
    });
}

function selectAllToDelete() {
    if (isDeleteMode == true) {
        const valuesArray = Object.values(lastLoadedDownloads);
        if (isAllSelectedForDel == false) {	
            isAllSelectedForDel = true;
            deleteArray = [];
            for (let i = 0; i < valuesArray.length; i++) {
                //deleteArray.push(valuesArray[i].levelid);
                markLevelAsDeleting(valuesArray[i]);
            }
        } else {
            isAllSelectedForDel = false;
            deleteArray = [];
            for (let i = 0; i < valuesArray.length; i++) {
                const clickedLevelObj = document.getElementById(`object-${valuesArray[i].levelid}`);
                if (clickedLevelObj) {
                    clickedLevelObj.style.backgroundColor = "";
                }
            }
        }
    }
}

function deleteSelected() {
    if (isDeleteMode == true) {
        if (deleteArray.length == 0) {
            return;
        }
        for (let i = 0; i < deleteArray.length; i++) {
            deleteLevel(deleteArray[i]);
        }
        changeDeleteMode()
    }
}

function changeDeleteMode() {
    const confirmbtn = document.getElementById('confirmmultidelete-btn')
    const scrollFrame = document.getElementById('scrollable-objects')
    const selectallbtn = document.getElementById('selectallmultidelete-btn')
    const cancelbtn = document.getElementById('cancelmultidelete-btn')
    const multideletebtn = document.getElementById('multidelete-btn')
    if (isDeleteMode == false) {
        deleteArray = [];
        isDeleteMode = true;
        if (multideletebtn) {
            multideletebtn.style.display = "none";
        }
        if (cancelbtn) {
            cancelbtn.style.display = "";
        }
        if (confirmbtn) {
            confirmbtn.style.display = "";
        }
        if (selectallbtn) {
            selectallbtn.style.display = "";
        }
        if (scrollFrame) {
            scrollFrame.style.borderColor = "red";
        }
    } else {
        for (let i = 0; i < deleteArray.length; i++) {
            const clickedLevelObj = document.getElementById(`object-${deleteArray[i]}`);
            if (clickedLevelObj) {
                clickedLevelObj.style.backgroundColor = "";
            }
        }
        deleteArray = [];
        isDeleteMode = false;
        isAllSelectedForDel = false;
        if (multideletebtn) {
            multideletebtn.style.display = "";
        }
        if (cancelbtn) {
            cancelbtn.style.display = "none";
        }
        if (confirmbtn) {
            confirmbtn.style.display = "none";
        }
        if (selectallbtn) {
            selectallbtn.style.display = "none";
        }
        if (scrollFrame) {
            scrollFrame.style.borderColor = "rgb(204, 204, 204)";
        }
    }
}

function markLevelAsDeleting(levelObj){
    const clickedLevelObj = document.getElementById(`object-${levelObj.levelid}`);
    //console.log(levelObj.levelid, deleteArray)
    if (deleteArray.includes(parseInt(levelObj.levelid))) {
        //console.log("NotInArray")
        const index = deleteArray.indexOf(parseInt(levelObj.levelid));
        if (index > -1) {
            deleteArray.splice(index, 1);
        }
        if (clickedLevelObj) {
            clickedLevelObj.style.backgroundColor = "";
        }
    } else {
        //console.log("InArray")
        deleteArray.push(parseInt(levelObj.levelid));
        if (deleteArray.length == Object.values(lastLoadedDownloads).length) {
            isAllSelectedForDel = true;
        }
        if (clickedLevelObj) {
            clickedLevelObj.style.backgroundColor = "rosybrown";
        }
    }
}

function objectClicked(levelid, levelObj) {
    //console.log("Clicked")
    if (isDeleteMode == false) {
        loadFileInWindow(levelObj);
    } else {
        isAllSelectedForDel = false;
        markLevelAsDeleting(levelObj);
    }
}

function addObjects(levels) {
    const objectsContainer = document.getElementById('scrollable-objects');
    if (SettingsData.currentPage == 1){objectsContainer.innerHTML = '';}
    //console.log(levels);
    levels.forEach(obj => {
        const objectDiv = document.createElement('div');
        if (currentHTMLPage == "main") {
            checkIfLevelisAlreadyDownloaded(obj.levelid);
        }
        objectDiv.id = `object-${obj.levelid}`;
        objectDiv.classList.add('object');
        objectDiv.classList.add('searchable');
        objectDiv.setAttribute('data-name', obj.name);
        if (obj.folder) {
            objectDiv.innerHTML = ``
            +`<div id="courseName-${obj.levelid}">${obj.name}</div>`
            +`<div></div>`
            +`<div id="courseDisplay-${obj.levelid}"></div>`
            +`<div id="courseInfoDisplay-${obj.levelid}"></div>`
        //    +`<div>Course Folder: ${obj.folder}</div>`
            +`<div class="downloaded-display"></div>`
            +`<div id="downloadingBarContainer-${obj.levelid}" class="downloadingBarContainerClass" style=""><div id="downloadingBarProgress"></div></div>`;
        } else {
            objectDiv.innerHTML = `
            <div id="courseName-${obj.levelid}">${obj.name}</div>
            <div>Stars: ${obj.stars}</div>
            <div>Creator: ${obj.creator}</div>
            <div>Clear Rate: ${(obj.clearrate*100).toFixed(2).replace(/(\.0+|(\.\d+?)0+)$/, '$2')}%</div>
            <div class="downloaded-display"></div>
            <div id="downloadingBarContainer-${obj.levelid}" class="downloadingBarContainerClass" style=""><div id="downloadingBarProgress"></div></div>
            `;
            objectDiv.addEventListener('click', () => objectClicked(obj.levelid, obj));
        }
        objectsContainer.appendChild(objectDiv);
    });
}

function searchDivs(searchQuery, parentId) {
    // Get the parent div using its ID
    var parentDiv = document.getElementById(parentId);

    // Get all child divs within the parent div
    var childDivs = parentDiv.getElementsByClassName('searchable');

    // Loop through each child div
    for (var i = 0; i < childDivs.length; i++) {
        var currentDiv = childDivs[i];

        // If the current div does not contain the search query in its data-name attribute or innerText
        if (!currentDiv.getAttribute('data-name').toLowerCase().includes(searchQuery.toLowerCase()) && !currentDiv.innerText.toLowerCase().includes(searchQuery.toLowerCase())) {
            // Make the div invisible
            currentDiv.style.display = 'none';
        } else {
            // Else, make sure the div is visible (in case it was previously hidden)
            currentDiv.style.display = '';
        }
    }
}

function searchCheckBoxesChanged(searchType){
    if (searchType == "LevelName") {
        if (SettingsData.searchParams.LevelName == false) {
            setSubSetting("searchParams", "LevelName", true)
            increaseSettingvalue("amounttrue", 1)
        } else if (SettingsData.searchParams.LevelName == true) {
            setSubSetting("searchParams", "LevelName", false)
            decreaseSettingvalue("amounttrue", 1)
        } else {
            setSubSetting("searchParams", "LevelName", false)
            decreaseSettingvalue("amounttrue", 1)
        }
    }
    
    if (searchType == "LevelID") {
        if (SettingsData.searchParams.LevelID == false) {
            setSubSetting("searchParams", "LevelID", true)
            increaseSettingvalue("amounttrue", 1)
        } else if (SettingsData.searchParams.LevelID == true) {
            setSubSetting("searchParams", "LevelID", false)
            decreaseSettingvalue("amounttrue", 1)
        } else {
            setSubSetting("searchParams", "LevelID", false)
            decreaseSettingvalue("amounttrue", 1)
        }
    }

    if (searchType == "CreatorName") {
        if (SettingsData.searchParams.CreatorName == false) {
            setSubSetting("searchParams", "CreatorName", true)
            increaseSettingvalue("amounttrue", 1)
        } else if (SettingsData.searchParams.CreatorName == true) {
            setSubSetting("searchParams", "CreatorName", false)
            decreaseSettingvalue("amounttrue", 1)
        } else {
            setSubSetting("searchParams", "CreatorName", false)
            decreaseSettingvalue("amounttrue", 1)
        }
    }

    if (searchType == "CreatorID") {
        if (SettingsData.searchParams.CreatorID == false) {
            setSubSetting("searchParams", "CreatorID", true)
            increaseSettingvalue("amounttrue", 1)
        } else if (SettingsData.searchParams.CreatorID == true) {
            setSubSetting("searchParams", "CreatorID", false)
            decreaseSettingvalue("amounttrue", 1)
        } else {
            setSubSetting("searchParams", "CreatorID", false)
            decreaseSettingvalue("amounttrue", 1)
        }
    }

    if (searchType == "SearchExact") {
        if (SettingsData.searchParams.SearchExact == false) {
            setSubSetting("searchParams", "SearchExact", true)
        } else if (SettingsData.searchParams.SearchExact == true) {
            setSubSetting("searchParams", "SearchExact", false)
        } else {
            setSubSetting("searchParams", "SearchExact", false)
        }
    }

    if (SettingsData.amounttrue && SettingsData.amounttrue > 0 && document.getElementById("searchLevel-item").innerHTML == ``) {
        document.getElementById("searchLevel-item").innerHTML = `<label class="searchLevel-label" for="searchLevelText">Search: </label><input type="text" id="searchLevelText" name="searchLevel-option" value="" oninput="searchTextInputChanged()">`
    } else if (SettingsData.amounttrue && SettingsData.amounttrue <= 0) {
        document.getElementById("searchLevel-item").innerHTML = ``
    } else if (!SettingsData.amounttrue) {
        document.getElementById("searchLevel-item").innerHTML = ``
    }
}

function transformToDict(array) {
    const result = {};
    array.forEach(obj => {
        result[obj.levelid] = obj;
    });
    return result;
}

function onScrollToBottom() {
    if (isloadingLevels == true) {return;}
    if (connerrorCooldown == true) {return;}
    lazyLevelLoading(SettingsData.currentPage + 1);
}

function findRandomLevel() {
    const ranID = getRandomInt(12000000)

    SettingsData.currentPage = 1;
    const apiUrl = `${SettingsData.APILink}/searchLevels/${encodeURIComponent(ranID)}/1`
    +`?coursename=0`
    +`&courseid=1`
    +`&creatorname=0`
    +`&creatorid=0`
    +`&searchexact=0`;
        
        // Make a GET request to the API
        fetch(apiUrl)
            .then(response => {
                // Check if the response is successful
                if (!response.ok) {
                    throw new Error('Network response was not ok');
                }
                // Parse the JSON response
                return response.json();
            })
            .then(data => {
                // Handle the JSON data from the API
                //console.log(data); // You can process the data here
                if (SettingsData.currentPage == 1) {
                    setSetting("recentFoundLevels", transformToDict(data));
                    currentLoadedLevels = data;
                } else {
                    setSetting("recentFoundLevels", {...transformToDict(data),...SettingsData.recentFoundLevels});
                    currentLoadedLevels = [...currentLoadedLevels, ...data];
                }
                if (data.length > 0) {
                    // SettingsData.recentFoundLevels
                    document.getElementById("downloadAllLevel-button").style.display = ""
                    document.getElementById("selectRandomLevel-button").style.display = ""
                }
                displayLevels(data);
            })
            .catch(error => {
                // Handle errors that occur during the fetch
                window.api.send("toMain", {action:"write-to-log", message:'There was a problem with the fetch operation: '+error});
                console.error('There was a problem with the fetch operation:', error);
            });
}

function getRandomInt(max) {
    return Math.floor(Math.random() * max) || 0;
}

function showRandomLevel() {
    var recentLevelsArr = currentLoadedLevels;
    var arrayIndex = getRandomInt(recentLevelsArr.length);
    if (arrayIndex < 0) {arrayIndex = 0;}
    //console.log(recentLevelsArr)
    //console.log(recentLevelsArr[arrayIndex])
    loadFileInWindow(recentLevelsArr[arrayIndex])
    //runLevelDownloader(SettingsData.recentFoundLevels[arrayIndex])
}

function lazyLevelLoading(page) {
    if (isloadingLevels == true) {return;}
    if (connerrorCooldown == true) {return;}
    if (page === 1) {
        document.getElementById("scrollable-objects").innerHTML = "";
    }
    changeImageSrc()
    isloadingLevels = true
    SettingsData.currentPage = page;
    const loadingholder = document.getElementById("loadingholder").cloneNode(true);
    const apiUrl = `${SettingsData.APILink}/searchLevels/${encodeURIComponent(SettingsData.lastSearchPhrase)}/${page}`
    +`?coursename=${SettingsData.searchParams.LevelName == true? 1 : 0}`
    +`&courseid=${SettingsData.searchParams.LevelID == true? 1 : 0}`
    +`&creatorname=${SettingsData.searchParams.CreatorName == true? 1 : 0}`
    +`&creatorid=${SettingsData.searchParams.CreatorID == true? 1 : 0}`
    +`&searchexact=${SettingsData.searchParams.SearchExact == true? 1 : 0}`;

    loadingholder.style.display = "";
    document.getElementById("scrollable-objects").appendChild(loadingholder);
        // Make a GET request to the API
        fetch(apiUrl)
            .then(response => {
                // Check if the response is successful
                if (!response.ok) {
                    throw new Error('Network response was not ok');
                }
                // Parse the JSON response
                return response.json();
            })
            .then(data => {
                // Handle the JSON data from the API
                //console.log(data); // You can process the data here
                if (SettingsData.currentPage == 1) {
                    setSetting("recentFoundLevels", transformToDict(data));
                    currentLoadedLevels = data;
                } else {
                    setSetting("recentFoundLevels", {...transformToDict(data),...SettingsData.recentFoundLevels});
                    currentLoadedLevels = [...currentLoadedLevels, ...data];
                }
                if (data.length > 0) {
                    // SettingsData.recentFoundLevels
                    document.getElementById("downloadAllLevel-button").style.display = ""
                    document.getElementById("selectRandomLevel-button").style.display = ""
                }
                loadingholder.style.display = "none";
                isloadingLevels = false;
                displayLevels(data);
            })
            .catch(error => {
                if (page > 1) {
                    SettingsData.currentPage--
                }
                connerrorCooldown = true;
                // Handle errors that occur during the fetch
                window.api.send("toMain", {action:"write-to-log", message:'There was a problem with the fetch operation: '+error});
                console.error('There was a problem with the fetch operation:', error);
                loadingholder.style = "font-size: x-large;"
                loadingholder.style.color = "red";
                loadingholder.textContent = "Couldn't connect to the server. Please Check your Connection or try again later.";
                isloadingLevels = false;
                delay(5000).then(() => {
                    connerrorCooldown = false;
                    loadingholder.remove();
                })
            });
}

async function downloadAll() {
    var levels = []
    var count = 0
    for (const [key, value] of Object.entries(SettingsData.recentFoundLevels)) {
        
        runLevelDownloader(value)
        /*
        levels[count] = value;
        count++;
        
        if (count >= 10) {
            for (var i = 0; i < levels.length; i++) {
                runLevelDownloader(levels[i])
                //console.log(count, value);
            }
            //await delay(1000).then(() => {
                count = 0;
                levels = [];
            //});
        }*/
    }
}

function searchLevel() {
    // Get the search phrase from the input field
    const searchPhrase = document.getElementById('searchLevelText').value.trim();
    
    // Set the last search phrase in settings
    setSetting("lastSearchPhrase", searchPhrase);
    
    // Check if the API link should be used
    if (SettingsData.useAPILink) {
        isloadingLevels = false;
        connerrorCooldown = false;
        lazyLevelLoading(1)
    }
}

function selectFolder() {
    window.api.send("toMain", {action:"select-folder"});
}

function runLevelDownloader(levelObj) {

    downloadingBar.addBar(levelObj.levelid);

    const levelDisplayObjDownloadActions = document.getElementById(`download-actions`)
    if (levelDisplayObjDownloadActions) {
        levelDisplayObjDownloadActions.innerHTML = `<p>Downloading...</p>`
    }

    link = levelObj.url;
    levelID = levelObj.levelid;
    currentStep[levelObj.levelid] = 1;
    window.api.send("toMain", {action:"download-level", url:link, levelID:levelID, levelObj:levelObj, useProxy:SettingsData.useProxy});
}

function loadPage(page) {
    if (page != "../pages/settings.html") {
        saveSettingsOnSettingsExit();
    }
    //saveSettings();
    fetch(page)
        .then(response => response.text())
        .then(data => {
            document.getElementById('main-content').innerHTML = data;
            loadPageScripts(page);
        })
        .catch(error => console.log(error));
}

function loadPageScripts(page) {
    if (page == "../pages/settings.html") {
        deleteArray = [];
        isDeleteMode = false;
        isAllSelectedForDel = false;
        currentHTMLPage = "settings"
        if (SettingsData.useCemuDir == true) {
            window.api.send("toMain", {action:"get-smm1-profiles", path:SettingsData.CemuDirPath});
            document.getElementById("useCEMUfolder").checked = true;
            document.getElementById('optionalCEMU').innerHTML = `<h2>Select Cemu Folder:</h2><button onclick="selectFolder()">Select Folder</button><br><br>`
        }
        if (SettingsData.useProxy == true) {
            document.getElementById("useProxy").checked = true;
            document.getElementById('optionalProxy').innerHTML = `<h2>Open Proxy File</h2><button onclick="openProxiesFile()">Open</button><br><br>`
        }
        if (SettingsData.BackupLevels == true) {
            document.getElementById("autoBackupLevels").checked = true;
        }
    } else if (page == "../pages/main.html") {
        deleteArray = [];
        isDeleteMode = false;
        isAllSelectedForDel = false;
        currentHTMLPage = "main"
        amounttrue = 0;
        if(SettingsData.searchParams.LevelName == true) {
            document.getElementById("usesearchLevelbyLevelName").checked = true;
            amounttrue++;
        }
        if(SettingsData.searchParams.LevelID == true) {
            document.getElementById("usesearchLevelbyLevelID").checked = true;
            amounttrue++;
        }
        if(SettingsData.searchParams.CreatorName == true) {
            document.getElementById("usesearchLevelbyCreatorName").checked = true;
            amounttrue++;
        }
        if(SettingsData.searchParams.CreatorID == true) {
            document.getElementById("usesearchLevelbyCreatorID").checked = true;
            amounttrue++;
        }
        if(SettingsData.searchParams.SearchExact == true) {
            document.getElementById("useseExactSearch").checked = true;
        }

        if (amounttrue > 0) {
            document.getElementById("searchLevel-item").innerHTML = `<label class="searchLevel-label" for="searchLevelText">Search: </label><input type="text" id="searchLevelText" name="searchLevel-option" value="">`
            const source = document.getElementById('searchLevelText');

            source.addEventListener('input', searchTextInputChanged);
            source.addEventListener('propertychange', searchTextInputChanged);
        } else {
            document.getElementById("searchLevel-item").innerHTML = ``
        }

        var scrollableElement = document.getElementById('scrollable-objects');

        scrollableElement.addEventListener('scroll', function() {
            if (scrollableElement.scrollTop + scrollableElement.clientHeight >= scrollableElement.scrollHeight) {
                onScrollToBottom();
            }
        });

        setSetting("amounttrue", amounttrue);
    } else if (page == "../pages/savedLevels.html") {
        deleteArray = [];
        isDeleteMode = false;
        isAllSelectedForDel = false;
        currentHTMLPage = "savedLevels"
        document.getElementById('searchSavedLevel-button').addEventListener("click", () => {
            searchDivs(document.getElementById('searchSavedLevelText').value.trim(), 'scrollable-objects');
        })
        loadSavedLevels();
    }
}

function loadSavedLevels(){
    const savedLevelsDrop = document.getElementById('savedlevelsDropdown')
    const confirmbtn = document.getElementById('confirmmultidelete-btn')
    const selectallbtn = document.getElementById('selectallmultidelete-btn')
    const cancelbtn = document.getElementById('cancelmultidelete-btn')
    const multideletebtn = document.getElementById('multidelete-btn')
    const _downloadedFolderbtn = document.getElementById('openfolderdownload-btn')
    const _backuppedFolderbtn = document.getElementById('openfolderbackup-btn')
    const _cemuFolderbtn = document.getElementById('openfoldercemu-btn')

    if (multideletebtn) {
        multideletebtn.style.display = "";
    }
    if (cancelbtn) {
        cancelbtn.style.display = "none";
    }
    if (confirmbtn) {
        confirmbtn.style.display = "none";
    }
    if (selectallbtn) {
        selectallbtn.style.display = "none";
    }

    if (savedLevelsDrop.value == "downloaded") {
        deleteArray = [];
        isDeleteMode = false;
        isAllSelectedForDel = false;
        _downloadedFolderbtn.style.display = ""
        _backuppedFolderbtn.style.display = "none"
        _cemuFolderbtn.style.display = "none"
        loadDownloadedLevels()
    } else if (savedLevelsDrop.value == "cemu") {
        deleteArray = [];
        isDeleteMode = false;
        isAllSelectedForDel = false;
        _downloadedFolderbtn.style.display = "none"
        _backuppedFolderbtn.style.display = "none"
        if (SettingsData.CemuDirPath != "") {
            _cemuFolderbtn.style.display = ""
        }
        loadLevelsfromCEMU()
    } else if (savedLevelsDrop.value == "backupped") {
        deleteArray = [];
        isDeleteMode = false;
        isAllSelectedForDel = false;
        _downloadedFolderbtn.style.display = "none"
        _backuppedFolderbtn.style.display = ""
        _cemuFolderbtn.style.display = "none"
        loadBackuppedLevels()
    }
}

function openFolderDownloaded() {
    var ParentDir = window.location.href.substring(0, window.location.href.lastIndexOf('/'));
    ParentDir = ParentDir.substring(0, ParentDir.lastIndexOf('/'));
    window.api.send("toMain", {action:"open-folder", path: ParentDir+"/SMMDownloader/Data/DownloadCache"})
}

function openFolderBackupped() {
    var ParentDir = window.location.href.substring(0, window.location.href.lastIndexOf('/'));
    ParentDir = ParentDir.substring(0, ParentDir.lastIndexOf('/'));
    window.api.send("toMain", {action:"open-folder", path: ParentDir+"/SMMDownloader/Data/BackupCache"})
}

function openFolderCemu() {
    window.api.send("toMain", {action:"open-folder", path:SettingsData.CemuDirPath})
}

function increaseSettingvalue(val, amount) {
    setSetting(val, SettingsData[val] + amount);
}

function decreaseSettingvalue(val, amount) {
    if (SettingsData[val] > 0) {
        setSetting(val, SettingsData[val] - amount);
    }
}

function setSetting(settingsKey, settingsValue) {
    SettingsData[settingsKey] = settingsValue;
    if (settingsKey != "amounttrue") {
        saveSettings();
    }
}

function setSubSetting(settingsKey1, settingsKey2, settingsValue) {
    SettingsData[settingsKey1][settingsKey2] = settingsValue;
    saveSettings();
}

function resetSettings() {
    window.api.send("toMain", {action:"save-settings", settings:"RESET"});
}

function saveSettings() {
    window.api.send("toMain", {action:"save-settings", settings:SettingsData, refreshProxies:false});
}

function saveSettingsOnSettingsExit() {
    window.api.send("toMain", {action:"save-settings", settings:SettingsData, refreshProxies:true});
}

function loadSettings() {
    fetch(settingsFile)
        .then(response => response.json())
        .then(data => {
            SettingsData = data;
        })
        .catch(error => console.log(error));
}

function objectToArray(obj) {
    // Check if obj is not an object, return obj directly
    if (typeof obj !== 'object' || obj === null) {
        return obj;
    }

    // Check if obj is an array, iterate through its elements
    if (Array.isArray(obj)) {
        return obj.map(item => objectToArray(item));
    }

    // Return only the values of the object
    return Object.values(obj);
}

function raiseDownloadBar(levelid, state){
    if (state == "INIT") {
        downloadingBar.initialize(totalSteps, levelid);
    } else {
        downloadingBar.incrementStep(levelid);
    }
}

function loadDownloadedLevels() {
    fetch(downloadCache)
        .then(response => response.json())
        .then(data => {
            //console.log(data.length);
            if (data && Object.keys(data).length > 0) {
                lastLoadedDownloads = data;
                // loadDownloadedLevels()
                window.api.send("toMain", {action:"get-smm1-cached-downloads"});
                //console.log(objectToArray(data))
                displayLevels(objectToArray(data));
            } else {
                const levellistObj = document.getElementById('scrollable-objects');
                levellistObj.innerHTML = `<h2>Error</h2>Download List is Empty! <br> U need to Download Levels First.<br><br>`
            }
        })
        .catch(error => console.log(error));
}

function loadLevelsfromCEMU() {
    const levellistObj = document.getElementById('scrollable-objects');
    if (SettingsData.useCemuDir == true) {
        if (SettingsData.CemuDirPath!= "") {
            window.api.send("toMain", {action:"get-smm1-courses", path:SettingsData.CemuDirPath, selectedProfile: SettingsData.selectedProfile});
        } else {
            levellistObj.innerHTML = `<h2>Error</h2>Cemu Path Not Found or not Set! <br> Please set the Cemu Path in the settings.<br><br>`
        }
    } else {
        levellistObj.innerHTML = `<h2>Error</h2>Using Cemu Folder is disabled! <br> Please enable it in the settings.<br><br>`
    }
}

function loadBackuppedLevels() {
    const levellistObj = document.getElementById('scrollable-objects');
    if (SettingsData.BackupLevels == true) {
        fetch(backupCache)
        .then(response => response.json())
        .then(data => {

            if (data) {
                displayLevels(objectToArray(data));
            } else {
                levellistObj.innerHTML = `<h2>Error</h2>Backup List is Empty! <br> U need to Replace Levels first.<br><br>`
            }
        })
        .catch(error => console.log(error));
    } else {
        levellistObj.innerHTML = `<h2>Error</h2>Backups are Disabled! <br> Please enable it in the settings.<br><br>`
    }
}

function displayLevels(levels) {
    addObjects(levels);
}

function exitApp() {
    window.api.send("toMain", {action:"exit-app"});
}

function openURL(url) {
    window.api.send("toMain", {action:"openURL", url:url});
}

function openProxiesFile() {
    window.api.send("toMain", {action:"openProxies"});
}

function deleteLevel(levelid) {
    window.api.send("toMain", {action:"delete-course-file", levelid:levelid});
}

function checkIfLevelisAlreadyDownloaded(levelid){
    //console.log(levelid);
    window.api.send("toMain", {action:"checkIfAlreadyDownloaded", levelID:levelid});
}

function changedProfileDropdown(){
    const currentProfile = document.getElementById('profile-Dropdown').value;
    setSetting("selectedProfile", currentProfile);
}

function enqueueDrawTask(levelid, course, objects) {
    if (document.getElementById(`courseDisplay-${levelid}`)!= null) {
        drawQueue.push({ levelid, course, objects });
    }
}

function drawLevel(levelid, course, objects) {
    if (document.getElementById(`courseDisplay-${levelid}`)!= null) {
        new Draw(`courseDisplay-${levelid}`, course, objects, "8"); //32 64
    }
}

setInterval(() => {
    if (drawQueue.length > 0) {
        const tasksToDraw = drawQueue.splice(0, 10); // Get up to 10 tasks to draw
        tasksToDraw.forEach(task => {
            drawLevel(task.levelid, task.course, task.objects);
        });
    }
}, 100);

window.addEventListener('DOMContentLoaded', () => {
    window.api.receive("fromMain", (data) => {
        if (data.action == "selectedFolder") {
            setSetting("CemuDirPath", data.path);
        }
        if (data.action == "saveing") {
            //console.log(data.result)
        }
        //TODO: add a mark if it is a mass download (A Download for every level in whole SMM1)
        if (data.action == "download-info") {
            if (data.resultType == "INIT") {
                //console.log(data.resultType);
                raiseDownloadBar(data.levelid, data.resultType);
            } else if (data.resultType == "SUCCESS") {
                //console.log(data.resultType);
                //console.log("["+data.levelid+"] "+ currentStep[data.levelid] +" / "+ totalSteps, data.info)
                raiseDownloadBar(data.levelid,  data.resultType);
                currentStep[data.levelid] = null;
                const levelHTMLObj = document.getElementById(`object-${data.levelid}`)
                const levelDisplayObjDownloadActions = document.getElementById(`download-actions-button-1`)
                if (levelHTMLObj && currentHTMLPage == "main") {
                    const barcontainer = document.getElementById(`downloadingBarContainer-${data.levelid}`);
                    if (barcontainer) {
                        barcontainer.style.display = 'contents';
                        barcontainer.style.backgroundColor = '';
                        barcontainer.innerHTML = `<p2>Already Downloaded</p2>`
                    }
                }
                //console.log("TEST")
                if (levelDisplayObjDownloadActions && document.getElementById('levelID') && document.getElementById('levelID').innerHTML.includes(data.levelid)) {
                    levelDisplayObjDownloadActions.innerHTML = `<button class="deletedownload-btn">Delete</button>`
                    document.getElementsByClassName("deletedownload-btn")[0].addEventListener("click", () => {deleteLevel(data.levelid)})
                }
                //console.log("TEST2")
            } else if (data.resultType == "IN_PROGRESS") {
                //console.log(data.resultType);
                //console.log("["+data.levelid+"] "+ currentStep[data.levelid] +" / "+ totalSteps, data.info)
                raiseDownloadBar(data.levelid, data.resultType);
            } else if (data.resultType == "ERROR") {
                if (data.step == "initializing") {
                    const barcontainer = document.getElementById(`downloadingBarContainer-${data.levelid}`);
                    if (barcontainer) {
                        barcontainer.style.display = 'contents';
                        barcontainer.style.backgroundColor = '';
                        barcontainer.innerHTML = `<p2>Already Downloaded</p2>`
                    }
                } else {
                    downloadingBar.showError(data.levelid, data.info)
                }
                //console.log(data.resultType);
                currentStep[data.levelid] = null;
                //console.log("["+data.levelid+"] "+ data.info)
            }
        }
        if (data.action == "checkIfAlreadyDownloaded-info") {
            //console.log(data)
            if (data.answer == true){
                const levelHTMLObj = document.getElementById(`object-${data.levelid}`)
                const levelDisplayObjDownloadActions = document.getElementById(`download-actions-button-1`)
                if (levelHTMLObj && currentHTMLPage == "main") {
                    const barcontainer = document.getElementById(`downloadingBarContainer-${data.levelid}`);
                    if (barcontainer) {
                        barcontainer.style.display = 'contents';
                        barcontainer.style.backgroundColor = '';
                        barcontainer.innerHTML = `<p2>Already Downloaded</p2>`
                    }
                }
                if (levelDisplayObjDownloadActions && document.getElementById('levelID') && document.getElementById('levelID').innerHTML.includes(data.levelid)) {
                    levelDisplayObjDownloadActions.innerHTML = `<button class="deletedownload-btn">Delete</button>`
                    document.getElementsByClassName("deletedownload-btn")[0].addEventListener("click", () => {deleteLevel(data.levelid)})
                }
            }
        }
        if (data.action == "courseFileDeleted") {
            if (document.getElementById('levelID')) {
                if (document.getElementById('levelID').innerHTML.includes(data.levelid)) {
                    document.getElementById("myModal").style.display = 'none';
                }
            }
            document.getElementById(`object-${data.levelid}`).remove();
        }
        if (data.action == "currentUsersInSMM1Dir"){
            //console.log(data.users)
            const profileselectionDropdown = document.getElementById('profileselection');
            var buildHTML = "";
            buildHTML = `<h2>Select Profile</h2>`;
            buildHTML += `<select id="profile-Dropdown" onchange="changedProfileDropdown()" name="profileselection-option">`;
            
            data.users.forEach(user => {
                // Check if the current user is the selected profile and set the selected attribute accordingly
                const isSelected = user === SettingsData.selectedProfile ? 'selected' : '';
                buildHTML += `<option value="${user}" ${isSelected}>${user}</option>`;
            });
            
            buildHTML += `</select>`;
            profileselectionDropdown.innerHTML = buildHTML;
        }
        if (data.action == "currentLevelsInSMM1ProfileDir") {
            if (data.levels == null) {
                document.getElementById('scrollable-objects').innerHTML = `<h2>${data.problem}</h2>`
                return;
            }
            document.getElementById('scrollable-objects').innerHTML = `<h2></h2>`;
            displayLevels(data.levels);
        }
        if (data.action == "displayCourse") {
            document.getElementById(`courseName-${data.levelid}`).innerHTML = `<b>${data.course.name} (${data.course.mode})</b><br>`
            var courseInfoHTML = ``;
            if (lastLoadedDownloads[data.levelid]) {
                courseInfoHTML += `<b>Uploader:</b> ${lastLoadedDownloads[data.levelid].creator} <br><b>Clearrate:</b> ${(lastLoadedDownloads[data.levelid].clearrate*100).toFixed(2).replace(/(\.0+|(\.\d+?)0+)$/, '$2') || "0.00%"}% (${lastLoadedDownloads[data.levelid].clears} / ${lastLoadedDownloads[data.levelid].total_attempts})<br>`
                const objectDiv = document.getElementById(`object-${data.levelid}`)
                objectDiv.addEventListener('click', () => objectClicked(data.levelid, lastLoadedDownloads[data.levelid]));   
            }
//            courseInfoHTML += `<b>Date</b>: ${data.course.year}/${data.course.month}/${data.course.day} - ${data.course.hour}:${data.course.minute}<br>`
//            courseInfoHTML += `<b>Folder</b>: ${data.fileName} (0)<br>`
//            courseInfoHTML += `<b>Theme</b>: overworld (0)<br>`
//            courseInfoHTML += `<b>Game Time</b>: 500s<br>`
//            courseInfoHTML += `<b>Objects Count</b>: 115<br>`
//            courseInfoHTML += `<b>Scroll</b>: none (0) over 39 blocks<br>`
            courseInfoHTML += `</div>`
            enqueueDrawTask(data.levelid, data.course, data.objects)
            document.getElementById(`courseInfoDisplay-${data.levelid}`).innerHTML = courseInfoHTML;
        }
    });
});


// NPM SCRIPTS