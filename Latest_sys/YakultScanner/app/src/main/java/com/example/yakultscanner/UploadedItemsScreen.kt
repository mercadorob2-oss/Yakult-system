package com.example.yakultscanner

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.yakultscanner.api.ApiClient
import com.example.yakultscanner.api.ApiResult
import com.example.yakultscanner.api.SetItemUpdateDto
import com.example.yakultscanner.api.safeApiCall
import com.example.yakultscanner.ui.components.ApiFailurePanel
import com.example.yakultscanner.ui.components.ListLoadingSkeleton
import kotlinx.coroutines.launch

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun UploadedItemsScreen(setCode: String, onNavigateBack: () -> Unit) {
    val scope = rememberCoroutineScope()
    var isLoading by remember { mutableStateOf(true) }
    var errorUi by remember { mutableStateOf<Pair<String, String>?>(null) }
    var items by remember { mutableStateOf<List<SetItemUpdateDto>>(emptyList()) }

    suspend fun refresh() {
        isLoading = true
        errorUi = null
        when (val result = safeApiCall { ApiClient.service.getSetUpdates(setCode) }) {
            is ApiResult.Success -> {
                items = result.data
            }
            is ApiResult.HttpError -> {
                val details = buildString {
                    appendLine("Endpoint: ${result.endpoint ?: "(unknown)"}")
                    appendLine("HTTP: ${result.code}")
                    if (!result.message.isNullOrBlank()) append("Message: ${result.message}")
                }.trim()
                errorUi = "Couldn't load uploaded items." to details
            }
            is ApiResult.NetworkError -> {
                val details = buildString {
                    appendLine("Endpoint: ${result.endpoint ?: "(unknown)"}")
                    appendLine("Type: Network error")
                    if (!result.message.isNullOrBlank()) append("Message: ${result.message}")
                }.trim()
                errorUi = "Network issue while loading uploads." to details
            }
            is ApiResult.UnknownError -> {
                val details = buildString {
                    appendLine("Endpoint: ${result.endpoint ?: "(unknown)"}")
                    appendLine("Type: Unexpected error")
                    if (!result.message.isNullOrBlank()) append("Message: ${result.message}")
                }.trim()
                errorUi = "Unexpected error while loading uploads." to details
            }
        }
        isLoading = false
    }

    LaunchedEffect(setCode) {
        refresh()
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Uploaded Items for $setCode") },
                navigationIcon = {
                    IconButton(onClick = onNavigateBack) {
                        Icon(
                            imageVector = Icons.AutoMirrored.Filled.ArrowBack,
                            contentDescription = "Back"
                        )
                    }
                },
            )
        }
    ) { paddingValues ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            if (errorUi != null) {
                ApiFailurePanel(
                    message = errorUi!!.first,
                    details = errorUi!!.second,
                    retryLabel = "Retry",
                    onRetry = { scope.launch { refresh() } },
                    modifier = Modifier.padding(horizontal = 16.dp)
                )
            }

            when {
                isLoading -> {
                    ListLoadingSkeleton(modifier = Modifier.fillMaxSize().padding(16.dp))
                }

                items.isEmpty() && errorUi == null -> {
                    Text(
                        text = "No uploaded items for this set yet.",
                        textAlign = TextAlign.Center,
                        fontSize = 16.sp
                    )
                }

                else -> {
                    LazyColumn(
                        modifier = Modifier
                            .fillMaxSize()
                            .padding(16.dp),
                        verticalArrangement = Arrangement.spacedBy(12.dp)
                    ) {
                        items(items) { item ->
                            UploadedItemCard(item)
                        }
                        item {
                            Spacer(modifier = Modifier.height(100.dp))
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun UploadedItemCard(item: SetItemUpdateDto) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.surfaceVariant
        )
    ) {
        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(4.dp)) {
            Text(
                text = item.serialNumber ?: "(No serial)",
                fontWeight = FontWeight.Bold,
                fontSize = 16.sp
            )
            if (!item.modelNumber.isNullOrBlank()) {
                Text("Model: ${item.modelNumber}")
            }
            val fromStatus = item.previousStatus ?: "(none)"
            val toStatus = item.newStatus ?: "(none)"
            Text("Status: $fromStatus → $toStatus")
            if (!item.remark.isNullOrBlank()) {
                Text("Remark: ${item.remark}")
            }
            Text(
                text = "By ${item.updatedByName ?: item.updatedByUserId ?: "Unknown"} at ${item.createdAt}",
                fontSize = 12.sp,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
        }
    }
}
