package com.facescan.edgeagent

data class EdgeConfig(
    val serverUrl: String = "",
    val stationCode: String = "",
    val stationToken: String = "",
    val snapshotUrl: String = "",
    val cameraAuthType: String = "auto",
    val cameraUser: String = "",
    val cameraPassword: String = "",
    val intervalMs: String = "1500",
    val minConfidence: String = "0.60",
    val heartbeatEveryNFrames: String = "10",
    val timeoutSeconds: String = "8",
    val enablePreview: Boolean = true
)
