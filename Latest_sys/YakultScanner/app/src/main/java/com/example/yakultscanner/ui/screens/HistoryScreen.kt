package com.example.yakultscanner.ui.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
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
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.FilterList
import androidx.compose.material.icons.filled.Image
import androidx.compose.material.icons.filled.PushPin
import androidx.compose.material.icons.filled.Star
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material.icons.outlined.StarBorder
import androidx.compose.material3.AssistChip
import androidx.compose.material3.AssistChipDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.FilterChip
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalLifecycleOwner
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import androidx.navigation.NavController
import com.example.yakultscanner.PinnedSetsStore
import com.example.yakultscanner.ScanHistoryEntry
import com.example.yakultscanner.api.ApiClient
import com.example.yakultscanner.api.ApiResult
import com.example.yakultscanner.api.safeApiCall
import com.example.yakultscanner.loadScanHistory
import com.example.yakultscanner.ui.components.ApiFailurePanel
import com.example.yakultscanner.updateScanHistoryStatuses
import kotlinx.coroutines.launch
import java.net.URLEncoder
import java.nio.charset.StandardCharsets
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

@androidx.compose.material3.ExperimentalMaterial3Api
@Composable
fun HistoryScreen(navController: NavController) {
    val context = LocalContext.current
    val lifecycleOwner = LocalLifecycleOwner.current
    val scope = rememberCoroutineScope()
    var history by remember { mutableStateOf(loadScanHistory(context)) }
    var query by remember { mutableStateOf("") }
    var selectedDateFilter by remember { mutableStateOf(HistoryDateFilter.All) }
    var selectedStatusFilter by remember { mutableStateOf(HistoryStatusFilter.All) }
    var pendingIssuesBySetCode by remember { mutableStateOf<Map<String, Int>>(emptyMap()) }
    var pendingIssuesErrorUi by remember { mutableStateOf<Pair<String, String>?>(null) }
    var pinnedSetCodes by remember { mutableStateOf(PinnedSetsStore.load(context)) }
    var showAdvancedFilters by remember { mutableStateOf(false) }

    suspend fun refreshHistoryStatuses() {
        val currentHistory = loadScanHistory(context)
        if (currentHistory.isEmpty()) {
            history = currentHistory
            return
        }

        val setCodes = currentHistory
            .mapNotNull { it.setCode?.trim()?.takeIf(String::isNotEmpty) }
            .distinctBy { it.uppercase(Locale.getDefault()) }

        if (setCodes.isEmpty()) {
            history = currentHistory
            return
        }

        when (val result = safeApiCall { ApiClient.service.getSetStatuses(setCodes) }) {
            is ApiResult.Success -> {
                val statusesBySetCode = result.data
                    .mapNotNull { row ->
                        val setCode = row.setCode?.trim()?.takeIf(String::isNotEmpty)
                        val status = row.status?.trim()?.takeIf(String::isNotEmpty)
                        if (setCode == null || status == null) null else setCode to status
                    }
                    .toMap()
                if (statusesBySetCode.isNotEmpty()) {
                    updateScanHistoryStatuses(context, statusesBySetCode)
                }
                history = loadScanHistory(context)
            }
            else -> {
                history = currentHistory
            }
        }
    }

    suspend fun refreshPendingIssueBadges() {
        when (val result = safeApiCall { ApiClient.service.getPendingUpdates() }) {
            is ApiResult.Success -> {
                pendingIssuesBySetCode = result.data
                    .mapNotNull { it.setCode?.trim()?.takeIf(String::isNotEmpty) }
                    .groupingBy { it.uppercase(Locale.getDefault()) }
                    .eachCount()
                pendingIssuesErrorUi = null
            }
            is ApiResult.HttpError -> {
                pendingIssuesBySetCode = emptyMap()
                pendingIssuesErrorUi = buildApiError(
                    "Couldn't load pending issue counts.",
                    result.endpoint,
                    "HTTP: ${result.code}",
                    result.message
                )
            }
            is ApiResult.NetworkError -> {
                pendingIssuesBySetCode = emptyMap()
                pendingIssuesErrorUi = buildApiError(
                    "Network issue while loading issue counts.",
                    result.endpoint,
                    "Type: Network error",
                    result.message
                )
            }
            is ApiResult.UnknownError -> {
                pendingIssuesBySetCode = emptyMap()
                pendingIssuesErrorUi = buildApiError(
                    "Unexpected error while loading issue counts.",
                    result.endpoint,
                    "Type: Unexpected error",
                    result.message
                )
            }
        }
    }

    DisposableEffect(context, lifecycleOwner) {
        val observer = LifecycleEventObserver { _, event ->
            if (event == Lifecycle.Event.ON_RESUME) {
                history = loadScanHistory(context)
                scope.launch {
                    refreshPendingIssueBadges()
                    refreshHistoryStatuses()
                }
            }
        }
        lifecycleOwner.lifecycle.addObserver(observer)
        onDispose { lifecycleOwner.lifecycle.removeObserver(observer) }
    }

    LaunchedEffect(Unit) {
        refreshPendingIssueBadges()
        refreshHistoryStatuses()
    }

    val filteredHistory = remember(history, query, selectedDateFilter, selectedStatusFilter) {
        val trimmed = query.trim()
        val q = trimmed.lowercase(Locale.getDefault())
        val now = System.currentTimeMillis()
        val dayKeyFormat = SimpleDateFormat("yyyyMMdd", Locale.getDefault())
        val todayKey = dayKeyFormat.format(Date(now))

        val dateFiltered = when (selectedDateFilter) {
            HistoryDateFilter.Today -> history.filter { dayKeyFormat.format(Date(it.timestamp)) == todayKey }
            HistoryDateFilter.ThisWeek -> {
                val cutoff = now - (7L * 24L * 60L * 60L * 1000L)
                history.filter { it.timestamp >= cutoff }
            }
            HistoryDateFilter.All -> history
        }

        val searched = if (trimmed.isEmpty()) {
            dateFiltered
        } else {
            dateFiltered.filter { entry ->
                val setCode = entry.setCode ?: ""
                val employee = entry.employee ?: ""
                val status = entry.status ?: ""
                val raw = entry.rawJson
                setCode.lowercase(Locale.getDefault()).contains(q) ||
                    employee.lowercase(Locale.getDefault()).contains(q) ||
                    status.lowercase(Locale.getDefault()).contains(q) ||
                    raw.lowercase(Locale.getDefault()).contains(q)
            }
        }

        if (selectedStatusFilter == HistoryStatusFilter.All) {
            searched
        } else {
            searched.filter { entry -> toStatusFilter(entry.status) == selectedStatusFilter }
        }
    }

    val pinnedEntries = filteredHistory
        .filter { entry ->
            val normalized = normalizeSetCode(entry.setCode)
            normalized != null && pinnedSetCodes.contains(normalized)
        }
        .distinctBy { normalizeSetCode(it.setCode) }
    val otherEntries = filteredHistory.filterNot { entry ->
        val normalized = normalizeSetCode(entry.setCode)
        normalized != null && pinnedSetCodes.contains(normalized)
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Scan History") },
                navigationIcon = {
                    IconButton(onClick = { navController.popBackStack() }) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back")
                    }
                }
            )
        }
    ) { paddingValues ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
                .padding(horizontal = 16.dp, vertical = 12.dp)
        ) {
            Surface(
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(18.dp),
                color = com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Surface
            ) {
                Column(
                    modifier = Modifier.padding(14.dp),
                    verticalArrangement = Arrangement.spacedBy(12.dp)
                ) {
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Column(verticalArrangement = Arrangement.spacedBy(2.dp)) {
                            Text(
                                text = "History",
                                style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold)
                            )
                            Text(
                                text = if (filteredHistory.isEmpty()) "No matching scans" else "${filteredHistory.size} scan records",
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                        }
                        AssistChip(
                            onClick = { showAdvancedFilters = !showAdvancedFilters },
                            label = { Text(if (showAdvancedFilters) "Hide Filters" else "Filters") },
                            leadingIcon = {
                                Icon(
                                    imageVector = Icons.Filled.FilterList,
                                    contentDescription = null,
                                    modifier = Modifier.size(18.dp)
                                )
                            },
                            colors = AssistChipDefaults.assistChipColors()
                        )
                    }

                    OutlinedTextField(
                        value = query,
                        onValueChange = { query = it },
                        modifier = Modifier.fillMaxWidth(),
                        label = { Text("Search set, employee, or status") },
                        singleLine = true
                    )

                    if (showAdvancedFilters) {
                        Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                            Text(
                                text = "Date",
                                style = MaterialTheme.typography.labelLarge,
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                            Row(
                                modifier = Modifier.fillMaxWidth().horizontalScroll(rememberScrollState()),
                                horizontalArrangement = Arrangement.spacedBy(8.dp)
                            ) {
                                FilterChip(selected = selectedDateFilter == HistoryDateFilter.All, onClick = { selectedDateFilter = HistoryDateFilter.All }, label = { Text("All") })
                                FilterChip(selected = selectedDateFilter == HistoryDateFilter.Today, onClick = { selectedDateFilter = HistoryDateFilter.Today }, label = { Text("Today") })
                                FilterChip(selected = selectedDateFilter == HistoryDateFilter.ThisWeek, onClick = { selectedDateFilter = HistoryDateFilter.ThisWeek }, label = { Text("This week") })
                            }

                            Text(
                                text = "Status",
                                style = MaterialTheme.typography.labelLarge,
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                            Row(
                                modifier = Modifier.fillMaxWidth().horizontalScroll(rememberScrollState()),
                                horizontalArrangement = Arrangement.spacedBy(8.dp)
                            ) {
                                FilterChip(selected = selectedStatusFilter == HistoryStatusFilter.All, onClick = { selectedStatusFilter = HistoryStatusFilter.All }, label = { Text("All") })
                                FilterChip(selected = selectedStatusFilter == HistoryStatusFilter.Pending, onClick = { selectedStatusFilter = HistoryStatusFilter.Pending }, label = { Text("Pending") })
                                FilterChip(selected = selectedStatusFilter == HistoryStatusFilter.Dispatched, onClick = { selectedStatusFilter = HistoryStatusFilter.Dispatched }, label = { Text("Dispatched") })
                                FilterChip(selected = selectedStatusFilter == HistoryStatusFilter.Other, onClick = { selectedStatusFilter = HistoryStatusFilter.Other }, label = { Text("Other") })
                            }
                        }
                    }
                }
            }

            Spacer(modifier = Modifier.height(12.dp))

            if (pendingIssuesErrorUi != null) {
                ApiFailurePanel(
                    message = pendingIssuesErrorUi!!.first,
                    details = pendingIssuesErrorUi!!.second,
                    retryLabel = "Retry",
                    onRetry = { scope.launch { refreshPendingIssueBadges() } }
                )
                Spacer(modifier = Modifier.height(12.dp))
            }

            if (filteredHistory.isEmpty()) {
                Box(
                    modifier = Modifier.fillMaxSize(),
                    contentAlignment = Alignment.Center
                ) {
                    Column(
                        horizontalAlignment = Alignment.CenterHorizontally,
                        verticalArrangement = Arrangement.spacedBy(8.dp)
                    ) {
                        Text(
                            text = if (query.isBlank()) "No scans for this filter yet" else "No results for \"$query\"",
                            style = MaterialTheme.typography.titleMedium,
                            textAlign = TextAlign.Center
                        )
                        Text(
                            text = "Try changing the filters or start a new scan from the scanner workspace.",
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                            textAlign = TextAlign.Center
                        )
                    }
                }
            } else {
                LazyColumn(
                    modifier = Modifier.fillMaxSize(),
                    verticalArrangement = Arrangement.spacedBy(12.dp)
                ) {
                    if (pinnedEntries.isNotEmpty()) {
                        item {
                            Row(
                                verticalAlignment = Alignment.CenterVertically,
                                horizontalArrangement = Arrangement.spacedBy(8.dp)
                            ) {
                                Icon(
                                    imageVector = Icons.Filled.PushPin,
                                    contentDescription = null,
                                    tint = MaterialTheme.colorScheme.primary,
                                    modifier = Modifier.size(18.dp)
                                )
                                Text(
                                    text = "Pinned",
                                    style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.Bold),
                                    color = MaterialTheme.colorScheme.onSurface
                                )
                            }
                        }
                        items(pinnedEntries) { entry ->
                            val normalized = normalizeSetCode(entry.setCode)
                            HistoryEntryCard(
                                entry = entry,
                                pinned = normalized != null && pinnedSetCodes.contains(normalized),
                                pendingIssueCount = normalized?.let { pendingIssuesBySetCode[it] } ?: 0,
                                onTogglePin = { pinnedSetCodes = PinnedSetsStore.toggle(context, entry.setCode ?: "") },
                                onOpen = {
                                    val encoded = URLEncoder.encode(entry.rawJson, StandardCharsets.UTF_8.toString())
                                    navController.navigate("details/$encoded")
                                }
                            )
                        }
                    }

                    if (otherEntries.isNotEmpty()) {
                        if (pinnedEntries.isNotEmpty()) {
                            item {
                                Text(
                                    text = "All Results",
                                    style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.Bold),
                                    color = MaterialTheme.colorScheme.onSurface
                                )
                            }
                        }
                        items(otherEntries) { entry ->
                            val normalized = normalizeSetCode(entry.setCode)
                            HistoryEntryCard(
                                entry = entry,
                                pinned = normalized != null && pinnedSetCodes.contains(normalized),
                                pendingIssueCount = normalized?.let { pendingIssuesBySetCode[it] } ?: 0,
                                onTogglePin = { pinnedSetCodes = PinnedSetsStore.toggle(context, entry.setCode ?: "") },
                                onOpen = {
                                    val encoded = URLEncoder.encode(entry.rawJson, StandardCharsets.UTF_8.toString())
                                    navController.navigate("details/$encoded")
                                }
                            )
                        }
                    }

                    item { Spacer(modifier = Modifier.height(24.dp)) }
                }
            }
        }
    }
}

private enum class HistoryDateFilter { All, Today, ThisWeek }
private enum class HistoryStatusFilter { All, Pending, Dispatched, Other }

private fun normalizeSetCode(value: String?): String? {
    val v = value?.trim()?.uppercase(Locale.getDefault())
    return v?.takeIf { it.isNotBlank() }
}

private fun toStatusFilter(value: String?): HistoryStatusFilter {
    val v = value?.trim().orEmpty()
    return when {
        v.contains("dispatch", ignoreCase = true) -> HistoryStatusFilter.Dispatched
        v.contains("pending", ignoreCase = true) -> HistoryStatusFilter.Pending
        v.isBlank() -> HistoryStatusFilter.Other
        else -> HistoryStatusFilter.Other
    }
}

@Composable
private fun HistoryEntryCard(
    entry: ScanHistoryEntry,
    pinned: Boolean,
    pendingIssueCount: Int,
    onTogglePin: () -> Unit,
    onOpen: () -> Unit
) {
    val formattedDate = remember(entry.timestamp) {
        SimpleDateFormat("yyyy-MM-dd HH:mm", Locale.getDefault()).format(Date(entry.timestamp))
    }
    val statusText = entry.status ?: "No status"
    val statusColor = when {
        statusText.contains("dispatch", ignoreCase = true) -> Color(0xFF2E7D32)
        statusText.contains("pending", ignoreCase = true) -> Color(0xFFF57C00)
        else -> MaterialTheme.colorScheme.error
    }

    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(16.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        elevation = CardDefaults.cardElevation(defaultElevation = 2.dp),
        onClick = onOpen
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(10.dp)
            ) {
                Box(
                    modifier = Modifier
                        .size(10.dp)
                        .clip(CircleShape)
                        .background(statusColor)
                )
                Text(
                    text = entry.setCode ?: "Unknown Set",
                    style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                    modifier = Modifier.weight(1f)
                )
                IconButton(onClick = onTogglePin) {
                    Icon(
                        imageVector = if (pinned) Icons.Filled.Star else Icons.Outlined.StarBorder,
                        contentDescription = if (pinned) "Unpin set" else "Pin set",
                        tint = MaterialTheme.colorScheme.primary
                    )
                }
            }

            Text(
                text = entry.employee ?: "Unknown Employee",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurface
            )

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(8.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                Surface(
                    color = MaterialTheme.colorScheme.surfaceVariant,
                    shape = RoundedCornerShape(50)
                ) {
                    Text(
                        text = statusText,
                        modifier = Modifier.padding(horizontal = 10.dp, vertical = 4.dp),
                        style = MaterialTheme.typography.labelMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
                Text(
                    text = formattedDate,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }

            if (pendingIssueCount > 0) {
                Surface(
                    color = MaterialTheme.colorScheme.errorContainer,
                    shape = RoundedCornerShape(50),
                    border = androidx.compose.foundation.BorderStroke(1.dp, MaterialTheme.colorScheme.error.copy(alpha = 0.4f))
                ) {
                    Row(
                        modifier = Modifier.padding(horizontal = 10.dp, vertical = 4.dp),
                        horizontalArrangement = Arrangement.spacedBy(6.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Icon(
                            imageVector = Icons.Filled.Warning,
                            contentDescription = null,
                            tint = MaterialTheme.colorScheme.error,
                            modifier = Modifier.size(14.dp)
                        )
                        Text(
                            text = if (pendingIssueCount == 1) "1 issue pending" else "$pendingIssueCount issues pending",
                            style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.Bold),
                            color = MaterialTheme.colorScheme.error
                        )
                    }
                }
            }

            Text(
                text = "Tap to re-open this scan",
                style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
        }
    }
}