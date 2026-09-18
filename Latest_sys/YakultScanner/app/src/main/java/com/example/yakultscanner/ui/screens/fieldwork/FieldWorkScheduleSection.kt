package com.example.yakultscanner.ui.screens.fieldwork

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.example.yakultscanner.api.CallLookupItem

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun FieldWorkScheduleSection(
    itEmployees: List<CallLookupItem>,
    selectedTechId: Int?,
    onTechSelected: (Int?) -> Unit,
    notesDraft: String,
    onNotesChange: (String) -> Unit,
    onSchedule: () -> Unit,
    techExpanded: Boolean,
    onExpandedChange: (Boolean) -> Unit
) {
    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
        if (itEmployees.isNotEmpty()) {
            ExposedDropdownMenuBox(expanded = techExpanded, onExpandedChange = onExpandedChange) {
                OutlinedTextField(
                    value = itEmployees.find { it.id == selectedTechId }?.name ?: "",
                    onValueChange = {}, readOnly = true,
                    label = { Text("Technician") }, placeholder = { Text("Select technician") },
                    modifier = Modifier.fillMaxWidth().menuAnchor(), shape = RoundedCornerShape(10.dp)
                )
                ExposedDropdownMenu(expanded = techExpanded, onDismissRequest = { onExpandedChange(false) }) {
                    DropdownMenuItem(text = { Text("— No technician —") }, onClick = { onTechSelected(null); onExpandedChange(false) })
                    itEmployees.forEach { emp -> DropdownMenuItem(text = { Text(emp.name) }, onClick = { onTechSelected(emp.id); onExpandedChange(false) }) }
                }
            }
        }
        OutlinedTextField(value = notesDraft, onValueChange = onNotesChange, modifier = Modifier.fillMaxWidth(), label = { Text("Notes (optional)") }, placeholder = { Text("Reason for visit, location, etc.") }, maxLines = 3, shape = RoundedCornerShape(10.dp))
        Button(onClick = onSchedule, modifier = Modifier.fillMaxWidth(), colors = ButtonDefaults.buttonColors(containerColor = com.example.yakultscanner.ui.components.ItcmUi.Brand)) {
            Text("Schedule Visit", fontWeight = androidx.compose.ui.text.font.FontWeight.Bold)
        }
    }
}
