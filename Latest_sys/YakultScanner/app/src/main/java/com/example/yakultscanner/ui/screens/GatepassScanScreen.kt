package com.example.yakultscanner.ui.screens

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
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.navigation.NavController
import androidx.navigation.compose.currentBackStackEntryAsState
import com.example.yakultscanner.SCAN_RESULT_SERIAL_KEY
import com.example.yakultscanner.SCAN_RETURN_ROUTE_KEY
import com.example.yakultscanner.SCAN_RETURN_SERIAL_ROUTE
import com.example.yakultscanner.data.model.GatepassCompany
import com.example.yakultscanner.data.model.GatepassFormType
import com.example.yakultscanner.data.model.GatepassItemCategory
import com.example.yakultscanner.data.model.GatepassModel
import com.example.yakultscanner.data.model.GatepassReport
import com.example.yakultscanner.data.model.TransmittalItemDraft
import com.example.yakultscanner.ui.adaptive.LocalAdaptiveLayout
import com.example.yakultscanner.ui.components.ScannerActionBar
import com.example.yakultscanner.ui.components.ScannerMetricChip
import com.example.yakultscanner.ui.components.ScannerSectionHeader
import com.example.yakultscanner.ui.components.ScannerSurfaceCard
import com.example.yakultscanner.ui.components.ScannerWorkspaceHeader
import com.example.yakultscanner.ui.components.ScannerWorkspaceUi
import com.example.yakultscanner.utils.TransmittalDocumentStore
import com.example.yakultscanner.utils.printTransmittalPdf
import com.example.yakultscanner.utils.normalizeSerial
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.util.Locale

private const val MAX_GATEPASS_ITEMS = GatepassReport.TEMPLATE_ITEM_COUNT

private val gatepassItemSaver = listSaver<List<TransmittalItemDraft>, String>(
    save = { items -> items.flatMap { listOf(it.description, it.mobileNumber, it.serialNumber, it.imei1, it.imei2) } },
    restore = { values ->
        values.chunked(5).mapNotNull { chunk ->
            if (chunk.size == 5) {
                TransmittalItemDraft(chunk[0], chunk[1], chunk[2], chunk[3], chunk[4])
            } else {
                null
            }
        }.ifEmpty { listOf(TransmittalItemDraft()) }
    }
)

@androidx.compose.material3.ExperimentalMaterial3Api
@Composable
fun GatepassScanScreen(navController: NavController) {
    val context = androidx.compose.ui.platform.LocalContext.current
    val scope = rememberCoroutineScope()
    val snackbarHostState = remember { SnackbarHostState() }
    val navEntry by navController.currentBackStackEntryAsState()
    val adaptiveLayout = LocalAdaptiveLayout.current

    var formTypeName by rememberSaveable { mutableStateOf(GatepassFormType.File.name) }
    var printAllFormTypes by rememberSaveable { mutableStateOf(false) }
    var companyName by rememberSaveable { mutableStateOf(GatepassCompany.YakultPhilippines.name) }
    var to by rememberSaveable { mutableStateOf("") }
    var from by rememberSaveable { mutableStateOf("") }
    var reportDate by rememberSaveable { mutableStateOf("") }
    var categoryName by rememberSaveable { mutableStateOf("") }
    var categoryOther by rememberSaveable { mutableStateOf("") }
    var modelName by rememberSaveable { mutableStateOf("") }
    var fixedAssetNumber by rememberSaveable { mutableStateOf("") }
    var quantity by rememberSaveable { mutableStateOf("") }
    var others by rememberSaveable { mutableStateOf("") }
    var remarks by rememberSaveable { mutableStateOf("") }
    var issuedBy by rememberSaveable { mutableStateOf("") }
    var notedBy by rememberSaveable { mutableStateOf("") }
    var receivedByDate by rememberSaveable { mutableStateOf("") }
    var approvedByDate by rememberSaveable { mutableStateOf("") }
    var manualSerial by rememberSaveable { mutableStateOf("") }
    var selectedItemIndex by rememberSaveable { mutableIntStateOf(0) }
    var pendingScanItemIndex by rememberSaveable { mutableIntStateOf(0) }
    var busy by rememberSaveable { mutableStateOf(false) }
    var showGatepassPreview by rememberSaveable { mutableStateOf(true) }
    var items by rememberSaveable(stateSaver = gatepassItemSaver) {
        mutableStateOf(listOf(TransmittalItemDraft()))
    }

    fun selectedFormType(): GatepassFormType =
        GatepassFormType.entries.firstOrNull { it.name == formTypeName } ?: GatepassFormType.File

    fun selectedCompany(): GatepassCompany =
        GatepassCompany.entries.firstOrNull { it.name == companyName } ?: GatepassCompany.YakultPhilippines

    fun selectedCategory(): GatepassItemCategory? =
        GatepassItemCategory.entries.firstOrNull { it.name == categoryName }

    fun selectedModel(): GatepassModel? =
        GatepassModel.entries.firstOrNull { it.name == modelName }

    fun report(): GatepassReport = GatepassReport(
        formType = selectedFormType(),
        company = selectedCompany(),
        to = to.trim(),
        from = from.trim(),
        date = reportDate.trim(),
        itemCategory = selectedCategory(),
        itemCategoryOther = categoryOther.trim(),
        model = selectedModel(),
        fixedAssetNumber = fixedAssetNumber.trim(),
        quantity = quantity.trim(),
        items = items,
        others = others.trim(),
        remarks = remarks.trim(),
        issuedBy = issuedBy.trim(),
        notedBy = notedBy.trim(),
        receivedByDate = receivedByDate.trim(),
        approvedByDate = approvedByDate.trim(),
        printAllFormTypes = printAllFormTypes
    )

    fun updateItem(index: Int, update: (TransmittalItemDraft) -> TransmittalItemDraft) {
        items = items.mapIndexed { rowIndex, item ->
            if (rowIndex == index) update(item) else item
        }
    }

    fun applySerial(raw: String, source: String) {
        val value = normalizeSerial(raw)
        if (value.isBlank()) {
            scope.launch { snackbarHostState.showSnackbar("Scan or enter a valid serial number first.") }
            return
        }
        if (items.any { normalizeSerial(it.serialNumber) == value }) {
            scope.launch { snackbarHostState.showSnackbar("Duplicate serial skipped.") }
            return
        }

        var targetIndex = selectedItemIndex.coerceIn(0, items.lastIndex)
        if (items[targetIndex].serialNumber.isNotBlank()) {
            val emptyIndex = items.indexOfFirst { it.serialNumber.isBlank() }
            if (emptyIndex >= 0) {
                targetIndex = emptyIndex
            } else if (items.size < MAX_GATEPASS_ITEMS) {
                items = items + TransmittalItemDraft()
                targetIndex = items.lastIndex
            } else {
                scope.launch { snackbarHostState.showSnackbar("The Gatepass supports up to $MAX_GATEPASS_ITEMS item lines.") }
                return
            }
        }

        updateItem(targetIndex) { it.copy(serialNumber = value) }
        selectedItemIndex = if (targetIndex + 1 < items.size) targetIndex + 1 else targetIndex
        manualSerial = ""
        scope.launch { snackbarHostState.showSnackbar("$source serial added to item ${targetIndex + 1}.") }
    }

    fun openScanner(route: String) {
        val targetIndex = selectedItemIndex.coerceIn(0, items.lastIndex)
        pendingScanItemIndex = targetIndex
        selectedItemIndex = targetIndex
        navController.currentBackStackEntry?.savedStateHandle?.set(
            SCAN_RETURN_ROUTE_KEY,
            SCAN_RETURN_SERIAL_ROUTE
        )
        navController.navigate(route)
    }

    val scannedSerial = remember(navEntry) {
        navEntry?.savedStateHandle?.getStateFlow<String?>(SCAN_RESULT_SERIAL_KEY, null)
    }?.collectAsState(initial = null)?.value

    LaunchedEffect(scannedSerial) {
        val value = scannedSerial?.trim().orEmpty()
        if (value.isBlank()) return@LaunchedEffect
        selectedItemIndex = pendingScanItemIndex.coerceIn(0, items.lastIndex)
        applySerial(value, "Scanner")
        navEntry?.savedStateHandle?.remove<String>(SCAN_RESULT_SERIAL_KEY)
    }

    val currentReport = report()
    val scannedCount = items.count { it.serialNumber.isNotBlank() }
    val canSave = currentReport.hasContent()

    fun exportReport() {
        val snapshot = report()
        if (!snapshot.hasContent()) {
            scope.launch { snackbarHostState.showSnackbar("Add an item or Gatepass detail before saving.") }
            return
        }
        scope.launch {
            busy = true
            val result = withContext(Dispatchers.IO) {
                TransmittalDocumentStore.saveGatepassPdf(context, snapshot)
            }
            busy = false
            result.onSuccess { saved ->
                snackbarHostState.showSnackbar("Saved ${saved.displayName} locally in E Documents.")
            }.onFailure { error ->
                snackbarHostState.showSnackbar(error.message ?: "Gatepass PDF export failed.")
            }
        }
    }

    fun printReport() {
        val snapshot = report()
        if (!snapshot.hasContent()) {
            scope.launch { snackbarHostState.showSnackbar("Add an item or Gatepass detail before printing.") }
            return
        }
        scope.launch {
            busy = true
            val saved = withContext(Dispatchers.IO) {
                TransmittalDocumentStore.saveGatepassPdf(context, snapshot)
            }.getOrElse { error ->
                busy = false
                snackbarHostState.showSnackbar(error.message ?: "Couldn't create the Gatepass PDF.")
                return@launch
            }
            val started = printTransmittalPdf(
                context,
                TransmittalDocumentStore.uriFor(context, saved),
                "Yakult Gatepass"
            )
            busy = false
            if (!started && !TransmittalDocumentStore.share(context, saved)) {
                snackbarHostState.showSnackbar("No print service is enabled. The Gatepass was saved locally.")
            }
        }
    }

    Scaffold(
        containerColor = ScannerWorkspaceUi.Canvas,
        topBar = {
            TopAppBar(
                title = {
                    Text(
                        "Gatepass / File Transmittal",
                        color = ScannerWorkspaceUi.Ink,
                        fontWeight = FontWeight.Bold
                    )
                },
                navigationIcon = {
                    IconButton(onClick = { navController.popBackStack() }) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back", tint = ScannerWorkspaceUi.Ink)
                    }
                },
                actions = {
                    IconButton(onClick = { printReport() }, enabled = !busy && canSave) {
                        Icon(Icons.Filled.Print, contentDescription = "Print Gatepass", tint = ScannerWorkspaceUi.Brand)
                    }
                    IconButton(onClick = { navController.navigate("saved_transmittal_files") }, enabled = !busy) {
                        Icon(Icons.Filled.Folder, contentDescription = "Saved E Documents", tint = ScannerWorkspaceUi.Brand)
                    }
                    IconButton(onClick = { exportReport() }, enabled = !busy && canSave) {
                        Icon(Icons.Filled.FileDownload, contentDescription = "Save Gatepass PDF", tint = ScannerWorkspaceUi.Brand)
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(containerColor = ScannerWorkspaceUi.Surface)
            )
        },
        snackbarHost = { SnackbarHost(snackbarHostState) },
        bottomBar = {
            if (!adaptiveLayout.isTablet) {
                ScannerActionBar(
                    primaryLabel = if (busy) "Saving Gatepass…" else "Save Gatepass PDF",
                    onPrimary = { exportReport() },
                    primaryEnabled = !busy && canSave
                )
            }
        }
    ) { paddingValues ->
        val workspaceHeader: @Composable () -> Unit = {
            ScannerWorkspaceHeader(
                title = "Scan, review, save locally",
                subtitle = "Use the same camera/device serial scanning pattern as Transmittal Scan. This Gatepass never uploads to the API.",
                trailing = { ScannerMetricChip("Serials", "$scannedCount/${items.size}") }
            )
        }

        val documentSection: @Composable () -> Unit = {
            ScannerSurfaceCard(modifier = Modifier.fillMaxWidth()) {
                ScannerSectionHeader("Document type and company", "Select the checkbox values represented on the paper Gatepass form.")
                ChoiceRow(
                    label = "Form type",
                    options = GatepassFormType.entries.map { it.name to it.label },
                    selected = formTypeName,
                    onSelected = { formTypeName = it }
                )
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Column(modifier = Modifier.weight(1f)) {
                        Text("Print all three form types", fontWeight = FontWeight.Bold)
                        Text(
                            "Sheet 1: Gatepass + Transmittal. Sheet 2: File on top; data-only copy below.",
                            style = MaterialTheme.typography.bodySmall,
                            color = ScannerWorkspaceUi.Muted
                        )
                    }
                    androidx.compose.material3.Switch(
                        checked = printAllFormTypes,
                        onCheckedChange = { printAllFormTypes = it }
                    )
                }
                ChoiceRow(
                    label = "Company",
                    options = GatepassCompany.entries.map { it.name to it.label },
                    selected = companyName,
                    onSelected = { companyName = it }
                )
            }
        }

        val detailsSection: @Composable () -> Unit = {
            ScannerSurfaceCard(modifier = Modifier.fillMaxWidth()) {
                ScannerSectionHeader("Gatepass details", "These values are kept locally and printed on the form.")
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    OutlinedTextField(
                        value = to,
                        onValueChange = { to = it },
                        modifier = Modifier.weight(1f),
                        label = { Text("To") },
                        placeholder = { Text("Information Technology Department") },
                        singleLine = true
                    )
                    OutlinedTextField(
                        value = reportDate,
                        onValueChange = { reportDate = it },
                        modifier = Modifier.weight(1f),
                        label = { Text("Date") },
                        singleLine = true
                    )
                }
                OutlinedTextField(
                    value = from,
                    onValueChange = { from = it },
                    modifier = Modifier.fillMaxWidth(),
                    label = { Text("From") },
                    singleLine = true
                )
            }
        }

        val itemSelectionSection: @Composable () -> Unit = {
            ScannerSurfaceCard(modifier = Modifier.fillMaxWidth()) {
                ScannerSectionHeader("Item selection", "Choose the marked item and model from the paper form.")
                ChoiceRow(
                    label = "Item",
                    options = GatepassItemCategory.entries.map { it.name to it.label },
                    selected = categoryName,
                    onSelected = { categoryName = if (categoryName == it) "" else it },
                    allowClear = true,
                    onClear = { categoryName = "" }
                )
                if (selectedCategory() == GatepassItemCategory.Others) {
                    OutlinedTextField(
                        value = categoryOther,
                        onValueChange = { categoryOther = it },
                        modifier = Modifier.fillMaxWidth(),
                        label = { Text("Other item description") },
                        singleLine = true
                    )
                }
                ChoiceRow(
                    label = "Model",
                    options = GatepassModel.entries.map { it.name to it.label },
                    selected = modelName,
                    onSelected = { modelName = if (modelName == it) "" else it },
                    allowClear = true,
                    onClear = { modelName = "" }
                )
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    OutlinedTextField(
                        value = fixedAssetNumber,
                        onValueChange = { fixedAssetNumber = it },
                        modifier = Modifier.weight(1f),
                        label = { Text("Fixed asset no.") },
                        singleLine = true
                    )
                    OutlinedTextField(
                        value = quantity,
                        onValueChange = { quantity = it.filter(Char::isDigit) },
                        modifier = Modifier.weight(1f),
                        label = { Text("Quantity") },
                        singleLine = true
                    )
                }
            }
        }

        val scannerControlsSection: @Composable () -> Unit = {
            ScannerSurfaceCard(modifier = Modifier.fillMaxWidth()) {
                ScannerSectionHeader("Scanner controls", "Select a line, then use the camera, Honeywell device, or manual entry.")
                OutlinedTextField(
                    value = manualSerial,
                    onValueChange = { manualSerial = it },
                    modifier = Modifier.fillMaxWidth(),
                    label = { Text("Serial number") },
                    singleLine = true
                )
                Column(
                    verticalArrangement = Arrangement.spacedBy(8.dp),
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Button(
                        onClick = { applySerial(manualSerial, "Manual") },
                        modifier = Modifier.fillMaxWidth().height(50.dp),
                        contentPadding = PaddingValues(horizontal = 14.dp, vertical = 8.dp)
                    ) {
                        Icon(Icons.Filled.Add, contentDescription = null, modifier = Modifier.size(18.dp))
                        Spacer(Modifier.width(6.dp))
                        Text("Add serial", maxLines = 1, softWrap = false)
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
            ScannerSurfaceCard(modifier = Modifier.fillMaxWidth()) {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.SpaceBetween
                ) {
                    ScannerSectionHeader(
                        "Numbered items",
                        "These serials appear in the item details section of the local PDF.",
                        Modifier.weight(1f)
                    )
                    TextButton(
                        onClick = {
                            if (items.size < MAX_GATEPASS_ITEMS) items = items + TransmittalItemDraft()
                        },
                        enabled = items.size < MAX_GATEPASS_ITEMS
                    ) {
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
                        Text(
                            "${index + 1}",
                            modifier = Modifier.padding(horizontal = 11.dp, vertical = 7.dp),
                            fontWeight = FontWeight.Bold,
                            color = ScannerWorkspaceUi.Ink
                        )
                    }
                    Spacer(Modifier.width(8.dp))
                    Text(
                        if (index == selectedItemIndex) "Selected scan line" else "Item ${index + 1}",
                        fontWeight = FontWeight.Bold,
                        modifier = Modifier.weight(1f)
                    )
                    TextButton(onClick = { selectedItemIndex = index }) { Text("Select") }
                    IconButton(
                        onClick = {
                            if (items.size > 1) {
                                items = items.filterIndexed { rowIndex, _ -> rowIndex != index }
                                selectedItemIndex = selectedItemIndex.coerceAtMost(items.lastIndex)
                            }
                        },
                        enabled = items.size > 1
                    ) {
                        Icon(Icons.Filled.Delete, contentDescription = "Remove item ${index + 1}", tint = ScannerWorkspaceUi.Error)
                    }
                }
                OutlinedTextField(
                    value = item.description,
                    onValueChange = { value -> updateItem(index) { it.copy(description = value) } },
                    modifier = Modifier.fillMaxWidth(),
                    label = { Text("Description / item line") },
                    singleLine = true
                )
                OutlinedTextField(
                    value = item.serialNumber,
                    onValueChange = { value -> updateItem(index) { it.copy(serialNumber = value) } },
                    modifier = Modifier.fillMaxWidth(),
                    label = { Text("Serial") },
                    singleLine = true
                )
            }
        }

        val otherDetailsSection: @Composable () -> Unit = {
            ScannerSurfaceCard(modifier = Modifier.fillMaxWidth()) {
                ScannerSectionHeader("Other details", "Optional notes and sign-off fields from the paper form.")
                OutlinedTextField(
                    value = others,
                    onValueChange = { others = it },
                    modifier = Modifier.fillMaxWidth(),
                    label = { Text("Others") },
                    singleLine = true
                )
                OutlinedTextField(
                    value = remarks,
                    onValueChange = { remarks = it },
                    modifier = Modifier.fillMaxWidth(),
                    label = { Text("Remarks") },
                    singleLine = true
                )
                OutlinedTextField(
                    value = issuedBy,
                    onValueChange = { issuedBy = it },
                    modifier = Modifier.fillMaxWidth(),
                    label = { Text("Issued by") },
                    singleLine = true
                )
                OutlinedTextField(
                    value = notedBy,
                    onValueChange = { notedBy = it },
                    modifier = Modifier.fillMaxWidth(),
                    label = { Text("Noted by") },
                    singleLine = true
                )
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    OutlinedTextField(
                        value = receivedByDate,
                        onValueChange = { receivedByDate = it },
                        modifier = Modifier.weight(1f),
                        label = { Text("Received by / date") },
                        singleLine = true
                    )
                    OutlinedTextField(
                        value = approvedByDate,
                        onValueChange = { approvedByDate = it },
                        modifier = Modifier.weight(1f),
                        label = { Text("Approved by / date") },
                        singleLine = true
                    )
                }
            }
        }

        val previewSection: @Composable () -> Unit = {
            ScannerSurfaceCard(modifier = Modifier.fillMaxWidth()) {
                Row(verticalAlignment = Alignment.CenterVertically, modifier = Modifier.fillMaxWidth()) {
                    ScannerSectionHeader(
                        "Gatepass preview",
                        "This preview follows the photographed Gatepass/File Transmittal layout.",
                        Modifier.weight(1f)
                    )
                    Column(horizontalAlignment = Alignment.End) {
                        Text(
                            if (showGatepassPreview) "Shown" else "Hidden",
                            style = MaterialTheme.typography.labelSmall,
                            color = ScannerWorkspaceUi.Muted
                        )
                        androidx.compose.material3.Switch(
                            checked = showGatepassPreview,
                            onCheckedChange = { showGatepassPreview = it }
                        )
                    }
                }
                if (showGatepassPreview) {
                    GatepassSheetPreview(currentReport)
                } else {
                    Surface(
                        modifier = Modifier.fillMaxWidth(),
                        shape = RoundedCornerShape(14.dp),
                        color = ScannerWorkspaceUi.SurfaceSubtle
                    ) {
                        Text(
                            "Gatepass preview collapsed. Turn on the toggle to expand it.",
                            modifier = Modifier.padding(12.dp),
                            style = MaterialTheme.typography.bodySmall,
                            color = ScannerWorkspaceUi.Muted
                        )
                    }
                }
            }
        }

        val localActionsSection: @Composable () -> Unit = {
            ScannerSurfaceCard(modifier = Modifier.fillMaxWidth()) {
                ScannerSectionHeader("Local actions", "Saving creates a PDF in the app's local E Documents archive. No API call is made.")
                Text(
                    "${items.count { it.hasAnyValue() }} of ${items.size} item lines contain values.",
                    style = MaterialTheme.typography.bodySmall,
                    color = ScannerWorkspaceUi.Muted
                )
                Button(
                    onClick = { exportReport() },
                    enabled = !busy && canSave,
                    modifier = Modifier.fillMaxWidth().height(50.dp),
                    contentPadding = PaddingValues(horizontal = 14.dp, vertical = 8.dp)
                ) {
                    Icon(Icons.Filled.FileDownload, contentDescription = null, modifier = Modifier.size(18.dp))
                    Spacer(Modifier.width(6.dp))
                    Text(if (busy) "Saving Gatepass…" else "Save Gatepass PDF", maxLines = 1, softWrap = false)
                }
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.fillMaxWidth()) {
                    OutlinedButton(
                        onClick = { printReport() },
                        enabled = !busy && canSave,
                        modifier = Modifier.weight(1f).height(48.dp),
                        contentPadding = PaddingValues(horizontal = 8.dp, vertical = 8.dp)
                    ) {
                        Icon(Icons.Filled.Print, contentDescription = null, modifier = Modifier.size(18.dp))
                        Spacer(Modifier.width(4.dp))
                        Text("Print", maxLines = 1, softWrap = false)
                    }
                    OutlinedButton(
                        onClick = { navController.navigate("saved_transmittal_files") },
                        enabled = !busy,
                        modifier = Modifier.weight(1f).height(48.dp),
                        contentPadding = PaddingValues(horizontal = 8.dp, vertical = 8.dp)
                    ) {
                        Icon(Icons.Filled.Folder, contentDescription = null, modifier = Modifier.size(18.dp))
                        Spacer(Modifier.width(4.dp))
                        Text("Saved files", maxLines = 1, softWrap = false)
                    }
                }
            }
        }

        if (!adaptiveLayout.isTablet) {
            LazyColumn(
                modifier = Modifier
                    .fillMaxSize()
                    .background(ScannerWorkspaceUi.Canvas)
                    .padding(paddingValues),
                contentPadding = PaddingValues(horizontal = 16.dp, vertical = 14.dp),
                verticalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                item { workspaceHeader() }
                item { documentSection() }
                item { detailsSection() }
                item { itemSelectionSection() }
                item { scannerControlsSection() }
                item { numberedItemsHeader() }
                itemsIndexed(items) { index, item -> itemCard(index, item) }
                item { otherDetailsSection() }
                item { previewSection() }
                item { localActionsSection() }
                item { Spacer(Modifier.height(24.dp)) }
            }
        } else {
            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .background(ScannerWorkspaceUi.Canvas)
                    .padding(paddingValues)
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
                        item { documentSection() }
                        item { detailsSection() }
                        item { itemSelectionSection() }
                        item { scannerControlsSection() }
                        item {
                            ScannerSurfaceCard(modifier = Modifier.fillMaxWidth()) {
                                ScannerSectionHeader("Scan workspace", "The camera and device scanner continue in their existing dedicated scan screens.")
                                Text("Selected line: ${selectedItemIndex + 1}", fontWeight = FontWeight.Bold, color = ScannerWorkspaceUi.Ink)
                                Text("Progress: $scannedCount/${items.size}", style = MaterialTheme.typography.bodySmall, color = ScannerWorkspaceUi.Muted)
                            }
                        }
                    }
                    LazyColumn(
                        modifier = Modifier.weight(0.56f).fillMaxHeight(),
                        verticalArrangement = Arrangement.spacedBy(12.dp),
                        contentPadding = PaddingValues(bottom = 24.dp)
                    ) {
                        item { localActionsSection() }
                        item { numberedItemsHeader() }
                        itemsIndexed(items) { index, item -> itemCard(index, item) }
                        item { otherDetailsSection() }
                        item { previewSection() }
                    }
                }
            }
        }
    }
}

@Composable
private fun ChoiceRow(
    label: String,
    options: List<Pair<String, String>>,
    selected: String,
    onSelected: (String) -> Unit,
    allowClear: Boolean = false,
    onClear: (() -> Unit)? = null
) {
    Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Text(label, style = MaterialTheme.typography.labelLarge, fontWeight = FontWeight.Bold)
            if (allowClear && selected.isNotBlank() && onClear != null) {
                TextButton(onClick = onClear) { Text("Clear") }
            }
        }
        Row(
            modifier = Modifier.horizontalScroll(rememberScrollState()),
            horizontalArrangement = Arrangement.spacedBy(6.dp)
        ) {
            options.forEach { (value, text) ->
                FilterChip(
                    selected = selected == value,
                    onClick = { onSelected(value) },
                    label = { Text(text) }
                )
            }
        }
    }
}

@Composable
private fun GatepassSheetPreview(report: GatepassReport) {
    val horizontalScroll = rememberScrollState()
    Box(modifier = Modifier.fillMaxWidth().horizontalScroll(horizontalScroll)) {
        Column(
            modifier = Modifier
                .width(680.dp)
                .background(Color.White)
                .border(1.dp, Color.Black)
                .padding(12.dp),
            verticalArrangement = Arrangement.spacedBy(6.dp)
        ) {
            Text(
                "GATEPASS / FILE TRANSMITTAL",
                modifier = Modifier.fillMaxWidth(),
                textAlign = TextAlign.Center,
                fontFamily = FontFamily.Serif,
                fontWeight = FontWeight.Bold
            )
            Row(horizontalArrangement = Arrangement.spacedBy(16.dp)) {
                PreviewCheck("GATEPASS", report.formType == GatepassFormType.Gatepass)
                PreviewCheck("TRANSMITTAL", report.formType == GatepassFormType.Transmittal)
                PreviewCheck("FILE", report.formType == GatepassFormType.File)
            }
            Row(horizontalArrangement = Arrangement.spacedBy(16.dp)) {
                PreviewCheck("YAKULT PHILIPPINES INC.", report.company == GatepassCompany.YakultPhilippines)
                PreviewCheck("YAKULT MARKETING CORP.", report.company == GatepassCompany.YakultMarketing)
            }
            PreviewValueRow("TO", report.to, "DATE", report.date)
            PreviewValueRow("FROM", report.from)
            Row(horizontalArrangement = Arrangement.spacedBy(18.dp)) {
                Column(modifier = Modifier.weight(1f)) {
                    Text("ITEMS", fontWeight = FontWeight.Bold)
                    GatepassItemCategory.entries.forEach { category ->
                        PreviewCheck(category.label, report.itemCategory == category)
                    }
                }
                Column(modifier = Modifier.weight(1f)) {
                    Text("MODEL / ASSET", fontWeight = FontWeight.Bold)
                    PreviewCheck("LX300", report.model == GatepassModel.Lx300)
                    PreviewCheck("LX310", report.model == GatepassModel.Lx310)
                    PreviewValueRow("FIXED ASSET NO.", report.fixedAssetNumber)
                    PreviewValueRow("QUANTITY", report.quantity)
                }
            }
            Text("SCANNED / MANUAL ITEM DETAILS", fontWeight = FontWeight.Bold)
            repeat(GatepassReport.TEMPLATE_ITEM_COUNT) { index ->
                Row(modifier = Modifier.fillMaxWidth().height(20.dp), verticalAlignment = Alignment.CenterVertically) {
                    Text("${index + 1}", modifier = Modifier.width(32.dp), textAlign = TextAlign.End)
                    Text(
                        report.lineText(index),
                        modifier = Modifier.weight(1f).border(BorderStroke(1.dp, Color.Black)).padding(horizontal = 5.dp, vertical = 2.dp),
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis,
                        style = MaterialTheme.typography.bodySmall
                    )
                }
            }
            PreviewValueRow("OTHERS", report.others)
            PreviewValueRow("REMARKS", report.remarks)
            Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                Column(modifier = Modifier.weight(1f)) {
                    PreviewValueRow("ISSUED BY", report.issuedBy)
                    PreviewValueRow("NOTED BY", report.notedBy)
                }
                Column(modifier = Modifier.weight(1f)) {
                    PreviewValueRow("RECEIVED BY / DATE", report.receivedByDate)
                    PreviewValueRow("APPROVED BY / DATE", report.approvedByDate)
                }
            }
        }
    }
}

@Composable
private fun PreviewCheck(label: String, checked: Boolean) {
    Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(4.dp)) {
        Text(if (checked) "☑" else "☐", fontWeight = FontWeight.Bold)
        Text(label, style = MaterialTheme.typography.bodySmall)
    }
}

@Composable
private fun PreviewValueRow(
    firstLabel: String,
    firstValue: String,
    secondLabel: String? = null,
    secondValue: String = ""
) {
    Row(horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.fillMaxWidth()) {
        Column(modifier = Modifier.weight(1f)) {
            Text(firstLabel, style = MaterialTheme.typography.labelSmall, fontWeight = FontWeight.Bold)
            Text(firstValue, modifier = Modifier.fillMaxWidth().border(1.dp, Color.Black).padding(4.dp), style = MaterialTheme.typography.bodySmall)
        }
        if (secondLabel != null) {
            Column(modifier = Modifier.weight(1f)) {
                Text(secondLabel, style = MaterialTheme.typography.labelSmall, fontWeight = FontWeight.Bold)
                Text(secondValue, modifier = Modifier.fillMaxWidth().border(1.dp, Color.Black).padding(4.dp), style = MaterialTheme.typography.bodySmall)
            }
        }
    }
}
