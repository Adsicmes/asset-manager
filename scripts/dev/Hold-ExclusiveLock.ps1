param(
    [Parameter(Mandatory = $true)]
    [string]$Path,
    [int]$Seconds = 60
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$resolvedPath = (Resolve-Path -LiteralPath $Path).Path
$access = [System.IO.FileAccess]::ReadWrite
$share = [System.IO.FileShare]::None
$mode = [System.IO.FileMode]::Open

Write-Output ("LockingPath=" + $resolvedPath)
Write-Output ("DurationSeconds=" + $Seconds)

$stream = [System.IO.File]::Open($resolvedPath, $mode, $access, $share)
try {
    Start-Sleep -Seconds $Seconds
}
finally {
    $stream.Dispose()
    Write-Output ("ReleasedPath=" + $resolvedPath)
}
