# APK Output Folder

โฟลเดอร์นี้ใช้เก็บไฟล์ APK ที่ build เสร็จแล้วเพื่อส่งมอบติดตั้งหน้างาน

## โครงสร้าง

- debug/
- release/

## มาตรฐานชื่อไฟล์ที่แนะนำ

- debug/facescan-edge-agent-debug.apk
- release/facescan-edge-agent-v{version}-release.apk

## ขั้นตอนใช้งาน

1. build จาก Android project
2. copy APK ที่ได้มาใส่ในโฟลเดอร์นี้
3. ส่งมอบทีมติดตั้งหรือ MDM จากโฟลเดอร์ release/
