/**
 * DEV-ONLY Development Tools
 *
 * This file is only loaded in Development environment.
 * Provides SECRET shortcuts for development and testing utilities.
 *
 * SECRET SHORTCUTS:
 * - Ctrl + Shift + Click: Switch database (choose from available databases)
 */

(function () {
    'use strict';

    console.log('%c[DevTools] Development tools loaded', 'color: #4CAF50; font-weight: bold');
    console.log('%c[DevTools] SECRET: Ctrl + Shift + Click anywhere to switch database', 'color: #FF9800');

    /**
     * SECRET: Ctrl + Shift + Click handler
     */
    document.addEventListener('click', function (event) {
        // Ctrl + Shift + Click: Switch Database
        if (event.ctrlKey && event.shiftKey) {
            event.preventDefault(); // Prevent browser default behavior
            console.log('[DevTools] Secret combo detected: Ctrl + Shift + Click');
            showDatabaseSwitcher();
        }
    });

    /**
     * Shows the database switcher prompt
     */
    async function showDatabaseSwitcher() {
        console.log('[DevTools] Opening database switcher...');

        try {
            // Fetch available databases and current selection
            const response = await fetch('/DevTools/GetAvailableDatabases');
            const data = await response.json();

            const databases = data.databases;
            const currentDb = data.current;

            // Build options for prompt
            let message = '📦 DATABASE SWITCHER (DEV ONLY)\n\n';
            message += 'Select database:\n\n';
            databases.forEach((db, index) => {
                const current = db === currentDb ? ' ← CURRENT' : '';
                message += `${index + 1}. ${db}${current}\n`;
            });
            message += '\nEnter number (1-' + databases.length + '):';

            const input = prompt(message);
            if (!input) {
                console.log('[DevTools] Database switch cancelled');
                return;
            }

            const selectedIndex = parseInt(input) - 1;
            if (isNaN(selectedIndex) || selectedIndex < 0 || selectedIndex >= databases.length) {
                alert('❌ Invalid selection');
                return;
            }

            const selectedDb = databases[selectedIndex];
            if (selectedDb === currentDb) {
                alert(`ℹ️ Already using ${selectedDb}`);
                return;
            }

            // Confirm switch
            if (!confirm(`Switch database to ${selectedDb}?\n\nThis will affect all subsequent queries in this session.`)) {
                console.log('[DevTools] Database switch cancelled');
                return;
            }

            // Call backend to set database
            const setResponse = await fetch('/DevTools/SetDatabase', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ databaseName: selectedDb })
            });

            const result = await setResponse.json();

            if (result.success) {
                console.log('%c[DevTools] Database switched to: ' + selectedDb, 'color: #4CAF50; font-weight: bold');
                alert(`✅ Database Switched\n\nNow using: ${selectedDb}\n\nReload page to see changes?`);

                if (confirm('Reload page now?')) {
                    window.location.reload();
                }
            } else {
                console.error('[DevTools] Failed to switch database:', result.message);
                alert('❌ Failed to switch database\n\n' + result.message);
            }

        } catch (error) {
            console.error('[DevTools] Error in database switcher:', error);
            alert('❌ Error\n\nFailed to load database switcher.\nCheck console for details.');
        }
    }

    /**
     * Ping the DevTools controller to verify it's available
     */
    async function pingDevTools() {
        try {
            const response = await fetch('/DevTools/Ping');
            const result = await response.json();
            console.log('[DevTools] Controller ping:', result);

            // Also fetch and log current database
            const dbResponse = await fetch('/DevTools/GetCurrentDatabase');
            const dbResult = await dbResponse.json();
            console.log('%c[DevTools] Current database: ' + dbResult.database, 'color: #FF9800; font-weight: bold');
        } catch (error) {
            console.error('[DevTools] Controller ping failed:', error);
        }
    }

    // Verify DevTools controller is available
    pingDevTools();

})();
