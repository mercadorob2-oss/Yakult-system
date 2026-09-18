package com.example.yakultscanner.ui.screens

import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.graphics.Matrix
import android.media.ExifInterface
import android.media.MediaMetadataRetriever
import android.net.Uri
import android.util.Base64
import android.widget.MediaController
import android.widget.VideoView
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.PickVisualMediaRequest
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.ExperimentalFoundationApi
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.pager.HorizontalPager
import androidx.compose.foundation.pager.rememberPagerState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.automirrored.filled.ArrowBackIos
import androidx.compose.material.icons.automirrored.filled.ArrowForwardIos
import androidx.compose.material.icons.filled.Build
import androidx.compose.material.icons.filled.CameraAlt
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.Collections
import androidx.compose.material.icons.filled.CloudUpload
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.PlayCircle
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.Videocam
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.compose.ui.viewinterop.AndroidView
import androidx.compose.ui.window.Dialog
import androidx.compose.ui.window.DialogProperties
import androidx.core.content.FileProvider
import androidx.navigation.NavController
import com.example.yakultscanner.api.ApiClient
import com.example.yakultscanner.api.ApiResult
import com.example.yakultscanner.api.RepairPartPhotoUploadRequest
import com.example.yakultscanner.api.RepairPartSummaryDto
import com.example.yakultscanner.api.RepairTicketActionRequest
import com.example.yakultscanner.api.RepairTicketSummaryDto
import com.example.yakultscanner.api.safeApiCall
import com.example.yakultscanner.data.repository.RepairPhotoRepository
import com.example.yakultscanner.ui.components.ApiFailurePanel
import com.example.yakultscanner.ui.components.ListLoadingSkeleton
import kotlinx.coroutines.launch
import java.io.ByteArrayOutputStream
import java.io.File
import java.util.UUID

private fun RepairPartSummaryDto.label() = partDisplayName ?: "Part #$partNumber"

/** One staged-or-uploaded photo/video, held in memory for the duration of this screen — tagged
 * with the Part it belongs to, since a technician can stage media for several Parts before
 * uploading them all together. Both the "ready to upload" review grid and the "uploaded this
 * session" grid use this same shape so the same preview pager works for either. */
private data class MediaItem(
    val id: String = UUID.randomUUID().toString(),
    val uri: Uri,
    val isVideo: Boolean,
    val mimeType: String,
    val previewBitmap: Bitmap?,
    val partId: Int,
    val partLabel: String,
    val sizeBytes: Long,
    val partAttachmentId: Int? = null
)

// The server's request body cap is ~28.6 MB (Web.config's httpRuntime maxRequestLength /
// IIS requestLimits), and base64-encoding a file inflates it by ~33% on top of the small JSON
// wrapper — so raw files need to stay comfortably under that. 20 MB leaves headroom.
private const val MAX_UPLOAD_BYTES = 20L * 1024 * 1024
private const val MAX_UPLOAD_MB = MAX_UPLOAD_BYTES / (1024 * 1024)

/**
 * Second step of the mobile Repair Part photo upload flow — resolves the scanned/typed ticket
 * reference (a plain Repair No., or a scanned "yakult:repair:v1:{guid}" QR token), lets the
 * technician tag photos/videos to one or more Parts, review/drop any before committing, then
 * uploads straight from the phone's camera or gallery instead of copying files to a PC.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun RepairPhotoUploadScreen(navController: NavController, ticketRef: String) {
    RepairSystemBars()
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    val repository = remember { RepairPhotoRepository(ApiClient.service) }

    var isLoading by remember { mutableStateOf(true) }
    var errorUi by remember { mutableStateOf<Pair<String, String>?>(null) }
    var ticket by remember { mutableStateOf<RepairTicketSummaryDto?>(null) }
    var parts by remember { mutableStateOf<List<RepairPartSummaryDto>>(emptyList()) }
    // The Part new captures/picks get tagged with — switching this does NOT clear anything
    // already staged for other Parts, so photos for multiple Parts can be queued together.
    var activePart by remember { mutableStateOf<RepairPartSummaryDto?>(null) }

    var isUploading by remember { mutableStateOf(false) }
    var uploadStatus by remember { mutableStateOf<String?>(null) }
    // Staged media — picked/captured but not yet uploaded, so the technician can drop any before
    // committing. Spans every Part tagged so far, not just the currently active one.
    var pendingMedia by remember { mutableStateOf<List<MediaItem>>(emptyList()) }
    // Successfully uploaded this session, across all Parts — "see what I just uploaded" without
    // needing a new read endpoint from the server.
    var uploadedMedia by remember { mutableStateOf<List<MediaItem>>(emptyList()) }
    var pendingCameraUri by remember { mutableStateOf<Uri?>(null) }
    var stagingWarning by remember { mutableStateOf<String?>(null) }
    // 50 MB per-ticket evidence cap (whole-equipment + every Part combined) — server-enforced on
    // every upload; mirrored here so staging can warn *before* the technician even tries.
    var evidenceUsed by remember { mutableStateOf(0L) }
    var evidenceLimit by remember { mutableStateOf(50L * 1024 * 1024) }

    // Fullscreen preview state — which list is being previewed and at what index. Shared by both
    // the "ready to upload" and "uploaded" grids.
    var previewList by remember { mutableStateOf<List<MediaItem>?>(null) }
    var previewIndex by remember { mutableStateOf(0) }
    var uploadedMediaForDelete by remember { mutableStateOf<MediaItem?>(null) }
    var isDeleting by remember { mutableStateOf(false) }

    suspend fun lookup() {
        isLoading = true
        errorUi = null
        val isToken = ticketRef.startsWith("yakult:", ignoreCase = true)
        when (val result = safeApiCall {
            repository.lookupRepairTicket(
                ticketCode = if (isToken) null else ticketRef,
                token = if (isToken) ticketRef else null
            )
        }) {
            is ApiResult.Success -> {
                ticket = result.data.ticket
                parts = result.data.parts
                result.data.ticket?.let {
                    evidenceUsed = it.evidenceBytesUsed
                    evidenceLimit = it.evidenceBytesLimit
                }
                if (parts.size == 1) activePart = parts.first()
            }
            is ApiResult.HttpError -> errorUi = "Couldn't find that repair ticket." to (result.message ?: "HTTP ${result.code}")
            is ApiResult.NetworkError -> errorUi = "Network issue while looking up the ticket." to (result.message ?: "")
            is ApiResult.UnknownError -> errorUi = "Unexpected error while looking up the ticket." to (result.message ?: "")
        }
        isLoading = false
    }

    LaunchedEffect(ticketRef) { lookup() }

    fun fileSizeBytes(uri: Uri): Long {
        return try {
            context.contentResolver.openFileDescriptor(uri, "r")?.use { it.statSize } ?: -1L
        } catch (e: Exception) { -1L }
    }

    fun exifRotationDegrees(uri: Uri): Int {
        return try {
            context.contentResolver.openInputStream(uri)?.use { stream ->
                when (ExifInterface(stream).getAttributeInt(ExifInterface.TAG_ORIENTATION, ExifInterface.ORIENTATION_NORMAL)) {
                    ExifInterface.ORIENTATION_ROTATE_90 -> 90
                    ExifInterface.ORIENTATION_ROTATE_180 -> 180
                    ExifInterface.ORIENTATION_ROTATE_270 -> 270
                    else -> 0
                }
            } ?: 0
        } catch (e: Exception) { 0 }
    }

    fun loadImageItem(uri: Uri, part: RepairPartSummaryDto, sizeBytes: Long): MediaItem? {
        val bitmap = try {
            context.contentResolver.openInputStream(uri)?.use { BitmapFactory.decodeStream(it) }
        } catch (e: Exception) { null } ?: return null
        // Camera sensor data (and some gallery content) stores pixels in a fixed orientation with
        // the actual "how to display this" rotation recorded separately in EXIF — BitmapFactory
        // ignores that tag, so a portrait shot decodes as landscape unless corrected here.
        val degrees = exifRotationDegrees(uri)
        val oriented = if (degrees == 0) bitmap else {
            val matrix = Matrix().apply { postRotate(degrees.toFloat()) }
            Bitmap.createBitmap(bitmap, 0, 0, bitmap.width, bitmap.height, matrix, true)
        }
        val mime = context.contentResolver.getType(uri) ?: "image/jpeg"
        return MediaItem(uri = uri, isVideo = false, mimeType = mime, previewBitmap = oriented, partId = part.repairPartId, partLabel = part.label(), sizeBytes = sizeBytes)
    }

    fun loadVideoItem(uri: Uri, part: RepairPartSummaryDto, sizeBytes: Long): MediaItem {
        val frame = try {
            val retriever = MediaMetadataRetriever()
            try {
                retriever.setDataSource(context, uri)
                retriever.getFrameAtTime(0)
            } finally {
                retriever.release()
            }
        } catch (e: Exception) { null }
        val mime = context.contentResolver.getType(uri) ?: "video/mp4"
        return MediaItem(uri = uri, isVideo = true, mimeType = mime, previewBitmap = frame, partId = part.repairPartId, partLabel = part.label(), sizeBytes = sizeBytes)
    }

    suspend fun uploadMedia(items: List<MediaItem>) {
        if (items.isEmpty()) return

        isUploading = true
        var uploadedCount = 0
        var failureMessage: String? = null

        for (item in items) {
            val base64: String
            try {
                if (item.isVideo) {
                    // Videos are sent as-is (no re-encoding available client-side) — kept small by
                    // the server's request-size cap, so this only suits short clips.
                    val bytes = context.contentResolver.openInputStream(item.uri)?.use { it.readBytes() }
                        ?: throw IllegalStateException("Couldn't read video file")
                    base64 = Base64.encodeToString(bytes, Base64.DEFAULT)
                } else {
                    val stream = ByteArrayOutputStream()
                    (item.previewBitmap ?: throw IllegalStateException("Missing image data"))
                        .compress(Bitmap.CompressFormat.JPEG, 80, stream)
                    base64 = Base64.encodeToString(stream.toByteArray(), Base64.DEFAULT)
                }
            } catch (e: Exception) {
                failureMessage = "Couldn't read one of the selected files."
                continue
            }

            when (val result = safeApiCall {
                repository.uploadRepairPartPhoto(
                    RepairPartPhotoUploadRequest(
                        repairPartId = item.partId,
                        imageBase64 = base64,
                        mimeType = item.mimeType
                    )
                )
            }) {
                is ApiResult.Success -> {
                    uploadedCount += 1
                    uploadedMedia = uploadedMedia + item.copy(partAttachmentId = result.data.partAttachmentId)
                    evidenceUsed = result.data.evidenceBytesUsed ?: (evidenceUsed + item.sizeBytes)
                    result.data.evidenceBytesLimit?.let { evidenceLimit = it }
                }
                is ApiResult.HttpError -> failureMessage = result.message ?: "Upload failed (HTTP ${result.code})"
                is ApiResult.NetworkError -> failureMessage = result.message ?: "Network error during upload"
                is ApiResult.UnknownError -> failureMessage = result.message ?: "Unexpected error during upload"
            }
        }

        isUploading = false
        uploadStatus = failureMessage
            ?: if (uploadedCount == 1) "1 file uploaded successfully." else "$uploadedCount files uploaded successfully."
    }

    suspend fun deleteUploadedMedia(item: MediaItem) {
        val ticketId = ticket?.repairTicketId ?: 0
        val partAttachmentId = item.partAttachmentId
        if (ticketId <= 0 || partAttachmentId == null) {
            uploadStatus = "This upload cannot be deleted yet. Refresh the ticket and try again."
            return
        }

        isDeleting = true
        try {
            when (val result = safeApiCall {
                repository.deleteRepairPartPhoto(ticketId, partAttachmentId)
            }) {
                is ApiResult.Success -> {
                    if (result.data.success) {
                        uploadedMedia = uploadedMedia.filter { it.id != item.id }
                        uploadStatus = result.data.message ?: "Evidence deleted successfully."
                        lookup()
                    } else {
                        uploadStatus = result.data.message ?: "Evidence could not be deleted."
                    }
                }
                is ApiResult.HttpError -> uploadStatus = result.message ?: "Delete failed (HTTP ${result.code})"
                is ApiResult.NetworkError -> uploadStatus = result.message ?: "Network error while deleting evidence"
                is ApiResult.UnknownError -> uploadStatus = result.message ?: "Unexpected error while deleting evidence"
            }
        } finally {
            isDeleting = false
        }
    }

    // How much of the 50 MB ticket budget is still free, accounting for what's already staged
    // (not yet uploaded) on top of what the server already has recorded.
    fun remainingTicketBudget(): Long = evidenceLimit - evidenceUsed - pendingMedia.sumOf { it.sizeBytes }

    fun ticketCapMessage(): String {
        val limitMb = evidenceLimit / (1024 * 1024)
        val usedMb = (evidenceUsed + pendingMedia.sumOf { it.sizeBytes }) / (1024 * 1024)
        return "Skipped — this ticket's $limitMb MB evidence limit is reached ($usedMb MB used). Delete some evidence first."
    }

    fun buildStagingWarning(rejectedForSize: Int, rejectedForTicketCap: Int): String? {
        val parts = mutableListOf<String>()
        if (rejectedForSize > 0)
            parts += "$rejectedForSize file${if (rejectedForSize == 1) "" else "s"} skipped — over the $MAX_UPLOAD_MB MB upload limit."
        if (rejectedForTicketCap > 0)
            parts += "$rejectedForTicketCap file${if (rejectedForTicketCap == 1) "" else "s"} skipped — ticket's evidence limit reached."
        return if (parts.isEmpty()) null else parts.joinToString(" ")
    }

    // "X MB used from Y MB, Z MB remaining" — live ticket evidence usage, updated after lookup
    // and after every successful upload, shown near the ticket header.
    val evidenceUsageText by remember {
        derivedStateOf {
            val usedMb = evidenceUsed / (1024 * 1024)
            val limitMb = evidenceLimit / (1024 * 1024)
            val remainingMb = ((evidenceLimit - evidenceUsed).coerceAtLeast(0)) / (1024 * 1024)
            "$usedMb MB used from $limitMb MB, $remainingMb MB remaining"
        }
    }

    val galleryPicker = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.PickMultipleVisualMedia()
    ) { uris ->
        val part = activePart ?: return@rememberLauncherForActivityResult
        if (uris.isEmpty()) return@rememberLauncherForActivityResult
        scope.launch {
            var rejectedForSize = 0
            var rejectedForTicketCap = 0
            var budget = remainingTicketBudget()
            val items = uris.mapNotNull { uri ->
                val size = fileSizeBytes(uri)
                when {
                    size !in 0..MAX_UPLOAD_BYTES -> { rejectedForSize += 1; null }
                    size > budget -> { rejectedForTicketCap += 1; null }
                    else -> {
                        budget -= size
                        val mime = context.contentResolver.getType(uri) ?: ""
                        if (mime.startsWith("video/")) loadVideoItem(uri, part, size) else loadImageItem(uri, part, size)
                    }
                }
            }
            pendingMedia = pendingMedia + items
            stagingWarning = buildStagingWarning(rejectedForSize, rejectedForTicketCap)
        }
    }

    val cameraPhotoLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.TakePicture()
    ) { success ->
        val uri = pendingCameraUri
        val part = activePart
        if (success && uri != null && part != null) {
            scope.launch {
                val size = fileSizeBytes(uri)
                when {
                    size !in 0..MAX_UPLOAD_BYTES -> stagingWarning = "Photo skipped — over the $MAX_UPLOAD_MB MB upload limit."
                    size > remainingTicketBudget() -> stagingWarning = ticketCapMessage()
                    else -> {
                        loadImageItem(uri, part, size)?.let { pendingMedia = pendingMedia + it }
                        stagingWarning = null
                    }
                }
            }
        }
    }

    val cameraVideoLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.CaptureVideo()
    ) { success ->
        val uri = pendingCameraUri
        val part = activePart
        if (success && uri != null && part != null) {
            scope.launch {
                val size = fileSizeBytes(uri)
                when {
                    size !in 0..MAX_UPLOAD_BYTES -> {
                        val sizeMb = if (size > 0) size / (1024 * 1024) else -1
                        stagingWarning = "Video skipped — ${sizeMb}MB is over the $MAX_UPLOAD_MB MB upload limit. Try a shorter clip or lower resolution."
                    }
                    size > remainingTicketBudget() -> stagingWarning = ticketCapMessage()
                    else -> {
                        pendingMedia = pendingMedia + loadVideoItem(uri, part, size)
                        stagingWarning = null
                    }
                }
            }
        }
    }

    fun newCameraUri(extension: String): Uri {
        val file = File(context.filesDir, "repair_media_${System.currentTimeMillis()}.$extension")
        return FileProvider.getUriForFile(context, "${context.packageName}.fileprovider", file)
    }

    fun launchCameraPhoto() {
        val uri = newCameraUri("jpg")
        pendingCameraUri = uri
        cameraPhotoLauncher.launch(uri)
    }

    fun launchCameraVideo() {
        val uri = newCameraUri("mp4")
        pendingCameraUri = uri
        cameraVideoLauncher.launch(uri)
    }

    Scaffold(
        containerColor = RepairUi.Canvas,
        topBar = {
            TopAppBar(
                title = {
                    Column(verticalArrangement = Arrangement.spacedBy(1.dp)) {
                        Text("Repair evidence", fontWeight = androidx.compose.ui.text.font.FontWeight.Bold, color = RepairUi.Ink)
                        Text("Capture, tag, review, then upload", style = MaterialTheme.typography.labelSmall, color = RepairUi.Muted)
                    }
                },
                navigationIcon = {
                    IconButton(onClick = { navController.popBackStack() }) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back", tint = RepairUi.Ink)
                    }
                },
                actions = {
                    IconButton(onClick = { scope.launch { lookup() } }, enabled = !isLoading) {
                        Icon(Icons.Filled.Refresh, contentDescription = "Refresh Parts list", tint = RepairUi.Ink)
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
                .padding(16.dp)
        ) {
            when {
                isLoading -> ListLoadingSkeleton(modifier = Modifier.fillMaxSize())

                errorUi != null -> ApiFailurePanel(
                    message = errorUi!!.first,
                    details = errorUi!!.second,
                    retryLabel = "Retry",
                    onRetry = { scope.launch { lookup() } }
                )

                else -> {
                    ticket?.let { t ->
                        RepairSurfaceCard(tonalColor = RepairUi.Surface) {
                            Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.Top) {
                                Column(modifier = Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(3.dp)) {
                                    Text(text = t.ticketCode ?: ticketRef, style = MaterialTheme.typography.titleLarge, color = RepairUi.Ink, fontWeight = androidx.compose.ui.text.font.FontWeight.Bold)
                                    Text(text = t.itemName ?: "Repair equipment", style = MaterialTheme.typography.bodyMedium, color = RepairUi.Ink)
                                }
                                RepairStatusPill(t.status)
                            }
                            Text(
                                text = evidenceUsageText,
                                style = MaterialTheme.typography.bodySmall,
                                color = if (remainingTicketBudget() <= 0) RepairUi.Critical else RepairUi.Muted
                            )
                        }
                    }

                    Spacer(modifier = Modifier.height(16.dp))
                    RepairSectionHeading("Tag evidence to a part", "Pick a part, add photos or videos, then switch parts without losing staged files.")
                    Spacer(modifier = Modifier.height(8.dp))

                    if (parts.isEmpty()) {
                        Text(
                            text = "This ticket has no Parts logged yet.",
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    } else {
                        LazyColumn(verticalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.weight(1f, fill = false)) {
                            items(parts) { part ->
                                val isActive = activePart?.repairPartId == part.repairPartId
                                val queuedCount = pendingMedia.count { it.partId == part.repairPartId }
                                RepairSurfaceCard(
                                    modifier = Modifier.clickable { activePart = part },
                                    tonalColor = if (isActive) RepairUi.ActiveSoft else RepairUi.Surface
                                ) {
                                    Row(
                                        modifier = Modifier.fillMaxWidth(),
                                        verticalAlignment = Alignment.CenterVertically,
                                        horizontalArrangement = Arrangement.spacedBy(10.dp)
                                    ) {
                                        Box(
                                            modifier = Modifier
                                                .size(32.dp)
                                                .clip(RoundedCornerShape(8.dp))
                                                .background(if (isActive) RepairUi.BrandSoft else RepairUi.SurfaceSubtle),
                                            contentAlignment = Alignment.Center
                                        ) {
                                            Icon(
                                                if (isActive) Icons.Filled.CheckCircle else Icons.Filled.Build,
                                                contentDescription = null,
                                                modifier = Modifier.size(18.dp),
                                                tint = if (isActive) RepairUi.Brand else RepairUi.Muted
                                            )
                                        }
                                        Column(modifier = Modifier.weight(1f)) {
                                            Text(text = part.label(), style = MaterialTheme.typography.bodyLarge, color = RepairUi.Ink, fontWeight = androidx.compose.ui.text.font.FontWeight.SemiBold)
                                            part.status?.takeIf { it.isNotBlank() }?.let {
                                                Text(text = it, style = MaterialTheme.typography.bodySmall, color = RepairUi.Muted)
                                            }
                                        }
                                        if (queuedCount > 0) {
                                            Text(
                                                text = "$queuedCount queued",
                                                color = RepairUi.Brand,
                                                style = MaterialTheme.typography.labelSmall,
                                                fontWeight = androidx.compose.ui.text.font.FontWeight.Bold,
                                                modifier = Modifier
                                                    .clip(RoundedCornerShape(6.dp))
                                                    .background(RepairUi.BrandSoft)
                                                    .padding(horizontal = 8.dp, vertical = 4.dp)
                                            )
                                        }
                                    }
                                }
                            }
                        }
                    }

                    Spacer(modifier = Modifier.height(16.dp))

                    Row(horizontalArrangement = Arrangement.spacedBy(10.dp), modifier = Modifier.fillMaxWidth()) {
                        RepairPrimaryButton(
                            onClick = { launchCameraPhoto() },
                            enabled = activePart != null && !isUploading,
                            modifier = Modifier.weight(1f)
                        ) {
                            Icon(Icons.Filled.CameraAlt, contentDescription = null)
                            Spacer(Modifier.width(6.dp))
                            Text("Photo")
                        }
                        RepairPrimaryButton(
                            onClick = { launchCameraVideo() },
                            enabled = activePart != null && !isUploading,
                            modifier = Modifier.weight(1f)
                        ) {
                            Icon(Icons.Filled.Videocam, contentDescription = null)
                            Spacer(Modifier.width(6.dp))
                            Text("Video")
                        }
                        RepairSecondaryButton(
                            onClick = {
                                galleryPicker.launch(PickVisualMediaRequest(ActivityResultContracts.PickVisualMedia.ImageAndVideo))
                            },
                            enabled = activePart != null && !isUploading,
                            modifier = Modifier.weight(1.15f)
                        ) {
                            Icon(Icons.Filled.Collections, contentDescription = null)
                            Spacer(Modifier.width(6.dp))
                            Text("Gallery")
                        }
                    }
                    activePart?.let {
                        Text(
                            text = "Adding to: ${it.label()}",
                            style = MaterialTheme.typography.bodySmall,
                            color = RepairUi.Muted,
                            modifier = Modifier.padding(top = 4.dp)
                        )
                    }

                    stagingWarning?.let { warning ->
                        Spacer(modifier = Modifier.height(8.dp))
                        Text(text = warning, color = MaterialTheme.colorScheme.error, style = MaterialTheme.typography.bodySmall)
                    }

                    MediaGridSection(
                        title = { count -> "Ready to Upload ($count) — tap to preview, remove any before uploading" },
                        items = pendingMedia,
                        showRemove = true,
                        onRemove = { item -> pendingMedia = pendingMedia.filter { it.id != item.id } },
                        onTap = { index -> previewList = pendingMedia; previewIndex = index }
                    )

                    if (pendingMedia.isNotEmpty()) {
                        Spacer(modifier = Modifier.height(12.dp))
                        RepairPrimaryButton(
                            onClick = {
                                scope.launch {
                                    val toUpload = pendingMedia
                                    pendingMedia = emptyList()
                                    uploadMedia(toUpload)
                                }
                            },
                            enabled = !isUploading,
                            modifier = Modifier.fillMaxWidth()
                        ) {
                            Icon(Icons.Filled.CloudUpload, contentDescription = null)
                            Spacer(modifier = Modifier.width(8.dp))
                            Text("Upload ${pendingMedia.size} File${if (pendingMedia.size == 1) "" else "s"}")
                        }
                    }

                    if (isUploading) {
                        Spacer(modifier = Modifier.height(10.dp))
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            CircularProgressIndicator(modifier = Modifier.size(18.dp), strokeWidth = 2.dp)
                            Spacer(modifier = Modifier.width(8.dp))
                            Text("Uploading...")
                        }
                    }

                    uploadStatus?.let { status ->
                        Spacer(modifier = Modifier.height(10.dp))
                        Text(text = status, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }

                    MediaGridSection(
                        title = { count -> "Uploaded this session ($count) — tap to preview or delete" },
                        items = uploadedMedia,
                        showRemove = true,
                        onRemove = { item ->
                            if (!isDeleting) {
                                if (item.partAttachmentId == null) {
                                    uploadStatus = "This upload cannot be deleted yet. Refresh the ticket and try again."
                                } else {
                                    uploadedMediaForDelete = item
                                }
                            }
                        },
                        onTap = { index -> previewList = uploadedMedia; previewIndex = index }
                    )
                }
            }
        }
    }

    uploadedMediaForDelete?.let { item ->
        AlertDialog(
            onDismissRequest = { uploadedMediaForDelete = null },
            containerColor = RepairUi.Surface,
            title = { Text("Delete uploaded evidence?") },
            text = {
                Text(
                    "Delete this ${if (item.isVideo) "video" else "image"} from ${item.partLabel}? This action cannot be undone."
                )
            },
            confirmButton = {
                RepairPrimaryButton(
                    onClick = {
                        uploadedMediaForDelete = null
                        scope.launch { deleteUploadedMedia(item) }
                    }
                ) { Text("Delete") }
            },
            dismissButton = {
                TextButton(onClick = { uploadedMediaForDelete = null }) {
                    Text("Cancel")
                }
            }
        )
    }

    val listForPreview = previewList
    if (listForPreview != null && listForPreview.isNotEmpty()) {
        MediaPreviewDialog(
            items = listForPreview,
            startIndex = previewIndex.coerceIn(0, listForPreview.size - 1),
            onDismiss = { previewList = null }
        )
    }
}

@Composable
private fun MediaGridSection(
    title: (Int) -> String,
    items: List<MediaItem>,
    showRemove: Boolean,
    onRemove: (MediaItem) -> Unit,
    onTap: (Int) -> Unit
) {
    if (items.isEmpty()) return
    Spacer(modifier = Modifier.height(16.dp))
    Text(text = title(items.size), style = MaterialTheme.typography.titleSmall)
    Spacer(modifier = Modifier.height(8.dp))
    LazyRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
        items(items.size) { index ->
            val item = items[index]
            Box(modifier = Modifier.width(96.dp).height(104.dp)) {
                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(88.dp)
                        .clip(RoundedCornerShape(8.dp))
                        .background(Color.Black)
                        .clickable { onTap(index) }
                ) {
                    if (item.previewBitmap != null) {
                        Image(
                            bitmap = item.previewBitmap.asImageBitmap(),
                            contentDescription = if (item.isVideo) "Video pending" else "Photo pending",
                            modifier = Modifier.fillMaxSize()
                        )
                    }
                    if (item.isVideo) {
                        Icon(
                            Icons.Filled.PlayCircle,
                            contentDescription = "Video",
                            tint = Color.White,
                            modifier = Modifier.align(Alignment.Center).size(28.dp)
                        )
                    }

                    if (showRemove) {
                        IconButton(
                            onClick = { onRemove(item) },
                            modifier = Modifier
                                .align(Alignment.TopEnd)
                                .size(30.dp)
                                .background(MaterialTheme.colorScheme.error, RoundedCornerShape(8.dp))
                        ) {
                            Icon(
                                Icons.Filled.Delete,
                                contentDescription = "Delete evidence",
                                tint = MaterialTheme.colorScheme.onError,
                                modifier = Modifier.size(14.dp)
                            )
                        }
                    }
                }
                Text(
                    text = item.partLabel,
                    style = MaterialTheme.typography.labelSmall,
                    maxLines = 1,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier
                        .align(Alignment.BottomCenter)
                        .fillMaxWidth()
                        .padding(top = 2.dp)
                )
            }
        }
    }
}

/** Fullscreen preview — swipe left/right (HorizontalPager) or tap the < / > buttons to navigate.
 * Images render directly from the in-memory bitmap; videos play via a VideoView pointed at the
 * original content Uri (no extra player dependency needed for local playback). Shows which Part
 * each item belongs to, since the queue can span several Parts at once. */
@OptIn(ExperimentalFoundationApi::class)
@Composable
private fun MediaPreviewDialog(items: List<MediaItem>, startIndex: Int, onDismiss: () -> Unit) {
    val pagerState = rememberPagerState(initialPage = startIndex) { items.size }
    val scope = rememberCoroutineScope()

    Dialog(
        onDismissRequest = onDismiss,
        properties = DialogProperties(usePlatformDefaultWidth = false)
    ) {
        Box(
            modifier = Modifier
                .fillMaxSize()
                .background(Color.Black)
        ) {
            HorizontalPager(state = pagerState, modifier = Modifier.fillMaxSize()) { page ->
                val item = items[page]
                Box(modifier = Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                    if (item.isVideo) {
                        AndroidView(
                            modifier = Modifier.fillMaxSize(),
                            factory = { ctx ->
                                VideoView(ctx).apply {
                                    setVideoURI(item.uri)
                                    setMediaController(MediaController(ctx).also { it.setAnchorView(this) })
                                    setOnPreparedListener { start() }
                                }
                            }
                        )
                    } else if (item.previewBitmap != null) {
                        Image(
                            bitmap = item.previewBitmap.asImageBitmap(),
                            contentDescription = "Preview",
                            modifier = Modifier.fillMaxSize()
                        )
                    }
                }
            }

            IconButton(
                onClick = onDismiss,
                modifier = Modifier
                    .align(Alignment.TopEnd)
                    .padding(12.dp)
                    .background(Color.Black.copy(alpha = 0.4f), RoundedCornerShape(6.dp))
            ) {
                Icon(Icons.Filled.Close, contentDescription = "Close", tint = Color.White)
            }

            if (pagerState.currentPage > 0) {
                IconButton(
                    onClick = { scope.launch { pagerState.animateScrollToPage(pagerState.currentPage - 1) } },
                    modifier = Modifier
                        .align(Alignment.CenterStart)
                        .padding(8.dp)
                        .background(Color.Black.copy(alpha = 0.4f), RoundedCornerShape(6.dp))
                ) {
                    Icon(Icons.AutoMirrored.Filled.ArrowBackIos, contentDescription = "Previous", tint = Color.White)
                }
            }
            if (pagerState.currentPage < items.size - 1) {
                IconButton(
                    onClick = { scope.launch { pagerState.animateScrollToPage(pagerState.currentPage + 1) } },
                    modifier = Modifier
                        .align(Alignment.CenterEnd)
                        .padding(8.dp)
                        .background(Color.Black.copy(alpha = 0.4f), RoundedCornerShape(6.dp))
                ) {
                    Icon(Icons.AutoMirrored.Filled.ArrowForwardIos, contentDescription = "Next", tint = Color.White)
                }
            }

            Column(
                horizontalAlignment = Alignment.CenterHorizontally,
                modifier = Modifier
                    .align(Alignment.BottomCenter)
                    .padding(16.dp)
                    .background(Color.Black.copy(alpha = 0.4f), RoundedCornerShape(12.dp))
                    .padding(horizontal = 14.dp, vertical = 8.dp)
            ) {
                Text(
                    text = items[pagerState.currentPage].partLabel,
                    color = Color(0xFF60A5FA),
                    style = MaterialTheme.typography.bodyMedium
                )
                Text(
                    text = "${pagerState.currentPage + 1} / ${items.size}",
                    color = Color.White,
                    style = MaterialTheme.typography.bodySmall
                )
            }
        }
    }
}
