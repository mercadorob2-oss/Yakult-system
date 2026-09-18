package com.example.yakultscanner.ui.screens

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
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.CloudUpload
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.FileDownload
import androidx.compose.material.icons.filled.PictureAsPdf
import androidx.compose.material.icons.filled.QrCodeScanner
import androidx.compose.material.icons.filled.Save
import androidx.compose.material.icons.filled.Send
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Surface
import androidx.compose.material3.Tab
import androidx.compose.material3.TabRow
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
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.navigation.NavController
import androidx.navigation.compose.currentBackStackEntryAsState
import com.example.yakultscanner.SCAN_RESULT_SERIALS_KEY
import com.example.yakultscanner.SCAN_RESULT_SERIAL_KEY
import com.example.yakultscanner.SCAN_RETURN_ROUTE_KEY
import com.example.yakultscanner.SCAN_RETURN_SERIAL_ROUTE
import com.example.yakultscanner.data.db.LocalScanItemEntity
import com.example.yakultscanner.data.db.LocalScanSessionEntity
import com.example.yakultscanner.data.db.LocalScanSessionStatus
import com.example.yakultscanner.data.db.LocalScanSessionWithCount
import com.example.yakultscanner.data.repository.LocalScanSendResult
import com.example.yakultscanner.data.repository.LocalScanRepository
import com.example.yakultscanner.ui.components.ScannerActionBar
import com.example.yakultscanner.ui.components.ScannerEmptyState
import com.example.yakultscanner.ui.components.ScannerMetricChip
import com.example.yakultscanner.ui.components.ScannerModeSegment
import com.example.yakultscanner.ui.components.ScannerMonospaceValue
import com.example.yakultscanner.ui.components.ScannerSectionHeader
import com.example.yakultscanner.ui.components.ScannerStatusChip
import com.example.yakultscanner.ui.components.ScannerSurfaceCard
import com.example.yakultscanner.ui.components.ScannerWorkspaceHeader
import com.example.yakultscanner.ui.components.ScannerWorkspaceUi
import com.example.yakultscanner.ui.components.ScannerSpacer
import com.example.yakultscanner.utils.TextExportUtils
import com.example.yakultscanner.utils.generateLocalScanReportPdf
import com.example.yakultscanner.utils.normalizeSerial
import com.example.yakultscanner.utils.parseNormalizedSerials
import com.example.yakultscanner.viewmodels.LocalSerialScanViewModel
import kotlinx.coroutines.launch
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

private data class ModernLocalPhoneRow(
    val cellPhoneNumber: String = "",
    val serialNumber: String = "",
    val imei1: String = "",
    val imei2: String = "",
    val source: String = "Manual"
)

private enum class ModernPhoneTarget(val label: String) {
    Auto("Auto"), Serial("Serial"), Imei1("IMEI 1"), Imei2("IMEI 2")
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ModernLocalSerialScanReportScreen(
    navController: NavController,
    viewModel: LocalSerialScanViewModel = androidx.hilt.lifecycle.viewmodel.compose.hiltViewModel()
) {
    val context = androidx.compose.ui.platform.LocalContext.current
    val scope = rememberCoroutineScope()
    val snackbarHostState = remember { SnackbarHostState() }
    val sessions by viewModel.sessions.collectAsState()
    val navEntry by navController.currentBackStackEntryAsState()

    var selectedTab by rememberSaveable { mutableStateOf(0) }
    var currentSessionId by rememberSaveable { mutableStateOf<String?>(null) }
    var title by rememberSaveable { mutableStateOf(modernDefaultTitle()) }
    var notes by rememberSaveable { mutableStateOf("") }
    var phoneMode by rememberSaveable { mutableStateOf(false) }
    var currentInput by rememberSaveable { mutableStateOf("") }
    var serialRows by rememberSaveable { mutableStateOf(listOf<String>()) }
    var phoneRows by remember { mutableStateOf(listOf(ModernLocalPhoneRow())) }
    var phoneTarget by rememberSaveable { mutableStateOf(ModernPhoneTarget.Auto.name) }
    var busy by rememberSaveable { mutableStateOf(false) }
    var reviewItems by remember { mutableStateOf<List<LocalScanItemEntity>>(emptyList()) }
    var showSendReview by remember { mutableStateOf(false) }

    fun targetValue(): ModernPhoneTarget = ModernPhoneTarget.entries.firstOrNull { it.name == phoneTarget } ?: ModernPhoneTarget.Auto
    fun allCodes(): Set<String> = phoneRows.flatMap { listOf(it.serialNumber, it.imei1, it.imei2) }.map(::normalizeSerial).filter(String::isNotBlank).toSet()

    fun addSerial(raw: String, source: String = "Manual") {
        val values = parseNormalizedSerials(raw)
        if (values.isEmpty()) {
            scope.launch { snackbarHostState.showSnackbar("Enter a valid serial number.") }
            return
        }
        val existing = serialRows.map(::normalizeSerial).toMutableSet()
        val accepted = values.filter { existing.add(it) }
        val duplicates = values.size - accepted.size
        serialRows = serialRows + accepted
        currentInput = ""
        if (duplicates > 0) scope.launch { snackbarHostState.showSnackbar("Added ${accepted.size}; skipped $duplicates duplicate(s).") }
    }

    fun addPhoneValue(raw: String, source: String = "Manual") {
        val value = normalizeSerial(raw)
        if (value.isBlank()) return
        if (allCodes().contains(value)) {
            scope.launch { snackbarHostState.showSnackbar("Duplicate serial or IMEI.") }
            return
        }
        val target = targetValue()
        val rowIndex = phoneRows.indexOfFirst { row ->
            when (target) {
                ModernPhoneTarget.Auto -> row.serialNumber.isBlank() || row.imei1.isBlank() || row.imei2.isBlank()
                ModernPhoneTarget.Serial -> row.serialNumber.isBlank()
                ModernPhoneTarget.Imei1 -> row.imei1.isBlank()
                ModernPhoneTarget.Imei2 -> row.imei2.isBlank()
            }
        }
        val index = if (rowIndex >= 0) rowIndex else phoneRows.lastIndex
        val row = phoneRows.getOrElse(index) { ModernLocalPhoneRow() }
        val resolvedTarget = when (target) {
            ModernPhoneTarget.Auto -> when {
                row.serialNumber.isBlank() -> ModernPhoneTarget.Serial
                row.imei1.isBlank() -> ModernPhoneTarget.Imei1
                else -> ModernPhoneTarget.Imei2
            }
            else -> target
        }
        val updated = when (resolvedTarget) {
            ModernPhoneTarget.Serial -> row.copy(serialNumber = value, source = source)
            ModernPhoneTarget.Imei1 -> row.copy(imei1 = value, source = source)
            ModernPhoneTarget.Imei2 -> row.copy(imei2 = value, source = source)
            ModernPhoneTarget.Auto -> row
        }
        phoneRows = if (rowIndex >= 0) phoneRows.mapIndexed { i, item -> if (i == index) updated else item } else phoneRows + updated
        currentInput = ""
    }

    fun phoneDuplicateMessage(): String? {
        val seen = mutableMapOf<String, String>()
        phoneRows.forEachIndexed { index, row ->
            listOf("Serial" to row.serialNumber, "IMEI 1" to row.imei1, "IMEI 2" to row.imei2).forEach { (label, raw) ->
                val value = normalizeSerial(raw)
                if (value.isBlank()) return@forEach
                val previous = seen[value]
                if (previous != null) return "Duplicate value $value in $previous and Phone ${index + 1} $label."
                seen[value] = "Phone ${index + 1} $label"
            }
        }
        return null
    }

    fun draftItems(sessionId: String): List<LocalScanItemEntity> = if (phoneMode) {
        phoneRows.map { row ->
            LocalScanItemEntity(sessionId = sessionId, rowNumber = 0, serialNumber = normalizeSerial(row.serialNumber), cellPhoneNumber = row.cellPhoneNumber.trim().ifBlank { null }, imei1 = normalizeSerial(row.imei1).ifBlank { null }, imei2 = normalizeSerial(row.imei2).ifBlank { null }, source = row.source)
        }.filter { it.serialNumber.isNotBlank() }.mapIndexed { index, item -> item.copy(rowNumber = index + 1) }
    } else {
        serialRows.map(::normalizeSerial).filter(String::isNotBlank).distinct().mapIndexed { index, value -> LocalScanItemEntity(sessionId = sessionId, rowNumber = index + 1, serialNumber = value, source = "Manual") }
    }

    fun validate(items: List<LocalScanItemEntity>): Boolean {
        if (phoneMode) phoneDuplicateMessage()?.let { scope.launch { snackbarHostState.showSnackbar(it) }; return false }
        if (items.isEmpty()) { scope.launch { snackbarHostState.showSnackbar("Capture at least one serial before saving.") }; return false }
        return true
    }

    fun saveDraft(afterSave: ((String) -> Unit)? = null) {
        val tempId = currentSessionId ?: java.util.UUID.randomUUID().toString()
        val items = draftItems(tempId)
        if (!validate(items)) return
        scope.launch {
            busy = true
            val savedId = viewModel.saveSession(currentSessionId, title.trim().ifBlank { modernDefaultTitle() }, notes.trim().ifBlank { null }, items)
            currentSessionId = savedId
            busy = false
            snackbarHostState.showSnackbar("Saved ${items.size} item(s) locally.")
            afterSave?.invoke(savedId)
        }
    }

    fun reviewSend() {
        val tempId = currentSessionId ?: java.util.UUID.randomUUID().toString()
        val items = draftItems(tempId)
        if (validate(items)) { reviewItems = items; showSendReview = true }
    }

    fun openSession(session: LocalScanSessionWithCount) {
        scope.launch {
            val saved = viewModel.getSession(session.id) ?: return@launch
            val items = viewModel.getItems(session.id)
            currentSessionId = saved.id
            title = saved.title
            notes = saved.notes.orEmpty()
            phoneMode = items.any { !it.cellPhoneNumber.isNullOrBlank() || !it.imei1.isNullOrBlank() || !it.imei2.isNullOrBlank() }
            if (phoneMode) {
                phoneRows = items.map { ModernLocalPhoneRow(it.cellPhoneNumber.orEmpty(), it.serialNumber, it.imei1.orEmpty(), it.imei2.orEmpty(), it.source) }.ifEmpty { listOf(ModernLocalPhoneRow()) }
                serialRows = emptyList()
            } else {
                serialRows = items.map { it.serialNumber }
                phoneRows = listOf(ModernLocalPhoneRow())
            }
            selectedTab = 0
        }
    }

    fun exportSession(sessionId: String, pdf: Boolean) {
        scope.launch {
            val saved = viewModel.getSession(sessionId) ?: return@launch
            val items = viewModel.getItems(sessionId)
            if (pdf) generateLocalScanReportPdf(context, saved, items)
            else {
                val result = TextExportUtils.saveTextToDownloads(context, "LocalSerialScan_${modernFileStamp()}.csv", "text/csv", modernLocalScanCsv(saved, items))
                Toast.makeText(context, result.fold({ "CSV saved: $it" }, { "CSV export failed." }), Toast.LENGTH_LONG).show()
            }
        }
    }

    val scannedSerial = remember(navEntry) { navEntry?.savedStateHandle?.getStateFlow<String?>(SCAN_RESULT_SERIAL_KEY, null) }?.collectAsState(initial = null)?.value
    val scannedSerials = remember(navEntry) { navEntry?.savedStateHandle?.getStateFlow<java.util.ArrayList<String>?>(SCAN_RESULT_SERIALS_KEY, null) }?.collectAsState(initial = null)?.value
    LaunchedEffect(scannedSerial) { scannedSerial?.takeIf(String::isNotBlank)?.let { if (phoneMode) addPhoneValue(it, "Scanner") else addSerial(it, "Scanner") ; navEntry?.savedStateHandle?.remove<String>(SCAN_RESULT_SERIAL_KEY) } }
    LaunchedEffect(scannedSerials) { scannedSerials?.toList()?.takeIf { it.isNotEmpty() }?.let { values -> if (phoneMode) values.forEach { addPhoneValue(it, "Scanner") } else addSerial(values.joinToString("\n"), "Scanner"); navEntry?.savedStateHandle?.remove<java.util.ArrayList<String>>(SCAN_RESULT_SERIALS_KEY) } }

    val draftCount = if (phoneMode) phoneRows.count { normalizeSerial(it.serialNumber).isNotBlank() } else serialRows.size
    val sentCount = sessions.count { it.status == LocalScanSessionStatus.SENT }

    Scaffold(
        containerColor = ScannerWorkspaceUi.Canvas,
        topBar = {
            TopAppBar(
                title = { Text("Local Scan Workspace", color = ScannerWorkspaceUi.Ink, fontWeight = FontWeight.Bold) },
                navigationIcon = { IconButton(onClick = { navController.popBackStack() }) { Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back", tint = ScannerWorkspaceUi.Ink) } },
                colors = TopAppBarDefaults.topAppBarColors(containerColor = ScannerWorkspaceUi.Surface)
            )
        },
        snackbarHost = { SnackbarHost(snackbarHostState, modifier = Modifier) },
        bottomBar = {
            if (selectedTab == 0 && draftCount > 0) {
                ScannerActionBar(primaryLabel = "Save locally", onPrimary = { saveDraft() }, secondaryLabel = "Review & send", onSecondary = { reviewSend() }, primaryEnabled = !busy && draftCount > 0, modifier = Modifier)
            }
        }
    ) { padding ->
        LazyColumn(
            modifier = Modifier.fillMaxSize().background(ScannerWorkspaceUi.Canvas).padding(padding),
            contentPadding = PaddingValues(horizontal = 16.dp, vertical = 14.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            item {
                ScannerWorkspaceHeader(
                    title = "Capture, review, send",
                    subtitle = "Keep field serials local until you are ready to export or send them to desktop.",
                    trailing = { ScannerMetricChip("Ready", draftCount.toString()) }
                )
            }
            item {
                Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    ScannerMetricChip("Current", draftCount.toString(), Modifier.weight(1f))
                    ScannerMetricChip("Saved", sessions.size.toString(), Modifier.weight(1f))
                    ScannerMetricChip("Sent", sentCount.toString(), Modifier.weight(1f))
                }
            }
            item {
                Surface(modifier = Modifier.fillMaxWidth(), shape = RoundedCornerShape(16.dp), color = ScannerWorkspaceUi.Surface) {
                    TabRow(selectedTabIndex = selectedTab, containerColor = Color.Transparent, contentColor = ScannerWorkspaceUi.Brand) {
                        Tab(selected = selectedTab == 0, onClick = { selectedTab = 0 }, text = { Text("Capture") })
                        Tab(selected = selectedTab == 1, onClick = { selectedTab = 1 }, text = { Text("Saved sessions (${sessions.size})") })
                    }
                }
            }
            if (selectedTab == 0) {
                item {
                    ScannerSurfaceCard {
                        ScannerSectionHeader("Session details", "Name the work so it is easy to find later.")
                        OutlinedTextField(value = title, onValueChange = { title = it }, modifier = Modifier.fillMaxWidth(), singleLine = true, label = { Text("Session title") }, shape = RoundedCornerShape(16.dp))
                        OutlinedTextField(value = notes, onValueChange = { notes = it }, modifier = Modifier.fillMaxWidth(), minLines = 2, maxLines = 4, label = { Text("Notes (optional)") }, shape = RoundedCornerShape(16.dp))
                    }
                }
                item {
                    ScannerSurfaceCard {
                        ScannerSectionHeader("Capture mode", "Choose the shape of the data you are collecting.")
                        ScannerModeSegment("Serial list", "Phone / IMEI", !phoneMode, { phoneMode = false }, { phoneMode = true })
                        Row(horizontalArrangement = Arrangement.spacedBy(8.dp), verticalAlignment = Alignment.CenterVertically) {
                            OutlinedTextField(value = currentInput, onValueChange = { currentInput = it }, modifier = Modifier.weight(1f), label = { Text(if (phoneMode) "Scan serial or IMEI" else "Scan or type serial") }, singleLine = true, keyboardOptions = KeyboardOptions(imeAction = ImeAction.Done), keyboardActions = KeyboardActions(onDone = { if (phoneMode) addPhoneValue(currentInput) else addSerial(currentInput) }), shape = RoundedCornerShape(16.dp))
                            IconButton(onClick = { if (phoneMode) addPhoneValue(currentInput) else addSerial(currentInput) }, enabled = currentInput.isNotBlank()) { Icon(Icons.Filled.Add, contentDescription = "Add captured value", tint = ScannerWorkspaceUi.Brand) }
                            IconButton(onClick = { navController.currentBackStackEntry?.savedStateHandle?.set(SCAN_RETURN_ROUTE_KEY, SCAN_RETURN_SERIAL_ROUTE); navController.navigate("scanner_camera") }) { Icon(Icons.Filled.QrCodeScanner, contentDescription = "Scan with camera", tint = ScannerWorkspaceUi.Brand) }
                        }
                        if (phoneMode) {
                            Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
                                Text("Next target: ${targetValue().label}", style = MaterialTheme.typography.labelMedium, color = ScannerWorkspaceUi.Muted)
                                Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                                    TextButton(onClick = { phoneTarget = ModernPhoneTarget.entries[(ModernPhoneTarget.entries.indexOf(targetValue()) + 1) % ModernPhoneTarget.entries.size].name }) { Text("Change target") }
                                    TextButton(onClick = { phoneRows = phoneRows + ModernLocalPhoneRow() }) { Text("+ Row") }
                                }
                            }
                        }
                    }
                }
                item {
                    ScannerSectionHeader("Captured queue", if (phoneMode) "$draftCount phone row(s) ready" else "$draftCount serial(s) ready")
                }
                if (phoneMode) {
                    itemsIndexed(phoneRows) { index, row ->
                        ModernPhoneRowCard(index, row, phoneRows.size > 1, { updated -> phoneRows = phoneRows.mapIndexed { i, item -> if (i == index) updated else item } }, { phoneRows = phoneRows.filterIndexed { i, _ -> i != index }.ifEmpty { listOf(ModernLocalPhoneRow()) } })
                    }
                } else if (serialRows.isEmpty()) {
                    item { ScannerEmptyState("Nothing captured yet", "Use the camera, hardware scanner, or paste a serial above to begin.") }
                } else {
                    itemsIndexed(serialRows) { index, serial ->
                        ScannerSurfaceCard {
                            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                                ScannerMetricChip("#${index + 1}", "", Modifier.width(54.dp))
                                ScannerMonospaceValue(serial, Modifier.weight(1f))
                                IconButton(onClick = { serialRows = serialRows.filterIndexed { i, _ -> i != index } }) { Icon(Icons.Filled.Delete, contentDescription = "Remove serial", tint = ScannerWorkspaceUi.Error) }
                            }
                        }
                    }
                }
                item { Spacer(Modifier.height(24.dp)) }
            } else {
                if (sessions.isEmpty()) item { ScannerEmptyState("No saved sessions", "Save a local capture and it will appear here for reopening, export, or sending.") }
                items(sessions, key = { it.id }) { session -> ModernSavedSessionCard(session, onOpen = { openSession(session) }, onCsv = { exportSession(session.id, false) }, onPdf = { exportSession(session.id, true) }, onSend = { scope.launch { val result = viewModel.sendSession(session.id); snackbarHostState.showSnackbar("Sent ${result.successCount}; failed ${result.failedCount}.") } }, onDelete = { scope.launch { viewModel.deleteSession(session.id); snackbarHostState.showSnackbar("Session deleted.") } }) }
                item { Spacer(Modifier.height(24.dp)) }
            }
        }
    }

    if (showSendReview) {
        ModalBottomSheet(onDismissRequest = { showSendReview = false }, containerColor = ScannerWorkspaceUi.Surface) {
            Column(modifier = Modifier.fillMaxWidth().padding(horizontal = 20.dp, vertical = 8.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                ScannerSectionHeader("Review before sending", "Only the listed serial rows will be sent to desktop.")
                Text("${reviewItems.size} item(s) selected", style = MaterialTheme.typography.labelLarge, color = ScannerWorkspaceUi.Brand, fontWeight = FontWeight.Bold)
                LazyColumn(modifier = Modifier.fillMaxWidth().height(280.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    itemsIndexed(reviewItems) { index, item -> Surface(modifier = Modifier.fillMaxWidth(), shape = RoundedCornerShape(12.dp), color = ScannerWorkspaceUi.SurfaceSubtle) { Row(modifier = Modifier.padding(12.dp), horizontalArrangement = Arrangement.spacedBy(10.dp)) { Text("${index + 1}", color = ScannerWorkspaceUi.Brand, fontWeight = FontWeight.Bold); ScannerMonospaceValue(item.serialNumber, Modifier.weight(1f)) } } }
                }
                Button(onClick = { showSendReview = false; saveDraft { id -> scope.launch { val result = viewModel.sendSession(id); snackbarHostState.showSnackbar("Sent ${result.successCount}; failed ${result.failedCount}.") } } }, enabled = !busy, modifier = Modifier.fillMaxWidth().height(52.dp), shape = RoundedCornerShape(16.dp)) { Icon(Icons.Filled.Send, contentDescription = null); Spacer(Modifier.width(8.dp)); Text("Confirm & send", fontWeight = FontWeight.Bold) }
                Spacer(Modifier.height(20.dp))
            }
        }
    }
}

@Composable
private fun ModernPhoneRowCard(index: Int, row: ModernLocalPhoneRow, canRemove: Boolean, onChange: (ModernLocalPhoneRow) -> Unit, onRemove: () -> Unit) {
    ScannerSurfaceCard {
        Row(verticalAlignment = Alignment.CenterVertically) {
            ScannerSectionHeader("Phone ${index + 1}", "Complete the serial and IMEI fields when available.", Modifier.weight(1f))
            if (canRemove) TextButton(onClick = onRemove) { Text("Remove", color = ScannerWorkspaceUi.Error) }
        }
        OutlinedTextField(value = row.cellPhoneNumber, onValueChange = { onChange(row.copy(cellPhoneNumber = it)) }, modifier = Modifier.fillMaxWidth(), singleLine = true, label = { Text("Cell phone number") }, shape = RoundedCornerShape(16.dp))
        OutlinedTextField(value = row.serialNumber, onValueChange = { onChange(row.copy(serialNumber = it)) }, modifier = Modifier.fillMaxWidth(), singleLine = true, label = { Text("Serial number *") }, shape = RoundedCornerShape(16.dp))
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            OutlinedTextField(value = row.imei1, onValueChange = { onChange(row.copy(imei1 = it)) }, modifier = Modifier.weight(1f), singleLine = true, label = { Text("IMEI 1") }, shape = RoundedCornerShape(16.dp))
            OutlinedTextField(value = row.imei2, onValueChange = { onChange(row.copy(imei2 = it)) }, modifier = Modifier.weight(1f), singleLine = true, label = { Text("IMEI 2") }, shape = RoundedCornerShape(16.dp))
        }
    }
}

@Composable
private fun ModernSavedSessionCard(session: LocalScanSessionWithCount, onOpen: () -> Unit, onCsv: () -> Unit, onPdf: () -> Unit, onSend: () -> Unit, onDelete: () -> Unit) {
    ScannerSurfaceCard {
        Row(verticalAlignment = Alignment.Top) {
            Column(modifier = Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                Text(session.title, style = MaterialTheme.typography.titleMedium, color = ScannerWorkspaceUi.Ink, fontWeight = FontWeight.Bold, maxLines = 1, overflow = TextOverflow.Ellipsis)
                Text("${session.itemCount} item(s) · updated ${modernFormatMillis(session.updatedAt)}", style = MaterialTheme.typography.bodySmall, color = ScannerWorkspaceUi.Muted)
            }
            ScannerStatusChip(session.status.name)
        }
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.fillMaxWidth()) {
            OutlinedButton(onClick = onOpen, modifier = Modifier.weight(1f)) { Text("Open") }
            IconButton(onClick = onDelete) { Icon(Icons.Filled.Delete, contentDescription = "Delete session", tint = ScannerWorkspaceUi.Error) }
        }
        Row(horizontalArrangement = Arrangement.spacedBy(4.dp), modifier = Modifier.fillMaxWidth()) {
            TextButton(onClick = onCsv) { Icon(Icons.Filled.FileDownload, contentDescription = null); Text("CSV") }
            TextButton(onClick = onPdf) { Icon(Icons.Filled.PictureAsPdf, contentDescription = null); Text("PDF") }
            TextButton(onClick = onSend) { Icon(Icons.Filled.CloudUpload, contentDescription = null); Text("Send") }
        }
    }
}

private fun modernDefaultTitle(): String = "Local Scan ${modernFormatMillis(System.currentTimeMillis())}"
private fun modernFormatMillis(value: Long): String = SimpleDateFormat("yyyy-MM-dd HH:mm", Locale.getDefault()).format(Date(value))
private fun modernFileStamp(): String = SimpleDateFormat("yyyyMMdd_HHmmss", Locale.getDefault()).format(Date())
private fun modernCsvCell(value: String?): String { val text = value.orEmpty(); return if (text.any { it == ',' || it == '"' || it == '\n' || it == '\r' }) "\"${text.replace("\"", "\"\"")}\"" else text }
private fun modernLocalScanCsv(session: LocalScanSessionEntity, items: List<LocalScanItemEntity>): String {
    val rows = mutableListOf("Title,${modernCsvCell(session.title)}", "Created,${modernCsvCell(modernFormatMillis(session.createdAt))}", "Created By,${modernCsvCell(session.createdBy)}", "", "Row,Cell Phone Number,Serial Number,IMEI 1,IMEI 2,Source,Created At,Sent,Last Error")
    items.forEach { item -> rows += listOf(item.rowNumber.toString(), modernCsvCell(item.cellPhoneNumber), modernCsvCell(item.serialNumber), modernCsvCell(item.imei1), modernCsvCell(item.imei2), modernCsvCell(item.source), modernCsvCell(modernFormatMillis(item.createdAt)), if (item.sent) "Yes" else "No", modernCsvCell(item.lastError)).joinToString(",") }
    return rows.joinToString("\n")
}