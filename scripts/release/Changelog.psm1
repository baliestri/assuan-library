function New-ReleaseChangelog {
  param([string] $Root, [string] $Version, [string] $GitCliff, [string] $PreviousTag)
  $path = Join-Path $Root 'CHANGELOG.md'
  $before = [IO.File]::ReadAllText($path)
  $header = "# Assuan Library .NET Changelog`n`n"
  if (-not $before.StartsWith($header, [StringComparison]::Ordinal) -or
      $before.Contains("## [$Version]")) {
    throw 'The historical changelog is invalid or this version is already present.'
  }
  $generated = & $GitCliff --repository $Root --workdir $Root --config (Join-Path $Root 'cliff.toml') `
    --offline --tag "v$Version" --strip header "$PreviousTag..HEAD" 2>$null
  if ($LASTEXITCODE -ne 0) { throw 'Changelog generation failed.' }
  $section = ($generated -join "`n").Trim()
  if (-not $section.StartsWith("## [$Version]", [StringComparison]::Ordinal)) {
    throw 'Changelog version header is missing.'
  }
  $date = (& git -C $Root show -s '--format=%cI' HEAD).Trim()
  if ($LASTEXITCODE -ne 0) { throw 'Commit date is unavailable.' }
  $releaseDate = ([DateTimeOffset]::Parse($date, [Globalization.CultureInfo]::InvariantCulture)).UtcDateTime.ToString('yyyy-MM-dd')
  $section = [regex]::Replace($section, '(?m)\A(## \[' + [regex]::Escape($Version) + '\].* - )\d{4}-\d{2}-\d{2}',
    '${1}' + $releaseDate)
  $tail = $before.Substring($header.Length)
  return [pscustomobject]@{
    Section = $section
    Content = $header + $section + "`n`n" + $tail
  }
}

Export-ModuleMember -Function New-ReleaseChangelog
