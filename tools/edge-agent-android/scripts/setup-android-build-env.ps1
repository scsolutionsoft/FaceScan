param(
    [string]$JdkHome = "C:\Users\Oh-z600-A\AppData\Local\Programs\Microsoft\jdk-17.0.10.7-hotspot",
    [string]$SdkRoot = "C:\android-sdk",
    [string]$ProjectRoot = "../app"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path (Join-Path $JdkHome "bin\java.exe"))) {
    throw "JDK not found at $JdkHome. Install Microsoft.OpenJDK.17 first."
}

$projectPath = Resolve-Path (Join-Path $PSScriptRoot $ProjectRoot)

$cmdlineTools = Join-Path $SdkRoot "cmdline-tools\latest\bin\sdkmanager.bat"
if (-not (Test-Path $cmdlineTools)) {
    throw "Android cmdline-tools not found at $cmdlineTools"
}

$localPropsPath = Join-Path $projectPath "local.properties"
$sdkDirEscaped = $SdkRoot.Replace("\", "\\")
Set-Content -Path $localPropsPath -Value "sdk.dir=$sdkDirEscaped" -Encoding ascii

$env:JAVA_HOME = $JdkHome
$env:Path = "$JdkHome\bin;$env:Path"
$env:ANDROID_SDK_ROOT = $SdkRoot

Write-Host "Installing required Android SDK packages..." -ForegroundColor Cyan
& $cmdlineTools --sdk_root=$SdkRoot "platform-tools" "platforms;android-35" "build-tools;35.0.0"
if ($LASTEXITCODE -ne 0) {
    throw "sdkmanager package installation failed"
}

Write-Host "Android build environment is ready." -ForegroundColor Green
Write-Host "local.properties: $localPropsPath" -ForegroundColor Yellow
