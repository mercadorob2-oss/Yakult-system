using System;
using System.Net.Mail;

namespace Yakult.Inventory.App.Helpers
{
    public static class EmailAddressValidator
    {
        public static bool TryNormalize(string value, out string normalized)
        {
            normalized = null;
            var candidate = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(candidate))
                return false;

            try
            {
                var address = new MailAddress(candidate);
                if (!string.Equals(address.Address, candidate, StringComparison.OrdinalIgnoreCase))
                    return false;

                normalized = address.Address;
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        public static bool IsValid(string value)
        {
            return TryNormalize(value, out _);
        }
    }
}
