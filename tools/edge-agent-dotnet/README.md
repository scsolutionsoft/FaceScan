# FaceScan Edge Agent (.NET Worker)

ตัวเลือกนี้ใช้แทน Python agent โดยรันเป็น .NET worker บนเครื่องหน้างานในวง LAN กล้อง

## โครงสร้าง

- Project: `tools/edge-agent-dotnet/FaceScan.EdgeAgent`
- Endpoint ที่ใช้งาน:
  - `/Scan/Preview`
  - `/Scan/Verify`
  - `/EdgeAgent/Heartbeat`

## Build

```bash
dotnet build tools/edge-agent-dotnet/FaceScan.EdgeAgent/FaceScan.EdgeAgent.csproj
```

## Publish (installer payload)

Windows x64:

```bash
powershell -ExecutionPolicy Bypass -File tools/edge-agent-dotnet/publish/publish-win-x64.ps1
```

Linux x64:

```bash
chmod +x tools/edge-agent-dotnet/publish/publish-linux-x64.sh
tools/edge-agent-dotnet/publish/publish-linux-x64.sh
```

## ตัวแปรแวดล้อมที่ต้องตั้ง

- `EDGE_SERVER_URL` เช่น `https://scan.example.com`
- `EDGE_STATION_CODE`
- `EDGE_STATION_TOKEN`
- `EDGE_SNAPSHOT_URL`

ตัวเลือกเสริม:

- `EDGE_AGENT_ID`
- `EDGE_CAMERA_USERNAME`
- `EDGE_CAMERA_PASSWORD`
- `EDGE_INTERVAL_MS` default `1500`
- `EDGE_MIN_CONFIDENCE` default `0.60`
- `EDGE_HEARTBEAT_EVERY_N_FRAMES` default `10`
- `EDGE_ENABLE_PREVIEW` default `true`
- `EDGE_TIMEOUT_SECONDS` default `8`

## Run

```bash
dotnet run --project tools/edge-agent-dotnet/FaceScan.EdgeAgent/FaceScan.EdgeAgent.csproj
```

Agent จะโหลดค่า config จากไฟล์ `edge-agent.env` อัตโนมัติ (ถ้าไฟล์อยู่โฟลเดอร์เดียวกับ executable) หรือระบุด้วย `--env-file <path>`

## Install as Service (Windows)

1. Publish ก่อน
2. แก้ไฟล์ config ที่ `tools/edge-agent-dotnet/publish/dist/win-x64/edge-agent.env`
3. ติดตั้ง service:

```bash
powershell -ExecutionPolicy Bypass -File tools/edge-agent-dotnet/install/windows/install-edge-agent.ps1
```

ถอนติดตั้ง:

```bash
powershell -ExecutionPolicy Bypass -File tools/edge-agent-dotnet/install/windows/uninstall-edge-agent.ps1
```

## Install as Service (Linux)

1. Publish ก่อน
2. แก้ไฟล์ config ตามตัวอย่างใน `edge-agent.sample.env`
3. ติดตั้ง service:

```bash
chmod +x tools/edge-agent-dotnet/install/linux/install-edge-agent.sh
tools/edge-agent-dotnet/install/linux/install-edge-agent.sh
```

ถอนติดตั้ง:

```bash
chmod +x tools/edge-agent-dotnet/install/linux/uninstall-edge-agent.sh
tools/edge-agent-dotnet/install/linux/uninstall-edge-agent.sh
```

## Cloud Connection Checklist

1. สร้าง/แก้ไข Scan Device ในระบบกลางและได้ StationCode + StationToken
2. ตั้งค่า `EDGE_SERVER_URL` เป็นโดเมน cloud (HTTPS)
3. ตั้งค่า `EDGE_SNAPSHOT_URL` ให้เข้าถึงกล้อง local ได้จากเครื่อง agent
4. เปิด outbound จากเครื่อง local ไป cloud เฉพาะพอร์ต 443
5. ตรวจที่หน้าอุปกรณ์ (Devices) ว่า Edge Agent เปลี่ยนเป็นออนไลน์

## แนะนำ production

- ติดตั้งเป็น Windows Service หรือ systemd
- ให้เครื่อง agent ออก internet ได้เฉพาะโดเมน FaceScan
- หมุน station token เป็นระยะ
