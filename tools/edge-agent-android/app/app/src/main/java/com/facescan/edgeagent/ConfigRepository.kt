package com.facescan.edgeagent

import android.content.Context
import androidx.datastore.preferences.core.booleanPreferencesKey
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map

private val Context.dataStore by preferencesDataStore(name = "edge_agent_config")

class ConfigRepository(private val context: Context) {
    private object Keys {
        val ServerUrl = stringPreferencesKey("server_url")
        val StationCode = stringPreferencesKey("station_code")
        val StationToken = stringPreferencesKey("station_token")
        val SnapshotUrl = stringPreferencesKey("snapshot_url")
        val CameraAuthType = stringPreferencesKey("camera_auth_type")
        val CameraUser = stringPreferencesKey("camera_user")
        val CameraPassword = stringPreferencesKey("camera_password")
        val IntervalMs = stringPreferencesKey("interval_ms")
        val MinConfidence = stringPreferencesKey("min_confidence")
        val HeartbeatEveryNFrames = stringPreferencesKey("heartbeat_every_n_frames")
        val TimeoutSeconds = stringPreferencesKey("timeout_seconds")
        val EnablePreview = booleanPreferencesKey("enable_preview")
    }

    val config: Flow<EdgeConfig> = context.dataStore.data.map { pref ->
        EdgeConfig(
            serverUrl = pref[Keys.ServerUrl] ?: "",
            stationCode = pref[Keys.StationCode] ?: "",
            stationToken = pref[Keys.StationToken] ?: "",
            snapshotUrl = pref[Keys.SnapshotUrl] ?: "",
            cameraAuthType = pref[Keys.CameraAuthType] ?: "auto",
            cameraUser = pref[Keys.CameraUser] ?: "",
            cameraPassword = pref[Keys.CameraPassword] ?: "",
            intervalMs = pref[Keys.IntervalMs] ?: "1500",
            minConfidence = pref[Keys.MinConfidence] ?: "0.60",
            heartbeatEveryNFrames = pref[Keys.HeartbeatEveryNFrames] ?: "10",
            timeoutSeconds = pref[Keys.TimeoutSeconds] ?: "8",
            enablePreview = pref[Keys.EnablePreview] ?: true
        )
    }

    suspend fun save(config: EdgeConfig) {
        context.dataStore.edit { pref ->
            pref[Keys.ServerUrl] = config.serverUrl.trim()
            pref[Keys.StationCode] = config.stationCode.trim()
            pref[Keys.StationToken] = config.stationToken.trim()
            pref[Keys.SnapshotUrl] = config.snapshotUrl.trim()
            pref[Keys.CameraAuthType] = config.cameraAuthType.trim().lowercase()
            pref[Keys.CameraUser] = config.cameraUser.trim()
            pref[Keys.CameraPassword] = config.cameraPassword
            pref[Keys.IntervalMs] = config.intervalMs.trim()
            pref[Keys.MinConfidence] = config.minConfidence.trim()
            pref[Keys.HeartbeatEveryNFrames] = config.heartbeatEveryNFrames.trim()
            pref[Keys.TimeoutSeconds] = config.timeoutSeconds.trim()
            pref[Keys.EnablePreview] = config.enablePreview
        }
    }
}
