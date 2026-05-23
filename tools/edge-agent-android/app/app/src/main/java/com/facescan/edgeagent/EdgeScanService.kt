package com.facescan.edgeagent

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.Service
import android.content.Intent
import android.os.Build
import android.os.IBinder
import android.util.Base64
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.launch
import java.time.Instant
import kotlin.math.max

class EdgeScanService : Service() {
    private val serviceScope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    private var scanJob: Job? = null
    private lateinit var repository: ConfigRepository
    private lateinit var pendingScanDao: PendingScanDao
    private val apiClient = FaceScanApiClient()

    override fun onCreate() {
        super.onCreate()
        repository = ConfigRepository(applicationContext)
        pendingScanDao = EdgeDatabase.getInstance(applicationContext).pendingScanDao()
        startForeground(NOTIFICATION_ID, createNotification("Edge scan service is running"))
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        if (scanJob?.isActive != true) {
            scanJob = serviceScope.launch {
                runScanLoop()
            }
        }
        return START_STICKY
    }

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onDestroy() {
        super.onDestroy()
        scanJob?.cancel()
        serviceScope.cancel()
    }

    private suspend fun runScanLoop() {
        var frameCount = 0
        while (true) {
            val startedAt = System.currentTimeMillis()
            val config = repository.config.first()

            if (!config.isReady()) {
                updateNotification("Waiting config: server/station/snapshot")
                delay(3000)
                continue
            }

            syncPendingQueue(config)

            frameCount += 1
            try {
                val snapshot = apiClient.fetchSnapshot(config)
                if (snapshot != null) {
                    val heartbeatEvery = parseInt(config.heartbeatEveryNFrames, 10, 1, 1000)
                    if (frameCount % heartbeatEvery == 0) {
                        apiClient.sendHeartbeat(config, "alive")
                    }

                    val minConfidence = parseDouble(config.minConfidence, 0.60, 0.0, 1.0)
                    var shouldVerify = true
                    if (config.enablePreview) {
                        val preview = apiClient.callPreview(snapshot, config)
                        shouldVerify = apiClient.isPreviewCandidate(preview.json, minConfidence)
                    }

                    if (shouldVerify) {
                        val verify = apiClient.callVerify(snapshot, config)
                        val success = verify.json?.optBoolean("success", false) == true
                        val message = verify.json?.optString("message", "") ?: ""
                        if (success) {
                            val studentCode = verify.json?.optString("studentCode", "-") ?: "-"
                            updateNotification("Verify OK: $studentCode")
                        } else if (verify.json == null && verify.retryableFailure) {
                            enqueueFailedScan(snapshot)
                            updateNotification("Queued offline scan")
                        } else {
                            val show = if (message.isBlank()) "No match" else message
                            updateNotification("Verify: $show")
                        }
                    } else {
                        updateNotification("Preview filtered")
                    }
                } else {
                    updateNotification("Snapshot failed")
                }
            } catch (_: Exception) {
                updateNotification("Scan loop error")
            }

            val interval = parseInt(config.intervalMs, 1500, 500, 10000)
            val elapsed = System.currentTimeMillis() - startedAt
            val wait = max(interval - elapsed, 0)
            if (wait > 0) {
                delay(wait)
            }
        }
    }

    private fun createNotification(message: String): Notification {
        val manager = getSystemService(NOTIFICATION_SERVICE) as NotificationManager
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            val channel = NotificationChannel(
                CHANNEL_ID,
                "FaceScan Edge Agent",
                NotificationManager.IMPORTANCE_LOW
            )
            manager.createNotificationChannel(channel)
        }

        return Notification.Builder(this, CHANNEL_ID)
            .setContentTitle("FaceScan Edge Agent")
            .setContentText(message)
            .setSmallIcon(android.R.drawable.stat_notify_sync)
            .build()
    }

    private fun updateNotification(message: String) {
        val manager = getSystemService(NOTIFICATION_SERVICE) as NotificationManager
        manager.notify(NOTIFICATION_ID, createNotification(message))
    }

    private fun EdgeConfig.isReady(): Boolean {
        return serverUrl.isNotBlank() &&
            stationCode.isNotBlank() &&
            stationToken.isNotBlank() &&
            snapshotUrl.isNotBlank()
    }

    private fun parseInt(raw: String, fallback: Int, min: Int, max: Int): Int {
        val value = raw.toIntOrNull() ?: fallback
        return value.coerceIn(min, max)
    }

    private fun parseDouble(raw: String, fallback: Double, min: Double, max: Double): Double {
        val value = raw.toDoubleOrNull() ?: fallback
        return value.coerceIn(min, max)
    }

    private suspend fun enqueueFailedScan(imageBytes: ByteArray) {
        val payload = Base64.encodeToString(imageBytes, Base64.NO_WRAP)
        pendingScanDao.insert(
            PendingScanEntity(
                imageBase64 = payload,
                createdAtUtc = Instant.now().toString()
            )
        )

        val count = pendingScanDao.count()
        if (count > MAX_QUEUE_SIZE) {
            pendingScanDao.deleteOldest(count - MAX_QUEUE_SIZE)
        }
    }

    private suspend fun syncPendingQueue(config: EdgeConfig) {
        val batch = pendingScanDao.getNextBatch(MAX_SYNC_BATCH)
        if (batch.isEmpty()) {
            return
        }

        for (item in batch) {
            if (item.retryCount >= MAX_RETRY_COUNT) {
                pendingScanDao.deleteById(item.id)
                continue
            }

            val imageBytes = runCatching { Base64.decode(item.imageBase64, Base64.NO_WRAP) }.getOrNull()
            if (imageBytes == null) {
                pendingScanDao.deleteById(item.id)
                continue
            }

            val verify = apiClient.callVerify(imageBytes, config)
            if (verify.json != null) {
                pendingScanDao.deleteById(item.id)
                updateNotification("Synced queued scan")
            } else if (verify.retryableFailure) {
                pendingScanDao.markRetry(item.id, Instant.now().toString(), "retryable-status-${verify.statusCode ?: 0}")
            } else {
                pendingScanDao.deleteById(item.id)
            }
        }
    }

    companion object {
        private const val CHANNEL_ID = "facescan_edge_agent"
        private const val NOTIFICATION_ID = 4101
        private const val MAX_QUEUE_SIZE = 500
        private const val MAX_SYNC_BATCH = 5
        private const val MAX_RETRY_COUNT = 20
    }
}
