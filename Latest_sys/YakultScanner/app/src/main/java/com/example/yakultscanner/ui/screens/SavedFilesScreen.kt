package com.example.yakultscanner.ui.screens

import android.widget.Toast
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.ArrowDropDown
import androidx.compose.material.icons.filled.Clear
import androidx.compose.material.icons.filled.Description
import androidx.compose.material.icons.filled.Folder
import androidx.compose.material.icons.filled.PictureAsPdf
import androidx.compose.material.icons.filled.Print
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.Search
import androidx.compose.material.icons.filled.Sort
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
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
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.navigation.NavController
import com.example.yakultscanner.ui.adaptive.LocalAdaptiveLayout
import com.example.yakultscanner.ui.components.ScannerWorkspaceUi
import com.example.yakultscanner.utils.AppFileStore
import com.example.yakultscanner.utils.SavedAppFile
import com.example.yakultscanner.utils.formatSavedFileSize
import com.example.yakultscanner.utils.formatSavedFileTime
import com.example.yakultscanner.utils.printTransmittalPdf
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.util.Locale

private enum class FileSortOption(val label: String) {
    Newest("Newest"),
    Oldest("Oldest"),
    Name("Name A–Z"),
    Largest("Largest")
}

/** Dedicated app-managed Files page. Any external file is copied here only after user selection. */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun SavedFilesScreen(navController: NavController) {
    val context = LocalContext.current
    val adaptive = LocalAdaptiveLayout.current
    val scope = rememberCoroutineScope()
    var files by remember { mutableStateOf(emptyList<SavedAppFile>()) }
    var isBusy by remember { mutableStateOf(false) }
    var searchQuery by remember { mutableStateOf("") }
    var sortOption by remember { mutableStateOf(FileSortOption.Newest) }
    var sortMenuExpanded by remember { mutableStateOf(false) }
    var showPrintDialog by remember { mutableStateOf(false) }

    suspend fun refreshFiles() {
        isBusy = true
        try {
            files = withContext(Dispatchers.IO) { AppFileStore.list(context) }
        } finally {
            isBusy = false
        }
    }

    val importLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.OpenMultipleDocuments()
    ) { selectedUris ->
        if (selectedUris.isNotEmpty()) {
            scope.launch {
                isBusy = true
                try {
                    val results = withContext(Dispatchers.IO) {
                        selectedUris.map { uri -> AppFileStore.importUri(context, uri) }
                    }
                    files = withContext(Dispatchers.IO) { AppFileStore.list(context) }
                    val failedCount = results.count { it.isFailure }
                    if (failedCount > 0) {
                        Toast.makeText(
                            context,
                            "Imported ${results.size - failedCount} file(s); $failedCount could not be copied.",
                            Toast.LENGTH_LONG
                        ).show()
                    } else {
                        Toast.makeText(context, "Imported ${results.size} file(s).", Toast.LENGTH_SHORT).show()
                    }
                } finally {
                    isBusy = false
                }
            }
        }
    }

    fun launchImport() {
        importLauncher.launch(arrayOf("*/*"))
    }

    val normalizedQuery = searchQuery.trim()
    val filteredFiles = if (normalizedQuery.isBlank()) {
        files
    } else {
        files.filter { file ->
            file.displayName.contains(normalizedQuery, ignoreCase = true) ||
                file.formatLabel.contains(normalizedQuery, ignoreCase = true) ||
                file.extension.contains(normalizedQuery, ignoreCase = true) ||
                savedFileSystemSection(file.displayName).contains(normalizedQuery, ignoreCase = true)
        }
    }
    val groupedFiles = groupFiles(filteredFiles, sortOption)
    val savedPdfFiles = files.filter(SavedAppFile::isPdf)

    fun launchPrint() {
        if (savedPdfFiles.isEmpty()) {
            Toast.makeText(context, "No saved PDF files to print.", Toast.LENGTH_LONG).show()
        } else {
            showPrintDialog = true
        }
    }

    LaunchedEffect(Unit) {
        refreshFiles()
    }

    Scaffold(
        containerColor = ScannerWorkspaceUi.Canvas,
        topBar = {
            TopAppBar(
                title = {
                    Text(
                        "Files",
                        color = ScannerWorkspaceUi.Ink,
                        fontWeight = FontWeight.Bold
                    )
                },
                navigationIcon = {
                    IconButton(onClick = { navController.popBackStack() }) {
                        Icon(
                            Icons.AutoMirrored.Filled.ArrowBack,
                            contentDescription = "Back",
                            tint = ScannerWorkspaceUi.Ink
                        )
                    }
                },
                actions = {
                    IconButton(onClick = { launchImport() }, enabled = !isBusy) {
                        Icon(
                            Icons.Filled.Add,
                            contentDescription = "Import files",
                            tint = ScannerWorkspaceUi.Brand
                        )
                    }
                    IconButton(onClick = { launchPrint() }, enabled = !isBusy) {
                        Icon(
                            Icons.Filled.Print,
                            contentDescription = "Print saved PDF",
                            tint = ScannerWorkspaceUi.Brand
                        )
                    }
                    IconButton(
                        onClick = { scope.launch { refreshFiles() } },
                        enabled = !isBusy
                    ) {
                        Icon(
                            Icons.Filled.Refresh,
                            contentDescription = "Refresh files",
                            tint = ScannerWorkspaceUi.Brand
                        )
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = ScannerWorkspaceUi.Surface
                )
            )
        }
    ) { paddingValues ->
        if (files.isEmpty()) {
            SavedFilesEmptyState(
                paddingValues = paddingValues,
                onImport = { launchImport() },
                enabled = !isBusy
            )
        } else {
            LazyColumn(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(paddingValues),
                contentPadding = PaddingValues(
                    horizontal = adaptive.horizontalPadding,
                    vertical = adaptive.sectionSpacing + 8.dp
                ),
                verticalArrangement = Arrangement.spacedBy(adaptive.sectionSpacing)
            ) {
                item(key = "file-tools") {
                    Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Column(modifier = Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(3.dp)) {
                                Text(
                                    text = "Saved files",
                                    style = MaterialTheme.typography.titleMedium,
                                    fontWeight = FontWeight.Bold
                                )
                                Text(
                                    text = "${filteredFiles.size} of ${files.size} files · grouped by system section",
                                    style = MaterialTheme.typography.bodySmall,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                )
                            }
                            OutlinedButton(onClick = { launchImport() }, enabled = !isBusy) {
                                Text("Import")
                            }
                        }
                        OutlinedTextField(
                            value = searchQuery,
                            onValueChange = { searchQuery = it },
                            modifier = Modifier.fillMaxWidth(),
                            singleLine = true,
                            placeholder = { Text("Search by file name or type") },
                            leadingIcon = {
                                Icon(Icons.Filled.Search, contentDescription = null)
                            },
                            trailingIcon = {
                                if (searchQuery.isNotEmpty()) {
                                    IconButton(onClick = { searchQuery = "" }) {
                                        Icon(Icons.Filled.Clear, contentDescription = "Clear search")
                                    }
                                }
                            }
                        )
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            verticalAlignment = Alignment.CenterVertically,
                            horizontalArrangement = Arrangement.SpaceBetween
                        ) {
                            Text(
                                text = "Grouped by system section",
                                style = MaterialTheme.typography.labelLarge,
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                            Box {
                                OutlinedButton(onClick = { sortMenuExpanded = true }) {
                                    Icon(Icons.Filled.Sort, contentDescription = null)
                                    Text(
                                        text = sortOption.label,
                                        modifier = Modifier.padding(horizontal = 6.dp)
                                    )
                                    Icon(Icons.Filled.ArrowDropDown, contentDescription = "Choose sort order")
                                }
                                DropdownMenu(
                                    expanded = sortMenuExpanded,
                                    onDismissRequest = { sortMenuExpanded = false }
                                ) {
                                    FileSortOption.entries.forEach { option ->
                                        DropdownMenuItem(
                                            text = { Text(option.label) },
                                            onClick = {
                                                sortOption = option
                                                sortMenuExpanded = false
                                            }
                                        )
                                    }
                                }
                            }
                        }
                    }
                }

                if (groupedFiles.isEmpty()) {
                    item(key = "no-search-results") {
                        NoSearchResults(
                            query = normalizedQuery,
                            onClear = { searchQuery = "" }
                        )
                    }
                } else {
                    groupedFiles.forEach { (formatLabel, group) ->
                        item(key = "group-$formatLabel") {
                            FileTypeHeader(formatLabel = formatLabel, count = group.size)
                        }
                        group.chunked(adaptive.gridColumns).forEachIndexed { rowIndex, rowFiles ->
                            item(key = "group-$formatLabel-row-$rowIndex") {
                                Row(
                                    modifier = Modifier.fillMaxWidth(),
                                    horizontalArrangement = Arrangement.spacedBy(adaptive.cardSpacing),
                                    verticalAlignment = Alignment.Top
                                ) {
                                    rowFiles.forEach { file ->
                                        Box(modifier = Modifier.weight(1f)) {
                                            SavedFileCard(file)
                                        }
                                    }
                                    repeat(adaptive.gridColumns - rowFiles.size) {
                                        Spacer(modifier = Modifier.weight(1f))
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    if (showPrintDialog) {
        AlertDialog(
            onDismissRequest = { showPrintDialog = false },
            title = { Text("Print saved PDF") },
            text = {
                if (savedPdfFiles.isEmpty()) {
                    Text("No saved PDF files are available to print.")
                } else {
                    Column(
                        modifier = Modifier
                            .heightIn(max = 360.dp)
                            .verticalScroll(rememberScrollState()),
                        verticalArrangement = Arrangement.spacedBy(4.dp)
                    ) {
                        Text(
                            text = "Select a PDF to send to Mopria or Android printing. XLSX files must be printed from Excel or another spreadsheet app.",
                            style = MaterialTheme.typography.bodyMedium
                        )
                        savedPdfFiles.forEach { file ->
                            TextButton(
                                modifier = Modifier.fillMaxWidth(),
                                onClick = {
                                    showPrintDialog = false
                                    val started = printTransmittalPdf(
                                        context = context,
                                        pdfUri = AppFileStore.uriFor(context, file),
                                        jobName = file.displayName
                                    )
                                    if (!started) {
                                        Toast.makeText(
                                            context,
                                            "Couldn't start printing ${file.displayName} on this device.",
                                            Toast.LENGTH_LONG
                                        ).show()
                                    }
                                }
                            ) {
                                Icon(
                                    imageVector = Icons.Filled.PictureAsPdf,
                                    contentDescription = null,
                                    tint = MaterialTheme.colorScheme.error
                                )
                                Text(
                                    text = file.displayName,
                                    modifier = Modifier.padding(start = 8.dp),
                                    maxLines = 2,
                                    overflow = TextOverflow.Ellipsis
                                )
                            }
                        }
                    }
                }
            },
            confirmButton = {
                TextButton(onClick = { showPrintDialog = false }) {
                    Text("Cancel")
                }
            }
        )
    }
}

internal fun savedFileSystemSection(fileName: String): String {
    val normalizedName = fileName.trim().lowercase(Locale.ROOT)

    fun hasPrefix(prefix: String): Boolean {
        val normalizedPrefix = prefix.lowercase(Locale.ROOT)
        return normalizedName.startsWith("${normalizedPrefix}_") ||
            normalizedName.startsWith("${normalizedPrefix}.")
    }

    return when {
        hasPrefix("Gatepass") -> "Gatepass / File Transmittal"
        hasPrefix("Transmittal") -> "Transmittal"
        hasPrefix("Dispatch") -> "Dispatch / Receipt Sets"
        hasPrefix("Report") -> "Reports"
        hasPrefix("BorrowLog") -> "Borrow Records"
        hasPrefix("CallMonitoring") || hasPrefix("Ticket") -> "Call Monitoring / ITCM"
        hasPrefix("LocalSerialScan") -> "Serial Scanning"
        hasPrefix("Pending") || hasPrefix("Processed") -> "Updates / Sync"
        hasPrefix("Imported") -> "Imported Files"
        else -> "Other Saved Files"
    }
}

private fun groupFiles(
    files: List<SavedAppFile>,
    sortOption: FileSortOption
): List<Pair<String, List<SavedAppFile>>> {
    val groups = files.groupBy { savedFileSystemSection(it.displayName) }.map { (label, group) ->
        label to group.sortedWith(fileComparator(sortOption))
    }
    val groupComparator: Comparator<Pair<String, List<SavedAppFile>>> = when (sortOption) {
        FileSortOption.Name -> compareBy { it.first.lowercase(Locale.ROOT) }
        FileSortOption.Oldest -> compareBy { it.second.minOfOrNull(SavedAppFile::modifiedAt) ?: Long.MAX_VALUE }
        FileSortOption.Largest -> compareByDescending { it.second.maxOfOrNull(SavedAppFile::sizeBytes) ?: 0L }
        FileSortOption.Newest -> compareByDescending { it.second.maxOfOrNull(SavedAppFile::modifiedAt) ?: 0L }
    }
    return groups.sortedWith(groupComparator)
}

private fun fileComparator(sortOption: FileSortOption): Comparator<SavedAppFile> = when (sortOption) {
    FileSortOption.Newest -> compareByDescending(SavedAppFile::modifiedAt)
    FileSortOption.Oldest -> compareBy(SavedAppFile::modifiedAt)
    FileSortOption.Name -> compareBy { it.displayName.lowercase(Locale.ROOT) }
    FileSortOption.Largest -> compareByDescending(SavedAppFile::sizeBytes)
}

@Composable
private fun FileTypeHeader(formatLabel: String, count: Int) {
    Surface(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(12.dp),
        color = MaterialTheme.colorScheme.secondaryContainer.copy(alpha = 0.62f)
    ) {
        Row(
            modifier = Modifier.padding(horizontal = 12.dp, vertical = 8.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.SpaceBetween
        ) {
            Text(
                text = formatLabel,
                style = MaterialTheme.typography.titleSmall,
                fontWeight = FontWeight.Bold,
                color = MaterialTheme.colorScheme.onSecondaryContainer
            )
            Text(
                text = "$count ${if (count == 1) "file" else "files"}",
                style = MaterialTheme.typography.labelMedium,
                color = MaterialTheme.colorScheme.onSecondaryContainer
            )
        }
    }
}

@Composable
private fun NoSearchResults(query: String, onClear: () -> Unit) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(16.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
    ) {
        Column(
            modifier = Modifier.padding(20.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            Text(
                text = "No files match \"$query\".",
                style = MaterialTheme.typography.titleSmall,
                fontWeight = FontWeight.Bold
            )
            TextButton(onClick = onClear) { Text("Clear search") }
        }
    }
}

@Composable
private fun SavedFilesEmptyState(
    paddingValues: PaddingValues,
    onImport: () -> Unit,
    enabled: Boolean
) {
    val adaptive = LocalAdaptiveLayout.current
    Box(
        modifier = Modifier
            .fillMaxSize()
            .padding(paddingValues)
            .padding(
                horizontal = adaptive.horizontalPadding,
                vertical = adaptive.sectionSpacing + 12.dp
            ),
        contentAlignment = Alignment.Center
    ) {
        Card(
            modifier = Modifier.fillMaxWidth(),
            shape = RoundedCornerShape(20.dp),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
            elevation = CardDefaults.cardElevation(defaultElevation = 2.dp)
        ) {
            Column(
                modifier = Modifier.padding(adaptive.horizontalPadding + 4.dp),
                horizontalAlignment = Alignment.CenterHorizontally,
                verticalArrangement = Arrangement.spacedBy(adaptive.sectionSpacing)
            ) {
                Icon(
                    imageVector = Icons.Filled.Folder,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.primary,
                    modifier = Modifier.size(42.dp)
                )
                Text(
                    text = "No saved files yet",
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.Bold
                )
                Text(
                    text = "Export a Transmittal or import any file format from Android's file picker.",
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
                Button(onClick = onImport, enabled = enabled) {
                    Icon(Icons.Filled.Add, contentDescription = null)
                    Text("Import files", modifier = Modifier.padding(start = 8.dp))
                }
            }
        }
    }
}

@Composable
private fun SavedFileCard(file: SavedAppFile) {
    val context = LocalContext.current
    val actionLabel = if (file.isPdf) "Print PDF" else "Open"

    Surface(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(16.dp),
        color = MaterialTheme.colorScheme.surface,
        shadowElevation = 1.dp
    ) {
        Column(
            modifier = Modifier.padding(14.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                Icon(
                    imageVector = if (file.isPdf) Icons.Filled.PictureAsPdf else Icons.Filled.Description,
                    contentDescription = file.formatLabel,
                    tint = if (file.isPdf) MaterialTheme.colorScheme.error else MaterialTheme.colorScheme.primary,
                    modifier = Modifier.size(30.dp)
                )
                Column(modifier = Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(3.dp)) {
                    Text(
                        text = file.displayName,
                        style = MaterialTheme.typography.titleSmall,
                        fontWeight = FontWeight.Bold,
                        maxLines = 2,
                        overflow = TextOverflow.Ellipsis
                    )
                    Text(
                        text = "${file.formatLabel} · ${formatSavedFileTime(file.modifiedAt)} · ${formatSavedFileSize(file.sizeBytes)}",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        maxLines = 2,
                        overflow = TextOverflow.Ellipsis
                    )
                }
            }
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                TextButton(
                    onClick = {
                        val opened = if (file.isPdf) {
                            printTransmittalPdf(
                                context,
                                AppFileStore.uriFor(context, file),
                                "Yakult PDF"
                            )
                        } else {
                            AppFileStore.open(context, file)
                        }
                        if (!opened) {
                            val action = if (file.isPdf) "start printing" else "open the file"
                            Toast.makeText(
                                context,
                                "Couldn't $action on this device.",
                                Toast.LENGTH_LONG
                            ).show()
                        }
                    }
                ) {
                    Text(actionLabel)
                }
                TextButton(
                    onClick = {
                        if (!AppFileStore.share(context, file)) {
                            Toast.makeText(
                                context,
                                "Couldn't open the share menu.",
                                Toast.LENGTH_LONG
                            ).show()
                        }
                    }
                ) {
                    Text("Share")
                }
            }
        }
    }
}
