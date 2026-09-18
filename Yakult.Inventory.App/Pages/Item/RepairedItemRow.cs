using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using MaterialCard = ReaLTaiizor.Controls.MaterialCard;
using HopeButton = ReaLTaiizor.Controls.HopeButton;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Forms.CallMonitoring;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models.CallMonitoring;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Pages.Item
{
        public sealed class ConditionChoice
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public override string ToString() => Name;
        }

        public sealed class RepairedItemRow : INotifyPropertyChanged
        {
            /// <summary>Moved here (was a private const on ViewRepairedItemsPage) since both the
            /// DTO's IsSpare check and the page/ViewModel's "Mark Spare" action need it.</summary>
            public const string SpareRepairAction = "Repaired - Spare inventory";

            public event PropertyChangedEventHandler PropertyChanged;

            /// <summary>Backs the grid's per-row select checkbox. Raises PropertyChanged so the
            /// header "select all" checkbox can toggle already-rendered rows.</summary>
            private bool _selected;
            public bool Selected
            {
                get => _selected;
                set
                {
                    if (_selected == value) return;
                    _selected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected)));
                }
            }

            public int ItemId { get; set; }
            public string Name { get; set; }
            public string Category { get; set; }
            public string ModelNumber { get; set; }
            public string SerialNumber { get; set; }
            public int StockOnHand { get; set; }
            public bool Active { get; set; }
            public int ConditionId { get; set; }
            public string ConditionName { get; set; }
            public int RepairCount { get; set; }
            public string LastRepairAction { get; set; }
            public DateTime? LastRepairAt { get; set; }
            public DateTime? DurationStartDate { get; set; }
            public string CurrentSetCode { get; set; }
            public string LastRepairRemark { get; set; }
            public string LastRepairProcessedByName { get; set; }
            public string LastRepairSourceRaw { get; set; }

            public bool IsDeployed => DurationStartDate.HasValue;
            public bool IsInSet => !string.IsNullOrWhiteSpace(CurrentSetCode);
            public bool IsOut => IsDeployed || IsInSet;

            public string LocationLabel
            {
                get
                {
                    if (IsDeployed) return "Deployed";
                    if (IsInSet) return "In Set";
                    return "In Stock";
                }
            }

            public bool IsSpare
            {
                get
                {
                    var action = (LastRepairAction ?? string.Empty).Trim();
                    if (action.Length == 0)
                        return false;

                    if (action.Equals(SpareRepairAction, StringComparison.OrdinalIgnoreCase))
                        return true;

                    return action.IndexOf("spare", StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }

            /// <summary>True only when the item actually has a logged repair (RepairCount > 0) AND
            /// isn't currently sitting Damaged again — i.e. it was broken and has since been fixed.
            /// Deliberately NOT just "!NeedsRepairByCondition": an item with Condition = Good that
            /// was simply never damaged has no repair to speak of, and calling it "Repaired" would
            /// be as wrong as the old "Unrepaired despite Good condition" bug this was meant to fix
            /// — it isn't repaired, it just never needed to be. Those items fall into neither bucket
            /// here; "Needs Repair" (the renamed Unrepaired card/filter, see
            /// RepairedItemsPageViewModel.Filters.cs) is driven by NeedsRepairByCondition directly
            /// instead of by !IsRepaired, so they aren't miscounted there either.</summary>
            public bool IsRepaired
            {
                get
                {
                    if (NeedsRepairByCondition)
                        return false;

                    if (RepairCount <= 0)
                        return false;

                    var action = (LastRepairAction ?? string.Empty).Trim();
                    if (action.Length == 0)
                        return true;

                    if (action.Equals("Unrepaired", StringComparison.OrdinalIgnoreCase))
                        return false;

                    // Backward-compatible fallback: any other logged action (incl. "Repaired...")
                    // on a currently-Good item counts as repaired.
                    return true;
                }
            }

            public string SpareLabel => IsSpare ? "Yes" : "No";
            public string RepairStatus => IsRepaired ? "Repaired" : "Unrepaired";

            /// <summary>True when the item's current Condition (not its repair history) indicates
            /// it needs repair. Drives the grid's "Repair" column, which is about the item's
            /// present-day health, not whether a repair action was ever logged for it (that's
            /// what IsRepaired/RepairStatus track, for the Mark Repaired/Unrepaired workflow).</summary>
            public bool NeedsRepairByCondition =>
                string.Equals((ConditionName ?? string.Empty).Trim(), "Damaged", StringComparison.OrdinalIgnoreCase);

            public string NeedsRepairLabel =>
                NeedsRepairByCondition
                    ? "Needs Repair"
                    : (string.IsNullOrWhiteSpace(ConditionName) ? "Good" : ConditionName);

            public string LastRepairOrigin
            {
                get
                {
                    var source = (LastRepairSourceRaw ?? string.Empty).Trim();
                    if (source.Length == 0)
                        return "Unknown";

                    if (source.Equals("CallMonitoring", StringComparison.OrdinalIgnoreCase))
                        return "ITCM";
                    if (source.Equals("ManualRepair", StringComparison.OrdinalIgnoreCase))
                        return "Manual";

                    if (source.IndexOf("mobile", StringComparison.OrdinalIgnoreCase) >= 0
                        || source.IndexOf("android", StringComparison.OrdinalIgnoreCase) >= 0)
                        return "Mobile";

                    return "Other";
                }
            }

            public string LastRepairRemarkOneLine
            {
                get
                {
                    var remark = (LastRepairRemark ?? string.Empty).Trim();
                    if (remark.Length == 0)
                        return null;
                    return remark.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ").Trim();
                }
            }
        }
}
