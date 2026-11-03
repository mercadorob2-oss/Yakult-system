(function () {
    'use strict';

    window.yakultToast = function (message, type) {
        const region = document.getElementById('toastRegion');
        if (!region || !message) return;
        const toast = document.createElement('div');
        toast.className = 'portal-toast portal-toast--' + (['info', 'success', 'warning', 'error'].includes(type) ? type : 'info');
        const dot = document.createElement('span');
        dot.className = 'portal-toast__dot';
        const label = document.createElement('span');
        label.textContent = String(message);
        toast.append(dot, label);
        region.appendChild(toast);
        window.setTimeout(function () {
            toast.classList.add('is-leaving');
            window.setTimeout(function () { toast.remove(); }, 220);
        }, 3600);
    };

    function initializeShell() {
        const root = document.documentElement;
        const body = document.body;
        const sidebar = document.getElementById('portalAdminSidebar');
        const frame = document.getElementById('adminWorkspaceFrame');
        const toggle = document.getElementById('adminMenuButton');
        const close = document.getElementById('adminSidebarClose');
        const backdrop = document.getElementById('adminSidebarBackdrop');
        const account = document.getElementById('adminPortalDropdown');
        const tooltip = document.getElementById('adminNavTooltip');
        if (!sidebar || !frame || !toggle || !close || !backdrop) return;
        const mobile = window.matchMedia('(max-width: 960px)');
        let drawerOpen = false;
        let tooltipOwner = null;
        let focusBeforeDrawer = null;

        function hideTooltip() {
            if (tooltip) tooltip.hidden = true;
            if (tooltipOwner) tooltipOwner.removeAttribute('aria-describedby');
            tooltipOwner = null;
        }

        function synchronize() {
            const drawer = mobile.matches;
            const expanded = drawer ? drawerOpen : !root.classList.contains('admin-nav-collapsed');
            body.classList.toggle('is-admin-nav-open', drawer && drawerOpen);
            toggle.setAttribute('aria-expanded', String(expanded));
            toggle.setAttribute('aria-label', drawer ? (expanded ? 'Close navigation' : 'Open navigation') : (expanded ? 'Collapse sidebar' : 'Expand sidebar'));
            backdrop.hidden = !(drawer && drawerOpen);
            sidebar.inert = drawer && !drawerOpen;
            frame.inert = drawer && drawerOpen;
            if (drawer && drawerOpen) {
                sidebar.setAttribute('role', 'dialog');
                sidebar.setAttribute('aria-modal', 'true');
            } else {
                sidebar.removeAttribute('role');
                sidebar.removeAttribute('aria-modal');
            }
            if (drawer && !drawerOpen) sidebar.setAttribute('aria-hidden', 'true');
            else sidebar.removeAttribute('aria-hidden');
            hideTooltip();
        }

        function setDrawer(open, restoreFocus) {
            drawerOpen = mobile.matches && open;
            if (drawerOpen) {
                focusBeforeDrawer = document.activeElement;
                if (account) account.open = false;
            }
            // Remove inert from the destination before transferring keyboard focus.
            if (drawerOpen) {
                sidebar.inert = false;
                sidebar.removeAttribute('aria-hidden');
                body.classList.add('is-admin-nav-open');
            } else {
                frame.inert = false;
            }
            if (!drawerOpen && restoreFocus) (focusBeforeDrawer || toggle).focus();
            synchronize();
            if (drawerOpen) window.requestAnimationFrame(function () {
                if (drawerOpen) (sidebar.querySelector('[aria-current="page"]') || close).focus();
            });
        }

        toggle.addEventListener('click', function () {
            if (mobile.matches) {
                setDrawer(!drawerOpen, true);
                return;
            }
            const collapsed = root.classList.toggle('admin-nav-collapsed');
            try { localStorage.setItem('yakult.admin.sidebar.collapsed', String(collapsed)); } catch (_) { }
            synchronize();
        });

        close.addEventListener('click', function () { setDrawer(false, true); });
        backdrop.addEventListener('click', function () { setDrawer(false, true); });
        sidebar.querySelectorAll('a[href]').forEach(function (link) {
            link.addEventListener('click', function (event) {
                if (mobile.matches && drawerOpen && !event.ctrlKey && !event.metaKey && !event.shiftKey && !event.altKey) setDrawer(false, true);
            });
        });

        document.addEventListener('keydown', function (event) {
            if (event.key === 'Escape') {
                hideTooltip();
                if (drawerOpen) { event.preventDefault(); setDrawer(false, true); }
                else if (account?.open) { account.open = false; account.querySelector('summary').focus(); }
                return;
            }
            if (event.key !== 'Tab' || !drawerOpen) return;
            const targets = Array.from(sidebar.querySelectorAll('a[href], button:not([disabled]), [tabindex="0"]')).filter(element => !element.closest('[hidden]'));
            if (!targets.length) { event.preventDefault(); sidebar.focus(); return; }
            const first = targets[0];
            const last = targets[targets.length - 1];
            if (event.shiftKey && (document.activeElement === first || !sidebar.contains(document.activeElement))) {
                event.preventDefault(); last.focus();
            } else if (!event.shiftKey && (document.activeElement === last || !sidebar.contains(document.activeElement))) {
                event.preventDefault(); first.focus();
            }
        });

        document.addEventListener('focusin', function (event) {
            if (drawerOpen && !sidebar.contains(event.target)) close.focus();
        });
        document.addEventListener('click', function (event) {
            if (account?.open && !account.contains(event.target)) account.open = false;
        });
        document.addEventListener('focusin', function (event) {
            if (account?.open && !account.contains(event.target)) account.open = false;
        });

        sidebar.querySelectorAll('[data-nav-label]').forEach(function (link) {
            function showTooltip() {
                if (!tooltip || mobile.matches || !root.classList.contains('admin-nav-collapsed')) return;
                hideTooltip();
                tooltip.textContent = link.dataset.navLabel;
                const bounds = link.getBoundingClientRect();
                tooltip.style.left = (sidebar.getBoundingClientRect().right + 10) + 'px';
                tooltip.hidden = false;
                tooltip.style.top = Math.max(8, Math.min(bounds.top + (bounds.height - tooltip.offsetHeight) / 2, window.innerHeight - tooltip.offsetHeight - 8)) + 'px';
                link.setAttribute('aria-describedby', tooltip.id);
                tooltipOwner = link;
            }
            link.addEventListener('pointerenter', showTooltip);
            link.addEventListener('focus', showTooltip);
            link.addEventListener('pointerleave', hideTooltip);
            link.addEventListener('blur', hideTooltip);
        });
        sidebar.querySelector('nav')?.addEventListener('scroll', hideTooltip, { passive: true });
        window.addEventListener('resize', hideTooltip, { passive: true });
        mobile.addEventListener('change', function () {
            const focusInSidebar = sidebar.contains(document.activeElement);
            drawerOpen = false;
            frame.inert = false;
            if (mobile.matches && focusInSidebar) toggle.focus();
            synchronize();
        });
        synchronize();
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', initializeShell, { once: true });
    else initializeShell();
})();
