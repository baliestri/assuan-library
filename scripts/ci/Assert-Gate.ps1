[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [string] $Sha,
  [Parameter(Mandatory = $true)]
  [string] $RequiredJobsJson,
  [Parameter(Mandatory = $true)]
  [string] $ResultsJson,
  [string] $OutputFile
)

if ($Sha -cnotmatch '\A[0-9a-f]{40}\z') {
  [Console]::Error.WriteLine('Invalid source SHA.')
  exit 1
}

try {
  $required = ConvertFrom-Json -InputObject $RequiredJobsJson -NoEnumerate
  $results = ConvertFrom-Json -InputObject $ResultsJson -AsHashtable -NoEnumerate
}
catch {
  [Console]::Error.WriteLine('Invalid gate results.')
  exit 1
}

if ($required -isnot [array] -or $required.Count -eq 0 -or
    $results -isnot [System.Collections.IDictionary]) {
  [Console]::Error.WriteLine('Invalid required jobs or gate results.')
  exit 1
}

$seen = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($job in $required) {
  if ($job -isnot [string] -or [string]::IsNullOrWhiteSpace($job) -or
      -not $seen.Add($job) -or -not $results.Contains($job) -or
      $results[$job] -cne 'success') {
    [Console]::Error.WriteLine('A required gate job did not succeed.')
    exit 1
  }
}

if ($OutputFile) {
  try {
    Add-Content -LiteralPath $OutputFile -Value "source_sha=$Sha" -Encoding utf8 -ErrorAction Stop
  }
  catch {
    [Console]::Error.WriteLine('Gate output could not be written.')
    exit 1
  }
}

Write-Output $Sha
