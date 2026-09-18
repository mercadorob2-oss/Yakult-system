package com.example.yakultscanner.ui.components

import android.graphics.Bitmap
import android.util.Base64
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.activity.result.PickVisualMediaRequest
import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowForward
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.Error
import androidx.compose.material.icons.filled.History
import androidx.compose.material.icons.filled.LocationOn
import androidx.compose.material.icons.filled.PhotoLibrary
import androidx.compose.material.icons.filled.PhotoCamera
import androidx.compose.material.icons.filled.QrCodeScanner
import androidx.compose.material.icons.filled.Share
import androidx.compose.material.icons.filled.Draw
import androidx.compose.material.icons.filled.Verified
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.yakultscanner.api.SetConfirmationRequest
import kotlinx.coroutines.launch
import java.io.ByteArrayOutputStream

sealed class ConfirmationStep {
    object Review : ConfirmationStep()
    object Signature : ConfirmationStep()
    object Photo : ConfirmationStep()
    object Confirming : ConfirmationStep()
    data class Success(val confirmationId: String) : ConfirmationStep()
    data class Error(val message: String) : ConfirmationStep()
}

@Composable
fun DeploymentConfirmationDialog(
    setCode: String,
    employeeName: String,
    onDismiss: () -> Unit,
    onConfirm: (SetConfirmationRequest) -> Unit,
    modifier: Modifier = Modifier
) {
    var currentStep by remember { mutableStateOf<ConfirmationStep>(ConfirmationStep.Review) }
    var signatureView by remember { mutableStateOf<SignatureView?>(null) }
    var photoBitmap by remember { mutableStateOf<Bitmap?>(null) }
    var notes by remember { mutableStateOf("") }
    
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    
    // Photo picker
    val photoPicker = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.PickVisualMedia()
    ) { uri ->
        uri?.let {
            // Load bitmap from URI
            val bitmap = try {
                context.contentResolver.openInputStream(uri)?.use { stream ->
                    android.graphics.BitmapFactory.decodeStream(stream)
                }
            } catch (e: Exception) { null }
            photoBitmap = bitmap
        }
    }
    
    AlertDialog(
        onDismissRequest = { 
            if (currentStep !is ConfirmationStep.Confirming) onDismiss() 
        },
        modifier = modifier,
        title = {
            Row(verticalAlignment = Alignment.CenterVertically) {
                when (currentStep) {
                    is ConfirmationStep.Review -> Icon(Icons.Default.Verified, null)
                    is ConfirmationStep.Signature -> Icon(Icons.Default.Draw, null)
                    is ConfirmationStep.Photo -> Icon(Icons.Default.PhotoCamera, null)
                    is ConfirmationStep.Confirming -> CircularProgressIndicator(modifier = Modifier.size(24.dp))
                    is ConfirmationStep.Success -> Icon(Icons.Default.CheckCircle, null, tint = androidx.compose.ui.graphics.Color(0xFF4CAF50))
                    is ConfirmationStep.Error -> Icon(Icons.Default.Error, null, tint = androidx.compose.ui.graphics.Color(0xFFF44336))
                }
                Spacer(modifier = Modifier.width(8.dp))
                Text(
                    text = when (currentStep) {
                        is ConfirmationStep.Review -> "Confirm Deployment"
                        is ConfirmationStep.Signature -> "Sign to Confirm"
                        is ConfirmationStep.Photo -> "Add Photo (Optional)"
                        is ConfirmationStep.Confirming -> "Processing..."
                        is ConfirmationStep.Success -> "Deployed!"
                        is ConfirmationStep.Error -> "Failed"
                    },
                    fontWeight = FontWeight.Bold
                )
            }
        },
        text = {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .verticalScroll(rememberScrollState()),
                verticalArrangement = Arrangement.spacedBy(16.dp)
            ) {
                when (currentStep) {
                    is ConfirmationStep.Review -> {
                        // Review step
                        Surface(
                            color = MaterialTheme.colorScheme.surfaceVariant,
                            shape = RoundedCornerShape(8.dp)
                        ) {
                            Column(modifier = Modifier.padding(12.dp)) {
                                Text("📦 Set: #$setCode", fontWeight = FontWeight.Medium)
                                Text("👤 To: $employeeName", fontSize = 14.sp)
                            }
                        }
                        
                        OutlinedTextField(
                            value = notes,
                            onValueChange = { notes = it },
                            label = { Text("Notes (optional)") },
                            placeholder = { Text("e.g., Handed to security guard") },
                            minLines = 2,
                            modifier = Modifier.fillMaxWidth()
                        )
                        
                        Text(
                            text = "Next: You'll sign to confirm receipt",
                            fontSize = 12.sp,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                    
                    is ConfirmationStep.Signature -> {
                        Text(
                            text = "Please sign below to confirm deployment:",
                            fontSize = 14.sp
                        )
                        
                        SignaturePad(
                            label = "Your Signature",
                            onViewReady = { signatureView = it }
                        )
                        
                    }
                    
                    is ConfirmationStep.Photo -> {
                        Text(
                            text = "Add a photo of the handoff (optional):",
                            fontSize = 14.sp
                        )
                        
                        if (photoBitmap != null) {
                            Image(
                                bitmap = photoBitmap!!.asImageBitmap(),
                                contentDescription = "Selected photo",
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .height(200.dp)
                                    .clip(RoundedCornerShape(8.dp)),
                                contentScale = ContentScale.Crop
                            )
                            TextButton(
                                onClick = { photoBitmap = null },
                                modifier = Modifier.align(Alignment.CenterHorizontally)
                            ) {
                                Text("Remove Photo")
                            }
                        } else {
                            OutlinedButton(
                                onClick = {
                                    photoPicker.launch(
                                        PickVisualMediaRequest(ActivityResultContracts.PickVisualMedia.ImageOnly)
                                    )
                                },
                                modifier = Modifier.fillMaxWidth()
                            ) {
                                Icon(Icons.Default.PhotoLibrary, contentDescription = null)
                                Spacer(modifier = Modifier.width(8.dp))
                                Text("Choose from Gallery")
                            }
                        }
                    }
                    
                    is ConfirmationStep.Confirming -> {
                        Column(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalAlignment = Alignment.CenterHorizontally,
                            verticalArrangement = Arrangement.spacedBy(16.dp)
                        ) {
                            CircularProgressIndicator()
                            Text("Uploading confirmation...")
                            Text(
                                text = "Please don't close this dialog",
                                fontSize = 12.sp,
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                        }
                    }
                    
                    is ConfirmationStep.Success -> {
                        Column(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalAlignment = Alignment.CenterHorizontally,
                            verticalArrangement = Arrangement.spacedBy(16.dp)
                        ) {
                            Icon(
                                Icons.Default.CheckCircle,
                                contentDescription = null,
                                modifier = Modifier.size(64.dp),
                                tint = androidx.compose.ui.graphics.Color(0xFF4CAF50)
                            )
                            Text(
                                text = "Deployment Confirmed!",
                                fontWeight = FontWeight.Bold,
                                fontSize = 18.sp
                            )
                            Text(
                                text = "Confirmation ID: ${(currentStep as ConfirmationStep.Success).confirmationId}",
                                fontSize = 12.sp,
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                        }
                    }
                    
                    is ConfirmationStep.Error -> {
                        Column(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalAlignment = Alignment.CenterHorizontally,
                            verticalArrangement = Arrangement.spacedBy(16.dp)
                        ) {
                            Icon(
                                Icons.Default.Error,
                                contentDescription = null,
                                modifier = Modifier.size(48.dp),
                                tint = androidx.compose.ui.graphics.Color(0xFFF44336)
                            )
                            Text(
                                text = (currentStep as ConfirmationStep.Error).message,
                                color = androidx.compose.ui.graphics.Color(0xFFF44336)
                            )
                        }
                    }
                }
            }
        },
        confirmButton = {
            when (currentStep) {
                is ConfirmationStep.Review -> {
                    Button(
                        onClick = { currentStep = ConfirmationStep.Signature }
                    ) {
                        Text("Continue →")
                    }
                }
                is ConfirmationStep.Signature -> {
                    Button(
                        onClick = { 
                            if (signatureView?.hasSignature == true) {
                                currentStep = ConfirmationStep.Photo
                            }
                        },
                        enabled = signatureView?.hasSignature == true
                    ) {
                        Text("Next →")
                    }
                }
                is ConfirmationStep.Photo -> {
                    Button(
                        onClick = {
                            currentStep = ConfirmationStep.Confirming
                            scope.launch {
                                val request = createConfirmationRequest(
                                    signatureView = signatureView,
                                    photoBitmap = photoBitmap,
                                    notes = notes
                                )
                                onConfirm(request)
                            }
                        }
                    ) {
                        Text("✓ Confirm Deployment")
                    }
                }
                is ConfirmationStep.Success -> {
                    Button(onClick = onDismiss) {
                        Text("Done")
                    }
                }
                is ConfirmationStep.Error -> {
                    Button(
                        onClick = { currentStep = ConfirmationStep.Review }
                    ) {
                        Text("Try Again")
                    }
                }
                else -> { /* No button */ }
            }
        },
        dismissButton = {
            when (currentStep) {
                is ConfirmationStep.Review,
                is ConfirmationStep.Signature,
                is ConfirmationStep.Photo,
                is ConfirmationStep.Error -> {
                    TextButton(onClick = onDismiss) {
                        Text("Cancel")
                    }
                }
                is ConfirmationStep.Success -> {
                    OutlinedButton(
                        onClick = { /* Share/Screenshot */ }
                    ) {
                        Icon(Icons.Default.Share, contentDescription = null)
                        Spacer(modifier = Modifier.width(4.dp))
                        Text("Share")
                    }
                }
                else -> { /* No button */ }
            }
        }
    )
}

private fun createConfirmationRequest(
    signatureView: SignatureView?,
    photoBitmap: Bitmap?,
    notes: String
): SetConfirmationRequest {
    val signatureBase64 = signatureView?.getSignatureBitmap()?.let { bitmap ->
        val stream = ByteArrayOutputStream()
        bitmap.compress(Bitmap.CompressFormat.PNG, 100, stream)
        Base64.encodeToString(stream.toByteArray(), Base64.DEFAULT)
    }
    
    val photoBase64 = photoBitmap?.let { bitmap ->
        val stream = ByteArrayOutputStream()
        bitmap.compress(Bitmap.CompressFormat.JPEG, 80, stream)
        Base64.encodeToString(stream.toByteArray(), Base64.DEFAULT)
    }
    
    return SetConfirmationRequest(
        signatureBase64 = signatureBase64,
        photoBase64 = photoBase64,
        gpsCoordinates = null,
        deviceId = android.os.Build.MODEL,
        notes = notes.ifBlank { null },
        confirmedBy = null // Will be filled by API with current user
    )
}
