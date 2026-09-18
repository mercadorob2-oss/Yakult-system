package com.example.yakultscanner.ui.components

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.Search
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.ExposedDropdownMenuBox
import androidx.compose.material3.ExposedDropdownMenuDefaults
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.MenuAnchorType
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.OutlinedTextFieldDefaults
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.buildAnnotatedString
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.text.withStyle
import androidx.compose.ui.unit.dp

/**
 * Visual primitives scoped to IT Call Monitoring. They intentionally do not alter the
 * scanner-wide Material theme because the ITCM redesign is limited to ticket workflows.
 */
object ItcmUi {
    val Canvas = Color(0xFFFFF8F5)
    val Surface = Color(0xFFFFFEFC)
    val SurfaceSubtle = Color(0xFFF1E9ED)
    val Ink = Color(0xFF3B2525)
    val Muted = Color(0xFF7A6268)
    val Divider = Color(0xFFE7D9DD)
    val Brand = Color(0xFFE60012)
    val BrandSoft = Color(0xFFFFE6E8)
    val Critical = Color(0xFFB42318)
    val High = Color(0xFFC66A00)
    val Medium = Color(0xFFA36A72)
    val Low = Color(0xFF2E7D5B)
    val Pending = Color(0xFF9A6700)
    val Active = Color(0xFFA36A72)
    val Escalated = Color(0xFF7A4BB7)
    val Resolved = Color(0xFF23824F)
    val Temporary = Color(0xFF087C8C)
    val Closed = Color(0xFF5F6B7A)
}

fun itcmPriorityColor(priority: String?): Color = when (priority?.trim()?.lowercase()) {
    "critical" -> ItcmUi.Critical
    "high" -> ItcmUi.High
    "medium" -> ItcmUi.Medium
    else -> ItcmUi.Low
}

fun itcmStatusColor(status: String?): Color = when (status?.trim()?.lowercase()) {
    "pending" -> ItcmUi.Pending
    "in progress", "reopened" -> ItcmUi.Active
    "escalated" -> ItcmUi.Escalated
    "solved" -> ItcmUi.Resolved
    "resolved (temporary)" -> ItcmUi.Temporary
    "closed" -> ItcmUi.Closed
    else -> ItcmUi.Muted
}

@Composable
fun ItcmSurfaceCard(
    modifier: Modifier = Modifier,
    content: @Composable ColumnScope.() -> Unit
) {
    Card(
        modifier = modifier,
        shape = RoundedCornerShape(20.dp),
        colors = CardDefaults.cardColors(containerColor = ItcmUi.Surface),
        elevation = CardDefaults.cardElevation(defaultElevation = 0.dp),
        border = BorderStroke(1.dp, ItcmUi.Divider)
    ) {
        Column(
            modifier = Modifier.padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp),
            content = content
        )
    }
}

@Composable
fun ItcmStatusChip(
    status: String?,
    modifier: Modifier = Modifier
) {
    val color = itcmStatusColor(status)
    Box(
        modifier = modifier
            .clip(RoundedCornerShape(999.dp))
            .background(color.copy(alpha = 0.12f))
            .padding(horizontal = 10.dp, vertical = 5.dp)
    ) {
        Text(
            text = status?.takeIf { it.isNotBlank() } ?: "Unknown",
            style = MaterialTheme.typography.labelSmall,
            fontWeight = FontWeight.Bold,
            color = color,
            maxLines = 1,
            overflow = TextOverflow.Ellipsis
        )
    }
}

@Composable
fun ItcmPriorityChip(
    priority: String?,
    modifier: Modifier = Modifier
) {
    val color = itcmPriorityColor(priority)
    Row(
        modifier = modifier
            .clip(RoundedCornerShape(999.dp))
            .background(color.copy(alpha = 0.10f))
            .padding(horizontal = 9.dp, vertical = 5.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(6.dp)
    ) {
        Box(
            modifier = Modifier
                .size(6.dp)
                .clip(RoundedCornerShape(99.dp))
                .background(color)
        )
        Text(
            text = priority?.takeIf { it.isNotBlank() } ?: "Normal",
            style = MaterialTheme.typography.labelSmall,
            fontWeight = FontWeight.SemiBold,
            color = color
        )
    }
}

@Composable
fun ItcmSectionHeading(
    title: String,
    supportingText: String? = null,
    modifier: Modifier = Modifier
) {
    Column(modifier = modifier, verticalArrangement = Arrangement.spacedBy(3.dp)) {
        Text(
            text = title,
            style = MaterialTheme.typography.titleSmall,
            fontWeight = FontWeight.Bold,
            color = ItcmUi.Ink
        )
        supportingText?.takeIf { it.isNotBlank() }?.let {
            Text(
                text = it,
                style = MaterialTheme.typography.bodySmall,
                color = ItcmUi.Muted
            )
        }
    }
}

@Composable
fun ItcmChoiceCard(
    selected: Boolean,
    title: String,
    description: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier
) {
    Card(
        modifier = modifier
            .fillMaxWidth()
            .clickable(onClick = onClick),
        shape = RoundedCornerShape(16.dp),
        colors = CardDefaults.cardColors(
            containerColor = if (selected) ItcmUi.BrandSoft else ItcmUi.Surface
        ),
        border = BorderStroke(
            width = if (selected) 1.5.dp else 1.dp,
            color = if (selected) ItcmUi.Brand else ItcmUi.Divider
        ),
        elevation = CardDefaults.cardElevation(defaultElevation = 0.dp)
    ) {
        Row(
            modifier = Modifier.padding(14.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            Box(
                modifier = Modifier
                    .size(20.dp)
                    .clip(RoundedCornerShape(99.dp))
                    .background(if (selected) ItcmUi.Brand else ItcmUi.SurfaceSubtle),
                contentAlignment = Alignment.Center
            ) {
                if (selected) {
                    Box(
                        modifier = Modifier
                            .size(8.dp)
                            .clip(RoundedCornerShape(99.dp))
                            .background(Color.White)
                    )
                }
            }
            Column(verticalArrangement = Arrangement.spacedBy(3.dp)) {
                Text(title, style = MaterialTheme.typography.bodyMedium, fontWeight = FontWeight.Bold, color = ItcmUi.Ink)
                Text(description, style = MaterialTheme.typography.bodySmall, color = ItcmUi.Muted)
            }
        }
    }
}

@Composable
fun ItcmStepProgress(
    currentStep: Int,
    totalSteps: Int,
    labels: List<String>,
    modifier: Modifier = Modifier
) {
    val safeTotal = totalSteps.coerceAtLeast(1)
    val safeCurrent = currentStep.coerceIn(1, safeTotal)
    Column(modifier = modifier, verticalArrangement = Arrangement.spacedBy(8.dp)) {
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.spacedBy(6.dp)
        ) {
            repeat(safeTotal) { index ->
                Box(
                    modifier = Modifier
                        .weight(1f)
                        .height(5.dp)
                        .clip(RoundedCornerShape(99.dp))
                        .background(if (index < safeCurrent) ItcmUi.Brand else ItcmUi.Divider)
                )
            }
        }
        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
            Text(
                text = "Step $safeCurrent of $safeTotal",
                style = MaterialTheme.typography.labelMedium,
                fontWeight = FontWeight.Bold,
                color = ItcmUi.Brand
            )
            Text(
                text = labels.getOrNull(safeCurrent - 1).orEmpty(),
                style = MaterialTheme.typography.labelMedium,
                color = ItcmUi.Muted,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis
            )
        }
    }
}

@Composable
fun ItcmInitialAvatar(name: String, modifier: Modifier = Modifier) {
    val initials = name.trim().split(Regex("\\s+")).take(2)
        .mapNotNull { it.firstOrNull()?.uppercaseChar() }.joinToString("")
    Box(
        modifier = modifier
            .size(32.dp)
            .clip(RoundedCornerShape(99.dp))
            .background(ItcmUi.BrandSoft),
        contentAlignment = Alignment.Center
    ) {
        Text(
            initials.ifBlank { "?" },
            style = MaterialTheme.typography.labelMedium,
            fontWeight = FontWeight.Bold,
            color = ItcmUi.Brand
        )
    }
}

@Composable
fun ItcmMetaText(
    text: String,
    modifier: Modifier = Modifier
) {
    Text(
        text = text,
        modifier = modifier,
        style = MaterialTheme.typography.labelSmall,
        color = ItcmUi.Muted,
        maxLines = 1,
        overflow = TextOverflow.Ellipsis
    )
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ItcmExposedLookupDropdown(
    label: String,
    selectedName: String,
    items: List<String>,
    onSelectedByIndex: (Int) -> Unit,
    modifier: Modifier = Modifier,
    enabled: Boolean = true,
    placeholder: String = "Select…",
    isError: Boolean = false,
    supportingText: String? = null
) {
    var expanded by remember { mutableStateOf(false) }
    ExposedDropdownMenuBox(
        expanded = expanded,
        onExpandedChange = { if (enabled) expanded = it },
        modifier = modifier
    ) {
        OutlinedTextField(
            value = selectedName,
            onValueChange = {},
            readOnly = true,
            enabled = enabled,
            label = { Text(label) },
            placeholder = { Text(placeholder) },
            trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded) },
            isError = isError,
            supportingText = supportingText?.takeIf { it.isNotBlank() }?.let { { Text(it) } },
            singleLine = true,
            shape = RoundedCornerShape(16.dp),
            colors = OutlinedTextFieldDefaults.colors(
                focusedBorderColor = ItcmUi.Ink,
                focusedLabelColor = ItcmUi.Ink,
                cursorColor = ItcmUi.Ink,
                unfocusedBorderColor = ItcmUi.Divider,
                disabledBorderColor = ItcmUi.Muted.copy(alpha = 0.6f),
                disabledLabelColor = ItcmUi.Muted,
                disabledPlaceholderColor = ItcmUi.Muted.copy(alpha = 0.9f),
                disabledTrailingIconColor = ItcmUi.Muted,
                disabledTextColor = ItcmUi.Ink.copy(alpha = 0.7f),
                errorBorderColor = ItcmUi.Critical,
                errorLabelColor = ItcmUi.Critical
            ),
            modifier = Modifier.menuAnchor(MenuAnchorType.PrimaryNotEditable, enabled).fillMaxWidth()
        )
        ExposedDropdownMenu(
            expanded = expanded,
            onDismissRequest = { expanded = false },
            shape = RoundedCornerShape(16.dp),
            containerColor = ItcmUi.Surface,
            shadowElevation = 8.dp
        ) {
            if (items.isEmpty()) {
                DropdownMenuItem(
                    text = { Text("No options available", color = ItcmUi.Muted) },
                    onClick = { expanded = false }
                )
            }
            val selectedIndex = items.indexOf(selectedName)
            items.forEachIndexed { index, name ->
                val selected = selectedIndex >= 0 && index == selectedIndex
                DropdownMenuItem(
                    text = {
                        Text(
                            name,
                            color = ItcmUi.Ink,
                            fontWeight = if (selected) FontWeight.Bold else FontWeight.Normal
                        )
                    },
                    leadingIcon = { ItcmInitialAvatar(name) },
                    trailingIcon = if (selected) {
                        { Icon(Icons.Filled.Check, contentDescription = null, tint = ItcmUi.Brand) }
                    } else {
                        null
                    },
                    contentPadding = PaddingValues(horizontal = 16.dp, vertical = 4.dp),
                    modifier = Modifier
                        .heightIn(min = 48.dp)
                        .clip(RoundedCornerShape(12.dp))
                        .background(if (selected) ItcmUi.BrandSoft else Color.Transparent),
                    onClick = { onSelectedByIndex(index); expanded = false }
                )
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ItcmExposedStringDropdown(
    label: String,
    value: String,
    options: List<String>,
    onSelected: (String) -> Unit,
    modifier: Modifier = Modifier
) {
    var expanded by remember { mutableStateOf(false) }
    ExposedDropdownMenuBox(
        expanded = expanded,
        onExpandedChange = { expanded = it },
        modifier = modifier
    ) {
        OutlinedTextField(
            value = value,
            onValueChange = {},
            readOnly = true,
            label = { Text(label) },
            trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded) },
            singleLine = true,
            shape = RoundedCornerShape(16.dp),
            colors = OutlinedTextFieldDefaults.colors(
                focusedBorderColor = ItcmUi.Ink,
                focusedLabelColor = ItcmUi.Ink,
                cursorColor = ItcmUi.Ink,
                unfocusedBorderColor = ItcmUi.Divider
            ),
            modifier = Modifier.menuAnchor(MenuAnchorType.PrimaryNotEditable).fillMaxWidth()
        )
        ExposedDropdownMenu(
            expanded = expanded,
            onDismissRequest = { expanded = false },
            shape = RoundedCornerShape(16.dp),
            containerColor = ItcmUi.Surface,
            shadowElevation = 8.dp
        ) {
            options.forEach { option ->
                val selected = option == value
                val dotColor = when (option.trim().lowercase()) {
                    "critical", "high", "medium", "low" -> itcmPriorityColor(option)
                    else -> ItcmUi.Brand
                }
                DropdownMenuItem(
                    text = {
                        Text(
                            option,
                            color = ItcmUi.Ink,
                            fontWeight = if (selected) FontWeight.Bold else FontWeight.Normal
                        )
                    },
                    leadingIcon = {
                        Box(
                            modifier = Modifier
                                .size(8.dp)
                                .clip(RoundedCornerShape(99.dp))
                                .background(dotColor)
                        )
                    },
                    trailingIcon = if (selected) {
                        { Icon(Icons.Filled.Check, contentDescription = null, tint = ItcmUi.Brand) }
                    } else {
                        null
                    },
                    contentPadding = PaddingValues(horizontal = 16.dp, vertical = 4.dp),
                    modifier = Modifier
                        .heightIn(min = 48.dp)
                        .clip(RoundedCornerShape(12.dp))
                        .background(if (selected) ItcmUi.BrandSoft else Color.Transparent),
                    onClick = { onSelected(option); expanded = false }
                )
            }
        }
    }
}

@Composable
fun <T> ItcmLookupPickerDialog(
    title: String,
    items: List<T>,
    itemLabel: (T) -> String,
    onSelected: (T) -> Unit,
    onDismiss: () -> Unit,
    searchQuery: String? = null,
    onSearchChange: ((String) -> Unit)? = null,
    isLoading: Boolean = false,
    emptyText: String = "No matching choices."
) {
    AlertDialog(
        onDismissRequest = onDismiss,
        containerColor = ItcmUi.Surface,
        title = { Text(title, color = ItcmUi.Ink) },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                if (onSearchChange != null) {
                    OutlinedTextField(
                        value = searchQuery.orEmpty(),
                        onValueChange = onSearchChange,
                        modifier = Modifier.fillMaxWidth(),
                        label = { Text("Search") },
                        singleLine = true,
                        shape = RoundedCornerShape(16.dp)
                    )
                }
                if (isLoading) {
                    CircularProgressIndicator(
                        modifier = Modifier.align(Alignment.CenterHorizontally).size(24.dp)
                    )
                }
                Column(
                    modifier = Modifier.heightIn(max = 300.dp).verticalScroll(rememberScrollState())
                ) {
                    if (!isLoading && items.isEmpty()) {
                        Text(emptyText, style = MaterialTheme.typography.bodySmall, color = ItcmUi.Muted)
                    }
                    items.forEach { item ->
                        Row(
                            modifier = Modifier.fillMaxWidth().clickable { onSelected(item) }.padding(vertical = 8.dp),
                            verticalAlignment = Alignment.CenterVertically,
                            horizontalArrangement = Arrangement.spacedBy(12.dp)
                        ) {
                            ItcmInitialAvatar(itemLabel(item))
                            Text(
                                itemLabel(item),
                                style = MaterialTheme.typography.bodyMedium,
                                color = ItcmUi.Ink,
                                modifier = Modifier.weight(1f)
                            )
                        }
                        HorizontalDivider(color = ItcmUi.Divider)
                    }
                }
            }
        },
        confirmButton = { TextButton(onClick = onDismiss) { Text("Close") } }
    )
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun <T> ItcmBottomSheetPicker(
    title: String,
    supportingText: String? = null,
    items: List<T>,
    itemLabel: (T) -> String,
    itemSubtitle: ((T) -> String?)? = null,
    selectedItem: T?,
    onSelected: (T) -> Unit,
    onDismiss: () -> Unit,
    searchQuery: String? = null,
    onSearchChange: ((String) -> Unit)? = null,
    isLoading: Boolean = false,
    emptyText: String = "No matching choices.",
    emptyHint: String? = null,
    searchLabel: String = "Search",
    searchPlaceholder: String = "Type at least 2 characters…",
    highlightQuery: String? = null,
    resultCountText: String? = null,
    clearLabel: String = "Clear selection",
    onClear: (() -> Unit)? = null
) {
    ModalBottomSheet(
        onDismissRequest = onDismiss,
        containerColor = ItcmUi.Surface,
        shape = RoundedCornerShape(topStart = 24.dp, topEnd = 24.dp)
    ) {
        Column(
            modifier = Modifier.fillMaxWidth().padding(horizontal = 20.dp).padding(bottom = 24.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.Top,
                horizontalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                ItcmSectionHeading(title, supportingText, modifier = Modifier.weight(1f))
                IconButton(onClick = onDismiss) {
                    Icon(Icons.Filled.Close, contentDescription = "Close $title", tint = ItcmUi.Ink)
                }
            }
            if (onSearchChange != null) {
                val focusRequester = remember { FocusRequester() }
                LaunchedEffect(Unit) { focusRequester.requestFocus() }
                OutlinedTextField(
                    value = searchQuery.orEmpty(),
                    onValueChange = onSearchChange,
                    modifier = Modifier.fillMaxWidth().focusRequester(focusRequester),
                    label = { Text(searchLabel) },
                    placeholder = { Text(searchPlaceholder) },
                    leadingIcon = { Icon(Icons.Filled.Search, contentDescription = null, tint = ItcmUi.Muted) },
                    trailingIcon = if (!searchQuery.isNullOrEmpty()) {
                        {
                            IconButton(onClick = { onSearchChange("") }) {
                                Icon(Icons.Filled.Close, contentDescription = "Clear search", tint = ItcmUi.Muted)
                            }
                        }
                    } else {
                        null
                    },
                    singleLine = true,
                    shape = RoundedCornerShape(16.dp),
                    colors = OutlinedTextFieldDefaults.colors(
                        focusedBorderColor = ItcmUi.Ink,
                        focusedLabelColor = ItcmUi.Ink,
                        focusedLeadingIconColor = ItcmUi.Ink,
                        cursorColor = ItcmUi.Ink,
                        unfocusedBorderColor = ItcmUi.Divider,
                        unfocusedLabelColor = ItcmUi.Muted,
                        errorBorderColor = ItcmUi.Critical,
                        errorLabelColor = ItcmUi.Critical
                    )
                )
            }
            if (isLoading) {
                Row(
                    modifier = Modifier.fillMaxWidth().padding(vertical = 16.dp),
                    horizontalArrangement = Arrangement.Center,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    CircularProgressIndicator(modifier = Modifier.size(24.dp), color = ItcmUi.Brand)
                }
            }
            if (!isLoading && items.isEmpty()) {
                Column(
                    modifier = Modifier.fillMaxWidth().padding(vertical = 16.dp),
                    verticalArrangement = Arrangement.spacedBy(6.dp),
                    horizontalAlignment = Alignment.CenterHorizontally
                ) {
                    Icon(Icons.Filled.Search, contentDescription = null, tint = ItcmUi.Muted, modifier = Modifier.size(28.dp))
                    Text(emptyText, style = MaterialTheme.typography.bodyMedium, color = ItcmUi.Ink, fontWeight = FontWeight.SemiBold)
                    (emptyHint ?: if (!searchQuery.isNullOrEmpty()) "Try a different spelling or fewer characters." else null)?.let {
                        Text(it, style = MaterialTheme.typography.bodySmall, color = ItcmUi.Muted)
                    }
                }
            }
            LazyColumn(modifier = Modifier.heightIn(max = 360.dp)) {
                items(items) { item ->
                    val selected = item == selectedItem
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .heightIn(min = 64.dp)
                            .clip(RoundedCornerShape(12.dp))
                            .clickable { onSelected(item) }
                            .background(if (selected) ItcmUi.BrandSoft else Color.Transparent)
                            .padding(vertical = 12.dp, horizontal = 8.dp),
                        verticalAlignment = Alignment.CenterVertically,
                        horizontalArrangement = Arrangement.spacedBy(12.dp)
                    ) {
                        ItcmInitialAvatar(itemLabel(item))
                        Column(modifier = Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(2.dp)) {
                            ItcmHighlightedText(
                                fullText = itemLabel(item),
                                query = highlightQuery ?: searchQuery,
                                style = MaterialTheme.typography.bodyMedium,
                                color = ItcmUi.Ink,
                                highlightWeight = if (selected) FontWeight.Bold else FontWeight.Bold
                            )
                            itemSubtitle?.invoke(item)?.takeIf { it.isNotBlank() }?.let {
                                Text(
                                    it,
                                    style = MaterialTheme.typography.labelSmall,
                                    color = ItcmUi.Muted,
                                    maxLines = 1,
                                    overflow = TextOverflow.Ellipsis
                                )
                            }
                        }
                        if (selected) {
                            Icon(Icons.Filled.Check, contentDescription = "Selected", tint = ItcmUi.Brand)
                        }
                    }
                    HorizontalDivider(color = ItcmUi.Divider)
                }
            }
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    resultCountText ?: "${items.size} option${if (items.size == 1) "" else "s"}",
                    style = MaterialTheme.typography.labelSmall,
                    color = ItcmUi.Muted
                )
                if (onClear != null) {
                    TextButton(onClick = onClear) { Text(clearLabel, color = ItcmUi.Brand) }
                }
            }
        }
    }
}

@Composable
fun ItcmHighlightedText(
    fullText: String,
    query: String?,
    style: androidx.compose.ui.text.TextStyle,
    color: Color,
    highlightWeight: FontWeight = FontWeight.Bold,
    modifier: Modifier = Modifier
) {
    val q = query?.trim().orEmpty()
    if (q.length < 2) {
        Text(fullText, style = style, color = color, modifier = modifier, maxLines = 1, overflow = TextOverflow.Ellipsis)
        return
    }
    val start = fullText.indexOf(q, ignoreCase = true)
    if (start < 0) {
        Text(fullText, style = style, color = color, modifier = modifier, maxLines = 1, overflow = TextOverflow.Ellipsis)
        return
    }
    Text(
        buildAnnotatedString {
            append(fullText.substring(0, start))
            withStyle(SpanStyle(fontWeight = highlightWeight, background = ItcmUi.BrandSoft)) {
                append(fullText.substring(start, start + q.length))
            }
            append(fullText.substring(start + q.length))
        },
        style = style,
        color = color,
        modifier = modifier,
        maxLines = 1,
        overflow = TextOverflow.Ellipsis
    )
}