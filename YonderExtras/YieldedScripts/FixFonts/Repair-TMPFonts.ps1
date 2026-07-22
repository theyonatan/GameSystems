param(
    [switch]$Apply
)

$ErrorActionPreference = "Stop"

$RepoRoot = (& git -C $PSScriptRoot rev-parse --show-toplevel).Trim()

if (-not $RepoRoot) {
    throw "Could not find the Git repository."
}

Write-Host "Repository: $RepoRoot"
Write-Host ""

# Find tracked TMP font assets.
$AllFontAssets = @(
    & git -C $RepoRoot grep -l "m_AtlasPopulationMode:" -- "*.asset"
)

# Corrupted TMP assets contain regular URP material properties.
$AffectedFiles = @(
    foreach ($Path in $AllFontAssets) {
        $FullPath = Join-Path $RepoRoot $Path
        $Content = Get-Content -LiteralPath $FullPath -Raw

        $HasBaseMap = $Content -match "(?m)^\s+- _BaseMap:"
        $HasSurface = $Content -match "(?m)^\s+- _Surface:"
        $HasTinyAtlas = $Content -match "(?ms)Texture2D:.*?m_Width:\s+1\s+.*?m_Height:\s+1"

        if ($HasBaseMap -and $HasSurface) {
            $Path
        }
    }
)

if ($AffectedFiles.Count -eq 0) {
    Write-Host "No corrupted TMP font assets found." -ForegroundColor Green
    exit
}

Write-Host "Found $($AffectedFiles.Count) suspicious TMP font assets." -ForegroundColor Yellow
Write-Host ""

$RepairItems = @()
$FailedItems = @()

foreach ($Path in $AffectedFiles) {
    Write-Host "Checking: $Path"

    # Find commits where _BaseMap was introduced.
    $CandidateCommits = @(
        & git -C $RepoRoot log `
            --format="%H" `
            -S"_BaseMap:" `
            -- $Path
    )

    $CorruptionCommit = $null

    foreach ($Commit in $CandidateCommits) {
        $Diff = (
            & git -C $RepoRoot show `
                --format= `
                --unified=0 `
                $Commit `
                -- $Path
        ) -join "`n"

        if ($Diff -match "(?m)^\+\s+- _BaseMap:") {
            $CorruptionCommit = $Commit
            break
        }
    }

    if (-not $CorruptionCommit) {
        Write-Warning "Could not locate the corruption commit: $Path"
        $FailedItems += $Path
        continue
    }

    $GoodCommit = "$CorruptionCommit^"

    & git -C $RepoRoot cat-file -e "${GoodCommit}:$Path" 2>$null

    if ($LASTEXITCODE -ne 0) {
        Write-Warning "No earlier healthy version exists: $Path"
        $FailedItems += $Path
        continue
    }

    Write-Host "  Broken in:    $CorruptionCommit"
    Write-Host "  Restore from: $GoodCommit" -ForegroundColor Cyan

    $RepairItems += [PSCustomObject]@{
        Path             = $Path
        CorruptionCommit = $CorruptionCommit
        GoodCommit       = $GoodCommit
    }
}

Write-Host ""
Write-Host "Repairable: $($RepairItems.Count)"
Write-Host "Failed:     $($FailedItems.Count)"
Write-Host ""

if (-not $Apply) {
    Write-Host "DRY RUN ONLY - nothing was changed." -ForegroundColor Yellow

    foreach ($Item in $RepairItems) {
        Write-Host "  $($Item.Path)"
    }

    Write-Host ""
    Write-Host "Run again with -Apply if the list looks correct."
    exit
}

foreach ($Item in $RepairItems) {
    Write-Host "Restoring: $($Item.Path)" -ForegroundColor Cyan

    & git -C $RepoRoot restore `
        "--source=$($Item.GoodCommit)" `
        --staged `
        --worktree `
        -- `
        $Item.Path

    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Restore failed: $($Item.Path)"
    }
}

Write-Host ""
Write-Host "Finished. Restored assets are staged in Git." -ForegroundColor Green
Write-Host "Review with: git diff --cached --stat"