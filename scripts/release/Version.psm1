function Invoke-ReleaseGit {
  param([string] $Root, [string[]] $Arguments)
  $output = & git -C $Root @Arguments 2>$null
  if ($LASTEXITCODE -ne 0) { throw 'Release Git preflight failed.' }
  return ($output -join "`n").Trim()
}

function Assert-ReleaseVersion {
  param([string] $Version)
  if ($Version -cnotmatch '\A(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\z' -or
      $Version -cne '2.0.0') { throw 'Expected the first stable release version 2.0.0.' }
}

function Assert-ReleaseSource {
  param([string] $Root, [string] $Version, [string] $ExpectedSha)
  Assert-ReleaseVersion $Version
  if ((Invoke-ReleaseGit $Root @('symbolic-ref', '--short', 'HEAD')) -cne 'develop') {
    throw 'Release preparation requires develop.'
  }
  if (Invoke-ReleaseGit $Root @('status', '--porcelain=v1', '--untracked-files=all')) {
    throw 'Release checkout is dirty.'
  }
  $head = Invoke-ReleaseGit $Root @('rev-parse', 'HEAD')
  if ($head -cnotmatch '\A[0-9a-f]{40}\z' -or
      ($ExpectedSha -and ($ExpectedSha -cnotmatch '\A[0-9a-f]{40}\z' -or $ExpectedSha -cne $head))) {
    throw 'Release source SHA changed or is invalid.'
  }
  foreach ($ref in @("refs/heads/release/v$Version", "refs/tags/v$Version")) {
    & git -C $Root show-ref --verify --quiet $ref
    if ($LASTEXITCODE -eq 0) { throw 'Release ref already exists.' }
    if ($LASTEXITCODE -ne 1) { throw 'Release ref inspection failed.' }
  }

  $remote = & git -C $Root ls-remote origin 'refs/heads/main' 'refs/heads/develop' `
    "refs/heads/release/v$Version" "refs/tags/v$Version" 2>$null
  if ($LASTEXITCODE -ne 0) { throw 'Release remote refs are unavailable.' }
  $refs = @{}
  foreach ($line in $remote) {
    $parts = $line -split '\s+', 2
    if ($parts.Count -ne 2 -or $parts[0] -cnotmatch '\A[0-9a-f]{40}\z') {
      throw 'Invalid remote ref response.'
    }
    $refs[$parts[1]] = $parts[0]
  }
  if ($refs.ContainsKey("refs/heads/release/v$Version") -or $refs.ContainsKey("refs/tags/v$Version")) {
    throw 'Release ref already exists remotely.'
  }
  $main = $refs['refs/heads/main']
  if (-not $main -or $refs['refs/heads/develop'] -cne $head -or
      (Invoke-ReleaseGit $Root @('rev-parse', 'refs/remotes/origin/main')) -cne $main) {
    throw 'Remote main or develop is not the inspected candidate.'
  }
  & git -C $Root merge-base --is-ancestor $main $head
  if ($LASTEXITCODE -ne 0) { throw 'Main is not an ancestor of develop.' }
  if (-not (Invoke-ReleaseGit $Root @('rev-parse', '--verify', 'refs/tags/v1.2.0'))) {
    throw 'Historical release baseline is missing.'
  }
  return $head
}

function Get-ReleaseVersionEdits {
  param([string] $Root, [string] $Version)
  $changes = [ordered]@{}
  $project = 'src/AssuanLibrary/AssuanLibrary.csproj'
  $text = [IO.File]::ReadAllText((Join-Path $Root $project))
  $match = [regex]::Matches($text, '<Version>[^<]+</Version>')
  if ($match.Count -gt 1) { throw 'Package version is ambiguous.' }
  if ($match.Count -eq 1) {
    $updated = [regex]::Replace($text, '<Version>[^<]+</Version>', "<Version>$Version</Version>")
  }
  else {
    if (-not $text.Contains('<TargetFramework>net10.0</TargetFramework>')) {
      throw 'The main package target is unexpected.'
    }
    $newline = if ($text.Contains("`r`n")) { "`r`n" } else { "`n" }
    $updated = $text.Replace('<TargetFramework>net10.0</TargetFramework>',
      "<TargetFramework>net10.0</TargetFramework>$newline    <Version>$Version</Version>")
  }
  if ($updated -cne $text) { $changes[$project] = $updated }

  $readme = 'README.adoc'
  $text = [IO.File]::ReadAllText((Join-Path $Root $readme))
  $updated = $text.Replace('Until 2.0.0 is published, reference', 'For development from a source checkout, reference')
  $updated = $updated.Replace('When released, `dotnet add package AssuanLibrary --version 2.0.0` will install',
    '`dotnet add package AssuanLibrary --version 2.0.0` installs')
  $updated = $updated.Replace('; it is not published yet.', '.')
  if ($updated -cne $text) { $changes[$readme] = $updated }

  $guide = 'docs/guides/getting-started.md'
  $text = [IO.File]::ReadAllText((Join-Path $Root $guide))
  $updated = $text.Replace('Until 2.0 is published, a local project can reference `src/AssuanLibrary/AssuanLibrary.csproj`; a packaged consumer will use',
    'Install the released package with')
  if ($updated -cne $text) { $changes[$guide] = $updated }

  $packageReadme = [IO.File]::ReadAllText((Join-Path $Root 'docs/nuget/README.md'))
  if (-not $packageReadme.Contains("dotnet add package AssuanLibrary --version $Version")) {
    throw 'The NuGet README does not describe the selected version.'
  }
  return $changes
}

Export-ModuleMember -Function Assert-ReleaseVersion, Assert-ReleaseSource, Get-ReleaseVersionEdits
