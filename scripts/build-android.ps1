param([string]$Architectures = 'arm64-v8a,armeabi-v7a,x86_64', [string]$TemporaryDirectory = '', [string]$StagingDirectory = '')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$mobileRoot = Join-Path $projectRoot 'mobile'
$bundledJava = Join-Path $projectRoot '.tools/android-build/java'
if (-not $env:JAVA_HOME -and (Test-Path $bundledJava)) {
    $env:JAVA_HOME = (Get-ChildItem $bundledJava -Directory | Select-Object -First 1).FullName
}
if (-not $env:ANDROID_HOME) { $env:ANDROID_HOME = Join-Path $projectRoot '.tools/android-sdk' }
if (-not (Test-Path "$env:JAVA_HOME/bin/java.exe")) { throw 'Set JAVA_HOME to a JDK 17 installation.' }
if (-not (Test-Path "$env:ANDROID_HOME/platforms/android-36")) { throw 'Install Android SDK platform 36 and set ANDROID_HOME.' }
$env:PATH = "$env:JAVA_HOME/bin;$env:PATH"
$env:GRADLE_USER_HOME = Join-Path $projectRoot '.tools/gradle-home'
$env:CI = '1'
$env:NODE_ENV = 'production'
$env:JAVA_TOOL_OPTIONS = '-Djava.net.preferIPv4Stack=true'
if ($TemporaryDirectory) {
    $buildTemp = [IO.Path]::GetFullPath($TemporaryDirectory)
    New-Item -ItemType Directory -Force $buildTemp | Out-Null
    $env:TEMP = $buildTemp
    $env:TMP = $buildTemp
    $env:JAVA_TOOL_OPTIONS += ' "-Djava.io.tmpdir=' + $buildTemp + '" "-Djdk.net.unixdomain.tmpdir=' + $buildTemp + '"'
}

$signingDirectory = Join-Path $mobileRoot 'signing'
$signingProperties = Join-Path $signingDirectory 'release.properties'
$signingKey = Join-Path $signingDirectory 'tableflow-release.p12'
New-Item -ItemType Directory -Force $signingDirectory | Out-Null
if (-not (Test-Path $signingProperties)) {
    if (Test-Path $signingKey) { throw 'Signing key exists without its properties. Restore the original properties; do not replace the key.' }
    $keyPassword = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    $env:TABLEFLOW_KEY_PASSWORD = $keyPassword
    try {
        & "$env:JAVA_HOME/bin/keytool.exe" -genkeypair -noprompt -keystore $signingKey -storetype PKCS12 -storepass:env TABLEFLOW_KEY_PASSWORD -keypass:env TABLEFLOW_KEY_PASSWORD -alias tableflow -keyalg RSA -keysize 3072 -validity 10000 -dname 'CN=Tableflow Restaurant, OU=Mobile, O=Tableflow'
        if ($LASTEXITCODE -ne 0) { throw 'Signing key generation failed.' }
        [IO.File]::WriteAllText($signingProperties, "storePassword=$keyPassword`nkeyPassword=$keyPassword`nkeyAlias=tableflow`n")
    } finally { Remove-Item Env:TABLEFLOW_KEY_PASSWORD -ErrorAction SilentlyContinue }
}
if (-not (Test-Path $signingKey)) { throw 'Restore the original signing key before building an update.' }

if ($StagingDirectory) {
    $stage = [IO.Path]::GetFullPath($StagingDirectory)
    if ($stage -eq $projectRoot -or $stage.StartsWith($mobileRoot + [IO.Path]::DirectorySeparatorChar)) {
        throw 'Use a separate short staging directory, outside mobile.'
    }
    New-Item -ItemType Directory -Force $stage | Out-Null
    & robocopy.exe $mobileRoot (Join-Path $stage 'mobile') /E /XJ /R:1 /W:1 /NFL /NDL /NJH /NJS /NP /XD (Join-Path $mobileRoot 'android') (Join-Path $mobileRoot 'dist') (Join-Path $mobileRoot '.expo') '.cxx' '.gradle'
    if ($LASTEXITCODE -ge 8) { throw 'Copying mobile build inputs failed.' }
    & robocopy.exe (Join-Path $projectRoot 'shared') (Join-Path $stage 'shared') /E /R:1 /W:1 /NFL /NDL /NJH /NJS /NP
    if ($LASTEXITCODE -ge 8) { throw 'Copying shared build inputs failed.' }
    New-Item -ItemType Directory -Force (Join-Path $stage 'web/src') | Out-Null
    Copy-Item -LiteralPath (Join-Path $projectRoot 'web/src/types.ts') -Destination (Join-Path $stage 'web/src/types.ts')
    $mobileRoot = Join-Path $stage 'mobile'
}

Push-Location $mobileRoot
try {
    & npm.cmd run typecheck
    if ($LASTEXITCODE -ne 0) { throw 'TypeScript validation failed.' }
    & npx.cmd expo prebuild --platform android --no-install
    if ($LASTEXITCODE -ne 0) { throw 'Android generation failed.' }
    Push-Location android
    try {
        $gradle = Join-Path $projectRoot '.tools/android-build/gradle-9.0.0/bin/gradle.bat'
        if (-not (Test-Path $gradle)) { $gradle = '.\gradlew.bat' }
        & $gradle :app:assembleRelease "-PreactNativeArchitectures=$Architectures" --max-workers=2 --console=plain
        if ($LASTEXITCODE -ne 0) { throw 'Android release build failed.' }
    } finally { Pop-Location }
    $outputDirectory = Join-Path $projectRoot 'deliverables'
    New-Item -ItemType Directory -Force $outputDirectory | Out-Null
    $apk = Join-Path $outputDirectory 'Tableflow-1.0.0.apk'
    Copy-Item -LiteralPath 'android/app/build/outputs/apk/release/app-release.apk' -Destination $apk
    & "$env:ANDROID_HOME/build-tools/36.0.0/apksigner.bat" verify --verbose $apk
    if ($LASTEXITCODE -ne 0) { throw 'APK signature verification failed.' }
    $checksum = (Get-FileHash $apk -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText("$apk.sha256", "$checksum  Tableflow-1.0.0.apk`n")
    Write-Output "APK ready: $apk"
} finally {
    Pop-Location
    if ($StagingDirectory -and $mobileRoot -eq (Join-Path $stage 'mobile')) {
        foreach ($name in @('release.properties', 'tableflow-release.p12')) {
            $temporaryKey = Join-Path $mobileRoot "signing/$name"
            if (Test-Path $temporaryKey) { Remove-Item -LiteralPath $temporaryKey }
        }
    }
}
