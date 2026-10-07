# PowerShell Profile for Portable VSCode (PVS)
# This profile initializes the portable environment with custom modules and tools

$Base    = $PSScriptRoot
$Bin     = Join-Path $Base 'bin'
$Modules = Join-Path $Base 'Modules'

# Initialize Oh My Posh
$OmpTheme   = Join-Path $Base 'ohmyposh\themes\tos-term.omp.json'
$OhMyPoshExe = Join-Path $Bin 'oh-my-posh.exe'
if ((Test-Path $OhMyPoshExe) -and (Test-Path $OmpTheme)) {
    & $OhMyPoshExe init pwsh --config $OmpTheme | Invoke-Expression
}

# Add bin directory to PATH if not already present
if ((Test-Path $Bin) -and (-not (($env:Path -split ';') -contains $Bin))) {
    $env:Path = "$Bin;$env:Path"
}

# Add Modules directory to PSModulePath
$existing = @()
if ($env:PSModulePath) {
    $existing = $env:PSModulePath -split ';' | Where-Object { $_ }
}
$portableModules = $Modules
if (-not ($existing -contains $portableModules)) {
    $env:PSModulePath = ($portableModules, $existing) -join ';'
}

# Import portable modules
Import-Module Terminal-Icons  -ErrorAction SilentlyContinue
Import-Module modern-unix-win -ErrorAction SilentlyContinue
Import-Module PSFzf           -ErrorAction SilentlyContinue

# Setting alias of modern-unix-win 
if (Get-Command lsd -ErrorAction SilentlyContinue) {
    Remove-Item alias:ls -ErrorAction SilentlyContinue
    function ls { lsd @args 2>$null }
    function ll { lsd -l @args 2>$null }
    function la { lsd -lall @args 2>$null }
}
if (Get-Command bat -ErrorAction SilentlyContinue) {
    Set-Alias -Name cat -Value bat -Force
    $env:BAT_THEME = 'ansi'  # Readable on both dark and light backgrounds
}
Set-Alias -Name dig -Value dog -Force
Set-Alias -Name grep -Value rg -Force
Set-Alias -Name find -Value fd -Force
Set-Alias -Name ps -Value procs -Force
Set-Alias -Name top -Value btm -Force
Set-Alias -Name du -Value dust -Force
Set-Alias -Name df -Value duf -Force
Set-Alias -Name ping -Value gping -Force
Set-Alias -Name http -Value xh -Force
if (Get-Command zoxide -ErrorAction SilentlyContinue) {
    Invoke-Expression (& zoxide init powershell | Out-String)
}
Set-Alias -Name sed -Value sd -Force

# Configure PSFzf if available (zsh-like: Ctrl+t, Ctrl+r)
if (Get-Module PSFzf -ListAvailable) {
    Set-PsFzfOption -PsReadlineChordProvider 'Ctrl+t' `
                    -PsReadlineChordReverseHistory 'Ctrl+r'
}

# Configure PSReadLine (built-in with PowerShell 7)
Set-PSReadLineOption -EditMode Emacs `
                     -PredictionSource HistoryAndPlugin `
                     -PredictionViewStyle ListView `
                     -HistoryNoDuplicates `
                     -HistorySearchCursorMovesToEnd `
                     -HistorySavePath ~\History.txt

# zsh keybinding
Set-PSReadLineKeyHandler -Key Ctrl+a -Function BeginningOfLine
Set-PSReadLineKeyHandler -Key Ctrl+e -Function EndOfLine
Set-PSReadLineKeyHandler -Key Alt+b  -Function BackwardWord
Set-PSReadLineKeyHandler -Key Alt+f  -Function ForwardWord
Set-PSReadLineKeyHandler -Key Ctrl+u -Function BackwardDeleteLine
Set-PSReadLineKeyHandler -Key Ctrl+k -Function ForwardDeleteLine

# Tab: (zsh menu-complete)
Set-PSReadLineKeyHandler -Key Tab -Function MenuComplete

# Custom 'which' command (zsh which)
function which ($command) {
    Get-Command -Name $command -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty Path -ErrorAction SilentlyContinue
}

# Clear all history (file + in-memory PSReadLine buffer + session history)
function clh {
    $histPath = (Get-PSReadLineOption).HistorySavePath
    if ($histPath -and (Test-Path $histPath)) {
        Remove-Item $histPath -Force
    }
    [Microsoft.PowerShell.PSConsoleReadLine]::ClearHistory()
    Clear-History
}

# zsh style/alias
function ..    { Set-Location .. }
function ...   { Set-Location ../.. }
function ....  { Set-Location ../../.. }

# Remove the default '?' alias (Where-Object) and replace with custom function
if (Get-Alias -Name '?' -ErrorAction SilentlyContinue) {
    Remove-Item -Path Alias:? -Force
}

# Display modern-unix-win commands when '?' is entered
function global:? {
    # Read version from pvs.info
    $pvsInfo = Join-Path $Base '..\..\..\pvs.info'
    $pvsInfo = [System.IO.Path]::GetFullPath($pvsInfo)
    $modernUnixVersion = ''
    
    if (Test-Path $pvsInfo) {
        $lines = Get-Content $pvsInfo
        foreach ($line in $lines) {
            if ($line -match '^MODERN_UNIX_WIN_VERSION=(.+)$') {
                $modernUnixVersion = $matches[1]
                break
            }
        }
    }
    
    if (-not $modernUnixVersion) {
        Write-Host "modern-unix-win version not found in pvs.info" -ForegroundColor Red
        Write-Host "Expected path: $pvsInfo" -ForegroundColor Gray
        return
    }
    
    $modernUnixBin = Join-Path $Modules "modern-unix-win\$modernUnixVersion\bin"
    
    if (Test-Path $modernUnixBin) {
        # Command descriptions with aliases
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
        
        Get-ChildItem -Path $modernUnixBin -File |
            Where-Object { $_.Extension -in @('.exe', '.bat', '.cmd', '') } |
            Sort-Object Name |
            ForEach-Object {
                $name = $_.BaseName
                $info = $descriptions[$name]
                if ($info) {
                    Write-Host "  " -NoNewline
                    
                    # Show command name
                    Write-Host $name.PadRight(12) -ForegroundColor Green -NoNewline
                    
                    # Show alias if exists
                    if ($info.alias) {
                        Write-Host "[" -ForegroundColor DarkGray -NoNewline
                        Write-Host $info.alias -ForegroundColor Cyan -NoNewline
                        Write-Host "] " -ForegroundColor DarkGray -NoNewline
                    }
                    
                    # Show description
                    Write-Host $info.desc -ForegroundColor Gray
                } else {
                    Write-Host "  $name" -ForegroundColor Green
                }
            }
        
        # Custom profile commands
        Write-Host "`nCustom Commands:" -ForegroundColor Cyan
        Write-Host ("=" * 80) -ForegroundColor DarkGray
        @(
            @{ name = "clh";   desc = "Clear all terminal history (file + buffer + session)" }
            @{ name = "which"; desc = "Show full path of a command" }
            @{ name = ".. / ... / ...."; desc = "Go up 1 / 2 / 3 directories" }
        ) | ForEach-Object {
            Write-Host "  $($_.name.PadRight(20))" -ForegroundColor Green -NoNewline
            Write-Host $_.desc -ForegroundColor Gray
        }

        Write-Host "`nTip: Use --help with any command for detailed usage (e.g., 'bat --help')" -ForegroundColor Yellow
    } else {
        Write-Host "modern-unix-win module not found at: $modernUnixBin" -ForegroundColor Red
    }
}

# Python script handler: foo.py, ./foo.py, ./ch1/foo.py
$ExecutionContext.InvokeCommand.PreCommandLookupAction = {
    param($CommandName, $CommandLookupEventArgs)

    if ($CommandName -notmatch '\.py$') { return }

    $filePath = $null
    if ([System.IO.Path]::IsPathRooted($CommandName)) {
        if (Test-Path $CommandName) { $filePath = $CommandName }
    } elseif ($CommandName -match '^\.[\\/](.+)$') {
        $candidate = Join-Path (Get-Location).Path $Matches[1]
        if (Test-Path $candidate) { $filePath = $candidate }
    } else {
        $candidate = Join-Path (Get-Location).Path $CommandName
        if (Test-Path $candidate) { $filePath = $candidate }
    }

    if (-not $filePath) { return }

    $firstLine   = Get-Content $filePath -First 1 -ErrorAction SilentlyContinue
    $interpreter = if ($firstLine -match '^#!replx\s*$') { 'replx' } else { 'python' }

    $CommandLookupEventArgs.CommandScriptBlock = {
        & $interpreter $filePath @args
    }.GetNewClosure()
    $CommandLookupEventArgs.StopSearch = $true
}

# Command Not Found handler: zsh AUTO_CD
$ExecutionContext.InvokeCommand.CommandNotFoundAction = {
    param($CommandName, $CommandLookupEventArgs)

    if (Test-Path $CommandName -PathType Container) {
        $CommandLookupEventArgs.CommandScriptBlock = {
            Set-Location $CommandName
        }.GetNewClosure()
        $CommandLookupEventArgs.StopSearch = $true
    }
}
