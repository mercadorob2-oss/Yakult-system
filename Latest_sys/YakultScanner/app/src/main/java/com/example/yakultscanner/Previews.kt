@file:OptIn(ExperimentalMaterial3Api::class)

package com.example.yakultscanner

import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.runtime.Composable
import androidx.compose.ui.tooling.preview.Preview
import androidx.navigation.compose.rememberNavController
import com.example.yakultscanner.ui.theme.YakultScannerTheme
import com.example.yakultscanner.ui.screens.RootHomeScreen
import com.example.yakultscanner.ui.screens.SerialScanHomeScreen
import com.example.yakultscanner.ui.screens.HomeScreen
import com.example.yakultscanner.ui.screens.HistoryScreen
import com.example.yakultscanner.ui.screens.ReportIssueHistoryScreen
import com.example.yakultscanner.ui.screens.ProcessedUpdatesScreen
import com.example.yakultscanner.ui.screens.PendingUpdatesScreen

@Preview(showBackground = true, showSystemUi = true)
@Composable
fun LoginScreenPreview() {
    YakultScannerTheme {
        LoginScreen(navController = rememberNavController())
    }
}

@Preview(showBackground = true, showSystemUi = true)
@Composable
fun RootHomeScreenPreview() {
    YakultScannerTheme {
        RootHomeScreen(navController = rememberNavController())
    }
}

@Preview(showBackground = true, showSystemUi = true)
@Composable
fun SerialScanHomeScreenPreview() {
    YakultScannerTheme {
        SerialScanHomeScreen(navController = rememberNavController())
    }
}

@Preview(showBackground = true, showSystemUi = true)
@Composable
fun HomeScreenPreview() {
    YakultScannerTheme {
        HomeScreen(navController = rememberNavController())
    }
}

@Preview(showBackground = true, showSystemUi = true)
@Composable
fun BatchSerialEntryScreenPreview() {
    YakultScannerTheme {
        BatchSerialEntryScreen(navController = rememberNavController())
    }
}

@Preview(showBackground = true, showSystemUi = true)
@Composable
fun DirectSerialScanScreenPreview() {
    YakultScannerTheme {
        DirectSerialScanScreen(navController = rememberNavController())
    }
}

@Preview(showBackground = true, showSystemUi = true)
@Composable
fun HistoryScreenPreview() {
    YakultScannerTheme {
        HistoryScreen(navController = rememberNavController())
    }
}

@Preview(showBackground = true, showSystemUi = true)
@Composable
fun ReportIssueHistoryScreenPreview() {
    YakultScannerTheme {
        ReportIssueHistoryScreen(navController = rememberNavController())
    }
}

@Preview(showBackground = true, showSystemUi = true)
@Composable
fun UploadedItemsScreenPreview() {
    YakultScannerTheme {
        UploadedItemsScreen(setCode = "SET-123", onNavigateBack = {})
    }
}

@Preview(showBackground = true, showSystemUi = true)
@Composable
fun HowToUseScreenPreview() {
    YakultScannerTheme {
        HowToUseScreen(onNavigateBack = {})
    }
}

@Preview(showBackground = true, showSystemUi = true)
@Composable
fun AppInfoScreenPreview() {
    YakultScannerTheme {
        AppInfoScreen(onNavigateBack = {}, isDarkMode = false, onThemeToggle = {})
    }
}

@Preview(showBackground = true, showSystemUi = true)
@Composable
fun EnhancedScannerScreenPreview() {
    YakultScannerTheme {
        EnhancedScannerScreen(
            navController = rememberNavController(),
            useDeviceScannerMode = false,
            returnRoute = "details"
        )
    }
}
