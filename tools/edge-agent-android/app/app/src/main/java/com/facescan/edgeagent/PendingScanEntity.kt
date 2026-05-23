package com.facescan.edgeagent

import androidx.room.ColumnInfo
import androidx.room.Entity
import androidx.room.PrimaryKey

@Entity(tableName = "pending_scans")
data class PendingScanEntity(
    @PrimaryKey(autoGenerate = true) val id: Long = 0,
    @ColumnInfo(name = "image_base64") val imageBase64: String,
    @ColumnInfo(name = "created_at_utc") val createdAtUtc: String,
    @ColumnInfo(name = "retry_count") val retryCount: Int = 0,
    @ColumnInfo(name = "last_tried_at_utc") val lastTriedAtUtc: String? = null,
    @ColumnInfo(name = "last_error") val lastError: String? = null
)
