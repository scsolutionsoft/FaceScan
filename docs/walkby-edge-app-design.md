# Walk-by IP Camera Edge App Design (Windows + Linux)

เอกสารนี้ออกแบบระบบแอปหน้างานที่ติดตั้งบนเครื่อง local และส่งข้อมูลสแกนขึ้น Cloud Server เท่านั้น

## เป้าหมาย

- ใช้งานเฉพาะโหมดสแกนเดินผ่านจาก IP Camera
- รองรับติดตั้งบน Windows และ Linux
- ให้ local site ออก internet เพียงทางเดียว (outbound to cloud)
- ให้ cloud แสดงสถานะ agent online/offline ได้

## สถาปัตยกรรม

- Local Agent App:
  - ดึงภาพจากกล้อง IP ภายใน LAN
  - ประมวลผลรอบสแกน (Preview/Verify)
  - ส่ง heartbeat ไป cloud
- Cloud FaceScan Server:
  - รับสแกนผ่าน `/Scan/Preview` และ `/Scan/Verify`
  - รับ heartbeat ผ่าน `/EdgeAgent/Heartbeat`
  - แสดงสถานะ agent ที่หน้า Devices

Data flow:

1. Agent ดึง snapshot จากกล้อง local
2. Agent ส่งภาพไป cloud
3. Cloud บันทึกผลสแกนเข้า attendance path เดิม
4. Agent ส่ง heartbeat ทุก N เฟรม
5. Cloud คำนวณสถานะ online/offline จาก timestamp ล่าสุด

## องค์ประกอบที่พัฒนาในโค้ด

- App ฝั่ง local (.NET Worker):
  - `tools/edge-agent-dotnet/FaceScan.EdgeAgent/Program.cs`
  - รองรับ config ผ่าน environment และไฟล์ `edge-agent.env`
- Installer & publisher:
  - `tools/edge-agent-dotnet/publish/publish-win-x64.ps1`
  - `tools/edge-agent-dotnet/publish/publish-linux-x64.sh`
  - `tools/edge-agent-dotnet/install/windows/install-edge-agent.ps1`
  - `tools/edge-agent-dotnet/install/linux/install-edge-agent.sh`
- Cloud heartbeat backend:
  - `FaceScan.Web/Controllers/EdgeAgentController.cs`
  - `FaceScan.Web/Models/Entities/EdgeAgentHeartbeat.cs`
  - `FaceScan.Web/Views/Devices/Index.cshtml` (แสดงสถานะ)

## มาตรฐานความปลอดภัย

- ใช้ HTTPS ระหว่าง agent กับ cloud
- เก็บ token ในไฟล์ env ด้วยสิทธิ์จำกัด
- หมุน StationToken ตามรอบเวลา
- จำกัด outbound firewall ของเครื่อง agent เฉพาะโดเมน cloud

## แผนการติดตั้งหน้างาน

1. สร้าง station สำหรับแต่ละกล้องในระบบกลาง
2. publish binary ตาม OS เป้าหมาย
3. ติดตั้ง service ด้วยสคริปต์
4. กรอกค่า `edge-agent.env`
5. ตรวจ heartbeat ที่หน้า Devices

## การสเกลหลายสาขา

- 1 กล้อง = 1 station code
- 1 เครื่อง agent สามารถรันหลาย instance ได้ (แยก env/service name)
- แนะนำแยก token ต่อ station ต่อสาขา

## แนวทางพัฒนาต่อ

1. เพิ่ม local spool queue เพื่อส่งซ้ำเมื่อเน็ตล่ม
2. เพิ่ม auto update สำหรับ agent
3. เพิ่ม dashboard SLA ของ latency และ camera health
4. เพิ่ม signed config และ secret rotation API
