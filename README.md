# FaceScan - ระบบเช็คชื่อเข้าออกด้วยการสแกนใบหน้า

## ภาพรวมระบบ
FaceScan เป็นเว็บแอป ASP.NET Core 8 MVC สำหรับบันทึกเวลาเข้า-กลับของนักเรียนด้วยการสแกนใบหน้า รองรับการใช้งานผ่านมือถือ (Android/iPhone) และมีโมดูลจัดการข้อมูลนักเรียน, ลงทะเบียนใบหน้า, รายงานการมาเรียน, รายงานมาสาย, บันทึกระบบ และสิทธิ์ผู้ใช้งานตามบทบาท

## เทคโนโลยีที่ใช้
- ASP.NET Core 8 MVC
- SQL Server + EF Core 8
- ASP.NET Core Identity (Authentication / Authorization / Role)
- Bootstrap 5
- CsvHelper (Import/Export CSV)

## โครงสร้างโปรเจกต์
- `FaceScan.slnx`
- `FaceScan.Web/Controllers`
- `FaceScan.Web/Models`
- `FaceScan.Web/Data`
- `FaceScan.Web/ViewModels`
- `FaceScan.Web/Services`
- `FaceScan.Web/Repositories`
- `FaceScan.Web/Helpers`
- `FaceScan.Web/Mappings`
- `FaceScan.Web/Validators`
- `FaceScan.Web/Areas/Admin`
- `FaceScan.Web/Areas/Teacher`
- `FaceScan.Web/Views`
- `FaceScan.Web/wwwroot`
- `FaceScan.Web/Logs`

## Connection String
ตั้งค่าใน `FaceScan.Web/appsettings.json`

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=119.59.118.29;User Id=sa;password=Ab14781010; Database=FaceScanReg;TrustServerCertificate=True;MultipleActiveResultSets=true"
}
```

## วิธีติดตั้งและรัน
1. Restore package
```bash
dotnet restore FaceScan.slnx
```

2. ติดตั้ง EF Tool (ถ้ายังไม่มี)
```bash
dotnet tool install --global dotnet-ef
```

3. อัปเดตฐานข้อมูลจาก migrations ที่มีอยู่
```bash
dotnet ef database update --project FaceScan.Web/FaceScan.Web.csproj --startup-project FaceScan.Web/FaceScan.Web.csproj
```

4. Build
```bash
dotnet build FaceScan.slnx
```

5. Run
```bash
dotnet run --project FaceScan.Web/FaceScan.Web.csproj
```

## บัญชีเริ่มต้น (Seed Data)
- SuperAdmin: `superadmin` / `Admin@123456`
- Admin: `admin` / `Admin@123456`
- Staff: `staff` / `Staff@123456`
- Viewer: `viewer` / `Viewer@123456`

## บทบาทผู้ใช้
- `SuperAdmin`
- `Admin`
- `Staff`
- `Viewer`
- `Student`

## ข้อมูลตัวอย่างที่ถูก seed
- ปีการศึกษา 1 ชุด
- ระดับชั้น ม.1 ถึง ม.6
- ห้องเรียนตัวอย่างหลายห้อง
- นักเรียนตัวอย่างอย่างน้อย 20 คน
- อุปกรณ์สแกนตัวอย่าง 1 จุด
- System Settings เริ่มต้น (รวมเวลาเช็คมาสาย `08:00`)

## ฟีเจอร์นักเรียน
- สมัครสมาชิกนักเรียนผ่านหน้า `Account/StudentRegister` ด้วย `StudentCode + BirthDate`
- เมนูเฉพาะนักเรียน
  - ลงทะเบียนใบหน้า
  - ตรวจสอบเวลาเข้า-กลับ
  - พิมพ์รายงานการมาเรียนของตนเอง

## กติกามาสาย
- นักเรียนที่เช็คอินหลังเวลาที่กำหนดใน `Settings > เวลาเริ่มนับมาสาย`
- ค่าเริ่มต้นคือ `08:00`
- มีรายงานมาสายแยก และแสดงในสรุปรายวัน/สรุปตามชั้น

## หมายเหตุการใช้งานกล้องมือถือ
- เบราว์เซอร์ส่วนใหญ่ต้องใช้ `HTTPS` เพื่อให้ `getUserMedia` ใช้งานกล้องได้
- การทดสอบบน `localhost` มักใช้งานได้โดยไม่ต้องเปิด HTTPS จริง
- หน้า `/Scan/Index` กำหนดการเข้าถึงด้วย `stationCode` และ `stationToken` เมื่อไม่ได้ล็อกอิน

## IP Camera (Local Network + VPS)

รองรับ 2 แนวทางติดตั้งเมื่อกล้องอยู่ในวง LAN แต่ระบบอยู่บน VPS

- ระบบที่ 1: VPS เชื่อมเข้า LAN ผ่าน VPN/WireGuard
- ระบบที่ 2: ใช้ Edge Agent ในหน้างานดึงภาพจากกล้องแล้วส่งขึ้น VPS

ดูรายละเอียดที่:

- `docs/ip-camera-vps-architecture.md`
- `docs/walkby-edge-app-design.md`
- `docs/android-edge-app-plan.md`
- `tools/edge-agent/README.md`
- `tools/edge-agent-dotnet/README.md`
- `tools/edge-agent-android/README.md`
- `tools/edge-agent-android/apk/README.md`
- `tools/edge-agent-android/app`

## Mock Face Service และการเปลี่ยน Provider จริง
ระบบใช้ `MockFaceRecognitionService` ผ่าน interface `IFaceRecognitionService`

ไฟล์ที่เกี่ยวข้อง:
- Interface: `FaceScan.Web/Services/Interfaces/IFaceRecognitionService.cs`
- Mock: `FaceScan.Web/Services/MockFaceRecognitionService.cs`
- DI: `FaceScan.Web/Program.cs`
- Config: `FaceScan.Web/appsettings.json` section `FaceRecognition`

แนวทางเปลี่ยนเป็น Provider จริง:
1. สร้าง class ใหม่ (เช่น `AzureFaceRecognitionService`) และ implement `IFaceRecognitionService`
2. เพิ่ม config ที่จำเป็นใน `FaceRecognition`
3. ปรับ DI ใน `Program.cs` ให้ชี้ไป implementation ใหม่
4. ทดสอบ flow `FaceEnrollment` และ `Scan/Verify`

## ความเสถียรและบันทึกระบบ
- ใช้ global exception middleware และบันทึกลงไฟล์ใน `FaceScan.Web/Logs`
- มี AuditLog สำหรับการแก้ไขข้อมูลสำคัญ
- มี Export CSV สำหรับรายงานหลัก
