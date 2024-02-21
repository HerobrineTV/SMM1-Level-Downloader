const { ipcRenderer } = window; // Use provided ipcRenderer in Electron apps
const settingsFile = ('../SMMDownloader/data.json');
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

function loadFileInWindow(html, levelName, stars, creator, clearRate){
    console.log(levelName, stars, creator, clearRate);
}

function objectClicked(levelName, stars, creator, clearRate) {
    // Assuming 'filename' is the path to the HTML file to be loaded
    loadFileInWindow('../pages/download-level.html', levelName, stars, creator, clearRate);
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
            <div>Clear Rate: ${obj.clearrate}</div>
        `;
        objectDiv.addEventListener('click', () => objectClicked(obj.name, obj.stars, obj.creator, obj.clearrate));
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

function searchLevel() {
    // Get the search phrase from the input field
    const searchPhrase = document.getElementById('searchLevelText').value.trim();
    
    // Set the last search phrase in settings
    setSetting("lastSearchPhrase", searchPhrase);
    
    // Check if the API link should be used
    if (SettingsData.useAPILink) {
        // Construct the API URL with the search phrase
        const apiUrl = `${SettingsData.APILink}/smm1/searchLevelsByName/${encodeURIComponent(searchPhrase)}`;
        
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
                setSetting("recentFoundLevels", data);
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

function runLevelDownloader(link) {
    window.api.send("toMain", {action:"download-level", url:link})
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
    });
});