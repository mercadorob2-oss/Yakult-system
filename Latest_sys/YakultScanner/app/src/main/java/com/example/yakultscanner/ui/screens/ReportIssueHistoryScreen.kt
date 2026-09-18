package com.example.yakultscanner.ui.screens

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.navigation.NavController
import com.example.yakultscanner.api.ApiClient
import com.example.yakultscanner.api.ApiResult
import com.example.yakultscanner.api.safeApiCall
import com.example.yakultscanner.ui.components.ApiFailurePanel
import com.example.yakultscanner.ui.components.ListLoadingSkeleton
import kotlinx.coroutines.launch
import java.util.Locale

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ReportIssueHistoryScreen(navController: NavController) {
    val scope = rememberCoroutineScope()
    var query by remember { mutableStateOf("") }
    var isLoading by remember { mutableStateOf(true) }
    var errorUi by remember { mutableStateOf<Pair<String, String>?>(null) }
    var issues by remember { mutableStateOf<List<PendingSetIssue>>(emptyList()) }

    suspend fun refresh() {
        isLoading = true
        errorUi = null
        when (val result = safeApiCall { ApiClient.service.getPendingUpdates() }) {
            is ApiResult.Success -> {
                val grouped = result.data
                    .filter { !it.setCode.isNullOrBlank() }
                    .groupBy { it.setCode!!.trim().uppercase(Locale.getDefault()) }

                issues = grouped.entries
                    .map { (setCode, updates) ->
                        PendingSetIssue(
                            setCode = setCode,
                            pendingCount = updates.size,
                            lastCreatedAt = updates.maxOfOrNull { it.createdAt ?: "" }?.ifBlank { null }
                        )
                    }
                    .sortedWith(
                        compareByDescending<PendingSetIssue> { it.lastCreatedAt ?: "" }
                            .thenByDescending { it.pendingCount }
                    )
            }
            is ApiResult.HttpError -> {
                val details = buildString {
                    appendLine("Endpoint: ${result.endpoint ?: "(unknown)"}")
                    appendLine("HTTP: ${result.code}")
                    if (!result.message.isNullOrBlank()) append("Message: ${result.message}")
                }.trim()
                errorUi = "Couldn't load pending issues." to details
            }
            is ApiResult.NetworkError -> {
                val details = buildString {
                    appendLine("Endpoint: ${result.endpoint ?: "(unknown)"}")
                    appendLine("Type: Network error")
                    if (!result.message.isNullOrBlank()) append("Message: ${result.message}")
                }.trim()
                errorUi = "Network issue while loading issues." to details
            }
            is ApiResult.UnknownError -> {
                val details = buildString {
                    appendLine("Endpoint: ${result.endpoint ?: "(unknown)"}")
                    appendLine("Type: Unexpected error")
                    if (!result.message.isNullOrBlank()) append("Message: ${result.message}")
                }.trim()
                errorUi = "Unexpected error while loading issues." to details
            }
        }
        isLoading = false
    }

    LaunchedEffect(Unit) {
        refresh()
    }

    val filteredIssues = remember(issues, query) {
        val trimmed = query.trim()
        if (trimmed.isBlank()) {
            issues
        } else {
            val q = trimmed.lowercase(Locale.getDefault())
            issues.filter { it.setCode.lowercase(Locale.getDefault()).contains(q) }
        }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Pending Issues") },
                navigationIcon = {
                    IconButton(onClick = { navController.popBackStack() }) {
                        Icon(
                            imageVector = Icons.AutoMirrored.Filled.ArrowBack,
                            contentDescription = "Back"
                        )
                    }
                },
                actions = {
                    IconButton(onClick = { scope.launch { refresh() } }, enabled = !isLoading) {
                        Icon(Icons.Filled.Refresh, contentDescription = "Refresh")
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = MaterialTheme.colorScheme.surface
                )
            )
        }
    ) { paddingValues ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
                .padding(16.dp)
        ) {
            OutlinedTextField(
                value = query,
                onValueChange = { query = it },
                modifier = Modifier.fillMaxWidth(),
                label = { Text("Search by set code") },
                singleLine = true
            )

            Spacer(modifier = Modifier.height(12.dp))

            when {
                isLoading -> {
                    ListLoadingSkeleton(modifier = Modifier.fillMaxSize())
                }

                errorUi != null -> {
                    ApiFailurePanel(
                        message = errorUi!!.first,
                        details = errorUi!!.second,
                        retryLabel = "Retry",
                        onRetry = { scope.launch { refresh() } }
                    )
                }

                filteredIssues.isEmpty() -> {
                    Box(modifier = Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                        Text(
                            text = if (query.isBlank()) "No pending issues" else "No results for \"$query\"",
                            textAlign = TextAlign.Center
                        )
                    }
                }

                else -> {
                    LazyColumn(
                        modifier = Modifier.fillMaxSize(),
                        verticalArrangement = Arrangement.spacedBy(12.dp)
                    ) {
                        items(filteredIssues.size) { index ->
                            val issue = filteredIssues[index]
                            Card(
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .clip(RoundedCornerShape(12.dp)),
                                onClick = { navController.navigate("uploaded/${issue.setCode}") }
                            ) {
                                Row(
                                    modifier = Modifier
                                        .fillMaxWidth()
                                        .padding(16.dp),
                                    verticalAlignment = Alignment.CenterVertically,
                                    horizontalArrangement = Arrangement.spacedBy(12.dp)
                                ) {
                                    Icon(
                                        imageVector = Icons.Filled.Warning,
                                        contentDescription = null,
                                        tint = MaterialTheme.colorScheme.error
                                    )
                                    Column(modifier = Modifier.weight(1f)) {
                                        Text(
                                            text = issue.setCode,
                                            style = MaterialTheme.typography.titleMedium
                                        )
                                        Text(
                                            text = "${issue.pendingCount} issue(s) pending",
                                            style = MaterialTheme.typography.bodyMedium,
                                            color = MaterialTheme.colorScheme.onSurfaceVariant
                                        )
                                        if (!issue.lastCreatedAt.isNullOrBlank()) {
                                            Text(
                                                text = "Last upload: ${issue.lastCreatedAt}",
                                                style = MaterialTheme.typography.bodySmall,
                                                color = MaterialTheme.colorScheme.onSurfaceVariant
                                            )
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}

private data class PendingSetIssue(
    val setCode: String,
    val pendingCount: Int,
    val lastCreatedAt: String?
)
