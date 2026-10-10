[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)] [string] $Root,
  [Parameter(Mandatory = $true)] [string] $Version,
  [Parameter(Mandatory = $true)] [string] $ResumeSha
)

$ErrorActionPreference = 'Stop'
try {
  if ($Version -cne '2.0.0' -or $ResumeSha -cnotmatch '\A[0-9a-f]{40}\z') {
    throw 'Invalid explicit release candidate.'
  }
  $rootPath = [IO.Path]::GetFullPath($Root)
  $head = & git -C $rootPath rev-parse HEAD 2>$null
  if ($LASTEXITCODE -ne 0 -or $head -cne $ResumeSha) {
    throw 'Checkout does not match the requested candidate.'
  }
  $dirty = & git -C $rootPath status --porcelain=v1 --untracked-files=all 2>$null
  if ($LASTEXITCODE -ne 0 -or $dirty) { throw 'Candidate checkout is dirty.' }

  $remote = & git -C $rootPath ls-remote origin refs/heads/main refs/heads/release/v2.0.0 refs/tags/v2.0.0 2>$null
  if ($LASTEXITCODE -ne 0) { throw 'Release refs cannot be inspected.' }
  $refs = @{}
  foreach ($line in $remote) {
    $parts = $line -split '\s+', 2
    if ($parts.Count -ne 2 -or $parts[0] -cnotmatch '\A[0-9a-f]{40}\z') {
      throw 'Release ref response is invalid.'
    }
    $refs[$parts[1]] = $parts[0]
  }
  if ($refs['refs/heads/release/v2.0.0'] -cne $ResumeSha -or
      -not $refs['refs/heads/main'] -or $refs.ContainsKey('refs/tags/v2.0.0')) {
    throw 'Release branch, main or tag changed before resumption.'
  }
  & git -C $rootPath merge-base --is-ancestor $refs['refs/heads/main'] $ResumeSha
  if ($LASTEXITCODE -ne 0) { throw 'Main is not an ancestor of the release candidate.' }

  [xml]$project = [IO.File]::ReadAllText((Join-Path $rootPath 'src/AssuanLibrary/AssuanLibrary.csproj'))
  $versions = @($project.SelectNodes('//*[local-name()="Version"]'))
  if ($versions.Count -ne 1 -or $versions[0].InnerText -cne $Version -or
      [IO.File]::ReadAllText((Join-Path $rootPath 'CHANGELOG.md')) -notmatch '(?m)^## \[2\.0\.0\]') {
    throw 'Release candidate has no prepared package version or changelog.'
  }
  Write-Output $ResumeSha
}
catch {
  [Console]::Error.WriteLine($_.Exception.Message)
  exit 1
}
