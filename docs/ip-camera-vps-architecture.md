# IP Camera on Local LAN with FaceScan on VPS

เอกสารนี้สรุปการพัฒนาและการติดตั้ง 2 ระบบสำหรับโจทย์กล้องอยู่ LAN แต่แอปอยู่ VPS

## ระบบที่ 1: VPS <-> LAN ด้วย VPN/WireGuard

ใช้เมื่อหน้างานสามารถติดตั้ง gateway/router ที่ทำ site-to-site VPN ได้

### ภาพรวม

- VPS เชื่อม tunnel ไป gateway หน้างาน
- VPS เข้าถึงกล้อง private IP ได้ตรง
- ระบบเดิมของ FaceScan (`/IpCamera/ProxySnapshot`) ใช้ต่อได้ทันที

### Network ที่ควรตั้ง

- VPS subnet VPN: `10.200.0.1/24`
- Site subnet VPN: `10.200.0.2/24`
- Camera LAN: `192.168.10.0/24`

Route สำคัญ:

- ที่ VPS: route `192.168.10.0/24` ผ่าน peer `10.200.0.2`
- ที่ Site Gateway: route `10.200.0.1/32` หรือ subnet VPS ผ่าน peer `10.200.0.1`

### Security Baseline

- อนุญาตจาก VPS ไปกล้องเฉพาะพอร์ตที่จำเป็น (80/443)
- ปิด inbound จาก internet เข้ากล้องโดยตรง
- แยก VLAN กล้อง
- จำกัด source IP ฝั่งกล้องให้เฉพาะ gateway/VPN

### ข้อดี

- โค้ดแอปเดิมเปลี่ยนน้อยมาก
- ใช้หน้า `/IpCamera/Menu` และ `/IpCamera/Index` ต่อได้

### ข้อควรระวัง

- ต้องดูแล tunnel และ route
- ถ้า VPN ล่ม สถานีกล้องทั้งหมดจะ offline พร้อมกัน

---

## ระบบที่ 2: Edge Agent ที่หน้างานส่งภาพขึ้น VPS

ใช้เมื่อไม่ต้องการเชื่อม LAN เข้าหา VPS หรือจัดการ VPN ยาก

### ภาพรวม

- Agent อยู่ใน LAN เดียวกับกล้อง
- Agent ดึง snapshot local camera
- Agent ยิง API ไป VPS (`/Scan/Preview`, `/Scan/Verify`)
- ไม่มีการเปิดกล้องออก internet

### ส่วนที่เพิ่มในโปรเจกต์

- Agent script: [tools/edge-agent/edge_snapshot_agent.py](../tools/edge-agent/edge_snapshot_agent.py)
- คู่มือติดตั้ง: [tools/edge-agent/README.md](../tools/edge-agent/README.md)
- Agent (.NET Worker): [tools/edge-agent-dotnet/FaceScan.EdgeAgent/Program.cs](../tools/edge-agent-dotnet/FaceScan.EdgeAgent/Program.cs)
- คู่มือ .NET Worker: [tools/edge-agent-dotnet/README.md](../tools/edge-agent-dotnet/README.md)
- Heartbeat endpoint: `/EdgeAgent/Heartbeat`

### Security Baseline

- ใช้ HTTPS เสมอ
- เก็บ station token เป็น secret
- จำกัด egress ของเครื่อง agent ให้ไปเฉพาะโดเมน FaceScan
- หมุน token ตามรอบเวลา

### Scaling

- 1 agent ต่อ 1 กล้อง หรือหลายกล้องก็ได้ (แยก process)
- กรณีหลายสาขา ให้ตั้ง station code แยกชัดเจน

---

## ควรเลือกแบบไหน

- ต้องการใช้งานเร็วที่สุดและทีม network พร้อม: เลือก VPN/WireGuard
- ต้องการระบบ production ที่ปลอดภัยและไม่พึ่ง route ซับซ้อน: เลือก Edge Agent

## แนวทางพัฒนาต่อ (แนะนำ)

1. เพิ่มตาราง `EdgeAgentHeartbeat` สำหรับ monitor online/offline ของ agent
2. เพิ่ม endpoint rotate token เฉพาะ station
3. เพิ่ม queue/buffer ใน agent เวลา internet สะดุด
4. เพิ่ม dashboard แสดงสถานะ VPN/Agent แยกจากสถานะกล้อง
