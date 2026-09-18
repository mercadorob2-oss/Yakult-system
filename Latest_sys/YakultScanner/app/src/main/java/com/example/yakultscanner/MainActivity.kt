package com.example.yakultscanner

import android.Manifest
import android.content.pm.PackageManager
import android.os.Bundle
import android.widget.Toast
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.core.content.ContextCompat
import com.example.yakultscanner.navigation.AppNavigator
import com.example.yakultscanner.settings.ThemePreferences
import com.example.yakultscanner.ui.theme.YakultScannerTheme
import com.example.yakultscanner.UserSession
import com.example.yakultscanner.HoneywellScanReceiver
import com.example.yakultscanner.HONEYWELL_SCAN_ACTION
import dagger.hilt.android.AndroidEntryPoint

@AndroidEntryPoint(ComponentActivity::class)
@Suppress("UnprotectedReceiver")
class MainActivity : Hilt_MainActivity() {
    private var honeywellReceiver: HoneywellScanReceiver? = null
    private val cameraPermissionRequest =
        registerForActivityResult(ActivityResultContracts.RequestPermission()) { isGranted ->
            if (!isGranted) {
                Toast.makeText(this, "Camera permission is required.", Toast.LENGTH_SHORT).show()
            }
        }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        // Initialize persisted session so currentUser is available for UI (logout button visibility, etc.)
        UserSession.init(applicationContext)
        honeywellReceiver = HoneywellScanReceiver()

        val filter = android.content.IntentFilter().apply {
            addAction(HONEYWELL_SCAN_ACTION)
            addAction(HONEYWELL_DEFAULT_ACTION)
        }
        ContextCompat.registerReceiver(
            this,
            honeywellReceiver,
            filter,
            ContextCompat.RECEIVER_EXPORTED
        )

        setContent {
            var darkModeEnabled by remember {
                mutableStateOf(ThemePreferences.isDarkModeEnabled(this))
            }

            YakultScannerTheme(darkTheme = darkModeEnabled) {
                AppNavigator(
                    isDarkMode = darkModeEnabled,
                    onThemeToggle = {
                        val next = !darkModeEnabled
                        darkModeEnabled = next
                        ThemePreferences.setDarkModeEnabled(this, next)
                    }
                )
            }
        }
        
        // Request camera permission on start if not granted
        requestCameraPermission()
    }

    override fun onDestroy() {
        super.onDestroy()
        honeywellReceiver?.let { unregisterReceiver(it) }
    }

    private fun requestCameraPermission() {
        if (ContextCompat.checkSelfPermission(this, Manifest.permission.CAMERA)
            != PackageManager.PERMISSION_GRANTED
        ) {
            cameraPermissionRequest.launch(Manifest.permission.CAMERA)
        }
    }
}
