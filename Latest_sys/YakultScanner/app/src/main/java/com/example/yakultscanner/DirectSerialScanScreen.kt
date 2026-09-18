package com.example.yakultscanner

import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.util.Base64
import android.widget.Toast
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.IntentSenderRequest
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.Image
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.CameraAlt
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.DocumentScanner
import androidx.compose.material.icons.filled.QrCodeScanner
import androidx.compose.material.icons.filled.RadioButtonUnchecked
import androidx.compose.material.icons.filled.Save
import androidx.compose.material.icons.filled.Send
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.draw.clip
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.focus.onFocusChanged
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.window.Dialog
import androidx.compose.ui.window.DialogProperties
import androidx.navigation.NavController
import androidx.navigation.compose.currentBackStackEntryAsState
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import com.example.yakultscanner.api.ApiClient
import com.example.yakultscanner.api.ApiResult
import com.example.yakultscanner.api.SerialFromMobileRequest
import com.example.yakultscanner.api.SetImageUploadRequest
import com.example.yakultscanner.api.ReceiptImageUploadRequest
import com.example.yakultscanner.api.safeApiCall
import com.example.yakultscanner.api.userMessageOr
import com.example.yakultscanner.utils.normalizeSerial
import com.example.yakultscanner.utils.parseNormalizedSerials
import com.google.mlkit.vision.documentscanner.GmsDocumentScannerOptions
import com.google.mlkit.vision.documentscanner.GmsDocumentScanning
import com.google.mlkit.vision.documentscanner.GmsDocumentScanningResult
import java.io.ByteArrayOutputStream

private data class SerialAddResult(
    val added: Int,
    val duplicates: Int
)

private data class DirectPhoneEntry(
    val cellPhoneNumber: String = "",
    val serialNumber: String = "",
    val imei1: String = "",
    val imei2: String = ""
)

private enum class DocUploadTarget(val label: String) {
    SetImage("Set Image"),
    Receipt("Receipt (SI/DR/PO)")
}

/** Invoices are rows in dbo.[Set] too (IsInvoice = 1), but identified by DocumentNumber rather
 *  than the QR-scannable SetCode Sets use, so the "which target" prompt needs to ask for a
 *  different field label and pass a different query param depending on which entry point
 *  (Set vs Invoice) the user tapped. */
private enum class DocIdentifierType(val label: String) {
    SetCode("Set Code"),
    DocumentNumber("Document #")
}

private enum class DocFileFormat(val label: String, val mimeType: String) {
    Image("Image (JPEG)", "image/jpeg"),
    Png("Image (PNG)", "image/png"),
    Pdf("PDF", "application/pdf"),
    // Combines everything into one PDF filed under the dedicated "PDF" doc-type category
    // (mirrors the desktop Receipt Set Viewer's "PDF Document" tab, which replaces SI/DR/PO
    // entirely) -- unlike DocFileFormat.Pdf, which still combines pages but stays filed under
    // whichever single SI/DR/PO type was picked. Only meaningful for Receipt uploads.
    PdfCombined("PDF Combined", "application/pdf")
}

/**
 * Combines scanned pages into a single multi-page PDF using the platform PdfDocument API (no
 * extra library needed). Each page is re-encoded through JPEG at the same quality as standalone
 * Image-mode uploads first, rather than drawing the raw full-resolution bitmap straight into the
 * PDF -- multi-page PDFs from full-res camera captures were large enough to fail uploads even
 * after raising the server's request-size limit.
 */
private fun bitmapsToPdfBytes(bitmaps: List<Bitmap>): ByteArray {
    val document = android.graphics.pdf.PdfDocument()
    bitmaps.forEachIndexed { index, bitmap ->
        val jpegStream = ByteArrayOutputStream()
        bitmap.compress(Bitmap.CompressFormat.JPEG, 85, jpegStream)
        val jpegBytes = jpegStream.toByteArray()
        val compressedBitmap = BitmapFactory.decodeByteArray(jpegBytes, 0, jpegBytes.size)

        val pageInfo = android.graphics.pdf.PdfDocument.PageInfo.Builder(compressedBitmap.width, compressedBitmap.height, index + 1).create()
        val page = document.startPage(pageInfo)
        page.canvas.drawBitmap(compressedBitmap, 0f, 0f, null)
        document.finishPage(page)
    }
    val output = ByteArrayOutputStream()
    document.writeTo(output)
    document.close()
    return output.toByteArray()
}

// Tags each scanned page with the format it was captured under, so a single session/doc-type
// can mix formats -- e.g. 2 pages scanned as Image, then 3 more scanned as PDF, all queued
// together and uploaded as separate items (grouped by format at upload time).
private data class ScannedPage(val bitmap: Bitmap, val format: DocFileFormat)

private enum class PhoneScanTarget(val label: String) {
    Auto("Auto"),
    Serial("Serial"),
    Imei1("IMEI 1"),
    Imei2("IMEI 2")
}

private enum class PhoneFocusField {
    CellPhone,
    Serial,
    Imei1,
    Imei2
}

private data class ScanSessionEvent(
    val id: Long,
    val value: String,
    val source: String,
    val status: String,
    val detail: String,
    val undoKey: String? = null,
    val undone: Boolean = false
)

private data class DuplicateScanEvent(
    val value: String,
    val source: String,
    val matchedField: String,
    val existingRow: Int?,
    val attemptedRow: Int?,
    val detail: String
)

// â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
// Send-to-Windows confirmation/preview dialog
// â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

@Composable
private fun SendToWindowsConfirmDialog(
    serials: List<String>,
    onDismiss: () -> Unit,
    onConfirm: (List<String>) -> Unit
) {
    // Each serial starts checked; user can deselect individual ones.
    val checkedState = remember(serials) {
        mutableStateMapOf<String, Boolean>().also { map ->
            serials.forEach { map[it] = true }
        }
    }

    val selectedCount = checkedState.values.count { it }
    val allChecked = selectedCount == serials.size
    val noneChecked = selectedCount == 0

    Dialog(
        onDismissRequest = onDismiss,
        properties = DialogProperties(usePlatformDefaultWidth = false)
    ) {
        Card(
            modifier = Modifier
                .fillMaxWidth(0.92f)
                .fillMaxHeight(0.82f),
            shape = RoundedCornerShape(20.dp),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
            elevation = CardDefaults.cardElevation(defaultElevation = 8.dp)
        ) {
            Column(modifier = Modifier.fillMaxSize()) {

                // â”€â”€ Header â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .background(
                            brush = Brush.verticalGradient(
                                colors = listOf(
                                    MaterialTheme.colorScheme.primary,
                                    MaterialTheme.colorScheme.primary.copy(alpha = 0.85f)
                                )
                            ),
                            shape = RoundedCornerShape(topStart = 20.dp, topEnd = 20.dp)
                        )
                        .padding(horizontal = 20.dp, vertical = 16.dp)
                ) {
                    Column {
                        Text(
                            text = "Send to Windows",
                            style = MaterialTheme.typography.titleLarge,
                            fontWeight = FontWeight.Bold,
                            color = Color.White
                        )
                        Spacer(Modifier.height(2.dp))
                        Text(
                            text = "Review and confirm the serials below",
                            style = MaterialTheme.typography.bodySmall,
                            color = Color.White.copy(alpha = 0.80f)
                        )
                    }
                }

                // â”€â”€ Summary bar â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .background(MaterialTheme.colorScheme.secondaryContainer.copy(alpha = 0.45f))
                        .padding(horizontal = 20.dp, vertical = 10.dp),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.SpaceBetween
                ) {
                    Text(
                        text = if (selectedCount > 0)
                            "$selectedCount of ${serials.size} serial(s) selected"
                        else
                            "None selected",
                        style = MaterialTheme.typography.bodyMedium,
                        fontWeight = FontWeight.SemiBold,
                        color = MaterialTheme.colorScheme.onSecondaryContainer
                    )

                    // Toggle all / none
                    TextButton(
                        onClick = {
                            val newState = !allChecked
                            serials.forEach { checkedState[it] = newState }
                        },
                        contentPadding = PaddingValues(horizontal = 8.dp, vertical = 4.dp)
                    ) {
                        Text(
                            text = if (allChecked) "Deselect all" else "Select all",
                            style = MaterialTheme.typography.labelMedium,
                            color = MaterialTheme.colorScheme.primary
                        )
                    }
                }

                HorizontalDivider(thickness = 0.5.dp, color = MaterialTheme.colorScheme.outlineVariant)

                // â”€â”€ Serial list â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                LazyColumn(
                    modifier = Modifier
                        .weight(1f)
                        .fillMaxWidth(),
                    contentPadding = PaddingValues(horizontal = 16.dp, vertical = 8.dp),
                    verticalArrangement = Arrangement.spacedBy(6.dp)
                ) {
                    itemsIndexed(serials) { index, serial ->
                        val isChecked = checkedState[serial] == true

                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .clickable { checkedState[serial] = !isChecked }
                                .background(
                                    color = if (isChecked)
                                        MaterialTheme.colorScheme.primaryContainer.copy(alpha = 0.35f)
                                    else
                                        MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.3f),
                                    shape = RoundedCornerShape(10.dp)
                                )
                                .border(
                                    width = if (isChecked) 1.dp else 0.5.dp,
                                    color = if (isChecked)
                                        MaterialTheme.colorScheme.primary.copy(alpha = 0.4f)
                                    else
                                        MaterialTheme.colorScheme.outlineVariant,
                                    shape = RoundedCornerShape(10.dp)
                                )
                                .padding(horizontal = 12.dp, vertical = 10.dp),
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            // Row number badge
                            Box(
                                modifier = Modifier
                                    .size(26.dp)
                                    .background(
                                        color = if (isChecked)
                                            MaterialTheme.colorScheme.primary
                                        else
                                            MaterialTheme.colorScheme.outline.copy(alpha = 0.3f),
                                        shape = CircleShape
                                    ),
                                contentAlignment = Alignment.Center
                            ) {
                                Text(
                                    text = (index + 1).toString(),
                                    color = if (isChecked) Color.White
                                            else MaterialTheme.colorScheme.onSurfaceVariant,
                                    style = MaterialTheme.typography.labelSmall,
                                    fontWeight = FontWeight.Bold
                                )
                            }

                            Spacer(Modifier.width(10.dp))

                            // Serial number
                            Text(
                                text = serial,
                                modifier = Modifier.weight(1f),
                                style = MaterialTheme.typography.bodyLarge,
                                fontFamily = FontFamily.Monospace,
                                fontWeight = if (isChecked) FontWeight.SemiBold else FontWeight.Normal,
                                color = if (isChecked)
                                    MaterialTheme.colorScheme.onSurface
                                else
                                    MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.6f),
                                maxLines = 1,
                                overflow = TextOverflow.Ellipsis
                            )

                            // Checkbox icon
                            Icon(
                                imageVector = if (isChecked)
                                    Icons.Default.CheckCircle
                                else
                                    Icons.Default.RadioButtonUnchecked,
                                contentDescription = if (isChecked) "Selected" else "Not selected",
                                tint = if (isChecked)
                                    MaterialTheme.colorScheme.primary
                                else
                                    MaterialTheme.colorScheme.outline.copy(alpha = 0.4f),
                                modifier = Modifier.size(22.dp)
                            )
                        }
                    }
                }

                HorizontalDivider(thickness = 0.5.dp, color = MaterialTheme.colorScheme.outlineVariant)

                // â”€â”€ Action buttons â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(horizontal = 16.dp, vertical = 12.dp),
                    horizontalArrangement = Arrangement.spacedBy(10.dp)
                ) {
                    OutlinedButton(
                        onClick = onDismiss,
                        modifier = Modifier
                            .weight(1f)
                            .height(48.dp),
                        shape = RoundedCornerShape(12.dp)
                    ) {
                        Text("Cancel", fontWeight = FontWeight.SemiBold)
                    }

                    Button(
                        onClick = {
                            val toSend = serials.filter { checkedState[it] == true }
                            if (toSend.isNotEmpty()) onConfirm(toSend)
                        },
                        enabled = !noneChecked,
                        modifier = Modifier
                            .weight(2f)
                            .height(48.dp),
                        shape = RoundedCornerShape(12.dp),
                        colors = ButtonDefaults.buttonColors(
                            containerColor = MaterialTheme.colorScheme.primary
                        )
                    ) {
                        Icon(Icons.Default.Send, null, Modifier.size(18.dp))
                        Spacer(Modifier.width(6.dp))
                        Text(
                            text = if (noneChecked) "Select serials"
                                   else "Send $selectedCount Serial(s)",
                            fontWeight = FontWeight.Bold
                        )
                    }
                }
            }
        }
    }
}

// â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DirectSerialScanScreen(navController: NavController) {
    val context = LocalContext.current
    val haptic = androidx.compose.ui.platform.LocalHapticFeedback.current

    var currentSerial by remember { mutableStateOf("") }
    var serialNumbers by remember { mutableStateOf(listOf<String>()) }
    // Null while still loading (or if the fetch failed) -- the Supplier field skips the
    // exists-in-list restriction in that case rather than blocking every scan on a network hiccup.
    var vendorNames by remember { mutableStateOf<List<String>?>(null) }
    LaunchedEffect(Unit) {
        try {
            val response = ApiClient.service.getVendors()
            if (response.isSuccessful) {
                vendorNames = response.body().orEmpty()
                    .mapNotNull { it.vendorName.trim().takeIf { name -> name.isNotBlank() } }
                    .distinctBy { it.lowercase() }
                    .sortedWith(String.CASE_INSENSITIVE_ORDER)
            }
        } catch (e: Exception) {
            vendorNames = null
        }
    }
    var phoneMode by remember { mutableStateOf(false) }
    var phoneRowCount by remember { mutableStateOf("") }
    var phoneRows by remember { mutableStateOf(listOf(DirectPhoneEntry())) }
    val snackbarHostState = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()
    var isSending by remember { mutableStateOf(false) }
    var showConfirmDialog by remember { mutableStateOf(false) }
    var phoneScanTarget by remember { mutableStateOf(PhoneScanTarget.Auto) }
    var bulkImportText by remember { mutableStateOf("") }
    var eventCounter by remember { mutableStateOf(0L) }
    var scanLog by remember { mutableStateOf(listOf<ScanSessionEvent>()) }
    var duplicateScans by remember { mutableStateOf(listOf<DuplicateScanEvent>()) }
    var showSessionTools by remember { mutableStateOf(false) }
    var focusedPhoneRowIndex by remember { mutableStateOf(0) }
    var focusedPhoneField by remember { mutableStateOf(PhoneFocusField.Serial) }

    // ── Scan Documents for a Set (ML Kit document scanner) ──────────────────────────
    // This screen has no Set context of its own (pure serial/IMEI entry), so scanning a
    // document here has nowhere to attach to until the user tells us which Set it's for.
    var showSetCodePrompt by remember { mutableStateOf(false) }
    var pendingIdentifierType by remember { mutableStateOf(DocIdentifierType.SetCode) }
    var confirmedIdentifierType by remember { mutableStateOf(DocIdentifierType.SetCode) }
    var pendingSetCode by remember { mutableStateOf("") }
    var pendingUploadTarget by remember { mutableStateOf(DocUploadTarget.SetImage) }
    var pendingFileFormat by remember { mutableStateOf(DocFileFormat.Image) }
    var pendingDocType by remember { mutableStateOf("SI") }
    var pendingSupplier by remember { mutableStateOf("") }
    var pendingSiNumber by remember { mutableStateOf("") }
    var pendingDrNumber by remember { mutableStateOf("") }
    var pendingPoNumber by remember { mutableStateOf("") }
    var confirmedSetCode by remember { mutableStateOf<String?>(null) }
    var confirmedUploadTarget by remember { mutableStateOf(DocUploadTarget.SetImage) }
    var confirmedFileFormat by remember { mutableStateOf(DocFileFormat.Image) }
    var confirmedSupplier by remember { mutableStateOf("") }
    var confirmedSiNumber by remember { mutableStateOf("") }
    var confirmedDrNumber by remember { mutableStateOf("") }
    var confirmedPoNumber by remember { mutableStateOf("") }
    // Receipt pages are queued per doc type (SI/DR/PO) so a user can scan SI, switch to DR,
    // switch to PO, and upload everything together in one session -- previously this was a
    // single flat list shared across types, forcing an upload before a different type could be
    // selected. Set Image uploads have no doc-type concept so they keep a single list. Each page
    // carries its own DocFileFormat (see ScannedPage) so a single doc type can mix Image and PDF
    // pages in one session -- e.g. 2 pages scanned as Image, then 3 more as PDF, all queued
    // together and uploaded as separate items grouped by format.
    var receiptPagesByType by remember { mutableStateOf(mapOf("SI" to emptyList<ScannedPage>(), "DR" to emptyList<ScannedPage>(), "PO" to emptyList<ScannedPage>(), "PDF" to emptyList<ScannedPage>())) }
    var setImagePages by remember { mutableStateOf<List<ScannedPage>>(emptyList()) }
    var activeDocType by remember { mutableStateOf("SI") }
    // Format the NEXT "Add More Pages" scan will be tagged with -- defaults to whatever was
    // picked in the initial confirm dialog, but can be changed before each subsequent scan.
    var nextScanFormat by remember { mutableStateOf(DocFileFormat.Image) }
    var isDocUploading by remember { mutableStateOf(false) }
    var previewedPageIndex by remember { mutableStateOf<Int?>(null) }
    var uploadErrorDialogMessage by remember { mutableStateOf<String?>(null) }

    fun currentPages(): List<ScannedPage> =
        if (confirmedUploadTarget == DocUploadTarget.Receipt) receiptPagesByType[activeDocType].orEmpty()
        else setImagePages

    fun hasAnyQueuedPages(): Boolean =
        if (confirmedUploadTarget == DocUploadTarget.Receipt) receiptPagesByType.values.any { it.isNotEmpty() }
        else setImagePages.isNotEmpty()

    fun totalQueuedPages(): Int =
        if (confirmedUploadTarget == DocUploadTarget.Receipt) receiptPagesByType.values.sumOf { it.size }
        else setImagePages.size

    // Rotating the device destroys and recreates the Activity (no android:configChanges override
    // for orientation), which wipes every plain `remember` in this composable -- including all
    // queued scanned Bitmaps. Persisting them across that recreation via rememberSaveable isn't
    // safe either: Bitmaps go through the instance-state Bundle (a Binder transaction with a
    // ~1MB total limit), and a few full-resolution scanned pages would blow past that and crash
    // with TransactionTooLargeException. Locking orientation while a session is active sidesteps
    // the problem by never letting the destructive rotation happen in the first place.
    LaunchedEffect(receiptPagesByType, setImagePages) {
        val activity = context as? android.app.Activity ?: return@LaunchedEffect
        activity.requestedOrientation = if (hasAnyQueuedPages())
            android.content.pm.ActivityInfo.SCREEN_ORIENTATION_LOCKED
        else
            android.content.pm.ActivityInfo.SCREEN_ORIENTATION_UNSPECIFIED
    }
    DisposableEffect(Unit) {
        onDispose {
            (context as? android.app.Activity)?.requestedOrientation =
                android.content.pm.ActivityInfo.SCREEN_ORIENTATION_UNSPECIFIED
        }
    }

    fun clearQueuedPages() {
        receiptPagesByType = mapOf("SI" to emptyList(), "DR" to emptyList(), "PO" to emptyList(), "PDF" to emptyList())
        setImagePages = emptyList()
    }

    val documentScannerOptions = remember {
        GmsDocumentScannerOptions.Builder()
            .setGalleryImportAllowed(false)
            .setPageLimit(20)
            .setResultFormats(GmsDocumentScannerOptions.RESULT_FORMAT_JPEG)
            .setScannerMode(GmsDocumentScannerOptions.SCANNER_MODE_FULL)
            .build()
    }

    val documentScanLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.StartIntentSenderForResult()
    ) { activityResult ->
        if (activityResult.resultCode == android.app.Activity.RESULT_OK) {
            val scanResult = GmsDocumentScanningResult.fromActivityResultIntent(activityResult.data)
            val pageUris = scanResult?.pages.orEmpty().map { it.imageUri }
            // Decoding full-resolution scanned pages is real work -- doing it synchronously on
            // this ActivityResultCallback (which runs on the main thread) froze the UI for
            // multi-page/full-res scans. Moved to Dispatchers.IO.
            scope.launch {
                val bitmaps = withContext(Dispatchers.IO) {
                    pageUris.mapNotNull { uri ->
                        try {
                            context.contentResolver.openInputStream(uri)?.use { stream ->
                                BitmapFactory.decodeStream(stream)
                            }
                        } catch (e: Exception) { null }
                    }
                }
                if (bitmaps.isNotEmpty()) {
                    // Append rather than replace so "Add More Pages" can build up a batch across
                    // multiple scanner sessions. For Receipt uploads this appends to whichever
                    // doc-type tab (SI/DR/PO) is currently active, so scanning DR pages doesn't
                    // touch the SI pages already queued. Each page is tagged with nextScanFormat
                    // so different scans within the same doc type can carry different formats.
                    val newPages = bitmaps.map { ScannedPage(it, nextScanFormat) }
                    if (confirmedUploadTarget == DocUploadTarget.Receipt) {
                        receiptPagesByType = receiptPagesByType.toMutableMap().apply {
                            this[activeDocType] = this[activeDocType].orEmpty() + newPages
                        }
                    } else {
                        setImagePages = setImagePages + newPages
                    }
                } else {
                    Toast.makeText(context, "No pages captured.", Toast.LENGTH_SHORT).show()
                }
            }
        }
    }

    fun launchDocumentScanner() {
        val activity = context as? android.app.Activity
        if (activity == null) {
            Toast.makeText(context, "Unable to start scanner.", Toast.LENGTH_SHORT).show()
            return
        }
        GmsDocumentScanning.getClient(documentScannerOptions)
            .getStartScanIntent(activity)
            .addOnSuccessListener { intentSender ->
                documentScanLauncher.launch(IntentSenderRequest.Builder(intentSender).build())
            }
            .addOnFailureListener { e ->
                Toast.makeText(context, "Unable to start scanner: ${e.message}", Toast.LENGTH_LONG).show()
            }
    }

    fun uploadScannedDocPages() {
        val identifierValue = confirmedSetCode?.trim().orEmpty()
        if (identifierValue.isBlank() || !hasAnyQueuedPages()) return
        val setCode = if (confirmedIdentifierType == DocIdentifierType.SetCode) identifierValue else null
        val documentNumberValue = if (confirmedIdentifierType == DocIdentifierType.DocumentNumber) identifierValue else null

        // One batch per (doc type, format) combination -- so if a doc type has both Image and PDF
        // pages queued (mixed within one session), each format group uploads as its own item(s)
        // instead of forcing everything in a doc type to share one format. Set Image uploads have
        // no doc-type concept, so they're just grouped by format directly.
        val batches: List<Triple<String, DocFileFormat, List<Bitmap>>> = buildList {
            if (confirmedUploadTarget == DocUploadTarget.Receipt) {
                receiptPagesByType.forEach { (docType, pages) ->
                    pages.groupBy { it.format }.forEach { (format, grouped) ->
                        if (grouped.isNotEmpty()) add(Triple(docType, format, grouped.map { it.bitmap }))
                    }
                }
            } else {
                setImagePages.groupBy { it.format }.forEach { (format, grouped) ->
                    if (grouped.isNotEmpty()) add(Triple("", format, grouped.map { it.bitmap }))
                }
            }
        }

        scope.launch {
            isDocUploading = true
            val currentUser = UserSession.currentUser?.displayName ?: "Mobile User"
            var successCount = 0
            var failCount = 0
            var typeCount = 0
            // Surfaces the actual server/network reason for the LAST failure in the toast --
            // previously this only showed a bare "N failed" count, making it impossible to tell
            // a rejected doc_type from a network error from a validation failure without
            // reproducing the issue on a dev machine with logs.
            var lastFailureReason: String? = null
            // Tracks the ReceiptSetId returned by the first successful upload so every
            // subsequent item in this batch -- across ALL doc types and formats -- attaches to
            // the SAME receipt instead of each one creating its own separate ReceiptSet row.
            var activeReceiptSetId: Int? = null

            val seenDocTypes = mutableSetOf<String>()
            for ((docType, format, pages) in batches) {
                if (pages.isEmpty()) continue
                seenDocTypes.add(docType)

                // PDF mode combines every scanned page into ONE multi-page PDF and uploads it as
                // a single item; Image mode uploads each page as its own JPEG. Either way this
                // yields a list of byte-array items to upload uniformly below.
                // Bitmap compression/PDF assembly is real CPU work -- scope.launch runs on the
                // Main dispatcher by default, so without this the upload would block the UI
                // thread the same way the scanner result decoding did.
                val uploadItems: List<ByteArray> = withContext(Dispatchers.IO) {
                    when (format) {
                        DocFileFormat.Pdf, DocFileFormat.PdfCombined -> listOf(bitmapsToPdfBytes(pages))
                        DocFileFormat.Png -> pages.map { bitmap ->
                            val stream = ByteArrayOutputStream()
                            // PNG is lossless; the quality parameter is ignored by the platform for this format.
                            bitmap.compress(Bitmap.CompressFormat.PNG, 100, stream)
                            stream.toByteArray()
                        }
                        DocFileFormat.Image -> pages.map { bitmap ->
                            val stream = ByteArrayOutputStream()
                            bitmap.compress(Bitmap.CompressFormat.JPEG, 85, stream)
                            stream.toByteArray()
                        }
                    }
                }

                for (itemBytes in uploadItems) {
                    try {
                        val base64Data = Base64.encodeToString(itemBytes, Base64.NO_WRAP)
                        var uploadSucceeded = false

                        if (confirmedUploadTarget == DocUploadTarget.SetImage) {
                            val request = SetImageUploadRequest(
                                imageBase64 = base64Data,
                                imageType = "MobileUpload",
                                uploadedBy = currentUser,
                                mimeType = format.mimeType
                            )
                            when (val result = safeApiCall { ApiClient.service.uploadSetImage(token = null, setCode = setCode, request = request, documentNumber = documentNumberValue) }) {
                                is ApiResult.Success -> {
                                    uploadSucceeded = result.data.success
                                    if (!uploadSucceeded) lastFailureReason = result.data.message ?: "Server rejected the upload"
                                }
                                is ApiResult.HttpError -> lastFailureReason = result.userMessageOr("HTTP ${result.code}")
                                is ApiResult.NetworkError -> lastFailureReason = result.userMessageOr()
                                is ApiResult.UnknownError -> lastFailureReason = result.userMessageOr()
                            }
                        } else {
                            val request = ReceiptImageUploadRequest(
                                docType = docType,
                                supplier = confirmedSupplier.trim().ifBlank { null },
                                siNumber = confirmedSiNumber.trim().ifBlank { null },
                                drNumber = confirmedDrNumber.trim().ifBlank { null },
                                poNumber = confirmedPoNumber.trim().ifBlank { null },
                                imageBase64 = base64Data,
                                receiptSetId = activeReceiptSetId,
                                mimeType = format.mimeType
                            )
                            when (val result = safeApiCall { ApiClient.service.uploadReceiptImage(token = null, setCode = setCode, documentNumber = documentNumberValue, request = request) }) {
                                is ApiResult.Success -> {
                                    uploadSucceeded = result.data.success
                                    if (uploadSucceeded) {
                                        if (activeReceiptSetId == null) activeReceiptSetId = result.data.receiptSetId
                                    } else {
                                        lastFailureReason = result.data.message ?: "Server rejected the upload"
                                    }
                                }
                                is ApiResult.HttpError -> lastFailureReason = result.userMessageOr("HTTP ${result.code}")
                                is ApiResult.NetworkError -> lastFailureReason = result.userMessageOr()
                                is ApiResult.UnknownError -> lastFailureReason = result.userMessageOr()
                            }
                        }

                        if (uploadSucceeded) successCount++ else failCount++
                    } catch (e: Exception) {
                        lastFailureReason = e.message ?: e.javaClass.simpleName
                        failCount++
                    }
                }
            }
            typeCount = seenDocTypes.size

            isDocUploading = false
            clearQueuedPages()
            confirmedSetCode = null
            val targetLabel = if (confirmedUploadTarget == DocUploadTarget.SetImage) "Set Image(s)"
                else "Receipt page(s) across $typeCount type(s)"
            val destinationLabel = if (confirmedIdentifierType == DocIdentifierType.SetCode) "Set $identifierValue" else "Invoice $identifierValue"
            if (failCount == 0) {
                Toast.makeText(context, "Uploaded $successCount $targetLabel to $destinationLabel.", Toast.LENGTH_LONG).show()
            } else {
                // A Toast can get visually cut off/single-lined on some devices, which is exactly
                // what made the last failure ("We cou...") undiagnosable -- a dialog can't
                // truncate and stays on screen until dismissed, so the full reason is always
                // readable/copyable.
                uploadErrorDialogMessage = "Uploaded $successCount $targetLabel to $destinationLabel, $failCount failed.\n\n" +
                    "Reason: ${lastFailureReason ?: "Unknown"}"
            }
        }
    }

    if (showSetCodePrompt) {
        val dialogNoun = if (pendingIdentifierType == DocIdentifierType.SetCode) "Set" else "Invoice"
        AlertDialog(
            onDismissRequest = { showSetCodePrompt = false },
            title = { Text("Which $dialogNoun is this for?") },
            text = {
                // Document # / Attach as / Save as / Doc type / Supplier / SI / DR / PO Number can
                // together exceed the dialog's available height on smaller screens -- without
                // scrolling, the later fields (PO Number, bottom of DR Number) were getting clipped
                // behind the Cancel/Scan buttons with no way to reach them.
                Column(
                    verticalArrangement = Arrangement.spacedBy(10.dp),
                    modifier = Modifier
                        .heightIn(max = 420.dp)
                        .verticalScroll(rememberScrollState())
                ) {
                    Text(
                        "Enter the ${pendingIdentifierType.label} before scanning — there's no $dialogNoun selected in Quick Scan.",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                    OutlinedTextField(
                        value = pendingSetCode,
                        onValueChange = { pendingSetCode = it },
                        label = { Text(pendingIdentifierType.label) },
                        singleLine = true,
                        modifier = Modifier.fillMaxWidth()
                    )

                    Text("Attach as", style = MaterialTheme.typography.labelMedium, fontWeight = FontWeight.SemiBold)
                    Row(
                        horizontalArrangement = Arrangement.spacedBy(8.dp),
                        modifier = Modifier.horizontalScroll(rememberScrollState())
                    ) {
                        DocUploadTarget.entries.forEach { target ->
                            FilterChip(
                                selected = pendingUploadTarget == target,
                                onClick = {
                                    pendingUploadTarget = target
                                    // PDF Combined only makes sense for Receipt uploads (it's the
                                    // "replaces SI/DR/PO" category) -- fall back if switching away.
                                    if (target != DocUploadTarget.Receipt && pendingFileFormat == DocFileFormat.PdfCombined) {
                                        pendingFileFormat = DocFileFormat.Image
                                    }
                                },
                                label = { Text(target.label, style = MaterialTheme.typography.labelSmall, maxLines = 1) }
                            )
                        }
                    }

                    Text("Save as", style = MaterialTheme.typography.labelMedium, fontWeight = FontWeight.SemiBold)
                    Row(
                        horizontalArrangement = Arrangement.spacedBy(8.dp),
                        modifier = Modifier.horizontalScroll(rememberScrollState())
                    ) {
                        DocFileFormat.entries
                            .filter { it != DocFileFormat.PdfCombined || pendingUploadTarget == DocUploadTarget.Receipt }
                            .forEach { format ->
                                FilterChip(
                                    selected = pendingFileFormat == format,
                                    onClick = { pendingFileFormat = format },
                                    label = { Text(format.label, style = MaterialTheme.typography.labelSmall, maxLines = 1) }
                                )
                            }
                    }
                    if (pendingFileFormat == DocFileFormat.Pdf) {
                        Text(
                            "All scanned pages will be combined into one PDF file.",
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    } else if (pendingFileFormat == DocFileFormat.PdfCombined) {
                        Text(
                            "All scanned pages become one PDF filed as PDF Document, replacing SI/DR/PO -- no doc type selection needed.",
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }

                    if (pendingUploadTarget == DocUploadTarget.Receipt) {
                        if (pendingFileFormat != DocFileFormat.PdfCombined) {
                            // Just picks which doc-type tab starts active once scanning begins --
                            // SI/DR/PO can all be switched between and queued together afterward.
                            Text("Doc type", style = MaterialTheme.typography.labelMedium, fontWeight = FontWeight.SemiBold)
                            Row(
                                horizontalArrangement = Arrangement.spacedBy(8.dp),
                                modifier = Modifier.horizontalScroll(rememberScrollState())
                            ) {
                                listOf("SI", "DR", "PO").forEach { type ->
                                    FilterChip(
                                        selected = pendingDocType == type,
                                        onClick = { pendingDocType = type },
                                        label = { Text(type, maxLines = 1) }
                                    )
                                }
                            }
                        }
                        var supplierMenuExpanded by remember { mutableStateOf(false) }
                        val supplierMatches = remember(pendingSupplier, vendorNames) {
                            val names = vendorNames.orEmpty()
                            if (pendingSupplier.isBlank()) names
                            else names.filter { it.contains(pendingSupplier, ignoreCase = true) }
                        }
                        ExposedDropdownMenuBox(
                            expanded = supplierMenuExpanded && supplierMatches.isNotEmpty(),
                            onExpandedChange = { supplierMenuExpanded = it }
                        ) {
                            OutlinedTextField(
                                value = pendingSupplier,
                                onValueChange = {
                                    pendingSupplier = it
                                    supplierMenuExpanded = true
                                },
                                label = { Text("Supplier *") },
                                singleLine = true,
                                supportingText = {
                                    if (vendorNames == null) {
                                        Text("Loading vendor list…", style = MaterialTheme.typography.bodySmall)
                                    }
                                },
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .menuAnchor()
                            )
                            ExposedDropdownMenu(
                                expanded = supplierMenuExpanded && supplierMatches.isNotEmpty(),
                                onDismissRequest = { supplierMenuExpanded = false }
                            ) {
                                supplierMatches.forEach { name ->
                                    DropdownMenuItem(
                                        text = { Text(name) },
                                        onClick = {
                                            pendingSupplier = name
                                            supplierMenuExpanded = false
                                        }
                                    )
                                }
                            }
                        }
                        OutlinedTextField(
                            value = pendingSiNumber,
                            onValueChange = { pendingSiNumber = it },
                            label = { Text("SI Number") },
                            singleLine = true,
                            modifier = Modifier.fillMaxWidth()
                        )
                        OutlinedTextField(
                            value = pendingDrNumber,
                            onValueChange = { pendingDrNumber = it },
                            label = { Text("DR Number") },
                            singleLine = true,
                            modifier = Modifier.fillMaxWidth()
                        )
                        OutlinedTextField(
                            value = pendingPoNumber,
                            onValueChange = { pendingPoNumber = it },
                            label = { Text("PO Number") },
                            singleLine = true,
                            modifier = Modifier.fillMaxWidth()
                        )
                    }
                }
            },
            confirmButton = {
                TextButton(
                    onClick = {
                        val code = pendingSetCode.trim()
                        if (code.isBlank()) {
                            Toast.makeText(context, "Enter a ${pendingIdentifierType.label} first.", Toast.LENGTH_SHORT).show()
                            return@TextButton
                        }
                        if (pendingUploadTarget == DocUploadTarget.Receipt && pendingSupplier.isBlank()) {
                            Toast.makeText(context, "Supplier is required for receipt uploads.", Toast.LENGTH_SHORT).show()
                            return@TextButton
                        }
                        // Only enforce "must match an existing Vendor" when the list actually
                        // loaded -- vendorNames is null if the fetch is still running or failed.
                        if (pendingUploadTarget == DocUploadTarget.Receipt && vendorNames != null &&
                            vendorNames.orEmpty().none { it.equals(pendingSupplier.trim(), ignoreCase = true) }
                        ) {
                            Toast.makeText(context, "Supplier must be selected from the existing vendor list.", Toast.LENGTH_SHORT).show()
                            return@TextButton
                        }
                        confirmedSetCode = code
                        confirmedIdentifierType = pendingIdentifierType
                        confirmedUploadTarget = pendingUploadTarget
                        confirmedFileFormat = pendingFileFormat
                        // Format for this first scan; subsequent "Add More Pages" taps can change
                        // nextScanFormat independently via the chips in the preview area.
                        nextScanFormat = pendingFileFormat
                        // PDF Combined always files under the dedicated "PDF" category, skipping
                        // the SI/DR/PO doc-type choice entirely.
                        activeDocType = if (pendingUploadTarget == DocUploadTarget.Receipt && pendingFileFormat == DocFileFormat.PdfCombined)
                            "PDF" else pendingDocType
                        confirmedSupplier = pendingSupplier
                        confirmedSiNumber = pendingSiNumber
                        confirmedDrNumber = pendingDrNumber
                        confirmedPoNumber = pendingPoNumber
                        showSetCodePrompt = false
                        launchDocumentScanner()
                    }
                ) { Text("Scan") }
            },
            dismissButton = {
                TextButton(onClick = { showSetCodePrompt = false }) { Text("Cancel") }
            }
        )
    }

    uploadErrorDialogMessage?.let { message ->
        AlertDialog(
            onDismissRequest = { uploadErrorDialogMessage = null },
            title = { Text("Upload Failed") },
            text = { Text(message, style = MaterialTheme.typography.bodyMedium) },
            confirmButton = {
                TextButton(onClick = { uploadErrorDialogMessage = null }) { Text("OK") }
            }
        )
    }

    previewedPageIndex?.let { index ->
        val previewPages = currentPages()
        val page = previewPages.getOrNull(index)
        if (page != null) {
            Dialog(
                onDismissRequest = { previewedPageIndex = null },
                properties = DialogProperties(usePlatformDefaultWidth = false)
            ) {
                Column(
                    modifier = Modifier
                        .fillMaxSize()
                        .background(Color.Black)
                ) {
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(12.dp),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Text(
                            "Page ${index + 1} of ${previewPages.size} — ${page.format.label}",
                            color = Color.White,
                            fontWeight = FontWeight.SemiBold
                        )
                        IconButton(onClick = { previewedPageIndex = null }) {
                            Icon(Icons.Default.Close, contentDescription = "Close", tint = Color.White)
                        }
                    }
                    Image(
                        bitmap = page.bitmap.asImageBitmap(),
                        contentDescription = "Scanned page ${index + 1} full preview",
                        modifier = Modifier
                            .weight(1f)
                            .fillMaxWidth()
                            .padding(horizontal = 12.dp),
                        contentScale = ContentScale.Fit
                    )
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(16.dp),
                        horizontalArrangement = Arrangement.spacedBy(10.dp)
                    ) {
                        OutlinedButton(
                            onClick = { previewedPageIndex = null },
                            modifier = Modifier.weight(1f),
                            colors = ButtonDefaults.outlinedButtonColors(contentColor = Color.White)
                        ) { Text("Close") }
                        Button(
                            onClick = {
                                val updated = previewPages.filterIndexed { i, _ -> i != index }
                                if (confirmedUploadTarget == DocUploadTarget.Receipt) {
                                    receiptPagesByType = receiptPagesByType.toMutableMap().apply {
                                        this[activeDocType] = updated
                                    }
                                } else {
                                    setImagePages = updated
                                }
                                previewedPageIndex = null
                            },
                            modifier = Modifier.weight(1f),
                            colors = ButtonDefaults.buttonColors(containerColor = MaterialTheme.colorScheme.error)
                        ) {
                            Icon(Icons.Default.Delete, contentDescription = null, modifier = Modifier.size(18.dp))
                            Spacer(Modifier.width(6.dp))
                            Text("Remove Page")
                        }
                    }
                }
            }
        }
    }

    fun appendScanEvent(
        value: String,
        source: String,
        status: String,
        detail: String,
        undoKey: String? = null
    ) {
        eventCounter += 1
        scanLog = (listOf(
            ScanSessionEvent(
                id = eventCounter,
                value = value,
                source = source,
                status = status,
                detail = detail,
                undoKey = undoKey
            )
        ) + scanLog).take(100)
    }

    fun appendDuplicateEvent(
        value: String,
        source: String,
        matchedField: String,
        existingRow: Int?,
        attemptedRow: Int?,
        detail: String
    ) {
        duplicateScans = (listOf(
            DuplicateScanEvent(
                value = value,
                source = source,
                matchedField = matchedField,
                existingRow = existingRow,
                attemptedRow = attemptedRow,
                detail = detail
            )
        ) + duplicateScans)
        appendScanEvent(value, source, "Duplicate", detail)
    }

    fun addSerials(rawInput: String, showFeedback: Boolean = true, source: String = "Manual"): SerialAddResult {
        val parsedSerials = parseNormalizedSerials(rawInput)
        if (parsedSerials.isEmpty()) {
            if (showFeedback) {
                scope.launch { snackbarHostState.showSnackbar("Enter a valid serial") }
            }
            appendScanEvent(rawInput.trim(), source, "Rejected", "Invalid or blank serial")
            return SerialAddResult(added = 0, duplicates = 0)
        }

        val existing = serialNumbers
            .map { normalizeSerial(it) }
            .filter { it.isNotEmpty() }
            .toMutableSet()

        val newSerials = mutableListOf<String>()
        var duplicateCount = 0
        parsedSerials.forEach { serial ->
            if (existing.add(serial)) {
                newSerials.add(serial)
                appendScanEvent(serial, source, "Accepted", "Added to direct serial list", undoKey = "serial:$serial")
            } else {
                duplicateCount++
                val existingRow = serialNumbers.indexOfFirst { normalizeSerial(it) == serial }.takeIf { it >= 0 }?.plus(1)
                appendDuplicateEvent(
                    value = serial,
                    source = source,
                    matchedField = "Serial Number",
                    existingRow = existingRow,
                    attemptedRow = null,
                    detail = "Already exists in the direct serial list"
                )
            }
        }

        if (newSerials.isNotEmpty()) {
            serialNumbers = serialNumbers + newSerials
            currentSerial = ""
            haptic.performHapticFeedback(androidx.compose.ui.hapticfeedback.HapticFeedbackType.LongPress)
        }

        if (showFeedback) {
            scope.launch {
                when {
                    newSerials.isNotEmpty() && duplicateCount > 0 ->
                        snackbarHostState.showSnackbar("Added ${newSerials.size} serial(s), skipped $duplicateCount duplicate(s)")
                    newSerials.size > 1 ->
                        snackbarHostState.showSnackbar("Added ${newSerials.size} serial(s)")
                    newSerials.isEmpty() && duplicateCount > 0 ->
                        snackbarHostState.showSnackbar("All pasted serials are already in the list")
                }
            }
        }

        return SerialAddResult(added = newSerials.size, duplicates = duplicateCount)
    }
    fun normalizedPhoneRows(): List<DirectPhoneEntry> = phoneRows
        .map {
            it.copy(
                cellPhoneNumber = it.cellPhoneNumber.trim(),
                serialNumber = normalizeSerial(it.serialNumber),
                imei1 = normalizeSerial(it.imei1),
                imei2 = normalizeSerial(it.imei2)
            )
        }
        .filter { it.cellPhoneNumber.isNotBlank() || it.serialNumber.isNotBlank() || it.imei1.isNotBlank() || it.imei2.isNotBlank() }

    fun generatePhoneRows() {
        val count = phoneRowCount.toIntOrNull()?.coerceIn(1, 200) ?: return
        phoneRows = List(count) { index -> phoneRows.getOrNull(index) ?: DirectPhoneEntry() }
    }

    fun phoneCodeSet(excludeIndex: Int? = null): Set<String> {
        return phoneRows
            .asSequence()
            .filterIndexed { index, _ -> excludeIndex == null || index != excludeIndex }
            .flatMap { row -> sequenceOf(row.serialNumber, row.imei1, row.imei2) }
            .map { normalizeSerial(it) }
            .filter { it.isNotBlank() }
            .toSet()
    }

    fun updatePhoneRow(index: Int, value: DirectPhoneEntry) {
        val current = phoneRows.getOrNull(index) ?: DirectPhoneEntry()
        val used = phoneCodeSet(excludeIndex = index).toMutableSet()
        var blockedDuplicate = false

        fun keepIfUnique(raw: String, previous: String): String {
            val normalized = normalizeSerial(raw)
            if (normalized.isBlank()) return raw
            return if (used.add(normalized)) raw else {
                blockedDuplicate = true
                previous
            }
        }

        val cleaned = value.copy(
            serialNumber = keepIfUnique(value.serialNumber, current.serialNumber),
            imei1 = keepIfUnique(value.imei1, current.imei1),
            imei2 = keepIfUnique(value.imei2, current.imei2)
        )
        phoneRows = phoneRows.mapIndexed { rowIndex, row -> if (rowIndex == index) cleaned else row }
        if (blockedDuplicate) {
            scope.launch { snackbarHostState.showSnackbar("Duplicate serial or IMEI") }
        }
    }

    fun findPhoneDuplicate(value: String): Pair<String, Int>? {
        phoneRows.forEachIndexed { index, row ->
            val rowNumber = index + 1
            val values = listOf(
                "Serial Number" to normalizeSerial(row.serialNumber),
                "IMEI 1" to normalizeSerial(row.imei1),
                "IMEI 2" to normalizeSerial(row.imei2)
            )
            values.firstOrNull { it.second == value }?.let { return it.first to rowNumber }
        }
        return null
    }

    fun updatePhoneField(rowIndex: Int, target: PhoneScanTarget, value: String) {
        phoneRows = phoneRows.mapIndexed { index, row ->
            if (index != rowIndex) row else when (target) {
                PhoneScanTarget.Serial -> row.copy(serialNumber = value)
                PhoneScanTarget.Imei1 -> row.copy(imei1 = value)
                PhoneScanTarget.Imei2 -> row.copy(imei2 = value)
                PhoneScanTarget.Auto -> row
            }
        }
    }

    fun targetForAuto(row: DirectPhoneEntry): PhoneScanTarget? = when {
        normalizeSerial(row.serialNumber).isBlank() -> PhoneScanTarget.Serial
        normalizeSerial(row.imei1).isBlank() -> PhoneScanTarget.Imei1
        normalizeSerial(row.imei2).isBlank() -> PhoneScanTarget.Imei2
        else -> null
    }

    fun tabScanTarget() {
        val next = when (focusedPhoneField) {
            PhoneFocusField.CellPhone -> focusedPhoneRowIndex to PhoneFocusField.Serial
            PhoneFocusField.Serial -> focusedPhoneRowIndex to PhoneFocusField.Imei1
            PhoneFocusField.Imei1 -> focusedPhoneRowIndex to PhoneFocusField.Imei2
            PhoneFocusField.Imei2 -> ((focusedPhoneRowIndex + 1).coerceAtMost(phoneRows.lastIndex)) to PhoneFocusField.CellPhone
        }
        focusedPhoneRowIndex = next.first
        focusedPhoneField = next.second
        phoneScanTarget = when (focusedPhoneField) {
            PhoneFocusField.Serial -> PhoneScanTarget.Serial
            PhoneFocusField.Imei1 -> PhoneScanTarget.Imei1
            PhoneFocusField.Imei2 -> PhoneScanTarget.Imei2
            PhoneFocusField.CellPhone -> phoneScanTarget
        }
        appendScanEvent("Tab", "Button", "Focus", "Phone ${focusedPhoneRowIndex + 1} ${focusedPhoneField.name}")
    }

    fun addPhoneSerial(rawSerial: String, showFeedback: Boolean = false, source: String = "Manual"): Boolean {
        val normalized = normalizeSerial(rawSerial)
        if (normalized.isBlank()) {
            appendScanEvent(rawSerial.trim(), source, "Rejected", "Invalid or blank scan")
            return false
        }

        findPhoneDuplicate(normalized)?.let { (field, existingRow) ->
            val message = "$field already exists in Phone $existingRow"
            appendDuplicateEvent(
                value = normalized,
                source = source,
                matchedField = field,
                existingRow = existingRow,
                attemptedRow = null,
                detail = message
            )
            if (showFeedback) scope.launch { snackbarHostState.showSnackbar(message) }
            return false
        }

        val target = if (phoneScanTarget == PhoneScanTarget.Auto) null else phoneScanTarget
        val rowIndex = if (target == null) {
            phoneRows.indexOfFirst { targetForAuto(it) != null }
        } else {
            phoneRows.indexOfFirst { row ->
                when (target) {
                    PhoneScanTarget.Serial -> normalizeSerial(row.serialNumber).isBlank()
                    PhoneScanTarget.Imei1 -> normalizeSerial(row.imei1).isBlank()
                    PhoneScanTarget.Imei2 -> normalizeSerial(row.imei2).isBlank()
                    PhoneScanTarget.Auto -> false
                }
            }
        }

        val finalTarget = if (rowIndex >= 0) {
            target ?: targetForAuto(phoneRows[rowIndex])
        } else {
            target ?: PhoneScanTarget.Serial
        } ?: PhoneScanTarget.Serial

        val finalRowIndex = if (rowIndex >= 0) rowIndex else phoneRows.size
        if (rowIndex >= 0) {
            updatePhoneField(rowIndex, finalTarget, normalized)
        } else {
            val newRow = when (finalTarget) {
                PhoneScanTarget.Serial, PhoneScanTarget.Auto -> DirectPhoneEntry(serialNumber = normalized)
                PhoneScanTarget.Imei1 -> DirectPhoneEntry(imei1 = normalized)
                PhoneScanTarget.Imei2 -> DirectPhoneEntry(imei2 = normalized)
            }
            phoneRows = phoneRows + newRow
        }

        currentSerial = ""
        appendScanEvent(
            value = normalized,
            source = source,
            status = "Accepted",
            detail = "Phone ${finalRowIndex + 1} ${finalTarget.label}",
            undoKey = "phone:${finalRowIndex}:${finalTarget.name}:$normalized"
        )
        haptic.performHapticFeedback(androidx.compose.ui.hapticfeedback.HapticFeedbackType.LongPress)
        return true
    }
    fun duplicatePhoneEntryMessage(rows: List<DirectPhoneEntry>): String? {
        data class SeenCode(val label: String, val rowNumber: Int)

        val seen = mutableMapOf<String, SeenCode>()
        rows.forEachIndexed { index, row ->
            val rowNumber = index + 1
            val values = listOf(
                "Serial Number" to normalizeSerial(row.serialNumber),
                "IMEI 1" to normalizeSerial(row.imei1),
                "IMEI 2" to normalizeSerial(row.imei2)
            )

            for ((label, value) in values) {
                if (value.isBlank()) continue
                val existing = seen[value]
                if (existing != null) {
                    return "Duplicate $label: Phone ${existing.rowNumber} ${existing.label} and Phone $rowNumber $label have the same value."
                }
                seen[value] = SeenCode(label, rowNumber)
            }
        }

        return null
    }

    fun undoLastAcceptedScan() {
        val event = scanLog.firstOrNull { it.status == "Accepted" && !it.undone && it.undoKey != null } ?: return
        val key = event.undoKey.orEmpty()
        when {
            key.startsWith("serial:") -> {
                val value = key.removePrefix("serial:")
                serialNumbers = serialNumbers.filterNot { normalizeSerial(it) == value }
            }
            key.startsWith("phone:") -> {
                val parts = key.split(":", limit = 4)
                val rowIndex = parts.getOrNull(1)?.toIntOrNull()
                val target = parts.getOrNull(2)?.let { runCatching { PhoneScanTarget.valueOf(it) }.getOrNull() }
                val value = parts.getOrNull(3).orEmpty()
                if (rowIndex != null && target != null) {
                    phoneRows = phoneRows.mapIndexed { index, row ->
                        if (index != rowIndex) row else when (target) {
                            PhoneScanTarget.Serial -> if (normalizeSerial(row.serialNumber) == value) row.copy(serialNumber = "") else row
                            PhoneScanTarget.Imei1 -> if (normalizeSerial(row.imei1) == value) row.copy(imei1 = "") else row
                            PhoneScanTarget.Imei2 -> if (normalizeSerial(row.imei2) == value) row.copy(imei2 = "") else row
                            PhoneScanTarget.Auto -> row
                        }
                    }
                }
            }
        }
        scanLog = scanLog.map { if (it.id == event.id) it.copy(undone = true, detail = it.detail + " (undone)") else it }
    }

    fun importBulkPhoneRows(raw: String) {
        val rows = raw.lineSequence()
            .map { it.trim() }
            .filter { it.isNotBlank() }
            .map { line -> line.split(',', ';', '\t').map { it.trim() } }
            .mapNotNull { parts ->
                val first = parts.getOrNull(0).orEmpty()
                if (parts.size >= 4) {
                    DirectPhoneEntry(
                        cellPhoneNumber = first,
                        serialNumber = normalizeSerial(parts.getOrNull(1)),
                        imei1 = normalizeSerial(parts.getOrNull(2)),
                        imei2 = normalizeSerial(parts.getOrNull(3))
                    )
                } else {
                    val serial = normalizeSerial(first)
                    if (serial.isBlank()) null else DirectPhoneEntry(serialNumber = serial)
                }
            }
            .filter { it.cellPhoneNumber.isNotBlank() || it.serialNumber.isNotBlank() || it.imei1.isNotBlank() || it.imei2.isNotBlank() }
            .toList()

        if (rows.isEmpty()) {
            scope.launch { snackbarHostState.showSnackbar("No valid rows found") }
            return
        }

        rows.forEach { row ->
            listOf("Serial Number" to row.serialNumber, "IMEI 1" to row.imei1, "IMEI 2" to row.imei2)
                .filter { it.second.isNotBlank() }
                .forEach { (field, value) ->
                    findPhoneDuplicate(value)?.let { (existingField, existingRow) ->
                        appendDuplicateEvent(
                            value = value,
                            source = "Bulk paste",
                            matchedField = field,
                            existingRow = existingRow,
                            attemptedRow = null,
                            detail = "$field matches Phone $existingRow $existingField"
                        )
                    }
                }
        }

        val existingCodes = phoneCodeSet().toMutableSet()
        val acceptedRows = rows.mapNotNull { row ->
            val serial = row.serialNumber.takeIf { it.isBlank() || existingCodes.add(it) } ?: ""
            val imei1 = row.imei1.takeIf { it.isBlank() || existingCodes.add(it) } ?: ""
            val imei2 = row.imei2.takeIf { it.isBlank() || existingCodes.add(it) } ?: ""
            row.copy(serialNumber = serial, imei1 = imei1, imei2 = imei2)
                .takeIf { it.cellPhoneNumber.isNotBlank() || it.serialNumber.isNotBlank() || it.imei1.isNotBlank() || it.imei2.isNotBlank() }
        }

        if (acceptedRows.isNotEmpty()) {
            phoneRows = (phoneRows.filter { it.cellPhoneNumber.isNotBlank() || it.serialNumber.isNotBlank() || it.imei1.isNotBlank() || it.imei2.isNotBlank() } + acceptedRows)
                .ifEmpty { listOf(DirectPhoneEntry()) }
            appendScanEvent("${acceptedRows.size} row(s)", "Bulk paste", "Accepted", "Imported phone rows")
            bulkImportText = ""
        }
        scope.launch { snackbarHostState.showSnackbar("Imported ${acceptedRows.size} row(s)") }
    }
    // Listen for scanned results (reactively)
    val navBackStackEntry by navController.currentBackStackEntryAsState()
    val scannedSerialFlow = remember(navBackStackEntry) {
        navBackStackEntry?.savedStateHandle?.getStateFlow<String?>(SCAN_RESULT_SERIAL_KEY, null)
    }
    val scannedSerial by (scannedSerialFlow?.collectAsState(initial = null) ?: remember {
        mutableStateOf<String?>(null)
    })

    val scannedSerialsFlow = remember(navBackStackEntry) {
        navBackStackEntry?.savedStateHandle?.getStateFlow<java.util.ArrayList<String>?>(SCAN_RESULT_SERIALS_KEY, null)
    }
    val scannedSerials by (scannedSerialsFlow?.collectAsState(initial = null) ?: remember {
        mutableStateOf<java.util.ArrayList<String>?>(null)
    })


    LaunchedEffect(scannedSerial) {
        val scanned = scannedSerial?.trim().orEmpty()
        if (scanned.isBlank()) return@LaunchedEffect

        currentSerial = scanned
        if (phoneMode) {
            addPhoneSerial(scanned, source = "Camera")
        } else {
            addSerials(scanned, showFeedback = false, source = "Camera")
        }
        navBackStackEntry?.savedStateHandle?.remove<String>(SCAN_RESULT_SERIAL_KEY)
    }

    LaunchedEffect(scannedSerials) {
        val batch = scannedSerials?.toList().orEmpty()
        if (batch.isEmpty()) return@LaunchedEffect

        for (value in batch) {
            val scanned = value.trim()
            if (scanned.isBlank()) continue
            currentSerial = scanned
            if (phoneMode) {
                addPhoneSerial(scanned, source = "Camera")
            } else {
                addSerials(scanned, showFeedback = false, source = "Camera")
            }
        }
        navBackStackEntry?.savedStateHandle?.remove<java.util.ArrayList<String>>(SCAN_RESULT_SERIALS_KEY)
    }

    // â”€â”€ Confirmation dialog â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    if (showConfirmDialog) {
        val preview = if (phoneMode) normalizedPhoneRows().map { it.serialNumber }.filter { it.isNotEmpty() } else serialNumbers.map { normalizeSerial(it) }.filter { it.isNotEmpty() }
        SendToWindowsConfirmDialog(
            serials = preview,
            onDismiss = { showConfirmDialog = false },
            onConfirm = { selectedSerials ->
                showConfirmDialog = false
                isSending = true
                scope.launch {
                    var successCount = 0
                    val failedSerials = mutableListOf<String>()

                    for (serial in selectedSerials) {
                        try {
                            val phoneRow = if (phoneMode) normalizedPhoneRows().firstOrNull { it.serialNumber == serial } else null
                            val request = SerialFromMobileRequest(
                                serialNumber = serial,
                                cellPhoneNumber = phoneRow?.cellPhoneNumber?.ifBlank { null },
                                imei1 = phoneRow?.imei1?.ifBlank { null },
                                imei2 = phoneRow?.imei2?.ifBlank { null }
                            )
                            val response = ApiClient.service.sendSerialToWindows(request)
                            if (response.isSuccessful && response.body()?.success == true) {
                                successCount++
                            } else {
                                failedSerials.add(serial)
                            }
                        } catch (e: Exception) {
                            failedSerials.add(serial)
                        }
                    }

                    // Keep any serials that were not selected + any that failed to send
                    if (phoneMode) {
                        phoneRows = normalizedPhoneRows()
                            .filter { it.serialNumber !in selectedSerials || it.serialNumber in failedSerials }
                            .ifEmpty { listOf(DirectPhoneEntry()) }
                    } else {
                        val unselectedSerials = serialNumbers
                            .map { normalizeSerial(it) }
                            .filter { it.isNotEmpty() && it !in selectedSerials }
                        serialNumbers = unselectedSerials + failedSerials
                    }

                    isSending = false
                    val failCount = failedSerials.size
                    val msg = when {
                        failCount == 0 -> "Sent $successCount serial(s) successfully"
                        successCount == 0 -> "Couldn't send the selected serials"
                        else -> "Sent $successCount serial(s), $failCount could not be sent"
                    }
                    Toast.makeText(context, msg, Toast.LENGTH_LONG).show()
                }
            }
        )
    }

    Scaffold(
        modifier = Modifier.fillMaxSize(),
        topBar = {
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .background(com.example.yakultscanner.ui.components.ScannerWorkspaceUi.BrandSoft)
                    .statusBarsPadding()
                    .padding(horizontal = 16.dp, vertical = 10.dp)
            ) {
                Row(
                    verticalAlignment = Alignment.CenterVertically,
                    modifier = Modifier.fillMaxWidth()
                ) {
                    IconButton(
                        onClick = { navController.popBackStack() },
                        colors = IconButtonDefaults.iconButtonColors(contentColor = com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Ink)
                    ) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, "Back")
                    }
                    Spacer(modifier = Modifier.width(8.dp))
                    Column(modifier = Modifier.weight(1f)) {
                        Text(
                            text = "Direct Capture",
                            style = MaterialTheme.typography.titleLarge,
                            color = com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Ink,
                            fontWeight = FontWeight.Bold
                        )
                        Text(
                            text = "Capture serials or attach documents",
                            style = MaterialTheme.typography.labelSmall,
                            color = com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Muted
                        )
                    }
                }
            }
        },        bottomBar = {
            val sendCount = if (phoneMode) normalizedPhoneRows().count { it.serialNumber.isNotBlank() } else serialNumbers.size
            if (sendCount > 0) {
                Surface(
                    shadowElevation = 8.dp,
                    color = MaterialTheme.colorScheme.surface,
                    modifier = Modifier
                        .fillMaxWidth()
                ) {
                    Button(
                        onClick = {
                            if (isSending) return@Button
                            val phoneValidationRows = if (phoneMode) normalizedPhoneRows() else emptyList()
                            if (phoneMode) {
                                duplicatePhoneEntryMessage(phoneValidationRows)?.let { message ->
                                    scope.launch { snackbarHostState.showSnackbar(message) }
                                    return@Button
                                }
                            }
                            val toSend = if (phoneMode) phoneValidationRows.map { it.serialNumber }.filter { it.isNotEmpty() } else serialNumbers.map { normalizeSerial(it) }.filter { it.isNotEmpty() }
                            if (toSend.isEmpty()) return@Button
                            // Show confirmation preview instead of sending immediately
                            showConfirmDialog = true
                        },
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(16.dp)
                            .height(56.dp),
                        enabled = !isSending,
                        shape = RoundedCornerShape(16.dp),
                        colors = ButtonDefaults.buttonColors(
                            containerColor = MaterialTheme.colorScheme.primary
                        )
                    ) {
                        if (isSending) {
                            CircularProgressIndicator(
                                modifier = Modifier.size(24.dp),
                                color = Color.White,
                                strokeWidth = 2.dp
                            )
                        } else {
                            Icon(Icons.Default.Send, null)
                            Spacer(Modifier.width(8.dp))
                            Text("Review & send $sendCount serial(s)")
                        }
                    }
                }
            }
        },
        snackbarHost = { SnackbarHost(snackbarHostState) },
        containerColor = com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Canvas
    ) { paddingValues ->
        // The whole screen scrolls as one unit now -- this used to be a fixed (non-scrolling) top
        // Column followed by a separately weight(1f)-scrolling LazyColumn below it. Once the Scan
        // Documents for a Set/Invoice cards could grow tall (thumbnails, buttons), the fixed top
        // section could exceed the screen height with nothing to scroll it, squeezing the weighted
        // LazyColumn below to zero height -- which is what made the whole page feel stuck/unscrollable.
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
                .verticalScroll(rememberScrollState())
        ) {
            val directReadyCount = if (phoneMode) normalizedPhoneRows().count { it.serialNumber.isNotBlank() } else serialNumbers.size
            val directPageCount = totalQueuedPages()
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(horizontal = 16.dp, vertical = 12.dp),
                verticalArrangement = Arrangement.spacedBy(10.dp)
            ) {
                com.example.yakultscanner.ui.components.ScannerModeSegment(
                    firstLabel = "Serials",
                    secondLabel = "Phone / IMEI",
                    firstSelected = !phoneMode,
                    onFirstSelected = { phoneMode = false },
                    onSecondSelected = { phoneMode = true }
                )
                Surface(
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(14.dp),
                    color = com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Surface
                ) {
                    Row(
                        modifier = Modifier.padding(horizontal = 14.dp, vertical = 10.dp),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Column(verticalArrangement = Arrangement.spacedBy(2.dp)) {
                            Text("Capture queue", style = MaterialTheme.typography.labelLarge, fontWeight = FontWeight.Bold, color = com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Ink)
                            Text("Scan, type, or paste serials. Attach documents only when needed.", style = MaterialTheme.typography.bodySmall, color = com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Muted)
                        }
                        Text("$directReadyCount captured", modifier = Modifier.width(92.dp), maxLines = 1, softWrap = false, style = MaterialTheme.typography.labelMedium, color = com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Brand, fontWeight = FontWeight.Bold)
                    }
                }
            }            // Input Area
            Card(                modifier = Modifier
                    .fillMaxWidth()
                    .padding(16.dp),
                elevation = CardDefaults.cardElevation(defaultElevation = 2.dp),
                colors = CardDefaults.cardColors(containerColor = com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Surface),
                shape = RoundedCornerShape(16.dp)
            ) {
                Row(
                    modifier = Modifier.padding(8.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    OutlinedTextField(
                        value = currentSerial,
                        onValueChange = { currentSerial = it },
                        label = { Text("Scan or type serial") },
                        modifier = Modifier
                            .weight(1f)
                            .padding(end = 8.dp),
                        minLines = 1,
                        maxLines = 4,
                        shape = RoundedCornerShape(12.dp),
                        trailingIcon = {
                            IconButton(onClick = {
                                navController.currentBackStackEntry?.savedStateHandle?.set(SCAN_RETURN_ROUTE_KEY, SCAN_RETURN_SERIAL_ROUTE)
                                navController.navigate("scanner_camera")
                            }) {
                                Icon(Icons.Default.QrCodeScanner, "Scan", tint = MaterialTheme.colorScheme.primary)
                            }
                        },
                        keyboardActions = androidx.compose.foundation.text.KeyboardActions(
                            onDone = { if (phoneMode) addPhoneSerial(currentSerial, showFeedback = true) else addSerials(currentSerial) }
                        ),
                        keyboardOptions = androidx.compose.foundation.text.KeyboardOptions(
                            imeAction = androidx.compose.ui.text.input.ImeAction.Done
                        ),
                        colors = OutlinedTextFieldDefaults.colors(
                            unfocusedBorderColor = Color.Transparent,
                            focusedBorderColor = Color.Transparent
                        ),
                        supportingText = {
                            Text("Paste one or more serials. Use a new line, comma, or semicolon to add multiple at once.")
                        }
                    )

                    FilledIconButton(
                        onClick = { if (phoneMode) addPhoneSerial(currentSerial, showFeedback = true) else addSerials(currentSerial) },
                        enabled = currentSerial.isNotBlank(),
                        modifier = Modifier.size(48.dp),
                        colors = IconButtonDefaults.filledIconButtonColors(
                            containerColor = MaterialTheme.colorScheme.secondaryContainer,
                            contentColor = MaterialTheme.colorScheme.onSecondaryContainer
                        )
                    ) {
                        Icon(Icons.Default.Add, "Add")
                    }
                }
            }

            // Shared preview content for whichever card actually owns the active batch --
            // previously this was hardcoded inside the Set card only, so an Invoice scan would
            // visually "appear" under the Set card even though it correctly uploaded via
            // document_number under the hood. Now each card only shows this if
            // confirmedIdentifierType actually matches it.
            val scannedPagesPreviewContent: @Composable () -> Unit = {
                val destinationSummary = if (confirmedIdentifierType == DocIdentifierType.SetCode)
                    "Set ${confirmedSetCode.orEmpty()}" else "Invoice ${confirmedSetCode.orEmpty()}"

                if (confirmedUploadTarget == DocUploadTarget.Receipt) {
                    // Doc-type tabs let SI/DR/PO pages be queued in the same session -- switching
                    // tabs does not clear or upload anything, it only changes which bucket
                    // "Add More Pages" appends to and which pages are shown below. activeDocType
                    // stays "PDF" for the whole session once PDF Combined is chosen, since that
                    // category has no SI/DR/PO subdivision.
                    val isPdfCombinedSession = activeDocType == "PDF"
                    Text(
                        if (isPdfCombinedSession) "PDF Combined (replaces SI/DR/PO) for $destinationSummary"
                        else "Receipt for $destinationSummary",
                        style = MaterialTheme.typography.bodySmall,
                        fontWeight = FontWeight.Medium
                    )
                    if (!isPdfCombinedSession) {
                        Row(
                            horizontalArrangement = Arrangement.spacedBy(8.dp),
                            modifier = Modifier.horizontalScroll(rememberScrollState())
                        ) {
                            listOf("SI", "DR", "PO").forEach { type ->
                                val count = receiptPagesByType[type].orEmpty().size
                                FilterChip(
                                    selected = activeDocType == type,
                                    onClick = { activeDocType = type },
                                    label = { Text(if (count > 0) "$type ($count)" else type, maxLines = 1) }
                                )
                            }
                        }
                        // Lets each "Add More Pages" tap carry its own format, so a single doc
                        // type can end up with e.g. 2 Image pages and 3 PDF pages queued together.
                        Text("Format for next scan", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                        Row(
                            horizontalArrangement = Arrangement.spacedBy(8.dp),
                            modifier = Modifier.horizontalScroll(rememberScrollState())
                        ) {
                            listOf(DocFileFormat.Image, DocFileFormat.Png, DocFileFormat.Pdf).forEach { format ->
                                FilterChip(
                                    selected = nextScanFormat == format,
                                    onClick = { nextScanFormat = format },
                                    label = { Text(format.label, style = MaterialTheme.typography.labelSmall, maxLines = 1) }
                                )
                            }
                        }
                    }
                } else {
                    Text(
                        "${setImagePages.size} page(s) scanned for $destinationSummary",
                        style = MaterialTheme.typography.bodySmall,
                        fontWeight = FontWeight.Medium
                    )
                    Text("Format for next scan", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    Row(
                        horizontalArrangement = Arrangement.spacedBy(8.dp),
                        modifier = Modifier.horizontalScroll(rememberScrollState())
                    ) {
                        listOf(DocFileFormat.Image, DocFileFormat.Png, DocFileFormat.Pdf).forEach { format ->
                            FilterChip(
                                selected = nextScanFormat == format,
                                onClick = { nextScanFormat = format },
                                label = { Text(format.label, style = MaterialTheme.typography.labelSmall, maxLines = 1) }
                            )
                        }
                    }
                }

                val activePages = currentPages()
                LazyRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    itemsIndexed(activePages) { index, page ->
                        Box {
                            Image(
                                bitmap = page.bitmap.asImageBitmap(),
                                contentDescription = "Scanned page ${index + 1}",
                                modifier = Modifier
                                    .size(90.dp)
                                    .clip(RoundedCornerShape(8.dp))
                                    .clickable { previewedPageIndex = index },
                                contentScale = ContentScale.Crop
                            )
                            Text(
                                page.format.label,
                                style = MaterialTheme.typography.labelSmall,
                                color = Color.White,
                                modifier = Modifier
                                    .align(Alignment.BottomCenter)
                                    .fillMaxWidth()
                                    .background(Color.Black.copy(alpha = 0.55f))
                                    .padding(vertical = 2.dp),
                                textAlign = TextAlign.Center
                            )
                        }
                    }
                }
                if (confirmedUploadTarget == DocUploadTarget.Receipt && activeDocType != "PDF" && activePages.isEmpty()) {
                    Text(
                        "No $activeDocType pages yet — tap Add More Pages to scan some.",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
                OutlinedButton(
                    onClick = { launchDocumentScanner() },
                    modifier = Modifier.fillMaxWidth(),
                    enabled = !isDocUploading
                ) {
                    Icon(Icons.Default.Add, contentDescription = null, modifier = Modifier.size(18.dp))
                    Spacer(Modifier.width(8.dp))
                    Text(
                        if (confirmedUploadTarget == DocUploadTarget.Receipt && activeDocType != "PDF")
                            "Add More $activeDocType Pages" else "Add More Pages"
                    )
                }
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.fillMaxWidth()) {
                    OutlinedButton(
                        onClick = { clearQueuedPages(); confirmedSetCode = null },
                        modifier = Modifier.weight(1f),
                        enabled = !isDocUploading
                    ) { Text("Cancel") }
                    Button(
                        onClick = { uploadScannedDocPages() },
                        modifier = Modifier.weight(1f),
                        enabled = !isDocUploading && hasAnyQueuedPages()
                    ) {
                        if (isDocUploading) {
                            CircularProgressIndicator(modifier = Modifier.size(18.dp), strokeWidth = 2.dp, color = Color.White)
                        } else {
                            Text("Upload All (${totalQueuedPages()})")
                        }
                    }
                }
            }

            Card(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(horizontal = 16.dp, vertical = 6.dp),
                elevation = CardDefaults.cardElevation(defaultElevation = 0.dp),
                colors = CardDefaults.cardColors(containerColor = com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Surface),
                shape = RoundedCornerShape(16.dp)
            ) {
                Column(
                    modifier = Modifier.padding(14.dp),
                    verticalArrangement = Arrangement.spacedBy(10.dp)
                ) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Column(modifier = Modifier.weight(1f)) {
                            Text("Attach documents", fontWeight = FontWeight.SemiBold, color = com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Ink)
                            Text(
                                if (hasAnyQueuedPages()) "${totalQueuedPages()} page(s) ready for upload" else "Scan multi-page Set Images or Receipts only when required.",
                                style = MaterialTheme.typography.bodySmall,
                                color = com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Muted
                            )
                        }
                        Icon(Icons.Default.DocumentScanner, contentDescription = null, tint = com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Brand)
                    }
                    if (!hasAnyQueuedPages()) {
                        Row(horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.fillMaxWidth()) {
                            OutlinedButton(
                                onClick = {
                                    pendingSetCode = confirmedSetCode.orEmpty()
                                    pendingIdentifierType = DocIdentifierType.SetCode
                                    pendingUploadTarget = confirmedUploadTarget
                                    pendingFileFormat = confirmedFileFormat
                                    pendingDocType = activeDocType
                                    pendingSupplier = confirmedSupplier
                                    pendingSiNumber = confirmedSiNumber
                                    pendingDrNumber = confirmedDrNumber
                                    pendingPoNumber = confirmedPoNumber
                                    showSetCodePrompt = true
                                },
                                modifier = Modifier.weight(1f)
                            ) { Text("For a Set") }
                            OutlinedButton(
                                onClick = {
                                    pendingSetCode = confirmedSetCode.orEmpty()
                                    pendingIdentifierType = DocIdentifierType.DocumentNumber
                                    pendingUploadTarget = confirmedUploadTarget
                                    pendingFileFormat = confirmedFileFormat
                                    pendingDocType = activeDocType
                                    pendingSupplier = confirmedSupplier
                                    pendingSiNumber = confirmedSiNumber
                                    pendingDrNumber = confirmedDrNumber
                                    pendingPoNumber = confirmedPoNumber
                                    showSetCodePrompt = true
                                },
                                modifier = Modifier.weight(1f)
                            ) { Text("For an Invoice") }
                        }
                    } else {
                        scannedPagesPreviewContent()
                    }
                }
            }
            Card(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(horizontal = 16.dp),
                shape = RoundedCornerShape(16.dp),
                colors = CardDefaults.cardColors(containerColor = com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Surface)
            ) {
                Column(
                    modifier = Modifier.padding(12.dp),
                    verticalArrangement = Arrangement.spacedBy(10.dp)
                ) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Column(modifier = Modifier.weight(1f)) {
                            Text("Phone entry mode", fontWeight = FontWeight.SemiBold)
                            Text("Use rows for Serial Number, IMEI 1, and IMEI 2", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                        }
                        Switch(checked = phoneMode, onCheckedChange = { phoneMode = it })
                    }
                    if (phoneMode) {
                        Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                            OutlinedTextField(
                                value = phoneRowCount,
                                onValueChange = { value -> phoneRowCount = value.filter { it.isDigit() }.take(3) },
                                label = { Text("Devices") },
                                modifier = Modifier.weight(1f),
                                singleLine = true,
                                shape = RoundedCornerShape(12.dp)
                            )
                            Button(onClick = { generatePhoneRows() }) { Text("Create") }
                            OutlinedButton(onClick = { tabScanTarget() }) { Text("Tab") }
                            TextButton(onClick = { phoneRows = phoneRows + DirectPhoneEntry() }) { Text("+ Add Row") }
                        }
                    }
                }
            }

            Card(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(horizontal = 16.dp, vertical = 6.dp),
                shape = RoundedCornerShape(16.dp),
                colors = CardDefaults.cardColors(containerColor = com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Surface)
            ) {
                Column(
                    modifier = Modifier.padding(10.dp),
                    verticalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    val readyCount = if (phoneMode) normalizedPhoneRows().count { it.serialNumber.isNotBlank() } else serialNumbers.size
                    val failedCount = scanLog.count { it.status == "Failed" }
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Column(modifier = Modifier.weight(1f)) {
                            Text("Scan session", fontWeight = FontWeight.SemiBold)
                            Text(
                                text = "Ready $readyCount  |  Duplicates ${duplicateScans.size}  |  Failed $failedCount",
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                                maxLines = 1,
                                overflow = TextOverflow.Ellipsis
                            )
                        }
                        TextButton(onClick = { showSessionTools = !showSessionTools }) {
                            Text(if (showSessionTools) "Hide" else "Tools")
                        }
                    }

                    if (showSessionTools) {
                        Row(horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.fillMaxWidth()) {
                            SessionMetric("Ready", readyCount.toString(), Modifier.weight(1f))
                            SessionMetric("Duplicates", duplicateScans.size.toString(), Modifier.weight(1f))
                            SessionMetric("Failed", failedCount.toString(), Modifier.weight(1f))
                        }

                        if (phoneMode) {
                            var targetExpanded by remember { mutableStateOf(false) }
                            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                                Box(modifier = Modifier.weight(1f)) {
                                    OutlinedButton(onClick = { targetExpanded = true }, modifier = Modifier.fillMaxWidth()) {
                                        Text("Scan target: ${phoneScanTarget.label}")
                                    }
                                    DropdownMenu(expanded = targetExpanded, onDismissRequest = { targetExpanded = false }) {
                                        PhoneScanTarget.entries.forEach { target ->
                                            DropdownMenuItem(
                                                text = { Text(target.label) },
                                                onClick = {
                                                    phoneScanTarget = target
                                                    targetExpanded = false
                                                }
                                            )
                                        }
                                    }
                                }
                                TextButton(onClick = { undoLastAcceptedScan() }, enabled = scanLog.any { it.status == "Accepted" && !it.undone && it.undoKey != null }) {
                                    Text("Undo")
                                }
                            }
                            OutlinedTextField(
                                value = bulkImportText,
                                onValueChange = { bulkImportText = it },
                                label = { Text("Bulk paste") },
                                modifier = Modifier.fillMaxWidth(),
                                minLines = 1,
                                maxLines = 3,
                                shape = RoundedCornerShape(12.dp),
                                supportingText = { Text("Use: phone, serial, imei1, imei2") }
                            )
                            Button(onClick = { importBulkPhoneRows(bulkImportText) }, enabled = bulkImportText.isNotBlank()) {
                                Text("Import pasted rows")
                            }
                        } else {
                            TextButton(onClick = { undoLastAcceptedScan() }, enabled = scanLog.any { it.status == "Accepted" && !it.undone && it.undoKey != null }) {
                                Text("Undo last accepted scan")
                            }
                        }

                        if (duplicateScans.isNotEmpty()) {
                            Text("Duplicate scans", fontWeight = FontWeight.SemiBold, color = MaterialTheme.colorScheme.error)
                            duplicateScans.take(2).forEach { duplicate ->
                                Text(
                                    text = "${duplicate.value} - ${duplicate.detail}",
                                    style = MaterialTheme.typography.bodySmall,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                                    maxLines = 1,
                                    overflow = TextOverflow.Ellipsis
                                )
                            }
                        }

                        if (scanLog.isNotEmpty()) {
                            Text("Recent scans", fontWeight = FontWeight.SemiBold)
                            scanLog.take(3).forEach { event ->
                                Text(
                                    text = "${event.status}: ${event.value} - ${event.detail}",
                                    style = MaterialTheme.typography.bodySmall,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                                    maxLines = 1,
                                    overflow = TextOverflow.Ellipsis
                                )
                            }
                        }
                    }
                }
            }            // List Content
            if (phoneMode) {
                Column(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(start = 16.dp, top = 8.dp, end = 16.dp, bottom = 144.dp),
                    verticalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    phoneRows.forEachIndexed { index, row ->
                        val rowCodes = listOf(row.serialNumber, row.imei1, row.imei2).map { normalizeSerial(it) }.filter { it.isNotBlank() }
                        val hasDuplicate = rowCodes.any { code -> phoneRows.sumOf { other -> listOf(other.serialNumber, other.imei1, other.imei2).count { normalizeSerial(it) == code } } > 1 }
                        val rowStatus = when {
                            normalizeSerial(row.serialNumber).isBlank() -> "Missing serial"
                            hasDuplicate -> "Duplicate"
                            else -> "Ready"
                        }
                        DirectPhoneEntryCard(
                            index = index,
                            row = row,
                            status = rowStatus,
                            activeFocusField = focusedPhoneField.takeIf { focusedPhoneRowIndex == index },
                            onFocusedField = { field ->
                                focusedPhoneRowIndex = index
                                focusedPhoneField = field
                                phoneScanTarget = when (field) {
                                    PhoneFocusField.Serial -> PhoneScanTarget.Serial
                                    PhoneFocusField.Imei1 -> PhoneScanTarget.Imei1
                                    PhoneFocusField.Imei2 -> PhoneScanTarget.Imei2
                                    PhoneFocusField.CellPhone -> phoneScanTarget
                                }
                            },
                            onChange = { updated -> updatePhoneRow(index, updated) },
                            onRemove = { phoneRows = phoneRows.filterIndexed { rowIndex, _ -> rowIndex != index }.ifEmpty { listOf(DirectPhoneEntry()) } },
                            canRemove = phoneRows.size > 1
                        )
                    }
                }
            } else if (serialNumbers.isEmpty()) {
                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(320.dp),
                    contentAlignment = Alignment.Center
                ) {
                    Column(
                        horizontalAlignment = Alignment.CenterHorizontally,
                        verticalArrangement = Arrangement.Center,
                        modifier = Modifier.padding(32.dp)
                    ) {
                        Icon(
                            imageVector = Icons.Default.QrCodeScanner,
                            contentDescription = null,
                            modifier = Modifier
                                .size(80.dp)
                                .graphicsLayer { alpha = 0.2f },
                            tint = MaterialTheme.colorScheme.onSurface
                        )
                        Spacer(modifier = Modifier.height(16.dp))
                        Text(
                            text = "Ready to Scan",
                            style = MaterialTheme.typography.titleLarge,
                            fontWeight = FontWeight.Bold,
                            color = MaterialTheme.colorScheme.onSurface.copy(alpha = 0.6f)
                        )
                        Spacer(modifier = Modifier.height(8.dp))
                        Text(
                            text = "Use the camera button or type manually\nto add serials to the batch.",
                            style = MaterialTheme.typography.bodyMedium,
                            textAlign = TextAlign.Center,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                }
            } else {
                Column(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(start = 16.dp, top = 8.dp, end = 16.dp, bottom = 144.dp),
                    verticalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    val reversedSerials = serialNumbers.asReversed()
                    reversedSerials.forEachIndexed { index, serial ->
                        val serialNumberIndex = serialNumbers.size - index
                        SwipeToDismissItem(
                            item = serial,
                            indexLabel = serialNumberIndex,
                            onDismiss = {
                                serialNumbers = serialNumbers.filter { it != serial }
                                scope.launch {
                                    val result = snackbarHostState.showSnackbar(
                                        message = "Serial removed",
                                        actionLabel = "Undo"
                                    )
                                    if (result == SnackbarResult.ActionPerformed) {
                                        serialNumbers = serialNumbers + serial
                                    }
                                }
                            }
                        )
                    }
                }
            }
        }
    }
}
@Composable
private fun SessionMetric(label: String, value: String, modifier: Modifier = Modifier) {
    Surface(
        modifier = modifier,
        shape = RoundedCornerShape(10.dp),
        color = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.55f)
    ) {
        Column(modifier = Modifier.padding(horizontal = 10.dp, vertical = 8.dp)) {
            Text(label, style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
            Text(value, style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
        }
    }
}
@Composable
private fun DirectPhoneEntryCard(
    index: Int,
    row: DirectPhoneEntry,
    status: String,
    activeFocusField: PhoneFocusField?,
    onFocusedField: (PhoneFocusField) -> Unit,
    onChange: (DirectPhoneEntry) -> Unit,
    onRemove: () -> Unit,
    canRemove: Boolean
) {
    val cellRequester = remember { FocusRequester() }
    val serialRequester = remember { FocusRequester() }
    val imei1Requester = remember { FocusRequester() }
    val imei2Requester = remember { FocusRequester() }

    LaunchedEffect(activeFocusField) {
        when (activeFocusField) {
            PhoneFocusField.CellPhone -> cellRequester.requestFocus()
            PhoneFocusField.Serial -> serialRequester.requestFocus()
            PhoneFocusField.Imei1 -> imei1Requester.requestFocus()
            PhoneFocusField.Imei2 -> imei2Requester.requestFocus()
            null -> Unit
        }
    }

    Card(
        modifier = Modifier.fillMaxWidth(),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp),
        shape = RoundedCornerShape(12.dp),
        colors = CardDefaults.cardColors(containerColor = com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Surface)
    ) {
        Column(
            modifier = Modifier.padding(14.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Box(
                    modifier = Modifier
                        .size(28.dp)
                        .background(MaterialTheme.colorScheme.primary, CircleShape),
                    contentAlignment = Alignment.Center
                ) {
                    Text(
                        text = (index + 1).toString(),
                        color = Color.White,
                        style = MaterialTheme.typography.labelMedium,
                        fontWeight = FontWeight.Bold
                    )
                }
                Spacer(Modifier.width(10.dp))
                Column(modifier = Modifier.weight(1f)) {
                    Text("Phone ${index + 1}", fontWeight = FontWeight.Bold)
                    Text(status, style = MaterialTheme.typography.labelSmall, color = if (status == "Ready") MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.error)
                }
                if (canRemove) TextButton(onClick = onRemove) { Text("Remove") }
            }
            OutlinedTextField(
                value = row.cellPhoneNumber,
                onValueChange = { onChange(row.copy(cellPhoneNumber = it)) },
                label = { Text("Cell Phone Number") },
                modifier = Modifier
                    .fillMaxWidth()
                    .focusRequester(cellRequester)
                    .onFocusChanged { if (it.isFocused) onFocusedField(PhoneFocusField.CellPhone) },
                singleLine = true,
                shape = RoundedCornerShape(12.dp)
            )
            OutlinedTextField(
                value = row.serialNumber,
                onValueChange = { onChange(row.copy(serialNumber = it)) },
                label = { Text("Serial Number *") },
                modifier = Modifier
                    .fillMaxWidth()
                    .focusRequester(serialRequester)
                    .onFocusChanged { if (it.isFocused) onFocusedField(PhoneFocusField.Serial) },
                singleLine = true,
                shape = RoundedCornerShape(12.dp)
            )
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                OutlinedTextField(
                    value = row.imei1,
                    onValueChange = { onChange(row.copy(imei1 = it)) },
                    label = { Text("IMEI 1") },
                    modifier = Modifier
                        .weight(1f)
                        .focusRequester(imei1Requester)
                        .onFocusChanged { if (it.isFocused) onFocusedField(PhoneFocusField.Imei1) },
                    singleLine = true,
                    shape = RoundedCornerShape(12.dp)
                )
                OutlinedTextField(
                    value = row.imei2,
                    onValueChange = { onChange(row.copy(imei2 = it)) },
                    label = { Text("IMEI 2") },
                    modifier = Modifier
                        .weight(1f)
                        .focusRequester(imei2Requester)
                        .onFocusChanged { if (it.isFocused) onFocusedField(PhoneFocusField.Imei2) },
                    singleLine = true,
                    shape = RoundedCornerShape(12.dp)
                )
            }
        }
    }
}
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun SwipeToDismissItem(
    item: String,
    indexLabel: Int,
    onDismiss: () -> Unit
) {
    val dismissState = rememberSwipeToDismissBoxState(
        confirmValueChange = {
            if (it == SwipeToDismissBoxValue.EndToStart) {
                onDismiss()
                true
            } else {
                false
            }
        }
    )

    SwipeToDismissBox(
        state = dismissState,
        enableDismissFromStartToEnd = false,
        backgroundContent = {
            val color = if (dismissState.targetValue == SwipeToDismissBoxValue.EndToStart) 
                MaterialTheme.colorScheme.errorContainer 
            else 
                Color.Transparent
                
            Box(
                modifier = Modifier
                    .fillMaxSize()
                    .background(color, RoundedCornerShape(12.dp))
                    .padding(end = 16.dp),
                contentAlignment = Alignment.CenterEnd
            ) {
                Icon(
                    imageVector = Icons.Default.Delete,
                    contentDescription = "Delete",
                    tint = MaterialTheme.colorScheme.onErrorContainer
                )
            }
        },
        content = {
            Card(
                modifier = Modifier.fillMaxWidth(),
                elevation = CardDefaults.cardElevation(defaultElevation = 1.dp),
                shape = RoundedCornerShape(12.dp),
                colors = CardDefaults.cardColors(containerColor = com.example.yakultscanner.ui.components.ScannerWorkspaceUi.Surface)
            ) {
                Row(
                    modifier = Modifier
                        .padding(16.dp)
                        .fillMaxWidth(),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.SpaceBetween
                ) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Box(
                            modifier = Modifier
                                .size(28.dp)
                                .background(MaterialTheme.colorScheme.primary, androidx.compose.foundation.shape.CircleShape),
                            contentAlignment = Alignment.Center
                        ) {
                            Text(
                                text = indexLabel.toString(),
                                color = Color.White,
                                style = MaterialTheme.typography.labelMedium,
                                fontWeight = FontWeight.Bold
                            )
                        }
                        Spacer(modifier = Modifier.width(10.dp))
                        Text(
                            text = item,
                            style = MaterialTheme.typography.titleMedium,
                            fontFamily = androidx.compose.ui.text.font.FontFamily.Monospace,
                            fontWeight = FontWeight.Medium
                        )
                    }
                    Icon(
                        imageVector = Icons.Default.Delete,
                        contentDescription = "Remove",
                        tint = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.4f),
                        modifier = Modifier.size(18.dp)
                    )
                }
            }
        }
    )
}