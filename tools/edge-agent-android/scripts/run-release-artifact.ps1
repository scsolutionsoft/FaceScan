$ErrorActionPreference = "Stop"

$env:JAVA_HOME = "C:/Users/Oh-z600-A/AppData/Local/Programs/Microsoft/jdk-17.0.10.7-hotspot"
$env:Path = "$env:JAVA_HOME/bin;$env:Path"
$env:ANDROID_SDK_ROOT = "C:/android-sdk"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $root

& (Join-Path $PSScriptRoot "build-apk-release.ps1")

Get-ChildItem -Path (Join-Path $root "apk/release")
