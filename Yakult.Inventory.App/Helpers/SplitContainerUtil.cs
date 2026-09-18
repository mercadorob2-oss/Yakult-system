using System;
using System.Windows.Forms;

namespace Yakult.Inventory.App.Helpers
{
    public static class SplitContainerUtil
    {
        public static void SetSafeSplitterDistance(SplitContainer split, int desiredDistance)
        {
            if (split == null || split.IsDisposed)
                return;

            // If the control isn't laid out yet, Width/Height can be 0. Let a later call handle it.
            var total = split.Orientation == Orientation.Vertical ? split.Width : split.Height;
            if (total <= 0)
                return;

            var maxLeft = Math.Max(0, (total - split.SplitterWidth) - split.Panel2MinSize);
            var minLeft = Math.Min(split.Panel1MinSize, maxLeft);
            var safe = Math.Max(minLeft, Math.Min(desiredDistance, maxLeft));

            try
            {
                split.SplitterDistance = safe;
            }
            catch
            {
                // Best-effort: never crash UI due to splitter constraints.
            }
        }

        public static void BindSafeSplitterDistance(SplitContainer split, Func<int> desiredDistance)
        {
            if (split == null)
                throw new ArgumentNullException(nameof(split));
            if (desiredDistance == null)
                throw new ArgumentNullException(nameof(desiredDistance));

            void Apply()
            {
                try { SetSafeSplitterDistance(split, desiredDistance()); }
                catch { }
            }

            split.HandleCreated += (_, __) => Apply();
            split.SizeChanged += (_, __) => Apply();
            split.VisibleChanged += (_, __) =>
            {
                if (split.Visible)
                    Apply();
            };
        }
    }
}

