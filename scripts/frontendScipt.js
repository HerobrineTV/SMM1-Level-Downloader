const { ipcRenderer } = window; // Use provided ipcRenderer in Electron apps


function CEMUcheckBoxChanged() {
    if (document.getElementById("useCEMUfolderValue").value == "false") {
        document.getElementById("useCEMUfolderValue").value = "true"
        document.getElementById('optionalCEMU').innerHTML = `<h2>Select Cemu Folder:</h2><button onclick="selectFolder()">Select Folder (Coming Soon)</button><br><br>`
    } else if (document.getElementById("useCEMUfolderValue").value == "true") {
        document.getElementById("useCEMUfolderValue").value = "false"
        document.getElementById('optionalCEMU').innerHTML = ``
    } else {
        document.getElementById("useCEMUfolderValue").value = "true"
        document.getElementById('optionalCEMU').innerHTML = `<h2>Select Cemu Folder:</h2><button onclick="selectFolder()">Select Folder (Coming Soon)</button><br><br>`
    }
}

function searchCheckBoxesChanged(searchType){
    if (searchType == "LevelName") {
        if (document.getElementById("searchLevelbyLevelName").value == "false") {
            document.getElementById("searchLevelbyLevelName").value = "true"
        } else if (document.getElementById("searchLevelbyLevelName").value == "true") {
            document.getElementById("searchLevelbyLevelName").value = "false"
        } else {
            document.getElementById("searchLevelbyLevelName").value = "true"
        }
    }
    
    if (searchType == "LevelID") {
        if (document.getElementById("searchLevelbyLevelID").value == "false") {
            document.getElementById("searchLevelbyLevelID").value = "true"
        } else if (document.getElementById("searchLevelbyLevelID").value == "true") {
            document.getElementById("searchLevelbyLevelID").value = "false"
        } else {
            document.getElementById("searchLevelbyLevelID").value = "true"
        }
    }

    if (document.getElementById("searchLevelbyLevelName").value == "true" || document.getElementById("searchLevelbyLevelID").value == "true") {
        document.getElementById("searchLevel-item").innerHTML = `<label class="searchLevel-label" for="searchLevelText">Search: </label><input type="text" id="searchLevelText" name="searchLevel-option" value="">`
    } else {
        document.getElementById("searchLevel-item").innerHTML = ``
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
        })
        .catch(error => console.log(error));
}

function saveSettings() {

}

function loadSettings() {

}

window.addEventListener('DOMContentLoaded', () => {
    window.api.receive("fromMain", (data) => {
        if (data.action == "selectedFolder") {
            document.getElementById('selectedFolderValue').value = data.path;
            document.getElementById('selectedFolder').innerHTML = `<h2>Selected Folder: ${data.path}</h2>`
        }
    });
});