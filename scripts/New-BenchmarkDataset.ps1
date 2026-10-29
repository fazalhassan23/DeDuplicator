param([string]$Destination = 'benchmark-data', [int]$Files = 10000, [int]$BytesPerFile = 65536, [int]$Seed = 7301)
$ErrorActionPreference = 'Stop'
if ($Files -lt 1 -or $BytesPerFile -lt 1) { throw 'Positive file count and size required.' }
$target = [IO.Path]::GetFullPath($Destination)
if (Test-Path -LiteralPath $target) { throw 'Choose a new directory; existing data is never overwritten.' }
New-Item -ItemType Directory -Path $target | Out-Null
$random = [Random]::new($Seed)
$buffer = [byte[]]::new($BytesPerFile)
for ($index = 0; $index -lt $Files; $index++) {
    # Pairs share contents but names differ. Every 10th file is unique.
    if ($index % 2 -eq 0 -or $index % 10 -eq 9) { $random.NextBytes($buffer) }
    $bucket = Join-Path $target ([string][int]([Math]::Floor($index / 1000)))
    [IO.Directory]::CreateDirectory($bucket) | Out-Null
    [IO.File]::WriteAllBytes((Join-Path $bucket ('item-{0:D8}.bin' -f $index)), $buffer)
}
Write-Output "Created $Files files under $target using seed $Seed."
