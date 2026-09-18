package com.example.yakultscanner.ui.screens

import android.widget.Toast
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.defaultMinSize
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Assignment
import androidx.compose.material.icons.filled.CalendarMonth
import androidx.compose.material.icons.filled.Category
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.ContentCopy
import androidx.compose.material.icons.filled.Info
import androidx.compose.material.icons.filled.Inventory2
import androidx.compose.material.icons.filled.Person
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalClipboardManager
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.navigation.NavController
import com.example.yakultscanner.api.ApiClient
import com.example.yakultscanner.api.ApiResult
import com.example.yakultscanner.api.SerialLookupItemDto
import com.example.yakultscanner.api.safeApiCall
import java.text.SimpleDateFormat
import java.util.Locale

private sealed interface DispatchItemDetailsUiState {
    data object Loading : DispatchItemDetailsUiState
    data class Success(val item: SerialLookupItemDto) : DispatchItemDetailsUiState
    data object NotFound : DispatchItemDetailsUiState
    data class Error(val message: String) : DispatchItemDetailsUiState
}

private data class WarrantyPresentation(
    val label: String,
    val color: Color,
    val icon: androidx.compose.ui.graphics.vector.ImageVector
)

private val DispatchRed = Color(0xFF8C2F2C)
private val DispatchRedDeep = Color(0xFF5F201F)
private val DispatchRose = Color(0xFFC68182)
private val InkSoft = Color(0xFF5C5656)

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DispatchItemDetailsScreen(
    navController: NavController,
    serialNumber: String,
    setCode: String?,
    dispatchStatus: String?,
    assignedEmployee: String?
) {
    var reloadKey by remember { mutableIntStateOf(0) }
    var uiState by remember(serialNumber) {
        mutableStateOf<DispatchItemDetailsUiState>(DispatchItemDetailsUiState.Loading)
    }

    LaunchedEffect(serialNumber, reloadKey) {
        uiState = DispatchItemDetailsUiState.Loading
        uiState = when (val result = safeApiCall { ApiClient.service.serialLookup(serialNumber) }) {
            is ApiResult.Success -> {
                val item = result.data.item
                if (result.data.found && item != null) {
                    DispatchItemDetailsUiState.Success(item)
                } else {
                    DispatchItemDetailsUiState.NotFound
                }
            }
            is ApiResult.HttpError -> DispatchItemDetailsUiState.Error(
                result.message ?: "We couldn't load this inventory item."
            )
            is ApiResult.NetworkError -> DispatchItemDetailsUiState.Error(
                result.message ?: "Network error. Check your connection and try again."
            )
            is ApiResult.UnknownError -> DispatchItemDetailsUiState.Error(
                result.message ?: "We couldn't load this inventory item."
            )
        }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Text(
                        text = "Item details",
                        style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold)
                    )
                },
                navigationIcon = {
                    IconButton(onClick = navController::popBackStack) {
                        Icon(
                            imageVector = Icons.AutoMirrored.Filled.ArrowBack,
                            contentDescription = "Back"
                        )
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = DispatchRedDeep,
                    titleContentColor = Color.White,
                    navigationIconContentColor = Color.White
                )
            )
        },
        containerColor = MaterialTheme.colorScheme.background
    ) { paddingValues ->
        when (val state = uiState) {
            DispatchItemDetailsUiState.Loading -> {
                Box(
                    modifier = Modifier
                        .fillMaxSize()
                        .padding(paddingValues),
                    contentAlignment = Alignment.Center
                ) {
                    CircularProgressIndicator(color = DispatchRed)
                }
            }

            DispatchItemDetailsUiState.NotFound -> {
                ItemDetailsMessage(
                    modifier = Modifier.padding(paddingValues),
                    title = "Item not found",
                    message = "Serial $serialNumber is not available in the selected inventory environment.",
                    actionLabel = "Back",
                    onAction = navController::popBackStack
                )
            }

            is DispatchItemDetailsUiState.Error -> {
                ItemDetailsMessage(
                    modifier = Modifier.padding(paddingValues),
                    title = "Couldn't load item details",
                    message = state.message,
                    actionLabel = "Try again",
                    onAction = { reloadKey++ }
                )
            }

            is DispatchItemDetailsUiState.Success -> {
                DispatchItemDetailsContent(
                    item = state.item,
                    serialNumber = serialNumber,
                    setCode = setCode,
                    dispatchStatus = dispatchStatus,
                    assignedEmployee = assignedEmployee,
                    modifier = Modifier.padding(paddingValues)
                )
            }
        }
    }
}

@Composable
private fun DispatchItemDetailsContent(
    item: SerialLookupItemDto,
    serialNumber: String,
    setCode: String?,
    dispatchStatus: String?,
    assignedEmployee: String?,
    modifier: Modifier = Modifier
) {
    val context = LocalContext.current
    val clipboardManager = LocalClipboardManager.current
    val name = item.name.orDisplay(item.description.orDisplay("Unnamed item"))
    val description = item.description?.trim()?.takeIf {
        it.isNotEmpty() && !it.equals(name, ignoreCase = true)
    }
    val resolvedSerial = item.serialNumber.orDisplay(serialNumber)
    val warranty = item.warrantyPresentation()

    Box(
        modifier = modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background)
    ) {
        Box(
            modifier = Modifier
                .fillMaxWidth()
                .height(178.dp)
                .background(
                    Brush.verticalGradient(
                        colors = listOf(DispatchRedDeep, DispatchRed, DispatchRose)
                    )
                )
        )

        Column(
            modifier = Modifier
                .fillMaxSize()
                .verticalScroll(rememberScrollState())
                .padding(horizontal = 16.dp)
                .padding(top = 28.dp, bottom = 28.dp),
            verticalArrangement = Arrangement.spacedBy(14.dp)
        ) {
            ItemIdentityHero(
                name = name,
                description = description,
                serialNumber = resolvedSerial,
                active = item.active,
                onCopySerial = {
                    clipboardManager.setText(AnnotatedString(resolvedSerial))
                    Toast.makeText(context, "Serial number copied", Toast.LENGTH_SHORT).show()
                }
            )

            QuickFacts(
                itemType = item.itemType.orDisplay("Not encoded"),
                model = item.modelNumber.orDisplay("Not encoded"),
                condition = item.condition.orDisplay("Not encoded")
            )

            InventoryOverview(item = item)

            WarrantyPanel(
                item = item,
                presentation = warranty
            )

            DispatchContextPanel(
                setCode = setCode.orDisplay("Not recorded"),
                dispatchStatus = dispatchStatus.orDisplay("Not recorded"),
                assignedEmployee = assignedEmployee.orDisplay("Not recorded")
            )
        }
    }
}

@Composable
private fun ItemIdentityHero(
    name: String,
    description: String?,
    serialNumber: String,
    active: Boolean,
    onCopySerial: () -> Unit
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(26.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        elevation = CardDefaults.cardElevation(defaultElevation = 6.dp),
        border = BorderStroke(1.dp, Color.White.copy(alpha = 0.30f))
    ) {
        Column(
            modifier = Modifier.padding(20.dp),
            verticalArrangement = Arrangement.spacedBy(14.dp)
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(14.dp),
                verticalAlignment = Alignment.Top
            ) {
                Surface(
                    modifier = Modifier.size(50.dp),
                    shape = RoundedCornerShape(16.dp),
                    color = DispatchRed.copy(alpha = 0.12f)
                ) {
                    Icon(
                        imageVector = Icons.Filled.Inventory2,
                        contentDescription = null,
                        modifier = Modifier.padding(12.dp),
                        tint = DispatchRed
                    )
                }
                Column(
                    modifier = Modifier.weight(1f),
                    verticalArrangement = Arrangement.spacedBy(4.dp)
                ) {
                    Text(
                        text = "DISPATCHED ASSET",
                        style = MaterialTheme.typography.labelSmall.copy(
                            fontWeight = FontWeight.ExtraBold,
                            letterSpacing = 0.8.sp
                        ),
                        color = DispatchRed
                    )
                    Text(
                        text = name,
                        style = MaterialTheme.typography.headlineSmall.copy(fontWeight = FontWeight.ExtraBold),
                        maxLines = 2,
                        overflow = TextOverflow.Ellipsis
                    )
                    description?.let {
                        Text(
                            text = it,
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                            maxLines = 2,
                            overflow = TextOverflow.Ellipsis
                        )
                    }
                }
                AssetStatusPill(active = active)
            }

            Surface(
                modifier = Modifier
                    .fillMaxWidth()
                    .clip(RoundedCornerShape(16.dp))
                    .clickable(onClick = onCopySerial),
                shape = RoundedCornerShape(16.dp),
                color = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.64f),
                border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.55f))
            ) {
                Row(
                    modifier = Modifier.padding(horizontal = 14.dp, vertical = 12.dp),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.spacedBy(10.dp)
                ) {
                    Icon(
                        imageVector = Icons.Filled.ContentCopy,
                        contentDescription = "Copy serial number",
                        modifier = Modifier.size(18.dp),
                        tint = DispatchRed
                    )
                    Column(modifier = Modifier.weight(1f)) {
                        Text(
                            text = "SERIAL NUMBER",
                            style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.ExtraBold),
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                        Text(
                            text = serialNumber,
                            style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                            color = MaterialTheme.colorScheme.onSurface
                        )
                    }
                    Text(
                        text = "COPY",
                        style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.ExtraBold),
                        color = DispatchRed
                    )
                }
            }
        }
    }
}

@Composable
private fun AssetStatusPill(active: Boolean) {
    val color = if (active) Color(0xFF2E7D32) else MaterialTheme.colorScheme.error
    val label = if (active) "ACTIVE" else "INACTIVE"
    Surface(
        shape = RoundedCornerShape(50),
        color = color.copy(alpha = 0.12f),
        border = BorderStroke(1.dp, color.copy(alpha = 0.35f))
    ) {
        Row(
            modifier = Modifier.padding(horizontal = 9.dp, vertical = 5.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(4.dp)
        ) {
            Icon(
                imageVector = if (active) Icons.Filled.CheckCircle else Icons.Filled.Warning,
                contentDescription = null,
                modifier = Modifier.size(13.dp),
                tint = color
            )
            Text(
                text = label,
                style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.ExtraBold),
                color = color
            )
        }
    }
}

@Composable
private fun QuickFacts(
    itemType: String,
    model: String,
    condition: String
) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        QuickFact("TYPE", itemType, Modifier.weight(1f))
        QuickFact("MODEL", model, Modifier.weight(1f))
        QuickFact("CONDITION", condition, Modifier.weight(1f))
    }
}

@Composable
private fun QuickFact(label: String, value: String, modifier: Modifier = Modifier) {
    Surface(
        modifier = modifier.defaultMinSize(minHeight = 76.dp),
        shape = RoundedCornerShape(18.dp),
        color = MaterialTheme.colorScheme.surface,
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.55f))
    ) {
        Column(
            modifier = Modifier.padding(horizontal = 11.dp, vertical = 10.dp),
            verticalArrangement = Arrangement.spacedBy(4.dp)
        ) {
            Text(
                text = label,
                style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.ExtraBold),
                color = InkSoft
            )
            Text(
                text = value,
                style = MaterialTheme.typography.bodySmall.copy(fontWeight = FontWeight.Bold),
                maxLines = 2,
                overflow = TextOverflow.Ellipsis
            )
        }
    }
}

@Composable
private fun InventoryOverview(item: SerialLookupItemDto) {
    DetailPanel(
        title = "Inventory overview",
        icon = Icons.Filled.Category,
        accent = DispatchRed
    ) {
        DetailPairGrid(
            firstLabel = "Category",
            firstValue = item.category.orDisplay("Not encoded"),
            secondLabel = "Condition",
            secondValue = item.condition.orDisplay("Not encoded")
        )
        HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.62f))
        DetailPairGrid(
            firstLabel = "Item type",
            firstValue = item.itemType.orDisplay("Not encoded"),
            secondLabel = "Model number",
            secondValue = item.modelNumber.orDisplay("Not encoded")
        )
    }
}

@Composable
private fun WarrantyPanel(
    item: SerialLookupItemDto,
    presentation: WarrantyPresentation
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(22.dp),
        colors = CardDefaults.cardColors(containerColor = presentation.color.copy(alpha = 0.075f)),
        border = BorderStroke(1.dp, presentation.color.copy(alpha = 0.36f))
    ) {
        Column(
            modifier = Modifier.padding(18.dp),
            verticalArrangement = Arrangement.spacedBy(14.dp)
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(12.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                Surface(
                    modifier = Modifier.size(42.dp),
                    shape = CircleShape,
                    color = presentation.color.copy(alpha = 0.14f)
                ) {
                    Icon(
                        imageVector = presentation.icon,
                        contentDescription = null,
                        modifier = Modifier.padding(10.dp),
                        tint = presentation.color
                    )
                }
                Column(modifier = Modifier.weight(1f)) {
                    Text(
                        text = "WARRANTY COVERAGE",
                        style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.ExtraBold),
                        color = InkSoft
                    )
                    Text(
                        text = presentation.label,
                        style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.ExtraBold),
                        color = presentation.color
                    )
                }
            }

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(10.dp)
            ) {
                WarrantyDate(
                    label = "STARTS",
                    value = formatWarrantyDate(item.warrantyStartDate).orDisplay("Not encoded"),
                    modifier = Modifier.weight(1f)
                )
                WarrantyDate(
                    label = "ENDS",
                    value = formatWarrantyDate(item.warrantyEndDate).orDisplay("Not encoded"),
                    modifier = Modifier.weight(1f)
                )
            }
        }
    }
}

@Composable
private fun WarrantyDate(label: String, value: String, modifier: Modifier = Modifier) {
    Surface(
        modifier = modifier,
        shape = RoundedCornerShape(15.dp),
        color = MaterialTheme.colorScheme.surface.copy(alpha = 0.78f)
    ) {
        Column(
            modifier = Modifier.padding(12.dp),
            verticalArrangement = Arrangement.spacedBy(3.dp)
        ) {
            Text(
                text = label,
                style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.ExtraBold),
                color = InkSoft
            )
            Text(
                text = value,
                style = MaterialTheme.typography.bodySmall.copy(fontWeight = FontWeight.Bold),
                maxLines = 2,
                overflow = TextOverflow.Ellipsis
            )
        }
    }
}

@Composable
private fun DispatchContextPanel(
    setCode: String,
    dispatchStatus: String,
    assignedEmployee: String
) {
    DetailPanel(
        title = "Dispatch context",
        icon = Icons.Filled.Assignment,
        accent = Color(0xFF5A6E9B)
    ) {
        ContextRow(Icons.Filled.Assignment, "Set code", setCode)
        HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.62f))
        ContextRow(Icons.Filled.Info, "Set status", dispatchStatus)
        HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.62f))
        ContextRow(Icons.Filled.Person, "Assigned to", assignedEmployee)
    }
}

@Composable
private fun DetailPanel(
    title: String,
    icon: androidx.compose.ui.graphics.vector.ImageVector,
    accent: Color,
    content: @Composable () -> Unit
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(22.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.62f))
    ) {
        Column(
            modifier = Modifier.padding(18.dp),
            verticalArrangement = Arrangement.spacedBy(13.dp)
        ) {
            Row(
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(9.dp)
            ) {
                Surface(
                    modifier = Modifier.size(30.dp),
                    shape = RoundedCornerShape(9.dp),
                    color = accent.copy(alpha = 0.12f)
                ) {
                    Icon(
                        imageVector = icon,
                        contentDescription = null,
                        modifier = Modifier.padding(7.dp),
                        tint = accent
                    )
                }
                Text(
                    text = title,
                    style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.ExtraBold)
                )
            }
            content()
        }
    }
}

@Composable
private fun DetailPairGrid(
    firstLabel: String,
    firstValue: String,
    secondLabel: String,
    secondValue: String
) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.spacedBy(16.dp)
    ) {
        CompactDetail(firstLabel, firstValue, Modifier.weight(1f))
        CompactDetail(secondLabel, secondValue, Modifier.weight(1f))
    }
}

@Composable
private fun CompactDetail(label: String, value: String, modifier: Modifier = Modifier) {
    Column(modifier = modifier, verticalArrangement = Arrangement.spacedBy(3.dp)) {
        Text(
            text = label.uppercase(Locale.US),
            style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.ExtraBold),
            color = InkSoft
        )
        Text(
            text = value,
            style = MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.SemiBold),
            maxLines = 2,
            overflow = TextOverflow.Ellipsis
        )
    }
}

@Composable
private fun ContextRow(
    icon: androidx.compose.ui.graphics.vector.ImageVector,
    label: String,
    value: String
) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.spacedBy(12.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Icon(
            imageVector = icon,
            contentDescription = null,
            modifier = Modifier.size(18.dp),
            tint = MaterialTheme.colorScheme.onSurfaceVariant
        )
        Column(modifier = Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(2.dp)) {
            Text(
                text = label.uppercase(Locale.US),
                style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.ExtraBold),
                color = InkSoft
            )
            Text(
                text = value,
                style = MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.SemiBold),
                maxLines = 2,
                overflow = TextOverflow.Ellipsis
            )
        }
    }
}

private fun SerialLookupItemDto.warrantyPresentation(): WarrantyPresentation {
    return when (warrantyStatus?.trim()?.lowercase(Locale.US)) {
        "good" -> WarrantyPresentation("Warranty Good", Color(0xFF2E7D32), Icons.Filled.CheckCircle)
        "expiringsoon" -> WarrantyPresentation("Expiring Soon", Color(0xFFE07B16), Icons.Filled.Warning)
        "expired" -> WarrantyPresentation("Warranty Expired", Color(0xFFC62828), Icons.Filled.Warning)
        else -> WarrantyPresentation("Warranty not encoded", InkSoft, Icons.Filled.CalendarMonth)
    }
}

@Composable
private fun ItemDetailsMessage(
    modifier: Modifier,
    title: String,
    message: String,
    actionLabel: String,
    onAction: () -> Unit
) {
    Box(
        modifier = modifier.fillMaxSize(),
        contentAlignment = Alignment.Center
    ) {
        Card(
            modifier = Modifier
                .fillMaxWidth()
                .padding(24.dp),
            shape = RoundedCornerShape(22.dp),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
        ) {
            Column(
                modifier = Modifier.padding(24.dp),
                horizontalAlignment = Alignment.CenterHorizontally,
                verticalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                Surface(
                    modifier = Modifier.size(48.dp),
                    shape = CircleShape,
                    color = DispatchRed.copy(alpha = 0.12f)
                ) {
                    Icon(
                        imageVector = Icons.Filled.Info,
                        contentDescription = null,
                        modifier = Modifier.padding(12.dp),
                        tint = DispatchRed
                    )
                }
                Text(title, style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold))
                Text(
                    text = message,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    style = MaterialTheme.typography.bodyMedium
                )
                Spacer(Modifier.height(2.dp))
                Button(
                    onClick = onAction,
                    colors = ButtonDefaults.buttonColors(containerColor = DispatchRed)
                ) {
                    Text(actionLabel)
                }
            }
        }
    }
}

private fun String?.orDisplay(fallback: String): String =
    this?.trim()?.takeIf { it.isNotEmpty() } ?: fallback

private fun formatWarrantyDate(value: String?): String? {
    val raw = value?.trim()?.takeIf { it.isNotEmpty() } ?: return null
    return runCatching {
        val input = SimpleDateFormat("yyyy-MM-dd", Locale.US).apply { isLenient = false }
        val output = SimpleDateFormat("dd MMM yyyy", Locale.US)
        output.format(input.parse(raw) ?: return null)
    }.getOrNull()
}
