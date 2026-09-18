package com.example.yakultscanner.ui.screens

import android.content.Intent
import android.widget.Toast
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.AccessTime
import androidx.compose.material.icons.filled.Assessment
import androidx.compose.material.icons.filled.BarChart
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.Description
import androidx.compose.material.icons.filled.History
import androidx.compose.material.icons.filled.PictureAsPdf
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.Search
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Checkbox
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.DatePicker
import androidx.compose.material3.DatePickerDialog
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilterChip
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.rememberDatePickerState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.core.content.FileProvider
import androidx.hilt.lifecycle.viewmodel.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.navigation.NavController
import com.example.yakultscanner.api.RepairAttendanceDto
import com.example.yakultscanner.api.RepairReportPreviewResponse
import com.example.yakultscanner.api.RepairReportPreviewTicketDto
import com.example.yakultscanner.api.RepairReportSignatureDto
import com.example.yakultscanner.api.RepairReportsSummaryDto
import com.example.yakultscanner.viewmodels.RepairPortalViewModel
import com.example.yakultscanner.viewmodels.RepairReportPdfUiState
import com.example.yakultscanner.viewmodels.RepairReportPreviewUiState
import com.example.yakultscanner.viewmodels.RepairReportsUiState
import com.example.yakultscanner.viewmodels.RepairTicketsUiState
import java.text.SimpleDateFormat
import java.util.Locale
import java.util.TimeZone
import java.util.concurrent.TimeUnit
import kotlinx.coroutines.delay

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun RepairReportsTabContent(
    navController: NavController,
    viewModel: RepairPortalViewModel = hiltViewModel()
) {
    val state by viewModel.reportsState.collectAsStateWithLifecycle()
    val previewState by viewModel.reportPreviewState.collectAsStateWithLifecycle()
    val pdfState by viewModel.reportPdfState.collectAsStateWithLifecycle()
    val context = LocalContext.current
    var showPicker by remember { mutableStateOf(false) }
    var showPreview by remember { mutableStateOf(false) }
    var pickerScope by rememberSaveable { mutableStateOf("mine") }
    var reviewedBy by remember { mutableStateOf<RepairReportSignatureDto?>(null) }
    var receivedBy by remember { mutableStateOf<RepairReportSignatureDto?>(null) }

    LaunchedEffect(Unit) {
        if (state is RepairReportsUiState.Idle) viewModel.loadReports()
    }
    LaunchedEffect(previewState) {
        if (previewState is RepairReportPreviewUiState.Success) showPreview = true
    }
    LaunchedEffect(pdfState) {
        when (val s = pdfState) {
            is RepairReportPdfUiState.Success -> {
                Toast.makeText(context, "PDF generated: ${s.file.name}", Toast.LENGTH_LONG).show()
                try {
                    val uri = FileProvider.getUriForFile(context, "${context.packageName}.fileprovider", s.file)
                    val intent = Intent(Intent.ACTION_VIEW).apply {
                        setDataAndType(uri, "application/pdf")
                        addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
                    }
                    val share = Intent.createChooser(Intent(Intent.ACTION_SEND).apply {
                        type = "application/pdf"
                        putExtra(Intent.EXTRA_STREAM, uri)
                        addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
                    }, "Share Repair Report")
                    // Try view first, fallback to share
                    try { context.startActivity(intent) } catch (_: Exception) { context.startActivity(share) }
                } catch (_: Exception) {
                    Toast.makeText(context, "Saved to ${s.file.absolutePath}", Toast.LENGTH_LONG).show()
                }
                viewModel.resetReportPdfState()
            }
            is RepairReportPdfUiState.Error -> {
                Toast.makeText(context, s.message, Toast.LENGTH_LONG).show()
                viewModel.resetReportPdfState()
            }
            else -> Unit
        }
    }

    when (val current = state) {
        RepairReportsUiState.Idle, RepairReportsUiState.Loading -> Box(
            Modifier.fillMaxSize().padding(16.dp),
            contentAlignment = Alignment.Center
        ) {
            Column(horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(12.dp)) {
                CircularProgressIndicator(color = RepairUi.Brand)
                Text("Loading reports…", style = MaterialTheme.typography.bodySmall, color = RepairUi.Muted)
            }
        }
        is RepairReportsUiState.Error -> RepairReportsError(
            message = current.message,
            onRetry = { viewModel.loadReports() }
        )
        is RepairReportsUiState.Success -> RepairReportsSuccess(
            response = current.response,
            onRefresh = { viewModel.loadReports() },
            onTicketClick = { id -> navController.navigate("repair_ticket_detail/$id") },
            onGenerateReport = { showPicker = true; viewModel.loadReportPickerTickets(scope = pickerScope) },
            onStatusClick = { status ->
                showPicker = true
                pickerScope = if (current.response.isTechnician) "technician" else "mine"
                viewModel.loadReportPickerTickets(scope = pickerScope, status = status)
            },
            viewModel = viewModel
        )
    }

    if (showPicker) {
        RepairReportPickerDialog(
            viewModel = viewModel,
            initialScope = pickerScope,
            onScopeChange = { pickerScope = it },
            onDismiss = { showPicker = false },
            onContinue = { ids ->
                showPicker = false
                if (ids.isNotEmpty()) {
                    // Load preview; signatures are collected in next step
                    viewModel.loadReportPreview(ids, includeAttachments = true)
                }
            }
        )
    }

    if (showPreview) {
        val preview = (previewState as? RepairReportPreviewUiState.Success)?.response
        if (preview != null) {
            RepairReportPreviewDialog(
                preview = preview,
                viewModel = viewModel,
                reviewedBy = reviewedBy,
                receivedBy = receivedBy,
                onReviewedByChange = { reviewedBy = it },
                onReceivedByChange = { receivedBy = it },
                onDismiss = { showPreview = false; viewModel.resetReportPreviewState() },
                onGeneratePdf = {
                    // Use preview with signatures -> generate on-device PDF
                    viewModel.generateReportPdf(context, preview, reviewedBy, receivedBy)
                },
                onGenerateWithRecord = {
                    // Also record signatures server-side for reuse
                    val req = com.example.yakultscanner.api.RepairReportPreviewRequest(
                        ticketIds = preview.reports.map { it.repairTicketId },
                        includeAttachments = true,
                        recordSignatures = true,
                        reviewedBy = reviewedBy,
                        receivedBy = receivedBy
                    )
                    viewModel.generateReportWithSignatures(req)
                    // PDF will be generated from the new preview success (loop)
                }
            )
        } else if (previewState is RepairReportPreviewUiState.Loading) {
            AlertDialog(
                onDismissRequest = {},
                containerColor = RepairUi.Surface,
                title = { Text("Preparing preview…", color = RepairUi.Ink, fontWeight = FontWeight.Bold) },
                text = { Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(12.dp)) { CircularProgressIndicator(color = RepairUi.Brand, modifier = Modifier.size(22.dp)); Text("Fetching report data for selected tickets…", color = RepairUi.Muted) } },
                confirmButton = {}
            )
        } else if (previewState is RepairReportPreviewUiState.Error) {
            AlertDialog(
                onDismissRequest = { viewModel.resetReportPreviewState() },
                containerColor = RepairUi.Surface,
                title = { Text("Preview failed", color = RepairUi.Critical, fontWeight = FontWeight.Bold) },
                text = { Text((previewState as RepairReportPreviewUiState.Error).message, color = RepairUi.Ink) },
                confirmButton = { TextButton(onClick = { viewModel.resetReportPreviewState() }) { Text("Close") } }
            )
        }
    }
}

@Composable
private fun RepairReportsError(message: String, onRetry: () -> Unit) {
    Box(Modifier.fillMaxSize().padding(16.dp), contentAlignment = Alignment.Center) {
        RepairSurfaceCard(modifier = Modifier.fillMaxWidth(), tonalColor = RepairUi.CriticalSoft) {
            Icon(Icons.Filled.Warning, contentDescription = null, tint = RepairUi.Critical, modifier = Modifier.size(28.dp))
            Text("We could not load reports", style = MaterialTheme.typography.titleMedium, color = RepairUi.Critical, fontWeight = FontWeight.Bold)
            Text(message, style = MaterialTheme.typography.bodySmall, color = RepairUi.Ink)
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                RepairPrimaryButton(onClick = onRetry) { Text("Try again") }
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun RepairReportsSuccess(
    response: com.example.yakultscanner.api.RepairReportsResponse,
    onRefresh: () -> Unit,
    onTicketClick: (Int) -> Unit,
    onGenerateReport: () -> Unit,
    onStatusClick: (String) -> Unit,
    viewModel: RepairPortalViewModel
) {
    val summary = response.summary
    val mySummary = response.mySummary
    val isTech = response.isTechnician

    LazyColumn(
        modifier = Modifier.fillMaxSize(),
        contentPadding = PaddingValues(start = 16.dp, end = 16.dp, top = 12.dp, bottom = 96.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        // Header with refresh + Generate CTA
        item {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Column(verticalArrangement = Arrangement.spacedBy(2.dp)) {
                    Text("Reports", style = MaterialTheme.typography.titleMedium, color = RepairUi.Ink, fontWeight = FontWeight.Bold)
                    Text(
                        if (isTech) "Technician workspace · ${summary?.total ?: 0} total tickets"
                        else "Your repair summary · ${summary?.total ?: 0} tickets",
                        style = MaterialTheme.typography.bodySmall,
                        color = RepairUi.Muted
                    )
                }
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp), verticalAlignment = Alignment.CenterVertically) {
                    IconButton(onClick = onRefresh) {
                        Icon(Icons.Filled.Refresh, contentDescription = "Refresh reports", tint = RepairUi.Brand)
                    }
                }
            }
        }

        // Generate Report CTA — primary action, always visible
        item {
            RepairSurfaceCard(tonalColor = RepairUi.BrandSoft) {
                Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
                    Column(modifier = Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(2.dp)) {
                        Text("Need a printable report?", style = MaterialTheme.typography.titleSmall, color = RepairUi.Ink, fontWeight = FontWeight.Bold)
                        Text("Pick tickets, add signatures, export PDF — same layout as desktop", style = MaterialTheme.typography.bodySmall, color = RepairUi.Muted)
                    }
                    Button(onClick = onGenerateReport, colors = ButtonDefaults.buttonColors(containerColor = RepairUi.Brand)) {
                        Icon(Icons.Filled.Description, contentDescription = null, modifier = Modifier.size(18.dp))
                        Spacer(Modifier.width(6.dp))
                        Text("Generate", fontWeight = FontWeight.Bold)
                    }
                }
            }
        }

        // Attendance — only for technicians
        if (isTech) {
            item {
                RepairAttendanceReportsCard(
                    today = response.today,
                    history = response.history,
                    onRefresh = onRefresh
                )
            }
        } else {
            item {
                RepairSurfaceCard(tonalColor = RepairUi.SurfaceSubtle) {
                    Row(horizontalArrangement = Arrangement.spacedBy(10.dp), verticalAlignment = Alignment.CenterVertically) {
                        Box(
                            modifier = Modifier.size(38.dp).clip(RoundedCornerShape(10.dp)).background(RepairUi.BrandSoft),
                            contentAlignment = Alignment.Center
                        ) {
                            Icon(Icons.Filled.AccessTime, contentDescription = null, tint = RepairUi.Brand, modifier = Modifier.size(20.dp))
                        }
                        Column(verticalArrangement = Arrangement.spacedBy(2.dp), modifier = Modifier.weight(1f)) {
                            Text("Attendance", style = MaterialTheme.typography.titleSmall, color = RepairUi.Ink, fontWeight = FontWeight.Bold)
                            Text("Attendance tracking is available to IT technicians.", style = MaterialTheme.typography.bodySmall, color = RepairUi.Muted)
                        }
                    }
                }
            }
        }

        // Summary — main (tappable cells drill into picker filtered by status)
        if (summary != null) {
            item {
                RepairSummaryGrid(
                    title = if (isTech) "All tickets · by status" else "My tickets · by status",
                    subtitle = if (isTech) "Tap a status to generate a report for that slice · Workspace totals" else "Tap a status to narrow your report",
                    summary = summary,
                    onStatusClick = onStatusClick
                )
            }
            if (isTech && mySummary != null && mySummary.total > 0) {
                item {
                    RepairSummaryGrid(
                        title = "My requests · by status",
                        subtitle = "Your own submitted/requested tickets",
                        summary = mySummary,
                        compact = true,
                        onStatusClick = onStatusClick
                    )
                }
            }
        }

        // KPIs row — quick derived stats
        if (summary != null) {
            item {
                RepairKpiRow(summary)
            }
        }

        // Recent tickets — tappable + long-press could multi-select (future), for now tap opens detail
        item {
            RepairSurfaceCard {
                RepairSectionHeading(
                    title = "Recent tickets (${response.recentTickets.size})",
                    subtitle = if (isTech) "Latest updated across the workspace — tap Generate to pick any ticket" else "Your most recently updated requests"
                )
                if (response.recentTickets.isEmpty()) {
                    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                        Text("No tickets to show yet.", style = MaterialTheme.typography.bodySmall, color = RepairUi.Muted)
                        OutlinedButton(onClick = onGenerateReport) { Text("Generate your first report") }
                    }
                } else {
                    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                        response.recentTickets.take(12).forEachIndexed { index, ticket ->
                            if (index > 0) HorizontalDivider(color = RepairUi.Divider)
                            RepairRecentTicketRow(ticket, onClick = { onTicketClick(ticket.repairTicketId) })
                        }
                        TextButton(onClick = onGenerateReport, modifier = Modifier.fillMaxWidth()) { Text("View all & generate report →", color = RepairUi.Brand) }
                    }
                }
            }
        }

        // Footer hint
        item {
            RepairSurfaceCard(tonalColor = RepairUi.SurfaceSubtle) {
                Row(horizontalArrangement = Arrangement.spacedBy(10.dp), verticalAlignment = Alignment.CenterVertically) {
                    Icon(Icons.Filled.Assessment, contentDescription = null, tint = RepairUi.Muted, modifier = Modifier.size(20.dp))
                    Text(
                        "Reports update automatically. Tap a status cell or Generate to build a printable PDF.",
                        style = MaterialTheme.typography.bodySmall,
                        color = RepairUi.Muted
                    )
                }
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun RepairReportPickerDialog(
    viewModel: RepairPortalViewModel,
    initialScope: String,
    onScopeChange: (String) -> Unit,
    onDismiss: () -> Unit,
    onContinue: (List<Int>) -> Unit
) {
    val pickerState by viewModel.reportPickerTicketsState.collectAsStateWithLifecycle()
    val selectedIds by viewModel.selectedReportIds.collectAsStateWithLifecycle()
    var search by rememberSaveable { mutableStateOf("") }
    var scope by rememberSaveable { mutableStateOf(initialScope) }
    var statusFilter by rememberSaveable { mutableStateOf<String?>(null) }

    LaunchedEffect(scope, statusFilter) {
        viewModel.loadReportPickerTickets(scope = scope, status = statusFilter, search = search.takeIf { it.isNotBlank() })
    }
    LaunchedEffect(search) {
        delay(350)
        viewModel.loadReportPickerTickets(scope = scope, status = statusFilter, search = search.takeIf { it.isNotBlank() })
    }

    AlertDialog(
        onDismissRequest = onDismiss,
        containerColor = RepairUi.Surface,
        title = {
            Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
                Text("Generate Report — Pick Tickets", color = RepairUi.Ink, fontWeight = FontWeight.Bold)
                Text("${selectedIds.size} selected · max 20 · grouped by requester in final PDF", style = MaterialTheme.typography.labelSmall, color = RepairUi.Muted)
            }
        },
        text = {
            Column(modifier = Modifier.fillMaxWidth().heightIn(max = 520.dp).verticalScroll(rememberScrollState()), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                // Scope chips
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    FilterChip(selected = scope == "mine", onClick = { scope = "mine"; onScopeChange("mine") }, label = { Text("My requests") })
                    FilterChip(selected = scope == "technician", onClick = { scope = "technician"; onScopeChange("technician") }, label = { Text("Technician queue") })
                }
                // Status filter chips
                Row(horizontalArrangement = Arrangement.spacedBy(6.dp), modifier = Modifier.fillMaxWidth()) {
                    FilterChip(selected = statusFilter == null, onClick = { statusFilter = null }, label = { Text("All") })
                    listOf("Waiting" to "Waiting", "Repairing" to "Repairing", "Completed" to "Completed").forEach { (v, l) ->
                        FilterChip(selected = statusFilter == v, onClick = { statusFilter = if (statusFilter == v) null else v }, label = { Text(l) })
                    }
                }
                OutlinedTextField(
                    value = search,
                    onValueChange = { search = it },
                    modifier = Modifier.fillMaxWidth(),
                    label = { Text("Search code, item, serial") },
                    leadingIcon = { Icon(Icons.Filled.Search, null, tint = RepairUi.Muted) },
                    trailingIcon = { if (search.isNotBlank()) IconButton(onClick = { search = "" }) { Icon(Icons.Filled.Close, "Clear", tint = RepairUi.Muted) } },
                    singleLine = true,
                    shape = RoundedCornerShape(10.dp),
                    colors = repairTextFieldColors()
                )
                Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
                    val total = (pickerState as? RepairTicketsUiState.Success)?.response?.tickets?.size ?: 0
                    Text("$total ticket(s)", style = MaterialTheme.typography.labelSmall, color = RepairUi.Muted, fontWeight = FontWeight.SemiBold)
                    TextButton(onClick = {
                        val allIds = (pickerState as? RepairTicketsUiState.Success)?.response?.tickets?.map { it.repairTicketId }?.take(20)?.toSet() ?: emptySet()
                        if (selectedIds.size == allIds.size) viewModel.clearReportSelection() else viewModel.setReportSelection(allIds)
                    }) { Text(if (selectedIds.isEmpty()) "Select all" else "Clear") }
                }
                when (val s = pickerState) {
                    RepairTicketsUiState.Idle, RepairTicketsUiState.Loading -> Box(Modifier.fillMaxWidth().height(120.dp), contentAlignment = Alignment.Center) { CircularProgressIndicator(color = RepairUi.Brand) }
                    is RepairTicketsUiState.Error -> {
                        RepairSurfaceCard(tonalColor = RepairUi.CriticalSoft) {
                            Text(s.message, color = RepairUi.Critical, style = MaterialTheme.typography.bodySmall)
                            TextButton(onClick = { viewModel.loadReportPickerTickets(scope, search.takeIf { it.isNotBlank() }, statusFilter) }) { Text("Retry") }
                        }
                    }
                    is RepairTicketsUiState.Success -> {
                        if (s.response.tickets.isEmpty()) {
                            Text("No tickets match this filter.", color = RepairUi.Muted, style = MaterialTheme.typography.bodySmall)
                        } else {
                            Column(verticalArrangement = Arrangement.spacedBy(0.dp)) {
                                s.response.tickets.forEach { t ->
                                    val checked = t.repairTicketId in selectedIds
                                    Row(
                                        modifier = Modifier.fillMaxWidth().clip(RoundedCornerShape(10.dp)).clickable { viewModel.toggleReportSelection(t.repairTicketId) }.padding(horizontal = 4.dp, vertical = 6.dp),
                                        verticalAlignment = Alignment.CenterVertically,
                                        horizontalArrangement = Arrangement.spacedBy(10.dp)
                                    ) {
                                        Checkbox(checked = checked, onCheckedChange = { viewModel.toggleReportSelection(t.repairTicketId) })
                                        Column(modifier = Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(2.dp)) {
                                            Text(t.ticketCode ?: "Repair #${t.repairTicketId}", style = MaterialTheme.typography.labelMedium, color = RepairUi.Brand, fontWeight = FontWeight.Bold, maxLines = 1, overflow = TextOverflow.Ellipsis)
                                            Text(t.itemName?.takeIf { it.isNotBlank() } ?: "Unknown equipment", style = MaterialTheme.typography.bodySmall, color = RepairUi.Ink, fontWeight = FontWeight.SemiBold, maxLines = 1, overflow = TextOverflow.Ellipsis)
                                            Row(horizontalArrangement = Arrangement.spacedBy(6.dp), verticalAlignment = Alignment.CenterVertically) {
                                                RepairStatusPill(t.status); t.priority?.let { RepairPriorityPill(it, compact = true) }
                                            }
                                        }
                                    }
                                    HorizontalDivider(color = RepairUi.Divider)
                                }
                            }
                        }
                    }
                }
            }
        },
        confirmButton = {
            Button(
                onClick = { onContinue(selectedIds.toList()) },
                enabled = selectedIds.isNotEmpty(),
                colors = ButtonDefaults.buttonColors(containerColor = RepairUi.Brand)
            ) { Text("Continue · ${selectedIds.size}") }
        },
        dismissButton = { TextButton(onClick = onDismiss) { Text("Cancel") } }
    )
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun RepairReportPreviewDialog(
    preview: RepairReportPreviewResponse,
    viewModel: RepairPortalViewModel,
    reviewedBy: RepairReportSignatureDto?,
    receivedBy: RepairReportSignatureDto?,
    onReviewedByChange: (RepairReportSignatureDto?) -> Unit,
    onReceivedByChange: (RepairReportSignatureDto?) -> Unit,
    onDismiss: () -> Unit,
    onGeneratePdf: () -> Unit,
    onGenerateWithRecord: () -> Unit
) {
    var showReviewedPicker by remember { mutableStateOf(false) }
    var showReceivedPicker by remember { mutableStateOf(false) }
    var reviewedName by rememberSaveable(preview) { mutableStateOf(reviewedBy?.employeeName ?: "") }
    var reviewedTitle by rememberSaveable(preview) { mutableStateOf(reviewedBy?.title ?: "") }
    var reviewedDate by rememberSaveable(preview) { mutableStateOf(reviewedBy?.signedDate ?: SimpleDateFormat("yyyy-MM-dd", Locale.US).format(java.util.Date())) }
    var receivedName by rememberSaveable(preview) { mutableStateOf(receivedBy?.employeeName ?: "") }
    var receivedTitle by rememberSaveable(preview) { mutableStateOf(receivedBy?.title ?: "") }
    var receivedDate by rememberSaveable(preview) { mutableStateOf(receivedBy?.signedDate ?: SimpleDateFormat("yyyy-MM-dd", Locale.US).format(java.util.Date())) }
    var includeAttachments by rememberSaveable { mutableStateOf(true) }
    val pdfState by viewModel.reportPdfState.collectAsStateWithLifecycle()

    // Keep parent state in sync for PDF
    LaunchedEffect(reviewedName, reviewedTitle, reviewedDate) {
        onReviewedByChange(if (reviewedName.isBlank()) null else RepairReportSignatureDto(roleName = "Reviewed By", employeeName = reviewedName.trim(), title = reviewedTitle.trim().takeIf { it.isNotBlank() }, signedDate = reviewedDate))
    }
    LaunchedEffect(receivedName, receivedTitle, receivedDate) {
        onReceivedByChange(if (receivedName.isBlank()) null else RepairReportSignatureDto(roleName = "Received By", employeeName = receivedName.trim(), title = receivedTitle.trim().takeIf { it.isNotBlank() }, signedDate = receivedDate))
    }

    AlertDialog(
        onDismissRequest = onDismiss,
        containerColor = RepairUi.Surface,
        title = {
            Column {
                Text("Report Preview — ${preview.ticketCount} ticket(s)", color = RepairUi.Ink, fontWeight = FontWeight.Bold)
                Text("${preview.groupCount} requester group(s) • ${preview.generatedByName ?: ""} • Tap Generate to export PDF", style = MaterialTheme.typography.labelSmall, color = RepairUi.Muted)
            }
        },
        text = {
            Column(modifier = Modifier.fillMaxWidth().heightIn(max = 560.dp).verticalScroll(rememberScrollState()), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                // Groups overview
                preview.groups.forEach { g ->
                    RepairSurfaceCard(tonalColor = RepairUi.SurfaceSubtle) {
                        Text(g.groupLabel ?: "Unspecified", style = MaterialTheme.typography.titleSmall, color = RepairUi.Ink, fontWeight = FontWeight.Bold)
                        Text("${g.ticketCount} ticket(s): ${g.ticketCodes.joinToString(", ")}", style = MaterialTheme.typography.bodySmall, color = RepairUi.Muted)
                        Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
                            g.tickets.take(3).forEach { t ->
                                Text("• ${t.ticketCode ?: t.repairTicketId} — ${t.itemName ?: "Unknown"} • ${t.status ?: ""}", style = MaterialTheme.typography.labelSmall, color = RepairUi.Ink)
                            }
                            if (g.tickets.size > 3) Text("+ ${g.tickets.size - 3} more", style = MaterialTheme.typography.labelSmall, color = RepairUi.Muted)
                        }
                    }
                }
                // Signatures — Reviewed By
                RepairSurfaceCard {
                    RepairSectionHeading("Reviewed By", "Information Technology Dept. — leave blank to sign on paper")
                    OutlinedTextField(value = reviewedName, onValueChange = { reviewedName = it }, modifier = Modifier.fillMaxWidth(), label = { Text("Name") }, singleLine = true, shape = RoundedCornerShape(10.dp), colors = repairTextFieldColors(), trailingIcon = {
                        IconButton(onClick = { showReviewedPicker = true }) { Icon(Icons.Filled.Search, "Pick", tint = RepairUi.Brand) }
                    })
                    OutlinedTextField(value = reviewedTitle, onValueChange = { reviewedTitle = it }, modifier = Modifier.fillMaxWidth(), label = { Text("Title / Designation") }, singleLine = true, shape = RoundedCornerShape(10.dp), colors = repairTextFieldColors())
                    OutlinedTextField(value = reviewedDate, onValueChange = { reviewedDate = it }, modifier = Modifier.fillMaxWidth(), label = { Text("Date Signed (YYYY-MM-DD)") }, singleLine = true, shape = RoundedCornerShape(10.dp), colors = repairTextFieldColors())
                }
                // Received By
                RepairSurfaceCard {
                    RepairSectionHeading("Received By", "Requester department — leave blank to sign on paper")
                    OutlinedTextField(value = receivedName, onValueChange = { receivedName = it }, modifier = Modifier.fillMaxWidth(), label = { Text("Name") }, singleLine = true, shape = RoundedCornerShape(10.dp), colors = repairTextFieldColors(), trailingIcon = {
                        IconButton(onClick = { showReceivedPicker = true }) { Icon(Icons.Filled.Search, "Pick", tint = RepairUi.Brand) }
                    })
                    OutlinedTextField(value = receivedTitle, onValueChange = { receivedTitle = it }, modifier = Modifier.fillMaxWidth(), label = { Text("Title / Designation") }, singleLine = true, shape = RoundedCornerShape(10.dp), colors = repairTextFieldColors())
                    OutlinedTextField(value = receivedDate, onValueChange = { receivedDate = it }, modifier = Modifier.fillMaxWidth(), label = { Text("Date Signed (YYYY-MM-DD)") }, singleLine = true, shape = RoundedCornerShape(10.dp), colors = repairTextFieldColors())
                }
                // Attachments toggle
                RepairSurfaceCard {
                    Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
                        Column(modifier = Modifier.weight(1f)) {
                            Text("Include attachments", style = MaterialTheme.typography.titleSmall, color = RepairUi.Ink, fontWeight = FontWeight.Bold)
                            Text("Image evidence listed per ticket", style = MaterialTheme.typography.labelSmall, color = RepairUi.Muted)
                        }
                        Checkbox(checked = includeAttachments, onCheckedChange = { includeAttachments = it })
                    }
                    if (includeAttachments) {
                        val totalAttach = preview.reports.sumOf { it.attachments.size }
                        Text("$totalAttach file(s) across ${preview.ticketCount} ticket(s) will be listed", style = MaterialTheme.typography.bodySmall, color = RepairUi.Muted)
                    }
                }
                // Sample ticket detail (first ticket full preview)
                preview.reports.firstOrNull()?.let { t -> RepairPreviewTicketCard(t) }

                if (pdfState is RepairReportPdfUiState.Generating) {
                    Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        CircularProgressIndicator(modifier = Modifier.size(18.dp), color = RepairUi.Brand, strokeWidth = 2.dp)
                        Text("Generating PDF…", color = RepairUi.Muted, style = MaterialTheme.typography.bodySmall)
                    }
                }
            }
        },
        confirmButton = {
            Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
                Button(
                    onClick = onGeneratePdf,
                    enabled = pdfState !is RepairReportPdfUiState.Generating,
                    modifier = Modifier.fillMaxWidth(),
                    colors = ButtonDefaults.buttonColors(containerColor = RepairUi.Brand)
                ) {
                    Icon(Icons.Filled.PictureAsPdf, null, modifier = Modifier.size(18.dp))
                    Spacer(Modifier.width(6.dp))
                    Text("Generate PDF", fontWeight = FontWeight.Bold)
                }
                OutlinedButton(onClick = onGenerateWithRecord, enabled = pdfState !is RepairReportPdfUiState.Generating, modifier = Modifier.fillMaxWidth()) {
                    Text("Generate & Save Signatures", color = RepairUi.Brand)
                }
            }
        },
        dismissButton = { TextButton(onClick = onDismiss) { Text("Close") } }
    )

    if (showReviewedPicker) {
        RepairSignatureEmployeePicker(viewModel, title = "Pick Reviewer", onDismiss = { showReviewedPicker = false }, onSelected = { emp ->
            reviewedName = emp.name; reviewedTitle = reviewedTitle.takeIf { it.isNotBlank() } ?: "IT Staff"; showReviewedPicker = false
        })
    }
    if (showReceivedPicker) {
        RepairSignatureEmployeePicker(viewModel, title = "Pick Receiver", onDismiss = { showReceivedPicker = false }, onSelected = { emp ->
            receivedName = emp.name; showReceivedPicker = false
        })
    }
}

@Composable
private fun RepairSignatureEmployeePicker(
    viewModel: RepairPortalViewModel,
    title: String,
    onDismiss: () -> Unit,
    onSelected: (com.example.yakultscanner.api.RepairLookupItemDto) -> Unit
) {
    val items by viewModel.technicianLookup.collectAsStateWithLifecycle()
    val loading by viewModel.lookupLoading.collectAsStateWithLifecycle()
    var search by remember { mutableStateOf("") }
    LaunchedEffect(Unit) { viewModel.loadTechnicianLookup("employees", null) }
    LaunchedEffect(search) {
        delay(300)
        viewModel.loadTechnicianLookup("employees", search.takeIf { it.isNotBlank() })
    }
    AlertDialog(
        onDismissRequest = onDismiss,
        containerColor = RepairUi.Surface,
        title = { Text(title, color = RepairUi.Ink, fontWeight = FontWeight.Bold) },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                OutlinedTextField(value = search, onValueChange = { search = it }, modifier = Modifier.fillMaxWidth(), label = { Text("Search employee") }, singleLine = true, shape = RoundedCornerShape(10.dp), colors = repairTextFieldColors())
                if (loading) CircularProgressIndicator(Modifier.align(Alignment.CenterHorizontally), color = RepairUi.Brand)
                Column(Modifier.heightIn(max = 320.dp).verticalScroll(rememberScrollState())) {
                    if (!loading && items.isEmpty()) Text("No matches.", color = RepairUi.Muted, style = MaterialTheme.typography.bodySmall)
                    items.forEach { emp ->
                        Row(Modifier.fillMaxWidth().clip(RoundedCornerShape(10.dp)).clickable { onSelected(emp) }.padding(10.dp), verticalAlignment = Alignment.CenterVertically) {
                            Column(modifier = Modifier.weight(1f)) { Text(emp.name, color = RepairUi.Ink, fontWeight = FontWeight.SemiBold); Text(emp.displayText, style = MaterialTheme.typography.labelSmall, color = RepairUi.Muted) }
                        }
                        HorizontalDivider(color = RepairUi.Divider)
                    }
                }
            }
        },
        confirmButton = { TextButton(onClick = onDismiss) { Text("Close") } }
    )
}

@Composable
private fun RepairPreviewTicketCard(ticket: RepairReportPreviewTicketDto) {
    RepairSurfaceCard(tonalColor = RepairUi.SurfaceSubtle) {
        Text(ticket.ticketCode ?: "Repair #${ticket.repairTicketId}", color = RepairUi.Brand, fontWeight = FontWeight.Bold, style = MaterialTheme.typography.labelMedium)
        Text(ticket.itemName ?: "Unknown", color = RepairUi.Ink, fontWeight = FontWeight.SemiBold)
        ticket.problemText?.takeIf { it.isNotBlank() }?.let { Text(it, color = RepairUi.Muted, style = MaterialTheme.typography.bodySmall, maxLines = 3, overflow = TextOverflow.Ellipsis) }
        Row(horizontalArrangement = Arrangement.spacedBy(6.dp), verticalAlignment = Alignment.CenterVertically) {
            RepairStatusPill(ticket.status)
            ticket.priority?.let { RepairPriorityPill(it, compact = true) }
        }
        if (ticket.hasParts && ticket.parts.isNotEmpty()) Text("Parts: ${ticket.parts.joinToString { it.displayName + ": " + it.status }}", color = RepairUi.Muted, style = MaterialTheme.typography.labelSmall)
        if (ticket.hasAttachments) Text("${ticket.attachments.size} attachment(s)", color = RepairUi.Muted, style = MaterialTheme.typography.labelSmall)
    }
}

@Composable
private fun RepairAttendanceReportsCard(
    today: RepairAttendanceDto?,
    history: List<RepairAttendanceDto>,
    onRefresh: () -> Unit
) {
    val hasTimeIn = !today?.timeIn.isNullOrBlank()
    val hasTimeOut = !today?.timeOut.isNullOrBlank()
    val statusTitle = when {
        hasTimeIn && hasTimeOut -> "Today completed"
        hasTimeIn -> "Today · timed in"
        else -> "No session today"
    }
    val statusColor = when {
        hasTimeIn && hasTimeOut -> RepairUi.Complete
        hasTimeIn -> RepairUi.Active
        else -> RepairUi.Muted
    }
    val bg = when {
        hasTimeIn && hasTimeOut -> RepairUi.CompleteSoft
        hasTimeIn -> RepairUi.ActiveSoft
        else -> RepairUi.SurfaceSubtle
    }

    RepairSurfaceCard(tonalColor = bg) {
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            RepairSectionHeading("Attendance", if (hasTimeIn) "Today's technician session" else "Track your repair day")
            Icon(if (hasTimeIn) Icons.Filled.CheckCircle else Icons.Filled.AccessTime, null, tint = statusColor, modifier = Modifier.size(22.dp))
        }
        Column(
            modifier = Modifier.fillMaxWidth().clip(RoundedCornerShape(12.dp)).background(RepairUi.Surface).padding(12.dp),
            verticalArrangement = Arrangement.spacedBy(6.dp)
        ) {
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp), verticalAlignment = Alignment.CenterVertically) {
                Box(
                    modifier = Modifier.size(32.dp).clip(RoundedCornerShape(8.dp)).background(bg),
                    contentAlignment = Alignment.Center
                ) {
                    Icon(Icons.Filled.AccessTime, null, tint = statusColor, modifier = Modifier.size(18.dp))
                }
                Column(modifier = Modifier.weight(1f)) {
                    Text(statusTitle, style = MaterialTheme.typography.bodyMedium, color = RepairUi.Ink, fontWeight = FontWeight.Bold)
                    Text(today?.workDate ?: todayDateLabel(), style = MaterialTheme.typography.labelSmall, color = RepairUi.Muted)
                }
                IconButton(onClick = onRefresh, modifier = Modifier.size(32.dp)) {
                    Icon(Icons.Filled.Refresh, "Refresh", tint = RepairUi.Muted, modifier = Modifier.size(18.dp))
                }
            }
            HorizontalDivider(color = RepairUi.Divider)
            if (hasTimeIn) {
                RepairAttendanceTimeRow("Time in", formatAttendanceTime(today?.timeIn))
                if (hasTimeOut) {
                    RepairAttendanceTimeRow("Time out", formatAttendanceTime(today?.timeOut))
                    val dur = attendanceDuration(today?.timeIn, today?.timeOut)
                    if (dur != null) RepairAttendanceTimeRow("Duration", dur, bold = true)
                } else {
                    val elapsed = attendanceDuration(today?.timeIn, null)
                    if (elapsed != null) RepairAttendanceTimeRow("Elapsed", elapsed, bold = true)
                    Text("You are currently timed in.", style = MaterialTheme.typography.labelSmall, color = RepairUi.Active, fontWeight = FontWeight.SemiBold)
                }
            } else {
                Text("Time in to start tracking today's repair session.", style = MaterialTheme.typography.bodySmall, color = RepairUi.Muted)
            }
        }
        if (history.isNotEmpty()) {
            Text("History · last ${minOf(history.size, 10)} sessions", style = MaterialTheme.typography.labelMedium, color = RepairUi.Ink, fontWeight = FontWeight.Bold)
            Column(
                modifier = Modifier.fillMaxWidth().clip(RoundedCornerShape(12.dp)).background(RepairUi.Surface).padding(8.dp),
                verticalArrangement = Arrangement.spacedBy(6.dp)
            ) {
                history.take(10).forEachIndexed { idx, h ->
                    if (idx > 0) HorizontalDivider(color = RepairUi.Divider)
                    Row(
                        modifier = Modifier.fillMaxWidth().padding(vertical = 4.dp),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Column(modifier = Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(1.dp)) {
                            Text(h.workDate ?: "—", style = MaterialTheme.typography.bodySmall, color = RepairUi.Ink, fontWeight = FontWeight.SemiBold)
                            val inLabel = formatAttendanceTime(h.timeIn) ?: "—"
                            val outLabel = formatAttendanceTime(h.timeOut) ?: "—"
                            Text("$inLabel → $outLabel", style = MaterialTheme.typography.labelSmall, color = RepairUi.Muted)
                        }
                        val dur = attendanceDuration(h.timeIn, h.timeOut)
                        if (dur != null) {
                            Text(dur, style = MaterialTheme.typography.labelSmall, color = RepairUi.Brand, fontWeight = FontWeight.Bold)
                        } else if (h.timeOut.isNullOrBlank() && !h.timeIn.isNullOrBlank()) {
                            Text("Open", style = MaterialTheme.typography.labelSmall, color = RepairUi.Warning, fontWeight = FontWeight.Bold)
                        }
                    }
                }
            }
        } else if (hasTimeIn) {
            Text("History will appear after your first completed session.", style = MaterialTheme.typography.bodySmall, color = RepairUi.Muted)
        }
    }
}

@Composable
private fun RepairAttendanceTimeRow(label: String, value: String?, bold: Boolean = false) {
    if (value.isNullOrBlank()) return
    Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
        Text(label, style = MaterialTheme.typography.labelSmall, color = RepairUi.Muted)
        Text(value, style = MaterialTheme.typography.bodySmall, color = RepairUi.Ink, fontWeight = if (bold) FontWeight.Bold else FontWeight.SemiBold)
    }
}

@Composable
private fun RepairSummaryGrid(
    title: String,
    subtitle: String,
    summary: RepairReportsSummaryDto,
    compact: Boolean = false,
    onStatusClick: ((String) -> Unit)? = null
) {
    RepairSurfaceCard {
        RepairSectionHeading(title, subtitle)
        val firstRow = listOf(
            Triple("Waiting", summary.waiting, RepairUi.Warning to RepairUi.WarningSoft),
            Triple("Diagnosing", summary.diagnosing, RepairUi.Active to RepairUi.ActiveSoft),
            Triple("Repairing", summary.repairing, RepairUi.Brand to RepairUi.BrandSoft)
        )
        val secondRow = listOf(
            Triple("Awaiting", summary.awaitingParts, RepairUi.Caution to RepairUi.CautionSoft),
            Triple("Testing", summary.testing, RepairUi.Muted to RepairUi.SurfaceSubtle),
            Triple("Completed", summary.completed, RepairUi.Complete to RepairUi.CompleteSoft)
        )
        val thirdRow = listOf(
            Triple("Unrepairable", summary.unrepairable, RepairUi.Critical to RepairUi.CriticalSoft),
            Triple("Total", summary.total, RepairUi.Ink to RepairUi.SurfaceSubtle)
        )
        Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
            RepairSummaryGridRow(firstRow, compact, onStatusClick)
            RepairSummaryGridRow(secondRow, compact, onStatusClick)
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.fillMaxWidth()) {
                thirdRow.forEach { (label, count, colors) ->
                    RepairSummaryCell(label, count, colors.first, colors.second, compact, onClick = { onStatusClick?.invoke(label) }, modifier = Modifier.weight(1f))
                }
                if (thirdRow.size == 2) Spacer(Modifier.weight(1f))
            }
        }
    }
}

@Composable
private fun RepairSummaryGridRow(
    cells: List<Triple<String, Int, Pair<Color, Color>>>,
    compact: Boolean,
    onStatusClick: ((String) -> Unit)? = null
) {
    Row(horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.fillMaxWidth()) {
        cells.forEach { (label, count, colors) ->
            RepairSummaryCell(label, count, colors.first, colors.second, compact, onClick = { onStatusClick?.invoke(label) }, modifier = Modifier.weight(1f))
        }
    }
}

@Composable
private fun RepairSummaryCell(
    label: String,
    count: Int,
    fg: Color,
    bg: Color,
    compact: Boolean,
    onClick: (() -> Unit)? = null,
    modifier: Modifier = Modifier
) {
    Column(
        modifier = modifier
            .clip(RoundedCornerShape(if (compact) 10.dp else 12.dp))
            .background(bg)
            .then(if (onClick != null) Modifier.clickable(onClick = onClick) else Modifier)
            .padding(horizontal = 10.dp, vertical = if (compact) 10.dp else 12.dp),
        verticalArrangement = Arrangement.spacedBy(2.dp),
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        Text(
            count.toString(),
            style = if (compact) MaterialTheme.typography.titleMedium else MaterialTheme.typography.titleLarge,
            color = fg,
            fontWeight = FontWeight.Bold
        )
        Text(
            label,
            style = MaterialTheme.typography.labelSmall,
            color = RepairUi.Muted,
            fontWeight = FontWeight.SemiBold,
            maxLines = 1,
            overflow = TextOverflow.Ellipsis
        )
    }
}

@Composable
private fun RepairKpiRow(summary: RepairReportsSummaryDto) {
    val open = summary.waiting + summary.diagnosing + summary.repairing + summary.awaitingParts + summary.testing
    val done = summary.completed + summary.unrepairable
    val completionRate = if (summary.total > 0) (done * 100 / summary.total) else 0
    Row(horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.fillMaxWidth()) {
        RepairKpiCard("Open", open.toString(), "Needs action", RepairUi.Warning, Modifier.weight(1f))
        RepairKpiCard("Done", done.toString(), "$completionRate% of total", RepairUi.Complete, Modifier.weight(1f))
        RepairKpiCard("Total", summary.total.toString(), "All tickets", RepairUi.Brand, Modifier.weight(1f))
    }
}

@Composable
private fun RepairKpiCard(label: String, value: String, caption: String, color: Color, modifier: Modifier = Modifier) {
    Column(
        modifier = modifier
            .clip(RoundedCornerShape(12.dp))
            .background(RepairUi.Surface)
            .padding(12.dp),
        verticalArrangement = Arrangement.spacedBy(4.dp)
    ) {
        Row(horizontalArrangement = Arrangement.spacedBy(6.dp), verticalAlignment = Alignment.CenterVertically) {
            Icon(Icons.Filled.BarChart, null, tint = color, modifier = Modifier.size(16.dp))
            Text(label, style = MaterialTheme.typography.labelSmall, color = RepairUi.Muted, fontWeight = FontWeight.Bold)
        }
        Text(value, style = MaterialTheme.typography.titleLarge, color = color, fontWeight = FontWeight.Bold)
        Text(caption, style = MaterialTheme.typography.labelSmall, color = RepairUi.Muted, maxLines = 1, overflow = TextOverflow.Ellipsis)
    }
}

@Composable
private fun RepairRecentTicketRow(
    ticket: com.example.yakultscanner.api.RepairTicketSummaryDto,
    onClick: () -> Unit
) {
    Row(
        modifier = Modifier.fillMaxWidth().clip(RoundedCornerShape(10.dp)).clickable(onClick = onClick).padding(vertical = 4.dp),
        horizontalArrangement = Arrangement.spacedBy(10.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Box(
            modifier = Modifier.size(36.dp).clip(RoundedCornerShape(9.dp)).background(RepairUi.SurfaceSubtle),
            contentAlignment = Alignment.Center
        ) {
            Icon(Icons.Filled.History, null, tint = RepairUi.Brand, modifier = Modifier.size(18.dp))
        }
        Column(modifier = Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(2.dp)) {
            Text(
                ticket.ticketCode ?: "Repair #${ticket.repairTicketId}",
                style = MaterialTheme.typography.labelMedium,
                color = RepairUi.Brand,
                fontWeight = FontWeight.Bold,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis
            )
            Text(
                ticket.itemName?.takeIf { it.isNotBlank() } ?: "Unknown equipment",
                style = MaterialTheme.typography.bodySmall,
                color = RepairUi.Ink,
                fontWeight = FontWeight.SemiBold,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis
            )
            Row(horizontalArrangement = Arrangement.spacedBy(6.dp), verticalAlignment = Alignment.CenterVertically) {
                RepairStatusPill(ticket.status)
                ticket.priority?.let { RepairPriorityPill(it, compact = true) }
            }
        }
        Text("›", style = MaterialTheme.typography.titleLarge, color = RepairUi.Muted)
    }
}

// ── Helpers ────────────────────────────────────────────────────────────────

private fun todayDateLabel(): String =
    SimpleDateFormat("MMM d, yyyy", Locale.US).format(java.util.Date())

private fun formatAttendanceTime(iso: String?): String? {
    if (iso.isNullOrBlank()) return null
    return try {
        val patterns = listOf(
            "yyyy-MM-dd'T'HH:mm:ss.SSS'Z'",
            "yyyy-MM-dd'T'HH:mm:ss'Z'",
            "yyyy-MM-dd'T'HH:mm:ss.SSSXXX",
            "yyyy-MM-dd'T'HH:mm:ssXXX",
            "yyyy-MM-dd HH:mm:ss"
        )
        var parsed: java.util.Date? = null
        for (p in patterns) {
            parsed = try {
                val sdf = SimpleDateFormat(p, Locale.US)
                sdf.timeZone = TimeZone.getTimeZone("UTC")
                sdf.parse(iso)
            } catch (_: Exception) { null }
            if (parsed != null) break
        }
        parsed?.let {
            val out = SimpleDateFormat("h:mm a", Locale.US)
            out.timeZone = TimeZone.getDefault()
            out.format(it)
        } ?: iso
    } catch (_: Exception) { iso }
}

private fun attendanceDuration(timeInIso: String?, timeOutIso: String?): String? {
    if (timeInIso.isNullOrBlank()) return null
    return try {
        fun parse(iso: String): Long? {
            val patterns = listOf(
                "yyyy-MM-dd'T'HH:mm:ss.SSS'Z'",
                "yyyy-MM-dd'T'HH:mm:ss'Z'",
                "yyyy-MM-dd'T'HH:mm:ss.SSSXXX",
                "yyyy-MM-dd'T'HH:mm:ssXXX",
                "yyyy-MM-dd'T'HH:mm:ss",
                "yyyy-MM-dd HH:mm:ss"
            )
            for (p in patterns) {
                try {
                    val sdf = SimpleDateFormat(p, Locale.US)
                    sdf.timeZone = TimeZone.getTimeZone("UTC")
                    return sdf.parse(iso)?.time
                } catch (_: Exception) {}
            }
            return null
        }
        val start = parse(timeInIso) ?: return null
        val end = if (!timeOutIso.isNullOrBlank()) parse(timeOutIso) else System.currentTimeMillis()
        if (end == null || end < start) return null
        val diff = end - start
        val hours = TimeUnit.MILLISECONDS.toHours(diff)
        val minutes = TimeUnit.MILLISECONDS.toMinutes(diff) % 60
        when {
            hours > 0 && minutes > 0 -> "${hours}h ${minutes}m"
            hours > 0 -> "${hours}h"
            minutes > 0 -> "${minutes}m"
            else -> "<1m"
        }
    } catch (_: Exception) { null }
}
