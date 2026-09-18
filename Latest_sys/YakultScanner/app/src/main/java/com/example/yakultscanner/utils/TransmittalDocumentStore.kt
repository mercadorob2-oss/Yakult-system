package com.example.yakultscanner.utils

import android.content.Context
import android.content.Intent
import android.database.Cursor
import android.net.Uri
import android.provider.OpenableColumns
import android.webkit.MimeTypeMap
import androidx.core.content.FileProvider
import com.example.yakultscanner.data.model.GatepassReport
import com.example.yakultscanner.data.model.TransmittalReport
import java.io.File
import java.io.FileOutputStream
import java.text.DateFormat
import java.util.Date
import java.util.Locale

data class SavedAppFile(
    val file: File,
    val mimeType: String = mimeTypeForFileName(file.name)
) {
    val displayName: String get() = file.name
    val sizeBytes: Long get() = file.length()
    val modifiedAt: Long get() = file.lastModified()
    val extension: String get() = file.extension.lowercase(Locale.ROOT)
    val isPdf: Boolean get() = extension == "pdf" || mimeType.equals("application/pdf", ignoreCase = true)
    val formatLabel: String get() = fileFormatLabel(extension, mimeType)
}

private const val ARCHIVE_DIRECTORY = "saved-transmittal-files"

/**
 * App-managed file archive. The directory name is retained for backward compatibility with files
 * already saved by older Transmittal builds, but the archive now accepts every file format.
 * Files from outside the app are copied here only after the user explicitly selects them through
 * Android's Storage Access Framework.
 */
object AppFileStore {

    fun archiveWorkbook(
        context: Context,
        workbook: SavedTransmittalWorkbook
    ): Result<SavedAppFile> = archiveUri(
        context = context,
        sourceUri = workbook.uri,
        preferredName = workbook.displayName
    )

    fun savePrintReadyPdf(
        context: Context,
        report: TransmittalReport
    ): Result<SavedAppFile> = runCatching {
        val temporaryPdf = createTransmittalPrintPdf(context, report)
        archiveUri(
            context = context,
            sourceUri = temporaryPdf,
            preferredName = "Transmittal_${fileStamp()}_print_fit_v2.pdf",
            sourceMimeType = "application/pdf"
        ).getOrThrow()
    }

    fun saveGatepassPdf(
        context: Context,
        report: GatepassReport
    ): Result<SavedAppFile> = runCatching {
        val temporaryPdf = createGatepassPrintPdf(context, report)
        archiveUri(
            context = context,
            sourceUri = temporaryPdf,
            preferredName = "Gatepass_${fileStamp()}_local.pdf",
            sourceMimeType = "application/pdf"
        ).getOrThrow()
    }

    fun importUri(context: Context, uri: Uri): Result<SavedAppFile> = runCatching {
        val sourceMimeType = context.contentResolver.getType(uri)
        val preferredName = withMimeExtension(
            queryDisplayName(context, uri)
                ?: "Imported_${fileStamp()}${mimeExtension(sourceMimeType)}",
            sourceMimeType
        )
        archiveUri(
            context = context,
            sourceUri = uri,
            preferredName = preferredName,
            sourceMimeType = sourceMimeType
        ).getOrThrow()
    }

    fun list(context: Context): List<SavedAppFile> =
        archiveDirectory(context).listFiles()
            ?.filter(File::isFile)
            ?.map { candidate -> SavedAppFile(candidate) }
            ?.sortedByDescending(SavedAppFile::modifiedAt)
            .orEmpty()

    fun uriFor(context: Context, file: SavedAppFile): Uri =
        FileProvider.getUriForFile(context, "${context.packageName}.fileprovider", file.file)

    /**
     * Opens a saved file through Android's normal "Open with" chooser.
     *
     * The primary intent uses the file's exact MIME type. Some spreadsheet apps still
     * advertise the legacy XLS MIME type for both .xls and .xlsx files, so compatible
     * legacy handlers are added as explicit chooser alternatives when Android exposes them.
     * We never broaden the intent to application/octet-stream because that would produce
     * unrelated or misleading chooser entries.
     */
    fun open(context: Context, file: SavedAppFile): Boolean = runCatching {
        val primaryIntent = buildViewIntent(context, file, file.mimeType)
        val primaryComponents = resolvedComponents(context, primaryIntent)
        val alternativeIntents = buildAlternativeViewIntents(
            context = context,
            file = file,
            excludedComponents = primaryComponents.toSet()
        )

        // If no exact-MIME handler exists, use the first verified legacy handler as the
        // chooser's target and keep any remaining handlers as additional chooser entries.
        val chooserTarget = if (primaryComponents.isNotEmpty()) {
            primaryIntent
        } else {
            alternativeIntents.firstOrNull() ?: primaryIntent
        }
        val additionalIntents = if (primaryComponents.isNotEmpty()) {
            alternativeIntents
        } else {
            alternativeIntents.drop(1)
        }

        val chooser = Intent.createChooser(chooserTarget, "Open ${file.displayName}")
        if (additionalIntents.isNotEmpty()) {
            chooser.putExtra(Intent.EXTRA_INITIAL_INTENTS, additionalIntents.toTypedArray())
        }
        context.startActivity(chooser)
        true
    }.getOrDefault(false)

    fun share(context: Context, file: SavedAppFile): Boolean = runCatching {
        val uri = uriFor(context, file)
        context.startActivity(
            Intent(Intent.ACTION_SEND).apply {
                type = file.mimeType
                putExtra(Intent.EXTRA_STREAM, uri)
                clipData = android.content.ClipData.newRawUri(file.displayName, uri)
                addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
            }.let { Intent.createChooser(it, "Share ${file.displayName}") }
        )
        true
    }.getOrDefault(false)

    private fun buildViewIntent(
        context: Context,
        file: SavedAppFile,
        mimeType: String
    ): Intent {
        val uri = uriFor(context, file)
        return Intent(Intent.ACTION_VIEW).apply {
            setDataAndType(uri, mimeType)
            clipData = android.content.ClipData.newRawUri(file.displayName, uri)
            addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
        }
    }

    private fun resolvedComponents(
        context: Context,
        intent: Intent
    ): List<android.content.ComponentName> {
        return context.packageManager
            .queryIntentActivities(intent, android.content.pm.PackageManager.MATCH_DEFAULT_ONLY)
            .mapNotNull { resolveInfo ->
                val activityInfo = resolveInfo.activityInfo ?: return@mapNotNull null
                android.content.ComponentName(activityInfo.packageName, activityInfo.name)
            }
            .distinct()
    }

    private fun buildAlternativeViewIntents(
        context: Context,
        file: SavedAppFile,
        excludedComponents: Set<android.content.ComponentName>
    ): List<Intent> {
        val seen = excludedComponents.toMutableSet()
        return compatibleOpenMimeTypes(file).flatMap { mimeType ->
            val candidate = buildViewIntent(context, file, mimeType)
            resolvedComponents(context, candidate).mapNotNull { componentName ->
                if (!seen.add(componentName)) {
                    null
                } else {
                    Intent(candidate).apply { component = componentName }
                }
            }
        }
    }

    internal fun compatibleOpenMimeTypes(file: SavedAppFile): List<String> = when (file.extension) {
        "xlsx" -> listOf("application/vnd.ms-excel")
        "xls" -> listOf("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
        else -> emptyList()
    }

    private fun archiveUri(
        context: Context,
        sourceUri: Uri,
        preferredName: String,
        sourceMimeType: String? = null
    ): Result<SavedAppFile> = runCatching {
        val target = uniqueFile(archiveDirectory(context), preferredName)
        context.contentResolver.openInputStream(sourceUri)?.use { input ->
            FileOutputStream(target).use { output -> input.copyTo(output) }
        } ?: throw IllegalStateException("Couldn't read the selected file.")
        SavedAppFile(
            file = target,
            mimeType = sourceMimeType?.takeIf { it.isNotBlank() } ?: mimeTypeForFileName(target.name)
        )
    }

    private fun archiveDirectory(context: Context): File =
        File(context.filesDir, ARCHIVE_DIRECTORY).apply { mkdirs() }

    private fun uniqueFile(directory: File, preferredName: String): File {
        val safeName = preferredName
            .substringAfterLast('/')
            .substringAfterLast('\\')
            .replace(Regex("[^A-Za-z0-9._ -]"), "_")
            .trim()
            .ifBlank { "Imported_file" }
        val dotIndex = safeName.lastIndexOf('.')
        val hasExtension = dotIndex > 0 && dotIndex < safeName.lastIndex
        val baseName = if (hasExtension) safeName.substring(0, dotIndex) else safeName
        val extension = if (hasExtension) safeName.substring(dotIndex + 1) else ""
        var index = 0
        while (true) {
            val suffix = if (index == 0) "" else "_${index + 1}"
            val filename = if (extension.isBlank()) {
                "$baseName$suffix"
            } else {
                "$baseName$suffix.$extension"
            }
            val candidate = File(directory, filename)
            if (!candidate.exists()) return candidate
            index++
        }
    }

    private fun queryDisplayName(context: Context, uri: Uri): String? {
        val projection = arrayOf(OpenableColumns.DISPLAY_NAME)
        val queriedName = context.contentResolver.query(uri, projection, null, null, null)?.use { cursor: Cursor ->
            if (cursor.moveToFirst()) cursor.getString(0) else null
        }
        return queriedName ?: uri.lastPathSegment?.substringAfterLast('/')
    }
}

/** Compatibility facade for existing Transmittal export callers. */
object TransmittalDocumentStore {
    fun archiveWorkbook(context: Context, workbook: SavedTransmittalWorkbook) =
        AppFileStore.archiveWorkbook(context, workbook)

    fun savePrintReadyPdf(context: Context, report: TransmittalReport) =
        AppFileStore.savePrintReadyPdf(context, report)

    fun saveGatepassPdf(context: Context, report: GatepassReport) =
        AppFileStore.saveGatepassPdf(context, report)

    fun list(context: Context): List<SavedAppFile> = AppFileStore.list(context)

    fun uriFor(context: Context, file: SavedAppFile): Uri = AppFileStore.uriFor(context, file)

    fun open(context: Context, file: SavedAppFile): Boolean = AppFileStore.open(context, file)

    fun share(context: Context, file: SavedAppFile): Boolean = AppFileStore.share(context, file)
}

private fun mimeTypeForFileName(name: String): String {
    val extension = name.substringAfterLast('.', "").lowercase(Locale.ROOT)
    val commonMimeType = mapOf(
        "pdf" to "application/pdf",
        "xlsx" to "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "xls" to "application/vnd.ms-excel",
        "csv" to "text/csv",
        "docx" to "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "doc" to "application/msword",
        "pptx" to "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        "ppt" to "application/vnd.ms-powerpoint",
        "json" to "application/json",
        "xml" to "application/xml",
        "zip" to "application/zip"
    )[extension]
    return commonMimeType
        ?: MimeTypeMap.getSingleton().getMimeTypeFromExtension(extension)
        ?: "application/octet-stream"
}

private fun withMimeExtension(name: String, mimeType: String?): String {
    val dotIndex = name.lastIndexOf('.')
    val hasExtension = dotIndex > 0 && dotIndex < name.lastIndex
    return if (hasExtension) name else name + mimeExtension(mimeType)
}

private fun mimeExtension(mimeType: String?): String =
    mimeType?.let { MimeTypeMap.getSingleton().getExtensionFromMimeType(it) }
        ?.let { ".${it.lowercase(Locale.ROOT)}" }
        .orEmpty()

private fun fileFormatLabel(extension: String, mimeType: String): String = when (extension) {
    "pdf" -> "PDF"
    "xlsx" -> "Excel workbook"
    "xls" -> "Excel spreadsheet"
    "csv" -> "CSV file"
    "docx" -> "Word document"
    "doc" -> "Word document"
    "pptx" -> "PowerPoint presentation"
    "ppt" -> "PowerPoint presentation"
    "txt" -> "Text file"
    "json" -> "JSON file"
    "xml" -> "XML file"
    "zip" -> "ZIP archive"
    "jpg", "jpeg", "png", "gif", "webp", "heic" -> "Image"
    else -> when {
        mimeType.startsWith("image/") -> "Image"
        mimeType.startsWith("video/") -> "Video"
        mimeType.startsWith("audio/") -> "Audio"
        mimeType.startsWith("text/") -> "Text file"
        extension.isNotBlank() -> "${extension.uppercase(Locale.ROOT)} file"
        else -> "File"
    }
}

private fun fileStamp(): String =
    java.text.SimpleDateFormat("yyyyMMdd_HHmmss", Locale.US)
        .format(Date())

fun formatSavedFileTime(timestamp: Long): String =
    DateFormat.getDateTimeInstance(DateFormat.MEDIUM, DateFormat.SHORT).format(Date(timestamp))

fun formatSavedFileSize(bytes: Long): String = when {
    bytes < 1024 -> "$bytes B"
    bytes < 1024 * 1024 -> "${bytes / 1024} KB"
    else -> "%.1f MB".format(bytes / (1024f * 1024f))
}
