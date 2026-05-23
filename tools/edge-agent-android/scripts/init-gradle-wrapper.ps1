$ErrorActionPreference = "Stop"

$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "../app")
Push-Location $projectRoot
try {
    if (Test-Path "./gradlew.bat") {
        Write-Host "gradlew.bat already exists" -ForegroundColor Yellow
        exit 0
    }

    if (-not (Get-Command gradle -ErrorAction SilentlyContinue)) {
        throw "ไม่พบคำสั่ง gradle ใน PATH กรุณาติดตั้ง Gradle หรือสร้างโปรเจกต์ด้วย Android Studio ก่อน"
    }

    gradle wrapper --gradle-version 8.7
    if ($LASTEXITCODE -ne 0) {
        throw "gradle wrapper failed"
    }

    Write-Host "Gradle wrapper initialized" -ForegroundColor Green
}
finally {
    Pop-Location
}
