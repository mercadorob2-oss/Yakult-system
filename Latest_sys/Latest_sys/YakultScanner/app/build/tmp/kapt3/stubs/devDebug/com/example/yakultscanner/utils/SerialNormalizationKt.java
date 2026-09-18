package com.example.yakultscanner.utils;

@kotlin.Metadata(mv = {2, 2, 0}, k = 2, xi = 48, d1 = {"\u0000\u0010\n\u0000\n\u0002\u0010\u000e\n\u0002\b\u0002\n\u0002\u0010 \n\u0000\u001a\u0010\u0010\u0000\u001a\u00020\u00012\b\u0010\u0002\u001a\u0004\u0018\u00010\u0001\u001a\u0016\u0010\u0003\u001a\b\u0012\u0004\u0012\u00020\u00010\u00042\b\u0010\u0002\u001a\u0004\u0018\u00010\u0001\u00a8\u0006\u0005"}, d2 = {"normalizeSerial", "", "raw", "parseNormalizedSerials", "", "app_devDebug"})
public final class SerialNormalizationKt {
    
    /**
     * Normalizes serial numbers so they can be de-duped reliably across manual entry,
     * camera scans, and hardware scanner input.
     *
     * Rules:
     * - Trim leading/trailing whitespace
     * - Remove all internal whitespace
     * - Uppercase (locale-safe)
     */
    @org.jetbrains.annotations.NotNull()
    public static final java.lang.String normalizeSerial(@org.jetbrains.annotations.Nullable()
    java.lang.String raw) {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public static final java.util.List<java.lang.String> parseNormalizedSerials(@org.jetbrains.annotations.Nullable()
    java.lang.String raw) {
        return null;
    }
}