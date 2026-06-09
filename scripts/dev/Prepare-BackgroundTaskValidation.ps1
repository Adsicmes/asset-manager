param(
    [string]$OutputRoot = (Join-Path $PSScriptRoot "..\..\artifacts\manual-validation\background-task-center"),
    [int]$ImageCount = 48,
    [int]$TextFileCount = 12,
    [int]$LargeTextFileSizeMb = 8
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function New-Directory {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path | Out-Null
    }

    return (Resolve-Path -LiteralPath $Path).Path
}

function Write-PngFile {
    param(
        [string]$Path,
        [int]$Index
    )

    $pngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9pY2n4QAAAAASUVORK5CYII="
    [System.IO.File]::WriteAllBytes($Path, [Convert]::FromBase64String($pngBase64))
    [System.IO.File]::SetLastWriteTimeUtc($Path, [DateTime]::UtcNow.AddMinutes(-$Index))
}

function Write-WavFile {
    param([string]$Path)

    $sampleRate = 22050
    $durationSeconds = 1
    $sampleCount = $sampleRate * $durationSeconds
    $bytesPerSample = 2
    $dataSize = $sampleCount * $bytesPerSample

    $stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
    try {
        $writer = New-Object System.IO.BinaryWriter($stream)

        $writer.Write([Text.Encoding]::ASCII.GetBytes("RIFF"))
        $writer.Write([int](36 + $dataSize))
        $writer.Write([Text.Encoding]::ASCII.GetBytes("WAVE"))
        $writer.Write([Text.Encoding]::ASCII.GetBytes("fmt "))
        $writer.Write([int]16)
        $writer.Write([int16]1)
        $writer.Write([int16]1)
        $writer.Write([int]$sampleRate)
        $writer.Write([int]($sampleRate * $bytesPerSample))
        $writer.Write([int16]$bytesPerSample)
        $writer.Write([int16]16)
        $writer.Write([Text.Encoding]::ASCII.GetBytes("data"))
        $writer.Write([int]$dataSize)

        for ($i = 0; $i -lt $sampleCount; $i++) {
            $phase = 2.0 * [Math]::PI * 440.0 * $i / $sampleRate
            $sample = [int16]([Math]::Sin($phase) * 12000)
            $writer.Write($sample)
        }

        $writer.Flush()
    }
    finally {
        $stream.Dispose()
    }
}

function Write-LargeTextFile {
    param(
        [string]$Path,
        [int]$SizeMb,
        [int]$Index
    )

    $targetBytes = $SizeMb * 1024 * 1024
    $bufferLine = ("task-center-validation-file-{0:D2} " -f $Index) + ("0123456789abcdef" * 16)
    $encoding = New-Object System.Text.UTF8Encoding($false)

    $stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
    try {
        $writer = New-Object System.IO.StreamWriter($stream, $encoding)
        while ($stream.Length -lt $targetBytes) {
            $writer.WriteLine($bufferLine)
        }

        $writer.Flush()
    }
    finally {
        $stream.Dispose()
    }
}

$resolvedOutputRoot = New-Directory -Path $OutputRoot
$sessionName = Get-Date -Format "yyyyMMdd-HHmmss"
$sessionRoot = New-Directory -Path (Join-Path $resolvedOutputRoot $sessionName)
$libraryRoot = New-Directory -Path (Join-Path $sessionRoot "library-root")
$importSourceRoot = New-Directory -Path (Join-Path $sessionRoot "import-source")
$imagesRoot = New-Directory -Path (Join-Path $importSourceRoot "images")
$nestedImagesRoot = New-Directory -Path (Join-Path $imagesRoot "nested")
$textRoot = New-Directory -Path (Join-Path $importSourceRoot "text")
$audioRoot = New-Directory -Path (Join-Path $importSourceRoot "audio")
$notesRoot = New-Directory -Path (Join-Path $sessionRoot "notes")

for ($index = 1; $index -le $ImageCount; $index++) {
    $targetDirectory = if ($index % 2 -eq 0) { $imagesRoot } else { $nestedImagesRoot }
    $fileName = "image-{0:D3}.png" -f $index
    Write-PngFile -Path (Join-Path $targetDirectory $fileName) -Index $index
}

for ($index = 1; $index -le $TextFileCount; $index++) {
    $fileName = "large-note-{0:D2}.txt" -f $index
    Write-LargeTextFile -Path (Join-Path $textRoot $fileName) -SizeMb $LargeTextFileSizeMb -Index $index
}

Write-WavFile -Path (Join-Path $audioRoot "tone-01.wav")
[System.IO.File]::WriteAllText(
    (Join-Path $notesRoot "readme.txt"),
    @"
Background task center validation workspace

Library root:
$libraryRoot

Import source:
$importSourceRoot

Suggested source file to lock for import failure:
$(Join-Path $textRoot "large-note-01.txt")
"@)

Write-Output ("LibraryRoot=" + $libraryRoot)
Write-Output ("ImportSource=" + $importSourceRoot)
Write-Output ("SuggestedImportFailurePath=" + (Join-Path $textRoot "large-note-01.txt"))
Write-Output ("NotesPath=" + (Join-Path $notesRoot "readme.txt"))
