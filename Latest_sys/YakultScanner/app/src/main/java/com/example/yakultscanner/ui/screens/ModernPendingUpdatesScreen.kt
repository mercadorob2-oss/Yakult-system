package com.example.yakultscanner.ui.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.CloudUpload
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.PictureAsPdf
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.FilterChip
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import kotlinx.coroutines.launch
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.navigation.NavController
import com.example.yakultscanner.data.db.PendingUpdateEntity
import com.example.yakultscanner.data.db.SyncStatus
import com.example.yakultscanner.data.repository.SyncResult
import com.example.yakultscanner.ui.components.ScannerMetricChip
import com.example.yakultscanner.ui.components.ScannerSectionHeader
import com.example.yakultscanner.ui.components.ScannerStatusChip
import com.example.yakultscanner.ui.components.ScannerSurfaceCard
import com.example.yakultscanner.ui.components.ScannerWorkspaceHeader
import com.example.yakultscanner.ui.components.ScannerWorkspaceUi
import com.example.yakultscanner.utils.generatePendingUpdatesPdf

private enum class SyncCenterFilter(val label: String) {
    All("All"), NeedsAttention("Needs attention"), Failed("Failed"), Pending("Pending")
}

@OptIn(androidx.compose.material3.ExperimentalMaterial3Api::class)
@Composable
fun ModernPendingUpdatesScreen(
    navController: NavController,
    viewModel: PendingUpdatesViewModel = androidx.hilt.lifecycle.viewmodel.compose.hiltViewModel()
) {
    val updates by viewModel.pendingUpdates.collectAsState()
    val loading by viewModel.isLoading.collectAsState()
    val syncing by viewModel.isSyncing.collectAsState()
    val syncResult by viewModel.syncResult.collectAsState()
    val errorMessage by viewModel.errorMessage.collectAsState()
    val context = androidx.compose.ui.platform.LocalContext.current
    val snackbarHostState = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()
    var selectedFilter by remember { mutableStateOf(SyncCenterFilter.All) }
    var deleteTarget by remember { mutableStateOf<PendingUpdateEntity?>(null) }

    LaunchedEffect(Unit) { viewModel.loadPendingUpdates() }
    LaunchedEffect(syncResult) {
        syncResult?.let { result ->
            val message = when (result) {
                is SyncResult.Success -> "Uploaded ${result.uploadedCount} update(s)."
                is SyncResult.PartialSuccess -> "Uploaded ${result.uploadedCount}; ${result.failedCount} failed."
                is SyncResult.Error -> result.message
                SyncResult.NoPendingUpdates -> "There are no updates waiting to sync."
                SyncResult.NetworkUnavailable -> "No network connection. Your local updates are safe."
            }
            snackbarHostState.showSnackbar(message)
        }
    }

    val failedCount = updates.count { it.syncStatus == SyncStatus.FAILED }
    val syncingCount = updates.count { it.syncStatus == SyncStatus.SYNCING }
    val pendingCount = updates.count { it.syncStatus == SyncStatus.PENDING }
    val filtered = when (selectedFilter) {
        SyncCenterFilter.All -> updates
        SyncCenterFilter.NeedsAttention -> updates.filter { it.syncStatus == SyncStatus.FAILED || it.lastError?.isNotBlank() == true }
        SyncCenterFilter.Failed -> updates.filter { it.syncStatus == SyncStatus.FAILED }
        SyncCenterFilter.Pending -> updates.filter { it.syncStatus == SyncStatus.PENDING || it.syncStatus == SyncStatus.SYNCING }
    }

    Scaffold(
        containerColor = ScannerWorkspaceUi.Canvas,
        topBar = {
            TopAppBar(
                title = { Text("Sync Center", color = ScannerWorkspaceUi.Ink, fontWeight = FontWeight.Bold) },
                navigationIcon = { IconButton(onClick = { navController.popBackStack() }) { Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back", tint = ScannerWorkspaceUi.Ink) } },
                actions = {
                    IconButton(onClick = { viewModel.loadPendingUpdates() }, enabled = !loading && !syncing) { Icon(Icons.Filled.Refresh, contentDescription = "Refresh sync center", tint = ScannerWorkspaceUi.Ink) }
                    IconButton(onClick = { generatePendingUpdatesPdf(context, updates) }, enabled = updates.isNotEmpty()) { Icon(Icons.Filled.PictureAsPdf, contentDescription = "Export pending updates", tint = ScannerWorkspaceUi.Ink) }
                },
                colors = TopAppBarDefaults.topAppBarColors(containerColor = ScannerWorkspaceUi.Surface)
            )
        },
        snackbarHost = { SnackbarHost(snackbarHostState, modifier = Modifier) }
    ) { padding ->
        LazyColumn(
            modifier = Modifier.fillMaxSize().background(ScannerWorkspaceUi.Canvas).padding(padding),
            contentPadding = PaddingValues(horizontal = 16.dp, vertical = 14.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            item {
                ScannerWorkspaceHeader(
                    title = "Offline updates",
                    subtitle = "Review what is waiting, retry failures, and keep field work moving without losing local changes.",
                    trailing = { ScannerMetricChip("Waiting", updates.size.toString()) }
                )
            }
            item {
                Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    ScannerMetricChip("Pending", pendingCount.toString(), Modifier.weight(1f))
                    ScannerMetricChip("Syncing", syncingCount.toString(), Modifier.weight(1f))
                    ScannerMetricChip("Failed", failedCount.toString(), Modifier.weight(1f))
                }
            }
            item {
                ScannerSurfaceCard {
                    Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
                        ScannerSectionHeader("Sync actions", "Uploads are grouped by set and remain local when the network is unavailable.", Modifier.weight(1f))
                        if (syncing) CircularProgressIndicator(modifier = Modifier.size(22.dp), strokeWidth = 2.dp)
                    }
                    Button(onClick = { viewModel.syncNow() }, enabled = !syncing && updates.isNotEmpty(), modifier = Modifier.fillMaxWidth().height(50.dp), shape = RoundedCornerShape(16.dp)) {
                        Icon(Icons.Filled.CloudUpload, contentDescription = null)
                        Spacer(Modifier.width(8.dp))
                        Text(if (failedCount > 0) "Retry sync (${updates.size})" else "Sync now (${updates.size})", fontWeight = FontWeight.Bold)
                    }
                    syncResult?.let { result ->
                        val (text, color) = when (result) {
                            is SyncResult.Success -> "Last sync completed successfully." to ScannerWorkspaceUi.Success
                            is SyncResult.PartialSuccess -> "Some updates need another attempt." to ScannerWorkspaceUi.Warning
                            is SyncResult.NetworkUnavailable -> "Offline: updates remain safely stored." to ScannerWorkspaceUi.Warning
                            is SyncResult.Error -> "Sync needs attention." to ScannerWorkspaceUi.Error
                            SyncResult.NoPendingUpdates -> "No updates were waiting." to ScannerWorkspaceUi.Muted
                        }
                        Surface(modifier = Modifier.fillMaxWidth(), shape = RoundedCornerShape(12.dp), color = color.copy(alpha = 0.10f)) { Text(text, modifier = Modifier.padding(12.dp), style = MaterialTheme.typography.bodySmall, color = color, fontWeight = FontWeight.SemiBold) }
                    }
                }
            }
            item {
                Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    SyncCenterFilter.entries.forEach { filter ->
                        FilterChip(selected = selectedFilter == filter, onClick = { selectedFilter = filter }, label = { Text(filter.label) })
                    }
                }
            }
            errorMessage?.let { message -> item { Surface(modifier = Modifier.fillMaxWidth(), shape = RoundedCornerShape(14.dp), color = ScannerWorkspaceUi.Error.copy(alpha = 0.10f)) { Text(message, modifier = Modifier.padding(12.dp), style = MaterialTheme.typography.bodySmall, color = ScannerWorkspaceUi.Error) } } }
            if (loading) {
                item { ScannerSurfaceCard { Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(10.dp)) { CircularProgressIndicator(modifier = Modifier.size(20.dp), strokeWidth = 2.dp); Text("Loading local updates…", color = ScannerWorkspaceUi.Muted) } } }
            } else if (filtered.isEmpty()) {
                item { ScannerSurfaceCard { Text(if (updates.isEmpty()) "Everything is synced" else "No updates in this view", style = MaterialTheme.typography.titleMedium, color = ScannerWorkspaceUi.Ink, fontWeight = FontWeight.Bold); Text(if (updates.isEmpty()) "New offline changes will appear here until they reach the server." else "Try another status filter.", style = MaterialTheme.typography.bodySmall, color = ScannerWorkspaceUi.Muted) } }
            } else {
                items(filtered, key = { it.id }) { update -> ModernPendingUpdateCard(update, onDelete = { deleteTarget = update }) }
            }
            item { Spacer(Modifier.height(24.dp)) }
        }
    }

    deleteTarget?.let { update ->
        AlertDialog(
            onDismissRequest = { deleteTarget = null },
            title = { Text("Delete local update?") },
            text = { Text("This removes the unsynced update for ${update.serialNumber}. It cannot be uploaded afterward.") },
            confirmButton = { TextButton(onClick = { viewModel.deletePendingUpdate(update.id); deleteTarget = null; scope.launch { snackbarHostState.showSnackbar("Local update deleted.") } }) { Text("Delete", color = ScannerWorkspaceUi.Error) } },
            dismissButton = { TextButton(onClick = { deleteTarget = null }) { Text("Keep") } }
        )
    }
}

@Composable
private fun ModernPendingUpdateCard(update: PendingUpdateEntity, onDelete: () -> Unit) {
    val status = when (update.syncStatus) {
        SyncStatus.PENDING -> "Pending"
        SyncStatus.SYNCING -> "Syncing"
        SyncStatus.FAILED -> "Failed"
        SyncStatus.SYNCED -> "Synced"
    }
    ScannerSurfaceCard {
        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.Top) {
            Column(modifier = Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(3.dp)) {
                Text(update.setCode, style = MaterialTheme.typography.titleMedium, color = ScannerWorkspaceUi.Ink, fontWeight = FontWeight.Bold)
                Text("${update.itemType ?: "Item"} · ${update.serialNumber}", style = MaterialTheme.typography.bodySmall, color = ScannerWorkspaceUi.Muted, maxLines = 1, overflow = TextOverflow.Ellipsis)
            }
            ScannerStatusChip(status)
        }
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp), verticalAlignment = Alignment.CenterVertically) {
            Text(update.newStatus, style = MaterialTheme.typography.labelMedium, color = ScannerWorkspaceUi.Ink, fontWeight = FontWeight.SemiBold)
            update.retryCount.takeIf { it > 0 }?.let { Text("$it retries", style = MaterialTheme.typography.labelSmall, color = ScannerWorkspaceUi.Warning) }
        }
        update.remark?.takeIf { it.isNotBlank() }?.let { Text(it, style = MaterialTheme.typography.bodySmall, color = ScannerWorkspaceUi.Muted, maxLines = 2, overflow = TextOverflow.Ellipsis) }
        update.lastError?.takeIf { it.isNotBlank() }?.let { error -> Surface(modifier = Modifier.fillMaxWidth(), shape = RoundedCornerShape(10.dp), color = ScannerWorkspaceUi.Error.copy(alpha = 0.09f)) { Text("Error: $error", modifier = Modifier.padding(10.dp), style = MaterialTheme.typography.bodySmall, color = ScannerWorkspaceUi.Error) } }
        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
            Text("Created ${java.text.SimpleDateFormat("MMM d, h:mm a", java.util.Locale.getDefault()).format(java.util.Date(update.createdAt))}", style = MaterialTheme.typography.labelSmall, color = ScannerWorkspaceUi.Muted)
            IconButton(onClick = onDelete) { Icon(Icons.Filled.Delete, contentDescription = "Delete local update", tint = ScannerWorkspaceUi.Error) }
        }
    }
}