package com.example.yakultscanner.ui.components

import android.widget.Toast
import android.net.Uri
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.width
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.ui.unit.dp
import com.example.yakultscanner.ConnectionHealthStore
import com.example.yakultscanner.api.ApiClient
import com.example.yakultscanner.settings.ApiSettings
import com.example.yakultscanner.settings.ApiEnvironment
import com.example.yakultscanner.settings.ApiEnvironmentOption
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import okhttp3.MediaType.Companion.toMediaTypeOrNull
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import java.util.concurrent.TimeUnit

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ConnectionSettingsDialog(onDismiss: () -> Unit) {
    val context = LocalContext.current
    val scope = rememberCoroutineScope()

    val existing = ApiSettings.apiBaseUrl
        .removeSuffix("/")
        .removePrefix("http://")
        .removePrefix("https://")

    val existingHost = existing.substringBefore(":").ifBlank { "192.168.27.215" }
    val existingPort = existing.substringAfter(":", "80").ifBlank { "80" }

    var host by remember { mutableStateOf(existingHost) }
    var port by remember { mutableStateOf(existingPort) }
    var environment by remember { mutableStateOf(ApiSettings.selectedEnvironment) }
    var isTesting by remember { mutableStateOf(false) }
    var testResult by remember { mutableStateOf<String?>(null) }

    fun buildUrl(): String {
        val safeHost = host.trim().removePrefix("http://").removePrefix("https://").removeSuffix("/")
        val safePort = port.trim().ifBlank { "80" }
        return "http://$safeHost:$safePort/"
    }

    fun applyApprovedEnvironment(option: ApiEnvironmentOption) {
        environment = option.environment
        val uri = Uri.parse(option.baseUrl)
        host = uri.host.orEmpty()
        port = if (uri.port == -1) "80" else uri.port.toString()
    }

    fun save() {
        if (host.trim().isBlank()) {
            Toast.makeText(context, "Enter the server address first.", Toast.LENGTH_LONG).show()
            return
        }

        val url = buildUrl()
        val approved = ApiSettings.approvedEnvironments.firstOrNull { option ->
            option.baseUrl.trimEnd('/') == url.trimEnd('/')
        }
        if (approved != null) ApiSettings.selectEnvironment(approved.environment) else ApiSettings.setBaseUrl(url)
        ApiClient.clearCachedService()
        ConnectionHealthStore.updateBaseUrl(url)
        Toast.makeText(context, "Connection settings saved.", Toast.LENGTH_LONG).show()
        onDismiss()
    }

    fun test() {
        if (host.trim().isBlank()) {
            testResult = "Enter the server address first."
            return
        }

        val url = buildUrl()
        isTesting = true
        testResult = "Testing..."
        scope.launch {
            val result = withContext(Dispatchers.IO) {
                try {
                    val client = OkHttpClient.Builder()
                        .connectTimeout(5, TimeUnit.SECONDS)
                        .readTimeout(5, TimeUnit.SECONDS)
                        .followRedirects(true)
                        .build()
                    
                    val baseUrl = url.trimEnd('/')
                    val tests = mutableListOf<String>()
                    var serverReachable = false
                    
                    // Test 1: Base URL (server is running)
                    try {
                        val request = Request.Builder().url(baseUrl).get().build()
                        client.newCall(request).execute().use { resp ->
                            if (resp.isSuccessful) {
                                serverReachable = true
                                tests.add("✅ Server is running")
                                // Check if it's the Yakult API by looking at response
                                val body = resp.body?.string() ?: ""
                                if (body.contains("Yakult") || body.contains("API")) {
                                    tests.add("✅ Yakult API detected")
                                }
                            } else {
                                tests.add("⚠️ Server responded with ${resp.code}")
                            }
                        }
                    } catch (e: Exception) {
                        return@withContext "❌ Cannot reach server at $baseUrl\nError: ${e.message?.take(50)}"
                    }
                    
                    if (!serverReachable) {
                        return@withContext "❌ Server not reachable at $baseUrl"
                    }
                    
                    // Test 2: Try modern REST API endpoints
                    val apiTests = listOf(
                        "$baseUrl/mobile-auth-login.ashx" to "POST",
                        "$baseUrl/api/health" to "GET",
                        "$baseUrl/api/SetUpdates" to "GET"
                    )
                    
                    var apiEndpointsFound = 0
                    for ((testUrl, method) in apiTests) {
                        try {
                            val requestBody = if (method == "POST") {
                                "{}".toRequestBody("application/json".toMediaTypeOrNull())
                            } else null
                            val request = Request.Builder()
                                .url(testUrl)
                                .method(method, requestBody)
                                .build()
                            client.newCall(request).execute().use { resp ->
                                if (resp.code != 404) {
                                    apiEndpointsFound++
                                }
                            }
                        } catch (e: Exception) {
                            // Ignore individual endpoint errors
                        }
                    }
                    
                    if (apiEndpointsFound > 0) {
                        tests.add("✅ Modern REST API detected ($apiEndpointsFound endpoints)")
                    } else {
                        tests.add("⚠️ No modern REST API endpoints found")
                        tests.add("   (Server may be legacy API2 with .ashx only)")
                    }
                    
                    // Test 3: Try legacy .ashx endpoints
                    val legacyTests = listOf(
                        "$baseUrl/mobile-claim.ashx",
                        "$baseUrl/set-images.ashx?token=test",
                        "$baseUrl/get-image.ashx?id=1"
                    )
                    
                    var legacyEndpointsFound = 0
                    for (testUrl in legacyTests) {
                        try {
                            val request = Request.Builder().url(testUrl).get().build()
                            client.newCall(request).execute().use { resp ->
                                if (resp.code != 404) {
                                    legacyEndpointsFound++
                                }
                            }
                        } catch (e: Exception) {
                            // Ignore
                        }
                    }
                    
                    if (legacyEndpointsFound > 0) {
                        tests.add("✅ Legacy API2 detected (.ashx endpoints)")
                    }
                    
                    tests.joinToString("\n")
                    
                } catch (e: Exception) {
                    "❌ Connection test failed: ${e.message?.take(50)}"
                }
            }
            testResult = result
            isTesting = false
        }
    }

    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Connection") },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                Text("Configure backend connection and test reachability.")
                Text("Approved environment")
                Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    ApiSettings.approvedEnvironments.forEach { option ->
                        val selected = environment == option.environment
                        if (selected) {
                            Button(
                                onClick = { applyApprovedEnvironment(option) },
                                modifier = Modifier.weight(1f)
                            ) { Text(option.label) }
                        } else {
                            OutlinedButton(
                                onClick = { applyApprovedEnvironment(option) },
                                modifier = Modifier.weight(1f)
                            ) { Text(option.label) }
                        }
                    }
                }
                if (environment == ApiEnvironment.CUSTOM) {
                    Text("Custom diagnostic URL — verify it carefully before saving.", style = androidx.compose.material3.MaterialTheme.typography.bodySmall)
                }

                Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                    OutlinedTextField(
                        value = host,
                        onValueChange = { host = it; environment = ApiEnvironment.CUSTOM },
                        label = { Text("Server IP / Host") },
                        modifier = Modifier.weight(1f),
                        singleLine = true
                    )
                    OutlinedTextField(
                        value = port,
                        onValueChange = { port = it.filter(Char::isDigit); environment = ApiEnvironment.CUSTOM },
                        label = { Text("Port") },
                        modifier = Modifier.width(110.dp),
                        singleLine = true,
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number)
                    )
                }

                Text("URL: ${buildUrl()}")

                if (testResult != null) {
                    Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                        if (isTesting) {
                            CircularProgressIndicator(modifier = Modifier.height(18.dp))
                        }
                        Text(testResult ?: "")
                    }
                }
            }
        },
        confirmButton = {
            Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                TextButton(onClick = { test() }, enabled = !isTesting) { Text("Test") }
                Button(onClick = { save() }, enabled = !isTesting) { Text("Save") }
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) { Text("Close") }
        }
    )
}
