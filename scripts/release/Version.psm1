function Invoke-ReleaseGit {
  param([string] $Root, [string[]] $Arguments)
  $output = & git -C $Root @Arguments 2>$null
  if ($LASTEXITCODE -ne 0) { throw 'Release Git preflight failed.' }
  return ($output -join "`n").Trim()
}

function Assert-ReleaseVersion {
  param([string] $Version)
  if ($Version -cnotmatch '\A2\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\z') {
    throw 'Expected a stable 2.x.x release version.'
  }
  try { [void][version]::Parse($Version) }
  catch { throw 'Release version components exceed the supported range.' }
}

function Get-PreviousReleaseTag {
  param([string] $Root, [string] $MainSha)
  $tags = & git -C $Root tag --merged $MainSha --list 'v2.*' 2>$null
  if ($LASTEXITCODE -ne 0) { throw 'Published release tags cannot be inspected.' }
  $stable = @($tags | Where-Object { $_ -cmatch '\Av2\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\z' } |
    Sort-Object { [version]::Parse($_.Substring(1)) })
  if ($stable.Count -eq 0) { return 'v1.2.0' }
  return $stable[-1]
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
  $previous = Get-PreviousReleaseTag -Root $Root -MainSha $main
  $priorSha = Invoke-ReleaseGit $Root @('rev-parse', "refs/tags/$previous^{commit}")
  if (-not $priorSha) { throw 'Published release baseline is missing.' }
  $remoteTags = & git -C $Root ls-remote --tags origin 'refs/tags/v2.*' 2>$null
  if ($LASTEXITCODE -ne 0) { throw 'Remote release tags cannot be inspected.' }
  $remoteVersions = @{}
  foreach ($line in $remoteTags) {
    $parts = $line -split '\s+', 2
    if ($parts.Count -ne 2 -or $parts[0] -cnotmatch '\A[0-9a-f]{40}\z') {
      throw 'Invalid remote release tag response.'
    }
    if ($parts[1] -cmatch '\Arefs/tags/(v2\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*))(\^\{\})?\z') {
      $remoteVersions[$matches[1]] = $parts[0]
    }
  }
  $latestRemote = @($remoteVersions.Keys | Sort-Object { [version]::Parse($_.Substring(1)) } |
    Select-Object -Last 1)
  if ($previous -ne 'v1.2.0' -and
      ($latestRemote.Count -ne 1 -or $latestRemote[0] -cne $previous -or
       $remoteVersions[$previous] -cne $priorSha)) {
    throw 'Local and remote published release baselines differ.'
  }
  if ($previous -eq 'v1.2.0' -and $latestRemote.Count -ne 0) {
    throw 'A published 2.x release is missing from the local history.'
  }
  if ($previous -ne 'v1.2.0' -and [version]::Parse($Version) -le [version]::Parse($previous.Substring(1))) {
    throw 'Release version must be newer than the latest stable published tag.'
  }
  if ($previous -eq 'v1.2.0' -and $Version -cne '2.0.0') {
    throw 'The first stable 2.x release must be 2.0.0.'
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
    $sourceVersion = [regex]::Match($match[0].Value, '<Version>([^<]+)</Version>').Groups[1].Value
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
  if ($sourceVersion) {
    $updated = $updated.Replace("dotnet add package AssuanLibrary --version $sourceVersion",
      "dotnet add package AssuanLibrary --version $Version")
  }
  if ($updated -cne $text) { $changes[$readme] = $updated }

  $guide = 'docs/guides/getting-started.md'
  $text = [IO.File]::ReadAllText((Join-Path $Root $guide))
  $updated = $text.Replace('Until 2.0 is published, a local project can reference `src/AssuanLibrary/AssuanLibrary.csproj`; a packaged consumer will use',
    'Install the released package with')
  if ($sourceVersion) {
    $updated = $updated.Replace("dotnet add package AssuanLibrary --version $sourceVersion",
      "dotnet add package AssuanLibrary --version $Version")
  }
  if ($updated -cne $text) { $changes[$guide] = $updated }

  $packageReadme = [IO.File]::ReadAllText((Join-Path $Root 'docs/nuget/README.md'))
  $install = 'dotnet add package AssuanLibrary --version (?<version>[0-9]+\.[0-9]+\.[0-9]+)'
  $matches = [regex]::Matches($packageReadme, $install)
  if ($matches.Count -ne 1) { throw 'The NuGet README must have exactly one installation command.' }
  $priorVersion = $matches[0].Groups['version'].Value
  $updated = $packageReadme.Replace($matches[0].Value, "dotnet add package AssuanLibrary --version $Version")
  $updated = $updated.Replace("https://baliestri.github.io/assuan-library/$priorVersion/",
    "https://baliestri.github.io/assuan-library/$Version/")
  $updated = $updated.Replace("/tree/v$priorVersion/examples/", "/tree/v$Version/examples/")
  if ($updated -cne $packageReadme) { $changes['docs/nuget/README.md'] = $updated }
  return $changes
}

Export-ModuleMember -Function Assert-ReleaseVersion, Assert-ReleaseSource, Get-PreviousReleaseTag, Get-ReleaseVersionEdits
