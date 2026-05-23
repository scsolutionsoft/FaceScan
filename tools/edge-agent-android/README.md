# FaceScan Edge Agent Android (Design Starter)

โฟลเดอร์นี้เป็นจุดเริ่มต้นสำหรับพัฒนาแอป Android ที่สแกนอัตโนมัติจาก IP Camera แล้วส่งข้อมูลไป FaceScan Cloud

## Target Stack

- Android 10+
- Kotlin
- Jetpack Compose
- Foreground Service + WorkManager
- Retrofit/OkHttp
- DataStore (config)
- Room (optional retry queue)

## Scaffold Status

- Android app scaffold พร้อมใช้งานอยู่ที่ `app/`
- มี Compose config screen + DataStore + Foreground Service scan loop
- มีปุ่ม Test Connection (ยิง heartbeat)
- มี offline retry queue (Room) และ sync กลับอัตโนมัติ
- รองรับกล้องแบบ `auto`, `basic`, `digest`
- ใช้ endpoint เดิมของ FaceScan (`/Scan/Preview`, `/Scan/Verify`, `/EdgeAgent/Heartbeat`)

## First Run Setup

1. ติดตั้ง Android Studio (JDK 17 + Android SDK)
2. เปิดโฟลเดอร์ `tools/edge-agent-android/app`
3. ถ้ายังไม่มี gradle wrapper ให้รัน:

```powershell
powershell -ExecutionPolicy Bypass -File ./scripts/init-gradle-wrapper.ps1
```

4. build debug apk:

```powershell
powershell -ExecutionPolicy Bypass -File ./scripts/build-apk-debug.ps1
```

## One-shot Build Env Setup (Windows)

ถ้าติดตั้ง JDK + Android SDK command-line tools แล้ว ให้รัน:

```powershell
powershell -ExecutionPolicy Bypass -File ./scripts/setup-android-build-env.ps1
```

สคริปต์นี้จะ:
- ตั้งค่า `local.properties` ให้ชี้ `sdk.dir`
- ติดตั้ง SDK packages ที่ต้องใช้ (`platform-tools`, `platforms;android-35`, `build-tools;35.0.0`)

## Suggested Modules

- app
- core-network
- core-storage
- feature-config
- feature-monitor
- worker-scan

## APK Output

- apk/debug
- apk/release

ไฟล์ APK ที่ build เสร็จแล้วให้เก็บในโฟลเดอร์ข้างต้นเพื่อส่งมอบติดตั้ง

## Build Scripts (Windows)

ต้องมี Android Gradle project อยู่ในโฟลเดอร์ app ก่อน

Debug APK:

```powershell
powershell -ExecutionPolicy Bypass -File ./scripts/build-apk-debug.ps1
```

Release APK:

```powershell
powershell -ExecutionPolicy Bypass -File ./scripts/build-apk-release.ps1 -VersionName 0.1.0
```

Signed Release APK (Production):

1. คัดลอก `app/keystore.properties.sample` เป็น `app/keystore.properties`
2. ใส่ค่าจริงของ keystore ให้ครบ
3. รันคำสั่ง:

```powershell
powershell -ExecutionPolicy Bypass -File ./scripts/build-apk-signed-release.ps1 -VersionName 1.0.0
```

## Runtime Config (JSON example)

ดูไฟล์ `config.sample.json`

## API endpoints

- POST /Scan/Preview
- POST /Scan/Verify
- POST /EdgeAgent/Heartbeat

## Development Plan

อ่านเอกสารเต็มที่:
- `docs/android-edge-app-plan.md`
