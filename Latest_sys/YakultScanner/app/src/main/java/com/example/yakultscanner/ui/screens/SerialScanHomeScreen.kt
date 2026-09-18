package com.example.yakultscanner.ui.screens

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
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
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.automirrored.filled.ArrowForward
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.FileDownload
import androidx.compose.material.icons.filled.FindInPage
import androidx.compose.material.icons.filled.Help
import androidx.compose.material.icons.filled.Info
import androidx.compose.material.icons.filled.QrCodeScanner
import androidx.compose.material.icons.outlined.Logout
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.navigation.NavController
import com.example.yakultscanner.UserSession

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun SerialScanHomeScreen(navController: NavController) {
    val currentUser = UserSession.currentUser
    val headerBrush = Brush.verticalGradient(
        colors = listOf(
            MaterialTheme.colorScheme.primary,
            Color(0xFFB71C1C)
        )
    )

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Text(
                        "Serial Scan Dashboard",
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.Bold
                    )
                },
                navigationIcon = {
                    IconButton(onClick = { navController.popBackStack() }) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back to main home")
                    }
                },
                actions = {
                    IconButton(onClick = { navController.navigate("help") }) {
                        Icon(Icons.Filled.Help, contentDescription = "How to scan and upload")
                    }
                    IconButton(onClick = { navController.navigate("app_info") }) {
                        Icon(Icons.Filled.Info, contentDescription = "Settings and app info")
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
                            Icon(Icons.Outlined.Logout, contentDescription = "Logout")
                        }
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = MaterialTheme.colorScheme.primary,
                    titleContentColor = Color.White,
                    navigationIconContentColor = Color.White,
                    actionIconContentColor = Color.White
                )
            )
        }
    ) { paddingValues ->
        LazyColumn(
            modifier = Modifier
                .fillMaxSize()
                .background(MaterialTheme.colorScheme.background)
                .padding(paddingValues),
            contentPadding = PaddingValues(bottom = 28.dp),
            verticalArrangement = Arrangement.spacedBy(14.dp)
        ) {
            item {
                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .background(headerBrush)
                        .padding(horizontal = 16.dp, vertical = 18.dp)
                ) {
                    Column(verticalArrangement = Arrangement.spacedBy(14.dp)) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Column(modifier = Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                                Text(
                                    text = "Choose a serial workflow",
                                    style = MaterialTheme.typography.titleLarge,
                                    fontWeight = FontWeight.Bold,
                                    color = Color.White
                                )
                                Text(
                                    text = currentUser?.displayName?.let { "Logged in as $it" }
                                        ?: "Scan, identify, save, or export serial records",
                                    style = MaterialTheme.typography.bodySmall,
                                    color = Color.White.copy(alpha = 0.82f),
                                    maxLines = 1,
                                    overflow = TextOverflow.Ellipsis
                                )
                            }
                            Surface(
                                shape = CircleShape,
                                color = Color.White.copy(alpha = 0.16f)
                            ) {
                                Icon(
                                    Icons.Filled.QrCodeScanner,
                                    contentDescription = null,
                                    tint = Color.White,
                                    modifier = Modifier.padding(12.dp).size(30.dp)
                                )
                            }
                        }
                        Row(horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.fillMaxWidth()) {
                            SerialDashboardMetric("Modes", "5", Modifier.weight(1f))
                            SerialDashboardMetric("Local", "CSV/PDF", Modifier.weight(1f))
                            SerialDashboardMetric("Desktop", "Send", Modifier.weight(1f))
                        }
                    }
                }
            }

            item {
                Text(
                    text = "Primary scans",
                    modifier = Modifier.padding(start = 16.dp, end = 16.dp, top = 2.dp),
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.Bold
                )
            }

            item {
                SerialModuleCard(
                    title = "Direct Serial Scan",
                    subtitle = "Scan single serials and send them immediately to desktop inventory.",
                    badge = "Live send",
                    icon = Icons.Filled.QrCodeScanner,
                    iconColor = MaterialTheme.colorScheme.tertiaryContainer,
                    featured = false,
                    onClick = { navController.navigate("direct_serial_scan") }
                )
            }

            item {
                SerialModuleCard(
                    title = "Local Serial Scan Report",
                    subtitle = "Collect serials or phone IMEIs locally, then export CSV/PDF or send later.",
                    badge = "Offline ready",
                    icon = Icons.Filled.FileDownload,
                    iconColor = MaterialTheme.colorScheme.primaryContainer,
                    featured = true,
                    onClick = { navController.navigate("local_serial_scan_report") }
                )
            }

            item {
                Text(
                    text = "Supporting tools",
                    modifier = Modifier.padding(start = 16.dp, end = 16.dp, top = 8.dp),
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.Bold
                )
            }

            item {
                SerialModuleCard(
                    title = "Add Items by Serial",
                    subtitle = "Create item records by scanning or typing serial numbers for one model.",
                    badge = "Batch add",
                    icon = Icons.Filled.Add,
                    iconColor = MaterialTheme.colorScheme.secondaryContainer,
                    featured = false,
                    onClick = { navController.navigate("batch_serial_entry") }
                )
            }

            item {
                SerialModuleCard(
                    title = "Serial Set Identifier",
                    subtitle = "Check whether a scanned serial is registered and linked to a set.",
                    badge = "Lookup",
                    icon = Icons.Filled.FindInPage,
                    iconColor = MaterialTheme.colorScheme.surfaceVariant,
                    featured = false,
                    onClick = { navController.navigate("serial_set_identifier") }
                )
            }

            item {
                Text(
                    text = "E Documents",
                    modifier = Modifier.padding(start = 16.dp, end = 16.dp, top = 8.dp),
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.Bold
                )
            }

            item {
                SerialModuleCard(
                    title = "Transmittal Scan",
                    subtitle = "Scan serials, IMEIs, and mobile numbers into the original Excel transmittal form.",
                    badge = "Excel report",
                    icon = Icons.Filled.FileDownload,
                    iconColor = MaterialTheme.colorScheme.tertiaryContainer,
                    featured = true,
                    onClick = { navController.navigate("transmittal_scan") }
                )
            }

            item {
                SerialModuleCard(
                    title = "Gatepass / File Transmittal",
                    subtitle = "Scan serials into the local Gatepass form and save a PDF without uploading to the server.",
                    badge = "Local PDF",
                    icon = Icons.Filled.FileDownload,
                    iconColor = MaterialTheme.colorScheme.secondaryContainer,
                    featured = false,
                    onClick = { navController.navigate("gatepass_scan") }
                )
            }
        }
    }
}

@Composable
private fun SerialDashboardMetric(
    label: String,
    value: String,
    modifier: Modifier = Modifier
) {
    Surface(
        modifier = modifier,
        shape = RoundedCornerShape(14.dp),
        color = Color.White.copy(alpha = 0.16f)
    ) {
        Column(
            modifier = Modifier.padding(horizontal = 10.dp, vertical = 8.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Text(value, color = Color.White, style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.Bold)
            Text(label, color = Color.White.copy(alpha = 0.84f), style = MaterialTheme.typography.labelSmall)
        }
    }
}

@Composable
private fun SerialModuleCard(
    title: String,
    subtitle: String,
    badge: String,
    icon: ImageVector,
    iconColor: Color,
    featured: Boolean,
    onClick: () -> Unit
) {
    Card(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 16.dp),
        shape = RoundedCornerShape(18.dp),
        colors = CardDefaults.cardColors(
            containerColor = if (featured) MaterialTheme.colorScheme.primaryContainer.copy(alpha = 0.34f)
            else MaterialTheme.colorScheme.surface
        ),
        elevation = CardDefaults.cardElevation(defaultElevation = if (featured) 0.dp else 1.dp),
        border = if (featured) null else BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.45f)),
        onClick = onClick
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(16.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(14.dp)
        ) {
            Box(
                modifier = Modifier
                    .size(54.dp)
                    .background(iconColor, CircleShape),
                contentAlignment = Alignment.Center
            ) {
                Icon(
                    icon,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.onSurface,
                    modifier = Modifier.size(28.dp)
                )
            }
            Column(modifier = Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(5.dp)) {
                Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text(
                        text = title,
                        modifier = Modifier.weight(1f),
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.Bold,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis
                    )
                    Surface(
                        shape = RoundedCornerShape(999.dp),
                        color = if (featured) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.surfaceVariant
                    ) {
                        Text(
                            text = badge,
                            modifier = Modifier.padding(horizontal = 9.dp, vertical = 4.dp),
                            style = MaterialTheme.typography.labelSmall,
                            color = if (featured) Color.White else MaterialTheme.colorScheme.onSurfaceVariant,
                            fontWeight = FontWeight.Bold,
                            maxLines = 1
                        )
                    }
                }
                Text(
                    text = subtitle,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    maxLines = 2,
                    overflow = TextOverflow.Ellipsis
                )
            }
            Spacer(Modifier.width(2.dp))
            Icon(
                Icons.AutoMirrored.Filled.ArrowForward,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.size(20.dp)
            )
        }
    }
}

