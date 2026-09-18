package com.example.yakultscanner

import android.os.Bundle
import android.widget.TextView
import androidx.appcompat.app.AppCompatActivity
import androidx.recyclerview.widget.LinearLayoutManager
import androidx.recyclerview.widget.RecyclerView

data class ParsedSet(
    val setCode: String,
    val employee: String,
    val company: String,
    val department: String,
    val branch: String,
    val dispatchDate: String,
    val status: String,
    val created: String,
    val items: List<ParsedItem>
)

data class ParsedItem(
    val itemName: String,
    val category: String,
    val serial: String,
    val qty: String,
    val status: String
)

class DetailsActivity : AppCompatActivity() {

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_details)

        val rawQrData = intent.getStringExtra(EXTRA_QR_CODE_DATA)
        if (rawQrData != null) {
            val parsedSet = parseQrData(rawQrData)
            displayParsedData(parsedSet)
        }
    }

    private fun parseQrData(data: String): ParsedSet {
        val lines = data.split('\n')

        val setCode = lines.find { it.startsWith("Set Code:") }?.substringAfter(":")?.trim() ?: ""
        val employee = lines.find { it.startsWith("Employee:") }?.substringAfter(":")?.trim() ?: ""
        val company = lines.find { it.startsWith("Company:") }?.substringAfter(":")?.trim() ?: ""
        val department = lines.find { it.startsWith("Department:") }?.substringAfter(":")?.trim() ?: ""
        val branch = lines.find { it.startsWith("Branch:") }?.substringAfter(":")?.trim() ?: ""
        val dispatchDate = lines.find { it.startsWith("Dispatch Date:") }?.substringAfter(":")?.trim() ?: ""
        val status = lines.find { it.startsWith("Status:") }?.substringAfter(":")?.trim() ?: ""
        val created = lines.find { it.startsWith("Created:") }?.substringAfter(":")?.trim() ?: ""

        val itemsHeaderIndex = lines.indexOf("ITEMS:")
        val items = if (itemsHeaderIndex != -1) {
            lines.subList(itemsHeaderIndex + 1, lines.size)
                .mapNotNull { line ->
                    if (line.isNotBlank() && line != "Scan to verify dispatch record.") {
                        val parts = line.split('|').map { it.trim() }
                        if (parts.size == 5) {
                            ParsedItem(parts[0], parts[1], parts[2], parts[3], parts[4])
                        } else null
                    } else null
                }
        } else emptyList()

        return ParsedSet(setCode, employee, company, department, branch, dispatchDate, status, created, items)
    }

    private fun displayParsedData(parsedSet: ParsedSet) {
        findViewById<TextView>(R.id.setCodeText).text = "Set Code: ${parsedSet.setCode}"
        findViewById<TextView>(R.id.employeeText).text = "Employee: ${parsedSet.employee}"
        findViewById<TextView>(R.id.companyText).text = "Company: ${parsedSet.company}"
        findViewById<TextView>(R.id.departmentText).text = "Department: ${parsedSet.department}"
        findViewById<TextView>(R.id.branchText).text = "Branch: ${parsedSet.branch}"
        findViewById<TextView>(R.id.dispatchDateText).text = "Dispatch Date: ${parsedSet.dispatchDate}"
        findViewById<TextView>(R.id.statusText).text = "Status: ${parsedSet.status}"
        findViewById<TextView>(R.id.createdText).text = "Created: ${parsedSet.created}"

        val itemsRecyclerView = findViewById<RecyclerView>(R.id.itemsRecyclerView)
        itemsRecyclerView.layoutManager = LinearLayoutManager(this)
        itemsRecyclerView.adapter = ItemAdapter(parsedSet.items)
    }

    companion object {
        const val EXTRA_QR_CODE_DATA = "extra_qr_code_data"
    }
}
