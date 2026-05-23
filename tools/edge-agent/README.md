# Edge Snapshot Agent (Local LAN -> VPS)

Agent ตัวนี้รันที่เครื่องภายในวง LAN เดียวกับกล้อง IP Camera เพื่อดึงภาพจากกล้องแล้วส่งขึ้น FaceScan บน VPS ผ่าน HTTPS

## เหมาะกับกรณีใด

- กล้องอยู่ private network เช่น `192.168.x.x`
- FaceScan อยู่บน VPS ที่เข้าถึงกล้องตรงๆ ไม่ได้
- ต้องการให้ทราฟฟิกวิ่งออกจากหน้างานไป VPS อย่างเดียว

## ติดตั้ง

1. ติดตั้ง Python 3.10+
2. ติดตั้งแพ็กเกจ

```bash
pip install -r requirements.txt
```

## รัน

```bash
python edge_snapshot_agent.py \
  --server https://scan.example.com \
  --station-code MAIN-GATE \
  --station-token SCAN-12345 \
  --snapshot-url http://192.168.1.50/ISAPI/Streaming/channels/101/picture \
  --camera-username admin \
  --camera-password "your-password" \
  --interval-ms 1500 \
  --min-confidence 0.60 \
  --agent-id gate01-agent \
  --heartbeat-every-n-frames 10
```

## พารามิเตอร์หลัก

- `--server` URL ของ FaceScan VPS
- `--station-code`, `--station-token` ต้องตรงกับอุปกรณ์ในระบบ
- `--snapshot-url` URL snapshot ของกล้อง local
- `--interval-ms` ระยะดึงภาพแต่ละรอบ
- `--verify-every-n-frames` ลดโหลดการ verify โดย verify ทุก N เฟรม
- `--heartbeat-every-n-frames` ส่ง heartbeat ไป VPS ทุก N เฟรม
- `--agent-id` รหัสประจำ agent (ใช้แสดงสถานะ online/offline)
- `--disable-preview` ปิดขั้น preview และยิง verify ตรง
- `--insecure-tls` ใช้เฉพาะทดสอบ (self-signed cert)

## Security Notes

- แนะนำใช้ HTTPS ที่มี cert ถูกต้อง
- จำกัด outbound เฉพาะไปโดเมน FaceScan
- ตั้ง station token ให้ยากเดาและเปลี่ยนตามรอบ
- ให้ agent รันแบบ service และเก็บ log แยกไฟล์
