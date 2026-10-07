param(
    [string]$Tag,
    [string]$Title,
    [switch]$Rebuild
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$artifactPath = Join-Path $scriptDir "dist\devenv-setup.exe"

function Get-RepositorySlug {
    $remoteUrl = git config --get remote.origin.url
    if ([string]::IsNullOrWhiteSpace($remoteUrl)) {
        throw "remote.origin.url is not configured."
    }

    if ($remoteUrl -match 'github\.com[:/](?<owner>[^/]+)/(?<repo>[^/.]+?)(?:\.git)?$') {
        return "$($Matches.owner)/$($Matches.repo)"
    }

    throw "Could not parse GitHub repository from remote URL: $remoteUrl"
}

function Get-LatestReleaseTag([string]$repoSlug) {
    $output = gh api "repos/$repoSlug/releases/latest" --jq .tag_name 2>$null
    if ($LASTEXITCODE -ne 0) {
        return ""
    }

    return ($output | Out-String).Trim()
}

Push-Location $scriptDir
try {
    $null = Get-Command gh -ErrorAction Stop
    gh auth status | Out-Null

    if ($Rebuild -or -not (Test-Path $artifactPath)) {
        Write-Host "Building latest installer artifact..." -ForegroundColor Cyan
        ./build
    }

    if (-not (Test-Path $artifactPath)) {
        throw "Artifact not found: $artifactPath"
    }

    $repoSlug = Get-RepositorySlug

    if ([string]::IsNullOrWhiteSpace($Tag)) {
        $Tag = Get-LatestReleaseTag $repoSlug
        if ([string]::IsNullOrWhiteSpace($Tag)) {
            throw "No existing GitHub release found. Run with -Tag <tag> to create the first release."
        }
    }

    if ([string]::IsNullOrWhiteSpace($Title)) {
        $Title = $Tag
    }

    gh release view $Tag --repo $repoSlug *> $null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Uploading artifact to existing release $Tag..." -ForegroundColor Cyan
        gh release upload $Tag $artifactPath --clobber --repo $repoSlug
    }
    else {
        Write-Host "Creating release $Tag and uploading artifact..." -ForegroundColor Cyan
        gh release create $Tag $artifactPath --title $Title --notes "Release asset for devenv-setup.exe" --repo $repoSlug
    }

    Write-Host "Release asset upload completed." -ForegroundColor Green
}
finally {
    Pop-Location
}