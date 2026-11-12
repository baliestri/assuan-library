[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)] [string] $Version,
  [Parameter(Mandatory = $true)] [string] $OutFile,
  [double] $TimeoutMinutes = 45,
  [double] $InitialDelaySeconds = 15,
  [double] $MaxDelaySeconds = 60,
  [string] $ProbeUri = ''
)

# Waits for nuget.org to expose an already uploaded version and downloads it once.
# Validation and indexing regularly take longer than the upload; a missing version is
# therefore "pending", never a reason to upload again.
#   exit 0: the package was downloaded to OutFile
#   exit 2: the deadline expired while the version was still invisible (safe to re-run)
#   exit 1: invalid arguments or an unexpected registry answer
$ErrorActionPreference = 'Stop'
try {
  if ($Version -cnotmatch '\A2\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\z') { throw 'Invalid stable 2.x.x version.' }
  if ($TimeoutMinutes -le 0 -or $InitialDelaySeconds -le 0 -or $MaxDelaySeconds -lt $InitialDelaySeconds) {
    throw 'Invalid registry wait budget.'
  }
  $uri = if ($ProbeUri) { $ProbeUri }
    else { "https://api.nuget.org/v3-flatcontainer/assuanlibrary/$Version/assuanlibrary.$Version.nupkg" }
  # Only the public flat container, or a loopback fixture in tests, may be probed.
  if ($uri -cnotmatch '\Ahttps://api\.nuget\.org/v3-flatcontainer/assuanlibrary/' -and
      $uri -cnotmatch '\Ahttp://127\.0\.0\.1:[0-9]{1,5}/') {
    throw 'Registry probe address is not allowed.'
  }

  $target = [IO.Path]::GetFullPath($OutFile)
  if (Test-Path -LiteralPath $target) { throw 'Registry download target already exists.' }
  [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
  $partial = "$target.partial"
  $deadline = [Diagnostics.Stopwatch]::StartNew()
  $budget = [TimeSpan]::FromMinutes($TimeoutMinutes)
  $delay = $InitialDelaySeconds
  $attempt = 0
  while ($true) {
    $attempt++
    $status = 0
    try {
      if (Test-Path -LiteralPath $partial) { Remove-Item -LiteralPath $partial -Force }
      $response = Invoke-WebRequest -Uri $uri -OutFile $partial -PassThru -SkipHttpErrorCheck -TimeoutSec 120
      $status = [int]$response.StatusCode
    }
    catch {
      # DNS, TLS, reset or timeout: the registry state is unknown, so keep waiting.
      $status = -1
    }

    if ($status -eq 200) {
      Move-Item -LiteralPath $partial -Destination $target
      Write-Output $target
      exit 0
    }

    if (Test-Path -LiteralPath $partial) { Remove-Item -LiteralPath $partial -Force }
    $transient = $status -eq -1 -or $status -eq 404 -or $status -eq 408 -or $status -eq 429 -or $status -ge 500
    if (-not $transient) { throw 'Unexpected registry answer.' }
    if ($deadline.Elapsed + [TimeSpan]::FromSeconds($delay) -ge $budget) {
      [Console]::Error.WriteLine(
        "NuGet did not expose the version after $attempt attempts. Re-run this job; never upload again.")
      exit 2
    }

    Start-Sleep -Milliseconds ([int]($delay * 1000))
    $delay = [Math]::Min($delay * 2, $MaxDelaySeconds)
  }
}
catch {
  [Console]::Error.WriteLine('Registry confirmation failed and requires inspection.')
  exit 1
}
