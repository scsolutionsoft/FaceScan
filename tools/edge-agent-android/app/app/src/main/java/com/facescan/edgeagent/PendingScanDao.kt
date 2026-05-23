package com.facescan.edgeagent

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query

@Dao
interface PendingScanDao {
    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insert(item: PendingScanEntity)

    @Query("SELECT * FROM pending_scans ORDER BY id ASC LIMIT :limit")
    suspend fun getNextBatch(limit: Int): List<PendingScanEntity>

    @Query("DELETE FROM pending_scans WHERE id = :id")
    suspend fun deleteById(id: Long)

    @Query("UPDATE pending_scans SET retry_count = retry_count + 1, last_tried_at_utc = :triedAtUtc, last_error = :error WHERE id = :id")
    suspend fun markRetry(id: Long, triedAtUtc: String, error: String)

    @Query("SELECT COUNT(*) FROM pending_scans")
    suspend fun count(): Int

    @Query("DELETE FROM pending_scans WHERE id IN (SELECT id FROM pending_scans ORDER BY id ASC LIMIT :limit)")
    suspend fun deleteOldest(limit: Int)
}
