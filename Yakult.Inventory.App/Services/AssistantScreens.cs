using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using WinForms=global::System.Windows.Forms;
using WpfControls=global::System.Windows.Controls;

namespace Yakult.Inventory.App.Services
{
    public sealed class AssistantScreen
    {
        public string Module { get; internal set; }
        public string PageKey { get; internal set; }
        public string Title { get; internal set; }
        public string Capability { get; internal set; }
        public bool IsEditor { get; internal set; }
        internal string[] Types;
    }
    internal sealed class AssistantSelection
    {
        internal string Page;
        internal string Capability;
        internal long? RecordId;
    }
    public static class AssistantScreens
    {
        public static IReadOnlyList<AssistantScreen> All { get; }=Build().AsReadOnly();
        private static List<AssistantScreen> Build()
        {
            var result=new List<AssistantScreen>();
            Action<string,string,string,string,string> add=(module,page,title,cap,types)=>result.Add(new AssistantScreen {
                Module=module,PageKey=page,Title=title,Capability=cap,Types=(page+"|"+types).Split('|'),IsEditor=page.StartsWith("Add",StringComparison.Ordinal) });
            add("Inventory","ViewItemsPage","Items","items","ItemsPage|ViewItemsWpfHost");
            add("Inventory","ViewInventoryPage","Inventory","inventory","InventoryPage|ViewInventoryWpfHost");
            add("Inventory","ViewAssetPage","Assets","assets","AssetsPage|ViewAssetWpfHost");
            add("Inventory","ViewFixedAssetsPage","Fixed Assets","assets","FixedAssetsPage|ViewFixedAssetsWpfHost");
            add("Inventory","ViewWarrantyPage","Warranty","warranties","WarrantyPage|ViewWarrantyWpfHost");
            add("Inventory","ViewEmployeesPage","Employees","employees","ViewEmployeePage|EmployeesPage|ViewEmployeesWpfHost");
            add("Inventory","ViewCompaniesPage","Companies","companies","CompaniesPage|ViewCompaniesWpfHost");
            add("Inventory","ViewBranchesPage","Branches","branches","BranchesPage|ViewBranchesWpfHost");
            add("Inventory","ViewDepartmentsPage","Departments","departments","DepartmentsPage|ViewDepartmentsWpfHost");
            add("Inventory","ViewVendorsPage","Vendors","vendors","VendorsPage|ViewVendorsWpfHost");
            add("Inventory","ViewSetsPage","Sets","sets","SetsPage|ViewSetsWpfHost|ViewSetDetailPage");
            add("Inventory","ViewArchivePage","Archive","archive","ArchivePage|ViewArchiveWpfHost");
            add("Inventory","ViewItemMovementAuditPage","Item Movement Audit","item-history","ItemMovementAuditPage|ItemMovementAuditWpfHost");
            add("Inventory","ViewOwnershipHistoryPage","Ownership History","ownership-history","OwnershipHistoryPage|OwnershipHistoryWpfHost|OwnershipHistoryPageView");
            add("Inventory","ViewRepairedItemsPage","Repaired Items","item-repairs","RepairedItemsPage|ViewRepairedItemsWpfHost");
            add("Inventory","ViewCategoriesPage","Item Categories","categories","CategoriesPage|ViewCategoriesWpfHost");
            add("Inventory","ViewUpdatesPage","Set Item Updates","set-updates","UpdatesPage|ViewUpdatesWpfHost");
            add("Inventory","SetDispatchNotificationPage","Set Dispatch Notifications","sets","");
            add("Requests","ViewRequestsPage","Requests","requests","RequestsPage|ViewRequestsWpfHost");
            add("Requests","PartiallyFulfilledRequestsPage","Partially Fulfilled Requests","requests","PartiallyFulfilledRequestsWpfHost");
            add("Requests","UnfulfilledRequestsPage","Unfulfilled Requests","requests","UnfulfilledRequestsWpfHost");
            add("Requests","DepartmentRequestHistoryPage","Department Request History","requests","DepartmentRequestHistoryWpfHost");
            add("Consumables","ConsumableModelsPage","Consumable Models","consumables","ConsumableModelsWpfHost");
            add("Consumables","ViewCartridgeModelsPage","Cartridge Models","cartridges","ViewCartridgeModelsWpfHost");
            add("Consumables","ViewCartridgeRequestsPage","Cartridge Requests","exchanges","");
            add("Consumables","UnfulfilledCartridgeExchangesPage","Unfulfilled Exchanges","exchanges","");
            add("Consumables","VendorCartridgeRefillPage","Vendor Refill Batches","refills","");
            add("Consumables","ViewCartridgeTrackingPage","Cartridge Tracking","refills","");
            add("Reports","ViewInvoicePage","Invoices","invoices","InvoicePage|ViewInvoicesWpfHost|ViewInvoiceDetailPage");
            add("Reports","ViewInvoicePreparationPage","Invoice Sub Groups","invoice-preparation","InvoicePreparationListView|InvoicePreparationWpfHost");
            add("Reports","InvoiceLicenseReviewPage","Invoice License Review","license-review","InvoiceLicenseReviewWpfHost|ViewInvoiceLicenseReviewPage");
            add("Reports","NonLicensedInvoicesPage","Non-Licensed Invoices","nonlicensed","NonLicensedInvoicesWpfHost|ViewNonLicensedInvoicesPage");
            add("Reports","ViewReceiptsPage","Receipts","receipts","ReceiptsPage|ViewReceiptsWpfHost");
            add("Reports","ViewRenewalPage","Renewals","renewals","RenewalPage|ViewRenewalsWpfHost");
            add("Reports","ViewRenewalGroupPage","Renewal Groups","renewals","RenewalGroupPage|ViewRenewalGroupsWpfHost");
            add("Borrowing","BorrowItemsDashboard","Borrow Items","borrows","");
            add("Tickets","WpfCallMonitoringWorkspace","IT Call Monitoring","tickets","CallMonitoringDashboard|TicketListView");
            add("Tickets","WpfCallMonitoringProfilesHostControl","Employee Profiles","employees","");
            add("Tickets","WpfEmailLogView","Ticket Email Logs","ticket-email","EmailLogView");
            add("Repairs","RepairTicketListView","Repair Tickets","repairs","RepairTicketDetailView");
            add("Repairs","RepairPartsView","Repair Parts","repair-parts","");
            add("Admin","UserAccountManagementPage","Account Management","accounts","UserAccountManagementWpfHost|UserAccountManagementView");
            add("Admin","AccountPermissionsPage","Approver Management","approver-titles","ApproverManagementWpfHost|ApproverManagementView");
            add("Admin","UserActivityPage","User Activity","audit","UserActivityWpfHost");
            add("Admin","RoleManagementPage","Roles","roles","");
            add("Admin","EmployeeManagementPage","Employee Management","employees","EmployeeManagementWpfHost|EmployeeManagementView");
            add("Admin","DepartmentAccountsPage","Department Accounts","department-accounts","DepartmentAccountsWpfHost|DepartmentAccountsView");
            add("Admin","UserPositionsPage","User Positions","user-positions","UserPositionsWpfHost|UserPositionsView");
            add("Admin","PortalAccessPage","Portal Access","role-portals","");
            add("Admin","UserPortalAccessPage","User Portal Access","user-portals","UserAccessWpfHost|UserAccessView");
            add("Admin","HolidayManagementPage","Holidays","holidays","HolidayManagementWpfHost|HolidayManagementView");
            add("Admin","BranchAcronymPage","Branch Acronyms","branch-acronyms","");
            add("Admin","DepartmentAcronymPage","Department Acronyms","department-acronyms","");
            add("Admin","SmtpSettingsPage","SMTP Settings",null,"");
            add("Admin","EmailConfigurationPage","Email Configuration",null,"");
            add("Admin","EmailLogsPage","Email Logs","system-email","EmailLogsWpfHost|EmailLogsView");
            add("Admin","ConsumableStockMonitorPage","Consumable Stock Monitor","consumables","ConsumableStockMonitorWpfHost|ConsumableStockMonitorView");
            add("Admin","ConnectedDevicesPage","Connected Devices",null,"ConnectedDevicesWpfHost|ConnectedDevicesView");
            add("Admin","MyAccountPage","My Account",null,"");
            add("Admin","QrImageBackfillPage","QR Image Backfill",null,"");
            add("Admin","ReceiptBackfillPage","Receipt Backfill",null,"");
            foreach(string pair in new[] {"AddItemPage|items","AddEmployeePage|employees","AddRequestPage|requests","AddCompanyDialog|companies","AddBranchDialog|branches","AddDepartmentDialog|departments","AddVendorDialog|vendors","AddSalesInvoiceSet|invoices","AddRequestSet|sets"})
            { var parts=pair.Split('|');add("Inventory",parts[0],parts[0],parts[1],""); }
            return result;
        }
        internal static AssistantScreen Match(string name)
        { return All.FirstOrDefault(screen=>screen.Types.Any(t=>t.Length>0 && string.Equals(t,name,StringComparison.Ordinal))); }
        internal static string PrimaryKey(string capability)
        {
            switch(capability)
            {
                case "items":case "warranties":return "ItemId";
                case "inventory":return "InvId";
                case "assets":return "AssetId";
                case "employees":return "EmpId";
                case "sets":case "invoices":return "SetId";
                case "requests":return "ReqId";
                case "tickets":return "TicketId";
                case "repairs":return "RepairTicketId";
                case "repair-parts":return "RepairPartId";
                case "borrows":return "BorrowId";
                case "renewals":return "RenewalId";
                case "cartridges":return "CartridgeModelId";
                case "consumables":return "ConsumableModelId";
                case "refills":return "BatchId";
                case "accounts":return "UserId";
                case "item-repairs":return "RepairId";
                case "set-updates":return "UpdateId";
                case "invoice-preparation":return "PreparationId";
                case "department-accounts":return "Id";
                case "companies":return "ComId";
                case "branches":case "branch-acronyms":return "BranchId";
                case "departments":case "department-acronyms":return "DeptId";
                case "categories":return "CategoryId";
                case "conditions":return "ConditionID";
                case "vendors":return "VendorID";
                case "holidays":return "HolidayId";
                case "receipts":return "ReceiptSetId";
                case "system-email":return "LogId";
                case "ticket-email":return "EmailLogId";
                case "approvals":return "AuthorizationId";
                case "exchanges":return "UnfulfilledId";
                case "approver-titles":return "TitleId";
                default:return null;
            }
        }
        internal static long? SavedId(object item,string capability)
        {
            string key=PrimaryKey(capability);if(item==null || key==null) return null;
            try
            {
                object value=null;
                var row=item as DataRowView;
                if(row!=null && row.DataView.Table.Columns.Contains(key)) value=row[key];
                else
                {
                    // Read only an allowlisted numeric key. No arbitrary properties, screen text, or form inputs are inspected.
                    var property=item.GetType().GetProperty(key,BindingFlags.Instance|BindingFlags.Public);
                    if(property!=null && property.GetIndexParameters().Length==0) value=property.GetValue(item,null);
                }
                long id;return value!=null && value!=DBNull.Value && long.TryParse(Convert.ToString(value,System.Globalization.CultureInfo.InvariantCulture),out id) && id>0?(long?)id:null;
            }
            catch { return null; }
        }
        internal static AssistantSelection Resolve(object root)
        {
            var selection=new AssistantSelection();
            var candidates=new List<Tuple<AssistantScreen,object>>();
            var queue=new Queue<object>();queue.Enqueue(root);int visited=0;
            while(queue.Count>0 && visited++<500)
            {
                object current=queue.Dequeue();var screen=Match(current.GetType().Name);
                if(screen!=null) candidates.Add(Tuple.Create(screen,current));
                var formControl=current as WinForms.Control;
                if(formControl!=null)
                {
                    foreach(WinForms.Control child in formControl.Controls) if(child.Visible) queue.Enqueue(child);
                    var elementHost=current as System.Windows.Forms.Integration.ElementHost;
                    if(elementHost!=null && elementHost.Child!=null) queue.Enqueue(elementHost.Child);
                }
                var visual=current as Visual;
                if(visual!=null)
                    for(int i=0;i<VisualTreeHelper.GetChildrenCount(visual);i++)
                    { var child=VisualTreeHelper.GetChild(visual,i) as UIElement;if(child!=null && child.IsVisible) queue.Enqueue(child); }
            }
            var found=candidates.LastOrDefault();if(found==null) return selection;
            selection.Page=found.Item1.PageKey;selection.Capability=found.Item1.IsEditor?null:found.Item1.Capability;
            if(selection.Capability==null) return selection;
            queue.Clear();queue.Enqueue(found.Item2);visited=0;
            while(queue.Count>0 && visited++<250)
            {
                object current=queue.Dequeue();
                var wpfGrid=current as WpfControls.DataGrid;
                if(wpfGrid!=null && wpfGrid.SelectedItem!=null) {var id=SavedId(wpfGrid.SelectedItem,selection.Capability);if(id.HasValue) {selection.RecordId=id;break;} }
                var formsGrid=current as WinForms.DataGridView;
                if(formsGrid!=null && formsGrid.CurrentRow!=null && !formsGrid.CurrentRow.IsNewRow)
                { var id=SavedId(formsGrid.CurrentRow.DataBoundItem,selection.Capability);if(id.HasValue) {selection.RecordId=id;break;} }
                var control=current as WinForms.Control;
                if(control!=null) foreach(WinForms.Control child in control.Controls) if(child.Visible) queue.Enqueue(child);
                var host=current as System.Windows.Forms.Integration.ElementHost;if(host!=null && host.Child!=null) queue.Enqueue(host.Child);
                var visual=current as Visual;
                if(visual!=null) for(int i=0;i<VisualTreeHelper.GetChildrenCount(visual);i++) queue.Enqueue(VisualTreeHelper.GetChild(visual,i));
            }
            return selection;
        }
    }
}
