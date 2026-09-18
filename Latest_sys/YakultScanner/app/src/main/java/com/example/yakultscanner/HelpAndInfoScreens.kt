package com.example.yakultscanner

import android.os.Build
import android.provider.Settings
import android.widget.Toast
import androidx.compose.foundation.background
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.DarkMode
import androidx.compose.material.icons.filled.LightMode
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.remember
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.getValue
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import com.example.yakultscanner.api.ApiClient
import com.example.yakultscanner.api.DatabaseInfoResponse
import com.example.yakultscanner.settings.ApiSettings
import com.example.yakultscanner.UserSession

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun HowToUseScreen(onNavigateBack: () -> Unit) {
    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("How to scan & upload") },
                navigationIcon = {
                    IconButton(onClick = onNavigateBack) {
                        Icon(
                            imageVector = Icons.AutoMirrored.Filled.ArrowBack,
                            contentDescription = "Back"
                        )
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = MaterialTheme.colorScheme.surface
                )
            )
        }
    ) { paddingValues ->
        HowToUseContent(paddingValues)
    }
}

@Composable
private fun HowToUseContent(paddingValues: PaddingValues) {
    val headerBrush = Brush.verticalGradient(
        colors = listOf(com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Ink, Color(0xFFA36A72))
    )

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Canvas)
            .padding(paddingValues)
    ) {
        Box(
            modifier = Modifier
                .fillMaxWidth()
                .height(180.dp)
                .background(headerBrush)
        )

        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(24.dp),
            verticalArrangement = Arrangement.spacedBy(16.dp),
            horizontalAlignment = Alignment.Start
        ) {
            Surface(
                shape = androidx.compose.foundation.shape.RoundedCornerShape(22.dp),
                color = MaterialTheme.colorScheme.surface.copy(alpha = 0.92f)
            ) {
                Column(
                    modifier = Modifier.padding(18.dp),
                    verticalArrangement = Arrangement.spacedBy(6.dp)
                ) {
                    Text(
                        text = "Scanner guide",
                        style = MaterialTheme.typography.headlineSmall,
                        fontWeight = FontWeight.Bold
                    )
                    Text(
                        text = "A quick field workflow for scanning, reviewing, and sending updates back to the inventory system.",
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
            }

            InfoSectionCard(title = "Overview") {
                Text(
                    text = "This app lets you scan a dispatch QR, update item status and remarks in the field, and upload those changes back to the Yakult inventory system.",
                    style = MaterialTheme.typography.bodyMedium
                )
            }

            InfoSectionCard(title = "Steps") {
                HelpStep("1. Scan QR", "From the scanner workspace, choose the built-in scanner or the camera and point it at the dispatch QR.")
                HelpStep("2. Review set details", "Check the set code, employee, branch, and the included items before making changes.")
                HelpStep("3. Edit item status", "Update item status and remarks for damaged, missing, installed, or other field observations.")
                HelpStep("4. Upload updates", "Send your changes so they can be reviewed and processed by the office system.")
                HelpStep("5. Confirm in office", "Office staff apply the uploaded changes to the main desktop inventory records.")
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun AppInfoScreen(
    onNavigateBack: () -> Unit,
    isDarkMode: Boolean,
    onThemeToggle: () -> Unit
) {
    val context = LocalContext.current
    val scrollState = rememberScrollState()
    var showClearCacheConfirm by remember { mutableStateOf(false) }

    val packageInfo = remember {
        context.packageManager.getPackageInfo(context.packageName, 0)
    }
    val versionName = packageInfo.versionName ?: "?"
    val versionCode = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P) {
        packageInfo.longVersionCode.toString()
    } else {
        @Suppress("DEPRECATION")
        packageInfo.versionCode.toString()
    }

    val androidId = remember {
        Settings.Secure.getString(context.contentResolver, Settings.Secure.ANDROID_ID) ?: "unknown"
    }
    val currentUser = remember { UserSession.currentUser }
    val currentApiBaseUrl = ApiSettings.apiBaseUrl
    var databaseInfo by remember { mutableStateOf<DatabaseInfoResponse?>(null) }
    var databaseInfoLoading by remember { mutableStateOf(true) }
    var databaseInfoError by remember { mutableStateOf<String?>(null) }

    LaunchedEffect(currentApiBaseUrl) {
        databaseInfoLoading = true
        databaseInfoError = null
        databaseInfo = null
        try {
            val response = ApiClient.service.getDatabaseInfo()
            val body = response.body()
            if (response.isSuccessful && body != null &&
                (body.success || body.configured || !body.database.isNullOrBlank())) {
                databaseInfo = body
            } else {
                databaseInfoError = body?.message?.takeIf { it.isNotBlank() }
                    ?: "Database properties are unavailable."
            }
        } catch (_: Exception) {
            databaseInfoError = "Database properties are unavailable."
        } finally {
            databaseInfoLoading = false
        }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Settings & Info") },
                navigationIcon = {
                    IconButton(onClick = onNavigateBack) {
                        Icon(
                            imageVector = Icons.AutoMirrored.Filled.ArrowBack,
                            contentDescription = "Back"
                        )
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = MaterialTheme.colorScheme.surface
                )
            )
        }
    ) { paddingValues ->
        val headerBrush = Brush.verticalGradient(
            colors = listOf(com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Ink, Color(0xFFA36A72))
        )

        Box(
            modifier = Modifier
                .fillMaxSize()
                .background(com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Canvas)
                .padding(paddingValues)
        ) {
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .height(180.dp)
                    .background(headerBrush)
            )

            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .verticalScroll(scrollState)
                    .padding(24.dp),
                verticalArrangement = Arrangement.spacedBy(14.dp)
            ) {
                Surface(
                    shape = androidx.compose.foundation.shape.RoundedCornerShape(22.dp),
                    color = MaterialTheme.colorScheme.surface.copy(alpha = 0.92f)
                ) {
                    Column(
                        modifier = Modifier.padding(18.dp),
                        verticalArrangement = Arrangement.spacedBy(6.dp)
                    ) {
                        Text(
                            text = "Scanner settings",
                            style = MaterialTheme.typography.headlineSmall,
                            fontWeight = FontWeight.Bold
                        )
                        Text(
                            text = "Theme, device details, API information, and local maintenance live here now.",
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                }

                InfoSectionCard(title = "Account") {
                    InfoRow(label = "Signed in as", value = currentUser?.displayName ?: "Unknown user")
                    InfoRow(label = "Username", value = currentUser?.username ?: "Unknown")
                }

                InfoSectionCard(title = "Application") {
                    InfoRow(label = "Version", value = "$versionName ($versionCode)")
                    InfoRow(label = "Package", value = context.packageName)
                }

                InfoSectionCard(title = "Device") {
                    InfoRow(label = "Model", value = "${Build.MANUFACTURER} ${Build.MODEL}")
                    InfoRow(label = "Android", value = "${Build.VERSION.RELEASE} (SDK ${Build.VERSION.SDK_INT})")
                    InfoRow(label = "Android ID", value = androidId)
                }

                InfoSectionCard(title = "Network / API") {
                    InfoRow(label = "API base URL", value = currentApiBaseUrl)
                    InfoRow(
                        label = "Configured database",
                        value = if (databaseInfoLoading) "Loading…"
                        else databaseInfo?.database?.takeIf { it.isNotBlank() } ?: "Unavailable"
                    )
                    InfoRow(
                        label = "Database server",
                        value = databaseInfo?.server?.takeIf { it.isNotBlank() }
                            ?: databaseInfo?.dataSource?.takeIf { it.isNotBlank() }
                            ?: "Unavailable"
                    )
                    InfoRow(
                        label = "Database port",
                        value = databaseInfo?.port?.toString() ?: "Default / instance"
                    )
                    InfoRow(
                        label = "Database metadata",
                        value = when {
                            databaseInfoLoading -> "Loading…"
                            databaseInfo != null -> "Configuration loaded"
                            else -> databaseInfoError ?: "Unavailable"
                        }
                    )
                    Text(
                        text = "Only non-secret database properties are shown.",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }

                InfoSectionCard(title = "Appearance & Maintenance") {
                    Button(
                        onClick = onThemeToggle,
                        modifier = Modifier
                            .fillMaxWidth()
                            .height(48.dp)
                    ) {
                        Icon(
                            imageVector = if (isDarkMode) Icons.Filled.LightMode else Icons.Filled.DarkMode,
                            contentDescription = null,
                            modifier = Modifier.size(18.dp)
                        )
                        Text(
                            text = if (isDarkMode) "Switch to light mode" else "Switch to dark mode",
                            modifier = Modifier.padding(start = 8.dp)
                        )
                    }
                    Text(
                        text = if (isDarkMode) "Dark theme is enabled for the scanner app." else "Light theme is enabled for the scanner app.",
                        style = MaterialTheme.typography.bodySmall,
                        textAlign = TextAlign.Start
                    )
                    Button(
                        onClick = { showClearCacheConfirm = true },
                        modifier = Modifier
                            .fillMaxWidth()
                            .height(48.dp)
                    ) {
                        Text("Clear cache")
                    }
                    Text(
                        text = "Clears saved scan history (recent sets) and pinned sets on this device.",
                        style = MaterialTheme.typography.bodySmall,
                        textAlign = TextAlign.Start
                    )
                }

                Text(
                    text = "You can screenshot this page when reporting issues so support can see your app version, device, and server configuration.",
                    style = MaterialTheme.typography.bodySmall,
                    textAlign = TextAlign.Start
                )

                Box(modifier = Modifier.height(24.dp))
            }
        }
    }

    if (showClearCacheConfirm) {
        AlertDialog(
            onDismissRequest = { showClearCacheConfirm = false },
            title = { Text("Clear cache?") },
            text = { Text("This will remove recent set history and pinned sets saved on this device. Server data will not be affected.") },
            confirmButton = {
                TextButton(
                    onClick = {
                        clearScanHistory(context)
                        PinnedSetsStore.clear(context)
                        Toast.makeText(context, "Cache cleared.", Toast.LENGTH_SHORT).show()
                        showClearCacheConfirm = false
                    }
                ) {
                    Text("Clear")
                }
            },
            dismissButton = {
                TextButton(onClick = { showClearCacheConfirm = false }) { Text("Cancel") }
            }
        )
    }
}

@Composable
private fun InfoRow(label: String, value: String) {
    Column(modifier = Modifier.fillMaxWidth().padding(vertical = 2.dp)) {
        Text(text = label, style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
        Text(text = value, style = MaterialTheme.typography.bodyMedium)
    }
}

@Composable
private fun InfoSectionCard(
    title: String,
    content: @Composable ColumnScope.() -> Unit
) {
    Surface(
        modifier = Modifier.fillMaxWidth(),
        shape = androidx.compose.foundation.shape.RoundedCornerShape(18.dp),
        color = MaterialTheme.colorScheme.surface,
        tonalElevation = 1.dp
    ) {
        Column(
            modifier = Modifier.padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp),
            content = {
                Text(
                    text = title,
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.Bold
                )
                content()
            }
        )
    }
}

@Composable
private fun HelpStep(title: String, body: String) {
    Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
        Text(text = title, style = MaterialTheme.typography.bodyMedium, fontWeight = FontWeight.SemiBold)
        Text(text = body, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
    }
}