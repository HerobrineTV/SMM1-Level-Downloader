const { ipcRenderer } = window; // Use provided ipcRenderer in Electron apps
const settingsFile = ('../SMMDownloader/Data/data.json');
const downloadCache = ('../SMMDownloader/Data/downloaded.json');
const backupCache = ('../SMMDownloader/Data/backupped.json');
let SettingsData;
const totalSteps = 10;
var currentStep = [];
var currentHTMLPage = "";

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
        document.getElementById('searchLevel-button').innerHTML = `<br><button onclick="searchLevel()">Search Level</button><br><br>`
    } else {
        document.getElementById('searchLevel-button').innerHTML = ``
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

    checkIfLevelisAlreadyDownloaded(levelObj.levelid);

    levelInfo.innerHTML = `
    <div class="level-info">
        <!-- Use an icon or image here for a visual touch -->
        <h2 id="levelName">${levelObj.name}</h2>
    </div>
    <div class="level-details">
        <!-- Example of incorporating Mario-themed imagery -->
        <p><strong>Uploader:</strong> <span id="uploader">${levelObj.creator || null}</span> <img src="mario-icon.png" alt="" style="height:20px;"></p>
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
        <div id="download-actions">
            <select id="course-dropdown">
                <option value="option1">course000</option>
                <option value="option2">course001</option>
                <option value="option3">course002</option>
            </select>
            <button class="searchdownload-btn">Download</button>
        </div>
    </div>
    `;

    document.getElementsByClassName("searchdownload-btn")[0].addEventListener("click", () => {runLevelDownloader(levelObj)})

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


function objectClicked(levelid, levelObj) {
    // Assuming 'filename' is the path to the HTML file to be loaded
    loadFileInWindow(levelObj);
}

function addObjects(levels) {
    const objectsContainer = document.getElementById('scrollable-objects');
    if (SettingsData.currentPage == 1){objectsContainer.innerHTML = '';}

    levels.forEach(obj => {
        const objectDiv = document.createElement('div');
        if (currentHTMLPage == "main") {
            checkIfLevelisAlreadyDownloaded(obj.levelid);
        }
        objectDiv.id = `object-${obj.levelid}`;
        objectDiv.classList.add('object');
        if (obj.folder) {
            objectDiv.innerHTML = `
            <div>${obj.name}</div>
            <div></div>
            <div></div>
            <div>Course Folder: ${obj.folder}</div>
            <div class="downloaded-display"></div>
        `;
        } else {
            objectDiv.innerHTML = `
            <div>${obj.name}</div>
            <div>Stars: ${obj.stars}</div>
            <div>Creator: ${obj.creator}</div>
            <div>Clear Rate: ${(obj.clearrate*100).toFixed(2).replace(/(\.0+|(\.\d+?)0+)$/, '$2')}%</div>
            <div class="downloaded-display"></div>
        `;
        }
        objectDiv.addEventListener('click', () => objectClicked(obj.levelid, obj));
        objectsContainer.appendChild(objectDiv);
    });
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
    lazyLevelLoading(SettingsData.currentPage + 1);
}

function lazyLevelLoading(page) {
    SettingsData.currentPage = page;
    const apiUrl = `${SettingsData.APILink}/searchLevels/${encodeURIComponent(SettingsData.lastSearchPhrase)}/${page}`
    +`?coursename=${SettingsData.searchParams.LevelName == true? 1 : 0}`
    +`&courseid=${SettingsData.searchParams.LevelID == true? 1 : 0}`
    +`&creatorname=${SettingsData.searchParams.CreatorName == true? 1 : 0}`
    +`&creatorid=${SettingsData.searchParams.CreatorID == true? 1 : 0}`
    +`&searchexact=${SettingsData.searchParams.SearchExact == true? 1 : 0}`;
        
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
                console.log(data); // You can process the data here
                if (SettingsData.currentPage == 1) {
                    setSetting("recentFoundLevels", transformToDict(data));
                } else {
                    setSetting("recentFoundLevels", {...SettingsData.recentFoundLevels,...transformToDict(data)});
                }
                displayLevels(data);
            })
            .catch(error => {
                // Handle errors that occur during the fetch
                console.error('There was a problem with the fetch operation:', error);
            });
}

function searchLevel() {
    // Get the search phrase from the input field
    const searchPhrase = document.getElementById('searchLevelText').value.trim();
    
    // Set the last search phrase in settings
    setSetting("lastSearchPhrase", searchPhrase);
    
    // Check if the API link should be used
    if (SettingsData.useAPILink) {
        lazyLevelLoading(1)
    }
}

function selectFolder() {
    window.api.send("toMain", {action:"select-folder"});
}

function runLevelDownloader(levelObj) {
    link = levelObj.url;
    levelID = levelObj.levelid;
    currentStep[levelObj.levelid] = 1;
    window.api.send("toMain", {action:"download-level", url:link, levelID:levelID, levelObj:levelObj});
}

function loadPage(page) {
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
        currentHTMLPage = "settings"
        window.api.send("toMain", {action:"get-smm1-profiles", path:SettingsData.CemuDirPath});
        if (SettingsData.useCemuDir == true) {
            document.getElementById("useCEMUfolder").checked = true;
            document.getElementById('optionalCEMU').innerHTML = `<h2>Select Cemu Folder:</h2><button onclick="selectFolder()">Select Folder</button><br><br>`
        }
        if (SettingsData.BackupLevels == true) {
            document.getElementById("autoBackupLevels").checked = true;
        }
    } else if (page == "../pages/main.html") {
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
        currentHTMLPage = "savedLevels"
        loadSavedLevels();
    }
}

function loadSavedLevels(){
    const savedLevelsDrop = document.getElementById('savedlevelsDropdown')
    if (savedLevelsDrop.value == "downloaded") {
        loadDownloadedLevels()
    } else if (savedLevelsDrop.value == "cemu") {
        loadLevelsfromCEMU()
    } else if (savedLevelsDrop.value == "backupped") {
        loadBackuppedLevels()
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
    window.api.send("toMain", {action:"save-settings", settings:SettingsData});
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

function loadDownloadedLevels() {
    fetch(downloadCache)
        .then(response => response.json())
        .then(data => {
            //console.log(data);
            if (data.length > 0) {
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

            if (data.length > 0) {
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

function checkIfLevelisAlreadyDownloaded(levelid){
    window.api.send("toMain", {action:"checkIfAlreadyDownloaded", levelID:levelid});
}

function changedProfileDropdown(){
    const currentProfile = document.getElementById('profile-Dropdown').value;
    setSetting("selectedProfile", currentProfile);
}

window.addEventListener('DOMContentLoaded', () => {
    window.api.receive("fromMain", (data) => {
        if (data.action == "selectedFolder") {
            setSetting("CemuDirPath", data.path);
        }
        if (data.action == "saveing") {
            console.log(data.result)
        }
        if (data.action == "download-info") {
            if (data.resultType == "SUCCESS") {
                console.log("["+data.levelid+"] "+ currentStep[data.levelid] +" / "+ totalSteps, data.info)
                currentStep[data.levelid] = null;
                const levelHTMLObj = document.getElementById(`object-${data.levelid}`)
                const levelDisplayObjDownloadActions = document.getElementById(`download-actions`)
                if (levelHTMLObj) {
                    levelHTMLObj.getElementsByClassName("downloaded-display")[0].innerHTML = `<h2>Already Downloaded</h2>`
                }
                if (levelDisplayObjDownloadActions) {
                    levelDisplayObjDownloadActions.innerHTML = ``
                }
            } else if (data.resultType == "IN_PROGRESS") {
                console.log("["+data.levelid+"] "+ currentStep[data.levelid] +" / "+ totalSteps, data.info)
                currentStep[data.levelid]++;
            } else if (data.resultType == "ERROR") {
                currentStep[data.levelid] = null;
                console.log("["+data.levelid+"] "+ data.info)
            }
        }
        if (data.action == "checkIfAlreadyDownloaded-info") {
            //console.log(data)
            if (data.answer == true){
                const levelHTMLObj = document.getElementById(`object-${data.levelid}`)
                const levelDisplayObjDownloadActions = document.getElementById(`download-actions`)
                if (levelHTMLObj) {
                    levelHTMLObj.getElementsByClassName("downloaded-display")[0].innerHTML = `<h2>Already Downloaded</h2>`
                }
                if (levelDisplayObjDownloadActions) {
                    levelDisplayObjDownloadActions.innerHTML = ``
                }
            }
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
            document.getElementById('scrollable-objects').innerHTML = `<h2>Levels in Cemu Storage</h2>`;
            displayLevels(data.levels);
            console.log(data.levels)
        }
    });
});