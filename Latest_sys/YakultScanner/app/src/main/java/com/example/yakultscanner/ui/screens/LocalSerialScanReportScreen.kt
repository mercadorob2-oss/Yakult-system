package com.example.yakultscanner.ui.screens

import android.widget.Toast
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
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.Assessment
import androidx.compose.material.icons.filled.CloudUpload
import androidx.compose.material.icons.filled.FileDownload
import androidx.compose.material.icons.filled.PictureAsPdf
import androidx.compose.material.icons.filled.QrCodeScanner
import androidx.compose.material.icons.filled.Save
import androidx.compose.material.icons.filled.Send
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilledIconButton
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Switch
import androidx.compose.material3.Tab
import androidx.compose.material3.TabRow
import androidx.compose.material3.Text
import androidx.compose.material3.Surface
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
import androidx.compose.runtime.setValue
import androidx.compose.ui.draw.clip
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.hilt.lifecycle.viewmodel.compose.hiltViewModel
import androidx.navigation.NavController
import androidx.navigation.compose.currentBackStackEntryAsState
import com.example.yakultscanner.SCAN_RESULT_SERIALS_KEY
import com.example.yakultscanner.SCAN_RESULT_SERIAL_KEY
import com.example.yakultscanner.SCAN_RETURN_ROUTE_KEY
import com.example.yakultscanner.SCAN_RETURN_SERIAL_ROUTE
import com.example.yakultscanner.data.db.LocalScanItemEntity
import com.example.yakultscanner.data.db.LocalScanSessionEntity
import com.example.yakultscanner.data.db.LocalScanSessionStatus
import com.example.yakultscanner.utils.TextExportUtils
import com.example.yakultscanner.utils.generateLocalScanReportPdf
import com.example.yakultscanner.utils.normalizeSerial
import com.example.yakultscanner.utils.parseNormalizedSerials
import com.example.yakultscanner.viewmodels.LocalSerialScanViewModel
import kotlinx.coroutines.launch
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

private data class LocalScanDraftRow(
    val cellPhoneNumber: String = "",
    val serialNumber: String = "",
    val imei1: String = "",
    val imei2: String = "",
    val source: String = "Manual"
)

private enum class LocalPhoneTarget(val label: String) {
    Auto("Auto"),
    Serial("Serial"),
    Imei1("IMEI 1"),
    Imei2("IMEI 2")
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun LocalSerialScanReportScreen(
    navController: NavController,
    viewModel: LocalSerialScanViewModel = hiltViewModel()
) {
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    val snackbarHostState = remember { SnackbarHostState() }
    val sessions by viewModel.sessions.collectAsState()

    var currentSessionId by remember { mutableStateOf<String?>(null) }
    var title by remember { mutableStateOf(defaultLocalScanTitle()) }
    var notes by remember { mutableStateOf("") }
    var currentSerial by remember { mutableStateOf("") }
    var serialRows by remember { mutableStateOf(listOf<String>()) }
    var phoneMode by remember { mutableStateOf(false) }
    var phoneRows by remember { mutableStateOf(listOf(LocalScanDraftRow())) }
    var phoneTarget by remember { mutableStateOf(LocalPhoneTarget.Auto) }
    var isBusy by remember { mutableStateOf(false) }
    var selectedTab by remember { mutableStateOf(0) }
    var showSaveSendConfirm by remember { mutableStateOf(false) }
    var saveSendPreviewItems by remember { mutableStateOf(emptyList<LocalScanItemEntity>()) }

    fun normalizedPhoneRows(): List<LocalScanDraftRow> = phoneRows
        .map {
            it.copy(
                cellPhoneNumber = it.cellPhoneNumber.trim(),
                serialNumber = normalizeSerial(it.serialNumber),
                imei1 = normalizeSerial(it.imei1),
                imei2 = normalizeSerial(it.imei2)
            )
        }
        .filter { it.cellPhoneNumber.isNotBlank() || it.serialNumber.isNotBlank() || it.imei1.isNotBlank() || it.imei2.isNotBlank() }

    fun allCodes(): Set<String> = if (phoneMode) {
        phoneRows.flatMap { listOf(it.serialNumber, it.imei1, it.imei2) }.map { normalizeSerial(it) }.filter { it.isNotBlank() }.toSet()
    } else {
        serialRows.map { normalizeSerial(it) }.filter { it.isNotBlank() }.toSet()
    }

    fun addSerial(raw: String, source: String = "Manual") {
        val parsed = parseNormalizedSerials(raw)
        if (parsed.isEmpty()) return
        val existing = serialRows.map { normalizeSerial(it) }.toMutableSet()
        val added = parsed.filter { existing.add(it) }
        val duplicates = parsed.size - added.size
        if (added.isEmpty()) {
            scope.launch { snackbarHostState.showSnackbar("Duplicate serial") }
            return
        }
        serialRows = serialRows + added
        currentSerial = ""
        if (duplicates > 0) {
            scope.launch { snackbarHostState.showSnackbar("Added ${added.size} serial(s), skipped $duplicates duplicate(s)") }
        }
    }

    fun addPhoneScan(raw: String, source: String = "Manual") {
        val value = normalizeSerial(raw)
        if (value.isBlank()) return
        if (allCodes().contains(value)) {
            scope.launch { snackbarHostState.showSnackbar("Duplicate serial or IMEI") }
            return
        }
        fun autoTarget(row: LocalScanDraftRow): LocalPhoneTarget? = when {
            normalizeSerial(row.serialNumber).isBlank() -> LocalPhoneTarget.Serial
            normalizeSerial(row.imei1).isBlank() -> LocalPhoneTarget.Imei1
            normalizeSerial(row.imei2).isBlank() -> LocalPhoneTarget.Imei2
            else -> null
        }
        val locked = phoneTarget.takeUnless { it == LocalPhoneTarget.Auto }
        val rowIndex = phoneRows.indexOfFirst { row ->
            when (locked ?: autoTarget(row)) {
                LocalPhoneTarget.Serial -> normalizeSerial(row.serialNumber).isBlank()
                LocalPhoneTarget.Imei1 -> normalizeSerial(row.imei1).isBlank()
                LocalPhoneTarget.Imei2 -> normalizeSerial(row.imei2).isBlank()
                LocalPhoneTarget.Auto, null -> false
            }
        }
        val target = if (rowIndex >= 0) locked ?: autoTarget(phoneRows[rowIndex]) ?: LocalPhoneTarget.Serial else locked ?: LocalPhoneTarget.Serial
        val newRow = when (target) {
            LocalPhoneTarget.Serial, LocalPhoneTarget.Auto -> LocalScanDraftRow(serialNumber = value, source = source)
            LocalPhoneTarget.Imei1 -> LocalScanDraftRow(imei1 = value, source = source)
            LocalPhoneTarget.Imei2 -> LocalScanDraftRow(imei2 = value, source = source)
        }
        phoneRows = if (rowIndex >= 0) {
            phoneRows.mapIndexed { index, row ->
                if (index != rowIndex) row else when (target) {
                    LocalPhoneTarget.Serial -> row.copy(serialNumber = value, source = source)
                    LocalPhoneTarget.Imei1 -> row.copy(imei1 = value, source = source)
                    LocalPhoneTarget.Imei2 -> row.copy(imei2 = value, source = source)
                    LocalPhoneTarget.Auto -> row
                }
            }
        } else {
            phoneRows + newRow
        }
        currentSerial = ""
    }

    fun addPhoneScans(rawValues: List<String>, source: String = "Manual") {
        val values = rawValues.map { normalizeSerial(it) }.filter { it.isNotBlank() }
        if (values.isEmpty()) return

        fun autoTarget(row: LocalScanDraftRow): LocalPhoneTarget? = when {
            normalizeSerial(row.serialNumber).isBlank() -> LocalPhoneTarget.Serial
            normalizeSerial(row.imei1).isBlank() -> LocalPhoneTarget.Imei1
            normalizeSerial(row.imei2).isBlank() -> LocalPhoneTarget.Imei2
            else -> null
        }

        val existingCodes = phoneRows
            .flatMap { listOf(it.serialNumber, it.imei1, it.imei2) }
            .map { normalizeSerial(it) }
            .filter { it.isNotBlank() }
            .toMutableSet()
        val workingRows = phoneRows.toMutableList()
        val locked = phoneTarget.takeUnless { it == LocalPhoneTarget.Auto }
        var added = 0
        var duplicates = 0

        values.forEach { value ->
            if (!existingCodes.add(value)) {
                duplicates++
                return@forEach
            }

            val rowIndex = workingRows.indexOfFirst { row ->
                when (locked ?: autoTarget(row)) {
                    LocalPhoneTarget.Serial -> normalizeSerial(row.serialNumber).isBlank()
                    LocalPhoneTarget.Imei1 -> normalizeSerial(row.imei1).isBlank()
                    LocalPhoneTarget.Imei2 -> normalizeSerial(row.imei2).isBlank()
                    LocalPhoneTarget.Auto, null -> false
                }
            }
            val target = if (rowIndex >= 0) locked ?: autoTarget(workingRows[rowIndex]) ?: LocalPhoneTarget.Serial else locked ?: LocalPhoneTarget.Serial
            val newRow = when (target) {
                LocalPhoneTarget.Serial, LocalPhoneTarget.Auto -> LocalScanDraftRow(serialNumber = value, source = source)
                LocalPhoneTarget.Imei1 -> LocalScanDraftRow(imei1 = value, source = source)
                LocalPhoneTarget.Imei2 -> LocalScanDraftRow(imei2 = value, source = source)
            }

            if (rowIndex >= 0) {
                val row = workingRows[rowIndex]
                workingRows[rowIndex] = when (target) {
                    LocalPhoneTarget.Serial -> row.copy(serialNumber = value, source = source)
                    LocalPhoneTarget.Imei1 -> row.copy(imei1 = value, source = source)
                    LocalPhoneTarget.Imei2 -> row.copy(imei2 = value, source = source)
                    LocalPhoneTarget.Auto -> row
                }
            } else {
                workingRows.add(newRow)
            }
            added++
        }

        if (added > 0) {
            phoneRows = workingRows
            currentSerial = ""
        }
        if (duplicates > 0) {
            scope.launch { snackbarHostState.showSnackbar("Skipped $duplicates duplicate serial/IMEI") }
        } else if (added == 0) {
            scope.launch { snackbarHostState.showSnackbar("Duplicate serial or IMEI") }
        }
    }

    fun localPhoneDuplicateMessage(rows: List<LocalScanDraftRow>): String? {
        data class SeenCode(val label: String, val rowNumber: Int)

        val seen = mutableMapOf<String, SeenCode>()
        rows.forEachIndexed { index, row ->
            val rowNumber = index + 1
            val values = listOf(
                "Serial Number" to normalizeSerial(row.serialNumber),
                "IMEI 1" to normalizeSerial(row.imei1),
                "IMEI 2" to normalizeSerial(row.imei2)
            )
            for ((label, value) in values) {
                if (value.isBlank()) continue
                val existing = seen[value]
                if (existing != null) {
                    return "Duplicate $label: Phone ${existing.rowNumber} ${existing.label} and Phone $rowNumber $label have the same value."
                }
                seen[value] = SeenCode(label, rowNumber)
            }
        }
        return null
    }

    fun updatePhoneRow(index: Int, updated: LocalScanDraftRow) {
        val current = phoneRows.getOrNull(index) ?: LocalScanDraftRow()
        val used = phoneRows
            .asSequence()
            .filterIndexed { rowIndex, _ -> rowIndex != index }
            .flatMap { row -> sequenceOf(row.serialNumber, row.imei1, row.imei2) }
            .map { normalizeSerial(it) }
            .filter { it.isNotBlank() }
            .toMutableSet()
        var blockedDuplicate = false

        fun keepIfUnique(raw: String, previous: String): String {
            val normalized = normalizeSerial(raw)
            if (normalized.isBlank()) return raw
            return if (used.add(normalized)) raw else {
                blockedDuplicate = true
                previous
            }
        }

        val cleaned = updated.copy(
            serialNumber = keepIfUnique(updated.serialNumber, current.serialNumber),
            imei1 = keepIfUnique(updated.imei1, current.imei1),
            imei2 = keepIfUnique(updated.imei2, current.imei2)
        )
        phoneRows = phoneRows.mapIndexed { rowIndex, row -> if (rowIndex == index) cleaned else row }
        if (blockedDuplicate) {
            scope.launch { snackbarHostState.showSnackbar("Duplicate serial or IMEI") }
        }
    }
    fun tabTarget() {
        phoneTarget = when (phoneTarget) {
            LocalPhoneTarget.Auto -> LocalPhoneTarget.Serial
            LocalPhoneTarget.Serial -> LocalPhoneTarget.Imei1
            LocalPhoneTarget.Imei1 -> LocalPhoneTarget.Imei2
            LocalPhoneTarget.Imei2 -> LocalPhoneTarget.Serial
        }
    }

    fun draftItems(sessionId: String): List<LocalScanItemEntity> {
        return if (phoneMode) {
            normalizedPhoneRows()
                .filter { it.serialNumber.isNotBlank() }
                .mapIndexed { index, row ->
                    LocalScanItemEntity(
                        sessionId = sessionId,
                        rowNumber = index + 1,
                        serialNumber = row.serialNumber,
                        cellPhoneNumber = row.cellPhoneNumber.ifBlank { null },
                        imei1 = row.imei1.ifBlank { null },
                        imei2 = row.imei2.ifBlank { null },
                        source = row.source
                    )
                }
        } else {
            serialRows.map { normalizeSerial(it) }.filter { it.isNotBlank() }.distinct().mapIndexed { index, serial ->
                LocalScanItemEntity(sessionId = sessionId, rowNumber = index + 1, serialNumber = serial)
            }
        }
    }

    fun validateDraftItems(items: List<LocalScanItemEntity>): Boolean {
        if (phoneMode) {
            localPhoneDuplicateMessage(normalizedPhoneRows())?.let { message ->
                scope.launch { snackbarHostState.showSnackbar(message) }
                return false
            }
        }
        if (items.isEmpty()) {
            scope.launch { snackbarHostState.showSnackbar("Add at least one serial") }
            return false
        }
        return true
    }

    fun requestSaveAndSendConfirmation() {
        val tempId = currentSessionId ?: java.util.UUID.randomUUID().toString()
        val items = draftItems(tempId)
        if (!validateDraftItems(items)) return
        saveSendPreviewItems = items
        showSaveSendConfirm = true
    }
    fun saveDraft(afterSave: ((String) -> Unit)? = null) {
        val tempId = currentSessionId ?: java.util.UUID.randomUUID().toString()
        val items = draftItems(tempId)
        if (!validateDraftItems(items)) return
        scope.launch {
            isBusy = true
            val savedId = viewModel.saveSession(currentSessionId, title, notes, items)
            currentSessionId = savedId
            isBusy = false
            snackbarHostState.showSnackbar("Saved ${items.size} item(s) locally")
            afterSave?.invoke(savedId)
        }
    }

    val navBackStackEntry by navController.currentBackStackEntryAsState()
    val scannedSerialFlow = remember(navBackStackEntry) {
        navBackStackEntry?.savedStateHandle?.getStateFlow<String?>(SCAN_RESULT_SERIAL_KEY, null)
    }
    val scannedSerial = scannedSerialFlow?.collectAsState(initial = null)?.value
    val scannedSerialsFlow = remember(navBackStackEntry) {
        navBackStackEntry?.savedStateHandle?.getStateFlow<java.util.ArrayList<String>?>(SCAN_RESULT_SERIALS_KEY, null)
    }
    val scannedSerials = scannedSerialsFlow?.collectAsState(initial = null)?.value

    LaunchedEffect(scannedSerial) {
        val scanned = scannedSerial?.trim().orEmpty()
        if (scanned.isBlank()) return@LaunchedEffect
        if (phoneMode) addPhoneScan(scanned, "Scanner") else addSerial(scanned, "Scanner")
        navBackStackEntry?.savedStateHandle?.remove<String>(SCAN_RESULT_SERIAL_KEY)
    }

    LaunchedEffect(scannedSerials) {
        val batch = scannedSerials?.toList().orEmpty()
        if (batch.isEmpty()) return@LaunchedEffect
        if (phoneMode) addPhoneScans(batch, "Scanner") else addSerial(batch.joinToString("\n"), "Scanner")
        navBackStackEntry?.savedStateHandle?.remove<java.util.ArrayList<String>>(SCAN_RESULT_SERIALS_KEY)
    }

    val draftCount = if (phoneMode) normalizedPhoneRows().count { it.serialNumber.isNotBlank() } else serialRows.size
    val phoneRowCount = normalizedPhoneRows().size
    val sentReports = sessions.count { it.status == LocalScanSessionStatus.SENT }

    if (showSaveSendConfirm) {
        AlertDialog(
            onDismissRequest = { showSaveSendConfirm = false },
            title = { Text("Confirm Save & Send") },
            text = {
                Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                    Text("Review ${saveSendPreviewItems.size} item(s) before sending to desktop.", style = MaterialTheme.typography.bodyMedium)
                    LazyColumn(
                        modifier = Modifier
                            .fillMaxWidth()
                            .height(320.dp),
                        verticalArrangement = Arrangement.spacedBy(8.dp)
                    ) {
                        itemsIndexed(saveSendPreviewItems) { index, item ->
                            Surface(
                                modifier = Modifier.fillMaxWidth(),
                                shape = RoundedCornerShape(12.dp),
                                color = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.55f)
                            ) {
                                Column(Modifier.padding(10.dp), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                                    Text("${index + 1}. ${item.serialNumber}", fontFamily = FontFamily.Monospace, fontWeight = FontWeight.Bold)
                                    if (!item.cellPhoneNumber.isNullOrBlank() || !item.imei1.isNullOrBlank() || !item.imei2.isNullOrBlank()) {
                                        Text(
                                            listOfNotNull(
                                                item.cellPhoneNumber?.takeIf { it.isNotBlank() }?.let { "Cell: $it" },
                                                item.imei1?.takeIf { it.isNotBlank() }?.let { "IMEI 1: $it" },
                                                item.imei2?.takeIf { it.isNotBlank() }?.let { "IMEI 2: $it" }
                                            ).joinToString("  |  "),
                                            style = MaterialTheme.typography.bodySmall,
                                            color = MaterialTheme.colorScheme.onSurfaceVariant
                                        )
                                    }
                                }
                            }
                        }
                    }
                }
            },
            confirmButton = {
                Button(onClick = {
                    showSaveSendConfirm = false
                    saveDraft { id ->
                        scope.launch {
                            val r = viewModel.sendSession(id)
                            snackbarHostState.showSnackbar("Sent ${r.successCount}, failed ${r.failedCount}")
                        }
                    }
                }) { Text("Send") }
            },
            dismissButton = {
                TextButton(onClick = { showSaveSendConfirm = false }) { Text("Cancel") }
            }
        )
    }
    Scaffold(
        snackbarHost = { SnackbarHost(snackbarHostState) }
    ) { padding ->
        LazyColumn(
            modifier = Modifier
                .fillMaxSize()
                .background(MaterialTheme.colorScheme.background)
                .navigationBarsPadding()
                .padding(padding),
            contentPadding = PaddingValues(bottom = 128.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            item {
                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .background(
                            Brush.verticalGradient(
                                listOf(
                                    MaterialTheme.colorScheme.primary,
                                    Color(0xFFB71C1C)
                                )
                            )
                        )
                        .padding(horizontal = 16.dp, vertical = 16.dp)
                ) {
                    Column(verticalArrangement = Arrangement.spacedBy(14.dp)) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            IconButton(onClick = { navController.popBackStack() }) {
                                Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back", tint = Color.White)
                            }
                            Column(Modifier.weight(1f)) {
                                Text("Local Serial Scan Report", color = Color.White, style = MaterialTheme.typography.titleLarge, fontWeight = FontWeight.Bold)
                                Text("Save now, export later, send when ready", color = Color.White.copy(alpha = 0.82f), style = MaterialTheme.typography.bodySmall)
                            }
                            Icon(Icons.Filled.Assessment, contentDescription = null, tint = Color.White.copy(alpha = 0.9f), modifier = Modifier.size(32.dp))
                        }
                        Row(horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.fillMaxWidth()) {
                            LocalMetricChip("Draft", draftCount.toString(), Modifier.weight(1f))
                            LocalMetricChip("Saved", sessions.size.toString(), Modifier.weight(1f))
                            LocalMetricChip("Sent", sentReports.toString(), Modifier.weight(1f))
                        }
                    }
                }
            }

            item {
                Surface(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(horizontal = 16.dp),
                    shape = RoundedCornerShape(18.dp),
                    tonalElevation = 2.dp,
                    shadowElevation = 1.dp
                ) {
                    TabRow(selectedTabIndex = selectedTab) {
                        Tab(
                            selected = selectedTab == 0,
                            onClick = { selectedTab = 0 },
                            text = { Text("Collect ($draftCount)") }
                        )
                        Tab(
                            selected = selectedTab == 1,
                            onClick = { selectedTab = 1 },
                            text = { Text("Saved (${sessions.size})") }
                        )
                    }
                }
            }

            if (selectedTab == 0) {
            item {
                Card(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(horizontal = 16.dp),
                    shape = RoundedCornerShape(18.dp),
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
                    elevation = CardDefaults.cardElevation(defaultElevation = 2.dp)
                ) {
                    Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Column(Modifier.weight(1f)) {
                                Text("Scan workspace", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
                                Text(if (phoneMode) "$phoneRowCount phone row(s) in progress" else "$draftCount serial(s) in progress", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                            }
                            Surface(shape = RoundedCornerShape(999.dp), color = MaterialTheme.colorScheme.primaryContainer) {
                                Text(if (phoneMode) "Phone" else "Serial", modifier = Modifier.padding(horizontal = 12.dp, vertical = 6.dp), color = MaterialTheme.colorScheme.onPrimaryContainer, style = MaterialTheme.typography.labelMedium, fontWeight = FontWeight.Bold)
                            }
                        }
                        OutlinedTextField(value = title, onValueChange = { title = it }, label = { Text("Report title") }, modifier = Modifier.fillMaxWidth(), singleLine = true, shape = RoundedCornerShape(12.dp))
                        OutlinedTextField(value = notes, onValueChange = { notes = it }, label = { Text("Notes") }, modifier = Modifier.fillMaxWidth(), maxLines = 3, shape = RoundedCornerShape(12.dp))
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Column(Modifier.weight(1f)) {
                                Text("Phone tools", fontWeight = FontWeight.SemiBold)
                                Text("Use cellphone, serial and IMEI fields", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                            }
                            Switch(checked = phoneMode, onCheckedChange = { phoneMode = it })
                        }
                        Row(horizontalArrangement = Arrangement.spacedBy(8.dp), verticalAlignment = Alignment.CenterVertically) {
                            OutlinedTextField(
                                value = currentSerial,
                                onValueChange = { currentSerial = it },
                                label = { Text(if (phoneMode) "Scan serial / IMEI" else "Scan or type serial") },
                                modifier = Modifier.weight(1f),
                                singleLine = true,
                                shape = RoundedCornerShape(12.dp)
                            )
                            FilledIconButton(onClick = { if (phoneMode) addPhoneScan(currentSerial) else addSerial(currentSerial) }, enabled = currentSerial.isNotBlank()) {
                                Icon(Icons.Filled.Add, contentDescription = "Add")
                            }
                            IconButton(onClick = {
                                navController.currentBackStackEntry?.savedStateHandle?.set(SCAN_RETURN_ROUTE_KEY, SCAN_RETURN_SERIAL_ROUTE)
                                navController.navigate("scanner_camera")
                            }) {
                                Icon(Icons.Filled.QrCodeScanner, contentDescription = "Scan", tint = MaterialTheme.colorScheme.primary)
                            }
                        }
                        if (phoneMode) {
                            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                                OutlinedButton(onClick = { tabTarget() }, shape = RoundedCornerShape(12.dp)) { Text("Tab: ${phoneTarget.label}") }
                                TextButton(onClick = { phoneRows = phoneRows + LocalScanDraftRow() }) { Text("+ Add Row") }
                            }
                        }
                    }
                }
            }

            item {
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.fillMaxWidth().padding(horizontal = 16.dp)) {
                    Button(onClick = { saveDraft() }, enabled = !isBusy, modifier = Modifier.weight(1f).height(52.dp), shape = RoundedCornerShape(14.dp)) {
                        if (isBusy) CircularProgressIndicator(Modifier.size(18.dp)) else Icon(Icons.Filled.Save, null)
                        Spacer(Modifier.width(8.dp))
                        Text("Save Local", fontWeight = FontWeight.Bold)
                    }
                    OutlinedButton(onClick = { requestSaveAndSendConfirmation() }, enabled = !isBusy, modifier = Modifier.weight(1f).height(52.dp), shape = RoundedCornerShape(14.dp)) {
                        Icon(Icons.Filled.CloudUpload, null)
                        Spacer(Modifier.width(8.dp))
                        Text("Save & Send", fontWeight = FontWeight.Bold)
                    }
                }
            }

            if (phoneMode) {
                itemsIndexed(phoneRows) { index, row ->
                    LocalPhoneRowCard(
                        index = index,
                        row = row,
                        canRemove = phoneRows.size > 1,
                        onChange = { updated -> updatePhoneRow(index, updated) },
                        onRemove = { phoneRows = phoneRows.filterIndexed { i, _ -> i != index }.ifEmpty { listOf(LocalScanDraftRow()) } }
                    )
                }
            } else if (serialRows.isEmpty()) {
                item {
                    LocalEmptyStateCard(
                        title = "No collected serials yet",
                        message = "Scan, type, or paste a serial above to begin this local report."
                    )
                }
            } else {
                itemsIndexed(serialRows) { index, serial ->
                    Card(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(horizontal = 16.dp),
                        shape = RoundedCornerShape(14.dp),
                        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
                        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
                    ) {
                        Row(Modifier.fillMaxWidth().padding(12.dp), verticalAlignment = Alignment.CenterVertically) {
                            Box(Modifier.size(26.dp).background(MaterialTheme.colorScheme.primary, CircleShape), contentAlignment = Alignment.Center) {
                                Text("${index + 1}", color = Color.White, style = MaterialTheme.typography.labelSmall)
                            }
                            Spacer(Modifier.size(10.dp))
                            Text(serial, fontFamily = FontFamily.Monospace, modifier = Modifier.weight(1f), maxLines = 1, overflow = TextOverflow.Ellipsis)
                            IconButton(onClick = { serialRows = serialRows.filter { it != serial } }) { Icon(Icons.Filled.Delete, contentDescription = "Delete") }
                        }
                    }
                }
            }

            }

            if (selectedTab == 1) {
                item {
                    Text(
                        "Collected / saved scanned items",
                        modifier = Modifier.padding(horizontal = 16.dp),
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.Bold
                    )
                }

                if (sessions.isEmpty()) {
                    item {
                        LocalEmptyStateCard(
                            title = "No saved reports yet",
                            message = "Save a local scan from the Collect tab and it will appear here for CSV, PDF, or sending."
                        )
                    }
                }

            items(sessions) { session ->
                Card(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(horizontal = 16.dp),
                    shape = RoundedCornerShape(16.dp),
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
                    elevation = CardDefaults.cardElevation(defaultElevation = 2.dp)
                ) {
                    Column(Modifier.padding(12.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Column(Modifier.weight(1f)) {
                                Text(session.title, fontWeight = FontWeight.Bold, maxLines = 1, overflow = TextOverflow.Ellipsis)
                                Text("${session.itemCount} item(s) | ${session.status} | ${formatMillis(session.updatedAt)}", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                            }
                            IconButton(onClick = { scope.launch { viewModel.deleteSession(session.id) } }) { Icon(Icons.Filled.Delete, contentDescription = "Delete") }
                        }
                        HorizontalDivider()
                        Row(horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.fillMaxWidth()) {
                            TextButton(onClick = {
                                scope.launch {
                                    val saved = viewModel.getSession(session.id) ?: return@launch
                                    val items = viewModel.getItems(session.id)
                                    currentSessionId = saved.id
                                    title = saved.title
                                    notes = saved.notes.orEmpty()
                                    phoneMode = items.any { !it.cellPhoneNumber.isNullOrBlank() || !it.imei1.isNullOrBlank() || !it.imei2.isNullOrBlank() }
                                    if (phoneMode) {
                                        phoneRows = items.map { LocalScanDraftRow(it.cellPhoneNumber.orEmpty(), it.serialNumber, it.imei1.orEmpty(), it.imei2.orEmpty(), it.source) }.ifEmpty { listOf(LocalScanDraftRow()) }
                                    } else {
                                        serialRows = items.map { it.serialNumber }
                                    }
                                    selectedTab = 0
                                }
                            }) { Text("Open") }
                            TextButton(onClick = {
                                scope.launch {
                                    val saved = viewModel.getSession(session.id) ?: return@launch
                                    val items = viewModel.getItems(session.id)
                                    val result = TextExportUtils.saveTextToDownloads(context, "LocalSerialScan_${safeFileStamp()}.csv", "text/csv", buildLocalScanCsv(saved, items))
                                    Toast.makeText(context, result.fold({ "CSV saved: $it" }, { "CSV export failed" }), Toast.LENGTH_LONG).show()
                                }
                            }) { Icon(Icons.Filled.FileDownload, null); Text("CSV") }
                            TextButton(onClick = {
                                scope.launch {
                                    val saved = viewModel.getSession(session.id) ?: return@launch
                                    val items = viewModel.getItems(session.id)
                                    generateLocalScanReportPdf(context, saved, items)
                                }
                            }) { Icon(Icons.Filled.PictureAsPdf, null); Text("PDF") }
                            TextButton(onClick = {
                                scope.launch {
                                    val r = viewModel.sendSession(session.id)
                                    snackbarHostState.showSnackbar("Sent ${r.successCount}, failed ${r.failedCount}")
                                }
                            }) { Icon(Icons.Filled.Send, null); Text("Send") }
                        }
                    }
                }
            }
            }
        }
    }
}

@Composable
private fun LocalMetricChip(
    label: String,
    value: String,
    modifier: Modifier = Modifier
) {
    Surface(
        modifier = modifier,
        shape = RoundedCornerShape(14.dp),
        color = Color.White.copy(alpha = 0.16f)
    ) {
        Column(
            modifier = Modifier.padding(horizontal = 10.dp, vertical = 8.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Text(value, color = Color.White, style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
            Text(label, color = Color.White.copy(alpha = 0.84f), style = MaterialTheme.typography.labelSmall)
        }
    }
}

@Composable
private fun LocalEmptyStateCard(
    title: String,
    message: String
) {
    Card(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 16.dp),
        shape = RoundedCornerShape(16.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant)
    ) {
        Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
            Text(title, style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.Bold)
            Text(message, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }
}

@Composable
private fun LocalPhoneRowCard(
    index: Int,
    row: LocalScanDraftRow,
    canRemove: Boolean,
    onChange: (LocalScanDraftRow) -> Unit,
    onRemove: () -> Unit
) {
    Card(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 16.dp),
        shape = RoundedCornerShape(14.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
    ) {
        Column(Modifier.padding(12.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text("Phone ${index + 1}", fontWeight = FontWeight.Bold, modifier = Modifier.weight(1f))
                if (canRemove) TextButton(onClick = onRemove) { Text("Remove") }
            }
            OutlinedTextField(value = row.cellPhoneNumber, onValueChange = { onChange(row.copy(cellPhoneNumber = it)) }, label = { Text("Cell Phone Number") }, modifier = Modifier.fillMaxWidth(), singleLine = true)
            OutlinedTextField(value = row.serialNumber, onValueChange = { onChange(row.copy(serialNumber = it)) }, label = { Text("Serial Number *") }, modifier = Modifier.fillMaxWidth(), singleLine = true)
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                OutlinedTextField(value = row.imei1, onValueChange = { onChange(row.copy(imei1 = it)) }, label = { Text("IMEI 1") }, modifier = Modifier.weight(1f), singleLine = true)
                OutlinedTextField(value = row.imei2, onValueChange = { onChange(row.copy(imei2 = it)) }, label = { Text("IMEI 2") }, modifier = Modifier.weight(1f), singleLine = true)
            }
        }
    }
}

private fun defaultLocalScanTitle(): String = "Local Scan ${formatMillis(System.currentTimeMillis())}"

private fun formatMillis(value: Long): String = SimpleDateFormat("yyyy-MM-dd HH:mm", Locale.getDefault()).format(Date(value))

private fun safeFileStamp(): String = SimpleDateFormat("yyyyMMdd_HHmmss", Locale.getDefault()).format(Date())

private fun csvCell(value: String?): String {
    val text = value.orEmpty()
    return if (text.any { it == ',' || it == '"' || it == '\n' || it == '\r' }) {
        "\"${text.replace("\"", "\"\"")}\""
    } else text
}

private fun buildLocalScanCsv(session: LocalScanSessionEntity, items: List<LocalScanItemEntity>): String {
    val rows = mutableListOf<String>()
    rows += "Title,${csvCell(session.title)}"
    rows += "Created,${csvCell(formatMillis(session.createdAt))}"
    rows += "Created By,${csvCell(session.createdBy)}"
    rows += ""
    rows += "Row,Cell Phone Number,Serial Number,IMEI 1,IMEI 2,Source,Created At,Sent,Last Error"
    items.forEach { item ->
        rows += listOf(
            item.rowNumber.toString(),
            csvCell(item.cellPhoneNumber),
            csvCell(item.serialNumber),
            csvCell(item.imei1),
            csvCell(item.imei2),
            csvCell(item.source),
            csvCell(formatMillis(item.createdAt)),
            if (item.sent) "Yes" else "No",
            csvCell(item.lastError)
        ).joinToString(",")
    }
    return rows.joinToString("\n")
}
