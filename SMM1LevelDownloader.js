const axios = require('axios');
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
const outputDirectory = path.join(__dirname, 'SMMDownloader');
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

  function splitFile(filePath) {
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
    const partsDirectory = path.join(outputDirectory, `${path.basename(filePath, path.extname(filePath))}_Extracted`);
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

    decompressAndRenameFiles(partsDirectory, parts.length, partNamesFirst);
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
  async function decompressAndRenameFiles(partsDirectory, partCount, partNamesFirst) {
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

    console.log(`All files have been decompressed, u find them here ${partsDirectory}`);
}

async function processUrl(originalUrl) {
    const archiveUrl = await fetchArchiveUrl(originalUrl);
    if (!archiveUrl) return;

    const fileName = path.basename(new URL(originalUrl).pathname);
    const outputPath = path.join(outputDirectory, fileName);

    await downloadFile(archiveUrl, outputPath);
    splitFile(outputPath);
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

  startProcess();

// Course URLS have to be retrieved from https://app.gigasheet.com/spreadsheet/courses-jsonl/1493e4c3_5fed_45cb_b189_2e1428df82d5 or a copy of this sheet

//const originalUrl = 'https://d2sno3mhmk1ekx.cloudfront.net/10.WUP_AMAJ_datastore/ds/1/data/00030699122-00001?Expires=1624406273&Signature=dvCQjz~tTf09havdMwRVryzz9MfRp16RF5NJWQa8k-wJiizAOlbmb9kMsT5Kv9j4-QJ1RzU1rwit4QFFTF8Jg7wRRwS0RVjENcuxy6wag-~v187HMsX3yMGRs8VxSx5Syem9ZxjTqGpBqRfm~71rQYKH~32vqDVTXR6IRtyOnKtAWfIikJK8Tk0jBQM~fFqv4OqqFCRhHRjFyp8hJPMaz8P5qIm~puSkJ0wUNvDKV0upwQw9RJiDABo1aRkcpW0QghK1xfQEEHCG4RVOn5Zng6rBNhSOLGcJe~K0bBffA~Y5kkgEbOl18c-BXXy3-z3hV-mnIcxRU9e6VNMo00M1Zw__&Key-Pair-Id=APKAJUYKVK3BE6ZPNZBQ'
//processUrl(originalUrl);
