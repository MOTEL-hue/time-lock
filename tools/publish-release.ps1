param([Parameter(Mandatory)][string]$Version)
$ErrorActionPreference = 'Stop'
$zip = 'out/TimeLock-win-x64.zip'
$sha = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLower()
$tag = "v$Version"
$url = "https://github.com/$env:GITHUB_REPOSITORY/releases/download/$tag/TimeLock-win-x64.zip"
$json = @{ version = $Version; sha256 = $sha; url = $url } | ConvertTo-Json -Compress
[IO.File]::WriteAllText('out/update.json', $json)
gh release create $tag out/TimeLock-win-x64.zip out/update.json out/TimeLock.exe out/TimeLock.Service.exe --title $tag --notes "גרסה $Version" --target $env:GITHUB_SHA
