package com.example.yakultscanner.ui.components

import androidx.compose.animation.core.*
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.layout.*
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.CornerRadius
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.BlendMode
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp

@Composable
fun ScannerOverlay(
    modifier: Modifier = Modifier,
    isScanning: Boolean = false,
    borderColor: Color = MaterialTheme.colorScheme.primary,
    scanLineColor: Color = Color.Red,
    maxFrameSize: Dp = Dp.Unspecified
) {
    val infiniteTransition = rememberInfiniteTransition(label = "scanner_pulse")
    
    // Pulse animation for the corners
    val pulseAlpha by infiniteTransition.animateFloat(
        initialValue = 0.6f,
        targetValue = 1f,
        animationSpec = infiniteRepeatable(
            animation = tween(1000),
            repeatMode = RepeatMode.Reverse
        ),
        label = "pulse_alpha"
    )

    // Scanning line animation
    val scanLineProgress by infiniteTransition.animateFloat(
        initialValue = 0f,
        targetValue = 1f,
        animationSpec = infiniteRepeatable(
            animation = tween(2000, easing = LinearEasing),
            repeatMode = RepeatMode.Restart
        ),
        label = "scan_line"
    )

    Box(modifier = modifier.fillMaxSize()) {
        Canvas(modifier = Modifier.fillMaxSize()) {
            val canvasWidth = size.width
            val canvasHeight = size.height
            
            // Calculate frame size (square, 70% of min dimension), with a tablet cap when requested.
            val naturalFrameSize = minOf(canvasWidth, canvasHeight) * 0.7f
            val frameSizeLimit = if (maxFrameSize != Dp.Unspecified) maxFrameSize.toPx() else Float.POSITIVE_INFINITY
            val frameSize = minOf(naturalFrameSize, frameSizeLimit)
            val left = (canvasWidth - frameSize) / 2
            val top = (canvasHeight - frameSize) / 2
            
            // 1. Draw the scrim (semi-transparent background) with a hole
            // We use specific layer with SRC_OUT blending to "cut out" the hole
            // Note: Simplest way in Compose is drawing a big rect then a cleared rounded rect
            
            // Draw Scrim
            drawRect(
                color = Color.Black.copy(alpha = 0.6f),
                size = size
            )
            
            // Clear the center (Hole)
            drawRoundRect(
                color = Color.Transparent,
                topLeft = Offset(left, top),
                size = Size(frameSize, frameSize),
                cornerRadius = CornerRadius(24.dp.toPx()),
                blendMode = BlendMode.Clear
            )

            // 2. Draw Corner Indicators
            val cornerLength = 30.dp.toPx()
            val strokeWidth = 4.dp.toPx()
            val cornerRadius = 24.dp.toPx() // Match hole radius

            val pathColor = if (isScanning) Color.Green else borderColor.copy(alpha = pulseAlpha)

            // Top Left
            drawArc(
                color = pathColor,
                startAngle = 180f,
                sweepAngle = 90f,
                useCenter = false,
                topLeft = Offset(left, top),
                size = Size(cornerRadius * 2, cornerRadius * 2),
                style = Stroke(width = strokeWidth)
            )
            drawLine(
                color = pathColor,
                start = Offset(left, top + cornerRadius),
                end = Offset(left, top + cornerRadius + cornerLength),
                strokeWidth = strokeWidth
            )
            drawLine(
                color = pathColor,
                start = Offset(left + cornerRadius, top),
                end = Offset(left + cornerRadius + cornerLength, top),
                strokeWidth = strokeWidth
            )

            // Top Right
            drawArc(
                color = pathColor,
                startAngle = 270f,
                sweepAngle = 90f,
                useCenter = false,
                topLeft = Offset(left + frameSize - cornerRadius * 2, top),
                size = Size(cornerRadius * 2, cornerRadius * 2),
                style = Stroke(width = strokeWidth)
            )
            drawLine(
                color = pathColor,
                start = Offset(left + frameSize, top + cornerRadius),
                end = Offset(left + frameSize, top + cornerRadius + cornerLength),
                strokeWidth = strokeWidth
            )
            drawLine(
                color = pathColor,
                start = Offset(left + frameSize - cornerRadius, top),
                end = Offset(left + frameSize - cornerRadius - cornerLength, top),
                strokeWidth = strokeWidth
            )

            // Bottom Left
            drawArc(
                color = pathColor,
                startAngle = 90f,
                sweepAngle = 90f,
                useCenter = false,
                topLeft = Offset(left, top + frameSize - cornerRadius * 2),
                size = Size(cornerRadius * 2, cornerRadius * 2),
                style = Stroke(width = strokeWidth)
            )
            drawLine(
                color = pathColor,
                start = Offset(left, top + frameSize - cornerRadius),
                end = Offset(left, top + frameSize - cornerRadius - cornerLength),
                strokeWidth = strokeWidth
            )
            drawLine(
                color = pathColor,
                start = Offset(left + cornerRadius, top + frameSize),
                end = Offset(left + cornerRadius + cornerLength, top + frameSize),
                strokeWidth = strokeWidth
            )

            // Bottom Right
            drawArc(
                color = pathColor,
                startAngle = 0f,
                sweepAngle = 90f,
                useCenter = false,
                topLeft = Offset(left + frameSize - cornerRadius * 2, top + frameSize - cornerRadius * 2),
                size = Size(cornerRadius * 2, cornerRadius * 2),
                style = Stroke(width = strokeWidth)
            )
            drawLine(
                color = pathColor,
                start = Offset(left + frameSize, top + frameSize - cornerRadius),
                end = Offset(left + frameSize, top + frameSize - cornerRadius - cornerLength),
                strokeWidth = strokeWidth
            )
            drawLine(
                color = pathColor,
                start = Offset(left + frameSize - cornerRadius, top + frameSize),
                end = Offset(left + frameSize - cornerRadius - cornerLength, top + frameSize),
                strokeWidth = strokeWidth
            )

            // 3. Draw Scan Line (Laser effect)
            if (!isScanning) { // Only animate when searching, or maybe alway? Let's keep it subtle
                val lineY = top + (frameSize * scanLineProgress)
                drawLine(
                    color = scanLineColor.copy(alpha = 0.5f),
                    start = Offset(left + 10f, lineY),
                    end = Offset(left + frameSize - 10f, lineY),
                    strokeWidth = 2.dp.toPx()
                )
            }
        }
        
        // Instructional Text
        Text(
            text = "Align code within frame",
            style = MaterialTheme.typography.bodyMedium,
            color = Color.White.copy(alpha = 0.8f),
            modifier = Modifier
                .align(Alignment.Center)
                .padding(top = 280.dp) // Offset below the frame roughly
        )
    }
}
