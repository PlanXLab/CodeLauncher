# VSCode Portable - Build Script
# Compiles launcher.cs → launcher.exe, then installer.cs → installer.exe

param(
    [switch]$Clean,
    [switch]$Rebuild
)

$ErrorActionPreference = "Stop"

# Paths
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$resDir = Join-Path $scriptDir "res"
$distDir = Join-Path $scriptDir "dist"
$commonSource = Join-Path $scriptDir "VSCodePortableCommon.cs"

# Launcher sources
$launcherProgramSource = Join-Path $scriptDir "LauncherProgram.cs"
$launcherFormSource = Join-Path $scriptDir "launcher.cs"
$upgradeFormDesignerSource = Join-Path $scriptDir "UpgradeForm.Designer.cs"
$launcherAssemblyInfoSource = Join-Path $scriptDir "Properties\AssemblyInfo.Launcher.cs"

# Installer sources
$installerProgramSource = Join-Path $scriptDir "Program.cs"
$installerFormSource = Join-Path $scriptDir "installer.cs"
$installFormDesignerSource = Join-Path $scriptDir "InstallForm.Designer.cs"
$installerAssemblyInfoSource = Join-Path $scriptDir "Properties\AssemblyInfo.Installer.cs"

# Find csc.exe
$cscPaths = @(
    "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
    "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe",
    "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\Roslyn\csc.exe",
    "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\Roslyn\csc.exe"
)

$csc = $null
foreach ($path in $cscPaths) {
    if (Test-Path $path) {
        $csc = $path
        break
    }
}

if (-not $csc) {
    Write-Host "Error: C# compiler (csc.exe) not found!" -ForegroundColor Red
    Write-Host "Please ensure .NET Framework 4.x is installed." -ForegroundColor Yellow
    exit 1
}

Write-Host "Found C# compiler: $csc" -ForegroundColor Green

# Clean if requested
if ($Clean -or $Rebuild) {
    Write-Host "`nCleaning old build artifacts..." -ForegroundColor Cyan
    
    # Clean dist directory
    if (Test-Path $distDir) {
        Remove-Item $distDir -Recurse -Force
        Write-Host "  Removed: $distDir" -ForegroundColor Gray
    }
    
    # Clean intermediate files
    $toClean = @(
        (Join-Path $scriptDir "launcher.exe"),
        (Join-Path $scriptDir "CodeLauncher.exe"),
        (Join-Path $scriptDir "devenv-setup.exe")
    )
    foreach ($file in $toClean) {
        if (Test-Path $file) {
            Remove-Item $file -Force
            Write-Host "  Removed: $file" -ForegroundColor Gray
        }
    }
    
    # If only Clean (not Rebuild), exit after cleaning
    if ($Clean -and -not $Rebuild) {
        Write-Host "`nClean completed!" -ForegroundColor Green
        exit 0
    }
}

# Create dist directory
if (-not (Test-Path $distDir)) {
    New-Item -ItemType Directory -Path $distDir | Out-Null
    Write-Host "`nCreated dist directory: $distDir" -ForegroundColor Green
}

#region Step 1: Build launcher.exe
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Step 1: Building launcher.exe" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$launcherOutput = Join-Path $distDir "launcher.exe"
$launcherResx = Join-Path $scriptDir "launcher.resx"
$iconFile = Join-Path $resDir "installer.ico"
$pngFile = Join-Path $scriptDir "launcher.png"
$codeExe = Join-Path $scriptDir "Code.exe"

# Icon priority: launcher.png > Code.exe extraction > installer.ico (existing)
if (-not (Test-Path $iconFile) -and (Test-Path $pngFile)) {
    Write-Host "`nConverting launcher.png to installer.ico..." -ForegroundColor Cyan
    
    try {
        Add-Type -AssemblyName System.Drawing
        
        $pngImage = [System.Drawing.Image]::FromFile((Resolve-Path $pngFile).Path)
        $size = [Math]::Max($pngImage.Width, $pngImage.Height)
        if ($size -gt 256) { $size = 256 }
        
        $bitmap = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.DrawImage($pngImage, 0, 0, $size, $size)
        $graphics.Dispose()
        
        $memoryStream = New-Object System.IO.MemoryStream
        $bitmap.Save($memoryStream, [System.Drawing.Imaging.ImageFormat]::Png)
        
        $icoStream = [System.IO.File]::Create($iconFile)
        $writer = New-Object System.IO.BinaryWriter($icoStream)
        
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]1)
        
        if ($size -eq 256) {
            $writer.Write([byte]0)
            $writer.Write([byte]0)
        } else {
            $writer.Write([byte]$size)
            $writer.Write([byte]$size)
        }
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$memoryStream.Length)
        $writer.Write([uint32]22)
        
        $memoryStream.Position = 0
        $memoryStream.CopyTo($icoStream)
        
        $writer.Close()
        $icoStream.Close()
        $memoryStream.Close()
        $bitmap.Dispose()
        $pngImage.Dispose()
        
        Write-Host "  Icon converted: $iconFile" -ForegroundColor Green
    } catch {
        Write-Host "  Warning: PNG to ICO conversion failed: $_" -ForegroundColor Yellow
        if (Test-Path $iconFile) { Remove-Item $iconFile -Force }
    }
}

# Fallback: Extract icon from Code.exe
if (-not (Test-Path $iconFile) -and (Test-Path $codeExe)) {
    Write-Host "`nExtracting icon from Code.exe..." -ForegroundColor Cyan
    try {
        $icon = [System.Drawing.Icon]::ExtractAssociatedIcon((Resolve-Path $codeExe).Path)
        $fileStream = [System.IO.File]::Create($iconFile)
        $icon.Save($fileStream)
        $fileStream.Close()
        Write-Host "  Icon extracted: $iconFile" -ForegroundColor Green
    } catch {
        Write-Host "  Warning: Icon extraction failed." -ForegroundColor Yellow
    }
}

# Verify launcher source files
if (-not (Test-Path $launcherProgramSource)) {
    Write-Host "Error: LauncherProgram.cs not found!" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $launcherFormSource)) {
    Write-Host "Error: launcher.cs not found!" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $upgradeFormDesignerSource)) {
    Write-Host "Error: UpgradeForm.Designer.cs not found!" -ForegroundColor Red
    exit 1
}

Write-Host "`nBuilding launcher.exe..." -ForegroundColor Cyan
Write-Host "  Sources:" -ForegroundColor Gray
Write-Host "    - LauncherProgram.cs" -ForegroundColor Gray
Write-Host "    - launcher.cs" -ForegroundColor Gray
Write-Host "    - UpgradeForm.Designer.cs" -ForegroundColor Gray
Write-Host "    - VSCodePortableCommon.cs" -ForegroundColor Gray
Write-Host "  Output: $launcherOutput" -ForegroundColor Gray

$launcherArgs = @(
    "/out:$launcherOutput",
    "/target:winexe",
    "/platform:x64",
    "/optimize+",
    "/reference:System.Windows.Forms.dll",
    "/reference:System.Drawing.dll",
    "/reference:System.Net.Http.dll",
    "/reference:System.IO.Compression.dll",
    "/reference:System.IO.Compression.FileSystem.dll",
    "/nowarn:1701,1702",
    $commonSource,
    $launcherProgramSource,
    $launcherFormSource,
    $upgradeFormDesignerSource,
    $launcherAssemblyInfoSource
)

if (Test-Path $iconFile) {
    $launcherArgs += "/win32icon:$iconFile"
    Write-Host "  Using icon: $iconFile" -ForegroundColor Gray
}

# Add launcher.resx if it exists
if (Test-Path $launcherResx) {
    $launcherArgs += "/resource:$launcherResx,VSCodePortableLauncher.launcher.resources"
    Write-Host "  Using resources: launcher.resx" -ForegroundColor Gray
}

try {
    & $csc $launcherArgs 2>&1 | ForEach-Object {
        if ($_ -match "error") {
            Write-Host $_ -ForegroundColor Red
        } elseif ($_ -match "warning") {
            Write-Host $_ -ForegroundColor Yellow
        } else {
            Write-Host $_ -ForegroundColor Gray
        }
    }

    if ($LASTEXITCODE -ne 0) {
        Write-Host "`nLauncher build failed with exit code: $LASTEXITCODE" -ForegroundColor Red
        exit $LASTEXITCODE
    }

    if (Test-Path $launcherOutput) {
        $fileInfo = Get-Item $launcherOutput
        Write-Host "`n✓ launcher.exe build succeeded!" -ForegroundColor Green
        Write-Host "  Size: $([math]::Round($fileInfo.Length / 1KB, 1)) KB" -ForegroundColor Gray
    } else {
        Write-Host "`nLauncher build failed: output file not found!" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "`nLauncher build error: $_" -ForegroundColor Red
    exit 1
}
#endregion

#region Step 2: Build CodeLauncher.exe
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Step 2: Building CodeLauncher.exe" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$installerOutput = Join-Path $distDir "CodeLauncher.exe"
$installerResx = Join-Path $scriptDir "installer.resx"
$profileTemplate = Join-Path $resDir "Microsoft.PowerShell_profile.ps1"
$settingsTemplate = Join-Path $resDir "settings.json"

# Verify installer source files
if (-not (Test-Path $installerProgramSource)) {
    Write-Host "Error: Program.cs not found!" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $installerFormSource)) {
    Write-Host "Error: installer.cs not found!" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $installFormDesignerSource)) {
    Write-Host "Error: InstallForm.Designer.cs not found!" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $profileTemplate)) {
    Write-Host "Error: Microsoft.PowerShell_profile.ps1 not found in res/!" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $settingsTemplate)) {
    Write-Host "Error: settings.json not found in res/!" -ForegroundColor Red
    exit 1
}

Write-Host "`nBuilding installer.exe..." -ForegroundColor Cyan
Write-Host "  Sources:" -ForegroundColor Gray
Write-Host "    - Program.cs" -ForegroundColor Gray
Write-Host "    - installer.cs" -ForegroundColor Gray
Write-Host "    - InstallForm.Designer.cs" -ForegroundColor Gray
Write-Host "    - VSCodePortableCommon.cs" -ForegroundColor Gray
Write-Host "  Output: $installerOutput" -ForegroundColor Gray

$installerArgs = @(
    "/out:$installerOutput",
    "/target:winexe",
    "/platform:x64",
    "/optimize+",
    "/reference:System.Windows.Forms.dll",
    "/reference:System.Drawing.dll",
    "/reference:System.Net.Http.dll",
    "/reference:System.IO.Compression.dll",
    "/reference:System.IO.Compression.FileSystem.dll",
    "/nowarn:1701,1702",
    $commonSource,
    $installerProgramSource,
    $installerFormSource,
    $installFormDesignerSource,
    $installerAssemblyInfoSource
)

# Add icon
if (Test-Path $iconFile) {
    $installerArgs += "/win32icon:$iconFile"
    Write-Host "  Using icon: $iconFile" -ForegroundColor Gray
}

# Add installer.resx if it exists
if (Test-Path $installerResx) {
    $installerArgs += "/resource:$installerResx,VSCodePortableInstaller.installer.resources"
    Write-Host "  Using resources: installer.resx" -ForegroundColor Gray
}

# Add embedded resources
# NOTE: res/*.sh files (devenv-setup.sh, launcher.sh) are macOS-only scripts
# maintained for GitHub. They must NOT be embedded as resources.
$installerArgs += "/resource:$profileTemplate,Microsoft.PowerShell_profile.ps1"
$installerArgs += "/resource:$settingsTemplate,settings.json"

# Add font files
$nerdFont = Join-Path $resDir "0xProtoNerdFont-Regular.ttf"
$dalseoFont = Join-Path $resDir "DalseoHealingMedium.ttf"
if ((Test-Path $nerdFont) -and (Test-Path $dalseoFont)) {
    $installerArgs += "/resource:$nerdFont,0xProtoNerdFont-Regular.ttf"
    $installerArgs += "/resource:$dalseoFont,DalseoHealingMedium.ttf"
}

# Add launcher.exe and theme
$tosTermTheme = Join-Path $resDir "tos-term.omp.json"
if (Test-Path $launcherOutput) {
    $installerArgs += "/resource:$launcherOutput,launcher.exe"
    if (Test-Path $tosTermTheme) {
        $installerArgs += "/resource:$tosTermTheme,tos-term.omp.json"
        Write-Host "  Embedded resources:" -ForegroundColor Gray
        Write-Host "    - Microsoft.PowerShell_profile.ps1" -ForegroundColor Gray
        Write-Host "    - settings.json" -ForegroundColor Gray
        Write-Host "    - 0xProtoNerdFont-Regular.ttf" -ForegroundColor Gray
        Write-Host "    - DalseoHealingMedium.ttf" -ForegroundColor Gray
        Write-Host "    - launcher.exe" -ForegroundColor Gray
        Write-Host "    - tos-term.omp.json" -ForegroundColor Gray
    } else {
        Write-Host "  Embedded resources:" -ForegroundColor Gray
        Write-Host "    - Microsoft.PowerShell_profile.ps1" -ForegroundColor Gray
        Write-Host "    - settings.json" -ForegroundColor Gray
        Write-Host "    - 0xProtoNerdFont-Regular.ttf" -ForegroundColor Gray
        Write-Host "    - DalseoHealingMedium.ttf" -ForegroundColor Gray
        Write-Host "    - launcher.exe" -ForegroundColor Gray
        Write-Host "  Warning: tos-term.omp.json not found" -ForegroundColor Yellow
    }
} else {
    Write-Host "  Warning: launcher.exe not found, will not be embedded" -ForegroundColor Yellow
}

try {
    & $csc $installerArgs 2>&1 | ForEach-Object {
        if ($_ -match "error") {
            Write-Host $_ -ForegroundColor Red
        } elseif ($_ -match "warning") {
            Write-Host $_ -ForegroundColor Yellow
        } else {
            Write-Host $_ -ForegroundColor Gray
        }
    }

    if ($LASTEXITCODE -ne 0) {
        Write-Host "`nInstaller build failed with exit code: $LASTEXITCODE" -ForegroundColor Red
        exit $LASTEXITCODE
    }

    if (Test-Path $installerOutput) {
        $fileInfo = Get-Item $installerOutput
        $sizeKB = [math]::Round($fileInfo.Length / 1KB, 1)
        
        Write-Host "`n✓ CodeLauncher.exe build succeeded!" -ForegroundColor Green
        Write-Host "  Size: $sizeKB KB" -ForegroundColor Gray
        Write-Host "  Modified: $($fileInfo.LastWriteTime)" -ForegroundColor Gray
    } else {
        Write-Host "`nInstaller build failed: output file not found!" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "`nInstaller build error: $_" -ForegroundColor Red
    exit 1
}

# Remove intermediate launcher.exe (already embedded in CodeLauncher.exe)
if (Test-Path $launcherOutput) {
    $launcherSize = [math]::Round((Get-Item $launcherOutput).Length / 1KB, 1)
    Remove-Item $launcherOutput -Force
    Write-Host "`nRemoved intermediate file: launcher.exe ($launcherSize KB)" -ForegroundColor Gray
}

# Remove old installer.exe if exists (renamed to CodeLauncher.exe)
$oldInstallerPath = Join-Path $distDir "installer.exe"
if (Test-Path $oldInstallerPath) {
    try {
        Remove-Item $oldInstallerPath -Force -ErrorAction Stop
        Write-Host "Removed old file: installer.exe" -ForegroundColor Gray
    } catch {
        Write-Host "Warning: Could not remove installer.exe (file may be in use)" -ForegroundColor Yellow
        Write-Host "Please close any running instances and delete manually" -ForegroundColor Yellow
    }
}

# Remove the previous distribution filename.
$legacyInstallerOutput = Join-Path $distDir "devenv-setup.exe"
if (Test-Path $legacyInstallerOutput) {
    Remove-Item $legacyInstallerOutput -Force
    Write-Host "Removed old distribution file: devenv-setup.exe" -ForegroundColor Gray
}
#endregion

#region Final Summary
Write-Host "`n========================================" -ForegroundColor Green
Write-Host "Build Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green

if (Test-Path $installerOutput) {
    $installerSize = [math]::Round((Get-Item $installerOutput).Length / 1KB, 1)
    Write-Host "  ✓ CodeLauncher.exe: $installerSize KB (final distribution)" -ForegroundColor Gray
}

Write-Host "`nDeployment:" -ForegroundColor Cyan
Write-Host "  Distribute: CodeLauncher.exe only" -ForegroundColor White
Write-Host "  All resources embedded (fonts, profiles, launcher, theme)" -ForegroundColor Gray
#endregion
