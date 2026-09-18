package com.example.yakultscanner.ui.screens

import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.interaction.collectIsPressedAsState
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.IntrinsicSize
import androidx.compose.foundation.layout.fillMaxHeight
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
import androidx.compose.material.icons.automirrored.filled.ArrowForward
import androidx.compose.material.icons.filled.BarChart
import androidx.compose.material.icons.filled.Assessment
import androidx.compose.material.icons.filled.Info
import androidx.compose.material.icons.filled.PlaylistAdd
import androidx.compose.material.icons.filled.QrCodeScanner
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material.icons.filled.HeadsetMic
import androidx.compose.material.icons.filled.PhotoCamera
import androidx.compose.material.icons.outlined.Logout
import androidx.compose.material3.ripple
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
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
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.navigation.NavController
import com.example.yakultscanner.ConnectionStatus
import com.example.yakultscanner.PinnedSetsStore
import com.example.yakultscanner.UserSession
import com.example.yakultscanner.api.ApiClient
import com.example.yakultscanner.api.ApiResult
import com.example.yakultscanner.api.safeApiCall
import com.example.yakultscanner.loadScanHistory
import com.example.yakultscanner.ui.components.ApiFailurePanel
import kotlinx.coroutines.launch
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

@androidx.compose.material3.ExperimentalMaterial3Api
@Composable
fun RootHomeScreen(navController: NavController) {
    val currentUser = UserSession.currentUser
    val context = LocalContext.current
    var pinnedSetCodes by remember { mutableStateOf(PinnedSetsStore.load(context)) }
    val scope = rememberCoroutineScope()
    val snackbarHostState = remember { SnackbarHostState() }
    val history = remember { loadScanHistory(context) }

    var issueCount by remember { mutableStateOf(0) }
    var processedCount by remember { mutableStateOf(0) }
    var openBorrowCount by remember { mutableStateOf<Int?>(null) }
    var dispatchCount by remember { mutableStateOf<Int?>(null) }
    var connectionStatus by remember { mutableStateOf(ConnectionStatus.Checking) }
    var isRefreshing by remember { mutableStateOf(false) }
    var lastUpdatedText by remember { mutableStateOf<String?>(null) }
    var connectionErrorUi by remember { mutableStateOf<Pair<String, String>?>(null) }

    suspend fun refreshDashboard() {
        isRefreshing = true
        connectionStatus = ConnectionStatus.Checking
        try {
            when (val result = safeApiCall { ApiClient.service.getPendingIssues() }) {
                is ApiResult.Success -> {
                    issueCount = result.data.pendingCount ?: 0
                    processedCount = result.data.processedCount ?: 0
                    connectionStatus = ConnectionStatus.Connected
                    connectionErrorUi = null
                }
                is ApiResult.HttpError -> {
                    connectionStatus = ConnectionStatus.Error
                    connectionErrorUi = buildApiError(
                        "Can't reach the system right now.",
                        result.endpoint,
                        "HTTP: ${result.code}",
                        result.message
                    )
                }
                is ApiResult.NetworkError -> {
                    connectionStatus = ConnectionStatus.Error
                    connectionErrorUi = buildApiError(
                        "Network issue while checking the system.",
                        result.endpoint,
                        "Type: Network error",
                        result.message
                    )
                }
                is ApiResult.UnknownError -> {
                    connectionStatus = ConnectionStatus.Error
                    connectionErrorUi = buildApiError(
                        "Something went wrong while checking the system.",
                        result.endpoint,
                        "Type: Unexpected error",
                        result.message
                    )
                }
            }

            when (val br = safeApiCall { ApiClient.service.getBorrowHomeSummary() }) {
                is ApiResult.Success -> openBorrowCount = br.data.openCount
                else -> openBorrowCount = 0
            }

            when (val dr = safeApiCall { ApiClient.service.getDispatchCount() }) {
                is ApiResult.Success -> dispatchCount = dr.data.todayCount
                else -> dispatchCount = 0
            }

            if (connectionStatus == ConnectionStatus.Connected) {
                lastUpdatedText = SimpleDateFormat("h:mm a", Locale.getDefault()).format(Date())
            }
        } finally {
            isRefreshing = false
        }
    }

    LaunchedEffect(Unit) {
        refreshDashboard()
    }

    val scannerInteractionSource = remember { MutableInteractionSource() }
    val scannerPressed by scannerInteractionSource.collectIsPressedAsState()
    val scannerScale by animateFloatAsState(
        targetValue = if (scannerPressed) 0.98f else 1f,
        animationSpec = tween(150),
        label = "scannerScale"
    )

    val borrowInteractionSource = remember { MutableInteractionSource() }
    val borrowPressed by borrowInteractionSource.collectIsPressedAsState()
    val borrowScale by animateFloatAsState(
        targetValue = if (borrowPressed) 0.98f else 1f,
        animationSpec = tween(150),
        label = "borrowScale"
    )

    val toolsInteractionSource = remember { MutableInteractionSource() }
    val toolsPressed by toolsInteractionSource.collectIsPressedAsState()
    val toolsScale by animateFloatAsState(
        targetValue = if (toolsPressed) 0.98f else 1f,
        animationSpec = tween(150),
        label = "toolsScale"
    )

    val reportsInteractionSource = remember { MutableInteractionSource() }
    val reportsPressed by reportsInteractionSource.collectIsPressedAsState()
    val reportsScale by animateFloatAsState(
        targetValue = if (reportsPressed) 0.98f else 1f,
        animationSpec = tween(150),
        label = "reportsScale"
    )

    val callMonitoringInteractionSource = remember { MutableInteractionSource() }
    val callMonitoringPressed by callMonitoringInteractionSource.collectIsPressedAsState()
    val callMonitoringScale by animateFloatAsState(
        targetValue = if (callMonitoringPressed) 0.98f else 1f,
        animationSpec = tween(150),
        label = "callMonitoringScale"
    )

    val repairPhotosInteractionSource = remember { MutableInteractionSource() }
    val repairPhotosPressed by repairPhotosInteractionSource.collectIsPressedAsState()
    val repairPhotosScale by animateFloatAsState(
        targetValue = if (repairPhotosPressed) 0.98f else 1f,
        animationSpec = tween(150),
        label = "repairPhotosScale"
    )

    val infiniteTransition = rememberInfiniteTransition(label = "pulse")
    val dotAlpha by infiniteTransition.animateFloat(
        initialValue = 0.4f,
        targetValue = 1f,
        animationSpec = infiniteRepeatable(animation = tween(1000), repeatMode = RepeatMode.Reverse),
        label = "dotPulse"
    )

    val headerBrush = Brush.verticalGradient(
        colors = listOf(com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Ink, Color(0xFFA36A72))
    )

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Text(
                            "Yakult Inventory",
                            style = MaterialTheme.typography.titleLarge.copy(fontWeight = FontWeight.Bold)
                        )
                        Spacer(modifier = Modifier.width(12.dp))
                        if (connectionStatus == ConnectionStatus.Connected) {
                            Box(
                                modifier = Modifier
                                    .size(8.dp)
                                    .background(Color.Green.copy(alpha = dotAlpha), CircleShape)
                            )
                        } else if (connectionStatus == ConnectionStatus.Error) {
                            Box(
                                modifier = Modifier
                                    .size(8.dp)
                                    .background(MaterialTheme.colorScheme.errorContainer, CircleShape)
                            )
                        }
                    }
                },
                actions = {
                    IconButton(
                        onClick = { scope.launch { refreshDashboard() } },
                        enabled = !isRefreshing
                    ) {
                        Icon(
                            imageVector = Icons.Filled.Refresh,
                            contentDescription = "Refresh",
                            tint = MaterialTheme.colorScheme.onPrimary
                        )
                    }
                    if (currentUser != null) {
                        IconButton(
                            onClick = {
                                UserSession.logout()
                                navController.navigate("login") {
                                    popUpTo("login") { inclusive = true }
                                }
                            }
                        ) {
                            Icon(
                                imageVector = Icons.Outlined.Logout,
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
        containerColor = Color.Transparent,
        snackbarHost = { SnackbarHost(hostState = snackbarHostState) }
    ) { paddingValues ->
        BoxWithConstraints(
            modifier = Modifier
                .fillMaxSize()
                .background(com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Canvas)
        ) {
            val rootIsTablet = maxWidth >= 600.dp
            val rootIsExpanded = maxWidth >= 840.dp

            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .height(290.dp)
                    .background(headerBrush)
            )

            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(paddingValues)
                    .verticalScroll(rememberScrollState())
                    .padding(horizontal = if (rootIsTablet) 32.dp else 24.dp, vertical = 16.dp),
                verticalArrangement = Arrangement.spacedBy(18.dp)
            ) {
                val greetingName = currentUser?.displayName?.takeIf { it.isNotBlank() } ?: "there"

                if (rootIsTablet) {
                    HomeCommandHero(
                        greetingName = greetingName,
                        lastUpdatedText = lastUpdatedText,
                        connectionStatus = connectionStatus,
                        dotAlpha = dotAlpha
                    )

                    if (connectionStatus != ConnectionStatus.Connected) {
                        if (connectionStatus == ConnectionStatus.Error && connectionErrorUi != null) {
                            ApiFailurePanel(
                                message = connectionErrorUi!!.first,
                                details = connectionErrorUi!!.second,
                                retryLabel = "Retry",
                                onRetry = { scope.launch { refreshDashboard() } }
                            )
                        } else {
                            ConnectionNoticeCard()
                        }
                    }

                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .height(IntrinsicSize.Min),
                        horizontalArrangement = Arrangement.spacedBy(20.dp),
                        verticalAlignment = Alignment.Top
                    ) {
                        ScannerCommandCard(
                            modifier = Modifier.weight(1f).fillMaxHeight(),
                            contentFill = true,
                            scale = scannerScale,
                            interactionSource = scannerInteractionSource,
                            localScanCount = history.size,
                            pendingIssueCount = issueCount,
                            onClick = { navController.navigate("home") }
                        )
                        DashboardOverviewCard(
                            modifier = Modifier.weight(1f).fillMaxHeight(),
                            processedCount = processedCount,
                            pendingIssueCount = issueCount,
                            dispatchCount = dispatchCount,
                            openBorrowCount = openBorrowCount,
                            onProcessedClick = { navController.navigate("processed_updates") },
                            onPendingClick = { navController.navigate("pending_updates") },
                            onReportsClick = { navController.navigate("reports") }
                        )
                    }

                    if (pinnedSetCodes.isNotEmpty() || history.isNotEmpty()) {
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.spacedBy(20.dp),
                            verticalAlignment = Alignment.Top
                        ) {
                            Spacer(Modifier.weight(1f))
                            Column(
                                modifier = Modifier.weight(1f),
                                verticalArrangement = Arrangement.spacedBy(18.dp)
                            ) {
                                if (pinnedSetCodes.isNotEmpty()) {
                                    Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                                        Row(
                                            modifier = Modifier.fillMaxWidth(),
                                            horizontalArrangement = Arrangement.SpaceBetween,
                                            verticalAlignment = Alignment.CenterVertically
                                        ) {
                                            Text(
                                                text = "Pinned Sets",
                                                style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                                                color = MaterialTheme.colorScheme.onSurface
                                            )
                                            TextButton(onClick = { pinnedSetCodes = emptySet() }) {
                                                Text("Clear all")
                                            }
                                        }

                                        pinnedSetCodes.take(3).forEach { setCode ->
                                            Surface(
                                                modifier = Modifier
                                                    .fillMaxWidth()
                                                    .clickable { navController.navigate("history") },
                                                shape = RoundedCornerShape(16.dp),
                                                color = MaterialTheme.colorScheme.secondaryContainer.copy(alpha = 0.65f)
                                            ) {
                                                Row(
                                                    modifier = Modifier
                                                        .fillMaxWidth()
                                                        .padding(horizontal = 16.dp, vertical = 12.dp),
                                                    horizontalArrangement = Arrangement.SpaceBetween,
                                                    verticalAlignment = Alignment.CenterVertically
                                                ) {
                                                    Column(verticalArrangement = Arrangement.spacedBy(2.dp)) {
                                                        Text(
                                                            text = setCode,
                                                            style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.Bold),
                                                            color = MaterialTheme.colorScheme.onSecondaryContainer
                                                        )
                                                        Text(
                                                            text = "Open in history",
                                                            style = MaterialTheme.typography.bodySmall,
                                                            color = MaterialTheme.colorScheme.onSecondaryContainer.copy(alpha = 0.75f)
                                                        )
                                                    }
                                                    TextButton(
                                                        onClick = { pinnedSetCodes = PinnedSetsStore.toggle(context, setCode) }
                                                    ) {
                                                        Text("Unpin")
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }

                                if (history.isNotEmpty()) {
                                    Text(
                                        text = "${history.size} scans stored locally",
                                        style = MaterialTheme.typography.bodySmall,
                                        color = MaterialTheme.colorScheme.onSurfaceVariant
                                    )
                                }
                            }
                        }
                    }
                    OperationsGridCard(
                        tabletColumns = if (rootIsExpanded) 3 else 2,
                        borrowScale = borrowScale,
                        borrowInteractionSource = borrowInteractionSource,
                        toolsScale = toolsScale,
                        toolsInteractionSource = toolsInteractionSource,
                        reportsScale = reportsScale,
                        reportsInteractionSource = reportsInteractionSource,
                        callMonitoringScale = callMonitoringScale,
                        callMonitoringInteractionSource = callMonitoringInteractionSource,
                        repairPhotosScale = repairPhotosScale,
                        repairPhotosInteractionSource = repairPhotosInteractionSource,
                        onBorrowClick = { navController.navigate("borrow_home") },
                        onToolsClick = { navController.navigate("serial_scan_home") },
                        onReportsClick = { navController.navigate("reports") },
                        onCallMonitoringClick = { navController.navigate("call_dashboard") },
                        onRepairPortalClick = { navController.navigate("repair_portal") },
                        onRepairPhotosClick = { navController.navigate("repair_photo_upload_entry") }
                    )

                    Spacer(modifier = Modifier.height(24.dp))
                } else {
                HomeCommandHero(
                    greetingName = greetingName,
                    lastUpdatedText = lastUpdatedText,
                    connectionStatus = connectionStatus,
                    dotAlpha = dotAlpha
                )

                if (connectionStatus != ConnectionStatus.Connected) {
                    if (connectionStatus == ConnectionStatus.Error && connectionErrorUi != null) {
                        ApiFailurePanel(
                            message = connectionErrorUi!!.first,
                            details = connectionErrorUi!!.second,
                            retryLabel = "Retry",
                            onRetry = { scope.launch { refreshDashboard() } }
                        )
                    } else {
                        ConnectionNoticeCard()
                    }
                }

                ScannerCommandCard(
                    scale = scannerScale,
                    interactionSource = scannerInteractionSource,
                    localScanCount = history.size,
                    pendingIssueCount = issueCount,
                    onClick = { navController.navigate("home") }
                )

                OperationsGridCard(
                    borrowScale = borrowScale,
                    borrowInteractionSource = borrowInteractionSource,
                    toolsScale = toolsScale,
                    toolsInteractionSource = toolsInteractionSource,
                    reportsScale = reportsScale,
                    reportsInteractionSource = reportsInteractionSource,
                    callMonitoringScale = callMonitoringScale,
                    callMonitoringInteractionSource = callMonitoringInteractionSource,
                    repairPhotosScale = repairPhotosScale,
                    repairPhotosInteractionSource = repairPhotosInteractionSource,
                    onBorrowClick = { navController.navigate("borrow_home") },
                    onToolsClick = { navController.navigate("serial_scan_home") },
                    onReportsClick = { navController.navigate("reports") },
                    onCallMonitoringClick = { navController.navigate("call_dashboard") },
                    onRepairPortalClick = { navController.navigate("repair_portal") },
                    onRepairPhotosClick = { navController.navigate("repair_photo_upload_entry") }
                )

                DashboardOverviewCard(
                    processedCount = processedCount,
                    pendingIssueCount = issueCount,
                    dispatchCount = dispatchCount,
                    openBorrowCount = openBorrowCount,
                    onProcessedClick = { navController.navigate("processed_updates") },
                    onPendingClick = { navController.navigate("pending_updates") },
                    onReportsClick = { navController.navigate("reports") }
                )

                if (pinnedSetCodes.isNotEmpty()) {
                    Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.SpaceBetween,
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Text(
                                text = "Pinned Sets",
                                style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                                color = MaterialTheme.colorScheme.onSurface
                            )
                            TextButton(onClick = { pinnedSetCodes = emptySet() }) {
                                Text("Clear all")
                            }
                        }

                        pinnedSetCodes.take(3).forEach { setCode ->
                            Surface(
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .clickable { navController.navigate("history") },
                                shape = RoundedCornerShape(16.dp),
                                color = MaterialTheme.colorScheme.secondaryContainer.copy(alpha = 0.65f)
                            ) {
                                Row(
                                    modifier = Modifier
                                        .fillMaxWidth()
                                        .padding(horizontal = 16.dp, vertical = 12.dp),
                                    horizontalArrangement = Arrangement.SpaceBetween,
                                    verticalAlignment = Alignment.CenterVertically
                                ) {
                                    Column(verticalArrangement = Arrangement.spacedBy(2.dp)) {
                                        Text(
                                            text = setCode,
                                            style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.Bold),
                                            color = MaterialTheme.colorScheme.onSecondaryContainer
                                        )
                                        Text(
                                            text = "Open in history",
                                            style = MaterialTheme.typography.bodySmall,
                                            color = MaterialTheme.colorScheme.onSecondaryContainer.copy(alpha = 0.75f)
                                        )
                                    }
                                    TextButton(
                                        onClick = { pinnedSetCodes = PinnedSetsStore.toggle(context, setCode) }
                                    ) {
                                        Text("Unpin")
                                    }
                                }
                            }
                        }
                    }
                }

                if (history.isNotEmpty()) {
                    Text(
                        text = "${history.size} scans stored locally",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }

                Spacer(modifier = Modifier.height(24.dp))
                }
            }
        }
    }
}

@Composable
private fun HomeCommandHero(
    greetingName: String,
    lastUpdatedText: String?,
    connectionStatus: ConnectionStatus,
    dotAlpha: Float
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(26.dp),
        colors = CardDefaults.cardColors(containerColor = Color.White.copy(alpha = 0.16f)),
        border = androidx.compose.foundation.BorderStroke(1.dp, Color.White.copy(alpha = 0.22f)),
        elevation = CardDefaults.cardElevation(defaultElevation = 0.dp)
    ) {
        Column(modifier = Modifier.padding(18.dp), verticalArrangement = Arrangement.spacedBy(14.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Column(modifier = Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                    Text("Operations Center", style = MaterialTheme.typography.headlineSmall.copy(fontWeight = FontWeight.ExtraBold), color = Color.White)
                    Text("Hello, $greetingName", style = MaterialTheme.typography.bodyLarge, color = Color.White.copy(alpha = 0.88f))
                }
                Surface(shape = RoundedCornerShape(999.dp), color = Color.White.copy(alpha = 0.16f)) {
                    Row(modifier = Modifier.padding(horizontal = 10.dp, vertical = 6.dp), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                        Box(modifier = Modifier.size(8.dp).background(if (connectionStatus == ConnectionStatus.Connected) Color.Green.copy(alpha = dotAlpha) else MaterialTheme.colorScheme.errorContainer, CircleShape))
                        Text(if (connectionStatus == ConnectionStatus.Connected) "Online" else "Checking", style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.Bold), color = Color.White)
                    }
                }
            }
            Text(
                text = "Scan dispatches, manage serial tools, monitor borrows, and review reports from one dashboard.",
                style = MaterialTheme.typography.bodyMedium,
                color = Color.White.copy(alpha = 0.82f)
            )
            if (!lastUpdatedText.isNullOrBlank()) {
                Surface(color = Color.White.copy(alpha = 0.15f), shape = RoundedCornerShape(999.dp)) {
                    Text("Updated $lastUpdatedText", style = MaterialTheme.typography.labelMedium, color = Color.White, modifier = Modifier.padding(horizontal = 10.dp, vertical = 5.dp))
                }
            }
        }
    }
}

@Composable
private fun ConnectionNoticeCard() {
    Surface(modifier = Modifier.fillMaxWidth(), shape = RoundedCornerShape(16.dp), color = MaterialTheme.colorScheme.surfaceVariant) {
        Row(modifier = Modifier.padding(horizontal = 14.dp, vertical = 12.dp), horizontalArrangement = Arrangement.spacedBy(8.dp), verticalAlignment = Alignment.CenterVertically) {
            Icon(imageVector = Icons.Filled.Info, contentDescription = null, tint = MaterialTheme.colorScheme.onSurfaceVariant)
            Text("Connecting to system...", style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }
}

@Composable
private fun ScannerCommandCard(
    modifier: Modifier = Modifier,
    contentFill: Boolean = false,
    scale: Float,
    interactionSource: MutableInteractionSource,
    localScanCount: Int,
    pendingIssueCount: Int,
    onClick: () -> Unit
) {
    Card(
        modifier = modifier.fillMaxWidth().scale(scale).clickable(interactionSource = interactionSource, indication = ripple(), onClick = onClick),
        shape = RoundedCornerShape(24.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        elevation = CardDefaults.cardElevation(defaultElevation = 5.dp),
        border = androidx.compose.foundation.BorderStroke(1.dp, MaterialTheme.colorScheme.primary.copy(alpha = 0.24f))
    ) {
        Column(
            modifier = Modifier
                .padding(if (contentFill) 24.dp else 18.dp)
                .then(if (contentFill) Modifier.fillMaxHeight() else Modifier),
            verticalArrangement = Arrangement.spacedBy(if (contentFill) 20.dp else 16.dp)
        ) {
            Row(
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(if (contentFill) 18.dp else 14.dp)
            ) {
                Box(
                    modifier = Modifier
                        .size(if (contentFill) 72.dp else 60.dp)
                        .clip(CircleShape)
                        .background(MaterialTheme.colorScheme.primaryContainer),
                    contentAlignment = Alignment.Center
                ) {
                    Icon(
                        imageVector = Icons.Filled.QrCodeScanner,
                        contentDescription = null,
                        tint = MaterialTheme.colorScheme.primary,
                        modifier = Modifier.size(if (contentFill) 36.dp else 30.dp)
                    )
                }
                Column(
                    modifier = Modifier.weight(1f),
                    verticalArrangement = Arrangement.spacedBy(if (contentFill) 5.dp else 3.dp)
                ) {
                    Text(
                        "Start Dispatch Scan",
                        style = (if (contentFill) MaterialTheme.typography.headlineSmall else MaterialTheme.typography.titleLarge)
                            .copy(fontWeight = FontWeight.ExtraBold),
                        color = MaterialTheme.colorScheme.onSurface
                    )
                    Text(
                        "Open hardware/camera scanning, recent sets, and dispatch issue handling.",
                        style = if (contentFill) MaterialTheme.typography.bodyMedium else MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
                Icon(
                    imageVector = Icons.AutoMirrored.Filled.ArrowForward,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.size(if (contentFill) 28.dp else 20.dp)
                )
            }
            Row(
                horizontalArrangement = Arrangement.spacedBy(if (contentFill) 14.dp else 10.dp),
                modifier = Modifier
                    .fillMaxWidth()
                    .then(if (contentFill) Modifier.weight(1f) else Modifier)
            ) {
                CommandStatChip(
                    label = "Local scans",
                    value = localScanCount.toString(),
                    modifier = Modifier.weight(1f).then(if (contentFill) Modifier.fillMaxHeight() else Modifier),
                    contentFill = contentFill
                )
                CommandStatChip(
                    label = "Pending issues",
                    value = pendingIssueCount.toString(),
                    modifier = Modifier.weight(1f).then(if (contentFill) Modifier.fillMaxHeight() else Modifier),
                    contentFill = contentFill
                )
            }
        }
    }
}
@Composable
private fun CommandStatChip(
    label: String,
    value: String,
    modifier: Modifier = Modifier,
    contentFill: Boolean = false
) {
    Surface(
        modifier = modifier,
        shape = RoundedCornerShape(14.dp),
        color = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.62f)
    ) {
        Column(
            modifier = Modifier
                .padding(
                    horizontal = if (contentFill) 18.dp else 12.dp,
                    vertical = if (contentFill) 16.dp else 9.dp
                )
                .then(if (contentFill) Modifier.fillMaxHeight() else Modifier),
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = if (contentFill) Arrangement.Center else Arrangement.Top
        ) {
            Text(
                value,
                style = (if (contentFill) MaterialTheme.typography.headlineSmall else MaterialTheme.typography.titleMedium)
                    .copy(fontWeight = FontWeight.Bold)
            )
            Text(
                label,
                style = if (contentFill) MaterialTheme.typography.bodyMedium else MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
        }
    }
}
@Composable
private fun OperationsGridCard(
    borrowScale: Float,
    borrowInteractionSource: MutableInteractionSource,
    toolsScale: Float,
    toolsInteractionSource: MutableInteractionSource,
    reportsScale: Float,
    reportsInteractionSource: MutableInteractionSource,
    callMonitoringScale: Float,
    callMonitoringInteractionSource: MutableInteractionSource,
    repairPhotosScale: Float,
    repairPhotosInteractionSource: MutableInteractionSource,
    onBorrowClick: () -> Unit,
    onToolsClick: () -> Unit,
    onReportsClick: () -> Unit,
    onCallMonitoringClick: () -> Unit,
    onRepairPortalClick: () -> Unit,
    onRepairPhotosClick: () -> Unit,
    tabletColumns: Int = 2
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(22.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        elevation = CardDefaults.cardElevation(defaultElevation = 2.dp),
        border = androidx.compose.foundation.BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.38f))
    ) {
        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Column(modifier = Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(2.dp)) {
                    Text("Work Modules", style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold))
                    Text("Jump directly into the next operational task.", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
                Icon(Icons.Filled.Assessment, contentDescription = null, tint = MaterialTheme.colorScheme.primary)
            }
            if (tabletColumns >= 3) {
                Row(horizontalArrangement = Arrangement.spacedBy(10.dp), modifier = Modifier.fillMaxWidth()) {
                    OperationTile(Modifier.weight(1f), "Borrow", "Assets in/out", Icons.Filled.PlaylistAdd, borrowScale, borrowInteractionSource, onBorrowClick)
                    OperationTile(Modifier.weight(1f), "Serial Tools", "Reports + lookup", Icons.Filled.BarChart, toolsScale, toolsInteractionSource, onToolsClick)
                    OperationTile(Modifier.weight(1f), "Reports", "Trends + counts", Icons.Filled.Assessment, reportsScale, reportsInteractionSource, onReportsClick)
                }
                Row(horizontalArrangement = Arrangement.spacedBy(10.dp), modifier = Modifier.fillMaxWidth()) {
                    OperationTile(Modifier.weight(1f), "IT Calls", "Tickets + support", Icons.Filled.HeadsetMic, callMonitoringScale, callMonitoringInteractionSource, onCallMonitoringClick)
                    OperationTile(Modifier.weight(1f), "Repair Portal", "Tickets + technician", Icons.Filled.PhotoCamera, repairPhotosScale, repairPhotosInteractionSource, onRepairPortalClick)
                    OperationTile(Modifier.weight(1f), "Repair Photos", "Upload evidence", Icons.Filled.PhotoCamera, repairPhotosScale, repairPhotosInteractionSource, onRepairPhotosClick)
                }
            } else {
                Row(horizontalArrangement = Arrangement.spacedBy(10.dp), modifier = Modifier.fillMaxWidth()) {
                    OperationTile(Modifier.weight(1f), "Borrow", "Assets in/out", Icons.Filled.PlaylistAdd, borrowScale, borrowInteractionSource, onBorrowClick)
                    OperationTile(Modifier.weight(1f), "Serial Tools", "Reports + lookup", Icons.Filled.BarChart, toolsScale, toolsInteractionSource, onToolsClick)
                }
                Row(horizontalArrangement = Arrangement.spacedBy(10.dp), modifier = Modifier.fillMaxWidth()) {
                    OperationTile(Modifier.weight(1f), "Reports", "Trends + counts", Icons.Filled.Assessment, reportsScale, reportsInteractionSource, onReportsClick)
                    OperationTile(Modifier.weight(1f), "IT Calls", "Tickets + support", Icons.Filled.HeadsetMic, callMonitoringScale, callMonitoringInteractionSource, onCallMonitoringClick)
                }
                Row(horizontalArrangement = Arrangement.spacedBy(10.dp), modifier = Modifier.fillMaxWidth()) {
                    OperationTile(Modifier.weight(1f), "Repair Portal", "Tickets + technician", Icons.Filled.PhotoCamera, repairPhotosScale, repairPhotosInteractionSource, onRepairPortalClick)
                    OperationTile(Modifier.weight(1f), "Repair Photos", "Upload evidence", Icons.Filled.PhotoCamera, repairPhotosScale, repairPhotosInteractionSource, onRepairPhotosClick)
                }
            }
        }
    }
}

@Composable
private fun OperationTile(
    modifier: Modifier = Modifier,
    title: String,
    subtitle: String,
    icon: ImageVector,
    scale: Float,
    interactionSource: MutableInteractionSource,
    onClick: () -> Unit
) {
    Surface(
        modifier = modifier.scale(scale).clickable(interactionSource = interactionSource, indication = ripple(), onClick = onClick),
        shape = RoundedCornerShape(16.dp),
        color = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.55f),
        border = androidx.compose.foundation.BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.4f))
    ) {
        Column(modifier = Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(9.dp)) {
            Box(modifier = Modifier.size(40.dp).clip(CircleShape).background(MaterialTheme.colorScheme.primaryContainer), contentAlignment = Alignment.Center) {
                Icon(icon, contentDescription = null, tint = MaterialTheme.colorScheme.primary, modifier = Modifier.size(22.dp))
            }
            Text(title, style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.Bold))
            Text(subtitle, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }
}
@Composable
private fun DashboardOverviewCard(
    modifier: Modifier = Modifier,
    processedCount: Int,
    pendingIssueCount: Int,
    dispatchCount: Int?,
    openBorrowCount: Int?,
    onProcessedClick: () -> Unit,
    onPendingClick: () -> Unit,
    onReportsClick: () -> Unit
) {
    Card(
        modifier = modifier.fillMaxWidth(),
        shape = RoundedCornerShape(22.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface.copy(alpha = 0.96f)),
        elevation = CardDefaults.cardElevation(defaultElevation = 3.dp),
        border = androidx.compose.foundation.BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.38f))
    ) {
        Column(
            modifier = Modifier.padding(18.dp),
            verticalArrangement = Arrangement.spacedBy(14.dp)
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Column(verticalArrangement = Arrangement.spacedBy(2.dp), modifier = Modifier.weight(1f)) {
                    Text(
                        text = "Today Dashboard",
                        style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                        color = MaterialTheme.colorScheme.onSurface
                    )
                    Text(
                        text = "Dispatch, issues, and borrow activity at a glance.",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
                Surface(
                    shape = RoundedCornerShape(999.dp),
                    color = MaterialTheme.colorScheme.primaryContainer
                ) {
                    Text(
                        text = "Live",
                        modifier = Modifier.padding(horizontal = 10.dp, vertical = 5.dp),
                        style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.Bold),
                        color = MaterialTheme.colorScheme.onPrimaryContainer
                    )
                }
            }

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(10.dp)
            ) {
                QuickMetricCard(
                    modifier = Modifier.weight(1f),
                    label = "Processed",
                    value = processedCount.toString(),
                    containerColor = MaterialTheme.colorScheme.primaryContainer,
                    contentColor = MaterialTheme.colorScheme.onPrimaryContainer,
                    onClick = onProcessedClick
                )
                QuickMetricCard(
                    modifier = Modifier.weight(1f),
                    label = "Pending",
                    value = pendingIssueCount.toString(),
                    containerColor = MaterialTheme.colorScheme.errorContainer,
                    contentColor = MaterialTheme.colorScheme.onErrorContainer,
                    onClick = onPendingClick
                )
            }

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(10.dp)
            ) {
                QuickMetricCard(
                    modifier = Modifier.weight(1f),
                    label = "Dispatch",
                    value = dispatchCount?.toString() ?: "--",
                    containerColor = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.66f),
                    contentColor = MaterialTheme.colorScheme.onSurface,
                    onClick = onReportsClick
                )
                QuickMetricCard(
                    modifier = Modifier.weight(1f),
                    label = "Borrows",
                    value = openBorrowCount?.toString() ?: "--",
                    containerColor = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.66f),
                    contentColor = MaterialTheme.colorScheme.onSurface,
                    onClick = onReportsClick
                )
            }
        }
    }
}

@Composable
private fun PrimaryModuleCard(
    title: String,
    description: String,
    icon: ImageVector,
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
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface.copy(alpha = 0.95f)),
        elevation = CardDefaults.cardElevation(defaultElevation = 6.dp),
        border = androidx.compose.foundation.BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.5f))
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
                    .size(58.dp)
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
            Icon(
                imageVector = Icons.AutoMirrored.Filled.ArrowForward,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.6f)
            )
        }
    }
}

@Composable
private fun SecondaryModuleCard(
    modifier: Modifier = Modifier,
    title: String,
    description: String,
    icon: ImageVector,
    scale: Float,
    interactionSource: MutableInteractionSource,
    onClick: () -> Unit
) {
    Card(
        modifier = modifier
            .scale(scale)
            .clickable(
                interactionSource = interactionSource,
                indication = ripple(),
                onClick = onClick
            ),
        shape = RoundedCornerShape(20.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface.copy(alpha = 0.92f)),
        elevation = CardDefaults.cardElevation(defaultElevation = 3.dp),
        border = androidx.compose.foundation.BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.5f))
    ) {
        Column(
            modifier = Modifier.padding(18.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            Box(
                modifier = Modifier
                    .size(44.dp)
                    .clip(CircleShape)
                    .background(MaterialTheme.colorScheme.primaryContainer),
                contentAlignment = Alignment.Center
            ) {
                Icon(
                    imageVector = icon,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.primary
                )
            }
            Text(
                text = title,
                style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.Bold),
                color = MaterialTheme.colorScheme.onSurface
            )
            Text(
                text = description,
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
        }
    }
}

@Composable
private fun QuickMetricCard(
    modifier: Modifier = Modifier,
    label: String,
    value: String,
    containerColor: Color,
    contentColor: Color,
    onClick: () -> Unit
) {
    Surface(
        modifier = modifier.clickable(onClick = onClick),
        shape = RoundedCornerShape(18.dp),
        color = containerColor
    ) {
        Column(
            modifier = Modifier.padding(horizontal = 16.dp, vertical = 14.dp),
            verticalArrangement = Arrangement.spacedBy(4.dp)
        ) {
            Text(
                text = value,
                style = MaterialTheme.typography.headlineMedium.copy(fontWeight = FontWeight.Bold),
                color = contentColor
            )
            Text(
                text = label,
                style = MaterialTheme.typography.bodyMedium,
                color = contentColor.copy(alpha = 0.82f)
            )
        }
    }
}



