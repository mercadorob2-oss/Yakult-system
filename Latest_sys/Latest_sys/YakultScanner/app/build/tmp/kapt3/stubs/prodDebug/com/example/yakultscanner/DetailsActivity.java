package com.example.yakultscanner;

import android.os.Bundle;
import android.widget.TextView;
import androidx.appcompat.app.AppCompatActivity;
import androidx.recyclerview.widget.LinearLayoutManager;
import androidx.recyclerview.widget.RecyclerView;

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000&\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0010\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\u000e\n\u0002\b\u0004\u0018\u0000 \u000e2\u00020\u0001:\u0001\u000eB\u0007\u00a2\u0006\u0004\b\u0002\u0010\u0003J\u0012\u0010\u0004\u001a\u00020\u00052\b\u0010\u0006\u001a\u0004\u0018\u00010\u0007H\u0014J\u0010\u0010\b\u001a\u00020\t2\u0006\u0010\n\u001a\u00020\u000bH\u0002J\u0010\u0010\f\u001a\u00020\u00052\u0006\u0010\r\u001a\u00020\tH\u0002\u00a8\u0006\u000f"}, d2 = {"Lcom/example/yakultscanner/DetailsActivity;", "Landroidx/appcompat/app/AppCompatActivity;", "<init>", "()V", "onCreate", "", "savedInstanceState", "Landroid/os/Bundle;", "parseQrData", "Lcom/example/yakultscanner/ParsedSet;", "data", "", "displayParsedData", "parsedSet", "Companion", "app_prodDebug"})
public final class DetailsActivity extends androidx.appcompat.app.AppCompatActivity {
    @org.jetbrains.annotations.NotNull()
    public static final java.lang.String EXTRA_QR_CODE_DATA = "extra_qr_code_data";
    @org.jetbrains.annotations.NotNull()
    public static final com.example.yakultscanner.DetailsActivity.Companion Companion = null;
    
    public DetailsActivity() {
        super();
    }
    
    @java.lang.Override()
    protected void onCreate(@org.jetbrains.annotations.Nullable()
    android.os.Bundle savedInstanceState) {
    }
    
    private final com.example.yakultscanner.ParsedSet parseQrData(java.lang.String data) {
        return null;
    }
    
    private final void displayParsedData(com.example.yakultscanner.ParsedSet parsedSet) {
    }
    
    @kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000\u0012\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0002\b\u0003\n\u0002\u0010\u000e\n\u0000\b\u0086\u0003\u0018\u00002\u00020\u0001B\t\b\u0002\u00a2\u0006\u0004\b\u0002\u0010\u0003R\u000e\u0010\u0004\u001a\u00020\u0005X\u0086T\u00a2\u0006\u0002\n\u0000\u00a8\u0006\u0006"}, d2 = {"Lcom/example/yakultscanner/DetailsActivity$Companion;", "", "<init>", "()V", "EXTRA_QR_CODE_DATA", "", "app_prodDebug"})
    public static final class Companion {
        
        private Companion() {
            super();
        }
    }
}