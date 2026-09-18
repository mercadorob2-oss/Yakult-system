using System;
using System.Configuration;

namespace Yakult.Inventory.App.Core
{
    /// <summary>
    /// Configuration constants for the application
    /// </summary>
    public static class AppConfig
    {
        // Connection String Key
        public const string ConnectionStringKey = "Yakult.Inventory.App.Properties.Settings.Yakult_Inventory_SystemConnectionString";

        // ITCM Server (centralized scheduler dashboard)
        private static string _itcmServerUrl;

        public static string ItcmServerUrl
        {
            get
            {
                if (string.IsNullOrEmpty(_itcmServerUrl))
                {
                    _itcmServerUrl = ConfigurationManager.AppSettings["ItcmServerUrl"] ?? "http://localhost:50330";
                }
                return _itcmServerUrl;
            }
        }

        // ITCM scheduler ownership. When true (default), Yakult.ITCM.Server is
        // the single scheduler owner and the in-app fallback timers stay off.
        // Set ItcmServerOwned=false in App.config only as a temporary fallback
        // if the server is down.
        private static bool? _itcmServerOwned;

        public static bool ItcmServerOwned
        {
            get
            {
                if (!_itcmServerOwned.HasValue)
                {
                    var raw = ConfigurationManager.AppSettings["ItcmServerOwned"];
                    if (string.IsNullOrWhiteSpace(raw))
                        _itcmServerOwned = true;
                    else if (bool.TryParse(raw.Trim(), out var parsed))
                        _itcmServerOwned = parsed;
                    else
                        _itcmServerOwned = !(raw.Trim() == "0"
                            || raw.Trim().Equals("no", StringComparison.OrdinalIgnoreCase));
                }
                return _itcmServerOwned.Value;
            }
        }

        // API Configuration
        private static string _apiBaseUrl;

        /// <summary>
        /// Gets the API base URL from App.config (cached)
        /// Example: "http://192.168.27.174:5001"
        /// </summary>
        public static string ApiBaseUrl
        {
            get
            {
                if (string.IsNullOrEmpty(_apiBaseUrl))
                {
                    _apiBaseUrl = ConfigurationManager.AppSettings["ApiBaseUrl"] ?? "http://localhost:5001";
                }
                return _apiBaseUrl;
            }
        }

        /// <summary>
        /// Persists a new API base URL to the running exe.config and refreshes the in-memory cache
        /// so all subsequent calls to ApiBaseUrl immediately use the new value.
        /// </summary>
        public static void SetApiBaseUrl(string newUrl)
        {
            if (string.IsNullOrWhiteSpace(newUrl))
                throw new ArgumentException("URL cannot be empty.", nameof(newUrl));

            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            if (config.AppSettings.Settings["ApiBaseUrl"] != null)
                config.AppSettings.Settings["ApiBaseUrl"].Value = newUrl.TrimEnd('/');
            else
                config.AppSettings.Settings.Add("ApiBaseUrl", newUrl.TrimEnd('/'));

            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");

            // Bust the in-memory cache so AppBaseUrl re-reads the new value
            _apiBaseUrl = null;
        }

        // Approver App API (replaces the old raw-TCP ApprovalListener)
        private static string _approverApiBaseUrl;

        /// <summary>
        /// Base URL of the IIS-hosted Approver API (Yakult.Approver.Api), used
        /// both by DevicePairingForm.cs to build the pairing QR payload and,
        /// previously, by the retired ApprovalListener. Example:
        /// "http://192.168.100.186:7020"
        /// </summary>
        public static string ApproverApiBaseUrl
        {
            get
            {
                if (string.IsNullOrEmpty(_approverApiBaseUrl))
                {
                    _approverApiBaseUrl = ConfigurationManager.AppSettings["ApproverApiBaseUrl"] ?? "http://localhost:7020";
                }
                return _approverApiBaseUrl;
            }
        }

        /// <summary>
        /// Gets the API health/pending count URL
        /// </summary>
        public static string ApiHealthUrl => $"{ApiBaseUrl}/api/SetUpdates/PendingCount";

        /// <summary>
        /// Gets the API URL for fetching last mobile serial (legacy)
        /// </summary>
        public static string ApiGetLastMobileSerialUrl => $"{ApiBaseUrl}/api/Items/GetLastMobileSerial";

        /// <summary>
        /// Phase 1: Claim the pending serial queue atomically. Returns claimToken + serials.
        /// </summary>
        public static string ApiClaimMobileSerialsUrl => $"{ApiBaseUrl}/mobile-claim.ashx";

        /// <summary>
        /// Phase 2a: Confirm claim — user accepted the preview dialog. Deletes the claim file.
        /// </summary>
        public static string ApiConfirmMobileSerialClaimUrl(string token) =>
            $"{ApiBaseUrl}/mobile-confirm.ashx?token={Uri.EscapeDataString(token ?? string.Empty)}";

        /// <summary>
        /// Phase 2b: Cancel claim — user dismissed the dialog. Serials are restored to queue.
        /// </summary>
        public static string ApiCancelMobileSerialClaimUrl(string token) =>
            $"{ApiBaseUrl}/mobile-cancel.ashx?token={Uri.EscapeDataString(token ?? string.Empty)}";

        // Validation Messages
        public const string ValidationTitle = "Validation";
        public const string ErrorTitle = "Error";
        public const string SuccessTitle = "Success";
        public const string ConfirmTitle = "Confirm";

        // Common Messages
        public const string SaveSuccessMessage = "✅ {0} saved successfully.";
        public const string SaveFailedMessage = "❌ Save failed: {0}";
        public const string ConnectionStringNotFound = "Connection string not found.";
        public const string CancelConfirmation = "Are you sure you want to cancel?";
        
        // Validation Messages
        public const string RequiredFieldMessage = "Please enter {0}.";
        public const string RequiredSelectionMessage = "Please select {0}.";
    }
}
