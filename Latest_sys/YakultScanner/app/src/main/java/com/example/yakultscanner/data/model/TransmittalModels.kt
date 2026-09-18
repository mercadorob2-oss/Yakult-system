package com.example.yakultscanner.data.model

/** The transmittal row shape used by the scanner workspace and report. */
enum class TransmittalItemMode(val label: String, val helperText: String) {
    Standard("Description + Serial", "Standard item: use a description and one serial number."),
    Phone("Phone", "Phone item: capture mobile number, serial number, and IMEI values.")
}

/** The field that receives the next scanner result in the transmittal workspace. */
enum class TransmittalScanTarget(val label: String) {
    Auto("Auto"),
    Serial("Serial"),
    Imei1("IMEI 1"),
    Imei2("IMEI 2"),
    Mobile("Mobile number")
}

data class TransmittalItemDraft(
    val description: String = "",
    val mobileNumber: String = "",
    val serialNumber: String = "",
    val imei1: String = "",
    val imei2: String = ""
) {
    fun hasAnyValue(): Boolean = listOf(description, mobileNumber, serialNumber, imei1, imei2)
        .any { it.isNotBlank() }

    fun hasDetailedValues(): Boolean = listOf(mobileNumber, serialNumber, imei1, imei2)
        .any { it.isNotBlank() }

    fun isBlankFor(target: TransmittalScanTarget): Boolean = when (target) {
        TransmittalScanTarget.Serial -> serialNumber.isBlank()
        TransmittalScanTarget.Imei1 -> imei1.isBlank()
        TransmittalScanTarget.Imei2 -> imei2.isBlank()
        TransmittalScanTarget.Mobile -> mobileNumber.isBlank()
        TransmittalScanTarget.Auto -> nextAutoTarget(TransmittalItemMode.Phone) != null
    }

    fun nextAutoTarget(mode: TransmittalItemMode): TransmittalScanTarget? {
        return when (mode) {
            TransmittalItemMode.Standard ->
                TransmittalScanTarget.Serial.takeIf { serialNumber.isBlank() }
            TransmittalItemMode.Phone -> when {
                serialNumber.isBlank() -> TransmittalScanTarget.Serial
                imei1.isBlank() -> TransmittalScanTarget.Imei1
                imei2.isBlank() -> TransmittalScanTarget.Imei2
                mobileNumber.isBlank() -> TransmittalScanTarget.Mobile
                else -> null
            }
        }
    }

    fun withScanValue(target: TransmittalScanTarget, value: String): TransmittalItemDraft {
        return when (target) {
            TransmittalScanTarget.Serial,
            TransmittalScanTarget.Auto -> copy(serialNumber = value)
            TransmittalScanTarget.Imei1 -> copy(imei1 = value)
            TransmittalScanTarget.Imei2 -> copy(imei2 = value)
            TransmittalScanTarget.Mobile -> copy(mobileNumber = value)
        }
    }

    fun detailSummary(): String {
        return listOf(
            "Mobile" to mobileNumber,
            "Serial" to serialNumber,
            "IMEI 1" to imei1,
            "IMEI 2" to imei2
        )
            .filter { it.second.isNotBlank() }
            .joinToString(" | ") { "${it.first}: ${it.second}" }
    }

    fun reportLine(mode: TransmittalItemMode): String {
        val detail = when (mode) {
            TransmittalItemMode.Standard -> serialNumber.trim().takeIf { it.isNotBlank() }?.let { "Serial: $it" }.orEmpty()
            TransmittalItemMode.Phone -> detailSummary()
        }
        return when {
            description.isNotBlank() && detail.isNotBlank() -> "${description.trim()} — $detail"
            description.isNotBlank() -> description.trim()
            detail.isNotBlank() -> detail
            else -> ""
        }
    }
}

data class TransmittalReport(
    val to: String,
    val from: String,
    val date: String,
    val items: List<TransmittalItemDraft>,
    val preparedBy: String,
    val transmitBy: String,
    val receivedByDate: String,
    val notedBy: String,
    val approvedByDate: String,
    val itemMode: TransmittalItemMode = TransmittalItemMode.Standard
) {
    companion object {
        const val TEMPLATE_ITEM_COUNT = 12
        const val MAX_BATCH_SIZE = 500
        const val ATTACHMENT_NOTE = "*see attached file for IMEI1, IMEI2, Serial numbers and Mobile number*"
    }

    private fun repeatedDescriptionSummaries(): List<String> {
        return items
            .map { it.description.trim() }
            .filter { it.isNotBlank() }
            .groupBy { it }
            .filterValues { it.size > 1 }
            .map { (description, rows) -> "${rows.size} units - $description" }
    }

    fun lineText(index: Int): String {
        val repeatedSummaries = repeatedDescriptionSummaries()
        if (repeatedSummaries.isNotEmpty()) {
            if (index == 0) return repeatedSummaries.joinToString("; ")
            if (itemMode == TransmittalItemMode.Phone && index == 4 && items.any { it.hasDetailedValues() }) {
                return ATTACHMENT_NOTE
            }
            return ""
        }

        val itemText = items.getOrNull(index)?.reportLine(itemMode).orEmpty()
        if (itemText.isNotBlank()) return itemText

        return if (itemMode == TransmittalItemMode.Phone && index == 4 && items.any { it.hasDetailedValues() }) {
            ATTACHMENT_NOTE
        } else {
            ""
        }
    }
}


enum class LocalDocumentKind(
    val title: String,
    val subtitle: String,
    val exportLabel: String
) {
    Transmittal(
        title = "Transmittal Scan",
        subtitle = "Populate the Yakult transmittal form from local serial, IMEI, and mobile-number scans.",
        exportLabel = "Export Excel report"
    ),
    Gatepass(
        title = "Gatepass / File Transmittal",
        subtitle = "Create the local Gatepass/File form from scanned or manually entered item details.",
        exportLabel = "Save Gatepass PDF"
    )
}

enum class GatepassFormType(val label: String) {
    Gatepass("Gatepass"),
    Transmittal("Transmittal"),
    File("File")
}

enum class GatepassCompany(val label: String) {
    YakultPhilippines("Yakult Philippines Inc."),
    YakultMarketing("Yakult Marketing Corp.")
}

enum class GatepassItemCategory(val label: String) {
    CartridgeRibbon("Cartridge Ribbon"),
    Monitor("Monitor"),
    Cpu("CPU"),
    Mouse("Mouse"),
    Keyboard("Keyboard"),
    Printer("Printer"),
    BackupUps("Backup UPS"),
    Avr("AVR"),
    ComputerTableFixedAsset("Computer Table / Fixed Asset"),
    Others("Others")
}

enum class GatepassModel(val label: String) {
    Lx300("LX300"),
    Lx310("LX310")
}

data class GatepassReport(
    val formType: GatepassFormType = GatepassFormType.File,
    val company: GatepassCompany = GatepassCompany.YakultPhilippines,
    val to: String,
    val from: String,
    val date: String,
    val itemCategory: GatepassItemCategory? = null,
    val itemCategoryOther: String = "",
    val model: GatepassModel? = null,
    val fixedAssetNumber: String = "",
    val quantity: String = "",
    val items: List<TransmittalItemDraft> = emptyList(),
    val others: String = "",
    val remarks: String = "",
    val issuedBy: String = "",
    val notedBy: String = "",
    val receivedByDate: String = "",
    val approvedByDate: String = "",
    /** When enabled, print Gatepass, Transmittal, and File across two Legal sheets. */
    val printAllFormTypes: Boolean = false
) {
    companion object {
        const val TEMPLATE_ITEM_COUNT = 12
    }

    fun hasContent(): Boolean =
        to.isNotBlank() ||
            from.isNotBlank() ||
            date.isNotBlank() ||
            itemCategory != null ||
            itemCategoryOther.isNotBlank() ||
            model != null ||
            fixedAssetNumber.isNotBlank() ||
            quantity.isNotBlank() ||
            items.any { it.hasAnyValue() } ||
            others.isNotBlank() ||
            remarks.isNotBlank() ||
            issuedBy.isNotBlank() ||
            notedBy.isNotBlank() ||
            receivedByDate.isNotBlank() ||
            approvedByDate.isNotBlank()

    fun categoryLabel(): String = itemCategory?.label.orEmpty()

    fun lineText(index: Int): String {
        val scannedLine = items.getOrNull(index)
            ?.reportLine(TransmittalItemMode.Standard)
            .orEmpty()
        if (scannedLine.isNotBlank()) return scannedLine

        if (index == 0) {
            return listOf(
                categoryLabel(),
                itemCategoryOther.takeIf { itemCategory == GatepassItemCategory.Others },
                model?.label,
                fixedAssetNumber.takeIf { it.isNotBlank() }?.let { "Asset: $it" },
                quantity.takeIf { it.isNotBlank() }?.let { "Qty: $it" }
            )
                .filterNotNull()
                .filter(String::isNotBlank)
                .joinToString(" | ")
        }
        return ""
    }
}
