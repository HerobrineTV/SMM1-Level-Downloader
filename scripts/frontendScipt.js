const { ipcRenderer } = window; // Use provided ipcRenderer in Electron apps
const settingsFile = ('../SMMDownloader/Data/data.json');
const downloadCache = ('../SMMDownloader/Data/downloaded.json');
const backupCache = ('../SMMDownloader/Data/backupped.json');
const changelog = ('../pages/changelog.json');
const levelpacksFile = ('../SMMDownloader/Data/levelpacks.json');
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
var subAreaClicked = false;
var existingPacks = [];
var existingPacksObj = {};
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
const formatCodes = {
    'Q': 8
  };

var downloadAsPack = false;

let debounceServerStatusTimer;

async function checkServerStatus() {
    const url = "https://api.bobac-analytics.com/smm1/ping";
    const response = await fetch(url);

    if (response.status == 200) {
        document.getElementById('onlinedisplay').innerText = "API: 🟢";
        console.log("API Reachable 🟢")
    } else {
        document.getElementById('onlinedisplay').innerText = "API: 🔴";
        console.log("API Unreachable 🔴")
    }

    // Set a debounce timer to wait for 1 second before allowing the function to be called again
    debounceTimer = setTimeout(checkServerStatus, 30000);
}

// Function to clear the debounce timer
function clearDebounceTimer() {
    clearTimeout(debounceTimer);
}

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
                    setTimeout(() => {
                        downloadingBarContainerWindow.innerHTML = `<p2>Download Complete</p2>`;
                        downloadingBarContainerWindow.style.display = 'contents';
                    }, 1000)
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
                    setTimeout(() => {
                      barcontainer.innerHTML = `<p2>Download Complete</p2>`;
                      barcontainer.style.display = 'contents';
                  }, 1000)
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
    }
};

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

function changeDownloadAsLevelPack() {
    if (downloadAsPack == false) {
        downloadAsPack = true;
        document.getElementById("downloadAsLevelPack-1").checked = true;
        document.getElementById("downloadAsLevelPack-2").checked = true;
    } else {
        downloadAsPack = false;
        document.getElementById("downloadAsLevelPack-1").checked = false;
        document.getElementById("downloadAsLevelPack-2").checked = false;
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

function invisBGCheckBoxChanged() {
    if (SettingsData.invisibleCourseBG == false) {
        setSetting("invisibleCourseBG", true)
    } else if (SettingsData.invisibleCourseBG == true) {
        setSetting("invisibleCourseBG", false)
    } else {
        setSetting("invisibleCourseBG", false)
    }
}

function searchFadeInChanged() {
    if (SettingsData.fadeInAnim.levelSearch == false) {
        setSubSetting("fadeInAnim", "levelSearch", true)
    } else if (SettingsData.fadeInAnim.levelSearch == true) {
        setSubSetting("fadeInAnim", "levelSearch", false)
    } else {
        setSubSetting("fadeInAnim", "levelSearch", false)
    }
}

function searchDownloadedInChanged() {
    if (SettingsData.fadeInAnim.downloadedLevels == false) {
        setSubSetting("fadeInAnim", "downloadedLevels", true)
    } else if (SettingsData.fadeInAnim.downloadedLevels == true) {
        setSubSetting("fadeInAnim", "downloadedLevels", false)
    } else {
        setSubSetting("fadeInAnim", "downloadedLevels", false)
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
    const barcontainer = document.getElementById(`downloadingBarContainer-${levelObj.levelid}`);
    var buildhtml = ``;
    //console.log(levelObj);
    checkIfLevelisAlreadyDownloaded(levelObj.levelid);

    // Modified part to include the custom dropdown instead of <select>
    buildhtml = `
    <div class="level-info">
        <h2 id="levelName">${levelObj.name} <br>${generateCode(levelObj.levelid)}</h2>
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
            <div id="download-actions-button-1">`

                if (barcontainer && (barcontainer.innerHTML == `<p2>Download Complete</p2>` || barcontainer.innerHTML == `<p2>Already Downloaded</p2>`)) {
                } else if (currentHTMLPage != "savedLevels") {
                    buildhtml+= `<button class="searchdownload-btn">Download</button>`
                } else {
                    buildhtml+= `<button class="deletedownload-btn">Delete</button>`
                }

                buildhtml+=`<button class="openCourseFolder-btn">Open Folder</button>
            </div>
        </div>
    </div>
    `;

    levelInfo.innerHTML = buildhtml;

    // Add event listeners for the custom dropdown
    setupCustomDropdown();

    if (barcontainer && (barcontainer.innerHTML == `<p2>Download Complete</p2>` || barcontainer.innerHTML == `<p2>Already Downloaded</p2>`)) {} else if (currentHTMLPage != "savedLevels") {
        document.getElementsByClassName("searchdownload-btn")[0].addEventListener("click", () => {
            if (downloadAsPack == false) {
                runLevelDownloader(levelObj)
            } else {
                runLevelDownloaderToPacks(levelObj, document.getElementById('levelPackInput-1').value || "Default Pack")
            }
        })
    } else {
        document.getElementsByClassName("deletedownload-btn")[0].addEventListener("click", () => {deleteLevel(data.levelid, data.pack || null)})
    }

    downloadingBar.updateWindow(levelObj.levelid);

    if (currentHTMLPage === "savedLevels") {
        document.getElementsByClassName("openCourseFolder-btn")[0].style.display = "";
        document.getElementsByClassName("openCourseFolder-btn")[0].addEventListener("click", () => {
            openCourseFolder(levelObj.levelid)
        })
    } else {
        if (document.getElementsByClassName("openCourseFolder-btn")[0]) {
            document.getElementsByClassName("openCourseFolder-btn")[0].style.display = "none";
        }
    }

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

function loadFileInWindowLight(levelObj) {
    const modal = document.getElementById("myModal");
    const modalContent = document.querySelector(".modal-content");
    const levelInfo = document.getElementById("levelInfo");

    // Modified part to include the custom dropdown instead of <select>
    levelInfo.innerHTML = `
    <div class="level-info">
        <h2 id="levelName">${levelObj.name}</h2>
    </div>
    <div class="level-details">
        <p><strong>Level ID:</strong> <span id="levelID">${levelObj.levelid}</span></p>
    </div>
    <div class="actions">
        <div id="download-actions" class="custom-select">
            <div id="download-actions-button-1">
                <button class="openCourseFolder-btn">Open Folder</button>
            </div>
        </div>
    </div>
    `;

    if (currentHTMLPage === "savedLevels") {
        document.getElementsByClassName("openCourseFolder-btn")[0].addEventListener("click", () => {
            openCourseFolder(levelObj.levelid)
        })
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
                if (document.getElementById(`object-${valuesArray[i].levelid}`).style.display != "none") {
                    markLevelAsDeleting(valuesArray[i]);
                }
            }
        } else if (isAllSelectedForDel == true) {
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
            scrollFrame.style.borderColor = "";
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

function objectClicked(levelid, levelObj, pack) {
    //console.log("Clicked", existingPacksObj[pack], lastLoadedDownloads[levelid+"_"+existingPacksObj[pack]])
    if (!subAreaClicked) {
        if (levelObj.mode === "light") {
            //console.log("LIGHT")
            loadFileInWindowLight(levelObj);
        } else {
            if (isDeleteMode == false) {
                if (existingPacksObj[pack] != null && existingPacksObj[pack] != "null") {
                    loadFileInWindow(lastLoadedDownloads[levelid+"_"+existingPacksObj[pack]])
                } else {
                    loadFileInWindow(levelObj);
                }
            } else {
                isAllSelectedForDel = false;
                markLevelAsDeleting(levelObj);
            }
        }
    }
}

function subAreaBtnClicked(levelid) {
    loadLevelPacks()
    const savedLevelsDrop = document.getElementById('savedlevelsDropdown')
    const objectDiv = document.getElementById(`object-${levelid}`);
    const btn = document.getElementById(`button-${levelid}`);
    var ParentDir = window.location.href.substring(0, window.location.href.lastIndexOf('/'));
    ParentDir = ParentDir.substring(0, ParentDir.lastIndexOf('/'));
    subAreaClicked = true;
    var path = "";
    var filename = "";

    //console.log("Clicked")
    var pack = objectDiv.getAttribute('packName')

    if (savedLevelsDrop.value == "downloaded") {
        if (pack != null && pack != "null") {
            path = "../SMMDownloader/Data/LevelPacks/" + existingPacksObj[pack]
        } else {
            path = "../SMMDownloader/Data/DownloadCache"
        }
    } else if (savedLevelsDrop.value == "official-testing") {
        path = "../SMMDownloader/Data/OfficialCourses/CourseFiles"
    } else if (savedLevelsDrop.value == "cemu") {
        //path = SettingsData.CemuDirPath;
    } else if (savedLevelsDrop.value == "backupped") {
        path = "../SMMDownloader/Data/BackupCache";
    }
    if (objectDiv.getAttribute('current-layer') == "overworld") {
        filename = "course_data_sub.cdt"
        btn.innerText = "Switch to Overworld";
        objectDiv.setAttribute('current-layer', "underworld");
        objectDiv.setAttribute('packName', pack)
    } else {
        filename = "course_data.cdt"
        btn.innerText = "Switch to Sub Area";
        objectDiv.setAttribute('current-layer', "overworld");
        objectDiv.setAttribute('packName', pack)
    }
    //console.log(path)
    window.api.send("toMain", {action:"loadSubArea", folderpath:path, levelid:levelid, filename:filename, pack:pack});

    setTimeout(() => {
        subAreaClicked = false;
      }, 100);
}

function addObjects(levels) {
    const objectsContainer = document.getElementById('scrollable-objects');
    if (SettingsData.currentPage == 1 && currentHTMLPage != "savedLevels"){objectsContainer.innerHTML = '';}
    //console.log(levels);
    levels.forEach(obj => {
        //console.log(obj.levelid);
        var objectDiv;
        if (document.getElementById(`object-${obj.levelid}`)) {
            objectDiv = document.getElementById(`object-${obj.levelid}`);
        } else {
            objectDiv = document.createElement('div');
        }
        if (currentHTMLPage == "main") {
            checkIfLevelisAlreadyDownloaded(obj.levelid);
        }
        objectDiv.id = `object-${obj.levelid}`;
        objectDiv.classList.add('object');
        objectDiv.classList.add('searchable');
        objectDiv.classList.add('hidden')
        objectDiv.setAttribute('data-name', obj.name);
        objectDiv.setAttribute('current-layer', "overworld");
        
        if (obj.folder) {
            objectDiv.innerHTML = `<button id="button-${obj.levelid}" onclick="subAreaBtnClicked('${obj.levelid}')">Switch to Sub Area</button>`
            +`<div id="courseName-${obj.levelid}">${obj.name} <br>${generateCode(obj.levelid)}</div>`
            +`<div></div>`
            +`<div id="courseDisplay-${obj.levelid}"></div>`
            +`<div id="courseInfoDisplay-${obj.levelid}"></div>`
        //    +`<div>Course Folder: ${obj.folder}</div>`
            +`<div class="downloaded-display"></div>`
            +`<div id="packName-${obj.levelid}"></div>`
            +`<div id="downloadingBarContainer-${obj.levelid}" class="downloadingBarContainerClass" style=""><div id="downloadingBarProgress"></div></div>`;
        } else {
            objectDiv.innerHTML = `
            <div id="courseName-${obj.levelid}">${obj.name} <br>${generateCode(obj.levelid)}</div>
            <div>Stars: ${obj.stars}</div>
            <div>Creator: ${obj.creator}</div>
            <div>Clear Rate: ${(obj.clearrate*100).toFixed(2).replace(/(\.0+|(\.\d+?)0+)$/, '$2')}%</div>
            <div class="downloaded-display"></div>
            <div id="packName-${obj.levelid}"></div>
            <div id="downloadingBarContainer-${obj.levelid}" class="downloadingBarContainerClass" style=""><div id="downloadingBarProgress"></div></div>
            `;
            function clickHandler() {
                objectClicked(obj.levelid, obj, obj.packname || null)
                //console.log("CLICKED-1")
            }
            objectDiv.onclick = clickHandler
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

        if (searchQuery.toLowerCase() == '') {
            const packname = currentDiv.getAttribute('packName')
            if (packname && document.getElementById(`packName-${packname}`) && document.getElementById(`packName-${packname}`).getAttribute('content-visible') == 'hidden') {
                currentDiv.style.display = 'none';
            } else {
                currentDiv.style.display = '';
            }

        } else if (!currentDiv.getAttribute('data-name').toLowerCase().includes(searchQuery.toLowerCase()) && !currentDiv.innerText.toLowerCase().includes(searchQuery.toLowerCase())) {
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

// Following needs to be reworked, since it has to use the EntryID instead of the LevelID
function findRandomLevel() {
    const ranID = getRandomInt(18118278)

    SettingsData.currentPage = 1;
    const apiUrl = `${SettingsData.APILink}/searchRandomLevels/${encodeURIComponent(ranID)}`;
        
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
                if (data.error) {
                    if (data.error == "No Random Level Found!") {
                        findRandomLevel(); 
                    } else {
                        console.error('There was a problem with the fetch operation:', data.error);
                    }
                    return;
                }
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
                }
                if (!data.error && data.length > 0) {
                    displayLevels(data);
                }
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

function calculateChecksum(idno) {
    // Convert the data ID to a hexadecimal string
    const idHex = idno.toString(16).padStart(8, '0');

    // Step 1: Compute the MD5 hash of the access key
    const accessKey = "9f2b4678";
    const key = CryptoJS.MD5(accessKey);

    // Step 2: Transform the data ID into an 8-byte little-endian number
    const data = new ArrayBuffer(8);
    const dataView = new DataView(data);
    for (let i = 0; i < 8; i += 2) {
        const byte = parseInt(idHex.substr(i, 2), 16);
        dataView.setUint8(7 - i, byte);
    }

    // Step 3: Compute the HMAC of the data ID using the MD5 hash as the key
    const hmac = CryptoJS.HmacMD5(CryptoJS.lib.WordArray.create(new Uint8Array(data)), key);

    // Step 4: Take the third and fourth byte of the HMAC, reverse their order, and return them as the checksum
    const checksum = hmac.toString(CryptoJS.enc.Hex).substr(6, 2) + hmac.toString(CryptoJS.enc.Hex).substr(4, 2);
    
    return checksum.toUpperCase();
}

    //structPack('<Q', idno.toString(16));

// Function to pack data into struct
function structPack(format, value) {
    let littleEndian = true;
    let size = formatCodes[format];

    // Ensure value is within bounds
    if (value < 0 || value > Math.pow(2, 8 * size)) {
        throw new Error('Value out of bounds for format ' + format);
    }

    // Pack the value into bytes
    let packed = [];
    for (let i = 0; i < size; i++) {
        packed.push(value & 0xff);
        value >>= 8;
    }

    // Reverse if littleEndian
    if (littleEndian) {
        packed.reverse();
    }

    // Convert to string of hex digits
    return packed.map(b => {
        let hex = b.toString(16);
        return hex.length === 1 ? '0' + hex : hex;
    }).join('').toUpperCase();
}


// Main function
function generateCode(levelid) {
    const idno = parseInt(levelid);
    const checksum = calculateChecksum(idno);
    let idstring = idno.toString(16).toUpperCase();
    idstring = idstring.padStart(16, '0');
    const code = checksum.substring(0, 4) + '-0000-' + idstring.substring(8, 12) + '-' + idstring.substring(12);
    //console.log(code);
    return code;
}

function isOnlyNumbers(str) {
    return /^[0-9]+$/.test(str);
  }

  function extractIdFromCode(code) {
    // Assuming the code format: [checksum][0000][idstring]
    const idstring = removeDashes(code).substring(8);
    return parseInt(idstring, 16); // Parse the hexadecimal string to integer
}

function removeDashes(input) {
    return input.replace(/-/g, ''); // Remove all dashes using regex
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

    var search = SettingsData.lastSearchPhrase;

    if (SettingsData.searchParams.LevelID == true 
        && !isOnlyNumbers(SettingsData.lastSearchPhrase) 
        && SettingsData.searchParams.LevelName == false
        && SettingsData.searchParams.CreatorName == false
        && SettingsData.searchParams.CreatorID == false) {
        search = extractIdFromCode(SettingsData.lastSearchPhrase)
    }
    const loadingholder = document.getElementById("loadingholder").cloneNode(true);
    const apiUrl = `${SettingsData.APILink}/searchLevels/${encodeURIComponent(search)}/${page}`
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
                setTimeout(() => {
                    connerrorCooldown = false;
                    loadingholder.remove();
                }, 5000);
            });
}

async function downloadAll() {
    var levels = []
    var count = 0
    for (const [key, value] of Object.entries(SettingsData.recentFoundLevels)) {
        
        if (downloadAsPack == false) {
            runLevelDownloader(value)
        } else {
            runLevelDownloaderToPacks(value, document.getElementById('levelPackInput-1').value || "Default Pack")
        }
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

function runLevelDownloaderToPacks(levelObj, pack) {

    downloadingBar.addBar(levelObj.levelid);

    const levelDisplayObjDownloadActions = document.getElementById(`download-actions`)
    if (levelDisplayObjDownloadActions) {
        levelDisplayObjDownloadActions.innerHTML = `<p>Downloading...</p>`
    }

    link = levelObj.url;
    levelID = levelObj.levelid;
    currentStep[levelObj.levelid] = 1;
    window.api.send("toMain", {action:"download-level-to-pack", url:link, levelID:levelID, levelObj:levelObj, useProxy:SettingsData.useProxy, pack:pack});
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
        if (SettingsData.fadeInAnim.levelSearch == true) {
            document.getElementById("useSearchFadeIn").checked = true;
        }
        if (SettingsData.invisibleCourseBG == true) {
            document.getElementById("useInvisBackground").checked = true;
        }
        if (SettingsData.fadeInAnim.downloadedLevels == true) {
            document.getElementById("useDownloadedFadeIn").checked = true;
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
        if(downloadAsPack == true) {
            document.getElementById("downloadAsLevelPack-1").checked = true;
            document.getElementById("downloadAsLevelPack-2").checked = true;
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

        const levelPackInput1 = document.getElementById('levelPackInput-1');
        const levelPackInput2 = document.getElementById('levelPackInput-2');
        const dropdown1 = document.getElementById('pack-dropdown-1');
        const dropdown2 = document.getElementById('pack-dropdown-2');
        
        levelPackInput1.addEventListener('input', function() {
          const input = this.value
          dropdown1.innerHTML = '';
        
          existingPacks.forEach(pack => {
            if (pack.toLowerCase().includes(input.toLowerCase())) {
              const option = document.createElement('a');
              option.textContent = pack;
              option.addEventListener('click', function() {
                levelPackInput1.value = pack;
                levelPackInput2.value = pack;
                dropdown1.style.display = 'none';
              });
              dropdown1.appendChild(option);
            }
          });
        
          // Add the "Create Pack: %Current_Input%" option
          if (input !== '' && input.toLowerCase()!== 'null') {
            const createOption = createOptionElement("Create Pack: " + input);
            createOption.addEventListener('click', function() {
              createPack(input);
            });
            dropdown1.appendChild(createOption);
          }
        
          toggleDropdownVisibility(dropdown1, input);
        });
        
        levelPackInput2.addEventListener('input', function() {
          const input = this.value
          dropdown2.innerHTML = '';
        
          existingPacks.forEach(pack => {
            if (pack.toLowerCase().includes(input.toLowerCase())) {
              const option = document.createElement('a');
              option.textContent = pack;
              option.addEventListener('click', function() {
                levelPackInput1.value = pack;
                levelPackInput2.value = pack;
                dropdown2.style.display = 'none';
              });
              dropdown2.appendChild(option);
            }
          });
        
          // Add the "Create Pack: %Current_Input%" option
          if (input !== '') {
            const createOption = createOptionElement("Create Pack: " + input);
            createOption.addEventListener('click', function() {
              createPack(input);
            });
            dropdown2.appendChild(createOption);
          }
        
          toggleDropdownVisibility(dropdown2, input);
        });
        
        // Close the dropdown if the user clicks outside of it
        document.addEventListener('click', function(event) {
          if (!event.target.matches('#levelPackInput-1') && !event.target.matches('#levelPackInput-2')) {
            dropdown1.style.display = 'none';
            dropdown2.style.display = 'none';
          }
        });
        
        // Function to create an option element
        function createOptionElement(text) {
          const option = document.createElement('a');
          option.textContent = text;
          option.classList.add('dropdown-item');
          return option;
        }
        
        // Function to toggle dropdown visibility based on input
        function toggleDropdownVisibility(dropdown, input) {
          if (input === '') {
            dropdown.style.display = 'none';
          } else {
            dropdown.style.display = 'block';
          }
        }
        
        // Function to handle creating a new pack
        function createPack(input) {
          const newPack = input.trim();
          if (existingPacks.includes(newPack) || newPack == "") {
            return;
          } else {
            window.api.send("toMain", {action:"create-pack", packname: newPack})
          }
          console.log("Creating Pack:", input);
        }
        

    } else if (page == "../pages/savedLevels.html") {
        deleteArray = [];
        isDeleteMode = false;
        isAllSelectedForDel = false;
        currentHTMLPage = "savedLevels"
        document.getElementById('searchSavedLevel-button').addEventListener("click", () => {
            searchDivs(document.getElementById('searchSavedLevelText').value.trim(), 'scrollable-objects');
        })
        loadSavedLevels();
        loadDownloadedLevels();
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
    const _officialFolderbtn = document.getElementById('openfolderofficial-btn')

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
        _officialFolderbtn.style.display = "none"
        document.getElementById('scrollable-objects').innerHTML = "";
        loadDownloadedLevels()
    } else if (savedLevelsDrop.value == "official-testing") {
        deleteArray = [];
        isDeleteMode = false;
        isAllSelectedForDel = false;
        _downloadedFolderbtn.style.display = "none"
        _backuppedFolderbtn.style.display = "none"
        _cemuFolderbtn.style.display = "none"
        multideletebtn.style.display = "none"
        _officialFolderbtn.style.display = ""
        document.getElementById('scrollable-objects').innerHTML = "";
        loadOfficialLevels("testing")
    } else if (savedLevelsDrop.value == "cemu") {
        deleteArray = [];
        isDeleteMode = false;
        isAllSelectedForDel = false;
        _downloadedFolderbtn.style.display = "none"
        _backuppedFolderbtn.style.display = "none"
        multideletebtn.style.display = "none"
        _officialFolderbtn.style.display = "none"
        if (SettingsData.CemuDirPath != "") {
            _cemuFolderbtn.style.display = ""
        }
        document.getElementById('scrollable-objects').innerHTML = "";
        loadLevelsfromCEMU()
    } else if (savedLevelsDrop.value == "backupped") {
        deleteArray = [];
        isDeleteMode = false;
        isAllSelectedForDel = false;
        _downloadedFolderbtn.style.display = "none"
        _backuppedFolderbtn.style.display = ""
        _cemuFolderbtn.style.display = "none"
        _officialFolderbtn.style.display = "none"
        document.getElementById('scrollable-objects').innerHTML = "";
        loadBackuppedLevels()
    }
}

function openFolderOfficial() {
    var ParentDir = window.location.href.substring(0, window.location.href.lastIndexOf('/'));
    ParentDir = ParentDir.substring(0, ParentDir.lastIndexOf('/'));
    window.api.send("toMain", {action:"open-folder", path: ParentDir+"/SMMDownloader/Data/OfficialCourses/CourseFiles"})
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

function openCourseFolder(levelid) {
    const savedLevelsDrop = document.getElementById('savedlevelsDropdown')
    var ParentDir = window.location.href.substring(0, window.location.href.lastIndexOf('/'));
    ParentDir = ParentDir.substring(0, ParentDir.lastIndexOf('/'));
    const objectDiv = document.getElementById(`object-${levelid}`)
    const pack = objectDiv.getAttribute('packName')
    if (savedLevelsDrop.value == "official-testing") {
        window.api.send("toMain", {action:"open-folder", path: ParentDir+"/SMMDownloader/Data/OfficialCourses/CourseFiles/"+levelid})
    }
    if (savedLevelsDrop.value == "downloaded") {
        if (pack == null || pack == "null") {
            window.api.send("toMain", {action:"open-folder", path: ParentDir+"/SMMDownloader/Data/DownloadCache/"+levelid})
        } else {
            window.api.send("toMain", {action:"open-folder", path: ParentDir+"/SMMDownloader/Data/LevelPacks/"+existingPacksObj[pack]+"/"+levelid})
        }
    }
    if (savedLevelsDrop.value == "backupped") {
        window.api.send("toMain", {action:"open-folder", path: ParentDir+"/SMMDownloader/Data/BackupCache/"+levelid})
    }
    if (savedLevelsDrop.value == "cemu") {
        window.api.send("toMain", {action:"open-folder", path: SettingsData.CemuDirPath+"/"+levelid})
    }
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

function loadLevelPacks() {
    fetch(levelpacksFile)
        .then(response => response.json())
        .then(data => {
            var num = 0
            for (const packName in data) {
                existingPacks[num] = packName;
                num++
            }
            existingPacksObj = data
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

function loadOfficialLevels(kind) {
    window.api.send("toMain", {action:"get-smm1-cached-officials-"+kind});
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

function viewChangelog() {
    fetch(changelog)
        .then(response => response.json())
        .then(data => {

            if (data) {
                //console.log(data);
                var changelogHTML = "";
                Object.keys(data).reverse().forEach(key => {
                    changelogHTML += `<h2>${key}</h2>`; // Use the key as the title
                    // Iterate over the values (assuming each value is an array)
                    data[key].forEach(item => {
                        changelogHTML += `<li>${item}</li>`; // Generate HTML for each item
                    });
                });
                document.getElementById('main-content-2').innerHTML = changelogHTML;
            } else {
                displayNotification("Changelog is Empty! <br> U need to Update SMMDownloader!", 5000)
            }
        })
        .catch(error => console.log(error));
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

function deleteLevel(levelid, packname) {
    const objectDiv = document.getElementById(`object-${levelid}`)
    const pack = objectDiv.getAttribute('packName') || packname || null
    //console.log(pack);
    window.api.send("toMain", {action:"delete-course-file", levelid:levelid, pack: pack});
}

function checkIfLevelisAlreadyDownloaded(levelid){
    //console.log(levelid);
    window.api.send("toMain", {action:"checkIfAlreadyDownloaded", levelID:levelid});
}

function changedProfileDropdown(){
    const currentProfile = document.getElementById('profile-Dropdown').value;
    setSetting("selectedProfile", currentProfile);
}

function resetOfficialCourses() {
    displayNotification(`Resetting official Courses!`, 3000)
    window.api.send("toMain", {action:"reset-official-courses"});
}

function enqueueDrawTask(levelid, course, objects) {
    if (document.getElementById(`courseDisplay-${levelid}`)!= null) {
        drawQueue.push({ levelid, course, objects });
    }
}

function drawLevel(levelid, course, objects) {
    if (document.getElementById(`courseDisplay-${levelid}`)!= null) {
        if (objects.length > 0) {
            new Draw(`courseDisplay-${levelid}`, course, objects, "8", SettingsData.invisibleCourseBG); //32 64
            setTimeout(() => {

            }, 1000);
            document.querySelector(`#courseDisplay-${levelid} > div`).addEventListener("wheel", function(event) {
                if (event.deltaY !== 0) {
                    event.preventDefault();
                    document.querySelector(`#courseDisplay-${levelid} > div`).scrollLeft += event.deltaY;
                }
            });
        } else {
            document.getElementById(`courseDisplay-${levelid}`).innerHTML = "<h1>Level with Filename: "+levelid+" Cant be displayed! Broken File!</h1>"
            window.api.send("toMain", {action:"write-to-log", message:"[ERROR] The Level with Filename: "+levelid+" contained "+ objects.length + " Objects"});
        }
    }
}

function switchDownloadTab(tabdiv) {
    const newtab = document.getElementById(tabdiv);
    const tabs = document.getElementsByClassName("downloadTabs")
    for (var i = 0; i < tabs.length; i++) {
        tabs[i].style.display = "none";
    }
    newtab.style.display = "";
}

function displayNotification(html, displaytime) {
    const notification = document.getElementById('notification');
    notification.innerHTML = html
    notification.classList.add('show');
    setTimeout(() => {
      notification.classList.remove('show');
    }, displaytime);
}


const observer = new IntersectionObserver((entries) => {
    entries.forEach((entry) => {
        //console.log(entry)
        if (entry.isIntersecting) {
            entry.target.classList.add('show');
        } else {
            entry.target.classList.remove('show');
        }
    });
});

function createPackDiv(packname, levelid) {
    console.log(packname);
    var packdiv = document.createElement('div')
    packdiv.id = `packName-${packname}`
    packdiv.classList.add('packName')
    packdiv.setAttribute('content-visible', 'hidden')
    document.getElementById('scrollable-objects').prepend(packdiv)
    packdiv.innerHTML = `<img class="folder-img" id="folder-img-${packname}" src="../pictures/smmdownloader/folder_closed.png"><b class="packTitle">Pack: ${packname}</b><br>`
    if (levelid != null) {
        packdiv.appendChild(document.getElementById(`object-${levelid}`))
    }

    const hoverTextDiv = document.getElementById('hover-text-div')

    var lineDiv = document.createElement('div');
    lineDiv.classList.add('folder-line');
    lineDiv.id = `folder-line-${packname}`
    packdiv.appendChild(lineDiv);
 
    lineDiv.addEventListener('mouseenter', function(event) {
        lineDiv = document.getElementById(`folder-line-${packname}`)
        if (event.target === lineDiv) {
            const mouseY = event.clientY;
            const mouseX = event.clientX;
            hoverTextDiv.style.top = `${mouseY+5}px`;
            hoverTextDiv.style.left = `${mouseX+5}px`;
            hoverTextDiv.innerText = `Close Pack: ${packname}`;
            hoverTextDiv.style.display = 'block';
        } else {
            hoverTextDiv.innerText = '';
            hoverTextDiv.style.display = 'none';
        }
    });
    
    lineDiv.addEventListener('mouseleave', function() {
        hoverTextDiv.innerText = '';
        hoverTextDiv.style.display = 'none';
    });
    
    function clickHandler() {
        packdiv = document.getElementById(`packName-${packname}`)
        if (packdiv.getAttribute('content-visible') == "hidden") {
            packdiv.setAttribute('content-visible', 'visible')
            document.getElementById(`folder-img-${packname}`).src = "../pictures/smmdownloader/folder_open.png";
            for (let i = 0; i < packdiv.children.length; i++) {
                if (document.getElementById(packdiv.children[i].id)) {
                    packdiv.children[i].style.display = "block";
                }
            }
            if (packdiv.children.length == 1) {
                packdiv.innerHTML += `Pack Empty`
            }
            lineDiv.style.display = 'block';
        } else {
            packdiv.setAttribute('content-visible', 'hidden')
            document.getElementById(`folder-img-${packname}`).src = "../pictures/smmdownloader/folder_closed.png";
            for (let i = 0; i < packdiv.children.length; i++) {
                if (document.getElementById(packdiv.children[i].id) && !packdiv.children[i].classList.contains('folder-img')) {
                    packdiv.children[i].style.display = "none";
                }
            }
            lineDiv.style.display = 'none';
        }
    }
    packdiv.addEventListener('click', function(event) {
        packdiv = document.getElementById(`packName-${packname}`)
        if (event.target === packdiv) {
            clickHandler();
        }
    });

    lineDiv.addEventListener('click', function(event) {
        lineDiv = document.getElementById(`folder-line-${packname}`)
        if (event.target === lineDiv) {
            clickHandler();
        }
    });
}

function observeNewElements() {
    const elements = document.querySelectorAll('.hidden');
    elements.forEach((element) => {
        if ((currentHTMLPage == "savedLevels" && SettingsData.fadeInAnim.downloadedLevels == true) || (currentHTMLPage == "main" && SettingsData.fadeInAnim.levelSearch == true)) {
            observer.observe(element);
        } else {
            element.classList.remove('hidden')
        }
    });
}

const mutationObserver = new MutationObserver((mutations) => {
    mutations.forEach((mutation) => {
        if (mutation.type === 'childList' && mutation.addedNodes.length > 0) {
            observeNewElements();
        }
    });
});

mutationObserver.observe(document.body, {
    childList: true,
    subtree: true
});

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
        if (data.action == "update-info") {
            displayNotification(`A new Update is available! New Version: ${data.foundVersion}<button style="margin-left: 1vw" onclick="openURL('${data.url}')">Update Now!</button>`, 10000)
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
                        barcontainer.innerHTML = `<p2>Download Complete</p2>`
                    }
                }
                //console.log("TEST")
                if (levelDisplayObjDownloadActions && document.getElementById('levelID') && document.getElementById('levelID').innerHTML.includes(data.levelid)) {
                    levelDisplayObjDownloadActions.innerHTML = `<button class="deletedownload-btn">Delete</button><button class="openCourseFolder-btn">Open Folder</button>`

                    if (currentHTMLPage === "savedLevels") {
                        document.getElementsByClassName("openCourseFolder-btn")[0].style.display = "";
                        document.getElementsByClassName("openCourseFolder-btn")[0].addEventListener("click", () => {
                            openCourseFolder(data.levelid)
                        })
                    } else {
                        document.getElementsByClassName("openCourseFolder-btn")[0].style.display = "none";
                    }
                    document.getElementsByClassName("deletedownload-btn")[0].addEventListener("click", () => {deleteLevel(data.levelid, data.pack || null)})
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
                const savedLevelsDrop = document.getElementById('savedlevelsDropdown')
                if (levelHTMLObj && currentHTMLPage == "main") {
                    const barcontainer = document.getElementById(`downloadingBarContainer-${data.levelid}`);
                    if (barcontainer) {
                        barcontainer.style.display = 'contents';
                        barcontainer.style.backgroundColor = '';
                        barcontainer.innerHTML = `<p2>Already Downloaded</p2>`
                    }
                }
                if (levelDisplayObjDownloadActions && document.getElementById('levelID') && document.getElementById('levelID').innerHTML.includes(data.levelid) && (savedLevelsDrop.value == "downloaded" || savedLevelsDrop.value == "backupped")) {
                    levelDisplayObjDownloadActions.innerHTML = `<button class="deletedownload-btn">Delete</button><button class="openCourseFolder-btn">Open Folder</button>`

                    if (currentHTMLPage === "savedLevels") {
                        document.getElementsByClassName("openCourseFolder-btn")[0].style.display = "";
                        document.getElementsByClassName("openCourseFolder-btn")[0].addEventListener("click", () => {
                            openCourseFolder(data.levelid)
                        })
                    } else {
                        document.getElementsByClassName("openCourseFolder-btn")[0].style.display = "none";
                    }
                    document.getElementsByClassName("deletedownload-btn")[0].addEventListener("click", () => {deleteLevel(data.levelid, data.pack || null)})
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
            //document.getElementById('scrollable-objects').innerHTML = `<h2></h2>`;
            displayLevels(data.levels);
        }
        if (data.action == "packCreated") {
            const levelPackInput1 = document.getElementById('levelPackInput-1');
            const levelPackInput2 = document.getElementById('levelPackInput-2');
    
            levelPackInput1.value = data.packname;
            levelPackInput2.value = data.packname;
            loadLevelPacks();
        }
        if (data.action == "displayCourse") {
            //console.log(data.course.name)
            //console.log(data)
            subAreaClicked = false;
            if (data.html == "<h1>Level Cant be displayed! Broken File!</h1>" || data.course == null || data.objects == null) {
                document.getElementById(`courseName-${data.levelid}`).innerHTML = "<h1>Level with ID: "+data.levelid+" Cant be displayed! Broken File!</h1>"
            } else {
                document.getElementById(`courseName-${data.levelid}`).innerHTML = `<b>${data.course.name} (${data.course.mode})<br>Level-ID: ${generateCode(data.levelid)} (${data.levelid})</b><br>`
                var courseInfoHTML = ``;
                if (lastLoadedDownloads[data.levelid] || lastLoadedDownloads[data.levelid+"_"+existingPacksObj[data.packname]]) {
                    const levelData = lastLoadedDownloads[data.levelid] || lastLoadedDownloads[data.levelid+"_"+existingPacksObj[data.packname]]
                    courseInfoHTML += `<b>Uploader:</b> ${levelData.creator} <br><b>Clearrate:</b> ${(levelData.clearrate*100).toFixed(2).replace(/(\.0+|(\.\d+?)0+)$/, '$2') || "0.00%"}% (${levelData.clears} / ${levelData.total_attempts})<br>`
                    const objectDiv = document.getElementById(`object-${data.levelid}`)
                    objectDiv.setAttribute('packName', data.packname || null)
                    function clickHandler() {
                        objectClicked(data.levelid, levelData, data.packname || null);
                        //console.log("CLICKED-2");
                    }
                    objectDiv.onclick = clickHandler
                } else {
                    //console.log(lastLoadedDownloads[data.levelid+"_"+existingPacksObj[data.packname || null]])
                    const objectDiv = document.getElementById(`object-${data.levelid}`)
                    objectDiv.setAttribute('packName', data.packname || null)
                    function clickHandler() {
                        objectClicked(data.levelid, {levelid : data.levelid, mode: "light", name : data.course.name}, data.packname || null);
                        //console.log("CLICKED-3");
                    }
                    objectDiv.onclick = clickHandler  
                }
                if (data.packname && data.packname!= null && data.packname!= "null") {
                    const objectDiv = document.getElementById(`object-${data.levelid}`)
                    objectDiv.style.display = "none";
                    packdiv = document.getElementById(`packName-${data.packname}`)
                    if (!packdiv) {
                        createPackDiv(data.packname, data.levelid)
                    } else if (packdiv) {
                        packdiv.appendChild(objectDiv)
                        if (packdiv.getAttribute('content-visible') == 'visible') {
                            objectDiv.style.display = "";
                        }
                    }
                } else {
                    const objectDiv = document.getElementById(`object-${data.levelid}`)
                    //console.log(data.levelid)
                    document.getElementById('scrollable-objects').appendChild(objectDiv);
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
                if (data.packname != null && data.packname!= "null") {
                    document.getElementById(`packName-${data.levelid}`).innerHTML = `<b>Pack Name:</b> ${data.packname}<br>`
                } else {
                    document.getElementById(`packName-${data.levelid}`).innerHTML = `<b>Not in a Level Pack</b><br>`
                }
            }
        }
    });
});

const hoverTextDiv = document.createElement('div');
hoverTextDiv.id = 'hover-text-div';
hoverTextDiv.classList.add('hover-text');
document.body.appendChild(hoverTextDiv);

checkServerStatus()
loadLevelPacks()