package com.example.yakultscanner.ui.screens

import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.tween
import androidx.compose.foundation.background
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.interaction.collectIsPressedAsState
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
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.automirrored.filled.ArrowForward
import androidx.compose.material.icons.automirrored.filled.Help
import androidx.compose.material.icons.filled.CloudOff
import androidx.compose.material.icons.filled.WifiOff
import androidx.compose.material.icons.filled.Info
import androidx.compose.material.icons.filled.PhotoCamera
import androidx.compose.material.icons.filled.QrCodeScanner
import androidx.compose.material.icons.filled.Star
import androidx.compose.material.icons.filled.CloudUpload
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material.icons.automirrored.outlined.Logout
import androidx.compose.material.icons.outlined.StarBorder
import androidx.compose.material3.ripple
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
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
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.scale
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.hilt.lifecycle.viewmodel.compose.hiltViewModel
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import androidx.navigation.NavController
import com.example.yakultscanner.PinnedSetsStore
import com.example.yakultscanner.UserSession
import com.example.yakultscanner.api.ApiClient
import com.example.yakultscanner.api.ApiResult
import com.example.yakultscanner.api.safeApiCall
import com.example.yakultscanner.loadScanHistory
import com.example.yakultscanner.ScanHistoryEntry
import com.example.yakultscanner.settings.ApiSettings
import com.example.yakultscanner.ui.components.ApiFailurePanel
import com.example.yakultscanner.ui.components.DispatchStatsCard
import com.example.yakultscanner.data.repository.OfflineRepository
import com.example.yakultscanner.network.ConnectivityMonitor
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import javax.inject.Inject
import java.net.URLEncoder
import java.nio.charset.StandardCharsets
import java.util.Locale

@HiltViewModel
class HomeViewModel @Inject constructor(
    private val offlineRepository: OfflineRepository,
    val connectivityMonitor: ConnectivityMonitor
) : androidx.lifecycle.ViewModel() {
    private val _pendingSyncCount = MutableStateFlow(0)
    val pendingSyncCount: StateFlow<Int> = _pendingSyncCount.asStateFlow()

    init {
        viewModelScope.launch {
            offlineRepository.getPendingCountFlow().collect { count ->
                _pendingSyncCount.value = count
            }
        }
    }
}

@androidx.compose.material3.ExperimentalMaterial3Api
@Composable
fun HomeScreen(
    navController: NavController,
    viewModel: HomeViewModel = hiltViewModel()
) {
    val currentUser = UserSession.currentUser
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    var pinnedSetCodes by remember { mutableStateOf(PinnedSetsStore.load(context)) }
    val history = remember { loadScanHistory(context) }
    val recentSets = history.filter { it.setCode != null }.distinctBy { it.setCode }
    val displayedRecentSets = remember(recentSets, pinnedSetCodes) {
        val pins = pinnedSetCodes
        val normalize: (String?) -> String? = { it?.trim()?.uppercase(Locale.getDefault())?.takeIf(String::isNotBlank) }
        val (pinned, others) = recentSets.partition { entry ->
            val normalized = normalize(entry.setCode)
            normalized != null && pins.contains(normalized)
        }
        pinned + others
    }

    val pendingSyncCount by viewModel.pendingSyncCount.collectAsState()
    val hasPendingSync = pendingSyncCount > 0

    // Connectivity and offline mode state
    val isConnected by viewModel.connectivityMonitor.isConnected.collectAsState()
    val isOfflineMode = remember { ApiSettings.isOfflineMode }

    val scrollState = rememberScrollState()
    var showLogoutConfirm by remember { mutableStateOf(false) }
    var pendingIssuesBySetCode by remember { mutableStateOf<Map<String, Int>>(emptyMap()) }
    var pendingIssuesErrorUi by remember { mutableStateOf<Pair<String, String>?>(null) }

    suspend fun refreshPendingIssueBadges() {
        when (val result = safeApiCall { ApiClient.service.getPendingUpdates() }) {
            is ApiResult.Success -> {
                pendingIssuesBySetCode = result.data
                    .mapNotNull { it.setCode?.trim()?.takeIf(String::isNotEmpty) }
                    .groupingBy { it.uppercase(Locale.getDefault()) }
                    .eachCount()
                pendingIssuesErrorUi = null
            }
            is ApiResult.HttpError -> {
                pendingIssuesBySetCode = emptyMap()
                pendingIssuesErrorUi = buildApiError(
                    "Couldn't load pending issue counts.",
                    result.endpoint,
                    "HTTP: ${result.code}",
                    result.message
                )
            }
            is ApiResult.NetworkError -> {
                pendingIssuesBySetCode = emptyMap()
                pendingIssuesErrorUi = buildApiError(
                    "Network issue while loading issue counts.",
                    result.endpoint,
                    "Type: Network error",
                    result.message
                )
            }
            is ApiResult.UnknownError -> {
                pendingIssuesBySetCode = emptyMap()
                pendingIssuesErrorUi = buildApiError(
                    "Unexpected error while loading issue counts.",
                    result.endpoint,
                    "Type: Unexpected error",
                    result.message
                )
            }
        }
    }

    LaunchedEffect(currentUser?.username) {
        refreshPendingIssueBadges()
    }

    val deviceScanInteractionSource = remember { MutableInteractionSource() }
    val deviceScanPressed by deviceScanInteractionSource.collectIsPressedAsState()
    val deviceScanScale by animateFloatAsState(
        targetValue = if (deviceScanPressed) 0.98f else 1f,
        animationSpec = tween(150),
        label = "deviceScanScale"
    )

    val cameraScanInteractionSource = remember { MutableInteractionSource() }
    val cameraScanPressed by cameraScanInteractionSource.collectIsPressedAsState()
    val cameraScanScale by animateFloatAsState(
        targetValue = if (cameraScanPressed) 0.98f else 1f,
        animationSpec = tween(150),
        label = "cameraScanScale"
    )

    val headerBrush = Brush.verticalGradient(
        colors = listOf(com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Ink, Color(0xFFA36A72))
    )

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Text(
                        "Yakult Scanner",
                        style = MaterialTheme.typography.headlineMedium.copy(fontWeight = FontWeight.Bold)
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
                    IconButton(onClick = { navController.navigate("help") }) {
                        Icon(
                            imageVector = Icons.AutoMirrored.Filled.Help,
                            contentDescription = "How to scan & upload",
                            tint = MaterialTheme.colorScheme.onPrimary
                        )
                    }
                    IconButton(onClick = { navController.navigate("app_info") }) {
                        Icon(
                            imageVector = Icons.Filled.Info,
                            contentDescription = "Settings and app info",
                            tint = MaterialTheme.colorScheme.onPrimary
                        )
                    }
                    if (currentUser != null) {
                        IconButton(onClick = { showLogoutConfirm = true }) {
                            Icon(
                                imageVector = Icons.AutoMirrored.Outlined.Logout,
                                contentDescription = "Logout",
                                tint = MaterialTheme.colorScheme.onPrimary
                            )
                        }
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = Color.Transparent,
                    titleContentColor = MaterialTheme.colorScheme.onPrimary,
                    actionIconContentColor = MaterialTheme.colorScheme.onPrimary
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
                    .height(272.dp)
                    .background(headerBrush)
            )

            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(paddingValues)
                    .verticalScroll(scrollState)
                    .padding(horizontal = 24.dp, vertical = 16.dp),
                verticalArrangement = Arrangement.spacedBy(16.dp)
            ) {
                if (currentUser != null) {
                    Row(
                        horizontalArrangement = Arrangement.spacedBy(8.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Surface(
                            color = MaterialTheme.colorScheme.onPrimary.copy(alpha = 0.16f),
                            shape = RoundedCornerShape(50),
                            border = BorderStroke(1.dp, MaterialTheme.colorScheme.onPrimary.copy(alpha = 0.22f))
                        ) {
                            Text(
                                text = "Logged in as ${currentUser.displayName}",
                                style = MaterialTheme.typography.labelMedium,
                                color = MaterialTheme.colorScheme.onPrimary,
                                modifier = Modifier.padding(horizontal = 12.dp, vertical = 6.dp)
                            )
                        }

                        // Offline/Online Indicator
                        if (!isConnected || isOfflineMode) {
                            Surface(
                                color = if (isOfflineMode) 
                                    MaterialTheme.colorScheme.error.copy(alpha = 0.9f)
                                else 
                                    MaterialTheme.colorScheme.tertiary.copy(alpha = 0.9f),
                                shape = RoundedCornerShape(50)
                            ) {
                                Row(
                                    horizontalArrangement = Arrangement.spacedBy(4.dp),
                                    verticalAlignment = Alignment.CenterVertically,
                                    modifier = Modifier.padding(horizontal = 10.dp, vertical = 4.dp)
                                ) {
                                    Icon(
                                        imageVector = if (isOfflineMode) 
                                            Icons.Filled.CloudOff 
                                        else 
                                            Icons.Filled.WifiOff,
                                        contentDescription = null,
                                        tint = MaterialTheme.colorScheme.onError,
                                        modifier = Modifier.size(14.dp)
                                    )
                                    Text(
                                        text = if (isOfflineMode) "OFFLINE MODE" else "NO NETWORK",
                                        style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.Bold),
                                        color = MaterialTheme.colorScheme.onError
                                    )
                                }
                            }
                        }
                    }
                }

                Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
                    Text(
                        text = "Start scanning",
                        style = MaterialTheme.typography.headlineSmall.copy(fontWeight = FontWeight.Bold),
                        color = MaterialTheme.colorScheme.onPrimary
                    )
                    Text(
                        text = "Use the built-in scanner first, or switch to the camera when needed.",
                        style = MaterialTheme.typography.bodyLarge,
                        color = MaterialTheme.colorScheme.onPrimary.copy(alpha = 0.9f)
                    )
                }

                // Dispatch Stats Card
                val scanHistory = loadScanHistory(context)
                val today = java.util.Calendar.getInstance()
                val todayScanned = scanHistory.count { entry: ScanHistoryEntry ->
                    val scanDate = java.util.Calendar.getInstance().apply { timeInMillis = entry.timestamp }
                    today.get(java.util.Calendar.YEAR) == scanDate.get(java.util.Calendar.YEAR) &&
                    today.get(java.util.Calendar.DAY_OF_YEAR) == scanDate.get(java.util.Calendar.DAY_OF_YEAR)
                }
                val todayDeployed = scanHistory.count { entry: ScanHistoryEntry ->
                    entry.status?.contains("dispatch", ignoreCase = true) == true && run {
                        val scanDate = java.util.Calendar.getInstance().apply { timeInMillis = entry.timestamp }
                        today.get(java.util.Calendar.YEAR) == scanDate.get(java.util.Calendar.YEAR) &&
                        today.get(java.util.Calendar.DAY_OF_YEAR) == scanDate.get(java.util.Calendar.DAY_OF_YEAR)
                    }
                }

                DispatchStatsCard(
                    setsScannedToday = todayScanned,
                    setsDeployedToday = todayDeployed,
                    pendingDeployments = pendingSyncCount,
                    onScanClick = { navController.navigate("scanner_device") },
                    onViewPendingClick = { navController.navigate("pending_updates") }
                )

                if (pendingIssuesErrorUi != null) {
                    ApiFailurePanel(
                        message = pendingIssuesErrorUi!!.first,
                        details = pendingIssuesErrorUi!!.second,
                        retryLabel = "Retry",
                        onRetry = { scope.launch { refreshPendingIssueBadges() } }
                    )
                }

                PrimaryScanCard(
                    title = "New Device Scan",
                    description = "Fastest option for daily scanning with the built-in hardware scanner.",
                    icon = Icons.Filled.QrCodeScanner,
                    scale = deviceScanScale,
                    interactionSource = deviceScanInteractionSource
                ) {
                    navController.navigate("scanner_device")
                }

                PrimaryScanCard(
                    title = "Use Phone Camera",
                    description = "Fallback workflow for camera-based scanning when hardware scanning is unavailable.",
                    icon = Icons.Filled.PhotoCamera,
                    scale = cameraScanScale,
                    interactionSource = cameraScanInteractionSource
                ) {
                    navController.navigate("scanner_camera")
                }

                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(12.dp)
                ) {
                    CompactActionButton(
                        modifier = Modifier.weight(1f),
                        title = "History",
                        subtitle = "Review scans",
                        icon = Icons.Filled.Star
                    ) {
                        navController.navigate("history")
                    }
                    CompactActionButton(
                        modifier = Modifier.weight(1f),
                        title = "Sync",
                        subtitle = if (hasPendingSync) "$pendingSyncCount pending" else "Up to date",
                        icon = Icons.Filled.CloudUpload,
                        badgeCount = if (hasPendingSync) pendingSyncCount else null
                    ) {
                        navController.navigate("pending_updates")
                    }
                    CompactActionButton(
                        modifier = Modifier.weight(1f),
                        title = "Issues",
                        subtitle = "Pending concerns",
                        icon = Icons.Filled.Warning
                    ) {
                        navController.navigate("report_issues")
                    }
                }

                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(12.dp)
                ) {
                    CompactActionButton(
                        modifier = Modifier.weight(1f),
                        title = "Repair Photos",
                        subtitle = "Upload evidence",
                        icon = Icons.Filled.PhotoCamera
                    ) {
                        navController.navigate("repair_photo_upload_entry")
                    }
                }

                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Column(verticalArrangement = Arrangement.spacedBy(2.dp)) {
                        Text(
                            text = "Recent Sets",
                            style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold)
                        )
                        Text(
                            text = if (recentSets.isEmpty()) "Nothing scanned yet" else "${recentSets.size} unique sets",
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                    if (recentSets.isNotEmpty()) {
                        TextButton(onClick = { navController.navigate("history") }) {
                            Text("Open history")
                        }
                    }
                }

                if (recentSets.isEmpty()) {
                    Surface(
                        modifier = Modifier.fillMaxWidth(),
                        shape = RoundedCornerShape(16.dp),
                        color = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.55f)
                    ) {
                        Box(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(24.dp),
                            contentAlignment = Alignment.Center
                        ) {
                            Text(
                                text = "No recent sets yet.\nRun your first scan to start building history.",
                                style = MaterialTheme.typography.bodyMedium,
                                textAlign = TextAlign.Center,
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                        }
                    }
                } else {
                    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                        displayedRecentSets.take(3).forEach { entry ->
                            val statusText = entry.status ?: "No status"
                            val statusColor = when {
                                statusText.contains("dispatch", ignoreCase = true) -> Color(0xFF4CAF50)
                                statusText.contains("pending", ignoreCase = true) -> Color(0xFFFFC107)
                                else -> Color(0xFFE53935)
                            }
                            val setCodeKey = entry.setCode?.trim()?.uppercase(Locale.getDefault())
                            val pendingIssueCount = if (!setCodeKey.isNullOrBlank()) {
                                pendingIssuesBySetCode[setCodeKey] ?: 0
                            } else {
                                0
                            }

                            Card(
                                modifier = Modifier.fillMaxWidth(),
                                shape = RoundedCornerShape(16.dp),
                                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
                                elevation = CardDefaults.cardElevation(defaultElevation = 2.dp),
                                border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.5f)),
                                onClick = {
                                    val encoded = URLEncoder.encode(entry.rawJson, StandardCharsets.UTF_8.toString())
                                    navController.navigate("details/$encoded")
                                }
                            ) {
                                Row(
                                    modifier = Modifier
                                        .fillMaxWidth()
                                        .padding(16.dp),
                                    verticalAlignment = Alignment.CenterVertically
                                ) {
                                    Box(
                                        modifier = Modifier
                                            .size(12.dp)
                                            .clip(CircleShape)
                                            .background(statusColor)
                                    )
                                    Spacer(modifier = Modifier.width(16.dp))
                                    Column(
                                        modifier = Modifier.weight(1f),
                                        verticalArrangement = Arrangement.spacedBy(4.dp)
                                    ) {
                                        Row(
                                            verticalAlignment = Alignment.CenterVertically,
                                            horizontalArrangement = Arrangement.spacedBy(8.dp)
                                        ) {
                                            Text(
                                                text = entry.setCode ?: "Unknown Set",
                                                style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.SemiBold),
                                                color = MaterialTheme.colorScheme.onSurface
                                            )

                                            val normalized = entry.setCode?.trim()?.uppercase(Locale.getDefault())
                                            val pinned = normalized != null && pinnedSetCodes.contains(normalized)
                                            IconButton(
                                                onClick = { pinnedSetCodes = PinnedSetsStore.toggle(context, entry.setCode ?: "") }
                                            ) {
                                                Icon(
                                                    imageVector = if (pinned) Icons.Filled.Star else Icons.Outlined.StarBorder,
                                                    contentDescription = if (pinned) "Unpin set" else "Pin set",
                                                    tint = MaterialTheme.colorScheme.primary
                                                )
                                            }
                                        }
                                        Text(
                                            text = listOfNotNull(entry.employee, entry.status).joinToString(" • "),
                                            style = MaterialTheme.typography.bodySmall,
                                            color = MaterialTheme.colorScheme.onSurfaceVariant
                                        )
                                        if (pendingIssueCount > 0) {
                                            Surface(
                                                color = MaterialTheme.colorScheme.errorContainer,
                                                shape = RoundedCornerShape(50),
                                                border = BorderStroke(1.dp, MaterialTheme.colorScheme.error.copy(alpha = 0.4f))
                                            ) {
                                                Text(
                                                    text = if (pendingIssueCount == 1) "1 issue pending" else "$pendingIssueCount issues pending",
                                                    modifier = Modifier.padding(horizontal = 10.dp, vertical = 4.dp),
                                                    style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.Bold),
                                                    color = MaterialTheme.colorScheme.error
                                                )
                                            }
                                        }
                                    }

                                    Icon(
                                        imageVector = Icons.AutoMirrored.Filled.ArrowForward,
                                        contentDescription = null,
                                        tint = MaterialTheme.colorScheme.onSurfaceVariant,
                                        modifier = Modifier.size(20.dp)
                                    )
                                }
                            }
                        }
                    }
                }

                Spacer(modifier = Modifier.height(24.dp))
            }
        }
    }

    if (showLogoutConfirm) {
        androidx.compose.material3.AlertDialog(
            onDismissRequest = { showLogoutConfirm = false },
            confirmButton = {
                TextButton(onClick = {
                    showLogoutConfirm = false
                    UserSession.logout()
                    navController.navigate("login") {
                        popUpTo("login") { inclusive = true }
                    }
                }) {
                    Text("Logout")
                }
            },
            dismissButton = {
                TextButton(onClick = { showLogoutConfirm = false }) {
                    Text("Cancel")
                }
            },
            title = { Text("Logout") },
            text = { Text("Do you want to sign out of the scanner app?") }
        )
    }
}

@Composable
private fun PrimaryScanCard(
    title: String,
    description: String,
    icon: androidx.compose.ui.graphics.vector.ImageVector,
    scale: Float,
    interactionSource: MutableInteractionSource,
    onClick: () -> Unit
) {
    Card(
        modifier = Modifier
            .fillMaxWidth()
            .scale(scale)
            .clickable(
                interactionSource = interactionSource,
                indication = ripple(),
                onClick = onClick
            ),
        shape = RoundedCornerShape(24.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface.copy(alpha = 0.92f)),
        elevation = CardDefaults.cardElevation(defaultElevation = 8.dp),
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.5f))
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(22.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(16.dp)
        ) {
            Box(
                modifier = Modifier
                    .size(56.dp)
                    .clip(CircleShape)
                    .background(MaterialTheme.colorScheme.primaryContainer),
                contentAlignment = Alignment.Center
            ) {
                Icon(
                    imageVector = icon,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.primary,
                    modifier = Modifier.size(28.dp)
                )
            }
            Column(
                modifier = Modifier.weight(1f),
                verticalArrangement = Arrangement.spacedBy(4.dp)
            ) {
                Text(
                    text = title,
                    style = MaterialTheme.typography.titleLarge.copy(fontWeight = FontWeight.Bold),
                    color = MaterialTheme.colorScheme.onSurface
                )
                Text(
                    text = description,
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
        }
    }
}

@Composable
private fun CompactActionButton(
    modifier: Modifier = Modifier,
    title: String,
    subtitle: String,
    icon: androidx.compose.ui.graphics.vector.ImageVector,
    badgeCount: Int? = null,
    onClick: () -> Unit
) {
    OutlinedButton(
        onClick = onClick,
        modifier = modifier,
        shape = RoundedCornerShape(16.dp),
        colors = ButtonDefaults.outlinedButtonColors(
            containerColor = MaterialTheme.colorScheme.surface,
            contentColor = MaterialTheme.colorScheme.onSurface
        ),
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant)
    ) {
        Box(modifier = Modifier.fillMaxWidth()) {
            Column(
                horizontalAlignment = Alignment.CenterHorizontally,
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(vertical = 10.dp),
                verticalArrangement = Arrangement.spacedBy(4.dp)
            ) {
                Icon(icon, contentDescription = null, tint = MaterialTheme.colorScheme.primary)
                Text(title, style = MaterialTheme.typography.labelLarge)
                Text(
                    text = subtitle,
                    style = MaterialTheme.typography.labelSmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }

            if (badgeCount != null) {
                Surface(
                    modifier = Modifier.align(Alignment.TopEnd),
                    color = MaterialTheme.colorScheme.error,
                    shape = CircleShape
                ) {
                    Text(
                        text = badgeCount.toString(),
                        modifier = Modifier.padding(horizontal = 6.dp, vertical = 2.dp),
                        style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.Bold),
                        color = MaterialTheme.colorScheme.onError
                    )
                }
            }
        }
    }
}