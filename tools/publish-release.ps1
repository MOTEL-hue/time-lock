param([Parameter(Mandatory)][string]$Version)
$ErrorActionPreference = 'Stop'
if (-not $env:UPDATE_KEY) { throw 'חסר הסוד UPDATE_SIGNING_KEY ב-GitHub' }
$zip = 'out/TimeLock-win-x64.zip'
$sha = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLower()
$tag = "v$Version"
$url = "https://github.com/$env:GITHUB_REPOSITORY/releases/download/$tag/TimeLock-win-x64.zip"
$json = @{ version = $Version; sha256 = $sha; url = $url } | ConvertTo-Json -Compress
$bytes = [Text.Encoding]::UTF8.GetBytes($json)
[IO.File]::WriteAllBytes('out/update.json', $bytes)
$rsa = [Security.Cryptography.RSA]::Create()
$rsa.ImportFromPem($env:UPDATE_KEY)
$sig = $rsa.SignData($bytes, [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)
[IO.File]::WriteAllBytes('out/update.json.sig', $sig)
gh release create $tag out/TimeLock-win-x64.zip out/update.json out/update.json.sig out/TimeLock.exe out/TimeLock.Service.exe --title $tag --notes "גרסה $Version" --target $env:GITHUB_SHA
