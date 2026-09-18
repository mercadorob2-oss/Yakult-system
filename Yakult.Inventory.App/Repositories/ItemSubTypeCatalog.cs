using System;

namespace Yakult.Inventory.App.Repositories
{
    /// <summary>
    /// The shared list of item/group Sub-Types (Contract/Subscription/License/Services) —
    /// used by dbo.Item.SubType (BatchAddItemDialog/EditItemDialog) and dbo.SetItem's
    /// per-group Sub-Type (InvoicePreparationRepository's Sub-Type Groups). Sub-Type is
    /// only ever a per-item/per-group attribute — an invoice/Set itself does not have one.
    /// </summary>
    public static class ItemSubTypeCatalog
    {
        public static readonly string[] ValidSubTypes = { "Contract", "Subscription", "License", "Services" };

        /// <summary>
        /// Sub-Type is optional. This only rejects a non-blank value that isn't one of
        /// the four recognized sub-types — it does not require a value to be present.
        /// </summary>
        public static void ValidateSubType(string subType)
        {
            if (!string.IsNullOrWhiteSpace(subType) && Array.IndexOf(ValidSubTypes, subType) < 0)
            {
                throw new InvalidOperationException("Sub-Type must be one of: Contract, Subscription, License, Services.");
            }
        }

        /// <summary>The reference-code field's display label for a given Sub-Type — kept in
        /// one place so every picker (AssignSubTypeGroupDialog, ViewInvoiceDetailPage's group
        /// cards, BatchAddInvoiceItemsDialog) calls the same field the same thing.</summary>
        public static string GetReferenceCodeLabel(string subType) =>
            subType == "Subscription" ? "Subscription ID"
            : subType == "Contract" ? "Contract Code"
            : subType == "License" ? "License ID"
            : subType == "Services" ? "Service ID"
            : "Reference Code";
    }
}
