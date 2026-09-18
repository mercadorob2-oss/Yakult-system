package com.example.yakultscanner.api;

import com.google.gson.annotations.SerializedName;

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000:\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0000\n\u0002\u0010\u000e\n\u0000\n\u0002\u0010\b\n\u0002\b\u0003\n\u0002\u0010 \n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b*\n\u0002\u0010\u000b\n\u0002\b\u0004\b\u0086\b\u0018\u00002\u00020\u0001B\u00d3\u0001\u0012\n\b\u0002\u0010\u0002\u001a\u0004\u0018\u00010\u0003\u0012\n\b\u0002\u0010\u0004\u001a\u0004\u0018\u00010\u0005\u0012\n\b\u0002\u0010\u0006\u001a\u0004\u0018\u00010\u0005\u0012\n\b\u0002\u0010\u0007\u001a\u0004\u0018\u00010\u0005\u0012\u000e\b\u0002\u0010\b\u001a\b\u0012\u0004\u0012\u00020\n0\t\u0012\n\b\u0002\u0010\u000b\u001a\u0004\u0018\u00010\u0003\u0012\u000e\b\u0002\u0010\f\u001a\b\u0012\u0004\u0012\u00020\u00030\t\u0012\u000e\b\u0002\u0010\r\u001a\b\u0012\u0004\u0012\u00020\u000e0\t\u0012\u000e\b\u0002\u0010\u000f\u001a\b\u0012\u0004\u0012\u00020\u00100\t\u0012\u000e\b\u0002\u0010\u0011\u001a\b\u0012\u0004\u0012\u00020\u000e0\t\u0012\u000e\b\u0002\u0010\u0012\u001a\b\u0012\u0004\u0012\u00020\u000e0\t\u0012\u000e\b\u0002\u0010\u0013\u001a\b\u0012\u0004\u0012\u00020\u000e0\t\u0012\u000e\b\u0002\u0010\u0014\u001a\b\u0012\u0004\u0012\u00020\u000e0\t\u0012\u000e\b\u0002\u0010\u0015\u001a\b\u0012\u0004\u0012\u00020\u000e0\t\u00a2\u0006\u0004\b\u0016\u0010\u0017J\u000b\u0010*\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\u0010\u0010+\u001a\u0004\u0018\u00010\u0005H\u00c6\u0003\u00a2\u0006\u0002\u0010\u001bJ\u0010\u0010,\u001a\u0004\u0018\u00010\u0005H\u00c6\u0003\u00a2\u0006\u0002\u0010\u001bJ\u0010\u0010-\u001a\u0004\u0018\u00010\u0005H\u00c6\u0003\u00a2\u0006\u0002\u0010\u001bJ\u000f\u0010.\u001a\b\u0012\u0004\u0012\u00020\n0\tH\u00c6\u0003J\u000b\u0010/\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\u000f\u00100\u001a\b\u0012\u0004\u0012\u00020\u00030\tH\u00c6\u0003J\u000f\u00101\u001a\b\u0012\u0004\u0012\u00020\u000e0\tH\u00c6\u0003J\u000f\u00102\u001a\b\u0012\u0004\u0012\u00020\u00100\tH\u00c6\u0003J\u000f\u00103\u001a\b\u0012\u0004\u0012\u00020\u000e0\tH\u00c6\u0003J\u000f\u00104\u001a\b\u0012\u0004\u0012\u00020\u000e0\tH\u00c6\u0003J\u000f\u00105\u001a\b\u0012\u0004\u0012\u00020\u000e0\tH\u00c6\u0003J\u000f\u00106\u001a\b\u0012\u0004\u0012\u00020\u000e0\tH\u00c6\u0003J\u000f\u00107\u001a\b\u0012\u0004\u0012\u00020\u000e0\tH\u00c6\u0003J\u00da\u0001\u00108\u001a\u00020\u00002\n\b\u0002\u0010\u0002\u001a\u0004\u0018\u00010\u00032\n\b\u0002\u0010\u0004\u001a\u0004\u0018\u00010\u00052\n\b\u0002\u0010\u0006\u001a\u0004\u0018\u00010\u00052\n\b\u0002\u0010\u0007\u001a\u0004\u0018\u00010\u00052\u000e\b\u0002\u0010\b\u001a\b\u0012\u0004\u0012\u00020\n0\t2\n\b\u0002\u0010\u000b\u001a\u0004\u0018\u00010\u00032\u000e\b\u0002\u0010\f\u001a\b\u0012\u0004\u0012\u00020\u00030\t2\u000e\b\u0002\u0010\r\u001a\b\u0012\u0004\u0012\u00020\u000e0\t2\u000e\b\u0002\u0010\u000f\u001a\b\u0012\u0004\u0012\u00020\u00100\t2\u000e\b\u0002\u0010\u0011\u001a\b\u0012\u0004\u0012\u00020\u000e0\t2\u000e\b\u0002\u0010\u0012\u001a\b\u0012\u0004\u0012\u00020\u000e0\t2\u000e\b\u0002\u0010\u0013\u001a\b\u0012\u0004\u0012\u00020\u000e0\t2\u000e\b\u0002\u0010\u0014\u001a\b\u0012\u0004\u0012\u00020\u000e0\t2\u000e\b\u0002\u0010\u0015\u001a\b\u0012\u0004\u0012\u00020\u000e0\tH\u00c6\u0001\u00a2\u0006\u0002\u00109J\u0013\u0010:\u001a\u00020;2\b\u0010<\u001a\u0004\u0018\u00010\u0001H\u00d6\u0003J\t\u0010=\u001a\u00020\u0005H\u00d6\u0001J\t\u0010>\u001a\u00020\u0003H\u00d6\u0001R\u0018\u0010\u0002\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0018\u0010\u0019R\u001a\u0010\u0004\u001a\u0004\u0018\u00010\u00058\u0006X\u0087\u0004\u00a2\u0006\n\n\u0002\u0010\u001c\u001a\u0004\b\u001a\u0010\u001bR\u001a\u0010\u0006\u001a\u0004\u0018\u00010\u00058\u0006X\u0087\u0004\u00a2\u0006\n\n\u0002\u0010\u001c\u001a\u0004\b\u001d\u0010\u001bR\u001a\u0010\u0007\u001a\u0004\u0018\u00010\u00058\u0006X\u0087\u0004\u00a2\u0006\n\n\u0002\u0010\u001c\u001a\u0004\b\u001e\u0010\u001bR\u001c\u0010\b\u001a\b\u0012\u0004\u0012\u00020\n0\t8\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u001f\u0010 R\u0018\u0010\u000b\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b!\u0010\u0019R\u001c\u0010\f\u001a\b\u0012\u0004\u0012\u00020\u00030\t8\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\"\u0010 R\u001c\u0010\r\u001a\b\u0012\u0004\u0012\u00020\u000e0\t8\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b#\u0010 R\u001c\u0010\u000f\u001a\b\u0012\u0004\u0012\u00020\u00100\t8\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b$\u0010 R\u001c\u0010\u0011\u001a\b\u0012\u0004\u0012\u00020\u000e0\t8\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b%\u0010 R\u001c\u0010\u0012\u001a\b\u0012\u0004\u0012\u00020\u000e0\t8\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b&\u0010 R\u001c\u0010\u0013\u001a\b\u0012\u0004\u0012\u00020\u000e0\t8\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\'\u0010 R\u001c\u0010\u0014\u001a\b\u0012\u0004\u0012\u00020\u000e0\t8\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b(\u0010 R\u001c\u0010\u0015\u001a\b\u0012\u0004\u0012\u00020\u000e0\t8\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b)\u0010 \u00a8\u0006?"}, d2 = {"Lcom/example/yakultscanner/api/ReportsSummaryResponse;", "", "range", "", "totalItems", "", "totalBorrowed", "totalSets", "kpis", "", "Lcom/example/yakultscanner/api/ReportKpiDto;", "executiveSummary", "highlights", "healthSignals", "Lcom/example/yakultscanner/api/ReportListRowDto;", "dispatchTrend", "Lcom/example/yakultscanner/api/ReportTrendPointDto;", "issueRows", "borrowRows", "activityRows", "actionRows", "timelineRows", "<init>", "(Ljava/lang/String;Ljava/lang/Integer;Ljava/lang/Integer;Ljava/lang/Integer;Ljava/util/List;Ljava/lang/String;Ljava/util/List;Ljava/util/List;Ljava/util/List;Ljava/util/List;Ljava/util/List;Ljava/util/List;Ljava/util/List;Ljava/util/List;)V", "getRange", "()Ljava/lang/String;", "getTotalItems", "()Ljava/lang/Integer;", "Ljava/lang/Integer;", "getTotalBorrowed", "getTotalSets", "getKpis", "()Ljava/util/List;", "getExecutiveSummary", "getHighlights", "getHealthSignals", "getDispatchTrend", "getIssueRows", "getBorrowRows", "getActivityRows", "getActionRows", "getTimelineRows", "component1", "component2", "component3", "component4", "component5", "component6", "component7", "component8", "component9", "component10", "component11", "component12", "component13", "component14", "copy", "(Ljava/lang/String;Ljava/lang/Integer;Ljava/lang/Integer;Ljava/lang/Integer;Ljava/util/List;Ljava/lang/String;Ljava/util/List;Ljava/util/List;Ljava/util/List;Ljava/util/List;Ljava/util/List;Ljava/util/List;Ljava/util/List;Ljava/util/List;)Lcom/example/yakultscanner/api/ReportsSummaryResponse;", "equals", "", "other", "hashCode", "toString", "app_devDebug"})
public final class ReportsSummaryResponse {
    @com.google.gson.annotations.SerializedName(value = "range")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String range = null;
    @com.google.gson.annotations.SerializedName(value = "totalItems")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.Integer totalItems = null;
    @com.google.gson.annotations.SerializedName(value = "totalBorrowed")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.Integer totalBorrowed = null;
    @com.google.gson.annotations.SerializedName(value = "totalSets")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.Integer totalSets = null;
    @com.google.gson.annotations.SerializedName(value = "kpis")
    @org.jetbrains.annotations.NotNull()
    private final java.util.List<com.example.yakultscanner.api.ReportKpiDto> kpis = null;
    @com.google.gson.annotations.SerializedName(value = "executiveSummary")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String executiveSummary = null;
    @com.google.gson.annotations.SerializedName(value = "highlights")
    @org.jetbrains.annotations.NotNull()
    private final java.util.List<java.lang.String> highlights = null;
    @com.google.gson.annotations.SerializedName(value = "healthSignals")
    @org.jetbrains.annotations.NotNull()
    private final java.util.List<com.example.yakultscanner.api.ReportListRowDto> healthSignals = null;
    @com.google.gson.annotations.SerializedName(value = "dispatchTrend")
    @org.jetbrains.annotations.NotNull()
    private final java.util.List<com.example.yakultscanner.api.ReportTrendPointDto> dispatchTrend = null;
    @com.google.gson.annotations.SerializedName(value = "issueRows")
    @org.jetbrains.annotations.NotNull()
    private final java.util.List<com.example.yakultscanner.api.ReportListRowDto> issueRows = null;
    @com.google.gson.annotations.SerializedName(value = "borrowRows")
    @org.jetbrains.annotations.NotNull()
    private final java.util.List<com.example.yakultscanner.api.ReportListRowDto> borrowRows = null;
    @com.google.gson.annotations.SerializedName(value = "activityRows")
    @org.jetbrains.annotations.NotNull()
    private final java.util.List<com.example.yakultscanner.api.ReportListRowDto> activityRows = null;
    @com.google.gson.annotations.SerializedName(value = "actionRows")
    @org.jetbrains.annotations.NotNull()
    private final java.util.List<com.example.yakultscanner.api.ReportListRowDto> actionRows = null;
    @com.google.gson.annotations.SerializedName(value = "timelineRows")
    @org.jetbrains.annotations.NotNull()
    private final java.util.List<com.example.yakultscanner.api.ReportListRowDto> timelineRows = null;
    
    public ReportsSummaryResponse(@org.jetbrains.annotations.Nullable()
    java.lang.String range, @org.jetbrains.annotations.Nullable()
    java.lang.Integer totalItems, @org.jetbrains.annotations.Nullable()
    java.lang.Integer totalBorrowed, @org.jetbrains.annotations.Nullable()
    java.lang.Integer totalSets, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.api.ReportKpiDto> kpis, @org.jetbrains.annotations.Nullable()
    java.lang.String executiveSummary, @org.jetbrains.annotations.NotNull()
    java.util.List<java.lang.String> highlights, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.api.ReportListRowDto> healthSignals, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.api.ReportTrendPointDto> dispatchTrend, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.api.ReportListRowDto> issueRows, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.api.ReportListRowDto> borrowRows, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.api.ReportListRowDto> activityRows, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.api.ReportListRowDto> actionRows, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.api.ReportListRowDto> timelineRows) {
        super();
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getRange() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer getTotalItems() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer getTotalBorrowed() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer getTotalSets() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.api.ReportKpiDto> getKpis() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getExecutiveSummary() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<java.lang.String> getHighlights() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.api.ReportListRowDto> getHealthSignals() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.api.ReportTrendPointDto> getDispatchTrend() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.api.ReportListRowDto> getIssueRows() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.api.ReportListRowDto> getBorrowRows() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.api.ReportListRowDto> getActivityRows() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.api.ReportListRowDto> getActionRows() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.api.ReportListRowDto> getTimelineRows() {
        return null;
    }
    
    public ReportsSummaryResponse() {
        super();
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component1() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.api.ReportListRowDto> component10() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.api.ReportListRowDto> component11() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.api.ReportListRowDto> component12() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.api.ReportListRowDto> component13() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.api.ReportListRowDto> component14() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer component2() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer component3() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer component4() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.api.ReportKpiDto> component5() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component6() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<java.lang.String> component7() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.api.ReportListRowDto> component8() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.api.ReportTrendPointDto> component9() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final com.example.yakultscanner.api.ReportsSummaryResponse copy(@org.jetbrains.annotations.Nullable()
    java.lang.String range, @org.jetbrains.annotations.Nullable()
    java.lang.Integer totalItems, @org.jetbrains.annotations.Nullable()
    java.lang.Integer totalBorrowed, @org.jetbrains.annotations.Nullable()
    java.lang.Integer totalSets, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.api.ReportKpiDto> kpis, @org.jetbrains.annotations.Nullable()
    java.lang.String executiveSummary, @org.jetbrains.annotations.NotNull()
    java.util.List<java.lang.String> highlights, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.api.ReportListRowDto> healthSignals, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.api.ReportTrendPointDto> dispatchTrend, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.api.ReportListRowDto> issueRows, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.api.ReportListRowDto> borrowRows, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.api.ReportListRowDto> activityRows, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.api.ReportListRowDto> actionRows, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.api.ReportListRowDto> timelineRows) {
        return null;
    }
    
    @java.lang.Override()
    public boolean equals(@org.jetbrains.annotations.Nullable()
    java.lang.Object other) {
        return false;
    }
    
    @java.lang.Override()
    public int hashCode() {
        return 0;
    }
    
    @java.lang.Override()
    @org.jetbrains.annotations.NotNull()
    public java.lang.String toString() {
        return null;
    }
}