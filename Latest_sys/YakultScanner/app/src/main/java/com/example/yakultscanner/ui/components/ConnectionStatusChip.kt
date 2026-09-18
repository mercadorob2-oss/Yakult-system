package com.example.yakultscanner.ui.components

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.width
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Cloud
import androidx.compose.material.icons.filled.CloudOff
import androidx.compose.material.icons.filled.ErrorOutline
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material3.AssistChip
import androidx.compose.material3.AssistChipDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import com.example.yakultscanner.ConnectionHealthStore
import java.util.concurrent.TimeUnit

@Composable
fun ConnectionStatusChip() {
    val snapshot by ConnectionHealthStore.state.collectAsState()
    val hasNetwork = rememberIsNetworkAvailable()
    var showDialog by remember { mutableStateOf(false) }

    val now = System.currentTimeMillis()
    val lastOk = snapshot.lastSuccessAtMs
    val lastOkText = lastOk?.let { formatRelativeTime(now - it) } ?: "never"

    val statusLabel: String
    val leadingIcon = when {
        !hasNetwork -> Icons.Filled.CloudOff
        lastOk != null && now - lastOk <= TimeUnit.MINUTES.toMillis(5) -> Icons.Filled.Cloud
        snapshot.lastFailureAtMs != null -> Icons.Filled.ErrorOutline
        else -> Icons.Filled.Cloud
    }

    val (container, content) = when {
        !hasNetwork -> ScannerWorkspaceUi.Error.copy(alpha = 0.12f) to ScannerWorkspaceUi.Error
        lastOk != null && now - lastOk <= TimeUnit.MINUTES.toMillis(5) -> ScannerWorkspaceUi.Success.copy(alpha = 0.12f) to ScannerWorkspaceUi.Success
        snapshot.lastFailureAtMs != null -> ScannerWorkspaceUi.Error.copy(alpha = 0.12f) to ScannerWorkspaceUi.Error
        else -> ScannerWorkspaceUi.SurfaceSubtle to ScannerWorkspaceUi.Muted
    }

    statusLabel = when {
        !hasNetwork -> "Offline"
        lastOk != null && now - lastOk <= TimeUnit.MINUTES.toMillis(5) -> "Online"
        snapshot.lastFailureAtMs != null -> "Error"
        else -> "Checking"
    }

    val hostLabel = snapshot.baseUrl
        .removePrefix("http://")
        .removePrefix("https://")
        .trim()
        .ifBlank { "API" }
        .removeSuffix("/")

    AssistChip(
        onClick = { showDialog = true },
        label = { Text("$hostLabel • $statusLabel • OK $lastOkText") },
        leadingIcon = {
            Icon(leadingIcon, contentDescription = null)
        },
        trailingIcon = {
            Icon(Icons.Filled.Settings, contentDescription = null)
        },
        colors = AssistChipDefaults.assistChipColors(
            containerColor = container,
            labelColor = content,
            leadingIconContentColor = content,
            trailingIconContentColor = content
        ),
        modifier = Modifier.semantics {
            contentDescription = "Connection status. $hostLabel. $statusLabel. Last OK $lastOkText. Tap to open settings."
        }
    )

    if (showDialog) {
        ConnectionSettingsDialog(onDismiss = { showDialog = false })
    }
}

private fun formatRelativeTime(deltaMs: Long): String {
    if (deltaMs < 0) return "now"
    val seconds = TimeUnit.MILLISECONDS.toSeconds(deltaMs)
    if (seconds < 10) return "now"
    if (seconds < 60) return "${seconds}s ago"
    val minutes = TimeUnit.MILLISECONDS.toMinutes(deltaMs)
    if (minutes < 60) return "${minutes}m ago"
    val hours = TimeUnit.MILLISECONDS.toHours(deltaMs)
    if (hours < 48) return "${hours}h ago"
    val days = TimeUnit.MILLISECONDS.toDays(deltaMs)
    return "${days}d ago"
}