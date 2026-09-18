package com.example.yakultscanner.ui.screens

import android.graphics.Color.parseColor
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.automirrored.filled.ArrowForward
import androidx.compose.material.icons.filled.PictureAsPdf
import androidx.compose.material.icons.filled.Assessment
import androidx.compose.material.icons.filled.BarChart
import androidx.compose.material.icons.filled.CalendarMonth
import androidx.compose.material.icons.filled.Insights
import androidx.compose.material.icons.filled.Inventory2
import androidx.compose.material.icons.filled.PendingActions
import androidx.compose.material.icons.filled.People
import androidx.compose.material.icons.filled.QrCodeScanner
import androidx.compose.material.icons.filled.Schedule
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.FilterChip
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.ui.platform.LocalContext
import com.example.yakultscanner.utils.generateReportPdf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import androidx.navigation.NavController
import com.example.yakultscanner.UserSession
import com.example.yakultscanner.api.ApiClient
import com.example.yakultscanner.ui.adaptive.LocalAdaptiveLayout
import com.example.yakultscanner.api.ApiResult
import com.example.yakultscanner.api.ReportKpiDto
import com.example.yakultscanner.api.ReportListRowDto
import com.example.yakultscanner.api.ReportModuleDetailResponse
import com.example.yakultscanner.api.ReportTrendPointDto
import com.example.yakultscanner.api.ReportsSummaryResponse
import com.example.yakultscanner.api.safeApiCall
import com.example.yakultscanner.api.userMessageOr

private data class ReportKpi(
    val title: String,
    val value: String,
    val delta: String,
    val icon: ImageVector,
    val tone: Color
)

private data class ReportModulePreview(
    val id: String,
    val title: String,
    val description: String,
    val statLabel: String,
    val statValue: String,
    val icon: ImageVector,
    val accent: Color
)

private data class ReportModuleDetailBundle(
    val title: String,
    val summary: String,
    val heroLabel: String,
    val heroValue: String,
    val heroTone: Color,
    val keyRows: List<ReportListRow>,
    val leaderboardRows: List<ReportListRow>,
    val timelineRows: List<ReportListRow>
)

private data class ReportTrendPoint(
    val label: String,
    val value: Int
)

private data class ReportListRow(
    val title: String,
    val subtitle: String,
    val value: String,
    val tone: Color
)

private data class ReportsBundle(
    val kpis: List<ReportKpi>,
    val executiveSummary: String,
    val highlights: List<String>,
    val healthSignals: List<ReportListRow>,
    val dispatchTrend: List<ReportTrendPoint>,
    val issueRows: List<ReportListRow>,
    val borrowRows: List<ReportListRow>,
    val activityRows: List<ReportListRow>,
    val actionRows: List<ReportListRow>,
    val timelineRows: List<ReportListRow>
)

@Composable
private fun ReportSupportDetails(title: String, details: String) {
    Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
        Text(
            text = title,
            style = MaterialTheme.typography.labelMedium.copy(fontWeight = FontWeight.Bold),
            color = MaterialTheme.colorScheme.onErrorContainer
        )
        Text(
            text = details,
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onErrorContainer.copy(alpha = 0.84f)
        )
    }
}

@androidx.compose.material3.ExperimentalMaterial3Api
@Composable
fun ReportsScreen(navController: NavController) {
    val headerBrush = Brush.verticalGradient(
        colors = listOf(com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Ink, Color(0xFFA36A72))
    )
    val ranges = listOf("Today", "7 Days", "30 Days")
    var selectedRange by remember { mutableStateOf("7 Days") }
    var summary by remember { mutableStateOf<ReportsSummaryResponse?>(null) }
    var loading by remember { mutableStateOf(true) }
    var errorText by remember { mutableStateOf<String?>(null) }
    var errorSupportDetails by remember { mutableStateOf<Pair<String, String>?>(null) }
    var requiresLogin by remember { mutableStateOf(false) }
    var reloadToken by remember { mutableStateOf(0) }
    val bundle = summary?.toLocalBundle(selectedRange)
    val adaptiveLayout = LocalAdaptiveLayout.current
    val context = LocalContext.current

    val modules = listOf(
        ReportModulePreview(
            id = "dispatch",
            title = "Dispatch Performance",
            description = "Track scanned sets, deployed sets, and pending dispatch workload by day.",
            statLabel = "",
            statValue = "",
            icon = Icons.Filled.BarChart,
            accent = Color(0xFFFFE2D9)
        ),
        ReportModulePreview(
            id = "issues",
            title = "Issue Monitoring",
            description = "Spot recurring item problems, pending fixes, and repair-heavy serials.",
            statLabel = "",
            statValue = "",
            icon = Icons.Filled.PendingActions,
            accent = Color(0xFFFFF0D6)
        ),
        ReportModulePreview(
            id = "borrow",
            title = "Borrow Insights",
            description = "Review open borrows, return turnaround, and overdue handovers.",
            statLabel = "",
            statValue = "",
            icon = Icons.Filled.Schedule,
            accent = Color(0xFFF1E3E8)
        ),
        ReportModulePreview(
            id = "activity",
            title = "User Activity",
            description = "See which encoders uploaded updates, deployments, and borrow actions.",
            statLabel = "",
            statValue = "",
            icon = Icons.Filled.Insights,
            accent = Color(0xFFE6F6DE)
        )
    )

    LaunchedEffect(selectedRange, reloadToken) {
        loading = true
        errorText = null
        errorSupportDetails = null
        requiresLogin = false
        summary = null
        when (val result = safeApiCall { ApiClient.service.getReportsSummary(toApiRange(selectedRange)) }) {
            is ApiResult.Success -> summary = result.data
            is ApiResult.HttpError -> {
                summary = ReportsSummaryResponse(range = toApiRange(selectedRange))
                if (result.code == 401) {
                    requiresLogin = false
                    errorText = null
                    errorSupportDetails = null
                } else {
                    errorText = result.userMessageOr("Reports aren't available right now. Please try again in a moment.")
                    errorSupportDetails = buildHttpApiError("Reports summary details", result)
                }
            }
            is ApiResult.NetworkError -> {
                summary = ReportsSummaryResponse(range = toApiRange(selectedRange))
                errorText = result.userMessageOr("Can't reach reports right now. Check your connection and try again.")
                errorSupportDetails = buildNetworkApiError("Reports summary details", result)
            }
            is ApiResult.UnknownError -> {
                summary = ReportsSummaryResponse(range = toApiRange(selectedRange))
                errorText = result.userMessageOr("Something went wrong while loading reports. Please try again.")
                errorSupportDetails = buildUnknownApiError("Reports summary details", result)
            }
        }
        loading = false
    }
    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Text(
                        "Reports",
                        style = MaterialTheme.typography.headlineSmall.copy(fontWeight = FontWeight.Bold)
                    )
                },
                navigationIcon = {
                    IconButton(onClick = { navController.popBackStack() }) {
                        Icon(
                            imageVector = Icons.AutoMirrored.Filled.ArrowBack,
                            contentDescription = "Back",
                            tint = MaterialTheme.colorScheme.onPrimary
                        )
                    }
                },
                actions = {
                    IconButton(
                        onClick = { summary?.let { generateReportPdf(context, it, selectedRange) } },
                        enabled = summary != null
                    ) {
                        Icon(
                            imageVector = Icons.Filled.PictureAsPdf,
                            contentDescription = "Export PDF",
                            tint = if (summary != null) MaterialTheme.colorScheme.onPrimary else MaterialTheme.colorScheme.onPrimary.copy(alpha = 0.4f)
                        )
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = Color.Transparent,
                    titleContentColor = MaterialTheme.colorScheme.onPrimary,
                    navigationIconContentColor = MaterialTheme.colorScheme.onPrimary
                )
            )
        },
        containerColor = Color.Transparent
    ) { paddingValues ->
        Box(
            modifier = Modifier
                .fillMaxSize()
                .background(com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Canvas)
        ) {
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .height(286.dp)
                    .background(headerBrush)
            )

            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(paddingValues)
                    .verticalScroll(rememberScrollState())
                    .padding(horizontal = if (adaptiveLayout.isTablet) adaptiveLayout.horizontalPadding else 24.dp, vertical = 16.dp),
                verticalArrangement = Arrangement.spacedBy(18.dp)
            ) {
                Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Surface(
                        color = MaterialTheme.colorScheme.onPrimary.copy(alpha = 0.15f),
                        shape = RoundedCornerShape(50),
                        border = BorderStroke(1.dp, MaterialTheme.colorScheme.onPrimary.copy(alpha = 0.2f))
                    ) {
                        Row(
                            modifier = Modifier.padding(horizontal = 12.dp, vertical = 6.dp),
                            verticalAlignment = Alignment.CenterVertically,
                            horizontalArrangement = Arrangement.spacedBy(8.dp)
                        ) {
                            Icon(
                                imageVector = Icons.Filled.CalendarMonth,
                                contentDescription = null,
                                tint = MaterialTheme.colorScheme.onPrimary,
                                modifier = Modifier.size(16.dp)
                            )
                            Text(
                                text = if (bundle != null) "Showing live report data" else "Waiting for report data...",
                                color = MaterialTheme.colorScheme.onPrimary,
                                style = MaterialTheme.typography.labelMedium
                            )
                        }
                    }

                    Text(
                        text = "Operational reports in one place",
                        style = MaterialTheme.typography.headlineMedium.copy(fontWeight = FontWeight.Bold),
                        color = MaterialTheme.colorScheme.onPrimary
                    )
                    Text(
                        text = if (bundle != null) {
                            "These report summaries and drill-downs are being loaded from the live reports service."
                        } else {
                            "Report data is loading or unavailable. Try again in a moment."
                        },
                        style = MaterialTheme.typography.bodyLarge,
                        color = MaterialTheme.colorScheme.onPrimary.copy(alpha = 0.92f)
                    )

                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.spacedBy(8.dp)
                    ) {
                        ranges.forEach { range ->
                            FilterChip(
                                selected = selectedRange == range,
                                onClick = { selectedRange = range },
                                label = { Text(range) }
                            )
                        }
                    }

                    if (loading) {
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.spacedBy(10.dp),
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            CircularProgressIndicator(
                                modifier = Modifier.size(18.dp),
                                color = MaterialTheme.colorScheme.onPrimary,
                                strokeWidth = 2.dp
                            )
                            Text(
                                text = "Loading live reports...",
                                style = MaterialTheme.typography.bodyMedium,
                                color = MaterialTheme.colorScheme.onPrimary
                            )
                        }
                    } else if (!errorText.isNullOrBlank()) {
                        Surface(
                            color = MaterialTheme.colorScheme.errorContainer.copy(alpha = 0.92f),
                            shape = RoundedCornerShape(16.dp)
                        ) {
                            Column(
                                modifier = Modifier.padding(horizontal = 14.dp, vertical = 12.dp),
                                verticalArrangement = Arrangement.spacedBy(10.dp)
                            ) {
                                Text(
                                    text = errorText ?: "",
                                    style = MaterialTheme.typography.bodySmall,
                                    color = MaterialTheme.colorScheme.onErrorContainer
                                )
                                if (errorSupportDetails != null) {
                                    ReportSupportDetails(
                                        title = errorSupportDetails!!.first,
                                        details = errorSupportDetails!!.second
                                    )
                                }
                                Row(
                                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                                    verticalAlignment = Alignment.CenterVertically
                                ) {
                                    OutlinedButton(onClick = { reloadToken++ }) {
                                        Text("Try Again")
                                    }
                                    if (requiresLogin) {
                                        OutlinedButton(
                                            onClick = {
                                                UserSession.logout()
                                                navController.navigate("login") {
                                                    popUpTo(0) { inclusive = true }
                                                }
                                            }
                                        ) {
                                            Text("Sign In Again")
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                if (bundle != null) {
                Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
                    Text(
                        text = "Snapshot",
                        style = MaterialTheme.typography.titleLarge.copy(fontWeight = FontWeight.Bold),
                        color = MaterialTheme.colorScheme.onSurface
                    )

                    bundle.kpis.chunked(if (adaptiveLayout.isTablet) adaptiveLayout.gridColumns else 2).forEach { row ->
                        BoxWithConstraints(modifier = Modifier.fillMaxWidth()) {
                            val gap = 12.dp
                            val cardWidth = reportCardWidth(maxWidth, row.size, gap)

                            Row(
                                modifier = Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.spacedBy(gap)
                            ) {
                                row.forEach { item ->
                                    ReportsKpiCard(
                                        modifier = Modifier.width(cardWidth),
                                        item = item
                                    )
                                }
                            }
                        }
                    }
                }

                ReportsMiniModuleCard(
                    title = "Executive Summary",
                    subtitle = "",
                    summary = bundle.executiveSummary,
                    highlights = bundle.highlights
                )

                Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
                    Text(
                        text = "Health Board",
                        style = MaterialTheme.typography.titleLarge.copy(fontWeight = FontWeight.Bold),
                        color = MaterialTheme.colorScheme.onSurface
                    )

                    DetailSectionCard(
                        title = "Operational Signals",
                        subtitle = "Quick readouts for what looks healthy and what needs attention."
                    ) {
                        bundle.healthSignals.forEach { row ->
                            DetailMetricRow(row = row)
                        }
                    }
                }

                Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
                    Text(
                        text = "Dispatch Trend",
                        style = MaterialTheme.typography.titleLarge.copy(fontWeight = FontWeight.Bold),
                        color = MaterialTheme.colorScheme.onSurface
                    )

                    DispatchTrendCard(points = bundle.dispatchTrend)
                }

                Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
                    Text(
                        text = "Report Modules",
                        style = MaterialTheme.typography.titleLarge.copy(fontWeight = FontWeight.Bold),
                        color = MaterialTheme.colorScheme.onSurface
                    )

                    if (adaptiveLayout.isTablet) {
                        modules.chunked(adaptiveLayout.gridColumns).forEach { row ->
                            Row(
                                modifier = Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.spacedBy(12.dp)
                            ) {
                                row.forEach { module ->
                                    Box(modifier = Modifier.weight(1f)) {
                                        val displayModule = module.withStats(bundle)
                                        ReportModuleCard(
                                            module = displayModule,
                                            onClick = { navController.navigate("reports_module/${module.id}?range=${toApiRange(selectedRange)}") }
                                        )
                                    }
                                }
                                repeat(adaptiveLayout.gridColumns - row.size) {
                                    Spacer(modifier = Modifier.weight(1f))
                                }
                            }
                        }
                    } else {
                        modules.forEach { module ->
                            val displayModule = module.withStats(bundle)
                            ReportModuleCard(
                                module = displayModule,
                                onClick = { navController.navigate("reports_module/${module.id}?range=${toApiRange(selectedRange)}") }
                            )
                        }
                    }
                }

                Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
                    Text(
                        text = "Action Queue",
                        style = MaterialTheme.typography.titleLarge.copy(fontWeight = FontWeight.Bold),
                        color = MaterialTheme.colorScheme.onSurface
                    )

                    DetailSectionCard(
                        title = "Recommended Next Moves",
                        subtitle = ""
                    ) {
                        bundle.actionRows.forEach { row ->
                            ActionMetricRow(row = row)
                        }
                    }
                }

                Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
                    Text(
                        text = "Detailed Previews",
                        style = MaterialTheme.typography.titleLarge.copy(fontWeight = FontWeight.Bold),
                        color = MaterialTheme.colorScheme.onSurface
                    )

                    DetailSectionCard(
                        title = "Issue Queue",
                        subtitle = "Live rows showing the current pending issue queue."
                    ) {
                        bundle.issueRows.forEach { row ->
                            DetailMetricRow(row = row)
                        }
                    }

                    DetailSectionCard(
                        title = "Borrow Watchlist",
                        subtitle = "Live rows for overdue and active borrow tracking."
                    ) {
                        bundle.borrowRows.forEach { row ->
                            DetailMetricRow(row = row)
                        }
                    }

                    DetailSectionCard(
                        title = "Encoder Activity",
                        subtitle = "Live rows for user contribution and action totals."
                    ) {
                        bundle.activityRows.forEach { row ->
                            DetailMetricRow(row = row)
                        }
                    }

                    DetailSectionCard(
                        title = "Recent Timeline",
                        subtitle = ""
                    ) {
                        bundle.timelineRows.forEach { row ->
                            TimelineMetricRow(row = row)
                        }
                    }
                }

                Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
                    Text(
                        text = "Ranked Highlights",
                        style = MaterialTheme.typography.titleLarge.copy(fontWeight = FontWeight.Bold),
                        color = MaterialTheme.colorScheme.onSurface
                    )
                }
                } // end if (bundle != null)

                Spacer(modifier = Modifier.height(24.dp))
            }
        }
    }
}

@androidx.compose.material3.ExperimentalMaterial3Api
@Composable
fun ReportModuleDetailScreen(
    navController: NavController,
    moduleId: String,
    range: String
) {
    var detailResponse by remember(moduleId, range) { mutableStateOf<ReportModuleDetailResponse?>(null) }
    var loading by remember(moduleId, range) { mutableStateOf(true) }
    var errorText by remember(moduleId, range) { mutableStateOf<String?>(null) }
    var errorSupportDetails by remember(moduleId, range) { mutableStateOf<Pair<String, String>?>(null) }
    var requiresLogin by remember(moduleId, range) { mutableStateOf(false) }
    var reloadToken by remember(moduleId, range) { mutableStateOf(0) }
    val detail = detailResponse?.toLocalDetail()
    val headerBrush = Brush.verticalGradient(
        colors = listOf(com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Ink, Color(0xFFA36A72))
    )

    LaunchedEffect(moduleId, range, reloadToken) {
        loading = true
        errorText = null
        errorSupportDetails = null
        requiresLogin = false
        detailResponse = null
        when (val result = safeApiCall { ApiClient.service.getReportModuleDetail(range, "api/Reports/Module/$moduleId") }) {
            is ApiResult.Success -> detailResponse = result.data
            is ApiResult.HttpError -> {
                detailResponse = ReportModuleDetailResponse(moduleId = moduleId)
                if (result.code == 401) {
                    requiresLogin = false
                    errorText = null
                    errorSupportDetails = null
                } else {
                    errorText = result.userMessageOr("That report section isn't available right now. Please try again in a moment.")
                    errorSupportDetails = buildHttpApiError("Report module details", result)
                }
            }
            is ApiResult.NetworkError -> {
                detailResponse = ReportModuleDetailResponse(moduleId = moduleId)
                errorText = result.userMessageOr("Can't reach reports right now. Check your connection and try again.")
                errorSupportDetails = buildNetworkApiError("Report module details", result)
            }
            is ApiResult.UnknownError -> {
                detailResponse = ReportModuleDetailResponse(moduleId = moduleId)
                errorText = result.userMessageOr("Something went wrong while loading this report section. Please try again.")
                errorSupportDetails = buildUnknownApiError("Report module details", result)
            }
        }
        loading = false
    }
    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Text(
                        detail?.title ?: moduleId.replaceFirstChar { it.uppercase() },
                        style = MaterialTheme.typography.titleLarge.copy(fontWeight = FontWeight.Bold)
                    )
                },
                navigationIcon = {
                    IconButton(onClick = { navController.popBackStack() }) {
                        Icon(
                            imageVector = Icons.AutoMirrored.Filled.ArrowBack,
                            contentDescription = "Back",
                            tint = MaterialTheme.colorScheme.onPrimary
                        )
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = Color.Transparent,
                    titleContentColor = MaterialTheme.colorScheme.onPrimary,
                    navigationIconContentColor = MaterialTheme.colorScheme.onPrimary
                )
            )
        },
        containerColor = Color.Transparent
    ) { paddingValues ->
        Box(
            modifier = Modifier
                .fillMaxSize()
                .background(com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Canvas)
        ) {
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .height(240.dp)
                    .background(headerBrush)
            )

            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(paddingValues)
                    .verticalScroll(rememberScrollState())
                    .padding(horizontal = 24.dp, vertical = 16.dp),
                verticalArrangement = Arrangement.spacedBy(16.dp)
            ) {
                Surface(
                    color = MaterialTheme.colorScheme.onPrimary.copy(alpha = 0.14f),
                    shape = RoundedCornerShape(50)
                ) {
                    Text(
                        text = if (detail != null) "Showing live module data" else "Waiting for module data...",
                        modifier = Modifier.padding(horizontal = 12.dp, vertical = 6.dp),
                        style = MaterialTheme.typography.labelMedium,
                        color = MaterialTheme.colorScheme.onPrimary
                    )
                }

                Text(
                    text = detail?.summary ?: "",
                    style = MaterialTheme.typography.bodyLarge,
                    color = MaterialTheme.colorScheme.onPrimary
                )

                if (loading) {
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.spacedBy(10.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        CircularProgressIndicator(
                            modifier = Modifier.size(18.dp),
                            color = MaterialTheme.colorScheme.onPrimary,
                            strokeWidth = 2.dp
                        )
                        Text(
                            text = "Loading module details...",
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.onPrimary
                        )
                    }
                } else if (!errorText.isNullOrBlank()) {
                    Surface(
                        color = MaterialTheme.colorScheme.errorContainer.copy(alpha = 0.92f),
                        shape = RoundedCornerShape(16.dp)
                    ) {
                        Column(
                            modifier = Modifier.padding(horizontal = 14.dp, vertical = 12.dp),
                            verticalArrangement = Arrangement.spacedBy(10.dp)
                        ) {
                                Text(
                                    text = errorText ?: "",
                                    style = MaterialTheme.typography.bodySmall,
                                    color = MaterialTheme.colorScheme.onErrorContainer
                                )
                                if (errorSupportDetails != null) {
                                    ReportSupportDetails(
                                        title = errorSupportDetails!!.first,
                                        details = errorSupportDetails!!.second
                                    )
                                }
                                Row(
                                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                                    verticalAlignment = Alignment.CenterVertically
                                ) {
                                    OutlinedButton(onClick = { reloadToken++ }) {
                                        Text("Try Again")
                                    }
                                    if (requiresLogin) {
                                        OutlinedButton(
                                            onClick = {
                                                UserSession.logout()
                                                navController.navigate("login") {
                                                    popUpTo(0) { inclusive = true }
                                                }
                                            }
                                        ) {
                                            Text("Sign In Again")
                                        }
                                    }
                                }
                            }
                        }
                    }

                if (detail != null) {
                Card(
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(24.dp),
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
                    elevation = CardDefaults.cardElevation(defaultElevation = 4.dp)
                ) {
                    Column(
                        modifier = Modifier.padding(18.dp),
                        verticalArrangement = Arrangement.spacedBy(8.dp)
                    ) {
                        Text(
                            text = detail.heroLabel,
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                        Text(
                            text = detail.heroValue,
                            style = MaterialTheme.typography.headlineMedium.copy(fontWeight = FontWeight.Bold),
                            color = detail.heroTone
                        )
                    }
                }

                DetailSectionCard(
                    title = "Key Metrics",
                    subtitle = "Top summary indicators for this report module."
                ) {
                    detail.keyRows.forEach { row ->
                        DetailMetricRow(row = row)
                    }
                }

                DetailSectionCard(
                    title = "Leaderboard",
                    subtitle = "Live ranking for the strongest drivers in this module."
                ) {
                    detail.leaderboardRows.forEach { row ->
                        DetailMetricRow(row = row)
                    }
                }

                DetailSectionCard(
                    title = "Module Timeline",
                    subtitle = ""
                ) {
                    detail.timelineRows.forEach { row ->
                        TimelineMetricRow(row = row)
                    }
                }
                } // end if (detail != null)

                Spacer(modifier = Modifier.height(24.dp))
            }
        }
    }
}



private fun toApiRange(selectedRange: String): String {
    return when (selectedRange) {
        "Today" -> "today"
        "30 Days" -> "30d"
        else -> "7d"
    }
}

private fun parseTone(raw: String?, fallback: Color): Color {
    return try {
        if (raw.isNullOrBlank()) fallback else Color(parseColor(raw))
    } catch (_: Exception) {
        fallback
    }
}

private fun ReportsSummaryResponse.toLocalBundle(selectedRange: String): ReportsBundle {
    val fallback = toFallbackBundle(selectedRange)
    return ReportsBundle(
        kpis = kpis.map { it.toLocalKpi() }.ifEmpty { fallback.kpis },
        executiveSummary = executiveSummary.orEmpty().ifBlank { fallback.executiveSummary },
        highlights = if (highlights.isEmpty()) fallback.highlights else highlights,
        healthSignals = healthSignals.map { it.toLocalRow(Color(0xFF2E7D32)) }.ifEmpty { fallback.healthSignals },
        dispatchTrend = dispatchTrend.map { it.toLocalTrend() }.ifEmpty { fallback.dispatchTrend },
        issueRows = issueRows.map { it.toLocalRow(Color(0xFFA63F2A)) }.ifEmpty { fallback.issueRows },
        borrowRows = borrowRows.map { it.toLocalRow(Color(0xFF265D73)) }.ifEmpty { fallback.borrowRows },
        activityRows = activityRows.map { it.toLocalRow(Color(0xFF466B3C)) }.ifEmpty { fallback.activityRows },
        actionRows = actionRows.map { it.toLocalRow(Color(0xFF7A2C24)) }.ifEmpty { fallback.actionRows },
        timelineRows = timelineRows.map { it.toLocalRow(Color(0xFFA63F2A)) }.ifEmpty { fallback.timelineRows }
    )
}

private fun ReportsSummaryResponse.toFallbackBundle(selectedRange: String): ReportsBundle {
    val items = (totalItems ?: 0).coerceAtLeast(0)
    val borrowed = (totalBorrowed ?: 0).coerceAtLeast(0)
    val sets = (totalSets ?: 0).coerceAtLeast(0)
    val rangeLabel = when (selectedRange) {
        "Today" -> "today"
        "30 Days" -> "the last 30 days"
        else -> "the last 7 days"
    }

    return ReportsBundle(
        kpis = listOf(
            ReportKpi("Dispatched Sets", sets.toString(), "Reported by the reports service", Icons.Filled.QrCodeScanner, Color(0xFF7A2C24)),
            ReportKpi("Inventory Items", items.toString(), "Active inventory records", Icons.Filled.Inventory2, Color(0xFF466B3C)),
            ReportKpi("Active Borrows", borrowed.toString(), "Currently borrowed items", Icons.Filled.Schedule, Color(0xFF265D73)),
            ReportKpi("Report Status", "Live", "Legacy-compatible report view", Icons.Filled.Assessment, Color(0xFFA63F2A))
        ),
        executiveSummary = "Reports are available for $rangeLabel. This view is using the compatible report summary while the live detail feed catches up.",
        highlights = listOf(
            "$sets sets are available from the reports service.",
            "$items inventory items are included in the current report totals.",
            "$borrowed items are currently marked as borrowed."
        ),
        healthSignals = listOf(
            ReportListRow("Set coverage", "Sets returned by the reports service", sets.toString(), Color(0xFF7A2C24)),
            ReportListRow("Inventory coverage", "Inventory items in the summary", items.toString(), Color(0xFF466B3C)),
            ReportListRow("Borrow workload", "Borrowed items in the summary", borrowed.toString(), Color(0xFF265D73))
        ),
        dispatchTrend = legacyTrendPoints(sets, selectedRange),
        issueRows = listOf(
            ReportListRow("No issue detail feed", "The summary endpoint did not include issue rows yet.", "--", Color(0xFFA63F2A))
        ),
        borrowRows = listOf(
            ReportListRow("Borrowed items", "Current borrow total from the summary", borrowed.toString(), Color(0xFF265D73))
        ),
        activityRows = listOf(
            ReportListRow("Inventory records", "Records available for report review", items.toString(), Color(0xFF466B3C))
        ),
        actionRows = listOf(
            ReportListRow("Refresh reports", "Retry once the live detail feed is available.", "Ready", Color(0xFF7A2C24))
        ),
        timelineRows = listOf(
            ReportListRow("Reports summary loaded", "Compatible summary data is displayed for $rangeLabel.", "Now", Color(0xFFA63F2A))
        )
    )
}

private fun legacyTrendPoints(totalSets: Int, selectedRange: String): List<ReportTrendPoint> {
    val labels = when (selectedRange) {
        "Today" -> listOf("00:00", "04:00", "08:00", "12:00", "16:00", "20:00")
        "30 Days" -> listOf("W1", "W2", "W3", "W4", "W5", "Now")
        else -> listOf("Day 1", "Day 2", "Day 3", "Day 4", "Day 5", "Day 6", "Today")
    }
    val count = totalSets.coerceAtLeast(0)
    return labels.mapIndexed { index, label ->
        val previous = (count * index) / labels.size
        val current = (count * (index + 1)) / labels.size
        ReportTrendPoint(label = label, value = (current - previous).coerceAtLeast(0))
    }
}

private fun ReportModulePreview.withStats(bundle: ReportsBundle): ReportModulePreview {
    fun kpiValue(title: String): String = bundle.kpis.firstOrNull { it.title == title }?.value ?: "--"
    return when (id) {
        "dispatch" -> copy(statLabel = "Sets", statValue = kpiValue("Dispatched Sets"))
        "issues" -> copy(statLabel = "Open", statValue = kpiValue("Open Issues"))
        "borrow" -> copy(statLabel = "Active", statValue = kpiValue("Active Borrows"))
        "activity" -> copy(statLabel = "Actions", statValue = kpiValue("Encoder Activity"))
        else -> this
    }
}
private fun ReportModuleDetailResponse.toLocalDetail(): ReportModuleDetailBundle {
    val fallback = fallbackModuleDetail(moduleId)
    val fallbackTone = parseTone(heroTone, fallback.heroTone)
    return ReportModuleDetailBundle(
        title = title.orEmpty().ifBlank { fallback.title },
        summary = summary.orEmpty().ifBlank { fallback.summary },
        heroLabel = heroLabel.orEmpty().ifBlank { fallback.heroLabel },
        heroValue = heroValue.orEmpty().ifBlank { fallback.heroValue },
        heroTone = fallbackTone,
        keyRows = keyRows.map { it.toLocalRow(fallbackTone) }.ifEmpty { fallback.keyRows },
        leaderboardRows = leaderboardRows.map { it.toLocalRow(fallbackTone) }.ifEmpty { fallback.leaderboardRows },
        timelineRows = timelineRows.map { it.toLocalRow(fallbackTone) }.ifEmpty { fallback.timelineRows }
    )
}

private fun fallbackModuleDetail(moduleId: String?): ReportModuleDetailBundle {
    return when (moduleId.orEmpty().lowercase()) {
        "issues" -> ReportModuleDetailBundle(
            title = "Issue Monitoring",
            summary = "Issue detail rows are not available from this reports service yet.",
            heroLabel = "Issue feed",
            heroValue = "--",
            heroTone = Color(0xFFA63F2A),
            keyRows = listOf(ReportListRow("Issue details", "Waiting for live issue rows.", "--", Color(0xFFA63F2A))),
            leaderboardRows = listOf(ReportListRow("No ranked issues", "No issue ranking was returned.", "--", Color(0xFFA63F2A))),
            timelineRows = listOf(ReportListRow("Issue report ready", "Open this again after the detail feed is deployed.", "Now", Color(0xFFA63F2A)))
        )
        "borrow" -> ReportModuleDetailBundle(
            title = "Borrow Insights",
            summary = "Borrow detail rows are not available from this reports service yet.",
            heroLabel = "Borrow feed",
            heroValue = "--",
            heroTone = Color(0xFF265D73),
            keyRows = listOf(ReportListRow("Borrow details", "Waiting for live borrow rows.", "--", Color(0xFF265D73))),
            leaderboardRows = listOf(ReportListRow("No ranked borrows", "No borrow ranking was returned.", "--", Color(0xFF265D73))),
            timelineRows = listOf(ReportListRow("Borrow report ready", "Open this again after the detail feed is deployed.", "Now", Color(0xFF265D73)))
        )
        "activity" -> ReportModuleDetailBundle(
            title = "User Activity",
            summary = "Activity detail rows are not available from this reports service yet.",
            heroLabel = "Activity feed",
            heroValue = "--",
            heroTone = Color(0xFF466B3C),
            keyRows = listOf(ReportListRow("Activity details", "Waiting for live user activity rows.", "--", Color(0xFF466B3C))),
            leaderboardRows = listOf(ReportListRow("No ranked users", "No user activity ranking was returned.", "--", Color(0xFF466B3C))),
            timelineRows = listOf(ReportListRow("Activity report ready", "Open this again after the detail feed is deployed.", "Now", Color(0xFF466B3C)))
        )
        else -> ReportModuleDetailBundle(
            title = "Dispatch Performance",
            summary = "Dispatch detail rows are not available from this reports service yet.",
            heroLabel = "Dispatch feed",
            heroValue = "--",
            heroTone = Color(0xFF7A2C24),
            keyRows = listOf(ReportListRow("Dispatch details", "Waiting for live dispatch rows.", "--", Color(0xFF7A2C24))),
            leaderboardRows = listOf(ReportListRow("No ranked dispatch rows", "No dispatch ranking was returned.", "--", Color(0xFF7A2C24))),
            timelineRows = listOf(ReportListRow("Dispatch report ready", "Open this again after the detail feed is deployed.", "Now", Color(0xFF7A2C24)))
        )
    }
}
private fun ReportKpiDto.toLocalKpi(): ReportKpi {
    val fallback = when (title.orEmpty()) {
        "Open Issues" -> Color(0xFFA63F2A)
        "Active Borrows" -> Color(0xFF265D73)
        "Encoder Activity" -> Color(0xFF466B3C)
        else -> Color(0xFF7A2C24)
    }
    return ReportKpi(
        title = title.orEmpty().ifBlank { "Metric" },
        value = value.orEmpty().ifBlank { "0" },
        delta = delta.orEmpty().ifBlank { "No trend info" },
        icon = when (title.orEmpty()) {
            "Open Issues" -> Icons.Filled.Warning
            "Active Borrows" -> Icons.Filled.Inventory2
            "Encoder Activity" -> Icons.Filled.People
            else -> Icons.Filled.QrCodeScanner
        },
        tone = parseTone(tone, fallback)
    )
}

private fun ReportTrendPointDto.toLocalTrend(): ReportTrendPoint {
    return ReportTrendPoint(
        label = label.orEmpty().ifBlank { "--" },
        value = value
    )
}

private fun ReportListRowDto.toLocalRow(fallback: Color): ReportListRow {
    return ReportListRow(
        title = title.orEmpty().ifBlank { "Untitled" },
        subtitle = subtitle.orEmpty().ifBlank { "No details available." },
        value = value.orEmpty().ifBlank { "--" },
        tone = parseTone(tone, fallback)
    )
}

private fun reportCardWidth(totalWidth: Dp, itemCount: Int, gap: Dp): Dp {
    if (itemCount <= 1) return totalWidth
    return (totalWidth - (gap * (itemCount - 1))) / itemCount
}

@Composable
private fun ReportsKpiCard(
    modifier: Modifier = Modifier,
    item: ReportKpi
) {
    Card(
        modifier = modifier,
        shape = RoundedCornerShape(20.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        elevation = CardDefaults.cardElevation(defaultElevation = 4.dp),
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.55f))
    ) {
        Column(
            modifier = Modifier.padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            Box(
                modifier = Modifier
                    .size(42.dp)
                    .clip(CircleShape)
                    .background(item.tone.copy(alpha = 0.14f)),
                contentAlignment = Alignment.Center
            ) {
                Icon(
                    imageVector = item.icon,
                    contentDescription = null,
                    tint = item.tone
                )
            }
            Text(
                text = item.value,
                style = MaterialTheme.typography.headlineMedium.copy(fontWeight = FontWeight.Bold),
                color = MaterialTheme.colorScheme.onSurface
            )
            Text(
                text = item.title,
                style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onSurface
            )
            Text(
                text = item.delta,
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
        }
    }
}

@Composable
private fun DispatchTrendCard(points: List<ReportTrendPoint>) {
    val maxValue = points.maxOfOrNull { it.value }?.coerceAtLeast(1) ?: 1
    val plotHeight = 118.dp

    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(24.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        elevation = CardDefaults.cardElevation(defaultElevation = 3.dp),
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.5f))
    ) {
        Column(
            modifier = Modifier.padding(18.dp),
            verticalArrangement = Arrangement.spacedBy(14.dp)
        ) {
            Text(
                text = if (points.any { it.value > 0 }) "Dispatch volume trend" else "No dispatch activity yet for this range",
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )

            BoxWithConstraints(modifier = Modifier.fillMaxWidth()) {
                val gap = 10.dp
                val barWidth = reportCardWidth(maxWidth, points.size, gap)

                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(gap),
                    verticalAlignment = Alignment.Bottom
                ) {
                    points.forEach { point ->
                        val ratio = point.value.toFloat() / maxValue.toFloat()
                        Column(
                            modifier = Modifier.width(barWidth),
                            verticalArrangement = Arrangement.spacedBy(8.dp),
                            horizontalAlignment = Alignment.CenterHorizontally
                        ) {
                            Text(
                                text = point.value.toString(),
                                style = MaterialTheme.typography.labelMedium,
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                            Box(
                                modifier = Modifier.height(plotHeight),
                                contentAlignment = Alignment.BottomCenter
                            ) {
                                Box(
                                    modifier = Modifier
                                        .fillMaxWidth()
                                        .height(plotHeight * ratio)
                                        .clip(RoundedCornerShape(topStart = 14.dp, topEnd = 14.dp))
                                        .background(
                                            Brush.verticalGradient(
                                                colors = listOf(Color(0xFFFFA36C), Color(0xFFB62E2E))
                                            )
                                        )
                                )
                            }
                            Text(
                                text = point.label,
                                style = MaterialTheme.typography.labelMedium,
                                color = MaterialTheme.colorScheme.onSurface
                            )
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun ReportsMiniModuleCard(
    title: String,
    subtitle: String,
    summary: String,
    highlights: List<String>
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(24.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.primaryContainer.copy(alpha = 0.45f)),
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.primary.copy(alpha = 0.2f))
    ) {
        Column(
            modifier = Modifier.padding(18.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Column(verticalArrangement = Arrangement.spacedBy(2.dp)) {
                    Text(
                        text = title,
                        style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                        color = MaterialTheme.colorScheme.onSurface
                    )
                    Text(
                        text = subtitle,
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
                Icon(
                    imageVector = Icons.Filled.Assessment,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.primary
                )
            }

            Text(
                text = summary,
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurface
            )

            highlights.forEach { item ->
                Row(
                    verticalAlignment = Alignment.Top,
                    horizontalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    Box(
                        modifier = Modifier
                            .padding(top = 6.dp)
                            .size(6.dp)
                            .clip(CircleShape)
                            .background(MaterialTheme.colorScheme.primary)
                    )
                    Text(
                        text = item,
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
            }
        }
    }
}

@Composable
private fun DetailSectionCard(
    title: String,
    subtitle: String,
    content: @Composable () -> Unit
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(22.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        elevation = CardDefaults.cardElevation(defaultElevation = 2.dp),
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.5f))
    ) {
        Column(
            modifier = Modifier.padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            Text(
                text = title,
                style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                color = MaterialTheme.colorScheme.onSurface
            )
            Text(
                text = subtitle,
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
            content()
        }
    }
}

@Composable
private fun DetailMetricRow(row: ReportListRow) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically
    ) {
        Row(
            modifier = Modifier.fillMaxWidth(0.74f),
            horizontalArrangement = Arrangement.spacedBy(10.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Box(
                modifier = Modifier
                    .size(10.dp)
                    .clip(CircleShape)
                    .background(row.tone)
            )
            Column(verticalArrangement = Arrangement.spacedBy(2.dp)) {
                Text(
                    text = row.title,
                    style = MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.SemiBold),
                    color = MaterialTheme.colorScheme.onSurface
                )
                Text(
                    text = row.subtitle,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
        }
        Surface(
            color = row.tone.copy(alpha = 0.12f),
            shape = RoundedCornerShape(50)
        ) {
            Text(
                text = row.value,
                modifier = Modifier.padding(horizontal = 10.dp, vertical = 6.dp),
                style = MaterialTheme.typography.labelMedium.copy(fontWeight = FontWeight.Bold),
                color = row.tone
            )
        }
    }
}

@Composable
private fun ActionMetricRow(row: ReportListRow) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically
    ) {
        Row(
            modifier = Modifier.fillMaxWidth(0.77f),
            horizontalArrangement = Arrangement.spacedBy(10.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Box(
                modifier = Modifier
                    .size(32.dp)
                    .clip(CircleShape)
                    .background(row.tone.copy(alpha = 0.14f)),
                contentAlignment = Alignment.Center
            ) {
                Icon(
                    imageVector = Icons.AutoMirrored.Filled.ArrowForward,
                    contentDescription = null,
                    tint = row.tone,
                    modifier = Modifier.size(16.dp)
                )
            }
            Column(verticalArrangement = Arrangement.spacedBy(2.dp)) {
                Text(
                    text = row.title,
                    style = MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.SemiBold),
                    color = MaterialTheme.colorScheme.onSurface
                )
                Text(
                    text = row.subtitle,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
        }
        Surface(
            color = row.tone.copy(alpha = 0.12f),
            shape = RoundedCornerShape(50)
        ) {
            Text(
                text = row.value,
                modifier = Modifier.padding(horizontal = 10.dp, vertical = 6.dp),
                style = MaterialTheme.typography.labelMedium.copy(fontWeight = FontWeight.Bold),
                color = row.tone
            )
        }
    }
}

@Composable
private fun TimelineMetricRow(row: ReportListRow) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.spacedBy(12.dp),
        verticalAlignment = Alignment.Top
    ) {
        Column(
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.spacedBy(4.dp)
        ) {
            Box(
                modifier = Modifier
                    .size(10.dp)
                    .clip(CircleShape)
                    .background(row.tone)
            )
            Box(
                modifier = Modifier
                    .width(2.dp)
                    .height(34.dp)
                    .background(row.tone.copy(alpha = 0.28f))
            )
        }
        Column(
            modifier = Modifier.fillMaxWidth(),
            verticalArrangement = Arrangement.spacedBy(4.dp)
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    text = row.title,
                    style = MaterialTheme.typography.labelLarge.copy(fontWeight = FontWeight.Bold),
                    color = MaterialTheme.colorScheme.onSurface
                )
                Surface(
                    color = row.tone.copy(alpha = 0.12f),
                    shape = RoundedCornerShape(50)
                ) {
                    Text(
                        text = row.value,
                        modifier = Modifier.padding(horizontal = 10.dp, vertical = 5.dp),
                        style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.Bold),
                        color = row.tone
                    )
                }
            }
            Text(
                text = row.subtitle,
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
        }
    }
}

@Composable
private fun ReportModuleCard(
    modifier: Modifier = Modifier,
    module: ReportModulePreview,
    onClick: () -> Unit
) {
    Card(
        modifier = modifier
            .fillMaxWidth()
            .clickable(onClick = onClick),
        shape = RoundedCornerShape(22.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        elevation = CardDefaults.cardElevation(defaultElevation = 3.dp),
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.5f))
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(18.dp),
            horizontalArrangement = Arrangement.spacedBy(16.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Box(
                modifier = Modifier
                    .size(52.dp)
                    .clip(CircleShape)
                    .background(module.accent),
                contentAlignment = Alignment.Center
            ) {
                Icon(
                    imageVector = module.icon,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.primary,
                    modifier = Modifier.size(26.dp)
                )
            }

            Column(
                modifier = Modifier.fillMaxWidth(0.78f),
                verticalArrangement = Arrangement.spacedBy(6.dp)
            ) {
                Text(
                    text = module.title,
                    style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                    color = MaterialTheme.colorScheme.onSurface
                )
                Text(
                    text = module.description,
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
                Surface(
                    color = MaterialTheme.colorScheme.surfaceVariant,
                    shape = RoundedCornerShape(50)
                ) {
                    Text(
                        text = "${module.statLabel}: ${module.statValue}",
                        modifier = Modifier.padding(horizontal = 10.dp, vertical = 5.dp),
                        style = MaterialTheme.typography.labelMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
            }

            Icon(
                imageVector = Icons.AutoMirrored.Filled.ArrowForward,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.7f)
            )
        }
    }
}

@Composable
private fun RankedInsightCard(
    title: String,
    items: List<String>
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(20.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        elevation = CardDefaults.cardElevation(defaultElevation = 2.dp),
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.5f))
    ) {
        Column(
            modifier = Modifier.padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            Text(
                text = title,
                style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                color = MaterialTheme.colorScheme.onSurface
            )
            items.forEach { line ->
                Text(
                    text = line,
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
        }
    }
}