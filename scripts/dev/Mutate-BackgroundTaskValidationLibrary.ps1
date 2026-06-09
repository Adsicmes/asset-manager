param(
    [Parameter(Mandatory = $true)]
    [string]$LibraryRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$resolvedLibraryRoot = (Resolve-Path -LiteralPath $LibraryRoot).Path
$managementDirectory = Join-Path $resolvedLibraryRoot ".asset-manager"
$databasePath = Join-Path $managementDirectory "asset-manager.db"

if (-not (Test-Path -LiteralPath $managementDirectory)) {
    throw "The specified path is not a material library root: $resolvedLibraryRoot"
}

if (-not (Test-Path -LiteralPath $databasePath)) {
    throw "The specified library root does not contain .asset-manager\\asset-manager.db: $resolvedLibraryRoot"
}

$contentFiles = Get-ChildItem -Path $resolvedLibraryRoot -File -Recurse |
    Where-Object { $_.FullName -notlike "$managementDirectory*" }

if ($contentFiles.Count -lt 2) {
    throw "At least two content files are required to run the sync mutation scenario."
}

$renameCandidate = $contentFiles |
    Where-Object { $_.Extension -in ".txt", ".png", ".wav" } |
    Select-Object -First 1

if ($null -eq $renameCandidate) {
    $renameCandidate = $contentFiles | Select-Object -First 1
}

$baseName = [System.IO.Path]::GetFileNameWithoutExtension($renameCandidate.Name)
$extension = $renameCandidate.Extension
$renameTarget = Join-Path $renameCandidate.DirectoryName ($baseName + "-renamed" + $extension)
$renameIndex = 1
while (Test-Path -LiteralPath $renameTarget) {
    $renameTarget = Join-Path $renameCandidate.DirectoryName ($baseName + "-renamed-" + $renameIndex + $extension)
    $renameIndex++
}

Move-Item -LiteralPath $renameCandidate.FullName -Destination $renameTarget

$deleteCandidate = $contentFiles |
    Where-Object { $_.FullName -ne $renameCandidate.FullName } |
    Select-Object -First 1

Remove-Item -LiteralPath $deleteCandidate.FullName

$newFilePath = Join-Path $resolvedLibraryRoot ("sync-added-{0}.txt" -f (Get-Date -Format "yyyyMMdd-HHmmss"))
[System.IO.File]::WriteAllText(
    $newFilePath,
    "Added during background task center sync validation at $(Get-Date -Format "yyyy-MM-dd HH:mm:ss").")

Write-Output ("RenamedFile=" + $renameTarget)
Write-Output ("DeletedFile=" + $deleteCandidate.FullName)
Write-Output ("AddedFile=" + $newFilePath)
