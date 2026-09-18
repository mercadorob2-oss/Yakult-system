package com.example.yakultscanner

import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.TextView
import androidx.recyclerview.widget.RecyclerView

class ItemAdapter(private val items: List<ParsedItem>) : RecyclerView.Adapter<ItemAdapter.ItemViewHolder>() {

    class ItemViewHolder(view: View) : RecyclerView.ViewHolder(view) {
        val itemNameText: TextView = view.findViewById(R.id.itemNameText)
        val categoryText: TextView = view.findViewById(R.id.categoryText)
        val serialText: TextView = view.findViewById(R.id.serialText)
        val qtyText: TextView = view.findViewById(R.id.qtyText)
        val itemStatusText: TextView = view.findViewById(R.id.itemStatusText)
    }

    override fun onCreateViewHolder(parent: ViewGroup, viewType: Int): ItemViewHolder {
        val view = LayoutInflater.from(parent.context)
            .inflate(R.layout.item_layout, parent, false)
        return ItemViewHolder(view)
    }

    override fun onBindViewHolder(holder: ItemViewHolder, position: Int) {
        val item = items[position]
        holder.itemNameText.text = "Item Name: ${item.itemName}"
        holder.categoryText.text = "Category: ${item.category}"
        holder.serialText.text = "Serial: ${item.serial}"
        holder.qtyText.text = "Qty: ${item.qty}"
        holder.itemStatusText.text = "Status: ${item.status}"
    }

    override fun getItemCount() = items.size
}