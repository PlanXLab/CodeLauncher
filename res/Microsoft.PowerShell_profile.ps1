# PowerShell profile for the CodeLauncher portable environment

[Console]::InputEncoding  = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$Base         = $PSScriptRoot
$Bin          = "$Base\bin"
$Modules      = "$Base\Modules"
$InstallRoot  = [System.IO.Path]::GetFullPath("$Base\..\..\..")
$PortableData = "$InstallRoot\data"
$CacheDir     = "$PortableData\cache\pwsh-init"

if (-not [System.IO.Directory]::Exists($CacheDir)) {
    [void][System.IO.Directory]::CreateDirectory($CacheDir)
}

# Fast String-based PATH & PSModulePath setup (No regex/array splitting overhead)
if ($env:Path -notlike "*$Bin*") {
    $env:Path = "$Bin;$env:Path"
}
if ($env:PSModulePath -notlike "*$Modules*") {
    $env:PSModulePath = "$Modules;$env:PSModulePath"
}

# Import modern-unix-win (Fast path registration only)
# Note: Terminal-Icons is removed because 'lsd' already provides native icons at 100x speed!
Import-Module modern-unix-win -ErrorAction SilentlyContinue

# Cached Oh My Posh initialization with theme and executable cache validation
$OmpTheme    = "$PortableData\lib\pwsh\ohmyposh\themes\tos-therm.omp.json"
$OhMyPoshExe = "$Bin\oh-my-posh.exe"
$OmpCacheDir = "$PortableData\cache\oh-my-posh"
$env:POSH_CACHE_PATH = $OmpCacheDir
$OmpInitCache = "$CacheDir\omp-init-v2.ps1"
$OmpInitState = "$CacheDir\omp-init-v2.state"

if ([System.IO.File]::Exists($OhMyPoshExe) -and [System.IO.File]::Exists($OmpTheme)) {
    $utf8NoBom = [System.Text.UTF8Encoding]::new($false, $true)
    $themeBytes = [System.IO.File]::ReadAllBytes($OmpTheme)
    $themeHasBom = $themeBytes.Length -ge 3 -and
        $themeBytes[0] -eq 0xEF -and $themeBytes[1] -eq 0xBB -and $themeBytes[2] -eq 0xBF
    if ($themeHasBom) {
        $themeContent = $utf8NoBom.GetString($themeBytes, 3, $themeBytes.Length - 3).TrimStart([char]0xFEFF)
        $themeTempPath = "$OmpTheme.$PID.$([guid]::NewGuid().ToString('N')).tmp"
        try {
            [System.IO.File]::WriteAllText($themeTempPath, $themeContent, [System.Text.UTF8Encoding]::new($false))
            [System.IO.File]::Replace($themeTempPath, $OmpTheme, $null)
        }
        finally {
            if ([System.IO.File]::Exists($themeTempPath)) {
                [System.IO.File]::Delete($themeTempPath)
            }
        }
    }

    Remove-Item Env:\POSH_SESSION_ID -ErrorAction SilentlyContinue
    [void][System.IO.Directory]::CreateDirectory($OmpCacheDir)

    $safeTheme = $OmpTheme -replace '\\', '/'
    $themeHash = (Get-FileHash -LiteralPath $OmpTheme -Algorithm SHA256).Hash
    $ompExeInfo = Get-Item -LiteralPath $OhMyPoshExe
    $ompCacheKey = "$safeTheme|$themeHash|$($ompExeInfo.Length)|$($ompExeInfo.LastWriteTimeUtc.Ticks)"
    $cachedKey = if ([System.IO.File]::Exists($OmpInitState)) {
        [System.IO.File]::ReadAllText($OmpInitState).Trim()
    } else {
        ''
    }

    if (-not [System.IO.File]::Exists($OmpInitCache) -or $cachedKey -cne $ompCacheKey) {
        foreach ($cacheDirectory in @(
            $OmpCacheDir,
            $(if ($env:LOCALAPPDATA) { Join-Path $env:LOCALAPPDATA 'oh-my-posh' })
        )) {
            if (-not $cacheDirectory -or -not [System.IO.Directory]::Exists($cacheDirectory)) {
                continue
            }

            foreach ($cacheEntry in [System.IO.Directory]::GetFileSystemEntries($cacheDirectory)) {
                if ([System.IO.Directory]::Exists($cacheEntry)) {
                    [System.IO.Directory]::Delete($cacheEntry, $true)
                } else {
                    [System.IO.File]::Delete($cacheEntry)
                }
            }
        }

        foreach ($staleCacheFile in @($OmpInitCache, $OmpInitState)) {
            if ([System.IO.File]::Exists($staleCacheFile)) {
                [System.IO.File]::Delete($staleCacheFile)
            }
        }

        $ompInitOutput = & $OhMyPoshExe init pwsh --config $safeTheme --print
        $ompInitExitCode = $LASTEXITCODE
        if ($ompInitExitCode -ne 0) {
            throw "Oh My Posh initialization failed with exit code $ompInitExitCode."
        }

        $ompInitScript = $ompInitOutput -join [Environment]::NewLine
        if ([string]::IsNullOrWhiteSpace($ompInitScript)) {
            throw 'Oh My Posh did not generate its PowerShell initialization script.'
        }

        $ompInitTempPath = "$OmpInitCache.$PID.$([guid]::NewGuid().ToString('N')).tmp"
        $ompStateTempPath = "$OmpInitState.$PID.$([guid]::NewGuid().ToString('N')).tmp"
        try {
            [System.IO.File]::WriteAllText($ompInitTempPath, $ompInitScript, [System.Text.UTF8Encoding]::new($false))
            [System.IO.File]::WriteAllText($ompStateTempPath, $ompCacheKey, [System.Text.UTF8Encoding]::new($false))
            [System.IO.File]::Move($ompInitTempPath, $OmpInitCache)
            [System.IO.File]::Move($ompStateTempPath, $OmpInitState)
        }
        finally {
            foreach ($tempPath in @($ompInitTempPath, $ompStateTempPath)) {
                if ([System.IO.File]::Exists($tempPath)) {
                    [System.IO.File]::Delete($tempPath)
                }
            }
        }
    }

    . $OmpInitCache
}

# Cached zoxide Initialization (Saves ~100ms per startup)
$zoxideInitCache = "$CacheDir\zoxide-init.ps1"
if (-not [System.IO.File]::Exists($zoxideInitCache)) {
    if (Get-Command zoxide -ErrorAction SilentlyContinue) {
        & zoxide init powershell | Set-Content -Path $zoxideInitCache -Encoding utf8NoBOM
    }
}
if ([System.IO.File]::Exists($zoxideInitCache)) {
    . $zoxideInitCache
}

# Zero-cost Alias Binding (No Get-Command disk lookup on startup)
Remove-Item Alias:ls -ErrorAction SilentlyContinue
function ls { lsd @args }
function ll { lsd -l @args }
function la { lsd -lall @args }

$env:BAT_THEME = 'ansi'
Set-Alias -Name cat  -Value bat   -Force
Set-Alias -Name dig  -Value dog   -Force
Set-Alias -Name grep -Value rg    -Force
Set-Alias -Name find -Value fd    -Force
Set-Alias -Name ps   -Value procs -Force
Set-Alias -Name top  -Value btm   -Force
Set-Alias -Name du   -Value dust  -Force
Set-Alias -Name df   -Value duf   -Force
Set-Alias -Name ping -Value gping -Force
Set-Alias -Name http -Value xh    -Force
Set-Alias -Name sed  -Value sd    -Force

# Configure PSReadLine FIRST
$HistoryPath = "$PortableData\user-data\PowerShell\History.txt"
$HistoryDir  = [System.IO.Path]::GetDirectoryName($HistoryPath)
if (-not [System.IO.Directory]::Exists($HistoryDir)) {
    [void][System.IO.Directory]::CreateDirectory($HistoryDir)
}

Set-PSReadLineOption -EditMode Emacs `
                     -PredictionSource HistoryAndPlugin `
                     -PredictionViewStyle ListView `
                     -HistoryNoDuplicates `
                     -HistorySearchCursorMovesToEnd `
                     -HistorySavePath $HistoryPath

# Lazy-Load PSFzf on first keypress
$global:PSFzfLoaded = $false
function Invoke-LazyPsFzf ($TargetKey) {
    if (-not $global:PSFzfLoaded) {
        Import-Module PSFzf -ErrorAction SilentlyContinue
        Set-PsFzfOption -PsReadlineChordProvider 'Ctrl+t' `
                        -PsReadlineChordReverseHistory 'Ctrl+r'
        $global:PSFzfLoaded = $true
    }
    
    $boundHandler = Get-PSReadLineKeyHandler -Bound | Where-Object { $_.Key -eq $TargetKey }
    if ($boundHandler -and $boundHandler.ScriptBlock) {
        & $boundHandler.ScriptBlock
    }
}

Set-PSReadLineKeyHandler -Key 'Ctrl+t' -ScriptBlock { Invoke-LazyPsFzf 'Ctrl+t' }
Set-PSReadLineKeyHandler -Key 'Ctrl+r' -ScriptBlock { Invoke-LazyPsFzf 'Ctrl+r' }

# Custom zsh Keybindings
Set-PSReadLineKeyHandler -Key Ctrl+a -Function BeginningOfLine
Set-PSReadLineKeyHandler -Key Ctrl+e -Function EndOfLine
Set-PSReadLineKeyHandler -Key Alt+b  -Function BackwardWord
Set-PSReadLineKeyHandler -Key Alt+f  -Function ForwardWord
Set-PSReadLineKeyHandler -Key Ctrl+u -Function BackwardDeleteLine
Set-PSReadLineKeyHandler -Key Ctrl+k -Function ForwardDeleteLine
Set-PSReadLineKeyHandler -Key Tab    -Function MenuComplete

# Helper Functions
function which ($command) {
    (Get-Command -Name $command -ErrorAction SilentlyContinue).Path
}

function clh {
    $histPath = (Get-PSReadLineOption).HistorySavePath
    if ($histPath -and [System.IO.File]::Exists($histPath)) {
        Remove-Item $histPath -Force
    }
    [Microsoft.PowerShell.PSConsoleReadLine]::ClearHistory()
    Clear-History
}

function ..   { Set-Location .. }
function ...  { Set-Location ../.. }
function .... { Set-Location ../../.. }

# Help command '??'
function global:?? {
    $pvsInfo = [System.IO.Path]::GetFullPath("$Base\..\..\..\codelauncher.info")
    $modernUnixVersion = ''
    
    if ([System.IO.File]::Exists($pvsInfo)) {
        foreach ($line in [System.IO.File]::ReadLines($pvsInfo)) {
            if ($line -match '^MODERN_UNIX_WIN_VERSION=(.+)$') {
                $modernUnixVersion = $matches[1].Trim()
                break
            }
        }
    }
    
    if (-not $modernUnixVersion) {
        Write-Host "modern-unix-win version not found in codelauncher.info" -ForegroundColor Red
        return
    }
    
    $modernUnixBin = "$Modules\modern-unix-win\$modernUnixVersion\bin"
    if ([System.IO.Directory]::Exists($modernUnixBin)) {
        $descriptions = @{
            "bat"       = @{ desc = "A cat(1) clone with syntax highlighting and Git integration"; alias = "cat" }
            "broot"     = @{ desc = "A tree explorer and a customizable launcher"; alias = $null }
            "btm"       = @{ desc = "A customizable cross-platform graphical process/system monitor"; alias = "top" }
            "cheat"     = @{ desc = "Create and view interactive cheatsheets on the command-line"; alias = $null }
            "curlie"    = @{ desc = "The power of curl with the ease of use of httpie (curl frontend)"; alias = $null }
            "delta"     = @{ desc = "A viewer for git and diff output"; alias = $null }
            "dog"       = @{ desc = "Command-line DNS client"; alias = "dig" }
            "duf"       = @{ desc = "Disk Usage/Free Utility - a better df alternative"; alias = "df" }
            "dust"      = @{ desc = "A more intuitive version of du in rust"; alias = "du" }
            "fd"        = @{ desc = "A simple, fast and user-friendly alternative to find"; alias = "find" }
            "fzf"       = @{ desc = "A command-line fuzzy finder"; alias = $null }
            "gping"     = @{ desc = "Ping, but with a graph"; alias = "ping" }
            "hyperfine" = @{ desc = "A command-line benchmarking tool"; alias = $null }
            "jq"        = @{ desc = "Command-line JSON processor"; alias = $null }
            "lsd"       = @{ desc = "An ls command with a lot of pretty colors and some other stuff"; alias = "ls, ll, la" }
            "procs"     = @{ desc = "A modern replacement for ps"; alias = "ps" }
            "rg"        = @{ desc = "Recursively searches the current directory for a regex pattern"; alias = "grep" }
            "sd"        = @{ desc = "Intuitive find & replace CLI (sed alternative)"; alias = "sed" }
            "xh"        = @{ desc = "A friendly and fast tool for sending HTTP requests"; alias = "http" }
            "zoxide"    = @{ desc = "A smarter cd command for your terminal"; alias = "z" }
        }
        
        Write-Host "`nModern Unix Commands (modern-unix-win v$modernUnixVersion):" -ForegroundColor Cyan
        Write-Host ("=" * 80) -ForegroundColor DarkGray
        
        Get-ChildItem -Path $modernUnixBin -File | Sort-Object Name | ForEach-Object {
            $name = $_.BaseName
            $info = $descriptions[$name]
            if ($info) {
                Write-Host ("  " + $name.PadRight(12)) -ForegroundColor Green -NoNewline
                if ($info.alias) {
                    Write-Host ("[" + $info.alias + "]").PadRight(14) -ForegroundColor Yellow -NoNewline
                } else {
                    Write-Host (" " * 14) -NoNewline
                }
                Write-Host $info.desc -ForegroundColor Gray
            }
        }
        Write-Host ""
    }
}
