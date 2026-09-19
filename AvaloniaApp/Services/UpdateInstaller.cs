using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SMMDownloader.Avalonia.Services;

public static class UpdateInstaller
{
    private const string ApplyUpdateArgument = "--apply-update";

    public static bool TryHandleUpdateArgs(string[] args)
    {
        if (args.Length != 2 || !string.Equals(args[0], ApplyUpdateArgument, StringComparison.Ordinal))
        {
            return false;
        }

        ApplyUpdate(args[1]);
        return true;
    }

    public static void Start(UpdatePlan plan)
    {
        var updateDirectory = Path.Combine(Path.GetTempPath(), "SMM1-Level-Downloader-Updater", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(updateDirectory);

        var manifestPath = Path.Combine(updateDirectory, "update.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(plan, JsonStore.JsonOptions));

        if (OperatingSystem.IsWindows())
        {
            var scriptPath = Path.Combine(updateDirectory, "apply-update.ps1");
            File.WriteAllText(scriptPath, BuildWindowsScript(manifestPath));
            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            return;
        }

        var shellScriptPath = Path.Combine(updateDirectory, "apply-update.sh");
        File.WriteAllText(shellScriptPath, BuildUnixScript(manifestPath));
        TryMakeExecutable(shellScriptPath);
        Process.Start(new ProcessStartInfo
        {
            FileName = "/bin/sh",
            Arguments = $"\"{shellScriptPath}\"",
            UseShellExecute = false
        });
    }

    private static string BuildWindowsScript(string manifestPath)
    {
        var escapedManifestPath = manifestPath.Replace("'", "''", StringComparison.Ordinal);
        return $$"""
$ErrorActionPreference = 'Stop'
$manifestPath = '{{escapedManifestPath}}'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
Wait-Process -Id $manifest.ParentProcessId -ErrorAction SilentlyContinue
$actualHash = (Get-FileHash -LiteralPath $manifest.PackagePath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualHash -ne $manifest.ExpectedSha256.ToLowerInvariant()) {
    throw "Downloaded update hash mismatch. Expected $($manifest.ExpectedSha256), got $actualHash."
}
$staging = Join-Path ([IO.Path]::GetDirectoryName($manifestPath)) 'staging'
if (Test-Path -LiteralPath $staging) {
    Remove-Item -LiteralPath $staging -Recurse -Force
}
New-Item -ItemType Directory -Path $staging | Out-Null
if ($manifest.PackagePath.ToLowerInvariant().EndsWith('.zip')) {
    Expand-Archive -LiteralPath $manifest.PackagePath -DestinationPath $staging -Force
    $source = $staging
    $entries = Get-ChildItem -LiteralPath $staging -Force
    if ($entries.Count -eq 1 -and $entries[0].PSIsContainer) {
        $source = $entries[0].FullName
    }
    Get-ChildItem -LiteralPath $source -Force | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $manifest.InstallDirectory -Recurse -Force
    }
} else {
    Copy-Item -LiteralPath $manifest.PackagePath -Destination $manifest.AppExecutablePath -Force
}
Start-Process -FilePath $manifest.AppExecutablePath -WorkingDirectory $manifest.InstallDirectory
""";
    }

    private static string BuildUnixScript(string manifestPath)
    {
        var escapedManifestPath = manifestPath.Replace("'", "'\"'\"'", StringComparison.Ordinal);
        return $$"""
#!/bin/sh
set -eu
manifest_path='{{escapedManifestPath}}'

read_json_string() {
    file="$1"
    key="$2"
    sed -n "s/.*\"$key\"[[:space:]]*:[[:space:]]*\"\([^\"]*\)\".*/\1/p; s/.*\"$key\"[[:space:]]*:[[:space:]]*\([0-9][0-9]*\).*/\1/p" "$file" | head -n 1
}

parent_pid=$(read_json_string "$manifest_path" parentProcessId 2>/dev/null || true)
package_path=$(read_json_string "$manifest_path" packagePath)
expected_sha256=$(read_json_string "$manifest_path" expectedSha256 | tr '[:upper:]' '[:lower:]')
install_directory=$(read_json_string "$manifest_path" installDirectory)
app_executable_path=$(read_json_string "$manifest_path" appExecutablePath)

if [ -n "$parent_pid" ]; then
    while kill -0 "$parent_pid" 2>/dev/null; do
        sleep 1
    done
fi

if command -v sha256sum >/dev/null 2>&1; then
    actual_sha256=$(sha256sum "$package_path" | awk '{print tolower($1)}')
elif command -v shasum >/dev/null 2>&1; then
    actual_sha256=$(shasum -a 256 "$package_path" | awk '{print tolower($1)}')
else
    echo "No SHA-256 tool found." >&2
    exit 1
fi

if [ "$actual_sha256" != "$expected_sha256" ]; then
    echo "Downloaded update hash mismatch. Expected $expected_sha256, got $actual_sha256." >&2
    exit 1
fi

staging="$(dirname "$manifest_path")/staging"
rm -rf "$staging"
mkdir -p "$staging"

case "$package_path" in
    *.zip|*.ZIP)
        if ! command -v unzip >/dev/null 2>&1; then
            echo "unzip is required to install zip updates." >&2
            exit 1
        fi
        unzip -oq "$package_path" -d "$staging"
        source_dir="$staging"
        entry_count=$(find "$staging" -mindepth 1 -maxdepth 1 | wc -l | tr -d ' ')
        first_entry=$(find "$staging" -mindepth 1 -maxdepth 1 | head -n 1)
        if [ "$entry_count" = "1" ] && [ -d "$first_entry" ]; then
            source_dir="$first_entry"
        fi
        cp -R "$source_dir"/. "$install_directory"/
        ;;
    *)
        cp "$package_path" "$app_executable_path"
        chmod +x "$app_executable_path" 2>/dev/null || true
        ;;
esac

chmod +x "$app_executable_path" 2>/dev/null || true
cd "$install_directory"
"$app_executable_path" >/dev/null 2>&1 &
""";
    }

    private static void TryMakeExecutable(string path)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
        }
        catch
        {
            // /bin/sh can still execute the script when it is passed as an argument.
        }
    }

    private static void ApplyUpdate(string manifestPath)
    {
        var plan = JsonSerializer.Deserialize<UpdatePlan>(File.ReadAllText(manifestPath), JsonStore.JsonOptions)
                   ?? throw new InvalidOperationException("Invalid update manifest.");

        try
        {
            Process.GetProcessById(plan.ParentProcessId).WaitForExit();
        }
        catch
        {
            // The parent already exited.
        }

        var actualHash = ComputeSha256(plan.PackagePath);
        if (!string.Equals(actualHash, plan.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Downloaded update hash mismatch. Expected {plan.ExpectedSha256}, got {actualHash}.");
        }

        if (plan.PackagePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            var stagingDirectory = Path.Combine(Path.GetDirectoryName(manifestPath)!, "staging");
            if (Directory.Exists(stagingDirectory))
            {
                Directory.Delete(stagingDirectory, true);
            }

            ZipFile.ExtractToDirectory(plan.PackagePath, stagingDirectory, true);
            var sourceDirectory = ResolveArchiveRoot(stagingDirectory);
            CopyDirectory(sourceDirectory, plan.InstallDirectory);
        }
        else
        {
            File.Copy(plan.PackagePath, plan.AppExecutablePath, true);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = plan.AppExecutablePath,
            WorkingDirectory = plan.InstallDirectory,
            UseShellExecute = true
        });
    }

    public static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string ResolveArchiveRoot(string stagingDirectory)
    {
        var directories = Directory.GetDirectories(stagingDirectory);
        var files = Directory.GetFiles(stagingDirectory);
        return directories.Length == 1 && files.Length == 0 ? directories[0] : stagingDirectory;
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);
        foreach (var sourcePath in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, sourcePath);
            var destinationPath = Path.Combine(destinationDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(sourcePath, destinationPath, true);
        }
    }
}

public sealed class UpdatePlan
{
    [JsonPropertyName("parentProcessId")]
    public int ParentProcessId { get; set; }

    [JsonPropertyName("packagePath")]
    public string PackagePath { get; set; } = "";

    [JsonPropertyName("expectedSha256")]
    public string ExpectedSha256 { get; set; } = "";

    [JsonPropertyName("installDirectory")]
    public string InstallDirectory { get; set; } = "";

    [JsonPropertyName("appExecutablePath")]
    public string AppExecutablePath { get; set; } = "";
}
