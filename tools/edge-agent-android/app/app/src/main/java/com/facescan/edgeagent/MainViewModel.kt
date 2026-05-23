package com.facescan.edgeagent

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

class MainViewModel(application: Application) : AndroidViewModel(application) {
    private val repository = ConfigRepository(application.applicationContext)
    private val apiClient = FaceScanApiClient()

    private val _uiState = MutableStateFlow(UiState())
    val uiState: StateFlow<UiState> = _uiState.asStateFlow()

    init {
        viewModelScope.launch {
            repository.config.collect { config ->
                _uiState.update { it.copy(config = config) }
            }
        }
    }

    fun updateConfig(config: EdgeConfig) {
        _uiState.update { it.copy(config = config) }
    }

    fun saveConfig() {
        viewModelScope.launch {
            repository.save(_uiState.value.config)
            _uiState.update { it.copy(status = "Saved") }
        }
    }

    fun setStatus(status: String) {
        _uiState.update { it.copy(status = status) }
    }

    fun testConnection() {
        viewModelScope.launch {
            val config = _uiState.value.config
            if (config.serverUrl.isBlank() || config.stationCode.isBlank() || config.stationToken.isBlank()) {
                _uiState.update { it.copy(status = "Missing server/station config") }
                return@launch
            }

            val ok = runCatching { apiClient.sendHeartbeat(config, "test-connection") }.getOrDefault(false)
            _uiState.update { it.copy(status = if (ok) "Test connection: OK" else "Test connection: Failed") }
        }
    }
}

data class UiState(
    val config: EdgeConfig = EdgeConfig(),
    val status: String = "Idle"
)
