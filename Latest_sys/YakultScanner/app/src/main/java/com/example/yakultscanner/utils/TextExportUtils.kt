package com.example.yakultscanner.utils

import android.content.ContentValues
import android.content.Context
import android.os.Build
import android.os.Environment
import android.provider.MediaStore
import java.io.File

object TextExportUtils {
    fun saveTextToDownloads(
        context: Context,
        fileName: String,
        mimeType: String,
        content: String
    ): Result<String> {
        return try {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
                val resolver = context.contentResolver
                val values = ContentValues().apply {
                    put(MediaStore.MediaColumns.DISPLAY_NAME, fileName)
                    put(MediaStore.MediaColumns.MIME_TYPE, mimeType)
                    put(MediaStore.MediaColumns.RELATIVE_PATH, Environment.DIRECTORY_DOWNLOADS)
                    put(MediaStore.MediaColumns.IS_PENDING, 1)
                }

                val outputUri = resolver.insert(MediaStore.Downloads.EXTERNAL_CONTENT_URI, values)
                    ?: throw IllegalStateException("Failed to create export file in Downloads.")

                resolver.openOutputStream(outputUri)?.bufferedWriter(Charsets.UTF_8).use { writer ->
                    if (writer == null) {
                        throw IllegalStateException("Failed to open export output stream.")
                    }
                    writer.write(content)
                }

                values.clear()
                values.put(MediaStore.MediaColumns.IS_PENDING, 0)
                resolver.update(outputUri, values, null, null)
                Result.success(fileName)
            } else {
                val downloadsDir = context.getExternalFilesDir(Environment.DIRECTORY_DOWNLOADS) ?: context.filesDir
                if (!downloadsDir.exists()) {
                    downloadsDir.mkdirs()
                }

                val file = File(downloadsDir, fileName)
                file.writeText(content, Charsets.UTF_8)
                Result.success(file.absolutePath)
            }
        } catch (ex: Exception) {
            Result.failure(ex)
        }
    }
}
