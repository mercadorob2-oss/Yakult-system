package com.example.yakultscanner.ui.screens

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.automirrored.filled.ArrowForward
import androidx.compose.material.icons.filled.History
import androidx.compose.material.icons.filled.PlaylistAdd
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.ExperimentalMaterial3Api
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
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.navigation.NavController
import com.example.yakultscanner.api.ApiClient
import com.example.yakultscanner.api.ApiResult
import com.example.yakultscanner.api.BorrowHomeSummaryResponse
import com.example.yakultscanner.api.BorrowLogDto
import com.example.yakultscanner.api.safeApiCall
import com.example.yakultscanner.api.userMessageOr
import com.example.yakultscanner.ui.adaptive.LocalAdaptiveLayout
import java.time.Duration
import java.time.Instant
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import java.util.Locale

private data class BorrowHomePreview(
    val serial: String,
    val title: String,
    val borrower: String,
    val status: String,
    val note: String,
    val timeLabel: String
)

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun BorrowHomeScreen(navController: NavController) {
    val adaptiveLayout = LocalAdaptiveLayout.current
    var summary by remember { mutableStateOf<BorrowHomeSummaryResponse?>(null) }
    var loading by remember { mutableStateOf(true) }
    var errorText by remember { mutableStateOf<String?>(null) }
    var errorSupportDetails by remember { mutableStateOf<Pair<String, String>?>(null) }
    var reloadToken by remember { mutableStateOf(0) }

    val headerBrush = Brush.verticalGradient(
        colors = listOf(
            MaterialTheme.colorScheme.primary,
            MaterialTheme.colorScheme.primaryContainer
        )
    )

    LaunchedEffect(reloadToken) {
        loading = true
        errorText = null
        errorSupportDetails = null
        when (val result = safeApiCall { ApiClient.service.getBorrowHomeSummary(3) }) {
            is ApiResult.Success -> {
                summary = result.data
            }
            is ApiResult.HttpError -> {
                summary = null
                errorText = result.userMessageOr("We couldn't load live borrow activity right now.")
                errorSupportDetails = buildHttpApiError("Borrow workspace details", result)
            }
            is ApiResult.NetworkError -> {
                summary = null
                errorText = result.userMessageOr("Can't reach the borrow workspace right now. Check your connection and try again.")
                errorSupportDetails = buildNetworkApiError("Borrow workspace details", result)
            }
            is ApiResult.UnknownError -> {
                summary = null
                errorText = result.userMessageOr("Something went wrong while loading the borrow workspace. Please try again.")
                errorSupportDetails = buildUnknownApiError("Borrow workspace details", result)
            }
        }
        loading = false
    }

    val recentItems = summary?.recentRows?.map(::mapBorrowHomePreview).orEmpty()
    val openCount = summary?.openCount ?: 0
    val overdueCount = summary?.overdueCount ?: 0
    val returnedTodayCount = summary?.returnedTodayCount ?: 0
    val oldestOpenText = summary?.oldestOpenBorrowedAtUtc?.let(::formatBorrowHomeElapsed) ?: "--"

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Text(
                        "Borrow Workspace",
                        style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold)
                    )
                },
                navigationIcon = {
                    IconButton(onClick = { navController.popBackStack() }) {
                        Icon(
                            imageVector = Icons.AutoMirrored.Filled.ArrowBack,
                            contentDescription = "Back"
                        )
                    }
                },
                actions = {
                    IconButton(onClick = { navController.navigate("borrow_records") }) {
                        Icon(
                            imageVector = Icons.Filled.History,
                            contentDescription = "Borrow records"
                        )
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
                .background(MaterialTheme.colorScheme.background)
        ) {
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .height(340.dp)
                    .background(headerBrush)
            )

            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(paddingValues)
                    .verticalScroll(rememberScrollState())
                    .padding(horizontal = 22.dp, vertical = 16.dp),
                verticalArrangement = Arrangement.spacedBy(22.dp)
            ) {
                if (loading) {
                    PreviewBanner(text = "Loading live borrow workspace")
                }

                Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text(
                        text = "A cleaner way to manage borrows on mobile",
                        style = MaterialTheme.typography.headlineMedium.copy(fontWeight = FontWeight.Bold),
                        color = MaterialTheme.colorScheme.onPrimary
                    )
                    Text(
                        text = "Record live borrow transactions, catch up missed logbook entries, and review current activity without bouncing between separate pages.",
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onPrimary.copy(alpha = 0.84f)
                    )
                }

                WorkspaceHeroCard(
                    openCount = openCount,
                    overdueCount = overdueCount,
                    returnedTodayCount = returnedTodayCount,
                    oldestOpenText = oldestOpenText,
                    usingPreview = false
                )

                if (adaptiveLayout.isTablet) {
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.spacedBy(adaptiveLayout.cardSpacing),
                        verticalAlignment = Alignment.Top
                    ) {
                        Box(modifier = Modifier.weight(1f)) {
                            PrimaryBorrowActionCard(
                                title = "Borrow / Return Items",
                                description = "Open the mobile transaction form for borrowing, returning, and backdated logbook encoding.",
                                accentBackground = MaterialTheme.colorScheme.primaryContainer,
                                iconTint = MaterialTheme.colorScheme.onPrimaryContainer,
                                badgeText = "Start here",
                                onClick = { navController.navigate("borrow_items") }
                            )
                        }
                        Box(modifier = Modifier.weight(1f)) {
                            SecondaryBorrowActionCard(
                                title = "Borrow Records",
                                description = "Review open and returned entries when you need details, history, or admin actions.",
                                accentBackground = MaterialTheme.colorScheme.secondaryContainer,
                                iconTint = MaterialTheme.colorScheme.onSecondaryContainer,
                                onClick = { navController.navigate("borrow_records") }
                            )
                        }
                    }
                } else {
                    PrimaryBorrowActionCard(
                        title = "Borrow / Return Items",
                        description = "Open the mobile transaction form for borrowing, returning, and backdated logbook encoding.",
                        accentBackground = MaterialTheme.colorScheme.primaryContainer,
                        iconTint = MaterialTheme.colorScheme.onPrimaryContainer,
                        badgeText = "Start here",
                        onClick = { navController.navigate("borrow_items") }
                    )

                    SecondaryBorrowActionCard(
                        title = "Borrow Records",
                        description = "Review open and returned entries when you need details, history, or admin actions.",
                        accentBackground = MaterialTheme.colorScheme.secondaryContainer,
                        iconTint = MaterialTheme.colorScheme.onSecondaryContainer,
                        onClick = { navController.navigate("borrow_records") }
                    )
                }

                if (loading) {
                    LoadingInfoCard()
                } else if (!errorText.isNullOrBlank()) {
                    HomeInfoCard(
                        title = "Live data unavailable",
                        text = errorText!!,
                        accentColor = MaterialTheme.colorScheme.errorContainer,
                        contentColor = MaterialTheme.colorScheme.onErrorContainer,
                        supportDetails = errorSupportDetails,
                        onRetry = { reloadToken++ }
                    )
                }

                Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
                    SectionTitle("Recent Borrow Activity")
                    Text(
                        text = if (recentItems.isEmpty()) {
                            "No recent borrow activity is available yet."
                        } else {
                            "Latest shared borrow activity."
                        },
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }

                if (recentItems.isEmpty() && !loading) {
                    BorrowStateCard(
                        title = "No Recent Activity",
                        message = if (!errorText.isNullOrBlank()) {
                            "Recent activity could not be loaded because the borrow API is currently unavailable."
                        } else {
                            "No recent borrow transactions are available yet."
                        },
                        tone = if (!errorText.isNullOrBlank()) BorrowCardTone.Warning else BorrowCardTone.Neutral
                    )
                } else {
                    recentItems.forEach { item ->
                        RecentBorrowPreviewCard(item)
                    }
                }

                Spacer(modifier = Modifier.height(24.dp))
            }
        }
    }
}

@Composable
private fun PreviewBanner(text: String) {
    BorrowStateCard(
        title = "Workspace Status",
        message = text,
        tone = BorrowCardTone.Neutral,
        badgeText = "Info"
    )
}

@Composable
private fun WorkspaceHeroCard(
    openCount: Int,
    overdueCount: Int,
    returnedTodayCount: Int,
    oldestOpenText: String,
    usingPreview: Boolean
) {
    Card(
        shape = RoundedCornerShape(30.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.35f)),
        elevation = CardDefaults.cardElevation(defaultElevation = 10.dp)
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(18.dp),
            verticalArrangement = Arrangement.spacedBy(14.dp)
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Column(modifier = Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(5.dp)) {
                    Text(
                        text = "Workspace Snapshot",
                        style = MaterialTheme.typography.titleLarge.copy(fontWeight = FontWeight.Bold),
                        color = MaterialTheme.colorScheme.onSurface
                    )
                    Text(
                        text = if (usingPreview) {
                            "Live workspace data is unavailable, so sample values are being shown for now."
                        } else {
                            "Current borrow counts and the latest shared activity are loaded from the live borrow log."
                        },
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
                if (overdueCount > 0) {
                    Surface(
                        color = MaterialTheme.colorScheme.errorContainer,
                        contentColor = MaterialTheme.colorScheme.onErrorContainer,
                        shape = RoundedCornerShape(999.dp)
                    ) {
                        Text(
                            text = if (overdueCount == 1) "1 overdue" else "$overdueCount overdue",
                            modifier = Modifier.padding(horizontal = 10.dp, vertical = 6.dp),
                            style = MaterialTheme.typography.labelMedium,
                            fontWeight = FontWeight.Bold
                        )
                    }
                }
            }

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                BorrowMetricCard(
                    value = openCount.toString(),
                    label = "Open",
                    helper = "Active",
                    accent = MaterialTheme.colorScheme.primaryContainer,
                    modifier = Modifier.weight(1f)
                )
                BorrowMetricCard(
                    value = overdueCount.toString(),
                    label = "Overdue",
                    helper = "24h+",
                    accent = MaterialTheme.colorScheme.errorContainer,
                    modifier = Modifier.weight(1f)
                )
                BorrowMetricCard(
                    value = returnedTodayCount.toString(),
                    label = "Returned",
                    helper = "Today",
                    accent = MaterialTheme.colorScheme.secondaryContainer,
                    modifier = Modifier.weight(1f)
                )
            }

            Surface(
                color = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f),
                shape = RoundedCornerShape(18.dp)
            ) {
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(horizontal = 14.dp, vertical = 12.dp),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Column(verticalArrangement = Arrangement.spacedBy(2.dp)) {
                        Text(
                            text = "Oldest open borrow",
                            style = MaterialTheme.typography.labelMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                        Text(
                            text = oldestOpenText,
                            style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.Bold),
                            color = MaterialTheme.colorScheme.onSurface
                        )
                    }
                    Text(
                        text = "Start in Borrow / Return Items",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
            }
        }
    }
}

@Composable
private fun LoadingInfoCard() {
    BorrowStateCard(
        title = "Loading Workspace",
        message = "Loading live borrow metrics and recent activity...",
        tone = BorrowCardTone.Neutral
    )
}

@Composable
private fun HomeInfoCard(
    title: String,
    text: String,
    accentColor: Color,
    contentColor: Color,
    supportDetails: Pair<String, String>? = null,
    onRetry: (() -> Unit)? = null
) {
    val tone = if (accentColor == MaterialTheme.colorScheme.errorContainer) BorrowCardTone.Error else BorrowCardTone.Warning
    BorrowStateCard(title = title, message = text, tone = tone) {
        if (supportDetails != null) {
            Text(
                text = supportDetails.first,
                style = MaterialTheme.typography.labelMedium,
                color = contentColor
            )
            Text(
                text = supportDetails.second,
                style = MaterialTheme.typography.bodySmall,
                color = contentColor.copy(alpha = 0.84f)
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
private fun SectionTitle(text: String) {
    Text(
        text = text,
        style = MaterialTheme.typography.titleLarge.copy(fontWeight = FontWeight.Bold),
        color = MaterialTheme.colorScheme.onSurface
    )
}

@Composable
private fun PrimaryBorrowActionCard(
    title: String,
    description: String,
    accentBackground: Color,
    iconTint: Color,
    badgeText: String,
    onClick: () -> Unit
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(28.dp),
        colors = CardDefaults.cardColors(containerColor = accentBackground),
        elevation = CardDefaults.cardElevation(defaultElevation = 7.dp),
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.35f)),
        onClick = onClick
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(22.dp),
            horizontalArrangement = Arrangement.spacedBy(16.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Box(
                modifier = Modifier
                    .size(60.dp)
                    .clip(CircleShape)
                    .background(MaterialTheme.colorScheme.surface.copy(alpha = 0.78f)),
                contentAlignment = Alignment.Center
            ) {
                Icon(
                    imageVector = Icons.Filled.PlaylistAdd,
                    contentDescription = null,
                    tint = iconTint,
                    modifier = Modifier.size(28.dp)
                )
            }
            Column(
                modifier = Modifier.weight(1f),
                verticalArrangement = Arrangement.spacedBy(6.dp)
            ) {
                Text(
                    text = title.uppercase(),
                    style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                    color = MaterialTheme.colorScheme.onPrimaryContainer
                )
                Text(
                    text = description,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onPrimaryContainer.copy(alpha = 0.82f)
                )
                Surface(
                    color = MaterialTheme.colorScheme.surface.copy(alpha = 0.9f),
                    shape = RoundedCornerShape(999.dp)
                ) {
                    Text(
                        text = badgeText,
                        modifier = Modifier.padding(horizontal = 10.dp, vertical = 5.dp),
                        style = MaterialTheme.typography.labelSmall,
                        color = MaterialTheme.colorScheme.onSurface
                    )
                }
            }
            Icon(
                imageVector = Icons.AutoMirrored.Filled.ArrowForward,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.onPrimaryContainer.copy(alpha = 0.72f)
            )
        }
    }
}

@Composable
private fun SecondaryBorrowActionCard(
    title: String,
    description: String,
    accentBackground: Color,
    iconTint: Color,
    onClick: () -> Unit
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(26.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.35f)),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp),
        onClick = onClick
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(20.dp),
            horizontalArrangement = Arrangement.spacedBy(16.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Box(
                modifier = Modifier
                    .size(54.dp)
                    .clip(CircleShape)
                    .background(accentBackground),
                contentAlignment = Alignment.Center
            ) {
                Icon(
                    imageVector = Icons.Filled.History,
                    contentDescription = null,
                    tint = iconTint,
                    modifier = Modifier.size(26.dp)
                )
            }
            Column(
                modifier = Modifier.weight(1f),
                verticalArrangement = Arrangement.spacedBy(4.dp)
            ) {
                Text(
                    text = title.uppercase(),
                    style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                    color = MaterialTheme.colorScheme.onSurface
                )
                Text(
                    text = description,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
            Icon(
                imageVector = Icons.AutoMirrored.Filled.ArrowForward,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.onSurfaceVariant
            )
        }
    }
}

@Composable
private fun BorrowMetricCard(
    value: String,
    label: String,
    helper: String,
    accent: Color,
    modifier: Modifier = Modifier
) {
    Card(
        modifier = modifier,
        shape = RoundedCornerShape(18.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.58f)),
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.22f))
    ) {
        Column(
            modifier = Modifier.padding(horizontal = 12.dp, vertical = 12.dp),
            verticalArrangement = Arrangement.spacedBy(6.dp)
        ) {
            Box(
                modifier = Modifier
                    .size(width = 28.dp, height = 5.dp)
                    .clip(RoundedCornerShape(999.dp))
                    .background(accent)
            )
            Text(
                text = value,
                style = MaterialTheme.typography.titleLarge.copy(fontWeight = FontWeight.Bold),
                color = MaterialTheme.colorScheme.onSurface
            )
            Text(
                text = label,
                style = MaterialTheme.typography.labelMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
            if (helper.isNotBlank()) {
                Text(
                    text = helper,
                    style = MaterialTheme.typography.labelSmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
        }
    }
}

@Composable
private fun RecentBorrowPreviewCard(item: BorrowHomePreview) {
    BorrowSummaryCard(
        title = item.title,
        subtitle = "${item.serial} • ${item.borrower}",
        lines = listOf(item.note, item.timeLabel),
        tone = when (item.status) {
            "Overdue" -> BorrowCardTone.Error
            "Returned" -> BorrowCardTone.Success
            else -> BorrowCardTone.Accent
        },
        badgeText = item.status
    )
}

private fun previewBorrowHomeRows(): List<BorrowHomePreview> = listOf(
    BorrowHomePreview(
        serial = "YKLT-LTP-001",
        title = "Lenovo ThinkPad T14",
        borrower = "Russel Mercado",
        status = "Open",
        note = "Workstation migration",
        timeLabel = "3h ago"
    ),
    BorrowHomePreview(
        serial = "YKLT-PRN-014",
        title = "Brother Laser Printer",
        borrower = "Paolo Cruz",
        status = "Returned",
        note = "Backdated from logbook",
        timeLabel = "Today"
    ),
    BorrowHomePreview(
        serial = "YKLT-RT-404",
        title = "MikroTik Router",
        borrower = "Mika Reyes",
        status = "Overdue",
        note = "Branch event support",
        timeLabel = "3d out"
    )
)

private fun mapBorrowHomePreview(row: BorrowLogDto): BorrowHomePreview {
    val borrowedAt = row.borrowedAtUtc?.let(::parseApiInstant)
    val returnedAt = row.returnedAtUtc?.let(::parseApiInstant)
    val isOverdue = row.isOpen && borrowedAt != null && borrowedAt.isBefore(Instant.now().minus(Duration.ofHours(24)))
    val status = when {
        !row.returnedAtUtc.isNullOrBlank() -> "Returned"
        isOverdue -> "Overdue"
        else -> "Open"
    }

    val note = when {
        !row.returnedAtUtc.isNullOrBlank() -> "Returned by ${row.returnedByEmpName.orEmpty().ifBlank { "staff" }}"
        isOverdue -> "Open more than 24 hours"
        else -> row.borrowedByDeptName.orEmpty().ifBlank { "Active borrow" }
    }

    val timeLabel = when {
        returnedAt != null -> formatRelativeInstant(returnedAt)
        borrowedAt != null -> formatRelativeInstant(borrowedAt)
        else -> "--"
    }

    return BorrowHomePreview(
        serial = row.serialNumber.orEmpty(),
        title = row.itemName.orEmpty().ifBlank { row.serialNumber.orEmpty() },
        borrower = row.borrowedByEmpName.orEmpty().ifBlank { "Unknown" },
        status = status,
        note = note,
        timeLabel = timeLabel
    )
}

private fun formatBorrowHomeElapsed(raw: String): String {
    val start = parseApiInstant(raw) ?: return raw
    val rawDuration = Duration.between(start, Instant.now())
    val duration = if (rawDuration.isNegative) Duration.ZERO else rawDuration
    val days = duration.toDays()
    val hours = duration.toHours()
    return if (days >= 1) "${days}d open" else "${hours}h open"
}

private fun formatRelativeInstant(instant: Instant): String {
    val rawDuration = Duration.between(instant, Instant.now())
    val duration = if (rawDuration.isNegative) Duration.ZERO else rawDuration
    val days = duration.toDays()
    val hours = duration.toHours()
    val minutes = duration.toMinutes()
    return when {
        days >= 1 -> "${days}d ago"
        hours >= 1 -> "${hours}h ago"
        minutes >= 1 -> "${minutes}m ago"
        else -> "Just now"
    }
}

private fun parseApiInstant(raw: String): Instant? {
    return try {
        Instant.parse(raw)
    } catch (_: Exception) {
        null
    }
}