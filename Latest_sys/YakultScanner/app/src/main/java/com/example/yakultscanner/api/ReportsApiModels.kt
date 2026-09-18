package com.example.yakultscanner.api

import com.google.gson.annotations.SerializedName

data class ReportsSummaryResponse(
    @SerializedName("range") val range: String? = null,
    @SerializedName("totalItems") val totalItems: Int? = null,
    @SerializedName("totalBorrowed") val totalBorrowed: Int? = null,
    @SerializedName("totalSets") val totalSets: Int? = null,
    @SerializedName("kpis") val kpis: List<ReportKpiDto> = emptyList(),
    @SerializedName("executiveSummary") val executiveSummary: String? = null,
    @SerializedName("highlights") val highlights: List<String> = emptyList(),
    @SerializedName("healthSignals") val healthSignals: List<ReportListRowDto> = emptyList(),
    @SerializedName("dispatchTrend") val dispatchTrend: List<ReportTrendPointDto> = emptyList(),
    @SerializedName("issueRows") val issueRows: List<ReportListRowDto> = emptyList(),
    @SerializedName("borrowRows") val borrowRows: List<ReportListRowDto> = emptyList(),
    @SerializedName("activityRows") val activityRows: List<ReportListRowDto> = emptyList(),
    @SerializedName("actionRows") val actionRows: List<ReportListRowDto> = emptyList(),
    @SerializedName("timelineRows") val timelineRows: List<ReportListRowDto> = emptyList()
)

data class ReportModuleDetailResponse(
    @SerializedName("moduleId") val moduleId: String? = null,
    @SerializedName("title") val title: String? = null,
    @SerializedName("summary") val summary: String? = null,
    @SerializedName("heroLabel") val heroLabel: String? = null,
    @SerializedName("heroValue") val heroValue: String? = null,
    @SerializedName("heroTone") val heroTone: String? = null,
    @SerializedName("keyRows") val keyRows: List<ReportListRowDto> = emptyList(),
    @SerializedName("leaderboardRows") val leaderboardRows: List<ReportListRowDto> = emptyList(),
    @SerializedName("timelineRows") val timelineRows: List<ReportListRowDto> = emptyList()
)

data class ReportKpiDto(
    @SerializedName("title") val title: String? = null,
    @SerializedName("value") val value: String? = null,
    @SerializedName("delta") val delta: String? = null,
    @SerializedName("tone") val tone: String? = null
)

data class ReportTrendPointDto(
    @SerializedName("label") val label: String? = null,
    @SerializedName("value") val value: Int = 0
)

data class ReportListRowDto(
    @SerializedName("title") val title: String? = null,
    @SerializedName("subtitle") val subtitle: String? = null,
    @SerializedName("value") val value: String? = null,
    @SerializedName("tone") val tone: String? = null
)
