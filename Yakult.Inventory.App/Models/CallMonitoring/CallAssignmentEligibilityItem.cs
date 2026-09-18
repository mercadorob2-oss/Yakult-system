using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Yakult.Inventory.App.Models.CallMonitoring
{
    public sealed class CallAssignmentEligibilityItem : INotifyPropertyChanged
    {
        private bool _isAssignmentEligible;
        private bool _isEscalationEligible;
        private bool _isDefaultAutoEscalation;

        public int EmpId { get; set; }
        public string EmployeeName { get; set; }
        public string Position { get; set; }
        public int? UserId { get; set; }
        public string UserName { get; set; }
        public string RoleNames { get; set; }
        public bool HasAllowedRole { get; set; }
        public bool HasStoredEligibilitySettings { get; set; }

        public bool IsAssignmentEligible
        {
            get => _isAssignmentEligible;
            set
            {
                if (_isAssignmentEligible == value) return;
                _isAssignmentEligible = value;
                OnPropertyChanged();
            }
        }

        public bool IsEscalationEligible
        {
            get => _isEscalationEligible;
            set
            {
                if (_isEscalationEligible == value) return;
                _isEscalationEligible = value;
                OnPropertyChanged();
            }
        }

        public bool IsDefaultAutoEscalation
        {
            get => _isDefaultAutoEscalation;
            set
            {
                if (_isDefaultAutoEscalation == value) return;
                _isDefaultAutoEscalation = value;
                OnPropertyChanged();
            }
        }

        public bool HasActiveAccount => UserId.HasValue && UserId.Value > 0;

        public bool HasEligiblePosition => MatchesAllowedPosition(Position);

        public bool HasAllowedAccess => HasAllowedRole || HasEligiblePosition;

        public bool CanBeAssigned => HasActiveAccount && HasAllowedAccess;

        public string AllowedAccessDisplay
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(RoleNames))
                    return RoleNames.Trim();

                if (HasEligiblePosition)
                    return $"Position: {(Position ?? string.Empty).Trim()}";

                return string.Empty;
            }
        }

        public string DisplayUserName => string.IsNullOrWhiteSpace(UserName) ? "No active account" : UserName.Trim();

        public string EligibilityStatus
        {
            get
            {
                if (!HasActiveAccount)
                    return "No active account";

                if (!HasAllowedAccess)
                    return "Missing allowed role";

                return "Ready";
            }
        }

        public static bool MatchesAllowedPosition(string position)
        {
            if (string.IsNullOrWhiteSpace(position))
                return false;

            var normalized = NormalizePosition(position);
            if (normalized.Length == 0)
                return false;

            if (normalized.Contains("SUPERVISOR") || normalized.Contains("PROGRAMMER") || normalized.Contains("DEVELOPER"))
                return true;

            if (normalized.Contains("TECH SUPPORT") || normalized.Contains("TECHNICALL SUPPORT"))
                return true;

            var hasItContext = normalized.Contains(" IT ")
                || normalized.StartsWith("IT ")
                || normalized.EndsWith(" IT")
                || normalized.Contains(" I T ")
                || normalized.StartsWith("I T ")
                || normalized.EndsWith(" I T");

            return hasItContext && (
                normalized.Contains("SUPPORT")
                || normalized.Contains("MANAGER")
                || normalized.Contains("TECHNICALL")
                || normalized.Contains("SPECIAL")
                || normalized.Contains("SECURITY"));
        }

        private static string NormalizePosition(string position)
        {
            var chars = (position ?? string.Empty)
                .ToUpperInvariant()
                .Select(ch => char.IsLetterOrDigit(ch) ? ch : ' ')
                .ToArray();
            return " " + string.Join(" ", new string(chars).Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries)) + " ";
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
