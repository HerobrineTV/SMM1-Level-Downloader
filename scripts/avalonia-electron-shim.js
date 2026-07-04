const { spawn } = require('child_process');
const readline = require('readline');

const bridgePrefix = '__SMM_AVALONIA__';
let ipcHandler = null;

function sendBridgeMessage(payload) {
  process.stdout.write(`${bridgePrefix}${JSON.stringify(payload)}\n`);
}

class BrowserWindow {
  constructor() {
    this.webContents = {
      send: (channel, data) => sendBridgeMessage({ channel, data })
    };
  }

  setMenu() {}
  setIcon() {}
  loadFile() {}
}

const ipcMain = {
  on(channel, handler) {
    if (channel === 'toMain') {
      ipcHandler = handler;
    }
  }
};

const app = {
  commandLine: {
    appendSwitch() {}
  },
  whenReady() {
    return Promise.resolve();
  },
  quit() {
    process.exit(0);
  }
};

const dialog = {
  showOpenDialog() {
    return Promise.resolve({ filePaths: [] });
  }
};

function openWithShell(target) {
  if (!target) {
    return Promise.resolve('');
  }

  const command = process.platform === 'win32'
    ? ['cmd', ['/c', 'start', '', target]]
    : process.platform === 'darwin'
      ? ['open', [target]]
      : ['xdg-open', [target]];

  const child = spawn(command[0], command[1], {
    detached: true,
    stdio: 'ignore',
    shell: false
  });

  child.unref();
  return Promise.resolve('');
}

const shell = {
  openPath: openWithShell,
  openExternal: openWithShell
};

const nativeImage = {
  createFromPath(path) {
    return path;
  }
};

const rl = readline.createInterface({
  input: process.stdin,
  crlfDelay: Infinity
});

rl.on('line', line => {
  if (!ipcHandler || !line.trim()) {
    return;
  }

  try {
    const message = JSON.parse(line);
    if (message.channel === 'toMain') {
      ipcHandler({}, message.data);
    }
  } catch (error) {
    process.stderr.write(`Avalonia bridge parse error: ${error.message}\n`);
  }
});

module.exports = {
  app,
  BrowserWindow,
  dialog,
  ipcMain,
  shell,
  nativeImage
};
