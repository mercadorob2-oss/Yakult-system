package com.example.yakultscanner.ui.screens

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.PictureAsPdf
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.dp
import androidx.compose.ui.platform.LocalContext
import androidx.navigation.NavController
import com.example.yakultscanner.api.ApiClient
import com.example.yakultscanner.utils.generateProcessedUpdatesPdf
import com.example.yakultscanner.api.ApiResult
import com.example.yakultscanner.api.SetItemUpdateDto
import com.example.yakultscanner.api.safeApiCall
import kotlinx.coroutines.launch

private const val PROCESSED_PAGE_SIZE = 10

@androidx.compose.material3.ExperimentalMaterial3Api
@Composable
fun ProcessedUpdatesScreen(navController: NavController) {
    val scope = rememberCoroutineScope()
    var isLoading by remember { mutableStateOf(true) }
    var errorUi by remember { mutableStateOf<Pair<String, String>?>(null) }
    var updates by remember { mutableStateOf<List<SetItemUpdateDto>>(emptyList()) }
    var currentPage by remember { mutableIntStateOf(0) }
    val context = LocalContext.current

    val totalPages = maxOf(1, (updates.size + PROCESSED_PAGE_SIZE - 1) / PROCESSED_PAGE_SIZE)
    val pagedUpdates = updates.drop(currentPage * PROCESSED_PAGE_SIZE).take(PROCESSED_PAGE_SIZE)

    suspend fun refresh() {
        isLoading = true
        errorUi = null
        currentPage = 0
        when (val result = safeApiCall { ApiClient.service.getProcessedUpdates() }) {
            is ApiResult.Success -> updates = result.data
            is ApiResult.HttpError -> errorUi = buildApiError("Couldn't load processed updates.", result.endpoint, "HTTP: ${result.code}", result.message)
            is ApiResult.NetworkError -> errorUi = buildApiError("Network issue while loading processed updates.", result.endpoint, "Type: Network error", result.message)
            is ApiResult.UnknownError -> errorUi = buildApiError("Unexpected error while loading updates.", result.endpoint, "Type: Unexpected error", result.message)
        }
        isLoading = false
    }

    LaunchedEffect(Unit) { refresh() }

    UpdatesListScreen(
        navController = navController,
        title = "Processed Updates",
        summaryTitle = "Already handled",
        summaryText = "These updates were already processed by the desktop system.",
        countLabel = "${updates.size} processed",
        icon = Icons.Filled.CheckCircle,
        iconContainerColor = Color(0xFFE8F5E9),
        iconTint = Color(0xFF2E7D32),
        headerBrush = Brush.verticalGradient(colors = listOf(com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Ink, Color(0xFF2E7D5B))),
        isLoading = isLoading,
        errorUi = errorUi,
        isEmpty = updates.isEmpty(),
        emptyTitle = "No processed updates yet",
        emptySubtitle = "Processed records will appear here after the desktop side completes them.",
        onRetry = { scope.launch { refresh() } },
        topBarActions = {
            IconButton(
                onClick = { generateProcessedUpdatesPdf(context, updates) },
                enabled = updates.isNotEmpty()
            ) {
                Icon(
                    imageVector = Icons.Filled.PictureAsPdf,
                    contentDescription = "Export PDF",
                    tint = if (updates.isNotEmpty()) Color.White else Color.White.copy(alpha = 0.4f)
                )
            }
        }
    ) {
        items(pagedUpdates) { update ->
            UpdateRecordCard(
                update = update,
                modeLabel = "Processed",
                timestamp = update.processedAt?.replace("T", " ") ?: "Unknown time",
                icon = Icons.Filled.CheckCircle,
                iconContainerColor = Color(0xFFE8F5E9),
                iconTint = Color(0xFF2E7D32)
            )
        }
        if (totalPages > 1) {
            item {
                ProcessedPaginationCard(
                    currentPage = currentPage,
                    totalPages = totalPages,
                    onPrev = { currentPage-- },
                    onNext = { currentPage++ }
                )
            }
        }
    }
}

@Composable
private fun ProcessedPaginationCard(
    currentPage: Int,
    totalPages: Int,
    onPrev: () -> Unit,
    onNext: () -> Unit
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(14.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f))
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 12.dp, vertical = 8.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            OutlinedButton(
                onClick = onPrev,
                enabled = currentPage > 0,
                shape = RoundedCornerShape(10.dp)
            ) { Text("Prev") }
            Text(
                text = "Page ${currentPage + 1} of $totalPages",
                style = MaterialTheme.typography.labelLarge
            )
            OutlinedButton(
                onClick = onNext,
                enabled = currentPage < totalPages - 1,
                shape = RoundedCornerShape(10.dp)
            ) { Text("Next") }
        }
    }
}