package com.example.yakultscanner.ui.screens

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.expandVertically
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.shrinkVertically
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
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.automirrored.filled.ArrowForward
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.Error
import androidx.compose.material.icons.filled.FindInPage
import androidx.compose.material.icons.filled.Inventory2
import androidx.compose.material.icons.filled.KeyboardArrowUp
import androidx.compose.material.icons.filled.QrCodeScanner
import androidx.compose.material.icons.filled.Search
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
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
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.navigation.NavController
import androidx.navigation.compose.currentBackStackEntryAsState
import com.example.yakultscanner.SCAN_RESULT_SERIAL_KEY
import com.example.yakultscanner.SCAN_RETURN_ROUTE_KEY
import com.example.yakultscanner.SCAN_RETURN_SERIAL_ROUTE
import com.example.yakultscanner.api.ApiClient
import com.example.yakultscanner.api.ApiResult
import com.example.yakultscanner.api.SCANNER_GENERIC_MESSAGE
import com.example.yakultscanner.api.SCANNER_NETWORK_MESSAGE
import androidx.compose.material.icons.filled.History
import com.example.yakultscanner.api.DeploymentHistoryDto
import com.example.yakultscanner.api.ItemMovementEntryDto
import com.example.yakultscanner.api.ItemMovementResponse
import com.example.yakultscanner.api.SerialLookupItemDto
import com.example.yakultscanner.api.SerialLookupSetDto
import com.example.yakultscanner.api.safeApiCall
import kotlinx.coroutines.launch

// ─── State ────────────────────────────────────────────────────────────────────

private sealed class LookupState {
    object Idle : LookupState()
    object Loading : LookupState()
    object NotFound : LookupState()
    data class Error(val message: String) : LookupState()
    data class FoundNoSet(val item: SerialLookupItemDto) : LookupState()
    data class FoundInSet(val item: SerialLookupItemDto, val sets: List<SerialLookupSetDto>) : LookupState()
}

// ─── Screen ───────────────────────────────────────────────────────────────────

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun SerialSetIdentifierScreen(navController: NavController) {
    var serialInput by remember { mutableStateOf("") }
    var lookupState by remember { mutableStateOf<LookupState>(LookupState.Idle) }
    val scope = rememberCoroutineScope()
    val scrollState = rememberScrollState()

    val headerBrush = Brush.verticalGradient(
        colors = listOf(
            MaterialTheme.colorScheme.primary,
            Color(0xFFB71C1C)
        )
    )

    fun performLookup(rawIdentifier: String = serialInput) {
        val identifier = rawIdentifier.trim()
        if (identifier.isBlank()) return
        scope.launch {
            lookupState = LookupState.Loading
            when (val result = safeApiCall { ApiClient.service.serialLookup(identifier) }) {
                is ApiResult.Success -> {
                    val resp = result.data
                    lookupState = when {
                        !resp.found -> LookupState.NotFound
                        !resp.isInSet -> LookupState.FoundNoSet(resp.item!!)
                        else -> LookupState.FoundInSet(resp.item!!, resp.sets)
                    }
                }
                is ApiResult.HttpError -> lookupState = LookupState.Error(result.message ?: SCANNER_GENERIC_MESSAGE)
                is ApiResult.NetworkError -> lookupState = LookupState.Error(result.message ?: SCANNER_NETWORK_MESSAGE)
                is ApiResult.UnknownError -> lookupState = LookupState.Error(result.message ?: SCANNER_GENERIC_MESSAGE)
            }
        }
    }

    val navBackStackEntry by navController.currentBackStackEntryAsState()
    val scannedSerialFlow = remember(navBackStackEntry) {
        navBackStackEntry?.savedStateHandle?.getStateFlow<String?>(SCAN_RESULT_SERIAL_KEY, null)
    }
    val scannedSerial by (scannedSerialFlow?.collectAsState(initial = null) ?: remember { mutableStateOf<String?>(null) })

    LaunchedEffect(scannedSerial) {
        val scanned = scannedSerial?.trim().orEmpty()
        if (scanned.isBlank()) return@LaunchedEffect
        navBackStackEntry?.savedStateHandle?.remove<String>(SCAN_RESULT_SERIAL_KEY)
        serialInput = scanned
        performLookup(scanned)
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Text(
                        "Serial Set Identifier",
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
                .background(MaterialTheme.colorScheme.background)
        ) {
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .height(260.dp)
                    .background(headerBrush)
            )

            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(paddingValues)
                    .verticalScroll(scrollState)
                    .padding(horizontal = 24.dp, vertical = 16.dp),
                verticalArrangement = Arrangement.spacedBy(20.dp)
            ) {
                Text(
                    text = "Identify Set",
                    style = MaterialTheme.typography.headlineSmall.copy(fontWeight = FontWeight.Bold),
                    color = MaterialTheme.colorScheme.onPrimary
                )
                Text(
                    text = "Scan or enter a serial number to check if the item is registered in inventory and linked to a deployment set.",
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onPrimary.copy(alpha = 0.85f)
                )

                Spacer(modifier = Modifier.height(2.dp))

                // ── Input card ────────────────────────────────────────────────
                Card(
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(24.dp),
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
                    elevation = CardDefaults.cardElevation(defaultElevation = 0.dp),
                    border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.3f))
                ) {
                    Column(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(20.dp),
                        verticalArrangement = Arrangement.spacedBy(12.dp)
                    ) {
                        Text(
                            text = "Serial Number",
                            style = MaterialTheme.typography.labelMedium.copy(fontWeight = FontWeight.Bold),
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                        OutlinedTextField(
                            value = serialInput,
                            onValueChange = { v ->
                                serialInput = v
                                if (v.isBlank()) lookupState = LookupState.Idle
                            },
                            placeholder = {
                                Text(
                                    "e.g. SN-2024-00123",
                                    color = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.45f)
                                )
                            },
                            modifier = Modifier.fillMaxWidth(),
                            singleLine = true,
                            shape = RoundedCornerShape(14.dp),
                            trailingIcon = {
                                if (serialInput.isNotBlank()) {
                                    IconButton(onClick = {
                                        serialInput = ""
                                        lookupState = LookupState.Idle
                                    }) {
                                        Icon(Icons.Filled.Close, contentDescription = "Clear input")
                                    }
                                }
                            }
                        )
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.spacedBy(10.dp)
                        ) {
                            OutlinedButton(
                                onClick = {
                                    navController.currentBackStackEntry?.savedStateHandle?.set(SCAN_RETURN_ROUTE_KEY, SCAN_RETURN_SERIAL_ROUTE)
                                    navController.navigate("scanner_camera")
                                },
                                modifier = Modifier.weight(1f),
                                shape = RoundedCornerShape(12.dp)
                            ) {
                                Icon(
                                    Icons.Filled.QrCodeScanner,
                                    contentDescription = null,
                                    modifier = Modifier.size(18.dp)
                                )
                                Spacer(Modifier.width(6.dp))
                                Text("Scan")
                            }
                            Button(
                                onClick = { performLookup() },
                                modifier = Modifier.weight(1f),
                                shape = RoundedCornerShape(12.dp),
                                enabled = serialInput.isNotBlank() && lookupState !is LookupState.Loading
                            ) {
                                Icon(
                                    Icons.Filled.Search,
                                    contentDescription = null,
                                    modifier = Modifier.size(18.dp)
                                )
                                Spacer(Modifier.width(6.dp))
                                Text("Look Up")
                            }
                        }
                    }
                }

                // ── Result area ───────────────────────────────────────────────
                when (val state = lookupState) {
                    is LookupState.Idle    -> IdleHintCard()
                    is LookupState.Loading -> LoadingCard()
                    is LookupState.NotFound -> NotFoundCard(serial = serialInput)
                    is LookupState.Error -> ErrorCard(message = state.message)
                    is LookupState.FoundNoSet -> FoundNoSetCard(item = state.item)
                    is LookupState.FoundInSet -> FoundInSetCard(
                        item = state.item,
                        sets = state.sets,
                        onViewDetails = { setCode -> navController.navigate("details_readonly/$setCode") }
                    )
                }

                Spacer(Modifier.height(80.dp))
            }
        }
    }
}

// ─── Idle hint ────────────────────────────────────────────────────────────────

@Composable
private fun IdleHintCard() {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(20.dp),
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.45f)
        ),
        elevation = CardDefaults.cardElevation(0.dp)
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
                    .size(48.dp)
                    .clip(CircleShape)
                    .background(MaterialTheme.colorScheme.primary.copy(alpha = 0.1f)),
                contentAlignment = Alignment.Center
            ) {
                Icon(
                    Icons.Filled.FindInPage,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.primary,
                    modifier = Modifier.size(24.dp)
                )
            }
            Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
                Text(
                    text = "Ready to identify",
                    style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.Bold),
                    color = MaterialTheme.colorScheme.onSurface
                )
                Text(
                    text = "Enter or scan a serial number above to check inventory registration and set assignment.",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
        }
    }
}

// ─── Loading ──────────────────────────────────────────────────────────────────

@Composable
private fun LoadingCard() {
    Box(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = 36.dp),
        contentAlignment = Alignment.Center
    ) {
        Column(
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.spacedBy(14.dp)
        ) {
            CircularProgressIndicator(
                modifier = Modifier.size(40.dp),
                color = MaterialTheme.colorScheme.primary
            )
            Text(
                text = "Checking inventory...",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
        }
    }
}

// ─── Not found ────────────────────────────────────────────────────────────────

@Composable
private fun NotFoundCard(serial: String) {
    val tone = Color(0xFFC62828)
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(20.dp),
        colors = CardDefaults.cardColors(containerColor = tone.copy(alpha = 0.07f)),
        elevation = CardDefaults.cardElevation(0.dp),
        border = BorderStroke(1.dp, tone.copy(alpha = 0.25f))
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
                    .size(48.dp)
                    .clip(CircleShape)
                    .background(tone.copy(alpha = 0.12f)),
                contentAlignment = Alignment.Center
            ) {
                Icon(Icons.Filled.Error, contentDescription = null, tint = tone, modifier = Modifier.size(26.dp))
            }
            Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
                Text(
                    text = "Not found in inventory",
                    style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.Bold),
                    color = tone
                )
                Text(
                    text = "\"${serial.trim()}\" is not registered in the inventory database.",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
        }
    }
}

// ─── Error ────────────────────────────────────────────────────────────────────

@Composable
private fun ErrorCard(message: String) {
    val tone = Color(0xFFC62828)
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(20.dp),
        colors = CardDefaults.cardColors(containerColor = tone.copy(alpha = 0.07f)),
        elevation = CardDefaults.cardElevation(0.dp),
        border = BorderStroke(1.dp, tone.copy(alpha = 0.25f))
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
                    .size(48.dp)
                    .clip(CircleShape)
                    .background(tone.copy(alpha = 0.12f)),
                contentAlignment = Alignment.Center
            ) {
                Icon(Icons.Filled.Error, contentDescription = null, tint = tone, modifier = Modifier.size(26.dp))
            }
            Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
                Text(
                    text = "Request failed",
                    style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.Bold),
                    color = tone
                )
                Text(
                    text = message,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
        }
    }
}

// ─── Found, no set ────────────────────────────────────────────────────────────

@Composable
private fun FoundNoSetCard(item: SerialLookupItemDto) {
    val tone = Color(0xFFE65100)
    Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
        ItemDetailCard(item = item)
        Card(
            modifier = Modifier.fillMaxWidth(),
            shape = RoundedCornerShape(20.dp),
            colors = CardDefaults.cardColors(containerColor = tone.copy(alpha = 0.07f)),
            elevation = CardDefaults.cardElevation(0.dp),
            border = BorderStroke(1.dp, tone.copy(alpha = 0.25f))
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
                        .size(48.dp)
                        .clip(CircleShape)
                        .background(tone.copy(alpha = 0.12f)),
                    contentAlignment = Alignment.Center
                ) {
                    Icon(Icons.Filled.Warning, contentDescription = null, tint = tone, modifier = Modifier.size(24.dp))
                }
                Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
                    Text(
                        text = "Not assigned to any set",
                        style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.Bold),
                        color = tone
                    )
                    Text(
                        text = "This item is registered in inventory but has not been assigned to a deployment set.",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
            }
        }
        ItemMovementSection(itemId = item.itemId)
    }
}

// ─── Found in set ─────────────────────────────────────────────────────────────

@Composable
private fun FoundInSetCard(item: SerialLookupItemDto, sets: List<SerialLookupSetDto>, onViewDetails: (String) -> Unit) {
    Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
        ItemDetailCard(item = item)
        sets.forEachIndexed { index, set ->
            SetResultCard(set = set, isLatest = index == 0, onViewDetails = onViewDetails)
        }
    }
}

// ─── Item detail card (green — item found) ────────────────────────────────────

@Composable
private fun ItemDetailCard(item: SerialLookupItemDto) {
    val tone = Color(0xFF2E7D32)
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(20.dp),
        colors = CardDefaults.cardColors(containerColor = tone.copy(alpha = 0.07f)),
        elevation = CardDefaults.cardElevation(0.dp),
        border = BorderStroke(1.dp, tone.copy(alpha = 0.2f))
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(20.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            Row(
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                Box(
                    modifier = Modifier
                        .size(44.dp)
                        .clip(CircleShape)
                        .background(tone.copy(alpha = 0.12f)),
                    contentAlignment = Alignment.Center
                ) {
                    Icon(
                        Icons.Filled.CheckCircle,
                        contentDescription = null,
                        tint = tone,
                        modifier = Modifier.size(22.dp)
                    )
                }
                Column(verticalArrangement = Arrangement.spacedBy(2.dp)) {
                    Text(
                        text = "Item Found in Inventory",
                        style = MaterialTheme.typography.labelMedium.copy(fontWeight = FontWeight.Bold),
                        color = tone
                    )
                    Text(
                        text = item.serialNumber ?: "",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
            }
            HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.35f))
            Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(16.dp)) {
                LabeledValue(label = "Name", value = item.name ?: "", modifier = Modifier.weight(1f))
                LabeledValue(label = "Model", value = item.modelNumber ?: "", modifier = Modifier.weight(1f))
            }
            Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(16.dp)) {
                LabeledValue(label = "Type", value = item.itemType ?: "", modifier = Modifier.weight(1f))
                LabeledValue(label = "Category", value = item.category ?: "", modifier = Modifier.weight(1f))
            }
            if (!item.cellPhoneNumber.isNullOrBlank() || !item.imei1.isNullOrBlank() || !item.imei2.isNullOrBlank()) {
                Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(16.dp)) {
                    LabeledValue(label = "Cellphone", value = item.cellPhoneNumber ?: "", modifier = Modifier.weight(1f))
                    LabeledValue(label = "IMEI 1", value = item.imei1 ?: "", modifier = Modifier.weight(1f))
                }
                if (!item.imei2.isNullOrBlank()) {
                    LabeledValue(label = "IMEI 2", value = item.imei2)
                }
            }
            LabeledValue(label = "Condition", value = item.condition ?: "")
            WarrantyCompactRow(item = item)
        }
    }
}

@Composable
private fun WarrantyCompactRow(item: SerialLookupItemDto) {
    val status = item.warrantyStatus?.trim()?.lowercase()
    val (label, tone) = when (status) {
        "good" -> "Warranty Good" to Color(0xFF2E7D32)
        "expiringsoon" -> "Expiring Soon" to Color(0xFFE65100)
        "expired" -> "Expired" to Color(0xFFC62828)
        else -> "Not Encoded" to MaterialTheme.colorScheme.onSurfaceVariant
    }

    val start = formatWarrantyDate(item.warrantyStartDate)
    val end = formatWarrantyDate(item.warrantyEndDate)
    val period = if (status == "good" || status == "expiringsoon" || status == "expired") {
        if (start != null && end != null) "$start – $end" else "Not encoded"
    } else {
        "Not encoded"
    }

    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.spacedBy(10.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Text(
            text = "Warranty",
            style = MaterialTheme.typography.labelMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant
        )
        Text(
            text = period,
            modifier = Modifier.weight(1f),
            style = MaterialTheme.typography.bodySmall.copy(fontWeight = FontWeight.Medium),
            color = MaterialTheme.colorScheme.onSurface
        )
        Surface(
            shape = RoundedCornerShape(50),
            color = tone.copy(alpha = 0.12f)
        ) {
            Text(
                text = label,
                modifier = Modifier.padding(horizontal = 8.dp, vertical = 4.dp),
                style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.Bold),
                color = tone
            )
        }
    }
}

private fun formatWarrantyDate(rawValue: String?): String? {
    val isoDate = rawValue?.trim()?.take(10)?.takeIf { it.length == 10 } ?: return null
    return try {
        val parser = java.text.SimpleDateFormat("yyyy-MM-dd", java.util.Locale.US).apply { isLenient = false }
        val formatter = java.text.SimpleDateFormat("dd MMM yyyy", java.util.Locale.US)
        formatter.format(parser.parse(isoDate) ?: return null)
    } catch (_: Exception) {
        null
    }
}
// ─── Set result card ──────────────────────────────────────────────────────────

@Composable
private fun SetResultCard(set: SerialLookupSetDto, isLatest: Boolean, onViewDetails: (String) -> Unit) {
    var expanded by remember { mutableStateOf(false) }
    val statusColor = when (set.status?.uppercase()) {
        "DISPATCHED" -> Color(0xFF7A2C24)
        "PENDING"    -> Color(0xFFE28A22)
        "ACTIVE"     -> Color(0xFF265D73)
        else         -> MaterialTheme.colorScheme.onSurfaceVariant
    }

    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(20.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        elevation = CardDefaults.cardElevation(defaultElevation = 0.dp),
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.3f))
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(20.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Row(
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.spacedBy(12.dp)
                ) {
                    Box(
                        modifier = Modifier
                            .size(42.dp)
                            .clip(CircleShape)
                            .background(statusColor.copy(alpha = 0.1f)),
                        contentAlignment = Alignment.Center
                    ) {
                        Icon(
                            Icons.Filled.Inventory2,
                            contentDescription = null,
                            tint = statusColor,
                            modifier = Modifier.size(20.dp)
                        )
                    }
                    Column(verticalArrangement = Arrangement.spacedBy(2.dp)) {
                        Text(
                            text = set.setCode ?: "",
                            style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.Bold),
                            color = MaterialTheme.colorScheme.onSurface
                        )
                        if (!isLatest) {
                            Text(
                                text = "Previous assignment",
                                style = MaterialTheme.typography.labelSmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                        }
                    }
                }
                Surface(
                    shape = RoundedCornerShape(50),
                    color = statusColor.copy(alpha = 0.12f)
                ) {
                    Text(
                        text = set.status ?: "",
                        modifier = Modifier.padding(horizontal = 10.dp, vertical = 4.dp),
                        style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.Bold),
                        color = statusColor
                    )
                }
            }

            HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.3f))

            if (!set.currentBranch.isNullOrBlank() || !set.currentDepartment.isNullOrBlank()) {
                Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(16.dp)) {
                    if (!set.currentBranch.isNullOrBlank()) {
                        LabeledValue(label = "Branch", value = set.currentBranch, modifier = Modifier.weight(1f))
                    }
                    if (!set.currentDepartment.isNullOrBlank()) {
                        LabeledValue(label = "Department", value = set.currentDepartment, modifier = Modifier.weight(1f))
                    }
                }
            }
            if (!set.site.isNullOrBlank()) {
                LabeledValue(label = "Site", value = set.site)
            }
            if (!set.dispatchDate.isNullOrBlank()) {
                LabeledValue(label = "Dispatch Date", value = set.dispatchDate)
            }
            if (!set.remarks.isNullOrBlank()) {
                LabeledValue(label = "Remarks", value = set.remarks)
            }

            if (!set.setCode.isNullOrBlank()) {
                Button(
                    onClick = { onViewDetails(set.setCode) },
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(12.dp)
                ) {
                    Icon(
                        imageVector = Icons.AutoMirrored.Filled.ArrowForward,
                        contentDescription = null,
                        modifier = Modifier.size(18.dp)
                    )
                    Spacer(Modifier.width(8.dp))
                    Text("View Dispatch Details")
                }
            }

            OutlinedButton(
                onClick = { expanded = !expanded },
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(12.dp)
            ) {
                Text(if (expanded) "Hide Details" else "View Set Details")
                Spacer(Modifier.width(6.dp))
                Icon(
                    imageVector = if (expanded) Icons.Filled.KeyboardArrowUp else Icons.AutoMirrored.Filled.ArrowForward,
                    contentDescription = null,
                    modifier = Modifier.size(16.dp)
                )
            }

            AnimatedVisibility(
                visible = expanded,
                enter = expandVertically() + fadeIn(),
                exit = shrinkVertically() + fadeOut()
            ) {
                Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.3f))
                    LabeledValue(label = "Set ID", value = set.setId.toString())
                    if (!set.qrToken.isNullOrBlank()) {
                        LabeledValue(label = "QR Token", value = set.qrToken)
                    }
                }
            }
            if (!set.qrToken.isNullOrBlank()) {
                SetDeploymentHistorySection(qrToken = set.qrToken)
            }
        }
    }
}

// ─── Item Movement History ────────────────────────────────────────────────────

@Composable
private fun ItemMovementSection(itemId: Int) {
    val scope = rememberCoroutineScope()
    var expanded by remember { mutableStateOf(false) }
    var isLoading by remember { mutableStateOf(false) }
    var entries by remember { mutableStateOf<List<ItemMovementEntryDto>>(emptyList()) }
    var error by remember { mutableStateOf<String?>(null) }
    var loaded by remember { mutableStateOf(false) }

    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
        OutlinedButton(
            onClick = {
                if (!loaded && !isLoading) {
                    scope.launch {
                        isLoading = true
                        when (val result = safeApiCall { ApiClient.service.getItemMovement(itemId) }) {
                            is ApiResult.Success -> { entries = result.data.movement; loaded = true }
                            is ApiResult.HttpError -> error = result.message ?: SCANNER_GENERIC_MESSAGE
                            is ApiResult.NetworkError -> error = result.message ?: SCANNER_NETWORK_MESSAGE
                            is ApiResult.UnknownError -> error = result.message ?: SCANNER_GENERIC_MESSAGE
                        }
                        isLoading = false
                    }
                }
                expanded = !expanded
            },
            modifier = Modifier.fillMaxWidth(),
            shape = RoundedCornerShape(12.dp)
        ) {
            Icon(Icons.Filled.History, contentDescription = null, modifier = Modifier.size(16.dp))
            Spacer(Modifier.width(6.dp))
            Text(if (expanded) "Hide Movement History" else "View Movement History")
            Spacer(Modifier.weight(1f))
            if (isLoading) {
                CircularProgressIndicator(modifier = Modifier.size(14.dp), strokeWidth = 2.dp)
            } else {
                Icon(
                    imageVector = if (expanded) Icons.Filled.KeyboardArrowUp else Icons.AutoMirrored.Filled.ArrowForward,
                    contentDescription = null, modifier = Modifier.size(16.dp)
                )
            }
        }
        AnimatedVisibility(
            visible = expanded,
            enter = expandVertically() + fadeIn(),
            exit = shrinkVertically() + fadeOut()
        ) {
            Card(
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(16.dp),
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.4f)),
                elevation = CardDefaults.cardElevation(0.dp)
            ) {
                Column(
                    modifier = Modifier.fillMaxWidth().padding(16.dp),
                    verticalArrangement = Arrangement.spacedBy(0.dp)
                ) {
                    Text(
                        text = "MOVEMENT HISTORY",
                        style = MaterialTheme.typography.labelSmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.65f),
                        modifier = Modifier.padding(bottom = 8.dp)
                    )
                    when {
                        error != null -> Text(text = error!!, style = MaterialTheme.typography.bodySmall, color = Color(0xFFC62828))
                        entries.isEmpty() -> Text(text = "No movement records found.", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                        else -> entries.forEachIndexed { i, e -> MovementTimelineItem(entry = e, isLast = i == entries.lastIndex) }
                    }
                }
            }
        }
    }
}

// ─── Set Deployment History ───────────────────────────────────────────────────

@Composable
private fun SetDeploymentHistorySection(qrToken: String) {
    val scope = rememberCoroutineScope()
    var expanded by remember { mutableStateOf(false) }
    var isLoading by remember { mutableStateOf(false) }
    var entries by remember { mutableStateOf<List<DeploymentHistoryDto>>(emptyList()) }
    var error by remember { mutableStateOf<String?>(null) }
    var loaded by remember { mutableStateOf(false) }

    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
        OutlinedButton(
            onClick = {
                if (!loaded && !isLoading) {
                    scope.launch {
                        isLoading = true
                        when (val result = safeApiCall { ApiClient.service.getSetDeploymentHistory(qrToken) }) {
                            is ApiResult.Success -> { entries = result.data.deploymentHistory; loaded = true }
                            is ApiResult.HttpError -> error = result.message ?: SCANNER_GENERIC_MESSAGE
                            is ApiResult.NetworkError -> error = result.message ?: SCANNER_NETWORK_MESSAGE
                            is ApiResult.UnknownError -> error = result.message ?: SCANNER_GENERIC_MESSAGE
                        }
                        isLoading = false
                    }
                }
                expanded = !expanded
            },
            modifier = Modifier.fillMaxWidth(),
            shape = RoundedCornerShape(12.dp)
        ) {
            Icon(Icons.Filled.History, contentDescription = null, modifier = Modifier.size(16.dp))
            Spacer(Modifier.width(6.dp))
            Text(if (expanded) "Hide Deployment History" else "View Deployment History")
            Spacer(Modifier.weight(1f))
            if (isLoading) {
                CircularProgressIndicator(modifier = Modifier.size(14.dp), strokeWidth = 2.dp)
            } else {
                Icon(
                    imageVector = if (expanded) Icons.Filled.KeyboardArrowUp else Icons.AutoMirrored.Filled.ArrowForward,
                    contentDescription = null, modifier = Modifier.size(16.dp)
                )
            }
        }
        AnimatedVisibility(
            visible = expanded,
            enter = expandVertically() + fadeIn(),
            exit = shrinkVertically() + fadeOut()
        ) {
            Card(
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(16.dp),
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.4f)),
                elevation = CardDefaults.cardElevation(0.dp)
            ) {
                Column(
                    modifier = Modifier.fillMaxWidth().padding(16.dp),
                    verticalArrangement = Arrangement.spacedBy(0.dp)
                ) {
                    Text(
                        text = "DEPLOYMENT HISTORY",
                        style = MaterialTheme.typography.labelSmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.65f),
                        modifier = Modifier.padding(bottom = 8.dp)
                    )
                    when {
                        error != null -> Text(text = error!!, style = MaterialTheme.typography.bodySmall, color = Color(0xFFC62828))
                        entries.isEmpty() -> Text(text = "No deployment history found.", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                        else -> entries.forEachIndexed { i, e -> DeploymentTimelineItem(entry = e, isLast = i == entries.lastIndex) }
                    }
                }
            }
        }
    }
}

// ─── Timeline Items ───────────────────────────────────────────────────────────

@Composable
private fun MovementTimelineItem(entry: ItemMovementEntryDto, isLast: Boolean) {
    val isPositive = entry.entryType?.uppercase() == "POSITIVE"
    val dotColor = if (isPositive) Color(0xFF2E7D32) else Color(0xFFC62828)
    Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(10.dp)) {
        Column(horizontalAlignment = Alignment.CenterHorizontally) {
            Box(modifier = Modifier.size(10.dp).clip(CircleShape).background(dotColor))
            if (!isLast) Box(modifier = Modifier.width(2.dp).height(56.dp).background(MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.5f)))
        }
        Column(
            modifier = Modifier.weight(1f).padding(bottom = if (isLast) 0.dp else 8.dp),
            verticalArrangement = Arrangement.spacedBy(3.dp)
        ) {
            Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
                Text(text = entry.entryType ?: "Entry", style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.Bold), color = dotColor)
                Text(text = entry.datePosted ?: "", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
            if (!entry.employee.isNullOrBlank()) Text(text = entry.employee, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurface)
            if (!entry.branch.isNullOrBlank() || !entry.department.isNullOrBlank()) {
                Text(text = listOfNotNull(entry.branch, entry.department).joinToString(" · "), style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
            if (!entry.setCode.isNullOrBlank()) Text(text = "Set: ${entry.setCode}", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
            if (!entry.description.isNullOrBlank()) Text(text = entry.description, style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.7f))
        }
    }
}

@Composable
private fun DeploymentTimelineItem(entry: DeploymentHistoryDto, isLast: Boolean) {
    val statusColor = when (entry.newStatus?.lowercase()) {
        "deployed", "delivered", "confirmed" -> Color(0xFF2E7D32)
        "dispatched" -> Color(0xFF1565C0)
        "pending" -> Color(0xFFE28A22)
        "cancelled" -> Color(0xFFC62828)
        else -> MaterialTheme.colorScheme.primary
    }
    Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(10.dp)) {
        Column(horizontalAlignment = Alignment.CenterHorizontally) {
            Box(modifier = Modifier.size(10.dp).clip(CircleShape).background(statusColor))
            if (!isLast) Box(modifier = Modifier.width(2.dp).height(56.dp).background(MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.5f)))
        }
        Column(
            modifier = Modifier.weight(1f).padding(bottom = if (isLast) 0.dp else 8.dp),
            verticalArrangement = Arrangement.spacedBy(3.dp)
        ) {
            Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
                Text(text = entry.newStatus ?: "Updated", style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.Bold), color = statusColor)
                Text(text = entry.deployedAt ?: "", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
            if (!entry.deployedBy.isNullOrBlank()) Text(text = entry.deployedBy, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurface)
            if (!entry.previousStatus.isNullOrBlank() && entry.previousStatus != entry.newStatus) {
                Text(text = "From: ${entry.previousStatus}", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        }
    }
}

// ─── Helper ───────────────────────────────────────────────────────────────────

@Composable
private fun LabeledValue(label: String, value: String, modifier: Modifier = Modifier) {
    Column(modifier = modifier, verticalArrangement = Arrangement.spacedBy(2.dp)) {
        Text(
            text = label.uppercase(),
            style = MaterialTheme.typography.labelSmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.65f)
        )
        Text(
            text = value,
            style = MaterialTheme.typography.bodySmall.copy(fontWeight = FontWeight.Medium),
            color = MaterialTheme.colorScheme.onSurface
        )
    }
}


