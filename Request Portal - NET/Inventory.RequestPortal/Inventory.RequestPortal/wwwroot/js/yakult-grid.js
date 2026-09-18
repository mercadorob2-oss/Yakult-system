/*
 * YakultGrid — shared AG Grid Community wrapper.
 *
 * Usage (call from inside @section Scripts so agGrid has already loaded):
 *
 *   var grid = YakultGrid.create('myGridContainer', {
 *       columnDefs: [ { field: 'name', headerName: 'Name' }, ... ],
 *       rowData: [...],
 *       onSelectionChanged: function (row) { ... },   // row is the selected data object, or null
 *       onRowDoubleClicked: function (row) { ... }
 *   });
 *
 *   grid.search('foo');                 // quick-filter across all columns
 *   grid.api.setGridOption('rowData', newRows);   // reload data
 *
 * Every grid gets, by default: pagination, sortable + resizable + reorderable
 * columns, per-column filters via an always-visible floating filter row (the
 * funnel icon for AG Grid's full multi-condition filter menu is suppressed —
 * just the quick single-condition text/number box), single-row selection, a
 * loading overlay, and an empty-state overlay — so page-specific code only
 * needs to supply columnDefs + rowData.
 *
 * Per-column filter type defaults to the text filter; set filter: 'agNumberColumnFilter'
 * or 'agDateColumnFilter' on a column that needs it (see History.cshtml's reqId/qty cols).
 */
window.YakultGrid = (function () {
    'use strict';

    function create(containerId, options) {
        options = options || {};
        var container = document.getElementById(containerId);
        if (!container) {
            console.error('YakultGrid.create: no element with id "' + containerId + '"');
            return null;
        }

        container.classList.add('ag-theme-alpine', 'ag-theme-yakult');

        var defaultColDef = Object.assign({
            sortable: true,
            resizable: true,
            filter: true,
            floatingFilter: true,
            suppressFilterButton: true,
            flex: 1,
            minWidth: 100
        }, options.defaultColDef || {});

        var gridOptions = Object.assign({
            columnDefs: options.columnDefs || [],
            rowData: options.rowData || [],
            defaultColDef: defaultColDef,
            pagination: true,
            paginationPageSize: options.pageSize || 10,
            paginationPageSizeSelector: false,
            rowSelection: 'single',
            suppressCellFocus: true,
            animateRows: true,
            domLayout: options.domLayout || 'normal',
            overlayLoadingTemplate: '<span class="yg-overlay">Loading…</span>',
            overlayNoRowsTemplate: '<span class="yg-overlay">' + (options.noRowsMessage || 'No records found.') + '</span>',
            onSelectionChanged: function (params) {
                if (!options.onSelectionChanged) return;
                var rows = params.api.getSelectedRows();
                options.onSelectionChanged(rows.length ? rows[0] : null);
            },
            onRowDoubleClicked: function (params) {
                if (options.onRowDoubleClicked) options.onRowDoubleClicked(params.data);
            }
        }, options.gridOptions || {});

        var api = agGrid.createGrid(container, gridOptions);

        return {
            api: api,
            search: function (text) {
                api.setGridOption('quickFilterText', text || '');
            },
            getSelected: function () {
                var rows = api.getSelectedRows();
                return rows.length ? rows[0] : null;
            },
            reload: function (rowData) {
                api.setGridOption('rowData', rowData || []);
            }
        };
    }

    return { create: create };
})();
