package com.example.yakultscanner.ui.screens

import android.content.ClipData
import android.content.ClipboardManager
import android.content.Intent
import android.provider.Settings
import android.widget.Toast
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.Assignment
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.FileDownload
import androidx.compose.material.icons.filled.Folder
import androidx.compose.material.icons.filled.Print
import androidx.compose.material.icons.filled.QrCodeScanner
import androidx.compose.material3.Button
import androidx.compose.material3.FilterChip
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
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
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.saveable.listSaver
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.navigation.NavController
import androidx.navigation.compose.currentBackStackEntryAsState
import com.example.yakultscanner.SCAN_RESULT_SERIAL_KEY
import com.example.yakultscanner.SCAN_RETURN_ROUTE_KEY
import com.example.yakultscanner.SCAN_RETURN_SERIAL_ROUTE
import com.example.yakultscanner.data.model.TransmittalItemDraft
import com.example.yakultscanner.data.model.TransmittalItemMode
import com.example.yakultscanner.data.model.TransmittalReport
import com.example.yakultscanner.data.model.TransmittalScanTarget
import com.example.yakultscanner.ui.components.ScannerActionBar
import com.example.yakultscanner.ui.components.ScannerMetricChip
import com.example.yakultscanner.ui.components.ScannerSectionHeader
import com.example.yakultscanner.ui.components.ScannerSurfaceCard
import com.example.yakultscanner.ui.components.ScannerWorkspaceHeader
import com.example.yakultscanner.ui.components.ScannerWorkspaceUi
import com.example.yakultscanner.ui.adaptive.LocalAdaptiveLayout
import com.example.yakultscanner.utils.exportTransmittalWorkbook
import com.example.yakultscanner.utils.openTransmittalWorkbook
import com.example.yakultscanner.utils.createTransmittalPrintPdf
import com.example.yakultscanner.utils.printTransmittalPdf
import com.example.yakultscanner.utils.shareTransmittalPrintPdf
import com.example.yakultscanner.utils.TransmittalDocumentStore
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.util.Locale

private const val MAX_TRANSMITTAL_ITEMS = TransmittalReport.MAX_BATCH_SIZE
private const val TRANSMITTAL_PRINTER_PREFS = "transmittal_printer_preferences"
private const val DIRECT_PRINTER_HOST_KEY = "direct_printer_host"

private val transmittalItemSaver = listSaver<List<TransmittalItemDraft>, String>(
    save = { items -> items.flatMap { listOf(it.description, it.mobileNumber, it.serialNumber, it.imei1, it.imei2) } },
    restore = { values -> values.chunked(5).map { chunk -> TransmittalItemDraft(chunk[0], chunk[1], chunk[2], chunk[3], chunk[4]) } }
)

@OptIn(androidx.compose.material3.ExperimentalMaterial3Api::class)
@Composable
fun TransmittalScanScreen(navController: NavController) {
    val context = androidx.compose.ui.platform.LocalContext.current
    val scope = rememberCoroutineScope()
    val snackbarHostState = remember { SnackbarHostState() }
    val navEntry by navController.currentBackStackEntryAsState()

    var to by rememberSaveable { mutableStateOf("") }
    var from by rememberSaveable { mutableStateOf("") }
    var reportDate by rememberSaveable { mutableStateOf("") }
    var preparedBy by rememberSaveable { mutableStateOf("") }
    var transmitBy by rememberSaveable { mutableStateOf("") }
    var receivedByDate by rememberSaveable { mutableStateOf("") }
    var notedBy by rememberSaveable { mutableStateOf("") }
    var approvedByDate by rememberSaveable { mutableStateOf("") }
    var itemModeName by rememberSaveable { mutableStateOf(TransmittalItemMode.Standard.name) }
    var scanTargetName by rememberSaveable { mutableStateOf(TransmittalScanTarget.Serial.name) }
    var selectedItemIndex by rememberSaveable { mutableIntStateOf(0) }
    var pendingScanItemIndex by rememberSaveable { mutableIntStateOf(0) }
    var pendingScanValue by rememberSaveable { mutableStateOf("") }
    var pendingScanSource by rememberSaveable { mutableStateOf("") }
    var pendingScanTargetName by rememberSaveable { mutableStateOf(TransmittalScanTarget.Serial.name) }
    var sharedDescription by rememberSaveable { mutableStateOf("") }
    var showSharedDescriptionDialog by rememberSaveable { mutableStateOf(false) }
    var manualScanValue by rememberSaveable { mutableStateOf("") }
    var busy by rememberSaveable { mutableStateOf(false) }
    var showPrinterSourceMenu by rememberSaveable { mutableStateOf(false) }
    var showDirectIpDialog by rememberSaveable { mutableStateOf(false) }
    var directPrinterHost by rememberSaveable { mutableStateOf("") }
    var exportedWorkbook by remember { mutableStateOf<com.example.yakultscanner.utils.SavedTransmittalWorkbook?>(null) }
    var showWorkbookHandoff by rememberSaveable { mutableStateOf(false) }
    var showExcelPreview by rememberSaveable { mutableStateOf(true) }
    var showBatchDialog by rememberSaveable { mutableStateOf(false) }
    var addValueNeedsDescription by rememberSaveable { mutableStateOf(false) }
    var batchQuantityText by rememberSaveable { mutableStateOf("1") }
    var batchDescriptionText by rememberSaveable { mutableStateOf("") }
    var activeBatchDescription by rememberSaveable { mutableStateOf("") }
    var items by rememberSaveable(stateSaver = transmittalItemSaver) {
        mutableStateOf(listOf(TransmittalItemDraft()))
    }

    fun selectedMode(): TransmittalItemMode =
        TransmittalItemMode.entries.firstOrNull { it.name == itemModeName } ?: TransmittalItemMode.Standard

    fun selectedTarget(): TransmittalScanTarget {
        val target = TransmittalScanTarget.entries.firstOrNull { it.name == scanTargetName }
            ?: TransmittalScanTarget.Serial
        return if (selectedMode() == TransmittalItemMode.Standard && target !in listOf(TransmittalScanTarget.Auto, TransmittalScanTarget.Serial)) {
            TransmittalScanTarget.Serial
        } else {
            target
        }
    }

    fun targetOptions(): List<TransmittalScanTarget> = when (selectedMode()) {
        TransmittalItemMode.Standard -> listOf(TransmittalScanTarget.Auto, TransmittalScanTarget.Serial)
        TransmittalItemMode.Phone -> TransmittalScanTarget.entries
    }

    fun report(): TransmittalReport = TransmittalReport(
        to = to.trim(),
        from = from.trim(),
        date = reportDate.trim(),
        items = items,
        preparedBy = preparedBy.trim(),
        transmitBy = transmitBy.trim(),
        receivedByDate = receivedByDate.trim(),
        notedBy = notedBy.trim(),
        approvedByDate = approvedByDate.trim(),
        itemMode = selectedMode()
    )

    fun updateItem(index: Int, update: (TransmittalItemDraft) -> TransmittalItemDraft) {
        items = items.mapIndexed { rowIndex, item -> if (rowIndex == index) update(item) else item }
    }

    fun hasDuplicate(value: String, currentIndex: Int): Boolean {
        val mode = selectedMode()
        return items.withIndex().any { (index, item) ->
            if (index == currentIndex) return@any false
            val values = if (mode == TransmittalItemMode.Standard) {
                listOf(item.serialNumber)
            } else {
                listOf(item.mobileNumber, item.serialNumber, item.imei1, item.imei2)
            }
            values.any { it.trim().replace("\\s+".toRegex(), "").equals(value, ignoreCase = true) }
        }
    }

    fun clearSharedDescriptionDialog() {
        pendingScanValue = ""
        pendingScanSource = ""
        pendingScanTargetName = TransmittalScanTarget.Serial.name
        sharedDescription = ""
        showSharedDescriptionDialog = false
    }

    fun placeValue(
        rawValue: String,
        source: String,
        requestedTarget: TransmittalScanTarget,
        descriptionForNewRow: String = ""
    ) {
        val value = rawValue.trim().replace("\\s+".toRegex(), "").uppercase(Locale.ROOT)
        if (value.isBlank()) {
            scope.launch { snackbarHostState.showSnackbar("Scan or enter a value first.") }
            return
        }

        val mode = selectedMode()
        var rowIndex = selectedItemIndex.coerceIn(0, items.lastIndex)
        var row = items[rowIndex]
        val modeTarget = if (mode == TransmittalItemMode.Standard && requestedTarget !in listOf(TransmittalScanTarget.Auto, TransmittalScanTarget.Serial)) {
            TransmittalScanTarget.Serial
        } else {
            requestedTarget
        }
        var target = if (modeTarget == TransmittalScanTarget.Auto) row.nextAutoTarget(mode) else modeTarget.takeIf { row.isBlankFor(it) }

        if (target == null && activeBatchDescription.isNotBlank()) {
            val nextBatchSlot = items.withIndex().firstOrNull { (candidateIndex, candidate) ->
                candidateIndex > rowIndex &&
                    candidate.description.trim().equals(activeBatchDescription.trim(), ignoreCase = true) &&
                    (modeTarget == TransmittalScanTarget.Auto || candidate.isBlankFor(modeTarget))
            }
            if (nextBatchSlot != null) {
                rowIndex = nextBatchSlot.index
                selectedItemIndex = rowIndex
                row = nextBatchSlot.value
                target = if (modeTarget == TransmittalScanTarget.Auto) row.nextAutoTarget(mode) else modeTarget
            }
        }

        if (target == null) {
            if (items.size >= MAX_TRANSMITTAL_ITEMS) {
                scope.launch { snackbarHostState.showSnackbar("The transmittal supports up to $MAX_TRANSMITTAL_ITEMS scan slots.") }
                return
            }
            items = items + TransmittalItemDraft(description = descriptionForNewRow)
            rowIndex = items.lastIndex
            selectedItemIndex = rowIndex
            row = items[rowIndex]
            target = if (modeTarget == TransmittalScanTarget.Auto) row.nextAutoTarget(mode) else modeTarget
        }

        val finalTarget = target ?: TransmittalScanTarget.Serial
        if (hasDuplicate(value, rowIndex)) {
            scope.launch { snackbarHostState.showSnackbar("Duplicate serial, IMEI, or mobile number skipped.") }
            return
        }

        updateItem(rowIndex) { it.withScanValue(finalTarget, value) }
        selectedItemIndex = if (rowIndex + 1 < items.size) rowIndex + 1 else rowIndex
        manualScanValue = ""
        scope.launch { snackbarHostState.showSnackbar("$source value added to item ${rowIndex + 1}.") }
    }

    fun applyScan(raw: String, source: String = "Scanner") {
        val value = raw.trim().replace("\\s+".toRegex(), "").uppercase(Locale.ROOT)
        if (value.isBlank()) {
            scope.launch { snackbarHostState.showSnackbar("Scan or enter a value first.") }
            return
        }

        val mode = selectedMode()
        val rowIndex = selectedItemIndex.coerceIn(0, items.lastIndex)
        val row = items[rowIndex]
        val requestedTarget = selectedTarget()
        val target = if (requestedTarget == TransmittalScanTarget.Auto) row.nextAutoTarget(mode) else requestedTarget.takeIf { row.isBlankFor(it) }
        val hasOpenBatchSlot = activeBatchDescription.isNotBlank() && items.any {
            it.description.trim().equals(activeBatchDescription.trim(), ignoreCase = true) && it.nextAutoTarget(mode) != null
        }
        if (activeBatchDescription.isNotBlank() && !hasOpenBatchSlot) {
            activeBatchDescription = ""
        }
        val isActiveBatchRow = activeBatchDescription.isNotBlank() && row.description.trim().equals(activeBatchDescription.trim(), ignoreCase = true)
        val newLineCandidate = items.asReversed().firstOrNull { it.description.isNotBlank() }?.description?.trim()

        if (!isActiveBatchRow && items.size < MAX_TRANSMITTAL_ITEMS && (rowIndex > 0 || target == null) && !newLineCandidate.isNullOrBlank()) {
            pendingScanValue = value
            pendingScanSource = source
            pendingScanTargetName = requestedTarget.name
            sharedDescription = newLineCandidate
            showSharedDescriptionDialog = true
            return
        }

        placeValue(value, source, requestedTarget)
    }

    fun resolveSharedDescription(reuse: Boolean) {
        val value = pendingScanValue
        val source = pendingScanSource.ifBlank { "Scanner" }
        val target = TransmittalScanTarget.entries.firstOrNull { it.name == pendingScanTargetName }
            ?: TransmittalScanTarget.Auto
        val description = if (reuse) sharedDescription else ""
        clearSharedDescriptionDialog()
        placeValue(value, source, target, description)
    }

    fun addBatch() {
        val quantity = batchQuantityText.trim().toIntOrNull()
        val description = batchDescriptionText.trim()
        if (quantity == null || quantity !in 1..MAX_TRANSMITTAL_ITEMS) {
            scope.launch { snackbarHostState.showSnackbar("Enter a quantity from 1 to $MAX_TRANSMITTAL_ITEMS.") }
            return
        }
        if (description.isBlank()) {
            scope.launch { snackbarHostState.showSnackbar("Enter an item description first.") }
            return
        }

        val existingItems = if (items.size == 1 && !items.first().hasAnyValue()) emptyList() else items
        if (existingItems.size + quantity > MAX_TRANSMITTAL_ITEMS) {
            scope.launch { snackbarHostState.showSnackbar("There is not enough room for this batch. Maximum is $MAX_TRANSMITTAL_ITEMS slots.") }
            return
        }

        val startIndex = existingItems.size
        items = existingItems + List(quantity) { TransmittalItemDraft(description = description) }
        activeBatchDescription = description
        selectedItemIndex = startIndex
        pendingScanItemIndex = startIndex
        showBatchDialog = false
        val firstManualValue = manualScanValue.trim()
        manualScanValue = ""
        batchQuantityText = "1"
        batchDescriptionText = ""
        if (firstManualValue.isNotBlank()) {
            placeValue(firstManualValue, "Manual", selectedTarget())
        }
        scope.launch { snackbarHostState.showSnackbar("Created $quantity scan slots for $description. Progress: 0/$quantity scanned.") }
    }

    fun triggerAddValue() {
        if (!addValueNeedsDescription) {
            applyScan(manualScanValue, "Manual")
            return
        }
        if (batchDescriptionText.isBlank()) {
            batchDescriptionText = items.asReversed().firstOrNull { it.description.isNotBlank() }?.description.orEmpty()
        }
        showBatchDialog = true
    }

    fun openScanner(route: String) {
        val targetIndex = when {
            selectedItemIndex in items.indices -> selectedItemIndex
            items.size < MAX_TRANSMITTAL_ITEMS -> items.lastIndex
            else -> items.lastIndex
        }
        pendingScanItemIndex = targetIndex
        selectedItemIndex = targetIndex
        navController.currentBackStackEntry?.savedStateHandle?.set(SCAN_RETURN_ROUTE_KEY, SCAN_RETURN_SERIAL_ROUTE)
        navController.navigate(route)
    }

    val scannedSerial = remember(navEntry) {
        navEntry?.savedStateHandle?.getStateFlow<String?>(SCAN_RESULT_SERIAL_KEY, null)
    }?.collectAsState(initial = null)?.value

    LaunchedEffect(scannedSerial) {
        val value = scannedSerial?.trim().orEmpty()
        if (value.isBlank()) return@LaunchedEffect
        selectedItemIndex = pendingScanItemIndex.coerceIn(0, items.lastIndex)
        applyScan(value)
        navEntry?.savedStateHandle?.remove<String>(SCAN_RESULT_SERIAL_KEY)
    }

    val scannedCount = when (selectedMode()) {
        TransmittalItemMode.Standard -> items.count { it.serialNumber.isNotBlank() }
        TransmittalItemMode.Phone -> items.count { it.hasDetailedValues() }
    }
    val expectedCount = if (activeBatchDescription.isNotBlank()) {
        items.count { it.description.trim().equals(activeBatchDescription.trim(), ignoreCase = true) }
    } else {
        items.size
    }.coerceAtLeast(1)

    fun printReport() {
        val currentReport = report()
        if (currentReport.items.none { it.hasAnyValue() }) {
            scope.launch { snackbarHostState.showSnackbar("Scan or add at least one transmittal item first.") }
            return
        }
        scope.launch {
            busy = true
            val savedPdf = withContext(Dispatchers.IO) {
                runCatching {
                    TransmittalDocumentStore.savePrintReadyPdf(context, currentReport).getOrThrow()
                }
            }.getOrElse { error ->
                busy = false
                snackbarHostState.showSnackbar(error.message ?: "Couldn't save the print-ready transmittal PDF.")
                return@launch
            }
            val started = printTransmittalPdf(context, TransmittalDocumentStore.uriFor(context, savedPdf), "Yakult Transmittal")
            busy = false
            if (!started) {
                val shared = TransmittalDocumentStore.share(context, savedPdf)
                if (!shared) {
                    snackbarHostState.showSnackbar("No print service is enabled. Install/enable Mopria or Android's print service, then try again.")
                }
            }
        }
    }

    fun openPrintSettings(): Boolean {
        return runCatching {
            context.startActivity(Intent(Settings.ACTION_PRINT_SETTINGS))
            true
        }.getOrDefault(false)
    }

    fun openBluetoothSettings(): Boolean {
        return runCatching {
            context.startActivity(Intent(Settings.ACTION_BLUETOOTH_SETTINGS))
            true
        }.getOrDefault(false)
    }

    fun openDirectIpSetup() {
        directPrinterHost = context
            .getSharedPreferences(TRANSMITTAL_PRINTER_PREFS, android.content.Context.MODE_PRIVATE)
            .getString(DIRECT_PRINTER_HOST_KEY, "")
            .orEmpty()
        showPrinterSourceMenu = false
        showDirectIpDialog = true
    }

    fun saveDirectIpAndOpenPrintSettings() {
        val host = directPrinterHost.trim()
        if (host.isBlank()) {
            scope.launch { snackbarHostState.showSnackbar("Enter the printer IP address or hostname first.") }
            return
        }

        context.getSharedPreferences(TRANSMITTAL_PRINTER_PREFS, android.content.Context.MODE_PRIVATE)
            .edit()
            .putString(DIRECT_PRINTER_HOST_KEY, host)
            .apply()
        context.getSystemService(ClipboardManager::class.java)
            ?.setPrimaryClip(ClipData.newPlainText("Yakult printer address", host))
        showDirectIpDialog = false
        Toast.makeText(
            context,
            "Printer address saved and copied. Add it in your enabled print service.",
            Toast.LENGTH_LONG
        ).show()
        if (!openPrintSettings()) {
            scope.launch { snackbarHostState.showSnackbar("Couldn't open printing settings on this device.") }
        }
    }

    fun openBluetoothPrinterSetup() {
        showPrinterSourceMenu = false
        Toast.makeText(
            context,
            "Pair the printer, enable its print service, then return here and choose Wi-Fi printer.",
            Toast.LENGTH_LONG
        ).show()
        if (!openBluetoothSettings()) {
            scope.launch { snackbarHostState.showSnackbar("Couldn't open Bluetooth settings on this device.") }
        }
    }

    fun shareReport(workbook: com.example.yakultscanner.utils.SavedTransmittalWorkbook): Boolean {
        return runCatching {
            val intent = Intent(Intent.ACTION_SEND).apply {
                type = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                putExtra(Intent.EXTRA_STREAM, workbook.uri)
                addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
            }
            context.startActivity(Intent.createChooser(intent, "Share transmittal workbook"))
            true
        }.getOrDefault(false)
    }

    fun exportReport() {
        val currentReport = report()
        if (currentReport.items.none { it.hasAnyValue() }) {
            scope.launch { snackbarHostState.showSnackbar("Scan or add at least one transmittal item first.") }
            return
        }
        scope.launch {
            busy = true
            val result = exportTransmittalWorkbook(context, currentReport)
            busy = false
            val saved = result.getOrNull()
            if (saved != null) {
                val archiveFailure = withContext(Dispatchers.IO) {
                    runCatching {
                        TransmittalDocumentStore.archiveWorkbook(context, saved).getOrThrow()
                        TransmittalDocumentStore.savePrintReadyPdf(context, currentReport).getOrThrow()
                    }.exceptionOrNull()
                }
                exportedWorkbook = saved
                showWorkbookHandoff = true
                Toast.makeText(context, "Saved ${saved.displayName} to Downloads and Saved Transmittal Files.", Toast.LENGTH_LONG).show()
                archiveFailure?.let { error ->
                    snackbarHostState.showSnackbar(error.message ?: "The Excel export was saved, but its local archive copy could not be created.")
                }
            } else {
                snackbarHostState.showSnackbar(result.exceptionOrNull()?.message ?: "Excel report export failed.")
            }
        }
    }

    if (showPrinterSourceMenu) {
        androidx.compose.material3.AlertDialog(
            onDismissRequest = { showPrinterSourceMenu = false },
            title = { Text("Choose printer source") },
            text = {
                Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                    Text("Android handles printer drivers and the final print job. Choose how you want to prepare the printer connection.")
                    OutlinedButton(
                        onClick = {
                            showPrinterSourceMenu = false
                            printReport()
                        },
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Column(modifier = Modifier.fillMaxWidth()) {
                            Text("Wi-Fi printer", fontWeight = FontWeight.Bold)
                            Text("Open Android's printer picker and scan enabled Wi-Fi print services.", style = MaterialTheme.typography.bodySmall)
                        }
                    }
                    OutlinedButton(
                        onClick = { openDirectIpSetup() },
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Column(modifier = Modifier.fillMaxWidth()) {
                            Text("Direct IP printer", fontWeight = FontWeight.Bold)
                            Text("Save an IP address, then add it in a compatible Android print service.", style = MaterialTheme.typography.bodySmall)
                        }
                    }
                    OutlinedButton(
                        onClick = { openBluetoothPrinterSetup() },
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Column(modifier = Modifier.fillMaxWidth()) {
                            Text("Bluetooth printer", fontWeight = FontWeight.Bold)
                            Text("Open Bluetooth settings to pair the printer before using Android printing.", style = MaterialTheme.typography.bodySmall)
                        }
                    }
                }
            },
            confirmButton = {},
            dismissButton = {
                TextButton(onClick = { showPrinterSourceMenu = false }) { Text("Cancel") }
            }
        )
    }

    if (showDirectIpDialog) {
        androidx.compose.material3.AlertDialog(
            onDismissRequest = { showDirectIpDialog = false },
            title = { Text("Direct IP printer") },
            text = {
                Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                    Text("Enter the printer's IP address or hostname. Yakult Scanner will save and copy it, then open Android printing settings so a compatible print service can add the printer.")
                    OutlinedTextField(
                        value = directPrinterHost,
                        onValueChange = { directPrinterHost = it },
                        modifier = Modifier.fillMaxWidth(),
                        label = { Text("Printer IP address or hostname") },
                        placeholder = { Text("Example: 192.168.1.50") },
                        singleLine = true
                    )
                    Text("A print service such as Default Print Service, Mopria, or the printer manufacturer's app is still required to send the print job.", style = MaterialTheme.typography.bodySmall, color = ScannerWorkspaceUi.Muted)
                }
            },
            confirmButton = {
                Button(onClick = { saveDirectIpAndOpenPrintSettings() }) {
                    Text("Save & open printing settings")
                }
            },
            dismissButton = {
                TextButton(onClick = { showDirectIpDialog = false }) { Text("Cancel") }
            }
        )
    }

    exportedWorkbook?.takeIf { showWorkbookHandoff }?.let { workbook ->
        androidx.compose.material3.AlertDialog(
            onDismissRequest = {
                showWorkbookHandoff = false
                exportedWorkbook = null
            },
            title = { Text("Excel report ready") },
            text = {
                Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                    Text("${workbook.displayName} was saved to Downloads.")
                    Text("To preserve the original Excel transmittal layout, open it in Microsoft Excel or Google Sheets. In that app, choose Print and select Mopria / your configured printer.")
                }
            },
            confirmButton = {
                Button(
                    onClick = {
                        if (openTransmittalWorkbook(context, workbook)) {
                            showWorkbookHandoff = false
                            exportedWorkbook = null
                        } else {
                            scope.launch {
                                snackbarHostState.showSnackbar("No Excel or spreadsheet app was found. Install Microsoft Excel or Google Sheets, then open the report from Downloads.")
                            }
                        }
                    }
                ) {
                    Text("Open Excel / Sheets")
                }
            },
            dismissButton = {
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    TextButton(
                        onClick = {
                            if (shareReport(workbook)) {
                                showWorkbookHandoff = false
                                exportedWorkbook = null
                            } else {
                                scope.launch { snackbarHostState.showSnackbar("Couldn't open the workbook share menu.") }
                            }
                        }
                    ) {
                        Text("Share workbook")
                    }
                    TextButton(
                        onClick = {
                            showWorkbookHandoff = false
                            exportedWorkbook = null
                        }
                    ) {
                        Text("Later")
                    }
                }
            }
        )
    }

    val adaptiveLayout = LocalAdaptiveLayout.current

    val workspaceHeader: @Composable () -> Unit = {
        ScannerWorkspaceHeader(
            title = "Scan, review, transmit",
            subtitle = "Populate the Yakult transmittal form from serial, IMEI, and mobile-number scans, then export the original Excel template format.",
            trailing = { ScannerMetricChip("Progress", "$scannedCount/$expectedCount") }
        )
    }

    val detailsSection: @Composable () -> Unit = {
        ScannerSurfaceCard {
            ScannerSectionHeader("Transmittal details", "These values populate both the TRANSMITTAL and FILE sections.")
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                OutlinedTextField(value = to, onValueChange = { to = it }, modifier = Modifier.weight(1f), label = { Text("To") }, singleLine = true)
                OutlinedTextField(value = reportDate, onValueChange = { reportDate = it }, modifier = Modifier.weight(1f), label = { Text("Date") }, singleLine = true)
            }
            OutlinedTextField(value = from, onValueChange = { from = it }, modifier = Modifier.fillMaxWidth(), label = { Text("From") }, singleLine = true)
        }
    }

    val itemModeSection: @Composable () -> Unit = {
        ScannerSurfaceCard(
            modifier = if (adaptiveLayout.isTablet) Modifier.fillMaxWidth() else Modifier
        ) {
            ScannerSectionHeader("Item filter", "Choose the shape of each transmittal line before scanning.")
            Row(modifier = Modifier.horizontalScroll(rememberScrollState()), horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                TransmittalItemMode.entries.forEach { mode ->
                    FilterChip(
                        selected = selectedMode() == mode,
                        onClick = {
                            itemModeName = mode.name
                            scanTargetName = if (mode == TransmittalItemMode.Standard) {
                                TransmittalScanTarget.Serial.name
                            } else {
                                TransmittalScanTarget.Auto.name
                            }
                        },
                        label = { Text(mode.label) }
                    )
                }
            }
            Text(selectedMode().helperText, style = MaterialTheme.typography.bodySmall, color = ScannerWorkspaceUi.Muted)
        }
    }

    val scannerControlsSection: @Composable () -> Unit = {
        ScannerSurfaceCard {
            ScannerSectionHeader("Scanner controls", "Choose where the next scan should be placed, or leave it on Auto.")
            Row(modifier = Modifier.horizontalScroll(rememberScrollState()), horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                targetOptions().forEach { target ->
                    FilterChip(selected = scanTargetName == target.name, onClick = { scanTargetName = target.name }, label = { Text(target.label) })
                }
            }
            OutlinedTextField(
                value = manualScanValue,
                onValueChange = { manualScanValue = it },
                modifier = Modifier.fillMaxWidth(),
                label = { Text(if (addValueNeedsDescription) "Optional first serial" else "Serial to add") },
                singleLine = true,
                keyboardOptions = androidx.compose.foundation.text.KeyboardOptions(imeAction = ImeAction.Done),
                keyboardActions = androidx.compose.foundation.text.KeyboardActions(onDone = { triggerAddValue() })
            )
            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.SpaceBetween
            ) {
                Column(modifier = Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(2.dp)) {
                    Text("Add value mode", style = MaterialTheme.typography.labelLarge, fontWeight = FontWeight.Bold)
                    Text(
                        if (addValueNeedsDescription) "Needs description: quantity + description dialog"
                        else "Serials only: add one value at a time",
                        style = MaterialTheme.typography.bodySmall,
                        color = ScannerWorkspaceUi.Muted
                    )
                }
                Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                    Text("Needs description", style = MaterialTheme.typography.labelSmall, color = ScannerWorkspaceUi.Muted)
                    androidx.compose.material3.Switch(
                        checked = addValueNeedsDescription,
                        onCheckedChange = { addValueNeedsDescription = it }
                    )
                }
            }
            Column(verticalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.fillMaxWidth()) {
                Button(
                    onClick = { triggerAddValue() },
                    enabled = true,
                    modifier = Modifier.fillMaxWidth().height(50.dp),
                    contentPadding = PaddingValues(horizontal = 14.dp, vertical = 8.dp)
                ) {
                    Icon(Icons.Filled.Add, contentDescription = null, modifier = Modifier.size(18.dp))
                    Spacer(Modifier.width(6.dp))
                    Text("Add value", maxLines = 1, softWrap = false)
                }
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.fillMaxWidth()) {
                    OutlinedButton(
                        onClick = { openScanner("scanner_camera") },
                        modifier = Modifier.weight(1f).height(50.dp),
                        contentPadding = PaddingValues(horizontal = 10.dp, vertical = 8.dp)
                    ) {
                        Icon(Icons.Filled.QrCodeScanner, contentDescription = null, modifier = Modifier.size(18.dp))
                        Spacer(Modifier.width(6.dp))
                        Text("Camera", maxLines = 1, softWrap = false)
                    }
                    OutlinedButton(
                        onClick = { openScanner("scanner_device") },
                        modifier = Modifier.weight(1f).height(50.dp),
                        contentPadding = PaddingValues(horizontal = 10.dp, vertical = 8.dp)
                    ) {
                        Icon(Icons.Filled.FileDownload, contentDescription = null, modifier = Modifier.size(18.dp))
                        Spacer(Modifier.width(6.dp))
                        Text("Device", maxLines = 1, softWrap = false)
                    }
                }
            }
        }
    }

    val numberedItemsHeader: @Composable () -> Unit = {
        ScannerSurfaceCard {
            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.SpaceBetween, modifier = Modifier.fillMaxWidth()) {
                ScannerSectionHeader("Numbered items", "The form shows 12 summary lines; detailed Excel output supports up to $MAX_TRANSMITTAL_ITEMS scan slots.", Modifier.weight(1f))
                TextButton(onClick = {
                    if (items.size < MAX_TRANSMITTAL_ITEMS) items = items + TransmittalItemDraft()
                }, enabled = items.size < MAX_TRANSMITTAL_ITEMS) {
                    Icon(Icons.Filled.Add, contentDescription = null)
                    Text("Line")
                }
            }
        }
    }

    val itemCard: @Composable (Int, TransmittalItemDraft) -> Unit = { index, item ->
        ScannerSurfaceCard(
            modifier = Modifier.border(
                width = if (index == selectedItemIndex) 2.dp else 1.dp,
                color = if (index == selectedItemIndex) ScannerWorkspaceUi.Brand else ScannerWorkspaceUi.Divider,
                shape = RoundedCornerShape(20.dp)
            )
        ) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Surface(shape = CircleShape, color = ScannerWorkspaceUi.SurfaceSubtle) {
                    Text("${index + 1}", modifier = Modifier.padding(horizontal = 11.dp, vertical = 7.dp), fontWeight = FontWeight.Bold, color = ScannerWorkspaceUi.Ink)
                }
                Spacer(Modifier.width(8.dp))
                Text(if (index == selectedItemIndex) "Selected scan line" else "Item ${index + 1}", fontWeight = FontWeight.Bold, modifier = Modifier.weight(1f))
                TextButton(onClick = { selectedItemIndex = index }) { Text("Select") }
                IconButton(onClick = {
                    if (items.size > 1) {
                        items = items.filterIndexed { rowIndex, _ -> rowIndex != index }
                        selectedItemIndex = selectedItemIndex.coerceAtMost(items.lastIndex)
                    }
                }, enabled = items.size > 1) {
                    Icon(Icons.Filled.Delete, contentDescription = "Remove item ${index + 1}", tint = ScannerWorkspaceUi.Error)
                }
            }
            OutlinedTextField(value = item.description, onValueChange = { value -> updateItem(index) { current -> current.copy(description = value) } }, modifier = Modifier.fillMaxWidth(), label = { Text("Description / item line") }, singleLine = true)
            if (selectedMode() == TransmittalItemMode.Standard) {
                OutlinedTextField(value = item.serialNumber, onValueChange = { value -> updateItem(index) { it.copy(serialNumber = value) } }, modifier = Modifier.fillMaxWidth(), label = { Text("Serial") }, singleLine = true)
            } else {
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    OutlinedTextField(value = item.serialNumber, onValueChange = { value -> updateItem(index) { it.copy(serialNumber = value) } }, modifier = Modifier.weight(1f), label = { Text("Serial") }, singleLine = true)
                    OutlinedTextField(value = item.mobileNumber, onValueChange = { value -> updateItem(index) { it.copy(mobileNumber = value) } }, modifier = Modifier.weight(1f), label = { Text("Mobile") }, singleLine = true)
                }
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    OutlinedTextField(value = item.imei1, onValueChange = { value -> updateItem(index) { it.copy(imei1 = value) } }, modifier = Modifier.weight(1f), label = { Text("IMEI 1") }, singleLine = true)
                    OutlinedTextField(value = item.imei2, onValueChange = { value -> updateItem(index) { it.copy(imei2 = value) } }, modifier = Modifier.weight(1f), label = { Text("IMEI 2") }, singleLine = true)
                }
                item.detailSummary().takeIf { it.isNotBlank() }?.let { detail ->
                    Text(detail, style = MaterialTheme.typography.bodySmall, color = ScannerWorkspaceUi.Muted, maxLines = 2, overflow = TextOverflow.Ellipsis)
                }
            }
        }
    }

    val signOffSection: @Composable () -> Unit = {
        ScannerSurfaceCard {
            ScannerSectionHeader("Sign-off fields", "These fields are copied into both form copies.")
            OutlinedTextField(value = preparedBy, onValueChange = { preparedBy = it }, modifier = Modifier.fillMaxWidth(), label = { Text("Prepared by") }, singleLine = true)
            OutlinedTextField(value = transmitBy, onValueChange = { transmitBy = it }, modifier = Modifier.fillMaxWidth(), label = { Text("Transmit by") }, singleLine = true)
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                OutlinedTextField(value = receivedByDate, onValueChange = { receivedByDate = it }, modifier = Modifier.weight(1f), label = { Text("Received by / date") }, singleLine = true)
                OutlinedTextField(value = approvedByDate, onValueChange = { approvedByDate = it }, modifier = Modifier.weight(1f), label = { Text("Approved by / date") }, singleLine = true)
            }
            OutlinedTextField(value = notedBy, onValueChange = { notedBy = it }, modifier = Modifier.fillMaxWidth(), label = { Text("Noted by") }, singleLine = true)
        }
    }

    val previewSection: @Composable () -> Unit = {
        ScannerSurfaceCard {
            Row(verticalAlignment = Alignment.CenterVertically, modifier = Modifier.fillMaxWidth()) {
                ScannerSectionHeader(
                    "Excel-style preview",
                    "The original workbook remains the export source; this preview follows its two-copy layout.",
                    Modifier.weight(1f)
                )
                Column(horizontalAlignment = Alignment.End) {
                    Text(
                        if (showExcelPreview) "Shown" else "Hidden",
                        style = MaterialTheme.typography.labelSmall,
                        color = ScannerWorkspaceUi.Muted
                    )
                    androidx.compose.material3.Switch(
                        checked = showExcelPreview,
                        onCheckedChange = { showExcelPreview = it }
                    )
                }
            }
            if (showExcelPreview) {
                TransmittalSheetPreview(report())
            } else {
                Surface(
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(14.dp),
                    color = ScannerWorkspaceUi.SurfaceSubtle
                ) {
                    Text(
                        "Excel preview collapsed. Turn on the toggle to expand it.",
                        modifier = Modifier.padding(12.dp),
                        style = MaterialTheme.typography.bodySmall,
                        color = ScannerWorkspaceUi.Muted
                    )
                }
            }
        }
    }

    val tabletActionsSection: @Composable () -> Unit = {
        ScannerSurfaceCard {
            ScannerSectionHeader("Review actions", "Finish the transmittal from the review pane without leaving the scan workspace.")
            Text(
                "${items.count { it.hasAnyValue() }} of ${items.size} lines contain values.",
                style = MaterialTheme.typography.bodySmall,
                color = ScannerWorkspaceUi.Muted
            )
            Button(
                onClick = { exportReport() },
                enabled = !busy && items.any { it.hasAnyValue() },
                modifier = Modifier.fillMaxWidth().height(50.dp)
            ) {
                Icon(Icons.Filled.FileDownload, contentDescription = null, modifier = Modifier.size(18.dp))
                Spacer(Modifier.width(6.dp))
                Text(if (busy) "Preparing report…" else "Export Excel report")
            }
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.fillMaxWidth()) {
                OutlinedButton(
                    onClick = { showPrinterSourceMenu = true },
                    enabled = !busy && items.any { it.hasAnyValue() },
                    modifier = Modifier.weight(1f).height(48.dp)
                ) {
                    Icon(Icons.Filled.Print, contentDescription = null, modifier = Modifier.size(18.dp))
                    Spacer(Modifier.width(4.dp))
                    Text("Print", maxLines = 1, softWrap = false)
                }
                OutlinedButton(
                    onClick = { navController.navigate("saved_transmittal_files") },
                    enabled = !busy,
                    modifier = Modifier.weight(1f).height(48.dp)
                ) {
                    Icon(Icons.Filled.Folder, contentDescription = null, modifier = Modifier.size(18.dp))
                    Spacer(Modifier.width(4.dp))
                    Text("Saved files", maxLines = 1, softWrap = false)
                }
            }
        }
    }

    Scaffold(
        containerColor = ScannerWorkspaceUi.Canvas,
        topBar = {
            TopAppBar(
                title = { Text("Transmittal Scan", color = ScannerWorkspaceUi.Ink, fontWeight = FontWeight.Bold) },
                navigationIcon = {
                    IconButton(onClick = { navController.popBackStack() }) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back", tint = ScannerWorkspaceUi.Ink)
                    }
                },
                actions = {
                    IconButton(onClick = { showPrinterSourceMenu = true }, enabled = !busy && items.any { it.hasAnyValue() }) {
                        Icon(Icons.Filled.Print, contentDescription = "Choose printer source", tint = ScannerWorkspaceUi.Brand)
                    }
                    IconButton(onClick = { navController.navigate("saved_transmittal_files") }, enabled = !busy) {
                        Icon(Icons.Filled.Folder, contentDescription = "Saved Transmittal Files", tint = ScannerWorkspaceUi.Brand)
                    }
                    IconButton(onClick = { exportReport() }, enabled = !busy) {
                        Icon(Icons.Filled.FileDownload, contentDescription = "Export Excel report", tint = ScannerWorkspaceUi.Brand)
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(containerColor = ScannerWorkspaceUi.Surface)
            )
        },
        snackbarHost = { SnackbarHost(snackbarHostState) },
        bottomBar = {
            if (!adaptiveLayout.isTablet) {
                ScannerActionBar(
                    primaryLabel = if (busy) "Preparing report…" else "Export Excel report",
                    onPrimary = { exportReport() },
                    primaryEnabled = !busy && items.any { it.hasAnyValue() }
                )
            }
        }
    ) { padding ->
        if (!adaptiveLayout.isTablet) {
            LazyColumn(
                modifier = Modifier.fillMaxSize().background(ScannerWorkspaceUi.Canvas).padding(padding),
                contentPadding = PaddingValues(horizontal = 16.dp, vertical = 14.dp),
                verticalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                item { workspaceHeader() }
                item { detailsSection() }
                item { itemModeSection() }
                item { scannerControlsSection() }
                item { numberedItemsHeader() }
                itemsIndexed(items) { index, item -> itemCard(index, item) }
                item { signOffSection() }
                item { previewSection() }
                item { Spacer(Modifier.height(24.dp)) }
            }
        } else {
            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .background(ScannerWorkspaceUi.Canvas)
                    .padding(padding)
                    .padding(horizontal = adaptiveLayout.horizontalPadding, vertical = 14.dp),
                verticalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                workspaceHeader()
                Row(
                    modifier = Modifier.fillMaxWidth().weight(1f),
                    horizontalArrangement = Arrangement.spacedBy(16.dp),
                    verticalAlignment = Alignment.Top
                ) {
                    LazyColumn(
                        modifier = Modifier.weight(0.44f).fillMaxHeight(),
                        verticalArrangement = Arrangement.spacedBy(12.dp),
                        contentPadding = PaddingValues(bottom = 24.dp)
                    ) {
                        item { detailsSection() }
                        item { itemModeSection() }
                        item { scannerControlsSection() }
                        item {
                            ScannerSurfaceCard {
                                ScannerSectionHeader("Scan workspace", "The camera and device scanner continue in their existing dedicated scan screens.")
                                Text("Selected line: ${selectedItemIndex + 1}", fontWeight = FontWeight.Bold, color = ScannerWorkspaceUi.Ink)
                                Text("Progress: $scannedCount/$expectedCount", style = MaterialTheme.typography.bodySmall, color = ScannerWorkspaceUi.Muted)
                            }
                        }
                    }
                    LazyColumn(
                        modifier = Modifier.weight(0.56f).fillMaxHeight(),
                        verticalArrangement = Arrangement.spacedBy(12.dp),
                        contentPadding = PaddingValues(bottom = 24.dp)
                    ) {
                        item { tabletActionsSection() }
                        item { numberedItemsHeader() }
                        itemsIndexed(items) { index, item -> itemCard(index, item) }
                        item { signOffSection() }
                        item { previewSection() }
                    }
                }
            }
        }
    }

    if (showBatchDialog) {
        androidx.compose.material3.AlertDialog(
            onDismissRequest = { showBatchDialog = false },
            title = { Text("Add item batch") },
            text = {
                Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                    Text("Create scan slots with one shared description, then scan each serial into the next slot.")
                    OutlinedTextField(
                        value = batchQuantityText,
                        onValueChange = { batchQuantityText = it.filter(Char::isDigit) },
                        modifier = Modifier.fillMaxWidth(),
                        label = { Text("How many items?") },
                        singleLine = true,
                        keyboardOptions = androidx.compose.foundation.text.KeyboardOptions(keyboardType = androidx.compose.ui.text.input.KeyboardType.Number)
                    )
                    OutlinedTextField(
                        value = batchDescriptionText,
                        onValueChange = { batchDescriptionText = it },
                        modifier = Modifier.fillMaxWidth(),
                        label = { Text("Item description") },
                        placeholder = { Text("Example: Monitor") },
                        singleLine = true
                    )
                    Text("Example: 50 + Monitor creates 50 slots and fills the next serial as you scan.", style = MaterialTheme.typography.bodySmall, color = ScannerWorkspaceUi.Muted)
                }
            },
            confirmButton = {
                Button(onClick = { addBatch() }) { Text("Create scan slots") }
            },
            dismissButton = {
                TextButton(onClick = { showBatchDialog = false }) { Text("Cancel") }
            }
        )
    }

    if (showSharedDescriptionDialog) {
        androidx.compose.material3.AlertDialog(
            onDismissRequest = { clearSharedDescriptionDialog() },
            title = { Text("Reuse this description?") },
            text = {
                Text(
                    "This scan is starting a new ${selectedMode().label.lowercase(Locale.getDefault())} line. Reuse the existing description below?\n\n\"$sharedDescription\""
                )
            },
            confirmButton = {
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    TextButton(onClick = { resolveSharedDescription(reuse = false) }) { Text("New description") }
                    Button(onClick = { resolveSharedDescription(reuse = true) }) { Text("Reuse description") }
                }
            },
            dismissButton = {
                TextButton(onClick = { clearSharedDescriptionDialog() }) { Text("Cancel") }
            }
        )
    }
}

@Composable
private fun TransmittalSheetPreview(report: TransmittalReport) {
    val horizontalScroll = rememberScrollState()
    Box(modifier = Modifier.fillMaxWidth().horizontalScroll(horizontalScroll)) {
        Column(
            modifier = Modifier
                .width(680.dp)
                .background(Color.White)
                .border(1.dp, Color(0xFF1D1D1D))
                .padding(horizontal = 12.dp, vertical = 8.dp),
            verticalArrangement = Arrangement.spacedBy(0.dp)
        ) {
            TransmittalFormPreview(report, "TRANSMITTAL")
            Spacer(Modifier.height(24.dp))
            TransmittalFormPreview(report, "FILE")
        }
    }
}

@Composable
private fun TransmittalFormPreview(report: TransmittalReport, title: String) {
    val labelModifier = Modifier.width(94.dp)
    Column(modifier = Modifier.fillMaxWidth()) {
        Text("YAKULT PHILIPPINES INC.", modifier = Modifier.fillMaxWidth(), textAlign = androidx.compose.ui.text.style.TextAlign.Center, fontFamily = androidx.compose.ui.text.font.FontFamily.Serif, fontWeight = FontWeight.Bold, style = MaterialTheme.typography.titleMedium)
        Text("1461 Agoncillo cor. Escoda St. Ermita Manila", modifier = Modifier.fillMaxWidth(), textAlign = androidx.compose.ui.text.style.TextAlign.Center, fontFamily = androidx.compose.ui.text.font.FontFamily.Serif, fontWeight = FontWeight.Bold, style = MaterialTheme.typography.bodySmall)
        Spacer(Modifier.height(6.dp))
        Text(title, modifier = Modifier.fillMaxWidth().border(1.dp, Color.Black).padding(vertical = 4.dp), textAlign = androidx.compose.ui.text.style.TextAlign.Center, fontFamily = androidx.compose.ui.text.font.FontFamily.Serif, fontWeight = FontWeight.Bold)
        Spacer(Modifier.height(8.dp))
        Row(verticalAlignment = Alignment.CenterVertically) {
            Text("TO :", modifier = labelModifier, fontFamily = androidx.compose.ui.text.font.FontFamily.Serif, fontWeight = FontWeight.Bold)
            Text(report.to, modifier = Modifier.weight(1f).border(1.dp, Color.Black).padding(horizontal = 4.dp, vertical = 2.dp), fontFamily = androidx.compose.ui.text.font.FontFamily.Serif, fontWeight = FontWeight.Bold)
            Text("DATE :", modifier = Modifier.width(64.dp), fontFamily = androidx.compose.ui.text.font.FontFamily.Serif, fontWeight = FontWeight.Bold)
            Text(report.date, modifier = Modifier.width(96.dp).border(1.dp, Color.Black).padding(horizontal = 4.dp, vertical = 2.dp), fontFamily = androidx.compose.ui.text.font.FontFamily.Serif, fontWeight = FontWeight.Bold)
        }
        Row(verticalAlignment = Alignment.CenterVertically) {
            Text("FROM :", modifier = labelModifier, fontFamily = androidx.compose.ui.text.font.FontFamily.Serif, fontWeight = FontWeight.Bold)
            Text(report.from, modifier = Modifier.weight(1f).border(1.dp, Color.Black).padding(horizontal = 4.dp, vertical = 2.dp), fontFamily = androidx.compose.ui.text.font.FontFamily.Serif, fontWeight = FontWeight.Bold)
        }
        Spacer(Modifier.height(8.dp))
        Text("RECEIVED THE FOLLOWING ITEMS:", modifier = Modifier.fillMaxWidth().border(1.dp, Color.Black).padding(4.dp), fontFamily = androidx.compose.ui.text.font.FontFamily.Serif, fontWeight = FontWeight.Bold)
        repeat(TransmittalReport.TEMPLATE_ITEM_COUNT) { index ->
            Row(modifier = Modifier.fillMaxWidth().height(21.dp), verticalAlignment = Alignment.CenterVertically) {
                Text("${index + 1}", modifier = Modifier.width(38.dp).padding(end = 4.dp), textAlign = androidx.compose.ui.text.style.TextAlign.End, fontFamily = androidx.compose.ui.text.font.FontFamily.Serif, fontWeight = FontWeight.Bold)
                Text(
                    report.lineText(index),
                    modifier = Modifier.weight(1f).fillMaxWidth().background(Color.Transparent).border(BorderStroke(1.dp, Color.Black)).padding(horizontal = 4.dp, vertical = 2.dp),
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis,
                    fontFamily = androidx.compose.ui.text.font.FontFamily.Serif,
                    style = MaterialTheme.typography.bodySmall
                )
            }
        }
        Text("TRANSMITTAL", modifier = Modifier.fillMaxWidth().padding(top = 5.dp), fontFamily = androidx.compose.ui.text.font.FontFamily.Serif, fontWeight = FontWeight.Bold)
        Row(verticalAlignment = Alignment.CenterVertically) {
            Text("PREPARED BY:", modifier = labelModifier, fontFamily = androidx.compose.ui.text.font.FontFamily.Serif, fontWeight = FontWeight.Bold, style = MaterialTheme.typography.bodySmall)
            Text(report.preparedBy, modifier = Modifier.weight(1f).border(1.dp, Color.Black).padding(horizontal = 4.dp, vertical = 2.dp), style = MaterialTheme.typography.bodySmall)
        }
        Row(verticalAlignment = Alignment.CenterVertically) {
            Text("TRANSMIT BY :", modifier = labelModifier, fontFamily = androidx.compose.ui.text.font.FontFamily.Serif, fontWeight = FontWeight.Bold, style = MaterialTheme.typography.bodySmall)
            Text(report.transmitBy, modifier = Modifier.weight(1f).border(1.dp, Color.Black).padding(horizontal = 4.dp, vertical = 2.dp), style = MaterialTheme.typography.bodySmall)
            Text("RECEIVED BY/DATE :", modifier = Modifier.width(132.dp), fontFamily = androidx.compose.ui.text.font.FontFamily.Serif, fontWeight = FontWeight.Bold, style = MaterialTheme.typography.bodySmall)
            Text(report.receivedByDate, modifier = Modifier.width(86.dp).border(1.dp, Color.Black).padding(horizontal = 4.dp, vertical = 2.dp), style = MaterialTheme.typography.bodySmall)
        }
        Row(verticalAlignment = Alignment.CenterVertically) {
            Text("NOTED BY:", modifier = labelModifier, fontFamily = androidx.compose.ui.text.font.FontFamily.Serif, fontWeight = FontWeight.Bold, style = MaterialTheme.typography.bodySmall)
            Text(report.notedBy, modifier = Modifier.weight(1f).border(1.dp, Color.Black).padding(horizontal = 4.dp, vertical = 2.dp), style = MaterialTheme.typography.bodySmall)
            Text("APPROVED BY/DATE:", modifier = Modifier.width(132.dp), fontFamily = androidx.compose.ui.text.font.FontFamily.Serif, fontWeight = FontWeight.Bold, style = MaterialTheme.typography.bodySmall)
            Text(report.approvedByDate, modifier = Modifier.width(86.dp).border(1.dp, Color.Black).padding(horizontal = 4.dp, vertical = 2.dp), style = MaterialTheme.typography.bodySmall)
        }
    }
}


