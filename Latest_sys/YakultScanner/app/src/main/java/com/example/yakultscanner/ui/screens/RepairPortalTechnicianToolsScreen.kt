package com.example.yakultscanner.ui.screens

import android.content.ActivityNotFoundException
import android.content.Intent
import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.graphics.Color
import android.net.Uri
import android.provider.OpenableColumns
import android.util.Base64
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
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
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.AccessTime
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.CameraAlt
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.Description
import androidx.compose.material.icons.filled.Download
import androidx.compose.material.icons.filled.ExpandMore
import androidx.compose.material.icons.filled.History
import androidx.compose.material.icons.filled.Person
import androidx.compose.material.icons.filled.QrCode
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.Surface
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.rotate
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.window.DialogProperties
import androidx.core.content.FileProvider
import androidx.hilt.lifecycle.viewmodel.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.navigation.NavController
import com.example.yakultscanner.api.RepairAttachmentDto
import com.example.yakultscanner.api.RepairLookupItemDto
import com.example.yakultscanner.api.RepairObservationDto
import com.example.yakultscanner.api.RepairPartAttachmentDto
import com.example.yakultscanner.api.RepairPartDto
import com.example.yakultscanner.api.RepairTicketActionRequest
import com.example.yakultscanner.api.RepairTicketDetailResponse
import com.example.yakultscanner.api.RepairTicketEvidenceUploadRequest
import com.google.zxing.BarcodeFormat
import com.google.zxing.MultiFormatWriter
import com.example.yakultscanner.viewmodels.RepairActionUiState
import com.example.yakultscanner.viewmodels.RepairAttendanceUiState
import com.example.yakultscanner.viewmodels.RepairDetailUiState
import com.example.yakultscanner.viewmodels.RepairDownloadUiState
import com.example.yakultscanner.viewmodels.RepairEvidenceUiState
import com.example.yakultscanner.viewmodels.RepairPortalViewModel
import com.example.yakultscanner.viewmodels.RepairQrUiState
import kotlinx.coroutines.launch
import java.io.ByteArrayOutputStream
import java.io.File

private const val MAX_REPAIR_EVIDENCE_FILE_BYTES = 20 * 1024 * 1024

private data class RepairPickerRequest(
    val title: String,
    val kind: String,
    val query: String? = null,
    val companyId: Int? = null,
    val departmentId: Int? = null,
    val onSelected: (RepairLookupItemDto) -> Unit
)

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun RepairTicketTechnicianToolsScreen(
    navController: NavController,
    ticketId: Int,
    viewModel: RepairPortalViewModel = hiltViewModel()
) {
    RepairSystemBars()
    val detailState by viewModel.detailState.collectAsStateWithLifecycle()
    val actionState by viewModel.actionState.collectAsStateWithLifecycle()
    val evidenceState by viewModel.evidenceState.collectAsStateWithLifecycle()
    val downloadState by viewModel.downloadState.collectAsStateWithLifecycle()
    val attendanceState by viewModel.attendanceState.collectAsStateWithLifecycle()
    val qrState by viewModel.qrState.collectAsStateWithLifecycle()
    val snackbar = remember { SnackbarHostState() }
    val context = LocalContext.current
    val scope = rememberCoroutineScope()

    var picker by remember { mutableStateOf<RepairPickerRequest?>(null) }
    var showRequesterEditor by remember { mutableStateOf(false) }
    var showConclusionEditor by remember { mutableStateOf(false) }
    var showDispositionEditor by remember { mutableStateOf(false) }
    var showAttendance by remember { mutableStateOf(false) }
    var showQr by remember { mutableStateOf(false) }
    var partForLabel by remember { mutableStateOf<RepairPartDto?>(null) }
    var partForStatus by remember { mutableStateOf<RepairPartDto?>(null) }
    var partForNote by remember { mutableStateOf<RepairPartDto?>(null) }
    var partForDelete by remember { mutableStateOf<RepairPartDto?>(null) }
    var ticketAttachmentForDelete by remember { mutableStateOf<RepairAttachmentDto?>(null) }
    var partAttachmentForDelete by remember { mutableStateOf<RepairPartAttachmentDto?>(null) }
    var observationForEdit by remember { mutableStateOf<RepairObservationDto?>(null) }
    var observationForDelete by remember { mutableStateOf<RepairObservationDto?>(null) }
    var orderedObservations by remember { mutableStateOf<List<RepairObservationDto>>(emptyList()) }

    LaunchedEffect(ticketId) { viewModel.loadDetail(ticketId) }
    LaunchedEffect(detailState) {
        val success = detailState as? RepairDetailUiState.Success ?: return@LaunchedEffect
        orderedObservations = success.response.observations.sortedBy { it.sortOrder }
    }
    LaunchedEffect(actionState) {
        when (val state = actionState) {
            is RepairActionUiState.Success -> {
                snackbar.showSnackbar(state.message)
                if (showAttendance) viewModel.loadAttendance()
                viewModel.resetActionState()
            }
            is RepairActionUiState.Error -> {
                snackbar.showSnackbar(state.message)
                viewModel.resetActionState()
            }
            else -> Unit
        }
    }
    LaunchedEffect(evidenceState) {
        when (val state = evidenceState) {
            is RepairEvidenceUiState.Success -> {
                snackbar.showSnackbar(state.message)
                viewModel.resetEvidenceState()
            }
            is RepairEvidenceUiState.Error -> {
                snackbar.showSnackbar(state.message)
                viewModel.resetEvidenceState()
            }
            else -> Unit
        }
    }
    LaunchedEffect(downloadState) {
        when (val state = downloadState) {
            is RepairDownloadUiState.Success -> {
                try {
                    val uri = FileProvider.getUriForFile(context, "${context.packageName}.fileprovider", state.file)
                    val intent = Intent(Intent.ACTION_VIEW).apply {
                        setDataAndType(uri, state.mimeType ?: "application/octet-stream")
                        addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
                    }
                    context.startActivity(Intent.createChooser(intent, "Open repair evidence"))
                } catch (_: ActivityNotFoundException) {
                    snackbar.showSnackbar("No app is available to open this evidence file")
                } catch (_: Exception) {
                    snackbar.showSnackbar("Evidence file was saved but could not be opened")
                } finally {
                    viewModel.resetDownloadState()
                }
            }
            is RepairDownloadUiState.Error -> {
                snackbar.showSnackbar(state.message)
                viewModel.resetDownloadState()
            }
            else -> Unit
        }
    }

    fun openPicker(
        title: String,
        kind: String,
        companyId: Int? = null,
        departmentId: Int? = null,
        onSelected: (RepairLookupItemDto) -> Unit
    ) {
        picker = RepairPickerRequest(title, kind, companyId = companyId, departmentId = departmentId, onSelected = onSelected)
    }

    fun fileNameFor(uri: Uri): String {
        return try {
            context.contentResolver.query(uri, arrayOf(OpenableColumns.DISPLAY_NAME), null, null, null)?.use { cursor ->
                if (cursor.moveToFirst()) cursor.getString(cursor.getColumnIndexOrThrow(OpenableColumns.DISPLAY_NAME)) else null
            }
        } catch (_: Exception) {
            null
        } ?: "repair_evidence_${System.currentTimeMillis()}"
    }

    fun uploadTicketEvidence(uri: Uri) {
        scope.launch {
            try {
                val bytes = context.contentResolver.openInputStream(uri)?.use { input ->
                    val output = ByteArrayOutputStream()
                    val buffer = ByteArray(DEFAULT_BUFFER_SIZE)
                    while (true) {
                        val count = input.read(buffer)
                        if (count <= 0) break
                        if (output.size() + count > MAX_REPAIR_EVIDENCE_FILE_BYTES) throw IllegalArgumentException("Each evidence file must be no larger than 20 MB")
                        output.write(buffer, 0, count)
                    }
                    output.toByteArray()
                } ?: throw IllegalArgumentException("The selected evidence file could not be read")
                val mime = context.contentResolver.getType(uri) ?: "application/pdf"
                viewModel.uploadEvidence(
                    RepairTicketEvidenceUploadRequest(
                        repairTicketId = ticketId,
                        fileName = fileNameFor(uri),
                        mimeType = mime,
                        fileBase64 = Base64.encodeToString(bytes, Base64.NO_WRAP)
                    )
                )
            } catch (e: IllegalArgumentException) {
                snackbar.showSnackbar(e.message ?: "Evidence file is invalid")
            } catch (_: Exception) {
                snackbar.showSnackbar("The selected evidence file could not be read")
            }
        }
    }

    val evidencePicker = rememberLauncherForActivityResult(ActivityResultContracts.GetContent()) { uri ->
        if (uri != null) uploadTicketEvidence(uri)
    }

    Scaffold(
        containerColor = RepairUi.Canvas,
        topBar = {
            TopAppBar(
                title = {
                    Column(verticalArrangement = Arrangement.spacedBy(1.dp)) {
                        Text("Technician workspace", color = RepairUi.Ink, fontWeight = FontWeight.Bold)
                        Text("Repair controls, evidence, and lifecycle actions", style = MaterialTheme.typography.labelSmall, color = RepairUi.Muted)
                    }
                },
                navigationIcon = { IconButton(onClick = { navController.popBackStack() }) { Icon(Icons.AutoMirrored.Filled.ArrowBack, "Back", tint = RepairUi.Ink) } },
                actions = { IconButton(onClick = { viewModel.loadDetail(ticketId) }) { Icon(Icons.Filled.Refresh, "Refresh", tint = RepairUi.Ink) } },
                colors = TopAppBarDefaults.topAppBarColors(containerColor = RepairUi.Surface)
            )
        },
        snackbarHost = { SnackbarHost(snackbar) }
    ) { padding ->
        when (val current = detailState) {
            RepairDetailUiState.Idle, RepairDetailUiState.Loading -> Box(Modifier.fillMaxSize().padding(padding), contentAlignment = Alignment.Center) { CircularProgressIndicator() }
            is RepairDetailUiState.Error -> RepairToolsError(current.message) { viewModel.loadDetail(ticketId) }
            is RepairDetailUiState.Success -> {
                if (!current.response.isTechnician) {
                    Box(Modifier.fillMaxSize().padding(padding), contentAlignment = Alignment.Center) { Text("Your account is not authorized for technician repair actions.") }
                } else {
                    RepairTechnicianToolsContent(
                        modifier = Modifier.fillMaxSize().padding(padding),
                        response = current.response,
                        orderedObservations = orderedObservations,
                        onMoveObservation = { index, delta ->
                            val target = index + delta
                            if (target in orderedObservations.indices) {
                                val next = orderedObservations.toMutableList()
                                val item = next.removeAt(index)
                                next.add(target, item)
                                orderedObservations = next
                                viewModel.submitAction(
                                    RepairTicketActionRequest("reorderObservations", ticketId = ticketId, orderedObservationIds = next.map { it.observationId })
                                )
                            }
                        },
                        onStatus = { value -> viewModel.submitAction(RepairTicketActionRequest("setStatus", ticketId = ticketId, status = value)) },
                        onPriority = { value -> viewModel.submitAction(RepairTicketActionRequest("setPriority", ticketId = ticketId, priority = value)) },
                        onRequester = { showRequesterEditor = true },
                        onConclusion = { showConclusionEditor = true },
                        onAttendance = { showAttendance = true; viewModel.loadAttendance() },
                        onQr = { showQr = true; viewModel.loadQr(ticketId) },
                        onAddTicketEvidence = { evidencePicker.launch("*/*") },
                        onOpenPartEvidence = { current.response.ticket?.ticketCode?.let { navController.navigate("repair_photo_upload/${Uri.encode(it)}") } },
                        onPartLabel = { partForLabel = it },
                        onPartStatus = { partForStatus = it },
                        onPartNote = { partForNote = it },
                        onPartDelete = { partForDelete = it },
                        onObservationEdit = { observationForEdit = it },
                        onObservationDelete = { observationForDelete = it },
                        onAssignSpare = { openPicker("Select temporary spare", "spares") { item -> viewModel.submitAction(RepairTicketActionRequest("assignSpare", ticketId = ticketId, spareItemId = item.id)) } },
                        onReturnSpare = { viewModel.submitAction(RepairTicketActionRequest("returnSpare", ticketId = ticketId)) },
                        onDisposition = { showDispositionEditor = true },
                        onDownloadTicketAttachment = { attachment ->
                            val destination = evidenceDestination(context.cacheDir, attachment.fileName, attachment.attachmentId)
                            viewModel.downloadEvidence(attachmentId = attachment.attachmentId, destination = destination)
                        },
                        onDeleteTicketAttachment = { attachment ->
                            ticketAttachmentForDelete = attachment
                        },
                        onDownloadPartAttachment = { attachment ->
                            val destination = evidenceDestination(context.cacheDir, attachment.fileName, attachment.partAttachmentId)
                            viewModel.downloadEvidence(partAttachmentId = attachment.partAttachmentId, destination = destination)
                        },
                        onDeletePartAttachment = { attachment ->
                            partAttachmentForDelete = attachment
                        }
                    )
                }
            }
        }
    }

    picker?.let { request ->
        RepairLookupPickerDialog(
            request = request,
            viewModel = viewModel,
            onDismiss = { picker = null },
            onSelected = { value -> request.onSelected(value); picker = null }
        )
    }
    if (showRequesterEditor) {
        val ticket = (detailState as? RepairDetailUiState.Success)?.response?.ticket
        if (ticket != null) RepairRequesterDialog(
            initialType = ticket.requestedByType ?: "Employee",
            initialCompany = RepairLookupItemDto(ticket.requestedByCompanyId ?: 0, name = ticket.requestedByCompanyName ?: ""),
            initialBranch = RepairLookupItemDto(ticket.requestedByBranchId ?: 0, name = ticket.requestedByBranchName ?: ""),
            initialDepartment = RepairLookupItemDto(ticket.requestedByDepartmentId ?: 0, name = ticket.requestedByDepartmentName ?: ""),
            initialEmployee = RepairLookupItemDto(ticket.requestedByEmployeeId ?: 0, name = ticket.requestedByEmployeeName ?: ""),
            onDismiss = { showRequesterEditor = false },
            onPickCompany = { openPicker("Select company", "companies") { chosen -> it(chosen) } },
            onPickBranch = { companyId, select -> openPicker("Select branch", "branches", companyId) { select(it) } },
            onPickDepartment = { companyId, select -> openPicker("Select department", "departments", companyId) { select(it) } },
            onPickEmployee = { companyId, departmentId, select -> openPicker("Select employee", "employees", companyId, departmentId) { select(it) } },
            onSave = { type, companyId, branchId, departmentId, employeeId ->
                viewModel.submitAction(
                    RepairTicketActionRequest(
                        "updateRequestedBy", ticketId = ticketId, requestedByType = type,
                        requestedByCompanyId = companyId, requestedByBranchId = branchId,
                        requestedByDepartmentId = departmentId, requestedByEmployeeId = employeeId
                    )
                )
                showRequesterEditor = false
            }
        )
    }
    if (showConclusionEditor) {
        val conclusion = (detailState as? RepairDetailUiState.Success)?.response?.conclusion
        RepairConclusionEditorDialog(
            initialRootCause = conclusion?.rootCause.orEmpty(),
            initialWorkPerformed = conclusion?.workPerformed.orEmpty(),
            initialFinalOutcome = conclusion?.finalOutcome.orEmpty(),
            initialRecommendations = conclusion?.recommendations.orEmpty(),
            initialVendor = RepairLookupItemDto(conclusion?.handedOverToVendorId ?: 0, name = conclusion?.handedOverToVendorName ?: ""),
            initialRepairedBy = conclusion?.repairedBy.orEmpty(),
            onDismiss = { showConclusionEditor = false },
            onPickVendor = { select -> openPicker("Select vendor", "vendors") { select(it) } },
            onAddRepairer = { existing, select -> openPicker("Select repaired-by technician", "technicians") { chosen -> if (existing.none { it.id == chosen.id }) select(existing + chosen) } },
            onSave = { rootCause, work, outcome, recommendations, vendorId, repairers ->
                viewModel.submitAction(
                    RepairTicketActionRequest(
                        "saveConclusion", ticketId = ticketId, rootCause = rootCause, workPerformed = work,
                        finalOutcome = outcome, recommendations = recommendations,
                        handedOverToVendorId = vendorId,
                        repairedByEmployeeIds = repairers.mapNotNull { (it.employeeId ?: it.id).takeIf { id -> id > 0 } }
                    )
                )
                showConclusionEditor = false
            }
        )
    }
    if (showDispositionEditor) {
        RepairDispositionDialog(
            onDismiss = { showDispositionEditor = false },
            onPickReplacement = { select -> openPicker("Select replacement item", "items") { select(it) } },
            onSave = { disposition, replacementId, note ->
                viewModel.submitAction(RepairTicketActionRequest("setDisposition", ticketId = ticketId, disposition = disposition, replacementItemId = replacementId, note = note))
                showDispositionEditor = false
            }
        )
    }
    if (showAttendance) {
        RepairAttendanceDialog(
            state = attendanceState,
            onDismiss = { showAttendance = false },
            onTimeIn = { viewModel.submitAction(RepairTicketActionRequest("timeIn"), refreshTicketId = null) },
            onTimeOut = { viewModel.submitAction(RepairTicketActionRequest("timeOut"), refreshTicketId = null) },
            onRefresh = { viewModel.loadAttendance() }
        )
    }
    if (showQr) {
        RepairQrDialog(qrState, onDismiss = { showQr = false; viewModel.resetQrState() })
    }
    partForLabel?.let { part ->
        RepairTextEntryDialog("Edit part label", "Part label", part.customLabel.orEmpty(), onDismiss = { partForLabel = null }) { label ->
            viewModel.submitAction(RepairTicketActionRequest("updatePartLabel", ticketId = ticketId, repairPartId = part.repairPartId, label = label))
            partForLabel = null
        }
    }
    partForStatus?.let { part ->
        RepairPartStatusDialog(part, onDismiss = { partForStatus = null }) { status, note ->
            viewModel.submitAction(RepairTicketActionRequest("setPartStatus", ticketId = ticketId, repairPartId = part.repairPartId, status = status, note = note))
            partForStatus = null
        }
    }
    partForNote?.let { part ->
        RepairPartNoteDialog(part, onDismiss = { partForNote = null }) { type, note ->
            viewModel.submitAction(RepairTicketActionRequest("addPartNote", ticketId = ticketId, repairPartId = part.repairPartId, noteType = type, noteText = note))
            partForNote = null
        }
    }
    partForDelete?.let { part ->
        RepairConfirmationDialog("Delete part", "Delete ${part.displayName ?: "this part"} and its related evidence?", onDismiss = { partForDelete = null }) {
            viewModel.submitAction(RepairTicketActionRequest("deletePart", ticketId = ticketId, repairPartId = part.repairPartId))
            partForDelete = null
        }
    }
    observationForEdit?.let { observation ->
        RepairTextEntryDialog("Edit observation", "Observation", observation.text.orEmpty(), onDismiss = { observationForEdit = null }) { text ->
            viewModel.submitAction(RepairTicketActionRequest("updateObservation", ticketId = ticketId, observationId = observation.observationId, text = text))
            observationForEdit = null
        }
    }
    observationForDelete?.let { observation ->
        RepairConfirmationDialog("Delete observation", "Delete this observation?", onDismiss = { observationForDelete = null }) {
            viewModel.submitAction(RepairTicketActionRequest("deleteObservation", ticketId = ticketId, observationId = observation.observationId))
            observationForDelete = null
        }
    }
    ticketAttachmentForDelete?.let { attachment ->
        RepairConfirmationDialog("Delete evidence", "Delete ${attachment.fileName ?: "this ticket evidence file"}? This cannot be undone.", onDismiss = { ticketAttachmentForDelete = null }) {
            viewModel.submitAction(RepairTicketActionRequest("deleteAttachment", ticketId = ticketId, attachmentId = attachment.attachmentId))
            ticketAttachmentForDelete = null
        }
    }
    partAttachmentForDelete?.let { attachment ->
        RepairConfirmationDialog("Delete part evidence", "Delete ${attachment.fileName ?: "this part evidence file"}? This cannot be undone.", onDismiss = { partAttachmentForDelete = null }) {
            viewModel.submitAction(RepairTicketActionRequest("deletePartAttachment", ticketId = ticketId, partAttachmentId = attachment.partAttachmentId))
            partAttachmentForDelete = null
        }
    }
}

@Composable
private fun RepairTechnicianToolsContent(
    modifier: Modifier,
    response: RepairTicketDetailResponse,
    orderedObservations: List<RepairObservationDto>,
    onMoveObservation: (Int, Int) -> Unit,
    onStatus: (String) -> Unit,
    onPriority: (String) -> Unit,
    onRequester: () -> Unit,
    onConclusion: () -> Unit,
    onAttendance: () -> Unit,
    onQr: () -> Unit,
    onAddTicketEvidence: () -> Unit,
    onOpenPartEvidence: () -> Unit,
    onPartLabel: (RepairPartDto) -> Unit,
    onPartStatus: (RepairPartDto) -> Unit,
    onPartNote: (RepairPartDto) -> Unit,
    onPartDelete: (RepairPartDto) -> Unit,
    onObservationEdit: (RepairObservationDto) -> Unit,
    onObservationDelete: (RepairObservationDto) -> Unit,
    onAssignSpare: () -> Unit,
    onReturnSpare: () -> Unit,
    onDisposition: () -> Unit,
    onDownloadTicketAttachment: (RepairAttachmentDto) -> Unit,
    onDeleteTicketAttachment: (RepairAttachmentDto) -> Unit,
    onDownloadPartAttachment: (RepairPartAttachmentDto) -> Unit,
    onDeletePartAttachment: (RepairPartAttachmentDto) -> Unit
) {
    val ticket = response.ticket ?: return
    val evidenceCount = response.attachments.size + response.partAttachments.size
    Column(
        modifier.verticalScroll(rememberScrollState()).padding(horizontal = 16.dp, vertical = 14.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        RepairSurfaceCard {
            val asset = listOfNotNull(
                ticket.modelNumber,
                ticket.serialNumber?.takeIf { it.isNotBlank() }?.let { "S/N $it" }
            ).joinToString(" · ")
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .clip(RoundedCornerShape(14.dp))
                    .background(RepairUi.BrandSoft)
                    .padding(horizontal = 12.dp, vertical = 10.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                Column(
                    modifier = Modifier.weight(1f),
                    verticalArrangement = Arrangement.spacedBy(2.dp)
                ) {
                    Text(
                        "REPAIR TICKET",
                        style = MaterialTheme.typography.labelSmall,
                        color = RepairUi.Brand,
                        fontWeight = FontWeight.Bold
                    )
                    Text(
                        ticket.ticketCode ?: "Repair #${ticket.repairTicketId}",
                        style = MaterialTheme.typography.titleLarge,
                        color = RepairUi.Ink,
                        fontWeight = FontWeight.Bold
                    )
                }
                RepairStatusPill(ticket.status)
            }
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(horizontal = 2.dp),
                verticalArrangement = Arrangement.spacedBy(3.dp)
            ) {
                Text(
                    ticket.itemName ?: "Unknown equipment",
                    style = MaterialTheme.typography.titleMedium,
                    color = RepairUi.Ink,
                    fontWeight = FontWeight.Bold,
                    maxLines = 2
                )
                if (asset.isNotBlank()) {
                    Text(
                        asset,
                        style = MaterialTheme.typography.bodySmall,
                        color = RepairUi.Muted,
                        fontWeight = FontWeight.SemiBold
                    )
                }
            }
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(8.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                RepairPriorityPill(ticket.priority)
                ticket.requestedByEmployeeName?.takeIf { it.isNotBlank() }?.let {
                    RepairMetaText("Requested by $it", modifier = Modifier.weight(1f))
                }
            }
            ticket.problem?.takeIf { it.isNotBlank() }?.let {
                Column(
                    modifier = Modifier
                        .fillMaxWidth()
                        .clip(RoundedCornerShape(12.dp))
                        .background(RepairUi.SurfaceSubtle)
                        .padding(horizontal = 10.dp, vertical = 8.dp),
                    verticalArrangement = Arrangement.spacedBy(2.dp)
                ) {
                    Text(
                        "REPORTED PROBLEM",
                        style = MaterialTheme.typography.labelSmall,
                        color = RepairUi.Brand,
                        fontWeight = FontWeight.Bold
                    )
                    Text(
                        it,
                        style = MaterialTheme.typography.bodySmall,
                        color = RepairUi.Ink,
                        maxLines = 3
                    )
                }
            }
        }

        RepairToolCard("Workflow controls", "Move this repair safely through its lifecycle.") {
            Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                RepairCompactMenu(
                    label = "Status",
                    values = listOf("Waiting", "Diagnosing", "Repairing", "AwaitingParts", "Testing", "Completed", "Unrepairable"),
                    selectedValue = ticket.status,
                    onSelect = onStatus,
                    modifier = Modifier.weight(1f)
                )
                RepairCompactMenu(
                    label = "Priority",
                    values = listOf("Low", "Medium", "High", "Critical"),
                    selectedValue = ticket.priority,
                    onSelect = onPriority,
                    modifier = Modifier.weight(1f)
                )
            }
            RepairActionRow("Requester details", "Review or correct who requested this repair.", Icons.Filled.Person, onRequester)
            RepairActionRow("Repair conclusion", "Capture cause, work completed, handover, and outcome.", Icons.Filled.Description, onConclusion)
            Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                RepairSecondaryButton(onClick = onAttendance, modifier = Modifier.weight(1f)) { Text("Attendance") }
                RepairSecondaryButton(onClick = onQr, modifier = Modifier.weight(1.15f)) {
                    Icon(Icons.Filled.QrCode, contentDescription = null, modifier = Modifier.size(18.dp))
                    Spacer(Modifier.size(6.dp))
                    Text("Ticket QR")
                }
            }
        }

        RepairToolCard("Temporary spare", "Keep the requester productive while equipment is under repair.") {
            if (response.activeSpare == null) {
                Text("No spare is currently loaned to this requester.", style = MaterialTheme.typography.bodySmall, color = RepairUi.Muted)
                RepairPrimaryButton(onClick = onAssignSpare) { Text("Assign spare") }
            } else {
                Text(
                    "${response.activeSpare.itemName ?: "Spare equipment"} · ${response.activeSpare.serialNumber ?: "No serial"}",
                    style = MaterialTheme.typography.bodyMedium,
                    color = RepairUi.Ink,
                    fontWeight = FontWeight.SemiBold
                )
                RepairMetaText("Borrower: ${response.activeSpare.borrowerName ?: "Requester"}")
                RepairSecondaryButton(onClick = onReturnSpare) { Text("Return / unlink spare") }
            }
        }

        RepairToolCard("Unrepairable disposition", "Record the inventory outcome when this equipment cannot be repaired.") {
            val disposition = response.conclusion?.disposition
            Text(
                if (disposition.isNullOrBlank()) "No disposition recorded." else "Current disposition: $disposition",
                style = MaterialTheme.typography.bodySmall,
                color = if (disposition.isNullOrBlank()) RepairUi.Muted else RepairUi.Critical
            )
            RepairSecondaryButton(onClick = onDisposition) { Text("Discard, replace, or unlink") }
        }

        RepairTechnicianAccordionCard(
            title = "Observations (${orderedObservations.size})",
            subtitle = "Inspection notes captured during repair.",
            initiallyExpanded = orderedObservations.isNotEmpty()
        ) {
            if (orderedObservations.isEmpty()) {
                Text("No observations yet.", style = MaterialTheme.typography.bodySmall, color = RepairUi.Muted)
            } else {
                orderedObservations.forEachIndexed { index, observation ->
                    if (index > 0) HorizontalDivider(color = RepairUi.Divider)
                    RepairObservationRow(
                        index = index,
                        observation = observation,
                        canMoveUp = index > 0,
                        canMoveDown = index < orderedObservations.lastIndex,
                        onMoveUp = { onMoveObservation(index, -1) },
                        onMoveDown = { onMoveObservation(index, 1) },
                        onEdit = { onObservationEdit(observation) },
                        onDelete = { onObservationDelete(observation) }
                    )
                }
            }
        }

        RepairTechnicianAccordionCard(
            title = "Parts (${response.parts.size})",
            subtitle = "Review part status, notes, and evidence.",
            initiallyExpanded = response.parts.isNotEmpty()
        ) {
            if (response.parts.isEmpty()) {
                Text("No parts are logged. Add a part from the ticket detail screen.", style = MaterialTheme.typography.bodySmall, color = RepairUi.Muted)
            } else {
                response.parts.forEachIndexed { index, part ->
                    if (index > 0) HorizontalDivider(color = RepairUi.Divider)
                    RepairTechnicianPartRow(
                        part = part,
                        onLabel = { onPartLabel(part) },
                        onStatus = { onPartStatus(part) },
                        onNote = { onPartNote(part) },
                        onDelete = { onPartDelete(part) }
                    )
                }
            }
            RepairSecondaryButton(onClick = onOpenPartEvidence, modifier = Modifier.fillMaxWidth()) {
                Icon(Icons.Filled.CameraAlt, contentDescription = null, modifier = Modifier.size(18.dp))
                Spacer(Modifier.size(6.dp))
                Text("Upload part evidence")
            }
        }

        RepairTechnicianAccordionCard(
            title = "Evidence ($evidenceCount)",
            subtitle = "Ticket and part files attached to this repair.",
            initiallyExpanded = evidenceCount > 0
        ) {
            RepairPrimaryButton(onClick = onAddTicketEvidence, modifier = Modifier.fillMaxWidth()) {
                Icon(Icons.Filled.Add, contentDescription = null, modifier = Modifier.size(18.dp))
                Spacer(Modifier.size(6.dp))
                Text("Add ticket evidence")
            }
            if (evidenceCount == 0) {
                Text("No evidence has been attached.", style = MaterialTheme.typography.bodySmall, color = RepairUi.Muted)
            }
            response.attachments.forEach { attachment ->
                RepairEvidenceRow(
                    attachment.fileName ?: "Ticket evidence",
                    attachment.mimeType,
                    onDownload = { onDownloadTicketAttachment(attachment) },
                    onDelete = { onDeleteTicketAttachment(attachment) }
                )
            }
            response.partAttachments.forEach { attachment ->
                val part = response.parts.firstOrNull { it.repairPartId == attachment.repairPartId }
                RepairEvidenceRow(
                    "${part?.displayName ?: "Part"}: ${attachment.fileName ?: "Evidence"}",
                    attachment.mimeType,
                    onDownload = { onDownloadPartAttachment(attachment) },
                    onDelete = { onDeletePartAttachment(attachment) }
                )
            }
        }
        Spacer(Modifier.height(96.dp))
    }
}

@Composable
private fun RepairTechnicianAccordionCard(
    title: String,
    subtitle: String,
    initiallyExpanded: Boolean,
    content: @Composable ColumnScope.() -> Unit
) {
    var expanded by remember { mutableStateOf(initiallyExpanded) }
    val rotation by androidx.compose.animation.core.animateFloatAsState(if (expanded) 180f else 0f, label = "technicianAccordionChevron")
    RepairSurfaceCard {
        Row(
            modifier = Modifier.fillMaxWidth().clickable { expanded = !expanded },
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            RepairSectionHeading(title, subtitle)
            Icon(
                androidx.compose.material.icons.Icons.Filled.ExpandMore,
                contentDescription = if (expanded) "Collapse $title" else "Expand $title",
                tint = RepairUi.Muted,
                modifier = Modifier.rotate(rotation)
            )
        }
        androidx.compose.animation.AnimatedVisibility(
            visible = expanded,
            enter = androidx.compose.animation.expandVertically() + androidx.compose.animation.fadeIn(),
            exit = androidx.compose.animation.shrinkVertically() + androidx.compose.animation.fadeOut()
        ) {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                HorizontalDivider(color = RepairUi.Divider)
                content()
            }
        }
    }
}

@Composable
private fun RepairObservationRow(
    index: Int,
    observation: RepairObservationDto,
    canMoveUp: Boolean,
    canMoveDown: Boolean,
    onMoveUp: () -> Unit,
    onMoveDown: () -> Unit,
    onEdit: () -> Unit,
    onDelete: () -> Unit
) {
    Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp), verticalAlignment = Alignment.Top) {
            Box(
                modifier = Modifier.size(28.dp).clip(RoundedCornerShape(9.dp)).background(RepairUi.BrandSoft),
                contentAlignment = Alignment.Center
            ) {
                Text("${index + 1}", style = MaterialTheme.typography.labelSmall, color = RepairUi.Brand, fontWeight = FontWeight.Bold)
            }
            Column(modifier = Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(2.dp)) {
                Text(observation.text.orEmpty(), style = MaterialTheme.typography.bodySmall, color = RepairUi.Ink)
                observation.createdByName?.takeIf { it.isNotBlank() }?.let { RepairMetaText(it) }
            }
        }
        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(6.dp)) {
            RepairSecondaryButton(onClick = onMoveUp, enabled = canMoveUp, modifier = Modifier.weight(1f)) { Text("Move up") }
            RepairSecondaryButton(onClick = onMoveDown, enabled = canMoveDown, modifier = Modifier.weight(1f)) { Text("Move down") }
            RepairSecondaryButton(onClick = onEdit, modifier = Modifier.weight(1f)) { Text("Edit") }
            RepairSecondaryButton(onClick = onDelete, modifier = Modifier.weight(1f)) { Text("Delete") }
        }
    }
}

@Composable
private fun RepairTechnicianPartRow(
    part: RepairPartDto,
    onLabel: () -> Unit,
    onStatus: () -> Unit,
    onNote: () -> Unit,
    onDelete: () -> Unit
) {
    Column(
        modifier = Modifier.fillMaxWidth().clip(RoundedCornerShape(12.dp)).background(RepairUi.SurfaceSubtle).padding(12.dp),
        verticalArrangement = Arrangement.spacedBy(6.dp)
    ) {
        Text(part.displayName ?: "Part ${part.partNumber}", style = MaterialTheme.typography.bodyMedium, color = RepairUi.Ink, fontWeight = FontWeight.Bold)
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp), verticalAlignment = Alignment.CenterVertically) {
            RepairStatusPill(part.status ?: "WaitingDiagnosis")
            Text("${part.severity ?: "Medium"} severity · ${part.attachmentCount} evidence", style = MaterialTheme.typography.labelSmall, color = RepairUi.Muted)
        }
        part.problemDescription?.takeIf { it.isNotBlank() }?.let { Text(it, style = MaterialTheme.typography.bodySmall, color = RepairUi.Muted, maxLines = 3) }
        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(6.dp)) {
            RepairSecondaryButton(onClick = onLabel, modifier = Modifier.weight(1f)) { Text("Label") }
            RepairSecondaryButton(onClick = onStatus, modifier = Modifier.weight(1f)) { Text("Status") }
            RepairSecondaryButton(onClick = onNote, modifier = Modifier.weight(1f)) { Text("Note") }
            RepairSecondaryButton(onClick = onDelete, modifier = Modifier.weight(1f)) { Text("Delete") }
        }
    }
}

@Composable
private fun RepairToolCard(title: String, subtitle: String? = null, content: @Composable () -> Unit) {
    RepairSurfaceCard(tonalColor = RepairUi.Surface) {
        RepairSectionHeading(title, subtitle)
        content()
    }
}

@Composable
private fun RepairEvidenceRow(label: String, mime: String?, onDownload: () -> Unit, onDelete: () -> Unit) {
    Row(
        modifier = Modifier.fillMaxWidth().clip(RoundedCornerShape(12.dp)).background(RepairUi.SurfaceSubtle).padding(horizontal = 10.dp, vertical = 6.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Column(modifier = Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(2.dp)) {
            Text(label, style = MaterialTheme.typography.bodySmall, color = RepairUi.Ink, fontWeight = FontWeight.SemiBold, maxLines = 2)
            mime?.let { Text(it, style = MaterialTheme.typography.labelSmall, color = RepairUi.Muted) }
        }
        IconButton(onClick = onDownload) { Icon(Icons.Filled.Download, "Download evidence", tint = RepairUi.Brand) }
        IconButton(onClick = onDelete) { Icon(Icons.Filled.Delete, "Delete evidence", tint = RepairUi.Critical) }
    }
}

@Composable
private fun RepairCompactMenu(
    label: String,
    values: List<String>,
    selectedValue: String?,
    onSelect: (String) -> Unit,
    modifier: Modifier = Modifier
) {
    var expanded by remember { mutableStateOf(false) }
    Box(modifier = modifier) {
        RepairSecondaryButton(onClick = { expanded = true }, modifier = Modifier.fillMaxWidth()) {
            Column(verticalArrangement = Arrangement.spacedBy(1.dp)) {
                Text(label, style = MaterialTheme.typography.labelSmall, color = RepairUi.Muted)
                Text(
                    selectedValue?.takeIf { it.isNotBlank() }?.let { if (label == "Status") formatTechnicianStatus(it) else it } ?: "Select",
                    style = MaterialTheme.typography.bodySmall,
                    color = RepairUi.Ink,
                    fontWeight = FontWeight.Bold
                )
            }
        }
        androidx.compose.material3.DropdownMenu(expanded = expanded, onDismissRequest = { expanded = false }) {
            values.forEach { value ->
                androidx.compose.material3.DropdownMenuItem(
                    text = { Text(if (label == "Status") formatTechnicianStatus(value) else value) },
                    onClick = { expanded = false; onSelect(value) }
                )
            }
        }
    }
}

private fun formatTechnicianStatus(status: String): String = when (status) {
    "AwaitingParts" -> "Awaiting parts"
    "WaitingDiagnosis" -> "Waiting diagnosis"
    else -> status
}

@Composable
private fun RepairLookupPickerDialog(
    request: RepairPickerRequest,
    viewModel: RepairPortalViewModel,
    onDismiss: () -> Unit,
    onSelected: (RepairLookupItemDto) -> Unit
) {
    val items by viewModel.technicianLookup.collectAsStateWithLifecycle()
    val loading by viewModel.lookupLoading.collectAsStateWithLifecycle()
    var search by remember { mutableStateOf(request.query.orEmpty()) }
    LaunchedEffect(request.kind, request.companyId, request.departmentId) {
        viewModel.loadTechnicianLookup(request.kind, search.ifBlank { null }, request.companyId, request.departmentId)
    }
    AlertDialog(
        onDismissRequest = onDismiss,
        containerColor = RepairUi.Surface,
        title = { Text(request.title) },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                OutlinedTextField(
                    search,
                    { search = it; viewModel.loadTechnicianLookup(request.kind, it.ifBlank { null }, request.companyId, request.departmentId) },
                    Modifier.fillMaxWidth(),
                    label = { Text("Search") },
                    singleLine = true,
                    colors = repairTextFieldColors()
                )
                if (loading) CircularProgressIndicator(Modifier.align(Alignment.CenterHorizontally))
                Column(Modifier.heightIn(max = 300.dp).verticalScroll(rememberScrollState())) {
                    if (!loading && items.isEmpty()) Text("No matching choices.", style = MaterialTheme.typography.bodySmall)
                    items.forEach { item ->
                        Row(Modifier.fillMaxWidth().clickable { onSelected(item) }.padding(vertical = 10.dp), verticalAlignment = Alignment.CenterVertically) {
                            Column(Modifier.weight(1f)) {
                                Text(item.name.ifBlank { "Item #${item.id}" })
                                item.displayText.takeIf { it != item.name }?.let { Text(it, style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant) }
                            }
                        }
                        HorizontalDivider()
                    }
                }
            }
        },
        confirmButton = { TextButton(onClick = onDismiss) { Text("Close") } }
    )
}

@Composable
private fun RepairRequesterDialog(
    initialType: String,
    initialCompany: RepairLookupItemDto,
    initialBranch: RepairLookupItemDto,
    initialDepartment: RepairLookupItemDto,
    initialEmployee: RepairLookupItemDto,
    onDismiss: () -> Unit,
    onPickCompany: ((RepairLookupItemDto) -> Unit) -> Unit,
    onPickBranch: (Int?, (RepairLookupItemDto) -> Unit) -> Unit,
    onPickDepartment: (Int?, (RepairLookupItemDto) -> Unit) -> Unit,
    onPickEmployee: (Int?, Int?, (RepairLookupItemDto) -> Unit) -> Unit,
    onSave: (String, Int?, Int?, Int?, Int?) -> Unit
) {
    var type by remember { mutableStateOf(if (initialType.equals("Department", true)) "Department" else "Employee") }
    var company by remember { mutableStateOf(initialCompany.takeIf { it.id > 0 }) }
    var branch by remember { mutableStateOf(initialBranch.takeIf { it.id > 0 }) }
    var department by remember { mutableStateOf(initialDepartment.takeIf { it.id > 0 }) }
    var employee by remember { mutableStateOf(initialEmployee.takeIf { it.id > 0 }) }
    AlertDialog(
        onDismissRequest = onDismiss,
        containerColor = RepairUi.Surface,
        title = { Text("Edit requested by") },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.verticalScroll(rememberScrollState())) {
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    RepairSecondaryButton(onClick = { type = "Employee" }) { Text(if (type == "Employee") "✓ Employee" else "Employee") }
                    RepairSecondaryButton(onClick = { type = "Department"; employee = null }) { Text(if (type == "Department") "✓ Department" else "Department") }
                }
                RepairPickButton("Company", company?.name, onClick = { onPickCompany { company = it; branch = null; department = null; employee = null } })
                RepairPickButton("Branch (optional)", branch?.name, onClick = { onPickBranch(company?.id) { branch = it } })
                RepairPickButton("Department", department?.name, onClick = { onPickDepartment(company?.id) { department = it; employee = null } })
                if (type == "Employee") RepairPickButton("Employee", employee?.name, onClick = { onPickEmployee(company?.id, department?.id) { employee = it } })
            }
        },
        confirmButton = {
            RepairPrimaryButton(
                enabled = department != null && (type == "Department" || employee != null),
                onClick = { onSave(type, company?.id, branch?.id, department?.id, employee?.id) }
            ) { Text("Save") }
        },
        dismissButton = { TextButton(onClick = onDismiss) { Text("Cancel") } }
    )
}

@Composable
private fun RepairPickButton(label: String, selected: String?, onClick: () -> Unit) {
    RepairSecondaryButton(onClick = onClick, modifier = Modifier.fillMaxWidth()) {
        Text("$label: ${selected?.takeIf { it.isNotBlank() } ?: "Select"}")
    }
}

@Composable
private fun RepairConclusionEditorDialog(
    initialRootCause: String,
    initialWorkPerformed: String,
    initialFinalOutcome: String,
    initialRecommendations: String,
    initialVendor: RepairLookupItemDto,
    initialRepairedBy: List<RepairLookupItemDto>,
    onDismiss: () -> Unit,
    onPickVendor: ((RepairLookupItemDto) -> Unit) -> Unit,
    onAddRepairer: (List<RepairLookupItemDto>, (List<RepairLookupItemDto>) -> Unit) -> Unit,
    onSave: (String, String, String, String, Int?, List<RepairLookupItemDto>) -> Unit
) {
    var rootCause by remember { mutableStateOf(initialRootCause) }
    var work by remember { mutableStateOf(initialWorkPerformed) }
    var outcome by remember { mutableStateOf(initialFinalOutcome) }
    var recommendations by remember { mutableStateOf(initialRecommendations) }
    var vendor by remember { mutableStateOf(initialVendor.takeIf { it.id > 0 }) }
    var repairers by remember { mutableStateOf(initialRepairedBy.map { if (it.id > 0) it else it.copy(id = it.employeeId ?: 0) }.filter { it.id > 0 }) }
    AlertDialog(
        onDismissRequest = onDismiss,
        containerColor = RepairUi.Surface,
        title = { Text("Repair conclusion") },
        text = {
            Column(Modifier.heightIn(max = 480.dp).verticalScroll(rememberScrollState()), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                OutlinedTextField(rootCause, { rootCause = it }, Modifier.fillMaxWidth(), label = { Text("Root cause") }, minLines = 2, colors = repairTextFieldColors())
                OutlinedTextField(work, { work = it }, Modifier.fillMaxWidth(), label = { Text("Work performed") }, minLines = 2, colors = repairTextFieldColors())
                OutlinedTextField(outcome, { outcome = it }, Modifier.fillMaxWidth(), label = { Text("Final outcome") }, minLines = 2, colors = repairTextFieldColors())
                OutlinedTextField(recommendations, { recommendations = it }, Modifier.fillMaxWidth(), label = { Text("Recommendations") }, minLines = 2, colors = repairTextFieldColors())
                RepairPickButton("Vendor handover (optional)", vendor?.name, onClick = { onPickVendor { vendor = it } })
                Text("Repaired by", style = MaterialTheme.typography.labelLarge)
                repairers.forEach { person ->
                    Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                        Text(person.name, Modifier.weight(1f))
                        TextButton(onClick = { repairers = repairers.filterNot { it.id == person.id } }) { Text("Remove") }
                    }
                }
                RepairSecondaryButton(onClick = { onAddRepairer(repairers) { repairers = it } }) { Text("Add technician") }
            }
        },
        confirmButton = { RepairPrimaryButton(onClick = { onSave(rootCause, work, outcome, recommendations, vendor?.id, repairers) }) { Text("Save") } },
        dismissButton = { TextButton(onClick = onDismiss) { Text("Cancel") } }
    )
}

@Composable
private fun RepairDispositionDialog(
    onDismiss: () -> Unit,
    onPickReplacement: ((RepairLookupItemDto) -> Unit) -> Unit,
    onSave: (String?, Int?, String?) -> Unit
) {
    var disposition by remember { mutableStateOf<String?>("Discard") }
    var replacement by remember { mutableStateOf<RepairLookupItemDto?>(null) }
    var note by remember { mutableStateOf("") }
    AlertDialog(
        modifier = Modifier
            .fillMaxWidth(0.92f)
            .widthIn(max = 460.dp),
        properties = DialogProperties(usePlatformDefaultWidth = false),
        shape = RoundedCornerShape(28.dp),
        tonalElevation = 8.dp,
        onDismissRequest = onDismiss,
        containerColor = RepairUi.Surface,
        title = { Text("Unrepairable disposition") },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Text("This performs the shared inventory lifecycle transaction. Confirm the physical item and replacement choice before saving.", style = MaterialTheme.typography.bodySmall)
                Column(
                    modifier = Modifier.fillMaxWidth(),
                    verticalArrangement = Arrangement.spacedBy(6.dp)
                ) {
                    val choiceButtonPadding = PaddingValues(horizontal = 12.dp, vertical = 8.dp)
                    RepairSecondaryButton(
                        onClick = { disposition = "Discard" },
                        modifier = Modifier.fillMaxWidth(),
                        contentPadding = choiceButtonPadding
                    ) {
                        Text(
                            if (disposition == "Discard") "✓ Discard" else "Discard",
                            style = MaterialTheme.typography.labelMedium,
                            maxLines = 1,
                            softWrap = false
                        )
                    }
                    RepairSecondaryButton(
                        onClick = { disposition = "Replace" },
                        modifier = Modifier.fillMaxWidth(),
                        contentPadding = choiceButtonPadding
                    ) {
                        Text(
                            if (disposition == "Replace") "✓ Replace" else "Replace",
                            style = MaterialTheme.typography.labelMedium,
                            maxLines = 1,
                            softWrap = false
                        )
                    }
                    RepairSecondaryButton(
                        onClick = { disposition = null },
                        modifier = Modifier.fillMaxWidth(),
                        contentPadding = choiceButtonPadding
                    ) {
                        Text(
                            if (disposition == null) "✓ Unlink" else "Unlink",
                            style = MaterialTheme.typography.labelMedium,
                            maxLines = 1,
                            softWrap = false
                        )
                    }
                }
                if (disposition == "Replace") RepairPickButton("Replacement item", replacement?.displayText, onClick = { onPickReplacement { replacement = it } })
                OutlinedTextField(note, { note = it }, Modifier.fillMaxWidth(), label = { Text("Decision note") }, minLines = 2, colors = repairTextFieldColors())
            }
        },
        confirmButton = {
            RepairPrimaryButton(
                enabled = disposition != "Replace" || replacement != null,
                onClick = { onSave(disposition, replacement?.id, note.ifBlank { null }) }
            ) { Text("Apply") }
        },
        dismissButton = { TextButton(onClick = onDismiss) { Text("Cancel") } }
    )
}

@Composable
private fun RepairAttendanceDialog(
    state: RepairAttendanceUiState,
    onDismiss: () -> Unit,
    onTimeIn: () -> Unit,
    onTimeOut: () -> Unit,
    onRefresh: () -> Unit
) {
    AlertDialog(
        modifier = Modifier
            .fillMaxWidth(0.92f)
            .widthIn(max = 460.dp),
        properties = DialogProperties(usePlatformDefaultWidth = false),
        shape = RoundedCornerShape(28.dp),
        tonalElevation = 8.dp,
        onDismissRequest = onDismiss,
        containerColor = RepairUi.Surface,
        title = {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(12.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                Box(
                    modifier = Modifier
                        .size(46.dp)
                        .clip(RoundedCornerShape(14.dp))
                        .background(RepairUi.BrandSoft),
                    contentAlignment = Alignment.Center
                ) {
                    Icon(
                        Icons.Filled.AccessTime,
                        contentDescription = null,
                        tint = RepairUi.Brand,
                        modifier = Modifier.size(24.dp)
                    )
                }
                Column(
                    modifier = Modifier.weight(1f),
                    verticalArrangement = Arrangement.spacedBy(2.dp)
                ) {
                    Text(
                        "Repair attendance",
                        style = MaterialTheme.typography.titleLarge,
                        color = RepairUi.Ink,
                        fontWeight = FontWeight.Bold
                    )
                    Text(
                        "Track today's technician session",
                        style = MaterialTheme.typography.bodySmall,
                        color = RepairUi.Muted
                    )
                }
            }
        },
        text = {
            when (state) {
                RepairAttendanceUiState.Idle, RepairAttendanceUiState.Loading -> RepairAttendanceLoadingState()
                is RepairAttendanceUiState.Error -> RepairAttendanceErrorState(state.message)
                is RepairAttendanceUiState.Success -> RepairAttendanceSummary(state)
            }
        },
        confirmButton = {
            Column(
                modifier = Modifier.fillMaxWidth(),
                verticalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    RepairPrimaryButton(
                        onClick = onTimeIn,
                        modifier = Modifier.weight(1f)
                    ) {
                        Icon(Icons.Filled.AccessTime, contentDescription = null, modifier = Modifier.size(18.dp))
                        Spacer(Modifier.size(6.dp))
                        Text("Time in", maxLines = 1, softWrap = false)
                    }
                    RepairPrimaryButton(
                        onClick = onTimeOut,
                        modifier = Modifier.weight(1f)
                    ) {
                        Icon(Icons.Filled.CheckCircle, contentDescription = null, modifier = Modifier.size(18.dp))
                        Spacer(Modifier.size(6.dp))
                        Text("Time out", maxLines = 1, softWrap = false)
                    }
                }
                RepairSecondaryButton(
                    onClick = onRefresh,
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Icon(Icons.Filled.Refresh, contentDescription = null, modifier = Modifier.size(18.dp))
                    Spacer(Modifier.size(6.dp))
                    Text("Refresh attendance", maxLines = 1, softWrap = false)
                }
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) {
                Text("Close", color = RepairUi.Brand, fontWeight = FontWeight.SemiBold)
            }
        }
    )
}

@Composable
private fun RepairAttendanceLoadingState() {
    Surface(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(18.dp),
        color = RepairUi.SurfaceSubtle
    ) {
        Row(
            modifier = Modifier.padding(16.dp),
            horizontalArrangement = Arrangement.spacedBy(12.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            CircularProgressIndicator(
                modifier = Modifier.size(26.dp),
                color = RepairUi.Brand,
                strokeWidth = 2.5.dp
            )
            Column(verticalArrangement = Arrangement.spacedBy(2.dp)) {
                Text(
                    "Loading attendance",
                    style = MaterialTheme.typography.titleSmall,
                    color = RepairUi.Ink,
                    fontWeight = FontWeight.Bold
                )
                Text(
                    "Checking today's session and recent history.",
                    style = MaterialTheme.typography.bodySmall,
                    color = RepairUi.Muted
                )
            }
        }
    }
}

@Composable
private fun RepairAttendanceErrorState(message: String) {
    Surface(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(18.dp),
        color = RepairUi.CriticalSoft
    ) {
        Row(
            modifier = Modifier.padding(16.dp),
            horizontalArrangement = Arrangement.spacedBy(12.dp),
            verticalAlignment = Alignment.Top
        ) {
            Icon(
                Icons.Filled.Warning,
                contentDescription = null,
                tint = RepairUi.Critical,
                modifier = Modifier.size(22.dp)
            )
            Column(
                modifier = Modifier.weight(1f),
                verticalArrangement = Arrangement.spacedBy(3.dp)
            ) {
                Text(
                    "Attendance unavailable",
                    style = MaterialTheme.typography.titleSmall,
                    color = RepairUi.Critical,
                    fontWeight = FontWeight.Bold
                )
                Text(
                    message.ifBlank { "Attendance could not be loaded. Try refreshing." },
                    style = MaterialTheme.typography.bodySmall,
                    color = RepairUi.Ink
                )
            }
        }
    }
}

@Composable
private fun RepairAttendanceSummary(state: RepairAttendanceUiState.Success) {
    val today = state.response.today
    val hasTimeIn = !today?.timeIn.isNullOrBlank()
    val statusTitle = if (hasTimeIn) "Today's session recorded" else "No active session today"
    val statusDetail = if (hasTimeIn) {
        "Time in: ${today?.timeIn}"
    } else {
        "Time in to start tracking your repair session."
    }

    Column(
        modifier = Modifier.fillMaxWidth(),
        verticalArrangement = Arrangement.spacedBy(10.dp)
    ) {
        Surface(
            modifier = Modifier.fillMaxWidth(),
            shape = RoundedCornerShape(18.dp),
            color = if (hasTimeIn) RepairUi.CompleteSoft else RepairUi.SurfaceSubtle
        ) {
            Row(
                modifier = Modifier.padding(16.dp),
                horizontalArrangement = Arrangement.spacedBy(12.dp),
                verticalAlignment = Alignment.Top
            ) {
                Icon(
                    if (hasTimeIn) Icons.Filled.CheckCircle else Icons.Filled.AccessTime,
                    contentDescription = null,
                    tint = if (hasTimeIn) RepairUi.Complete else RepairUi.Brand,
                    modifier = Modifier.size(24.dp)
                )
                Column(
                    modifier = Modifier.weight(1f),
                    verticalArrangement = Arrangement.spacedBy(3.dp)
                ) {
                    Text(
                        statusTitle,
                        style = MaterialTheme.typography.titleSmall,
                        color = RepairUi.Ink,
                        fontWeight = FontWeight.Bold
                    )
                    Text(
                        statusDetail,
                        style = MaterialTheme.typography.bodySmall,
                        color = RepairUi.Muted
                    )
                    today?.timeOut?.takeIf { it.isNotBlank() }?.let {
                        Text(
                            "Time out: $it",
                            style = MaterialTheme.typography.bodySmall,
                            color = RepairUi.Muted
                        )
                    }
                }
            }
        }
        Surface(
            modifier = Modifier.fillMaxWidth(),
            shape = RoundedCornerShape(16.dp),
            color = RepairUi.BrandSoft
        ) {
            Row(
                modifier = Modifier.padding(horizontal = 14.dp, vertical = 12.dp),
                horizontalArrangement = Arrangement.spacedBy(10.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                Icon(
                    Icons.Filled.History,
                    contentDescription = null,
                    tint = RepairUi.Brand,
                    modifier = Modifier.size(20.dp)
                )
                Column(modifier = Modifier.weight(1f)) {
                    Text(
                        "Recent sessions",
                        style = MaterialTheme.typography.labelLarge,
                        color = RepairUi.Ink,
                        fontWeight = FontWeight.Bold
                    )
                    Text(
                        "Attendance history",
                        style = MaterialTheme.typography.bodySmall,
                        color = RepairUi.Muted
                    )
                }
                Text(
                    state.response.history.size.toString(),
                    style = MaterialTheme.typography.titleLarge,
                    color = RepairUi.Brand,
                    fontWeight = FontWeight.Bold
                )
            }
        }
    }
}

@Composable
private fun RepairQrDialog(state: RepairQrUiState, onDismiss: () -> Unit) {
    AlertDialog(
        modifier = Modifier
            .fillMaxWidth(0.92f)
            .widthIn(max = 460.dp),
        properties = DialogProperties(usePlatformDefaultWidth = false),
        shape = RoundedCornerShape(28.dp),
        tonalElevation = 8.dp,
        onDismissRequest = onDismiss,
        containerColor = RepairUi.Surface,
        title = { Text("Repair ticket QR") },
        text = {
            when (state) {
                RepairQrUiState.Idle, RepairQrUiState.Loading -> Box(Modifier.fillMaxWidth().height(140.dp), contentAlignment = Alignment.Center) { CircularProgressIndicator() }
                is RepairQrUiState.Error -> Text(state.message)
                is RepairQrUiState.Success -> {
                    val response = state.response
                    val qrValue = response.qrData?.takeIf { it.isNotBlank() }
                        ?: response.qrToken?.takeIf { it.isNotBlank() }
                    val qrBitmap = remember(response.imageBase64, qrValue) {
                        response.imageBase64?.let(::decodeRepairQrBitmap)
                            ?: qrValue?.let(::generateRepairQrBitmap)
                    }
                    Column(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalAlignment = Alignment.CenterHorizontally,
                        verticalArrangement = Arrangement.spacedBy(10.dp)
                    ) {
                        if (qrBitmap != null) {
                            Text(
                                "Scan to open this repair ticket",
                                style = MaterialTheme.typography.labelLarge,
                                color = RepairUi.Ink,
                                fontWeight = FontWeight.Bold
                            )
                            Surface(
                                modifier = Modifier.fillMaxWidth(),
                                shape = RoundedCornerShape(20.dp),
                                color = RepairUi.SurfaceSubtle,
                                tonalElevation = 2.dp
                            ) {
                                Box(
                                    modifier = Modifier.fillMaxWidth().padding(12.dp),
                                    contentAlignment = Alignment.Center
                                ) {
                                    Surface(
                                        shape = RoundedCornerShape(14.dp),
                                        color = RepairUi.Surface,
                                        tonalElevation = 1.dp
                                    ) {
                                        Image(
                                            bitmap = qrBitmap.asImageBitmap(),
                                            contentDescription = "Repair ticket QR code",
                                            modifier = Modifier.size(220.dp).padding(8.dp)
                                        )
                                    }
                                }
                            }
                        } else {
                            Text("QR image unavailable.", style = MaterialTheme.typography.bodySmall, color = RepairUi.Muted)
                        }
                        Text(
                            "QR token",
                            modifier = Modifier.fillMaxWidth(),
                            style = MaterialTheme.typography.labelSmall,
                            color = RepairUi.Muted,
                            fontWeight = FontWeight.Bold
                        )
                        Text(
                            response.qrToken ?: qrValue ?: "QR token unavailable",
                            modifier = Modifier.fillMaxWidth(),
                            style = MaterialTheme.typography.bodySmall,
                            color = RepairUi.Ink
                        )
                        if (response.imageOmittedForSize && qrBitmap == null) {
                            Text("QR image is stored but too large to embed on this device.", style = MaterialTheme.typography.bodySmall, color = RepairUi.Muted)
                        }
                    }
                }
            }
        },
        confirmButton = { TextButton(onClick = onDismiss) { Text("Close") } }
    )
}

private fun decodeRepairQrBitmap(encoded: String): Bitmap? = runCatching {
    val normalized = encoded.substringAfter("base64,", encoded)
    val bytes = Base64.decode(normalized, Base64.DEFAULT)
    BitmapFactory.decodeByteArray(bytes, 0, bytes.size)
}.getOrNull()

private fun generateRepairQrBitmap(value: String, size: Int = 600): Bitmap? = runCatching {
    val matrix = MultiFormatWriter().encode(value, BarcodeFormat.QR_CODE, size, size)
    Bitmap.createBitmap(size, size, Bitmap.Config.ARGB_8888).also { bitmap ->
        for (y in 0 until size) {
            for (x in 0 until size) {
                bitmap.setPixel(x, y, if (matrix.get(x, y)) Color.BLACK else Color.WHITE)
            }
        }
    }
}.getOrNull()

@Composable
private fun RepairPartStatusDialog(part: RepairPartDto, onDismiss: () -> Unit, onSave: (String, String?) -> Unit) {
    var status by remember { mutableStateOf(part.status ?: "WaitingDiagnosis") }
    var note by remember { mutableStateOf("") }
    AlertDialog(
        onDismissRequest = onDismiss,
        containerColor = RepairUi.Surface,
        title = { Text("Set ${part.displayName ?: "part"} status") },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
                listOf("WaitingDiagnosis", "Diagnosing", "Repairing", "WaitingParts", "Testing", "Repaired", "CannotRepair").forEach { value ->
                    RepairSecondaryButton(onClick = { status = value }, modifier = Modifier.fillMaxWidth()) { Text(if (status == value) "✓ $value" else value) }
                }
                OutlinedTextField(note, { note = it }, Modifier.fillMaxWidth(), label = { Text("Status note (optional)") }, minLines = 2, colors = repairTextFieldColors())
            }
        },
        confirmButton = { RepairPrimaryButton(onClick = { onSave(status, note.ifBlank { null }) }) { Text("Save") } },
        dismissButton = { TextButton(onClick = onDismiss) { Text("Cancel") } }
    )
}

@Composable
private fun RepairPartNoteDialog(part: RepairPartDto, onDismiss: () -> Unit, onSave: (String, String) -> Unit) {
    var type by remember { mutableStateOf("Diagnostic") }
    var note by remember { mutableStateOf("") }
    AlertDialog(
        onDismissRequest = onDismiss,
        containerColor = RepairUi.Surface,
        title = { Text("Add note: ${part.displayName ?: "part"}") },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    RepairSecondaryButton(onClick = { type = "Diagnostic" }) { Text(if (type == "Diagnostic") "✓ Diagnostic" else "Diagnostic") }
                    RepairSecondaryButton(onClick = { type = "Repair" }) { Text(if (type == "Repair") "✓ Repair" else "Repair") }
                }
                OutlinedTextField(note, { note = it }, Modifier.fillMaxWidth(), label = { Text("Note") }, minLines = 3)
            }
        },
        confirmButton = { RepairPrimaryButton(enabled = note.isNotBlank(), onClick = { onSave(type, note.trim()) }) { Text("Add") } },
        dismissButton = { TextButton(onClick = onDismiss) { Text("Cancel") } }
    )
}

@Composable
private fun RepairTextEntryDialog(title: String, label: String, initial: String, onDismiss: () -> Unit, onSave: (String) -> Unit) {
    var value by remember { mutableStateOf(initial) }
    AlertDialog(
        onDismissRequest = onDismiss,
        containerColor = RepairUi.Surface,
        title = { Text(title) },
        text = { OutlinedTextField(value, { value = it }, Modifier.fillMaxWidth(), label = { Text(label) }, minLines = 2, colors = repairTextFieldColors()) },
        confirmButton = { RepairPrimaryButton(enabled = value.isNotBlank(), onClick = { onSave(value.trim()) }) { Text("Save") } },
        dismissButton = { TextButton(onClick = onDismiss) { Text("Cancel") } }
    )
}

@Composable
private fun RepairConfirmationDialog(title: String, message: String, onDismiss: () -> Unit, onConfirm: () -> Unit) {
    AlertDialog(
        onDismissRequest = onDismiss,
        containerColor = RepairUi.Surface,
        title = { Text(title) },
        text = { Text(message) },
        confirmButton = { RepairPrimaryButton(onClick = onConfirm) { Text("Confirm") } },
        dismissButton = { TextButton(onClick = onDismiss) { Text("Cancel") } }
    )
}

@Composable
private fun RepairToolsError(message: String, onRetry: () -> Unit) {
    Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
        Column(horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(10.dp)) {
            Text(message)
            RepairPrimaryButton(onClick = onRetry) { Text("Retry") }
        }
    }
}

private fun evidenceDestination(cacheDir: File, sourceName: String?, attachmentId: Int): File {
    val safeName = (sourceName ?: "repair_evidence_$attachmentId.bin").substringAfterLast('/').replace(Regex("[\\\\/:*?\"<>|]"), "_")
    return File(File(cacheDir, "repair_evidence"), "${attachmentId}_$safeName")
}


