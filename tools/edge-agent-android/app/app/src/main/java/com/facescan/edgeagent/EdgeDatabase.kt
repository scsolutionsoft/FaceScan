package com.facescan.edgeagent

import android.content.Context
import androidx.room.Database
import androidx.room.Room
import androidx.room.RoomDatabase

@Database(
    entities = [PendingScanEntity::class],
    version = 1,
    exportSchema = false
)
abstract class EdgeDatabase : RoomDatabase() {
    abstract fun pendingScanDao(): PendingScanDao

    companion object {
        @Volatile
        private var INSTANCE: EdgeDatabase? = null

        fun getInstance(context: Context): EdgeDatabase {
            return INSTANCE ?: synchronized(this) {
                INSTANCE ?: Room.databaseBuilder(
                    context.applicationContext,
                    EdgeDatabase::class.java,
                    "edge_agent.db"
                ).build().also { INSTANCE = it }
            }
        }
    }
}
