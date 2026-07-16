# Sync source changes from the local App2 dev folder into this GitHub-layout repo.
# App2 is read-only; only files under this repo are modified.

param(
    [string]$SourceRoot = "$env:USERPROFILE\Documents\visual_studio\test_app_with_menu\App2",
    [string]$DestRoot = (Split-Path $PSScriptRoot -Parent)
)

$ErrorActionPreference = "Stop"

function Copy-TreeFiltered {
    param(
        [string]$Source,
        [string]$Destination,
        [string[]]$ExcludeNames = @()
    )

    if (-not (Test-Path $Source)) {
        Write-Warning "Skip missing source: $Source"
        return
    }

    New-Item -ItemType Directory -Force -Path $Destination | Out-Null

    Get-ChildItem -LiteralPath $Source -Force | ForEach-Object {
        if ($ExcludeNames -contains $_.Name) {
            return
        }

        $target = Join-Path $Destination $_.Name
        if ($_.PSIsContainer) {
            Copy-TreeFiltered -Source $_.FullName -Destination $target -ExcludeNames $ExcludeNames
        }
        else {
            Copy-Item -LiteralPath $_.FullName -Destination $target -Force
        }
    }
}

$exclude = @(
    "bin", "obj", ".vs", "__pycache__", "terminals", "agent-tools",
    "BundleArtifacts", "Package", "Generated Files"
)

Write-Host "Source: $SourceRoot"
Write-Host "Dest:   $DestRoot"

# backend (except api.py and twikit_client.py)
$backendFiles = @("action_queue.py", "tweet_serializer.py")
foreach ($file in $backendFiles) {
    $src = Join-Path $SourceRoot $file
    $dst = Join-Path $DestRoot "backend\$file"
    if (Test-Path $src) {
        Copy-Item -LiteralPath $src -Destination $dst -Force
        Write-Host "Copied backend/$file"
    }
}

# scripts
$scriptFiles = @(
    "get_lists_twikit.py",
    "get_my_profile.py",
    "get_notifications_twikit.py",
    "get_search_twikit.py",
    "get_timeline_twikit.py",
    "post_tweet.py"
)
foreach ($file in $scriptFiles) {
    $src = Join-Path $SourceRoot $file
    $dst = Join-Path $DestRoot "scripts\$file"
    if (Test-Path $src) {
        Copy-Item -LiteralPath $src -Destination $dst -Force
        Write-Host "Copied scripts/$file"
    }
}

# frontend App2 project
$frontendSrc = Join-Path $SourceRoot "App2\App2"
$frontendDst = Join-Path $DestRoot "frontend\App2"
$frontendSkip = @("ServerManager.cs", "RepositoryPaths.cs", "SettingsPage.xaml.cs")
if (Test-Path $frontendSrc) {
    Get-ChildItem -LiteralPath $frontendSrc -Force | ForEach-Object {
        if ($frontendSkip -contains $_.Name) {
            Write-Host "Skipped frontend/App2/$($_.Name)"
            return
        }
        if ($_.PSIsContainer) {
            if ($exclude -contains $_.Name) { return }
            Copy-TreeFiltered -Source $_.FullName -Destination (Join-Path $frontendDst $_.Name) -ExcludeNames $exclude
            Write-Host "Copied frontend/App2/$($_.Name)/"
        }
        else {
            Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $frontendDst $_.Name) -Force
            Write-Host "Copied frontend/App2/$($_.Name)"
        }
    }
}

# frontend package project (images/manifest only; skip build output)
$packageSrc = Join-Path $SourceRoot "App2\App2 (Package)"
$packageDst = Join-Path $DestRoot "frontend\App2 (Package)"
if (Test-Path $packageSrc) {
    Copy-TreeFiltered -Source $packageSrc -Destination $packageDst -ExcludeNames $exclude
    Write-Host "Copied frontend/App2 (Package)/"
}

# solution file
$slnxSrc = Join-Path $SourceRoot "App2.slnx"
$slnxDst = Join-Path $DestRoot "frontend\App2.slnx"
if (Test-Path $slnxSrc) {
    Copy-Item -LiteralPath $slnxSrc -Destination $slnxDst -Force
    Write-Host "Copied frontend/App2.slnx"
}

# post-copy fixes for GitHub layout
$actionQueuePath = Join-Path $DestRoot "backend\action_queue.py"
if (Test-Path $actionQueuePath) {
    $content = Get-Content -LiteralPath $actionQueuePath -Raw
    $content = $content -replace 'twikit_client\.login\(\)', 'await twikit_client.login()'
    Set-Content -LiteralPath $actionQueuePath -Value $content -NoNewline
    Write-Host "Patched backend/action_queue.py (await login)"
}

# Remove legacy single-file code-behind if the xaml.cs version exists
$legacyTimeline = Join-Path $DestRoot "frontend\App2\TimelinePage.cs"
$timelineXamlCs = Join-Path $DestRoot "frontend\App2\TimelinePage.xaml.cs"
if ((Test-Path $legacyTimeline) -and (Test-Path $timelineXamlCs)) {
    Remove-Item -LiteralPath $legacyTimeline -Force
    Write-Host "Removed frontend/App2/TimelinePage.cs (superseded by TimelinePage.xaml.cs)"
}

Write-Host "Sync complete."