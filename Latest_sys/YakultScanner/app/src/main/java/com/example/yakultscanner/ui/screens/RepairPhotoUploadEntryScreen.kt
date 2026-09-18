package com.example.yakultscanner.ui.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.QrCodeScanner
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.navigation.NavController
import com.example.yakultscanner.SCAN_RETURN_ROUTE_KEY

private const val REPAIR_PHOTO_UPLOAD_RETURN_ROUTE = "repair_photo_upload"
private const val REPAIR_NO_PREFIX = "RPR-"

/**
 * Entry point for the mobile Repair Part evidence flow. A technician can scan the ticket QR
 * or enter the Repair No. before choosing a part and staging evidence.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun RepairPhotoUploadEntryScreen(navController: NavController) {
    RepairSystemBars()
    var ticketRef by remember { mutableStateOf(REPAIR_NO_PREFIX) }

    Scaffold(
        containerColor = RepairUi.Canvas,
        topBar = {
            TopAppBar(
                title = {
                    Column(verticalArrangement = Arrangement.spacedBy(1.dp)) {
                        Text("Repair evidence", color = RepairUi.Ink, fontWeight = FontWeight.Bold)
                        Text("Find a ticket to add photos or video", style = MaterialTheme.typography.labelSmall, color = RepairUi.Muted)
                    }
                },
                navigationIcon = {
                    IconButton(onClick = { navController.popBackStack() }) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back", tint = RepairUi.Ink)
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(containerColor = RepairUi.Surface)
            )
        }
    ) { paddingValues ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .background(RepairUi.Canvas)
                .padding(paddingValues)
                .padding(horizontal = 16.dp, vertical = 14.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            RepairSurfaceCard(tonalColor = RepairUi.BrandSoft) {
                RepairSectionHeading("Start with the repair ticket", "Use the ticket QR when available, or enter its repair number below.")
            }
            RepairSurfaceCard {
                RepairSectionHeading("Scan ticket QR", "Fastest way to open the correct repair and its parts.")
                RepairPrimaryButton(
                    onClick = {
                        navController.currentBackStackEntry?.savedStateHandle
                            ?.set(SCAN_RETURN_ROUTE_KEY, REPAIR_PHOTO_UPLOAD_RETURN_ROUTE)
                        navController.navigate("scanner_camera")
                    },
                    modifier = Modifier.fillMaxWidth().height(52.dp)
                ) {
                    Icon(Icons.Filled.QrCodeScanner, contentDescription = null)
                    Spacer(Modifier.width(8.dp))
                    Text("Scan ticket QR", fontWeight = FontWeight.Bold)
                }
            }
            RepairSurfaceCard {
                RepairSectionHeading("Or enter the repair number", "The RPR- prefix is included; enter the digits shown on the ticket.")
                OutlinedTextField(
                    value = ticketRef,
                    onValueChange = { newValue ->
                        val suffix = (if (newValue.startsWith(REPAIR_NO_PREFIX)) newValue.removePrefix(REPAIR_NO_PREFIX) else newValue)
                            .filter(Char::isDigit)
                        ticketRef = REPAIR_NO_PREFIX + suffix
                    },
                    modifier = Modifier.fillMaxWidth(),
                    label = { Text("Repair number") },
                    singleLine = true,
                    shape = androidx.compose.foundation.shape.RoundedCornerShape(12.dp),
                    colors = repairTextFieldColors()
                )
                RepairPrimaryButton(
                    onClick = { navController.navigate("$REPAIR_PHOTO_UPLOAD_RETURN_ROUTE/$ticketRef") },
                    enabled = ticketRef.removePrefix(REPAIR_NO_PREFIX).isNotBlank(),
                    modifier = Modifier.fillMaxWidth().height(52.dp)
                ) {
                    Text("Continue to evidence", fontWeight = FontWeight.Bold)
                }
            }
        }
    }
}
