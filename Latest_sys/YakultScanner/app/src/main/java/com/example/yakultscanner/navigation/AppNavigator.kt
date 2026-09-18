package com.example.yakultscanner.navigation

import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.widthIn
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Scaffold
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.navigation.NavType
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.currentBackStackEntryAsState
import androidx.navigation.compose.rememberNavController
import androidx.navigation.navArgument
import com.example.yakultscanner.*
import com.example.yakultscanner.ui.adaptive.AdaptiveLayoutProvider
import com.example.yakultscanner.ui.adaptive.LocalAdaptiveLayout
import com.example.yakultscanner.ui.components.CustomFloatingNavigationBar
import com.example.yakultscanner.ui.components.CustomNavigationRail
import com.example.yakultscanner.ui.components.shouldShowScannerBottomNav
import com.example.yakultscanner.ui.screens.BorrowHomeScreen
import com.example.yakultscanner.ui.screens.BorrowAddItemScreen
import com.example.yakultscanner.ui.screens.ModernPendingUpdatesScreen
import com.example.yakultscanner.ui.screens.BorrowItemsScreen
import com.example.yakultscanner.ui.screens.BorrowRecordsScreen
import com.example.yakultscanner.ui.screens.RootHomeScreen
import com.example.yakultscanner.ui.screens.SavedFilesScreen
import com.example.yakultscanner.ui.screens.SerialScanHomeScreen
import com.example.yakultscanner.ui.screens.SerialSetIdentifierScreen
import com.example.yakultscanner.ui.screens.DispatchItemDetailsScreen
import com.example.yakultscanner.ui.screens.ModernLocalSerialScanReportScreen
import com.example.yakultscanner.ui.screens.HomeScreen
import com.example.yakultscanner.ui.screens.ProcessedUpdatesScreen
import com.example.yakultscanner.ui.screens.ReportModuleDetailScreen
import com.example.yakultscanner.ui.screens.ReportsScreen
import com.example.yakultscanner.ui.screens.HistoryScreen
import com.example.yakultscanner.ui.screens.ReportIssueHistoryScreen
import com.example.yakultscanner.ui.screens.RepairPhotoUploadEntryScreen
import com.example.yakultscanner.ui.screens.RepairPhotoUploadScreen
import com.example.yakultscanner.ui.screens.CallDashboardScreen
import com.example.yakultscanner.ui.screens.CallMonitoringScreen
import com.example.yakultscanner.ui.screens.TicketDetailScreen
import com.example.yakultscanner.ui.screens.CreateTicketScreen
import com.example.yakultscanner.ui.screens.CallMarkAsResolvedScreen

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun AppNavigator(
    isDarkMode: Boolean,
    onThemeToggle: () -> Unit
) {
    AdaptiveLayoutProvider {
        val navController = rememberNavController()
        GlobalNav.navController = navController

        val navBackStackEntry = navController.currentBackStackEntryAsState().value
        val currentRoute = navBackStackEntry?.destination?.route
        val previousRoute = navController.previousBackStackEntry?.destination?.route
        val adaptiveLayout = LocalAdaptiveLayout.current
        LaunchedEffect(currentRoute, previousRoute) {
            ScanRouting.updateFromRoutes(currentRoute, previousRoute)
        }

        val showBottomBar = shouldShowScannerBottomNav(currentRoute)

        Scaffold(
            bottomBar = {
                if (showBottomBar && !adaptiveLayout.isTablet) {
                    CustomFloatingNavigationBar(navController)
                }
            }
        ) { innerPadding ->
            Row(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(innerPadding)
            ) {
                if (showBottomBar && adaptiveLayout.isTablet) {
                    CustomNavigationRail(navController)
                }

                Box(
                    modifier = Modifier
                        .weight(1f)
                        .fillMaxSize(),
                    contentAlignment = Alignment.TopCenter
                ) {
                    NavHost(
                        navController = navController,
                        startDestination = "login",
                        modifier = Modifier
                            .fillMaxSize()
                            .widthIn(max = adaptiveLayout.contentMaxWidth)
                    ) {
            composable("login") {
                LoginScreen(navController = navController)
            }
            composable("register") {
                RegisterScreen(navController = navController)
            }
            composable("root_home") {
                RootHomeScreen(navController = navController)
            }
            composable("saved_transmittal_files") {
                SavedFilesScreen(navController = navController)
            }
            composable("serial_scan_home") {
                SerialScanHomeScreen(navController = navController)
            }
            composable("borrow_home") {
                BorrowHomeScreen(navController = navController)
            }
            composable("home") {
                HomeScreen(navController = navController)
            }
            composable("scanner_device") { backStackEntry ->
                val returnRoute =
                    backStackEntry.savedStateHandle.get<String>(SCAN_RETURN_ROUTE_KEY)
                        ?: navController.previousBackStackEntry?.savedStateHandle?.get<String>(SCAN_RETURN_ROUTE_KEY)
                        ?: "details"
                EnhancedScannerScreen(
                    navController = navController,
                    useDeviceScannerMode = true,
                    returnRoute = returnRoute
                )
            }
            composable("scanner_camera") { backStackEntry ->
                val returnRoute =
                    backStackEntry.savedStateHandle.get<String>(SCAN_RETURN_ROUTE_KEY)
                        ?: navController.previousBackStackEntry?.savedStateHandle?.get<String>(SCAN_RETURN_ROUTE_KEY)
                        ?: "details"
                EnhancedScannerScreen(
                    navController = navController,
                    useDeviceScannerMode = false,
                    returnRoute = returnRoute
                )
            }
            composable(
                route = "details/{scannedString}?token={token}",
                arguments = listOf(
                    navArgument("scannedString") { type = NavType.StringType },
                    navArgument("token") { type = NavType.StringType; nullable = true; defaultValue = null }
                )
            ) { backStackEntry ->
                val scannedString = backStackEntry.arguments?.getString("scannedString") ?: ""
                val token = backStackEntry.arguments?.getString("token")
                DispatchDetailsScreen(
                    navController = navController,
                    scannedString = scannedString,
                    readOnly = false,
                    token = token
                )
            }
            composable("direct_scan_details/{scannedString}") { backStackEntry ->
                val scannedString = backStackEntry.arguments?.getString("scannedString") ?: ""
                // For direct scan, we want to return the scanned serial to the direct scan screen
                // We'll pop back to direct_serial_scan with the scanned value
                navController.previousBackStackEntry?.savedStateHandle?.set("scanned_serial", scannedString)
                navController.popBackStack()
            }
            composable("details_readonly/{scannedString}") { backStackEntry ->
                val scannedString = backStackEntry.arguments?.getString("scannedString") ?: ""
                DispatchDetailsScreen(
                    navController = navController,
                    scannedString = scannedString,
                    readOnly = true
                )
            }
            composable(
                route = "dispatch_item_details/{serialNumber}?setCode={setCode}&dispatchStatus={dispatchStatus}&employee={employee}",
                arguments = listOf(
                    navArgument("serialNumber") { type = NavType.StringType },
                    navArgument("setCode") { type = NavType.StringType; nullable = true; defaultValue = null },
                    navArgument("dispatchStatus") { type = NavType.StringType; nullable = true; defaultValue = null },
                    navArgument("employee") { type = NavType.StringType; nullable = true; defaultValue = null }
                )
            ) { backStackEntry ->
                DispatchItemDetailsScreen(
                    navController = navController,
                    serialNumber = backStackEntry.arguments?.getString("serialNumber") ?: "",
                    setCode = backStackEntry.arguments?.getString("setCode"),
                    dispatchStatus = backStackEntry.arguments?.getString("dispatchStatus"),
                    assignedEmployee = backStackEntry.arguments?.getString("employee")
                )
            }
            composable("history") {
                HistoryScreen(navController = navController)
            }
            composable("report_issues") {
                ReportIssueHistoryScreen(navController = navController)
            }
            composable("repair_photo_upload_entry") {
                RepairPhotoUploadEntryScreen(navController = navController)
            }
            composable("repair_photo_upload/{ticketRef}") { backStackEntry ->
                val ticketRef = backStackEntry.arguments?.getString("ticketRef") ?: ""
                RepairPhotoUploadScreen(navController = navController, ticketRef = ticketRef)
            }
            composable("repair_portal") {
                com.example.yakultscanner.ui.screens.RepairPortalHomeScreen(navController = navController)
            }
            composable("repair_ticket_create") {
                com.example.yakultscanner.ui.screens.RepairTicketCreateScreen(navController = navController)
            }
            composable(
                route = "repair_ticket_detail/{ticketId}",
                arguments = listOf(navArgument("ticketId") { type = NavType.IntType })
            ) { backStackEntry ->
                val ticketId = backStackEntry.arguments?.getInt("ticketId") ?: 0
                com.example.yakultscanner.ui.screens.RepairTicketDetailScreen(navController = navController, ticketId = ticketId)
            }
            composable(
                route = "repair_ticket_tools/{ticketId}",
                arguments = listOf(navArgument("ticketId") { type = NavType.IntType })
            ) { backStackEntry ->
                val ticketId = backStackEntry.arguments?.getInt("ticketId") ?: 0
                com.example.yakultscanner.ui.screens.RepairTicketTechnicianToolsScreen(navController = navController, ticketId = ticketId)
            }
            composable("uploaded/{setCode}") { backStackEntry ->
                val setCode = backStackEntry.arguments?.getString("setCode") ?: ""
                UploadedItemsScreen(
                    setCode = setCode,
                    onNavigateBack = { navController.popBackStack() }
                )
            }
            composable("help") {
                HowToUseScreen(onNavigateBack = { navController.popBackStack() })
            }
            composable("app_info") {
                AppInfoScreen(
                    onNavigateBack = { navController.popBackStack() },
                    isDarkMode = isDarkMode,
                    onThemeToggle = onThemeToggle
                )
            }
            composable("batch_serial_entry") {
                BatchSerialEntryScreen(navController = navController)
            }
            composable("direct_serial_scan") {
                DirectSerialScanScreen(navController = navController)
            }
            composable("local_serial_scan_report") {
                ModernLocalSerialScanReportScreen(navController = navController)
            }
            composable("serial_set_identifier") {
                SerialSetIdentifierScreen(navController = navController)
            }
            composable("transmittal_scan") {
                com.example.yakultscanner.ui.screens.TransmittalScanScreen(navController = navController)
            }
            composable("gatepass_scan") {
                com.example.yakultscanner.ui.screens.GatepassScanScreen(navController = navController)
            }
            composable("processed_updates") {
                ProcessedUpdatesScreen(navController = navController)
            }
            composable("pending_updates") {
                ModernPendingUpdatesScreen(navController = navController)
            }
            composable("borrow_items") {
                BorrowItemsScreen(navController = navController)
            }
            composable("borrow_add_item") {
                BorrowAddItemScreen(navController = navController)
            }
            composable("borrow_records") {
                BorrowRecordsScreen(navController = navController)
            }
            composable("reports") {
                ReportsScreen(navController = navController)
            }
            composable(
                route = "reports_module/{moduleId}?range={range}",
                arguments = listOf(
                    navArgument("moduleId") { type = NavType.StringType },
                    navArgument("range") { type = NavType.StringType; nullable = true; defaultValue = "7d" }
                )
            ) { backStackEntry ->
                val moduleId = backStackEntry.arguments?.getString("moduleId") ?: "dispatch"
                val range = backStackEntry.arguments?.getString("range") ?: "7d"
                ReportModuleDetailScreen(
                    navController = navController,
                    moduleId = moduleId,
                    range = range
                )
            }
            composable("call_dashboard") {
                CallDashboardScreen(navController = navController)
            }
            composable("call_monitoring") {
                com.example.yakultscanner.ui.screens.ItcmQueueScreen(navController = navController)
            }
            composable(
                route = "call_ticket_detail/{ticketId}",
                arguments = listOf(navArgument("ticketId") { type = NavType.IntType })
            ) { backStackEntry ->
                val ticketId = backStackEntry.arguments?.getInt("ticketId") ?: 0
                com.example.yakultscanner.ui.screens.ItcmTicketDetailScreen(navController = navController, ticketId = ticketId)
            }
            composable(
                route = "field_work_detail/{ticketId}",
                arguments = listOf(navArgument("ticketId") { type = NavType.IntType })
            ) { backStackEntry ->
                val ticketId = backStackEntry.arguments?.getInt("ticketId") ?: 0
                com.example.yakultscanner.ui.screens.FieldWorkDetailScreen(navController = navController, ticketId = ticketId)
            }
            composable("call_ticket_create") {
                com.example.yakultscanner.ui.screens.ItcmCreateTicketScreen(navController = navController)
            }
            composable(
                route = "call_ticket_resolve/{ticketId}",
                arguments = listOf(navArgument("ticketId") { type = NavType.IntType })
            ) { backStackEntry ->
                val tid = backStackEntry.arguments?.getInt("ticketId") ?: 0
                com.example.yakultscanner.ui.screens.ItcmResolutionScreen(navController = navController, ticketId = tid)
            }
        }
    }
}

}

}
}
