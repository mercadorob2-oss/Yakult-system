package com.example.yakultscanner.ui.components

import android.content.Context
import android.graphics.Bitmap
import android.graphics.Canvas
import android.graphics.Paint
import android.view.MotionEvent
import android.view.View
import android.graphics.Color as GraphicsColor
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.viewinterop.AndroidView

class SignatureView @JvmOverloads constructor(
    context: Context,
    attrs: android.util.AttributeSet? = null
) : View(context, attrs) {

    private val drawPaint = Paint().apply {
        color = GraphicsColor.BLACK
        style = Paint.Style.STROKE
        strokeWidth = 4f
        isAntiAlias = true
    }

    private val drawPath = android.graphics.Path()
    private var bitmap: Bitmap? = null
    private var bitmapCanvas: Canvas? = null

    var hasSignature: Boolean = false
        private set

    override fun onSizeChanged(w: Int, h: Int, oldw: Int, oldh: Int) {
        super.onSizeChanged(w, h, oldw, oldh)
        if (w > 0 && h > 0) {
            bitmap = Bitmap.createBitmap(w, h, Bitmap.Config.ARGB_8888)
            bitmapCanvas = Canvas(bitmap!!)
        }
    }

    override fun onDraw(canvas: Canvas) {
        super.onDraw(canvas)
        bitmap?.let { bmp ->
            canvas.drawBitmap(bmp, 0f, 0f, null)
        }
        canvas.drawPath(drawPath, drawPaint)
    }

    override fun onTouchEvent(event: MotionEvent): Boolean {
        val x = event.x
        val y = event.y

        when (event.action) {
            MotionEvent.ACTION_DOWN -> {
                drawPath.moveTo(x, y)
                hasSignature = true
            }
            MotionEvent.ACTION_MOVE -> {
                drawPath.lineTo(x, y)
                bitmapCanvas?.drawPath(drawPath, drawPaint)
            }
            MotionEvent.ACTION_UP -> {
                bitmapCanvas?.drawPath(drawPath, drawPaint)
                drawPath.reset()
                performClick()
            }
        }
        invalidate()
        return true
    }

    override fun performClick(): Boolean {
        return super.performClick()
    }

    fun clear() {
        bitmap?.eraseColor(GraphicsColor.TRANSPARENT)
        drawPath.reset()
        hasSignature = false
        invalidate()
    }

    fun getSignatureBitmap(): Bitmap? = bitmap
}

@Composable
fun SignatureDialog(
    label: String,
    onDismiss: () -> Unit,
    registerSignatureView: (SignatureView) -> Unit
) {
    AlertDialog(
        onDismissRequest = onDismiss,
        confirmButton = {
            TextButton(onClick = onDismiss) {
                Text("Done")
            }
        },
        title = {
            Text(
                text = label,
                fontWeight = FontWeight.Bold
            )
        },
        text = {
            SignaturePad(
                label = label,
                onViewReady = registerSignatureView
            )
        }
    )
}

@Composable
fun SignaturePad(
    label: String,
    onViewReady: (SignatureView) -> Unit
) {
    var signatureView by remember { mutableStateOf<SignatureView?>(null) }

    Column(
        verticalArrangement = Arrangement.spacedBy(8.dp),
        modifier = Modifier.fillMaxWidth()
    ) {
        Text(
            text = label,
            color = MaterialTheme.colorScheme.onSurface,
            fontSize = 14.sp,
            fontWeight = FontWeight.SemiBold
        )

        AndroidView(
            modifier = Modifier
                .fillMaxWidth()
                .height(140.dp)
                .clip(RoundedCornerShape(12.dp))
                .border(1.dp, MaterialTheme.colorScheme.outline, RoundedCornerShape(12.dp))
                .background(MaterialTheme.colorScheme.background),
            factory = { context ->
                SignatureView(context).also { view ->
                    signatureView = view
                    onViewReady(view)
                }
            }
        )

        Row(
            horizontalArrangement = Arrangement.End,
            modifier = Modifier.fillMaxWidth()
        ) {
            TextButton(onClick = { signatureView?.clear() }) {
                Text("Clear")
            }
        }
    }
}
