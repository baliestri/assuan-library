[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [string] $Repository,
  [string] $Solution = 'AssuanLibrary.slnx'
)

# The ReSharper command-line tools are the repository's formatter and inspector. This check runs the
# same two commands a contributor runs, then requires that the cleanup changed nothing and that the
# inspection reported nothing. The tool version is pinned in .config/dotnet-tools.json.
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($Repository)
$solutionPath = Join-Path $root $Solution
if (-not (Test-Path -LiteralPath $solutionPath -PathType Leaf)) { throw 'The solution is missing.' }

function Get-TreeState {
  $status = & git -C $root status --porcelain=v1 --untracked-files=all
  if ($LASTEXITCODE -ne 0) { throw 'The working tree cannot be inspected.' }
  $diff = & git -C $root diff
  if ($LASTEXITCODE -ne 0) { throw 'The working tree cannot be compared.' }
  return (@($status) + @($diff)) -join "`n"
}

Push-Location $root
try {
  & dotnet tool restore
  if ($LASTEXITCODE -ne 0) { throw 'The pinned ReSharper tools could not be restored.' }

  $before = Get-TreeState
  & dotnet jb cleanupcode '--profile=Built-in: Full Cleanup' $solutionPath
  if ($LASTEXITCODE -ne 0) { throw 'Code cleanup failed.' }
  if ((Get-TreeState) -cne $before) {
    & git -C $root diff --stat
    throw 'Code cleanup changed files. Run jb cleanupcode locally and commit the result.'
  }

  $report = Join-Path $root 'artifacts/inspection/inspectcode.sarif'
  [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($report)) | Out-Null
  if (Test-Path -LiteralPath $report) { Remove-Item -LiteralPath $report -Force }
  & dotnet jb inspectcode $solutionPath --format=Sarif "--output=$report"
  if ($LASTEXITCODE -ne 0) { throw 'Code inspection failed.' }
  $sarif = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
  $findings = @($sarif.runs | ForEach-Object { $_.results })
  if ($findings.Count -ne 0) {
    foreach ($finding in $findings) {
      $location = $finding.locations[0].physicalLocation
      Write-Output "$($location.artifactLocation.uri):$($location.region.startLine) $($finding.ruleId) $($finding.message.text)"
    }
    throw "Code inspection reported $($findings.Count) finding(s)."
  }

  Write-Output 'Cleanup changed nothing and the inspection is empty.'
}
finally { Pop-Location }
