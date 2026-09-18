package com.example.yakultscanner.ui.screens

import android.app.DatePickerDialog
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.horizontalScroll
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
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.History
import androidx.compose.material.icons.filled.PictureAsPdf
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilterChip
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.navigation.NavController
import com.example.yakultscanner.api.BorrowAccessDto
import com.example.yakultscanner.api.ApiClient
import com.example.yakultscanner.api.ApiResult
import com.example.yakultscanner.api.BorrowDeleteRequest
import com.example.yakultscanner.api.BorrowLogDto
import com.example.yakultscanner.api.BorrowLogPageResponse
import com.example.yakultscanner.api.safeApiCall
import com.example.yakultscanner.api.userMessageOr
import com.example.yakultscanner.utils.TextExportUtils
import com.example.yakultscanner.utils.generateBorrowRecordsPdf
import kotlinx.coroutines.launch
import java.time.Duration
import java.time.Instant
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import java.time.temporal.ChronoUnit
import java.util.Calendar
import java.util.Locale
import java.util.UUID

private enum class BorrowHistoryRange {
    ALL,
    TODAY,
    LAST_7_DAYS,
    LAST_30_DAYS,
    CUSTOM
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun BorrowRecordsScreen(navController: NavController) {
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    var selectedTab by rememberSaveable { mutableIntStateOf(0) }
    var searchInput by rememberSaveable { mutableStateOf("") }
    var appliedSearch by rememberSaveable { mutableStateOf("") }
    var historyRange by rememberSaveable { mutableStateOf(BorrowHistoryRange.ALL.name) }
    var historyToolsExpanded by rememberSaveable { mutableStateOf(false) }
    var customHistoryFromMillis by rememberSaveable { mutableStateOf(System.currentTimeMillis() - ChronoUnit.DAYS.duration.toMillis() * 30L) }
    var customHistoryToMillis by rememberSaveable { mutableStateOf(System.currentTimeMillis()) }
    var openPageIndex by rememberSaveable { mutableIntStateOf(0) }
    var historyPageIndex by rememberSaveable { mutableIntStateOf(0) }
    var isLoading by remember { mutableStateOf(false) }
    var isExporting by remember { mutableStateOf(false) }
    var errorText by remember { mutableStateOf<String?>(null) }
    var openErrorText by remember { mutableStateOf<String?>(null) }
    var historyErrorText by remember { mutableStateOf<String?>(null) }
    var generalErrorSupportDetails by remember { mutableStateOf<Pair<String, String>?>(null) }
    var openErrorSupportDetails by remember { mutableStateOf<Pair<String, String>?>(null) }
    var historyErrorSupportDetails by remember { mutableStateOf<Pair<String, String>?>(null) }
    var noticeText by remember { mutableStateOf<String?>(null) }
    var openPage by remember { mutableStateOf(BorrowLogPageResponse()) }
    var historyPage by remember { mutableStateOf(BorrowLogPageResponse()) }
    var pendingDelete by remember { mutableStateOf<BorrowLogDto?>(null) }
    var selectedRecord by remember { mutableStateOf<BorrowLogDto?>(null) }
    var pendingReturn by remember { mutableStateOf<BorrowLogDto?>(null) }
    var isReturning by remember { mutableStateOf(false) }
    var returnErrorText by remember { mutableStateOf<String?>(null) }
    var returnErrorSupportDetails by remember { mutableStateOf<Pair<String, String>?>(null) }
    val pageSize = 5
    val activeHistoryRange = BorrowHistoryRange.valueOf(historyRange)
    val loadLivePages: () -> Unit = {
        isLoading = true
        errorText = null
        openErrorText = null
        historyErrorText = null
        generalErrorSupportDetails = null
        openErrorSupportDetails = null
        historyErrorSupportDetails = null
        scope.launch {
            var openResultData: BorrowLogPageResponse? = null
            var historyResultData: BorrowLogPageResponse? = null

            when (val result = safeApiCall {
                ApiClient.service.getOpenBorrows(
                    serialContains = appliedSearch.ifBlank { null },
                    pageIndex = openPageIndex,
                    pageSize = pageSize
                )
            }) {
                is ApiResult.Success -> openResultData = result.data
                is ApiResult.HttpError -> {
                    openErrorText = result.userMessageOr("We couldn't load open borrows right now.")
                    openErrorSupportDetails = buildHttpApiError("Open borrow details", result)
                }
                is ApiResult.NetworkError -> {
                    openErrorText = result.userMessageOr("Can't reach open borrows right now. Check your connection and try again.")
                    openErrorSupportDetails = buildNetworkApiError("Open borrow details", result)
                }
                is ApiResult.UnknownError -> {
                    openErrorText = result.userMessageOr("Something went wrong while loading open borrows. Please try again.")
                    openErrorSupportDetails = buildUnknownApiError("Open borrow details", result)
                }
            }

            when (val result = safeApiCall {
                ApiClient.service.getBorrowHistory(
                    serialContains = appliedSearch.ifBlank { null },
                    pageIndex = historyPageIndex,
                    pageSize = pageSize,
                    fromUtc = buildHistoryFromUtc(activeHistoryRange, customHistoryFromMillis),
                    toUtc = buildHistoryToUtcExclusive(activeHistoryRange, customHistoryFromMillis, customHistoryToMillis)
                )
            }) {
                is ApiResult.Success -> historyResultData = result.data
                is ApiResult.HttpError -> {
                    historyErrorText = result.userMessageOr("We couldn't load borrow history right now.")
                    historyErrorSupportDetails = buildHttpApiError("Borrow history details", result)
                }
                is ApiResult.NetworkError -> {
                    historyErrorText = result.userMessageOr("Can't reach borrow history right now. Check your connection and try again.")
                    historyErrorSupportDetails = buildNetworkApiError("Borrow history details", result)
                }
                is ApiResult.UnknownError -> {
                    historyErrorText = result.userMessageOr("Something went wrong while loading borrow history. Please try again.")
                    historyErrorSupportDetails = buildUnknownApiError("Borrow history details", result)
                }
            }

            if (openResultData != null) openPage = openResultData!!
            if (historyResultData != null) historyPage = historyResultData!!
            isLoading = false
        }
    }

    fun applySearch() {
        val nextSearch = searchInput.trim()
        val sameSearch = appliedSearch == nextSearch
        val samePages = openPageIndex == 0 && historyPageIndex == 0
        appliedSearch = nextSearch
        openPageIndex = 0
        historyPageIndex = 0
        if (sameSearch && samePages) {
            loadLivePages()
        }
    }

    fun clearSearch() {
        val alreadyClear = searchInput.isBlank() && appliedSearch.isBlank() && openPageIndex == 0 && historyPageIndex == 0
        searchInput = ""
        appliedSearch = ""
        openPageIndex = 0
        historyPageIndex = 0
        if (alreadyClear) {
            loadLivePages()
        }
    }

    fun refreshRecords() {
        noticeText = null
        errorText = null
        openErrorText = null
        historyErrorText = null
        generalErrorSupportDetails = null
        openErrorSupportDetails = null
        historyErrorSupportDetails = null

        loadLivePages()
    }

    fun submitReturn(record: BorrowLogDto) {
        if (!record.isOpen || record.borrowId <= 0) {
            returnErrorText = "This borrow record is no longer open. Refresh the records and try again."
            returnErrorSupportDetails = null
            return
        }

        isReturning = true
        returnErrorText = null
        returnErrorSupportDetails = null
        scope.launch {
            try {
                val request = com.example.yakultscanner.api.BorrowReturnRequest(
                    clientRequestId = UUID.randomUUID().toString(),
                    borrowId = record.borrowId,
                    returnedByEmpId = record.borrowedByEmpId,
                    returnedByDeptId = record.borrowedByDeptId,
                    returnedByDeptName = record.borrowedByDeptName
                )

                when (val result = safeApiCall { ApiClient.service.returnBorrow(request) }) {
                    is ApiResult.Success -> {
                        if (result.data.success) {
                            pendingReturn = null
                            noticeText = result.data.message?.ifBlank { "Return entry saved successfully." }
                                ?: "Return entry saved successfully."
                            selectedTab = 0
                            loadLivePages()
                        } else {
                            returnErrorText = result.data.message?.ifBlank {
                                "The server did not complete the return. Please try again."
                            } ?: "The server did not complete the return. Please try again."
                            returnErrorSupportDetails = null
                        }
                    }
                    is ApiResult.HttpError -> {
                        returnErrorText = result.userMessageOr("We couldn't save the return entry right now.")
                        returnErrorSupportDetails = buildHttpApiError("Borrow return details", result)
                    }
                    is ApiResult.NetworkError -> {
                        returnErrorText = result.userMessageOr("Can't reach the borrow service right now. Check your connection and try again.")
                        returnErrorSupportDetails = buildNetworkApiError("Borrow return details", result)
                    }
                    is ApiResult.UnknownError -> {
                        returnErrorText = result.userMessageOr("Something went wrong while saving the return entry. Please try again.")
                        returnErrorSupportDetails = buildUnknownApiError("Borrow return details", result)
                    }
                }
            } finally {
                isReturning = false
            }
        }
    }

    fun exportCurrentRows(rowsToExport: List<BorrowLogDto>, isHistory: Boolean, access: BorrowAccessDto?) {
        if (rowsToExport.isEmpty()) {
            noticeText = if (selectedTab == 0) {
                "There are no open borrow rows on this page to export yet."
            } else {
                "There are no history rows on this page to export yet."
            }
            return
        }

        if (access?.canExportCsv != true) {
            errorText = "Export isn't available for this account. Ask an admin or developer account to export these records."
            generalErrorSupportDetails = buildApiError(
                title = "CSV export details",
                endpoint = "Downloads/CSV export",
                typeLine = "Type: Permission check",
                message = "User does not have CSV export access."
            )
            return
        }

        isExporting = true
        scope.launch {
            val stamp = DateTimeFormatter.ofPattern("yyyyMMdd_HHmmss", Locale.getDefault())
                .format(Instant.now().atZone(ZoneId.systemDefault()))
            val fileName = if (isHistory) "BorrowLog_History_$stamp.csv" else "BorrowLog_Open_$stamp.csv"
            val csv = buildBorrowCsv(rowsToExport, isHistory)
            val result = TextExportUtils.saveTextToDownloads(context, fileName, "text/csv", csv)
            result
                .onSuccess { savedLabel ->
                    generalErrorSupportDetails = null
                    noticeText = if (savedLabel.contains("\\") || savedLabel.contains("/")) {
                        "CSV export saved successfully: $savedLabel"
                    } else {
                        "CSV export saved to Downloads as $savedLabel"
                    }
                }
                .onFailure { ex ->
                    errorText = when {
                        ex.message?.contains("create export file", ignoreCase = true) == true ->
                            "We couldn't create the CSV file in Downloads. Check device storage and try again."
                        ex.message?.contains("output stream", ignoreCase = true) == true ->
                            "We couldn't write the CSV file to Downloads. Check storage access and try again."
                        else ->
                            "We couldn't save the CSV to Downloads right now. Please try again."
                    }
                    generalErrorSupportDetails = buildApiError(
                        title = "CSV export details",
                        endpoint = "Downloads/CSV export",
                        typeLine = "Type: Storage export failure",
                        message = ex.message
                    )
                }
            isExporting = false
        }
    }

    LaunchedEffect(appliedSearch, openPageIndex, historyPageIndex, historyRange, customHistoryFromMillis, customHistoryToMillis) {
        loadLivePages()
    }

    val rows = if (selectedTab == 0) {
        openPage.rows
    } else {
        historyPage.rows
    }

    val totalCount = if (selectedTab == 0) {
        openPage.totalCount
    } else {
        historyPage.totalCount
    }

    val currentPageIndex = if (selectedTab == 0) openPageIndex else historyPageIndex
    val oldestOpenText = openPage.oldestBorrowedAtUtc
    val currentAccess = if (selectedTab == 0) openPage.access else historyPage.access
    val selectedTabError = if (selectedTab == 0) openErrorText else historyErrorText
    val selectedTabErrorSupportDetails = if (selectedTab == 0) openErrorSupportDetails else historyErrorSupportDetails

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Borrow Records") },
                navigationIcon = {
                    IconButton(onClick = { navController.popBackStack() }) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back")
                    }
                },
                actions = {
                    IconButton(
                        onClick = {
                            val tabTitle = if (selectedTab == 0) "Open Borrows" else "History"
                            generateBorrowRecordsPdf(context, rows, tabTitle)
                        },
                        enabled = rows.isNotEmpty() && !isLoading
                    ) {
                        Icon(Icons.Filled.PictureAsPdf, contentDescription = "Export PDF")
                    }
                    IconButton(onClick = ::refreshRecords, enabled = !isLoading) {
                        Icon(Icons.Filled.Refresh, contentDescription = "Refresh")
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = Color.Transparent,
                    titleContentColor = MaterialTheme.colorScheme.onPrimary,
                    navigationIconContentColor = MaterialTheme.colorScheme.onPrimary,
                    actionIconContentColor = MaterialTheme.colorScheme.onPrimary
                )
            )
        },
        containerColor = Color.Transparent
    ) { paddingValues ->
        Box(
            modifier = Modifier
                .fillMaxSize()
                .background(
                    Brush.verticalGradient(
                        colors = listOf(
                            MaterialTheme.colorScheme.primary,
                            MaterialTheme.colorScheme.primaryContainer,
                            MaterialTheme.colorScheme.background
                        )
                    )
                )
                .padding(paddingValues)
        ) {
            LazyColumn(
                modifier = Modifier.fillMaxSize(),
                contentPadding = PaddingValues(horizontal = 20.dp, vertical = 14.dp),
                verticalArrangement = Arrangement.spacedBy(16.dp)
            ) {
                item {
                    BorrowStateCard(
                        title = "Live Borrow Records",
                        message = "Open and History now show live borrow data only. Use search, filters, and record details to review the actual borrow log.",
                        tone = BorrowCardTone.Neutral,
                        badgeText = "Live"
                    )
                }

                item {
                    Card(
                        shape = RoundedCornerShape(24.dp),
                        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
                        elevation = CardDefaults.cardElevation(defaultElevation = 10.dp)
                    ) {
                        Column(modifier = Modifier.fillMaxWidth()) {
                            RecordModeTabs(
                                selectedTab = selectedTab,
                                onTabSelected = {
                                    selectedTab = it
                                    if (it == 0) {
                                        historyToolsExpanded = false
                                    }
                                }
                            )
                            Column(
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .padding(16.dp),
                                verticalArrangement = Arrangement.spacedBy(12.dp)
                            ) {
                                Text(
                                    text = if (selectedTab == 0) {
                                        "Review active borrows and tap a row for full details."
                                    } else {
                                        "Review returned records. Open advanced filters only when you need a tighter history range."
                                    },
                                    style = MaterialTheme.typography.bodySmall,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                )
                                if (!selectedTabError.isNullOrBlank()) {
                                    ErrorCard(
                                        message = selectedTabError!!,
                                        supportDetails = selectedTabErrorSupportDetails,
                                        onRetry = ::refreshRecords
                                    )
                                }
                                if (selectedTab == 0 && !historyErrorText.isNullOrBlank()) {
                                    BorrowStateCard(
                                        title = "History needs attention",
                                        message = historyErrorText!!,
                                        tone = BorrowCardTone.Warning,
                                        badgeText = "History"
                                    )
                                } else if (selectedTab == 1 && !openErrorText.isNullOrBlank()) {
                                    BorrowStateCard(
                                        title = "Open records need attention",
                                        message = openErrorText!!,
                                        tone = BorrowCardTone.Warning,
                                        badgeText = "Open"
                                    )
                                }
                                OutlinedTextField(
                                    value = searchInput,
                                    onValueChange = { searchInput = it },
                                    modifier = Modifier.fillMaxWidth(),
                                    singleLine = true,
                                    label = { Text("Search serial / name / model") }
                                )

                                Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                                    Button(onClick = ::applySearch, modifier = Modifier.weight(1f)) {
                                        Text("Apply Search")
                                    }
                                    OutlinedButton(onClick = ::clearSearch, modifier = Modifier.weight(1f)) {
                                        Text("Clear")
                                    }
                                }

                                if (selectedTab == 1) {
                                    OutlinedButton(
                                        onClick = { historyToolsExpanded = !historyToolsExpanded },
                                        modifier = Modifier.fillMaxWidth()
                                    ) {
                                        Text(if (historyToolsExpanded) "Hide Advanced History Filters" else "Show Advanced History Filters")
                                    }

                                    if (historyToolsExpanded) {
                                        HistoryRangeCard(
                                            selectedRange = activeHistoryRange,
                                            customFromMillis = customHistoryFromMillis,
                                            customToMillis = customHistoryToMillis,
                                            onRangeSelected = {
                                                historyRange = it.name
                                                historyPageIndex = 0
                                            },
                                            onPickFrom = {
                                                showRecordsDatePicker(context, customHistoryFromMillis) {
                                                    customHistoryFromMillis = it
                                                    if (customHistoryToMillis < it) {
                                                        customHistoryToMillis = it
                                                    }
                                                    historyRange = BorrowHistoryRange.CUSTOM.name
                                                    historyPageIndex = 0
                                                }
                                            },
                                            onPickTo = {
                                                showRecordsDatePicker(context, customHistoryToMillis) {
                                                    customHistoryToMillis = it
                                                    if (customHistoryFromMillis > it) {
                                                        customHistoryFromMillis = it
                                                    }
                                                    historyRange = BorrowHistoryRange.CUSTOM.name
                                                    historyPageIndex = 0
                                                }
                                            }
                                        )
                                    }
                                }

                                SummaryStrip(
                                    openCount = openPage.totalCount,
                                    historyCount = historyPage.totalCount,
                                    oldestOpenText = formatRecordTimestamp(oldestOpenText)
                                )
                            }
                        }
                    }
                }

                if (!errorText.isNullOrBlank()) {
                    item {
                        ErrorCard(errorText!!, supportDetails = generalErrorSupportDetails)
                    }
                }

                if (!noticeText.isNullOrBlank()) {
                    item {
                        NoticeCard(noticeText!!)
                    }
                }

                if (isLoading) {
                    item {
                        LoadingCard()
                    }
                 } else if (rows.isEmpty()) {
                     item {
                        EmptyRecordsCard(isHistory = selectedTab == 1)
                     }
                 } else {
                     items(rows, key = { it.borrowId }) { row ->
                         BorrowRecordRow(
                             record = row,
                             isHistory = selectedTab == 1,
                             showDeleteAction = selectedTab == 0 && currentAccess?.canDeleteOpenBorrow == true,
                             onOpenDetails = { selectedRecord = row }
                         )
                     }
                 }

                 item {
                     RecordManagementCard(
                         selectedTab = selectedTab,
                        canExportCsv = currentAccess?.canExportCsv == true,
                        canDeleteOpenBorrow = currentAccess?.canDeleteOpenBorrow == true,
                         loading = isLoading,
                         exporting = isExporting,
                         onRefresh = ::refreshRecords,
                         onExport = {
                             exportCurrentRows(
                                rowsToExport = if (selectedTab == 0) openPage.rows else historyPage.rows,
                                 isHistory = selectedTab == 1,
                                 access = currentAccess
                             )
                         }
                     )
                 }

                item {
                    PaginationCard(
                        pageIndex = currentPageIndex,
                        totalCount = totalCount,
                        pageSize = pageSize,
                        onPrevious = {
                            if (selectedTab == 0) openPageIndex = (openPageIndex - 1).coerceAtLeast(0)
                            else historyPageIndex = (historyPageIndex - 1).coerceAtLeast(0)
                        },
                        onNext = {
                            if (selectedTab == 0) openPageIndex += 1 else historyPageIndex += 1
                        }
                    )
                }

                item {
                    Spacer(modifier = Modifier.height(24.dp))
                }
            }
        }
    }

    pendingDelete?.let { record ->
        AlertDialog(
            onDismissRequest = { pendingDelete = null },
            title = {
                Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
                    Text("Admin Correction")
                    Text(
                        "Delete open borrow",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
            },
            text = {
                Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
                    StatusBadge(text = "Admin only", isHistory = true)
                    Text(
                        "Admin correction only. Use this only for encoding mistakes, not for normal returns.",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                    DetailSectionCard(title = "Open borrow summary") {
                        DetailLine("Serial", record.serialNumber.orEmpty().ifBlank { "--" })
                        DetailLine("Item", record.itemName.orEmpty().ifBlank { "--" })
                        DetailLine("Borrowed By", record.borrowedByEmpName.orEmpty().ifBlank { "--" })
                        DetailLine("Borrowed At", formatRecordTimestamp(record.borrowedAtUtc))
                    }
                }
            },
            confirmButton = {
                Button(onClick = {
                    val borrowId = record.borrowId
                    pendingDelete = null
                    isLoading = true
                    errorText = null
                    noticeText = null
                    scope.launch {
                        val request = BorrowDeleteRequest(
                            clientRequestId = UUID.randomUUID().toString(),
                            borrowId = borrowId
                        )
                        when (val result = safeApiCall { ApiClient.service.deleteBorrow(request) }) {
                            is ApiResult.Success -> {
                                generalErrorSupportDetails = null
                                noticeText = result.data.message?.ifBlank { "Borrow record deleted." } ?: "Borrow record deleted."
                                loadLivePages()
                            }
                            is ApiResult.HttpError -> {
                                errorText = result.userMessageOr("We couldn't delete the borrow entry right now.")
                                generalErrorSupportDetails = buildHttpApiError("Borrow delete details", result)
                                isLoading = false
                            }
                            is ApiResult.NetworkError -> {
                                errorText = result.userMessageOr()
                                generalErrorSupportDetails = buildNetworkApiError("Borrow delete details", result)
                                isLoading = false
                            }
                            is ApiResult.UnknownError -> {
                                errorText = result.userMessageOr()
                                generalErrorSupportDetails = buildUnknownApiError("Borrow delete details", result)
                                isLoading = false
                            }
                        }
                    }
                }) {
                    Text("Delete Open Borrow")
                }
            },
            dismissButton = {
                TextButton(onClick = { pendingDelete = null }) {
                    Text("Cancel")
                }
            }
        )
    }

    pendingReturn?.let { record ->
        val returnerLabel = record.borrowedByEmpName.orEmpty().ifBlank {
            record.borrowedByDeptName.orEmpty().ifBlank { "Current record holder" }
        }
        AlertDialog(
            onDismissRequest = {
                if (!isReturning) {
                    pendingReturn = null
                    returnErrorText = null
                    returnErrorSupportDetails = null
                }
            },
            title = { Text("Confirm Return") },
            text = {
                Column(
                    modifier = Modifier.verticalScroll(rememberScrollState()),
                    verticalArrangement = Arrangement.spacedBy(12.dp)
                ) {
                    Text(
                        text = "This will close the open borrow record and move the item to returned history.",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                    DetailSectionCard(title = "Return summary") {
                        DetailLine("Serial", record.serialNumber.orEmpty().ifBlank { "--" })
                        DetailLine(
                            "Item",
                            record.itemName.orEmpty().ifBlank { record.itemDescription.orEmpty().ifBlank { "--" } }
                        )
                        DetailLine("Model", record.modelNumber.orEmpty().ifBlank { "--" })
                        DetailLine("Borrowed By", record.borrowedByEmpName.orEmpty().ifBlank { "--" })
                        DetailLine("Department", record.borrowedByDeptName.orEmpty().ifBlank { "--" })
                        DetailLine("Borrowed At", formatRecordTimestamp(record.borrowedAtUtc))
                        DetailLine("Returned By", returnerLabel)
                    }
                    Text(
                        text = "The return will use the current borrower or department shown above. If a different person is returning the item, use the Borrow / Return workspace instead.",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                    if (!returnErrorText.isNullOrBlank()) {
                        Surface(
                            color = MaterialTheme.colorScheme.errorContainer,
                            shape = RoundedCornerShape(12.dp)
                        ) {
                            Column(
                                modifier = Modifier.padding(12.dp),
                                verticalArrangement = Arrangement.spacedBy(4.dp)
                            ) {
                                Text(
                                    text = returnErrorText!!,
                                    style = MaterialTheme.typography.bodySmall,
                                    fontWeight = FontWeight.SemiBold,
                                    color = MaterialTheme.colorScheme.onErrorContainer
                                )
                                returnErrorSupportDetails?.let { details ->
                                    Text(
                                        text = details.first,
                                        style = MaterialTheme.typography.labelSmall,
                                        color = MaterialTheme.colorScheme.onErrorContainer
                                    )
                                    Text(
                                        text = details.second,
                                        style = MaterialTheme.typography.bodySmall,
                                        color = MaterialTheme.colorScheme.onErrorContainer.copy(alpha = 0.82f)
                                    )
                                }
                            }
                        }
                    }
                }
            },
            confirmButton = {
                Button(
                    onClick = { submitReturn(record) },
                    enabled = !isReturning
                ) {
                    if (isReturning) {
                        CircularProgressIndicator(
                            modifier = Modifier.size(18.dp),
                            strokeWidth = 2.dp,
                            color = MaterialTheme.colorScheme.onPrimary
                        )
                        Spacer(modifier = Modifier.size(8.dp))
                    }
                    Text(
                        text = when {
                            isReturning -> "Returning..."
                            !returnErrorText.isNullOrBlank() -> "Retry Return"
                            else -> "Confirm Return"
                        }
                    )
                }
            },
            dismissButton = {
                TextButton(
                    onClick = {
                        if (!isReturning) {
                            pendingReturn = null
                            returnErrorText = null
                            returnErrorSupportDetails = null
                        }
                    },
                    enabled = !isReturning
                ) {
                    Text("Cancel")
                }
            }
        )
    }

    selectedRecord?.let { record ->
        val canDeleteCorrection = record.isOpen && currentAccess?.canDeleteOpenBorrow == true
        val statusColor = if (record.isOpen) Color(0xFF1B8C4E) else Color(0xFF1565C0)
        val statusBg = if (record.isOpen) Color(0xFFE8F5E9) else Color(0xFFE3F2FD)
        AlertDialog(
            onDismissRequest = { selectedRecord = null },
            title = null,
            text = {
                Column(
                    modifier = Modifier.verticalScroll(rememberScrollState()),
                    verticalArrangement = Arrangement.spacedBy(0.dp)
                ) {
                    // ── Header ──────────────────────────────────────────────
                    Surface(
                        color = if (record.isOpen) Color(0xFF1B8C4E) else Color(0xFF1565C0),
                        shape = RoundedCornerShape(topStart = 16.dp, topEnd = 16.dp, bottomStart = 0.dp, bottomEnd = 0.dp)
                    ) {
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(horizontal = 18.dp, vertical = 16.dp),
                            horizontalArrangement = Arrangement.SpaceBetween,
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Column(
                                modifier = Modifier.weight(1f),
                                verticalArrangement = Arrangement.spacedBy(3.dp)
                            ) {
                                Text(
                                    text = "Borrow record",
                                    style = MaterialTheme.typography.labelMedium,
                                    color = Color.White.copy(alpha = 0.8f)
                                )
                                Text(
                                    text = record.serialNumber.orEmpty().ifBlank { "—" },
                                    style = MaterialTheme.typography.titleLarge,
                                    fontWeight = FontWeight.Bold,
                                    color = Color.White
                                )
                                if (!record.itemName.isNullOrBlank()) {
                                    Text(
                                        text = record.itemName,
                                        style = MaterialTheme.typography.bodySmall,
                                        color = Color.White.copy(alpha = 0.75f)
                                    )
                                }
                                if (!record.modelNumber.isNullOrBlank()) {
                                    Text(
                                        text = "Model ${record.modelNumber}",
                                        style = MaterialTheme.typography.bodySmall,
                                        color = Color.White.copy(alpha = 0.65f)
                                    )
                                }
                            }
                            Surface(
                                color = Color.White.copy(alpha = 0.18f),
                                shape = RoundedCornerShape(20.dp)
                            ) {
                                Text(
                                    text = if (record.isOpen) "Open" else "Returned",
                                    modifier = Modifier.padding(horizontal = 12.dp, vertical = 5.dp),
                                    style = MaterialTheme.typography.labelMedium,
                                    fontWeight = FontWeight.Bold,
                                    color = Color.White
                                )
                            }
                        }
                    }

                    // ── Body ─────────────────────────────────────────────────
                    Surface(
                        color = MaterialTheme.colorScheme.surface,
                        shape = RoundedCornerShape(topStart = 0.dp, topEnd = 0.dp, bottomStart = 16.dp, bottomEnd = 16.dp),
                        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.4f))
                    ) {
                        Column(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(18.dp),
                            verticalArrangement = Arrangement.spacedBy(14.dp)
                        ) {
                            // Borrow timeline section
                            Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                                Row(
                                    verticalAlignment = Alignment.CenterVertically,
                                    horizontalArrangement = Arrangement.spacedBy(6.dp)
                                ) {
                                    Surface(
                                        color = statusBg,
                                        shape = RoundedCornerShape(6.dp)
                                    ) {
                                        Text(
                                            text = "Borrow timeline",
                                            modifier = Modifier.padding(horizontal = 8.dp, vertical = 3.dp),
                                            style = MaterialTheme.typography.labelMedium,
                                            fontWeight = FontWeight.Bold,
                                            color = statusColor
                                        )
                                    }
                                }
                                DetailLine("Borrowed By", record.borrowedByEmpName.orEmpty().ifBlank { "--" })
                                DetailLine("Department", record.borrowedByDeptName.orEmpty().ifBlank { "--" })
                                DetailLine("Borrowed At", formatRecordTimestamp(record.borrowedAtUtc))
                                DetailLine("Borrow Encoded By", record.borrowEncodedByUserName.orEmpty().ifBlank { "--" })
                            }

                            if (!record.isOpen) {
                                Surface(
                                    color = MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.3f),
                                    shape = RoundedCornerShape(2.dp),
                                    modifier = Modifier
                                        .fillMaxWidth()
                                        .height(1.dp)
                                ) {}

                                // Return timeline section
                                Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                                    Surface(
                                        color = Color(0xFFE3F2FD),
                                        shape = RoundedCornerShape(6.dp)
                                    ) {
                                        Text(
                                            text = "Return timeline",
                                            modifier = Modifier.padding(horizontal = 8.dp, vertical = 3.dp),
                                            style = MaterialTheme.typography.labelMedium,
                                            fontWeight = FontWeight.Bold,
                                            color = Color(0xFF1565C0)
                                        )
                                    }
                                    DetailLine("Returned By", record.returnedByEmpName.orEmpty().ifBlank { "--" })
                                    DetailLine("Return Department", record.returnedByDeptName.orEmpty().ifBlank { "--" })
                                    DetailLine("Returned At", formatRecordTimestamp(record.returnedAtUtc))
                                    DetailLine("Return Encoded By", record.returnEncodedByUserName.orEmpty().ifBlank { "--" })
                                }
                            }

                            if (record.isOpen && record.borrowId > 0) {
                                Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                                    Button(
                                        onClick = {
                                            returnErrorText = null
                                            returnErrorSupportDetails = null
                                            selectedRecord = null
                                            pendingReturn = record
                                        },
                                        modifier = Modifier.fillMaxWidth(),
                                        enabled = !isReturning
                                    ) {
                                        Text("Return Item")
                                    }
                                    Text(
                                        text = "For a different returner, use the Borrow / Return workspace instead.",
                                        style = MaterialTheme.typography.bodySmall,
                                        color = MaterialTheme.colorScheme.onSurfaceVariant
                                    )
                                }
                            }

                            if (canDeleteCorrection) {
                                Surface(
                                    color = MaterialTheme.colorScheme.errorContainer.copy(alpha = 0.72f),
                                    shape = RoundedCornerShape(12.dp)
                                ) {
                                    Column(
                                        modifier = Modifier.padding(12.dp),
                                        verticalArrangement = Arrangement.spacedBy(4.dp)
                                    ) {
                                        Text(
                                            text = "Admin-only correction",
                                            style = MaterialTheme.typography.labelLarge,
                                            fontWeight = FontWeight.Bold,
                                            color = MaterialTheme.colorScheme.onErrorContainer
                                        )
                                        Text(
                                            text = "Delete is only for mistaken open encodings. Use return for normal hand-backs.",
                                            style = MaterialTheme.typography.bodySmall,
                                            color = MaterialTheme.colorScheme.onErrorContainer
                                        )
                                    }
                                }
                            } else if (record.isOpen) {
                                Surface(
                                    color = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f),
                                    shape = RoundedCornerShape(12.dp)
                                ) {
                                    Text(
                                        text = "This record is still open. Only admin or developer users can delete mistaken encodings.",
                                        modifier = Modifier.padding(12.dp),
                                        style = MaterialTheme.typography.bodySmall,
                                        color = MaterialTheme.colorScheme.onSurfaceVariant
                                    )
                                }
                            }
                        }
                    }
                }
            },
            confirmButton = {
                Button(onClick = { selectedRecord = null }) {
                    Text("Close")
                }
            },
            dismissButton = {
                if (canDeleteCorrection) {
                    TextButton(onClick = {
                        selectedRecord = null
                        pendingDelete = record
                    }) {
                        Text("Delete Correction", color = MaterialTheme.colorScheme.error)
                    }
                }
            }
        )
    }
}

@Composable
private fun RecordModeTabs(selectedTab: Int, onTabSelected: (Int) -> Unit) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 16.dp, vertical = 12.dp),
        horizontalArrangement = Arrangement.spacedBy(10.dp)
    ) {
        RecordModeTabCard(
            title = "Open",
            subtitle = "Active borrows",
            selected = selectedTab == 0,
            modifier = Modifier.weight(1f),
            onClick = { onTabSelected(0) }
        )
        RecordModeTabCard(
            title = "History",
            subtitle = "Returned logs",
            selected = selectedTab == 1,
            modifier = Modifier.weight(1f),
            onClick = { onTabSelected(1) }
        )
    }
}

@Composable
private fun RecordModeTabCard(
    title: String,
    subtitle: String,
    selected: Boolean,
    modifier: Modifier = Modifier,
    onClick: () -> Unit
) {
    val containerColor = if (selected) MaterialTheme.colorScheme.primaryContainer else MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.45f)
    val contentColor = if (selected) MaterialTheme.colorScheme.onPrimaryContainer else MaterialTheme.colorScheme.onSurface
    Card(
        modifier = modifier.clickable(onClick = onClick),
        shape = RoundedCornerShape(18.dp),
        colors = CardDefaults.cardColors(containerColor = containerColor),
        border = BorderStroke(
            1.dp,
            if (selected) MaterialTheme.colorScheme.primary.copy(alpha = 0.35f) else MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.28f)
        )
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 14.dp, vertical = 12.dp),
            verticalArrangement = Arrangement.spacedBy(2.dp)
        ) {
            Text(title, fontWeight = FontWeight.Bold, color = contentColor)
            Text(subtitle, style = MaterialTheme.typography.bodySmall, color = contentColor.copy(alpha = 0.82f))
        }
    }
}

@Composable
private fun SummaryStrip(openCount: Int, historyCount: Int, oldestOpenText: String) {
    Card(
        shape = RoundedCornerShape(18.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.4f))
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(12.dp),
            horizontalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            SummaryMetric("Open", openCount.toString(), Modifier.weight(1f))
            SummaryMetric("History", historyCount.toString(), Modifier.weight(1f))
            SummaryMetric("Oldest Open", oldestOpenText, Modifier.weight(1f))
        }
    }
}

@Composable
private fun SummaryMetric(label: String, value: String, modifier: Modifier = Modifier) {
    Surface(
        modifier = modifier,
        color = MaterialTheme.colorScheme.surface,
        shape = RoundedCornerShape(14.dp)
    ) {
        Column(
            modifier = Modifier.padding(horizontal = 10.dp, vertical = 10.dp),
            verticalArrangement = Arrangement.spacedBy(2.dp)
        ) {
            Text(text = value, fontWeight = FontWeight.Bold, style = MaterialTheme.typography.titleSmall)
            Text(text = label, style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }
}

@Composable
private fun HistoryRangeCard(
    selectedRange: BorrowHistoryRange,
    customFromMillis: Long,
    customToMillis: Long,
    onRangeSelected: (BorrowHistoryRange) -> Unit,
    onPickFrom: () -> Unit,
    onPickTo: () -> Unit
) {
    Surface(
        color = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.3f),
        shape = RoundedCornerShape(16.dp)
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(12.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            Text(
                text = "History range",
                style = MaterialTheme.typography.labelLarge,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
            Row(
                modifier = Modifier.horizontalScroll(rememberScrollState()),
                horizontalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                FilterChip(
                    selected = selectedRange == BorrowHistoryRange.ALL,
                    onClick = { onRangeSelected(BorrowHistoryRange.ALL) },
                    label = { Text("All") }
                )
                FilterChip(
                    selected = selectedRange == BorrowHistoryRange.TODAY,
                    onClick = { onRangeSelected(BorrowHistoryRange.TODAY) },
                    label = { Text("Today") }
                )
                FilterChip(
                    selected = selectedRange == BorrowHistoryRange.LAST_7_DAYS,
                    onClick = { onRangeSelected(BorrowHistoryRange.LAST_7_DAYS) },
                    label = { Text("7 Days") }
                )
                FilterChip(
                    selected = selectedRange == BorrowHistoryRange.LAST_30_DAYS,
                    onClick = { onRangeSelected(BorrowHistoryRange.LAST_30_DAYS) },
                    label = { Text("30 Days") }
                )
                FilterChip(
                    selected = selectedRange == BorrowHistoryRange.CUSTOM,
                    onClick = { onRangeSelected(BorrowHistoryRange.CUSTOM) },
                    label = { Text("Custom") }
                )
            }

            Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                OutlinedButton(
                    onClick = onPickFrom,
                    modifier = Modifier.weight(1f)
                ) {
                    Text("From ${formatShortDate(customFromMillis)}")
                }
                OutlinedButton(
                    onClick = onPickTo,
                    modifier = Modifier.weight(1f)
                ) {
                    Text("To ${formatShortDate(customToMillis)}")
                }
            }

            Text(
                text = if (selectedRange == BorrowHistoryRange.CUSTOM) {
                    "Using a custom borrow-date window."
                } else {
                    "Pick a preset first. Only use custom dates when you need a tighter search."
                },
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
        }
    }
}

@Composable
private fun RecordManagementCard(
    selectedTab: Int,
    canExportCsv: Boolean,
    canDeleteOpenBorrow: Boolean,
    loading: Boolean,
    exporting: Boolean,
    onRefresh: () -> Unit,
    onExport: () -> Unit
) {
    Surface(
        color = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.26f),
        shape = RoundedCornerShape(18.dp)
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(14.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            Text(
                text = if (selectedTab == 0) "Record tools" else "History tools",
                style = MaterialTheme.typography.labelLarge,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
            Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                OutlinedButton(
                    onClick = onRefresh,
                    enabled = !loading,
                    modifier = Modifier.weight(1f)
                ) {
                    Text("Refresh")
                }
                OutlinedButton(
                    onClick = onExport,
                    enabled = !exporting && canExportCsv,
                    modifier = Modifier.weight(1f)
                ) {
                    Text(if (exporting) "Exporting..." else "Export CSV")
                }
            }
            Text(
                text = when {
                    selectedTab == 0 && canDeleteOpenBorrow ->
                        "Admin-only delete is available inside the details view for correcting encoding mistakes."
                    !canExportCsv ->
                        "Your account can view records, but export and delete are reserved for admin or developer users."
                    else ->
                        "Export saves only the rows currently shown on this page."
                },
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
        }
    }
}

@Composable
private fun ErrorCard(
    message: String,
    supportDetails: Pair<String, String>? = null,
    onRetry: (() -> Unit)? = null
) {
    BorrowStateCard(title = "Something went wrong", message = message, tone = BorrowCardTone.Error) {
        if (supportDetails != null) {
            Text(
                text = supportDetails.first,
                style = MaterialTheme.typography.labelMedium,
                color = MaterialTheme.colorScheme.onErrorContainer
            )
            Text(
                text = supportDetails.second,
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onErrorContainer.copy(alpha = 0.82f)
            )
        }
        if (onRetry != null) {
            OutlinedButton(
                onClick = onRetry,
                modifier = Modifier.fillMaxWidth()
            ) {
                Text("Try Again")
            }
        }
    }
}

@Composable
private fun NoticeCard(message: String) {
    BorrowStateCard(title = "Update", message = message, tone = BorrowCardTone.Success)
}

@Composable
private fun LoadingCard() {
    BorrowStateCard(title = "Loading Records", message = "Fetching borrow records and record access details...", tone = BorrowCardTone.Neutral)
}

@Composable
private fun EmptyRecordsCard(isHistory: Boolean) {
    BorrowStateCard(
        title = "No records matched",
        message = if (isHistory) {
            "No returned records matched this search yet. Clear the serial search first, then open advanced history filters only if needed."
        } else {
            "No open borrows matched this search. Try clearing the serial search or switching to History."
        },
        tone = BorrowCardTone.Neutral
    )
}

@Composable
private fun BorrowRecordRow(
    record: BorrowLogDto,
    isHistory: Boolean,
    showDeleteAction: Boolean,
    onOpenDetails: () -> Unit
) {
    val personName = if (isHistory) {
        record.returnedByEmpName.orEmpty().ifBlank { record.borrowedByEmpName.orEmpty() }
    } else {
        record.borrowedByEmpName.orEmpty()
    }
    val personLabel = if (isHistory) "Returned by" else "Borrowed by"
    val primaryTime = if (isHistory) formatCompactTimestamp(record.returnedAtUtc) else formatElapsed(record.borrowedAtUtc)
    val statusText = if (isHistory) "Returned" else "Open"
    Card(
        modifier = Modifier.clickable(onClick = onOpenDetails),
        shape = RoundedCornerShape(22.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        elevation = CardDefaults.cardElevation(defaultElevation = 4.dp)
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(14.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Column(modifier = Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(2.dp)) {
                    Text(record.serialNumber.orEmpty(), fontWeight = FontWeight.Bold, style = MaterialTheme.typography.titleSmall)
                    Text(record.itemName.orEmpty(), style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
                StatusBadge(
                    text = statusText,
                    isHistory = isHistory,
                    modifier = Modifier
                )
            }

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Column(modifier = Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(2.dp)) {
                    Text(personLabel, style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    Text(personName.ifBlank { "--" }, fontWeight = FontWeight.SemiBold)
                    Text(record.borrowedByDeptName.orEmpty().ifBlank { record.returnedByDeptName.orEmpty().ifBlank { "--" } }, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
                Column(horizontalAlignment = Alignment.End, verticalArrangement = Arrangement.spacedBy(2.dp)) {
                    Text(if (isHistory) "Returned" else "Elapsed", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    Text(primaryTime, fontWeight = FontWeight.Bold)
                    Text(formatCompactTimestamp(record.borrowedAtUtc), style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
            }

            if (showDeleteAction) {
                Text(
                    text = "Admin-only delete is available inside details.",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
        }
    }
}

@Composable
private fun StatusBadge(text: String, isHistory: Boolean, modifier: Modifier = Modifier) {
    val containerColor = if (isHistory) MaterialTheme.colorScheme.secondaryContainer else MaterialTheme.colorScheme.primaryContainer
    val contentColor = if (isHistory) MaterialTheme.colorScheme.onSecondaryContainer else MaterialTheme.colorScheme.onPrimaryContainer

    Box(modifier = modifier, contentAlignment = Alignment.CenterEnd) {
        Surface(
            color = containerColor,
            contentColor = contentColor,
            shape = RoundedCornerShape(999.dp)
        ) {
            Text(
                text = text,
                modifier = Modifier.padding(horizontal = 10.dp, vertical = 6.dp),
                style = MaterialTheme.typography.labelMedium,
                fontWeight = FontWeight.Bold
            )
        }
    }
}

@Composable
private fun RecordDetailHeroCard(record: BorrowLogDto) {
    Surface(
        color = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.4f),
        shape = RoundedCornerShape(18.dp)
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(14.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Column(modifier = Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                    Text(
                        text = record.serialNumber.orEmpty().ifBlank { "--" },
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.Bold
                    )
                    Text(
                        text = record.itemName.orEmpty().ifBlank { record.itemDescription.orEmpty().ifBlank { "--" } },
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
                StatusBadge(text = if (record.isOpen) "Open" else "Returned", isHistory = !record.isOpen)
            }
            if (!record.modelNumber.isNullOrBlank()) {
                Text(
                    text = "Model ${record.modelNumber}",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
        }
    }
}

@Composable
private fun DetailSectionCard(title: String, content: @Composable () -> Unit) {
    Surface(
        color = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.28f),
        shape = RoundedCornerShape(18.dp)
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(14.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            Text(
                text = title,
                style = MaterialTheme.typography.labelLarge,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
            content()
        }
    }
}

@Composable
private fun DetailLine(label: String, value: String) {
    Column(
        modifier = Modifier.fillMaxWidth(),
        verticalArrangement = Arrangement.spacedBy(2.dp)
    ) {
        Text(
            label,
            style = MaterialTheme.typography.labelSmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant
        )
        Text(
            value,
            style = MaterialTheme.typography.bodyMedium,
            fontWeight = FontWeight.Medium,
            color = MaterialTheme.colorScheme.onSurface
        )
    }
}

@Composable
private fun PaginationCard(pageIndex: Int, totalCount: Int, pageSize: Int, onPrevious: () -> Unit, onNext: () -> Unit) {
    val hasPrevious = pageIndex > 0
    val hasNext = ((pageIndex + 1) * pageSize) < totalCount
    val currentPageLabel = if (totalCount <= 0) "Page 1 of 1" else "Page ${pageIndex + 1} of ${((totalCount - 1) / pageSize) + 1}"
    val fromItem = if (totalCount == 0) 0 else (pageIndex * pageSize) + 1
    val toItem = minOf(totalCount, (pageIndex + 1) * pageSize)

    Card(
        shape = RoundedCornerShape(22.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            Text(currentPageLabel, style = MaterialTheme.typography.bodyMedium, fontWeight = FontWeight.SemiBold)
            Text("Showing $fromItem-$toItem of $totalCount records", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
            Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                OutlinedButton(onClick = onPrevious, enabled = hasPrevious, modifier = Modifier.weight(1f)) {
                    Text("Prev Page")
                }
                Button(onClick = onNext, enabled = hasNext, modifier = Modifier.weight(1f)) {
                    Text("Next Page")
                }
            }
        }
    }
}

private fun buildHistoryFromUtc(range: BorrowHistoryRange, customFromMillis: Long): String? {
    val now = System.currentTimeMillis()
    val startMillis = when (range) {
        BorrowHistoryRange.ALL -> return null
        BorrowHistoryRange.TODAY -> startOfDay(now)
        BorrowHistoryRange.LAST_7_DAYS -> startOfDay(now - ChronoUnit.DAYS.duration.toMillis() * 6L)
        BorrowHistoryRange.LAST_30_DAYS -> startOfDay(now - ChronoUnit.DAYS.duration.toMillis() * 29L)
        BorrowHistoryRange.CUSTOM -> startOfDay(customFromMillis)
    }

    return Instant.ofEpochMilli(startMillis).toString()
}

private fun buildHistoryToUtcExclusive(range: BorrowHistoryRange, customFromMillis: Long, customToMillis: Long): String? {
    val now = System.currentTimeMillis()
    val endExclusiveMillis = when (range) {
        BorrowHistoryRange.ALL -> return null
        BorrowHistoryRange.TODAY,
        BorrowHistoryRange.LAST_7_DAYS,
        BorrowHistoryRange.LAST_30_DAYS -> startOfDay(now) + ChronoUnit.DAYS.duration.toMillis()
        BorrowHistoryRange.CUSTOM -> startOfDay(maxOf(customFromMillis, customToMillis)) + ChronoUnit.DAYS.duration.toMillis()
    }

    return Instant.ofEpochMilli(endExclusiveMillis).toString()
}

private fun isHistoryRecordVisible(
    record: BorrowLogDto,
    range: BorrowHistoryRange,
    customFromMillis: Long,
    customToMillis: Long
): Boolean {
    if (range == BorrowHistoryRange.ALL) {
        return true
    }

    val borrowedAt = try {
        record.borrowedAtUtc?.let { Instant.parse(it).toEpochMilli() }
    } catch (_: Exception) {
        null
    } ?: return true

    val fromMillis = when (range) {
        BorrowHistoryRange.ALL -> Long.MIN_VALUE
        BorrowHistoryRange.TODAY -> startOfDay(System.currentTimeMillis())
        BorrowHistoryRange.LAST_7_DAYS -> startOfDay(System.currentTimeMillis() - ChronoUnit.DAYS.duration.toMillis() * 6L)
        BorrowHistoryRange.LAST_30_DAYS -> startOfDay(System.currentTimeMillis() - ChronoUnit.DAYS.duration.toMillis() * 29L)
        BorrowHistoryRange.CUSTOM -> startOfDay(minOf(customFromMillis, customToMillis))
    }
    val toExclusiveMillis = when (range) {
        BorrowHistoryRange.ALL -> Long.MAX_VALUE
        BorrowHistoryRange.TODAY,
        BorrowHistoryRange.LAST_7_DAYS,
        BorrowHistoryRange.LAST_30_DAYS -> startOfDay(System.currentTimeMillis()) + ChronoUnit.DAYS.duration.toMillis()
        BorrowHistoryRange.CUSTOM -> startOfDay(maxOf(customFromMillis, customToMillis)) + ChronoUnit.DAYS.duration.toMillis()
    }

    return borrowedAt >= fromMillis && borrowedAt < toExclusiveMillis
}

private fun buildBorrowCsv(rows: List<BorrowLogDto>, isHistory: Boolean): String {
    val builder = StringBuilder()
    if (isHistory) {
        builder.appendLine("SerialNumber,ItemDescription,BorrowedBy,Department,BorrowedAt,ReturnedAt,ReturnedBy,EncodedBy")
        rows.forEach { row ->
            builder.appendLine(
                listOf(
                    csvCell(row.serialNumber),
                    csvCell(row.itemName.orEmpty().ifBlank { row.itemDescription.orEmpty() }),
                    csvCell(row.borrowedByEmpName),
                    csvCell(row.borrowedByDeptName),
                    csvCell(formatRecordTimestamp(row.borrowedAtUtc)),
                    csvCell(formatRecordTimestamp(row.returnedAtUtc)),
                    csvCell(row.returnedByEmpName),
                    csvCell(row.borrowEncodedByUserName)
                ).joinToString(",")
            )
        }
    } else {
        builder.appendLine("SerialNumber,ItemDescription,BorrowedBy,Department,Elapsed,EncodedBy,BorrowedAt")
        rows.forEach { row ->
            builder.appendLine(
                listOf(
                    csvCell(row.serialNumber),
                    csvCell(row.itemName.orEmpty().ifBlank { row.itemDescription.orEmpty() }),
                    csvCell(row.borrowedByEmpName),
                    csvCell(row.borrowedByDeptName),
                    csvCell(formatElapsed(row.borrowedAtUtc)),
                    csvCell(row.borrowEncodedByUserName),
                    csvCell(formatRecordTimestamp(row.borrowedAtUtc))
                ).joinToString(",")
            )
        }
    }

    return builder.toString()
}

private fun csvCell(value: String?): String {
    val safeValue = (value ?: "").replace("\"", "\"\"")
    return "\"$safeValue\""
}

private fun showRecordsDatePicker(context: android.content.Context, currentMillis: Long, onSelected: (Long) -> Unit) {
    val calendar = Calendar.getInstance().apply { timeInMillis = currentMillis }
    DatePickerDialog(
        context,
        { _, year, month, dayOfMonth ->
            val updated = Calendar.getInstance().apply { timeInMillis = currentMillis }
            updated.set(Calendar.YEAR, year)
            updated.set(Calendar.MONTH, month)
            updated.set(Calendar.DAY_OF_MONTH, dayOfMonth)
            onSelected(updated.timeInMillis)
        },
        calendar.get(Calendar.YEAR),
        calendar.get(Calendar.MONTH),
        calendar.get(Calendar.DAY_OF_MONTH)
    ).show()
}

private fun startOfDay(currentMillis: Long): Long {
    val calendar = Calendar.getInstance().apply { timeInMillis = currentMillis }
    calendar.set(Calendar.HOUR_OF_DAY, 0)
    calendar.set(Calendar.MINUTE, 0)
    calendar.set(Calendar.SECOND, 0)
    calendar.set(Calendar.MILLISECOND, 0)
    return calendar.timeInMillis
}

private fun formatShortDate(millis: Long): String {
    return DateTimeFormatter.ofPattern("MMM d", Locale.getDefault())
        .format(Instant.ofEpochMilli(millis).atZone(ZoneId.systemDefault()))
}

private fun formatElapsed(raw: String?): String {
    if (raw.isNullOrBlank()) return "--"
    return try {
        val duration = Duration.between(Instant.parse(raw), Instant.now())
        val totalHours = duration.toHours()
        val days = totalHours / 24
        val hours = totalHours % 24
        when {
            days > 0 -> "${days}d ${hours}h"
            totalHours > 0 -> "${hours}h"
            else -> "${duration.toMinutes().coerceAtLeast(0)}m"
        }
    } catch (_: Exception) {
        "--"
    }
}

private fun formatRecordTimestamp(raw: String?): String {
    if (raw.isNullOrBlank()) return "--"
    return try {
        DateTimeFormatter.ofPattern("MMM d, yyyy h:mm a", Locale.getDefault())
            .format(Instant.parse(raw).atZone(ZoneId.systemDefault()))
    } catch (_: Exception) {
        raw.replace('T', ' ').replace("Z", "").take(19)
    }
}

private fun formatCompactTimestamp(raw: String?): String {
    if (raw.isNullOrBlank()) return "--"
    return try {
        DateTimeFormatter.ofPattern("MMM d", Locale.getDefault())
            .format(Instant.parse(raw).atZone(ZoneId.systemDefault()))
    } catch (_: Exception) {
        raw.take(10)
    }
}