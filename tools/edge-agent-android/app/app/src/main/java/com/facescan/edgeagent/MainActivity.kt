package com.facescan.edgeagent

import android.content.Intent
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewmodel.compose.viewModel

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()

        setContent {
            MaterialTheme {
                val viewModel: MainViewModel = viewModel()
                val state by viewModel.uiState.collectAsStateWithLifecycle()
                ConfigScreen(
                    state = state,
                    onChange = viewModel::updateConfig,
                    onSave = viewModel::saveConfig,
                    onTest = viewModel::testConnection,
                    onStartService = {
                        startService(Intent(this, EdgeScanService::class.java))
                        viewModel.setStatus("Service started")
                    },
                    onStopService = {
                        stopService(Intent(this, EdgeScanService::class.java))
                        viewModel.setStatus("Service stopped")
                    }
                )
            }
        }
    }
}

@Composable
private fun ConfigScreen(
    state: UiState,
    onChange: (EdgeConfig) -> Unit,
    onSave: () -> Unit,
    onTest: () -> Unit,
    onStartService: () -> Unit,
    onStopService: () -> Unit
) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(10.dp)
    ) {
        Text("FaceScan Android Edge Agent", style = MaterialTheme.typography.headlineSmall)
        Text("Status: ${state.status}")

        Field("Server URL", state.config.serverUrl) { onChange(state.config.copy(serverUrl = it)) }
        Field("Station Code", state.config.stationCode) { onChange(state.config.copy(stationCode = it)) }
        Field("Station Token", state.config.stationToken) { onChange(state.config.copy(stationToken = it)) }
        Field("Snapshot URL", state.config.snapshotUrl) { onChange(state.config.copy(snapshotUrl = it)) }
        Field("Camera Auth (auto/basic/digest)", state.config.cameraAuthType) {
            onChange(state.config.copy(cameraAuthType = it))
        }
        Field("Camera Username", state.config.cameraUser) { onChange(state.config.copy(cameraUser = it)) }
        Field("Camera Password", state.config.cameraPassword) { onChange(state.config.copy(cameraPassword = it)) }
        Field("Interval (ms)", state.config.intervalMs) { onChange(state.config.copy(intervalMs = it)) }
        Field("Min Confidence", state.config.minConfidence) { onChange(state.config.copy(minConfidence = it)) }
        Field("Heartbeat Every N Frames", state.config.heartbeatEveryNFrames) {
            onChange(state.config.copy(heartbeatEveryNFrames = it))
        }
        Field("Timeout (seconds)", state.config.timeoutSeconds) { onChange(state.config.copy(timeoutSeconds = it)) }

        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.SpaceBetween
        ) {
            Text("Enable Preview")
            Switch(
                checked = state.config.enablePreview,
                onCheckedChange = { onChange(state.config.copy(enablePreview = it)) }
            )
        }

        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            Button(onClick = onSave) { Text("Save") }
            Button(onClick = onTest) { Text("Test") }
            Button(onClick = onStartService) { Text("Start") }
            Button(onClick = onStopService) { Text("Stop") }
        }
    }
}

@Composable
private fun Field(label: String, value: String, onValueChange: (String) -> Unit) {
    OutlinedTextField(
        value = value,
        onValueChange = onValueChange,
        modifier = Modifier.fillMaxWidth(),
        label = { Text(label) },
        singleLine = true
    )
}
