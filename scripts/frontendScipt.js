const { ipcRenderer } = window; // Use provided ipcRenderer in Electron apps
const settingsFile = ('../SMMDownloader/Data/data.json');
const downloadCache = ('../SMMDownloader/Data/downloaded.json');
let SettingsData;


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

    levelInfo.innerHTML = `
    <div class="level-info">
        <!-- Use an icon or image here for a visual touch -->
        <h2 id="levelName">${levelObj.name}</h2>
    </div>
    <div class="level-details">
        <!-- Example of incorporating Mario-themed imagery -->
        <p><strong>Uploader:</strong> <span id="uploader">${levelObj.creator}</span> <img src="mario-icon.png" alt="" style="height:20px;"></p>
        <p><strong>Upload Time:</strong> <span id="uploadTime">${levelObj.uploadTime}</span></p>
        <p><strong>Level ID:</strong> <span id="levelID">${levelObj.levelid}</span></p>
    </div>
    <div class="level-stats">
        <p><strong>Clear Rate:</strong> <span id="clearRate">${(levelObj.clearrate*100).toFixed(2).replace(/(\.0+|(\.\d+?)0+)$/, '$2')}%</span></p>
        <p><strong>Total Attempts:</strong> <span id="totalAttempts">${levelObj.total_attempts}</span></p>
        <p><strong>Completions:</strong> <span id="completions">${levelObj.clears}</span></p>
        <p><strong>Record Time:</strong> <span id="recordTime">${formatTime(levelObj.world_record_ms)}</span></p>
        <p><strong>Record Achieved Date:</strong> <span id="recordDate">${levelObj.world_record_achieved_date}</span></p>
        <p><strong>Record Holder:</strong> <span id="recordHolder">${levelObj.world_record_holder_nnid}</span></p>
    </div>
    <div class="actions">
        <!-- Consider adding Mario-themed button icons or styles -->
        <select id="course-dropdown">
            <option value="option1">course000</option>
            <option value="option2">course001</option>
            <option value="option3">course002</option>
        </select>
        <button class="searchdownload-btn">Download</button>
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
    objectsContainer.innerHTML = '';

    levels.forEach(obj => {
        const objectDiv = document.createElement('div');
        objectDiv.classList.add('object');
        objectDiv.innerHTML = `
            <div>${obj.name}</div>
            <div>Stars: ${obj.stars}</div>
            <div>Creator: ${obj.creator}</div>
            <div>Clear Rate: ${(obj.clearrate*100).toFixed(2).replace(/(\.0+|(\.\d+?)0+)$/, '$2')}%</div>
        `;
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


function searchLevel() {
    // Get the search phrase from the input field
    const searchPhrase = document.getElementById('searchLevelText').value.trim();
    
    // Set the last search phrase in settings
    setSetting("lastSearchPhrase", searchPhrase);
    
    // Check if the API link should be used
    if (SettingsData.useAPILink) {
        // Construct the API URL with the search phrase
        const apiUrl = `${SettingsData.APILink}/searchLevelsByName/${encodeURIComponent(searchPhrase)}`;
        
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
                setSetting("recentFoundLevels", transformToDict(data));
                displayLevels(data);
            })
            .catch(error => {
                // Handle errors that occur during the fetch
                console.error('There was a problem with the fetch operation:', error);
            });
    }
}

function selectFolder() {
    window.api.send("toMain", {action:"select-folder"});
}

function runLevelDownloader(levelObj) {
    link = levelObj.url;
    levelID = levelObj.levelid;
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
        if (SettingsData.useCemuDir == true) {
            document.getElementById("useCEMUfolder").checked = true;
            document.getElementById('optionalCEMU').innerHTML = `<h2>Select Cemu Folder:</h2><button onclick="selectFolder()">Select Folder</button><br><br>`
        }
    } else if (page == "../pages/main.html") {
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

        if (amounttrue > 0) {
            document.getElementById("searchLevel-item").innerHTML = `<label class="searchLevel-label" for="searchLevelText">Search: </label><input type="text" id="searchLevelText" name="searchLevel-option" value="">`
            const source = document.getElementById('searchLevelText');

            source.addEventListener('input', searchTextInputChanged);
            source.addEventListener('propertychange', searchTextInputChanged);
        } else {
            document.getElementById("searchLevel-item").innerHTML = ``
        }
        setSetting("amounttrue", amounttrue);
    } else if (page == "../pages/downloadedLevels.html") {
        loadDownloadedLevels();
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
            console.log(objectToArray(data))
            displayLevels(objectToArray(data));
        })
        .catch(error => console.log(error));
}

function displayLevels(levels) {
    addObjects(levels);
}

function exitApp() {
    window.api.send("toMain", {action:"exit-app"});
}

window.addEventListener('DOMContentLoaded', () => {
    window.api.receive("fromMain", (data) => {
        if (data.action == "selectedFolder") {
            setSetting("CemuDirPath", data.path);
        }
        if (data.action == "saveing") {
            console.log(data.result)
        }
        if (data.action == "found-levels"){
            if (data.resultType == "SUCCESS") {
                setSetting("recentFoundLevels", data.levels);
                displayLevels(data.levels);
            } else {
                console.log(data.result)
            }
        }
        if (data.action == "download-info") {
            if (data.resultType == "SUCCESS") {
                console.log(data.resultMessage)
            } else {
                console.log(data.resultMessage)
            }
        }
    });
});