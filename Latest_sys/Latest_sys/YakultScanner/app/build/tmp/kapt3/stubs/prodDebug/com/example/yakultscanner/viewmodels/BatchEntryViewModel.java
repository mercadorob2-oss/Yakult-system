package com.example.yakultscanner.viewmodels;

import androidx.lifecycle.ViewModel;
import com.example.yakultscanner.UserSession;
import com.example.yakultscanner.api.ApiResult;
import com.example.yakultscanner.api.BatchItemRequest;
import com.example.yakultscanner.api.BatchItemsRequest;
import com.example.yakultscanner.api.ConditionDto;
import com.example.yakultscanner.api.ItemCategoryDto;
import com.example.yakultscanner.api.VendorDto;
import com.example.yakultscanner.data.repository.InventoryRepository;
import java.text.SimpleDateFormat;
import java.util.*;
import dagger.hilt.android.lifecycle.HiltViewModel;
import javax.inject.Inject;

/**
 * ViewModel for BatchSerialEntryScreen.
 * Handles all state and business logic for batch item entry.
 */
@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000x\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0010\u000e\n\u0002\b\u0017\n\u0002\u0010 \n\u0002\b\u0006\n\u0002\u0018\u0002\n\u0002\b\u0004\n\u0002\u0018\u0002\n\u0002\b\u001e\n\u0002\u0018\u0002\n\u0002\b\n\n\u0002\u0018\u0002\n\u0002\b\u001a\n\u0002\u0010\u000b\n\u0002\b$\n\u0002\u0010\u0002\n\u0002\b\u0013\n\u0002\u0010\b\n\u0002\b\u0004\n\u0002\u0010\"\n\u0002\b\u0016\n\u0002\u0010\t\n\u0002\b\b\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\b\u0007\u0018\u00002\u00020\u0001B\u0011\b\u0007\u0012\u0006\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\u0004\b\u0004\u0010\u0005J\b\u0010\u0094\u0001\u001a\u00030\u0095\u0001J\u0011\u0010\u0096\u0001\u001a\u00030\u0095\u0001H\u0082@\u00a2\u0006\u0003\u0010\u0097\u0001J\u0011\u0010\u0098\u0001\u001a\u00030\u0095\u0001H\u0082@\u00a2\u0006\u0003\u0010\u0097\u0001J\u0011\u0010\u0099\u0001\u001a\u00030\u0095\u0001H\u0082@\u00a2\u0006\u0003\u0010\u0097\u0001J\u0011\u0010\u009a\u0001\u001a\u00030\u0095\u00012\u0007\u0010\u009b\u0001\u001a\u00020\u0007J\u0011\u0010\u009c\u0001\u001a\u00030\u0095\u00012\u0007\u0010\u009b\u0001\u001a\u00020\u0007J\u0011\u0010\u009d\u0001\u001a\u00030\u0095\u00012\u0007\u0010\u009b\u0001\u001a\u00020\u0007J\u0011\u0010\u009e\u0001\u001a\u00030\u0095\u00012\u0007\u0010\u009b\u0001\u001a\u00020\u0007J\u0011\u0010\u009f\u0001\u001a\u00030\u0095\u00012\u0007\u0010\u009b\u0001\u001a\u00020\u0007J\b\u0010\u00a0\u0001\u001a\u00030\u0095\u0001J\u0011\u0010\u00a1\u0001\u001a\u00030\u0095\u00012\u0007\u0010\u00a2\u0001\u001a\u00020\u0007J\u0007\u0010\u00a3\u0001\u001a\u00020pJ\b\u0010\u00a4\u0001\u001a\u00030\u0095\u0001J\u0011\u0010\u00a5\u0001\u001a\u00030\u0095\u00012\u0007\u0010\u00a6\u0001\u001a\u00020\u0007J\u0012\u0010\u00a7\u0001\u001a\u00030\u0095\u00012\b\u0010\u00a8\u0001\u001a\u00030\u00a9\u0001J\u001b\u0010\u00aa\u0001\u001a\u00030\u0095\u00012\b\u0010\u00a8\u0001\u001a\u00030\u00a9\u00012\u0007\u0010\u009b\u0001\u001a\u00020&J\u0010\u0010\u00ab\u0001\u001a\u00020p2\u0007\u0010\u00ac\u0001\u001a\u00020\u0007J$\u0010\u00ad\u0001\u001a\t\u0012\u0004\u0012\u00020\u00070\u00ae\u00012\f\b\u0002\u0010\u00af\u0001\u001a\u0005\u0018\u00010\u00a9\u0001H\u0002\u00a2\u0006\u0003\u0010\u00b0\u0001J\u000f\u0010\u00b1\u0001\u001a\b\u0012\u0004\u0012\u00020&0\u001fH\u0002J\u001a\u0010\u00b2\u0001\u001a\u0004\u0018\u00010\u00072\r\u0010\u00b3\u0001\u001a\b\u0012\u0004\u0012\u00020&0\u001fH\u0002J\u0013\u0010\u00b4\u0001\u001a\u00030\u0095\u00012\t\u0010\u00b5\u0001\u001a\u0004\u0018\u00010+J\u0013\u0010\u00b6\u0001\u001a\u00030\u0095\u00012\t\u0010\u00b7\u0001\u001a\u0004\u0018\u00010JJ\u0011\u0010\u00b8\u0001\u001a\u00030\u0095\u00012\u0007\u0010\u00b9\u0001\u001a\u00020\u0007J\u0011\u0010\u00ba\u0001\u001a\u00030\u0095\u00012\u0007\u0010\u009b\u0001\u001a\u00020\u0007J\u0011\u0010\u00bb\u0001\u001a\u00030\u0095\u00012\u0007\u0010\u009b\u0001\u001a\u00020\u0007J\u0011\u0010\u00bc\u0001\u001a\u00030\u0095\u00012\u0007\u0010\u009b\u0001\u001a\u00020\u0007J\u0013\u0010\u00bd\u0001\u001a\u00030\u0095\u00012\t\u0010\u00be\u0001\u001a\u0004\u0018\u00010UJ\u0011\u0010\u00bf\u0001\u001a\u00030\u0095\u00012\u0007\u0010\u009b\u0001\u001a\u00020\u0007J\u0011\u0010\u00c0\u0001\u001a\u00030\u0095\u00012\u0007\u0010\u009b\u0001\u001a\u00020\u0007J\u0011\u0010\u00c1\u0001\u001a\u00030\u0095\u00012\u0007\u0010\u009b\u0001\u001a\u00020\u0007J\u0011\u0010\u00c2\u0001\u001a\u00030\u0095\u00012\u0007\u0010\u009b\u0001\u001a\u00020\u0007J\u0013\u0010\u00c3\u0001\u001a\u00020\u00072\b\u0010\u00c4\u0001\u001a\u00030\u00c5\u0001H\u0002J\u0012\u0010\u00c6\u0001\u001a\u00030\u0095\u00012\b\u0010\u00c4\u0001\u001a\u00030\u00c5\u0001J\u0012\u0010\u00c7\u0001\u001a\u00030\u0095\u00012\b\u0010\u00c4\u0001\u001a\u00030\u00c5\u0001J\u0012\u0010\u00c8\u0001\u001a\u00030\u0095\u00012\b\u0010\u00c4\u0001\u001a\u00030\u00c5\u0001J\u0007\u0010\u00c9\u0001\u001a\u00020pJ\b\u0010\u00ca\u0001\u001a\u00030\u0095\u0001J\b\u0010\u00cb\u0001\u001a\u00030\u0095\u0001J0\u0010\u00cc\u0001\u001a\u00030\u0095\u00012\u000f\u0010\u00cd\u0001\u001a\n\u0012\u0005\u0012\u00030\u0095\u00010\u00ce\u00012\u0015\u0010\u00cf\u0001\u001a\u0010\u0012\u0004\u0012\u00020\u0007\u0012\u0005\u0012\u00030\u0095\u00010\u00d0\u0001J\n\u0010\u00d1\u0001\u001a\u00030\u0095\u0001H\u0002R\u000e\u0010\u0002\u001a\u00020\u0003X\u0082\u0004\u00a2\u0006\u0002\n\u0000R+\u0010\b\u001a\u00020\u00072\u0006\u0010\u0006\u001a\u00020\u00078F@BX\u0086\u008e\u0002\u00a2\u0006\u0012\n\u0004\b\r\u0010\u000e\u001a\u0004\b\t\u0010\n\"\u0004\b\u000b\u0010\fR+\u0010\u000f\u001a\u00020\u00072\u0006\u0010\u0006\u001a\u00020\u00078F@BX\u0086\u008e\u0002\u00a2\u0006\u0012\n\u0004\b\u0012\u0010\u000e\u001a\u0004\b\u0010\u0010\n\"\u0004\b\u0011\u0010\fR+\u0010\u0013\u001a\u00020\u00072\u0006\u0010\u0006\u001a\u00020\u00078F@BX\u0086\u008e\u0002\u00a2\u0006\u0012\n\u0004\b\u0016\u0010\u000e\u001a\u0004\b\u0014\u0010\n\"\u0004\b\u0015\u0010\fR+\u0010\u0017\u001a\u00020\u00072\u0006\u0010\u0006\u001a\u00020\u00078F@BX\u0086\u008e\u0002\u00a2\u0006\u0012\n\u0004\b\u001a\u0010\u000e\u001a\u0004\b\u0018\u0010\n\"\u0004\b\u0019\u0010\fR+\u0010\u001b\u001a\u00020\u00072\u0006\u0010\u0006\u001a\u00020\u00078F@BX\u0086\u008e\u0002\u00a2\u0006\u0012\n\u0004\b\u001e\u0010\u000e\u001a\u0004\b\u001c\u0010\n\"\u0004\b\u001d\u0010\fR7\u0010 \u001a\b\u0012\u0004\u0012\u00020\u00070\u001f2\f\u0010\u0006\u001a\b\u0012\u0004\u0012\u00020\u00070\u001f8F@BX\u0086\u008e\u0002\u00a2\u0006\u0012\n\u0004\b%\u0010\u000e\u001a\u0004\b!\u0010\"\"\u0004\b#\u0010$R7\u0010\'\u001a\b\u0012\u0004\u0012\u00020&0\u001f2\f\u0010\u0006\u001a\b\u0012\u0004\u0012\u00020&0\u001f8F@BX\u0086\u008e\u0002\u00a2\u0006\u0012\n\u0004\b*\u0010\u000e\u001a\u0004\b(\u0010\"\"\u0004\b)\u0010$R7\u0010,\u001a\b\u0012\u0004\u0012\u00020+0\u001f2\f\u0010\u0006\u001a\b\u0012\u0004\u0012\u00020+0\u001f8F@BX\u0086\u008e\u0002\u00a2\u0006\u0012\n\u0004\b/\u0010\u000e\u001a\u0004\b-\u0010\"\"\u0004\b.\u0010$R/\u00100\u001a\u0004\u0018\u00010+2\b\u0010\u0006\u001a\u0004\u0018\u00010+8F@BX\u0086\u008e\u0002\u00a2\u0006\u0012\n\u0004\b5\u0010\u000e\u001a\u0004\b1\u00102\"\u0004\b3\u00104R7\u00106\u001a\b\u0012\u0004\u0012\u00020\u00070\u001f2\f\u0010\u0006\u001a\b\u0012\u0004\u0012\u00020\u00070\u001f8F@BX\u0086\u008e\u0002\u00a2\u0006\u0012\n\u0004\b9\u0010\u000e\u001a\u0004\b7\u0010\"\"\u0004\b8\u0010$R+\u0010:\u001a\u00020\u00072\u0006\u0010\u0006\u001a\u00020\u00078F@BX\u0086\u008e\u0002\u00a2\u0006\u0012\n\u0004\b=\u0010\u000e\u001a\u0004\b;\u0010\n\"\u0004\b<\u0010\fR+\u0010>\u001a\u00020\u00072\u0006\u0010\u0006\u001a\u00020\u00078F@BX\u0086\u008e\u0002\u00a2\u0006\u0012\n\u0004\bA\u0010\u000e\u001a\u0004\b?\u0010\n\"\u0004\b@\u0010\fR+\u0010B\u001a\u00020\u00072\u0006\u0010\u0006\u001a\u00020\u00078F@BX\u0086\u008e\u0002\u00a2\u0006\u0012\n\u0004\bE\u0010\u000e\u001a\u0004\bC\u0010\n\"\u0004\bD\u0010\fR+\u0010F\u001a\u00020\u00072\u0006\u0010\u0006\u001a\u00020\u00078F@BX\u0086\u008e\u0002\u00a2\u0006\u0012\n\u0004\bI\u0010\u000e\u001a\u0004\bG\u0010\n\"\u0004\bH\u0010\fR7\u0010K\u001a\b\u0012\u0004\u0012\u00020J0\u001f2\f\u0010\u0006\u001a\b\u0012\u0004\u0012\u00020J0\u001f8F@BX\u0086\u008e\u0002\u00a2\u0006\u0012\n\u0004\bN\u0010\u000e\u001a\u0004\bL\u0010\"\"\u0004\bM\u0010$R/\u0010O\u001a\u0004\u0018\u00010J2\b\u0010\u0006\u001a\u0004\u0018\u00010J8F@BX\u0086\u008e\u0002\u00a2\u0006\u0012\n\u0004\bT\u0010\u000e\u001a\u0004\bP\u0010Q\"\u0004\bR\u0010SR7\u0010V\u001a\b\u0012\u0004\u0012\u00020U0\u001f2\f\u0010\u0006\u001a\b\u0012\u0004\u0012\u00020U0\u001f8F@BX\u0086\u008e\u0002\u00a2\u0006\u0012\n\u0004\bY\u0010\u000e\u001a\u0004\bW\u0010\"\"\u0004\bX\u0010$R/\u0010Z\u001a\u0004\u0018\u00010U2\b\u0010\u0006\u001a\u0004\u0018\u00010U8F@BX\u0086\u008e\u0002\u00a2\u0006\u0012\n\u0004\b_\u0010\u000e\u001a\u0004\b[\u0010\\\"\u0004\b]\u0010^R+\u0010`\u001a\u00020\u00072\u0006\u0010\u0006\u001a\u00020\u00078F@BX\u0086\u008e\u0002\u00a2\u0006\u0012\n\u0004\bc\u0010\u000e\u001a\u0004\ba\u0010\n\"\u0004\bb\u0010\fR+\u0010d\u001a\u00020\u00072\u0006\u0010\u0006\u001a\u00020\u00078F@BX\u0086\u008e\u0002\u00a2\u0006\u0012\n\u0004\bg\u0010\u000e\u001a\u0004\be\u0010\n\"\u0004\bf\u0010\fR+\u0010h\u001a\u00020\u00072\u0006\u0010\u0006\u001a\u00020\u00078F@BX\u0086\u008e\u0002\u00a2\u0006\u0012\n\u0004\bk\u0010\u000e\u001a\u0004\bi\u0010\n\"\u0004\bj\u0010\fR+\u0010l\u001a\u00020\u00072\u0006\u0010\u0006\u001a\u00020\u00078F@BX\u0086\u008e\u0002\u00a2\u0006\u0012\n\u0004\bo\u0010\u000e\u001a\u0004\bm\u0010\n\"\u0004\bn\u0010\fR+\u0010q\u001a\u00020p2\u0006\u0010\u0006\u001a\u00020p8F@FX\u0086\u008e\u0002\u00a2\u0006\u0012\n\u0004\bv\u0010\u000e\u001a\u0004\br\u0010s\"\u0004\bt\u0010uR+\u0010w\u001a\u00020p2\u0006\u0010\u0006\u001a\u00020p8F@FX\u0086\u008e\u0002\u00a2\u0006\u0012\n\u0004\bz\u0010\u000e\u001a\u0004\bx\u0010s\"\u0004\by\u0010uR+\u0010{\u001a\u00020p2\u0006\u0010\u0006\u001a\u00020p8F@FX\u0086\u008e\u0002\u00a2\u0006\u0012\n\u0004\b~\u0010\u000e\u001a\u0004\b|\u0010s\"\u0004\b}\u0010uR.\u0010\u007f\u001a\u00020p2\u0006\u0010\u0006\u001a\u00020p8F@FX\u0086\u008e\u0002\u00a2\u0006\u0015\n\u0005\b\u0082\u0001\u0010\u000e\u001a\u0005\b\u0080\u0001\u0010s\"\u0005\b\u0081\u0001\u0010uR/\u0010\u0083\u0001\u001a\u00020p2\u0006\u0010\u0006\u001a\u00020p8F@FX\u0086\u008e\u0002\u00a2\u0006\u0015\n\u0005\b\u0086\u0001\u0010\u000e\u001a\u0005\b\u0084\u0001\u0010s\"\u0005\b\u0085\u0001\u0010uR/\u0010\u0087\u0001\u001a\u00020p2\u0006\u0010\u0006\u001a\u00020p8F@BX\u0086\u008e\u0002\u00a2\u0006\u0015\n\u0005\b\u0089\u0001\u0010\u000e\u001a\u0005\b\u0087\u0001\u0010s\"\u0005\b\u0088\u0001\u0010uR3\u0010\u008a\u0001\u001a\u0004\u0018\u00010\u00072\b\u0010\u0006\u001a\u0004\u0018\u00010\u00078F@BX\u0086\u008e\u0002\u00a2\u0006\u0015\n\u0005\b\u008d\u0001\u0010\u000e\u001a\u0005\b\u008b\u0001\u0010\n\"\u0005\b\u008c\u0001\u0010\fR3\u0010\u008e\u0001\u001a\u0004\u0018\u00010\u00072\b\u0010\u0006\u001a\u0004\u0018\u00010\u00078F@BX\u0086\u008e\u0002\u00a2\u0006\u0015\n\u0005\b\u0091\u0001\u0010\u000e\u001a\u0005\b\u008f\u0001\u0010\n\"\u0005\b\u0090\u0001\u0010\fR\u0019\u0010\u0092\u0001\u001a\b\u0012\u0004\u0012\u00020\u00070\u001f\u00a2\u0006\t\n\u0000\u001a\u0005\b\u0093\u0001\u0010\"\u00a8\u0006\u00d2\u0001"}, d2 = {"Lcom/example/yakultscanner/viewmodels/BatchEntryViewModel;", "Landroidx/lifecycle/ViewModel;", "repository", "Lcom/example/yakultscanner/data/repository/InventoryRepository;", "<init>", "(Lcom/example/yakultscanner/data/repository/InventoryRepository;)V", "<set-?>", "", "itemName", "getItemName", "()Ljava/lang/String;", "setItemName", "(Ljava/lang/String;)V", "itemName$delegate", "Landroidx/compose/runtime/MutableState;", "description", "getDescription", "setDescription", "description$delegate", "selectedType", "getSelectedType", "setSelectedType", "selectedType$delegate", "modelNumber", "getModelNumber", "setModelNumber", "modelNumber$delegate", "currentSerial", "getCurrentSerial", "setCurrentSerial", "currentSerial$delegate", "", "serialNumbers", "getSerialNumbers", "()Ljava/util/List;", "setSerialNumbers", "(Ljava/util/List;)V", "serialNumbers$delegate", "Lcom/example/yakultscanner/viewmodels/PhoneItemEntry;", "phoneItems", "getPhoneItems", "setPhoneItems", "phoneItems$delegate", "Lcom/example/yakultscanner/api/ItemCategoryDto;", "categories", "getCategories", "setCategories", "categories$delegate", "selectedCategory", "getSelectedCategory", "()Lcom/example/yakultscanner/api/ItemCategoryDto;", "setSelectedCategory", "(Lcom/example/yakultscanner/api/ItemCategoryDto;)V", "selectedCategory$delegate", "unitOptions", "getUnitOptions", "setUnitOptions", "unitOptions$delegate", "selectedUnit", "getSelectedUnit", "setSelectedUnit", "selectedUnit$delegate", "amount", "getAmount", "setAmount", "amount$delegate", "startDate", "getStartDate", "setStartDate", "startDate$delegate", "endDate", "getEndDate", "setEndDate", "endDate$delegate", "Lcom/example/yakultscanner/api/ConditionDto;", "conditions", "getConditions", "setConditions", "conditions$delegate", "selectedCondition", "getSelectedCondition", "()Lcom/example/yakultscanner/api/ConditionDto;", "setSelectedCondition", "(Lcom/example/yakultscanner/api/ConditionDto;)V", "selectedCondition$delegate", "Lcom/example/yakultscanner/api/VendorDto;", "vendors", "getVendors", "setVendors", "vendors$delegate", "selectedVendor", "getSelectedVendor", "()Lcom/example/yakultscanner/api/VendorDto;", "setSelectedVendor", "(Lcom/example/yakultscanner/api/VendorDto;)V", "selectedVendor$delegate", "warrantyYears", "getWarrantyYears", "setWarrantyYears", "warrantyYears$delegate", "datePurchased", "getDatePurchased", "setDatePurchased", "datePurchased$delegate", "licenseNumber", "getLicenseNumber", "setLicenseNumber", "licenseNumber$delegate", "remarks", "getRemarks", "setRemarks", "remarks$delegate", "", "typeExpanded", "getTypeExpanded", "()Z", "setTypeExpanded", "(Z)V", "typeExpanded$delegate", "categoryExpanded", "getCategoryExpanded", "setCategoryExpanded", "categoryExpanded$delegate", "unitExpanded", "getUnitExpanded", "setUnitExpanded", "unitExpanded$delegate", "conditionExpanded", "getConditionExpanded", "setConditionExpanded", "conditionExpanded$delegate", "vendorExpanded", "getVendorExpanded", "setVendorExpanded", "vendorExpanded$delegate", "isSaving", "setSaving", "isSaving$delegate", "errorMessage", "getErrorMessage", "setErrorMessage", "errorMessage$delegate", "successMessage", "getSuccessMessage", "setSuccessMessage", "successMessage$delegate", "typeOptions", "getTypeOptions", "loadInitialData", "", "loadCategories", "(Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "loadConditions", "loadVendors", "updateItemName", "value", "updateDescription", "updateSelectedType", "updateModelNumber", "updateCurrentSerial", "addSerial", "removeSerial", "serial", "isCellphoneCategory", "addPhoneRow", "generatePhoneRows", "countText", "removePhoneRow", "index", "", "updatePhoneRow", "addPhoneSerialFromScan", "rawSerial", "phoneCodeSet", "", "excludeIndex", "(Ljava/lang/Integer;)Ljava/util/Set;", "validPhoneRows", "duplicatePhoneEntryMessage", "rows", "updateSelectedCategory", "category", "updateSelectedCondition", "condition", "updateSelectedUnit", "unit", "updateAmount", "updateStartDate", "updateEndDate", "updateSelectedVendor", "vendor", "updateWarrantyYears", "updateDatePurchased", "updateLicenseNumber", "updateRemarks", "formatDate", "millis", "", "onStartDateSelected", "onEndDateSelected", "onDatePurchasedSelected", "isFormValid", "clearError", "clearSuccess", "saveBatchItems", "onSuccess", "Lkotlin/Function0;", "onError", "Lkotlin/Function1;", "resetForm", "app_prodDebug"})
@dagger.hilt.android.lifecycle.HiltViewModel()
public final class BatchEntryViewModel extends androidx.lifecycle.ViewModel {
    @org.jetbrains.annotations.NotNull()
    private final com.example.yakultscanner.data.repository.InventoryRepository repository = null;
    @org.jetbrains.annotations.NotNull()
    private final androidx.compose.runtime.MutableState itemName$delegate = null;
    @org.jetbrains.annotations.NotNull()
    private final androidx.compose.runtime.MutableState description$delegate = null;
    @org.jetbrains.annotations.NotNull()
    private final androidx.compose.runtime.MutableState selectedType$delegate = null;
    @org.jetbrains.annotations.NotNull()
    private final androidx.compose.runtime.MutableState modelNumber$delegate = null;
    @org.jetbrains.annotations.NotNull()
    private final androidx.compose.runtime.MutableState currentSerial$delegate = null;
    @org.jetbrains.annotations.NotNull()
    private final androidx.compose.runtime.MutableState serialNumbers$delegate = null;
    @org.jetbrains.annotations.NotNull()
    private final androidx.compose.runtime.MutableState phoneItems$delegate = null;
    @org.jetbrains.annotations.NotNull()
    private final androidx.compose.runtime.MutableState categories$delegate = null;
    @org.jetbrains.annotations.NotNull()
    private final androidx.compose.runtime.MutableState selectedCategory$delegate = null;
    @org.jetbrains.annotations.NotNull()
    private final androidx.compose.runtime.MutableState unitOptions$delegate = null;
    @org.jetbrains.annotations.NotNull()
    private final androidx.compose.runtime.MutableState selectedUnit$delegate = null;
    @org.jetbrains.annotations.NotNull()
    private final androidx.compose.runtime.MutableState amount$delegate = null;
    @org.jetbrains.annotations.NotNull()
    private final androidx.compose.runtime.MutableState startDate$delegate = null;
    @org.jetbrains.annotations.NotNull()
    private final androidx.compose.runtime.MutableState endDate$delegate = null;
    @org.jetbrains.annotations.NotNull()
    private final androidx.compose.runtime.MutableState conditions$delegate = null;
    @org.jetbrains.annotations.NotNull()
    private final androidx.compose.runtime.MutableState selectedCondition$delegate = null;
    @org.jetbrains.annotations.NotNull()
    private final androidx.compose.runtime.MutableState vendors$delegate = null;
    @org.jetbrains.annotations.NotNull()
    private final androidx.compose.runtime.MutableState selectedVendor$delegate = null;
    @org.jetbrains.annotations.NotNull()
    private final androidx.compose.runtime.MutableState warrantyYears$delegate = null;
    @org.jetbrains.annotations.NotNull()
    private final androidx.compose.runtime.MutableState datePurchased$delegate = null;
    @org.jetbrains.annotations.NotNull()
    private final androidx.compose.runtime.MutableState licenseNumber$delegate = null;
    @org.jetbrains.annotations.NotNull()
    private final androidx.compose.runtime.MutableState remarks$delegate = null;
    @org.jetbrains.annotations.NotNull()
    private final androidx.compose.runtime.MutableState typeExpanded$delegate = null;
    @org.jetbrains.annotations.NotNull()
    private final androidx.compose.runtime.MutableState categoryExpanded$delegate = null;
    @org.jetbrains.annotations.NotNull()
    private final androidx.compose.runtime.MutableState unitExpanded$delegate = null;
    @org.jetbrains.annotations.NotNull()
    private final androidx.compose.runtime.MutableState conditionExpanded$delegate = null;
    @org.jetbrains.annotations.NotNull()
    private final androidx.compose.runtime.MutableState vendorExpanded$delegate = null;
    @org.jetbrains.annotations.NotNull()
    private final androidx.compose.runtime.MutableState isSaving$delegate = null;
    @org.jetbrains.annotations.NotNull()
    private final androidx.compose.runtime.MutableState errorMessage$delegate = null;
    @org.jetbrains.annotations.NotNull()
    private final androidx.compose.runtime.MutableState successMessage$delegate = null;
    @org.jetbrains.annotations.NotNull()
    private final java.util.List<java.lang.String> typeOptions = null;
    
    @javax.inject.Inject()
    public BatchEntryViewModel(@org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.data.repository.InventoryRepository repository) {
        super();
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getItemName() {
        return null;
    }
    
    private final void setItemName(java.lang.String p0) {
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getDescription() {
        return null;
    }
    
    private final void setDescription(java.lang.String p0) {
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getSelectedType() {
        return null;
    }
    
    private final void setSelectedType(java.lang.String p0) {
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getModelNumber() {
        return null;
    }
    
    private final void setModelNumber(java.lang.String p0) {
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getCurrentSerial() {
        return null;
    }
    
    private final void setCurrentSerial(java.lang.String p0) {
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<java.lang.String> getSerialNumbers() {
        return null;
    }
    
    private final void setSerialNumbers(java.util.List<java.lang.String> p0) {
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.viewmodels.PhoneItemEntry> getPhoneItems() {
        return null;
    }
    
    private final void setPhoneItems(java.util.List<com.example.yakultscanner.viewmodels.PhoneItemEntry> p0) {
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.api.ItemCategoryDto> getCategories() {
        return null;
    }
    
    private final void setCategories(java.util.List<com.example.yakultscanner.api.ItemCategoryDto> p0) {
    }
    
    @org.jetbrains.annotations.Nullable()
    public final com.example.yakultscanner.api.ItemCategoryDto getSelectedCategory() {
        return null;
    }
    
    private final void setSelectedCategory(com.example.yakultscanner.api.ItemCategoryDto p0) {
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<java.lang.String> getUnitOptions() {
        return null;
    }
    
    private final void setUnitOptions(java.util.List<java.lang.String> p0) {
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getSelectedUnit() {
        return null;
    }
    
    private final void setSelectedUnit(java.lang.String p0) {
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getAmount() {
        return null;
    }
    
    private final void setAmount(java.lang.String p0) {
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getStartDate() {
        return null;
    }
    
    private final void setStartDate(java.lang.String p0) {
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getEndDate() {
        return null;
    }
    
    private final void setEndDate(java.lang.String p0) {
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.api.ConditionDto> getConditions() {
        return null;
    }
    
    private final void setConditions(java.util.List<com.example.yakultscanner.api.ConditionDto> p0) {
    }
    
    @org.jetbrains.annotations.Nullable()
    public final com.example.yakultscanner.api.ConditionDto getSelectedCondition() {
        return null;
    }
    
    private final void setSelectedCondition(com.example.yakultscanner.api.ConditionDto p0) {
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.api.VendorDto> getVendors() {
        return null;
    }
    
    private final void setVendors(java.util.List<com.example.yakultscanner.api.VendorDto> p0) {
    }
    
    @org.jetbrains.annotations.Nullable()
    public final com.example.yakultscanner.api.VendorDto getSelectedVendor() {
        return null;
    }
    
    private final void setSelectedVendor(com.example.yakultscanner.api.VendorDto p0) {
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getWarrantyYears() {
        return null;
    }
    
    private final void setWarrantyYears(java.lang.String p0) {
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getDatePurchased() {
        return null;
    }
    
    private final void setDatePurchased(java.lang.String p0) {
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getLicenseNumber() {
        return null;
    }
    
    private final void setLicenseNumber(java.lang.String p0) {
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getRemarks() {
        return null;
    }
    
    private final void setRemarks(java.lang.String p0) {
    }
    
    public final boolean getTypeExpanded() {
        return false;
    }
    
    public final void setTypeExpanded(boolean p0) {
    }
    
    public final boolean getCategoryExpanded() {
        return false;
    }
    
    public final void setCategoryExpanded(boolean p0) {
    }
    
    public final boolean getUnitExpanded() {
        return false;
    }
    
    public final void setUnitExpanded(boolean p0) {
    }
    
    public final boolean getConditionExpanded() {
        return false;
    }
    
    public final void setConditionExpanded(boolean p0) {
    }
    
    public final boolean getVendorExpanded() {
        return false;
    }
    
    public final void setVendorExpanded(boolean p0) {
    }
    
    public final boolean isSaving() {
        return false;
    }
    
    private final void setSaving(boolean p0) {
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getErrorMessage() {
        return null;
    }
    
    private final void setErrorMessage(java.lang.String p0) {
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getSuccessMessage() {
        return null;
    }
    
    private final void setSuccessMessage(java.lang.String p0) {
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<java.lang.String> getTypeOptions() {
        return null;
    }
    
    public final void loadInitialData() {
    }
    
    private final java.lang.Object loadCategories(kotlin.coroutines.Continuation<? super kotlin.Unit> $completion) {
        return null;
    }
    
    private final java.lang.Object loadConditions(kotlin.coroutines.Continuation<? super kotlin.Unit> $completion) {
        return null;
    }
    
    private final java.lang.Object loadVendors(kotlin.coroutines.Continuation<? super kotlin.Unit> $completion) {
        return null;
    }
    
    public final void updateItemName(@org.jetbrains.annotations.NotNull()
    java.lang.String value) {
    }
    
    public final void updateDescription(@org.jetbrains.annotations.NotNull()
    java.lang.String value) {
    }
    
    public final void updateSelectedType(@org.jetbrains.annotations.NotNull()
    java.lang.String value) {
    }
    
    public final void updateModelNumber(@org.jetbrains.annotations.NotNull()
    java.lang.String value) {
    }
    
    public final void updateCurrentSerial(@org.jetbrains.annotations.NotNull()
    java.lang.String value) {
    }
    
    public final void addSerial() {
    }
    
    public final void removeSerial(@org.jetbrains.annotations.NotNull()
    java.lang.String serial) {
    }
    
    public final boolean isCellphoneCategory() {
        return false;
    }
    
    public final void addPhoneRow() {
    }
    
    public final void generatePhoneRows(@org.jetbrains.annotations.NotNull()
    java.lang.String countText) {
    }
    
    public final void removePhoneRow(int index) {
    }
    
    public final void updatePhoneRow(int index, @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.viewmodels.PhoneItemEntry value) {
    }
    
    public final boolean addPhoneSerialFromScan(@org.jetbrains.annotations.NotNull()
    java.lang.String rawSerial) {
        return false;
    }
    
    private final java.util.Set<java.lang.String> phoneCodeSet(java.lang.Integer excludeIndex) {
        return null;
    }
    
    private final java.util.List<com.example.yakultscanner.viewmodels.PhoneItemEntry> validPhoneRows() {
        return null;
    }
    
    private final java.lang.String duplicatePhoneEntryMessage(java.util.List<com.example.yakultscanner.viewmodels.PhoneItemEntry> rows) {
        return null;
    }
    
    public final void updateSelectedCategory(@org.jetbrains.annotations.Nullable()
    com.example.yakultscanner.api.ItemCategoryDto category) {
    }
    
    public final void updateSelectedCondition(@org.jetbrains.annotations.Nullable()
    com.example.yakultscanner.api.ConditionDto condition) {
    }
    
    public final void updateSelectedUnit(@org.jetbrains.annotations.NotNull()
    java.lang.String unit) {
    }
    
    public final void updateAmount(@org.jetbrains.annotations.NotNull()
    java.lang.String value) {
    }
    
    public final void updateStartDate(@org.jetbrains.annotations.NotNull()
    java.lang.String value) {
    }
    
    public final void updateEndDate(@org.jetbrains.annotations.NotNull()
    java.lang.String value) {
    }
    
    public final void updateSelectedVendor(@org.jetbrains.annotations.Nullable()
    com.example.yakultscanner.api.VendorDto vendor) {
    }
    
    public final void updateWarrantyYears(@org.jetbrains.annotations.NotNull()
    java.lang.String value) {
    }
    
    public final void updateDatePurchased(@org.jetbrains.annotations.NotNull()
    java.lang.String value) {
    }
    
    public final void updateLicenseNumber(@org.jetbrains.annotations.NotNull()
    java.lang.String value) {
    }
    
    public final void updateRemarks(@org.jetbrains.annotations.NotNull()
    java.lang.String value) {
    }
    
    private final java.lang.String formatDate(long millis) {
        return null;
    }
    
    public final void onStartDateSelected(long millis) {
    }
    
    public final void onEndDateSelected(long millis) {
    }
    
    public final void onDatePurchasedSelected(long millis) {
    }
    
    public final boolean isFormValid() {
        return false;
    }
    
    public final void clearError() {
    }
    
    public final void clearSuccess() {
    }
    
    /**
     * Validates and saves batch items
     * Returns error message if validation fails, null on success
     */
    public final void saveBatchItems(@org.jetbrains.annotations.NotNull()
    kotlin.jvm.functions.Function0<kotlin.Unit> onSuccess, @org.jetbrains.annotations.NotNull()
    kotlin.jvm.functions.Function1<? super java.lang.String, kotlin.Unit> onError) {
    }
    
    private final void resetForm() {
    }
}