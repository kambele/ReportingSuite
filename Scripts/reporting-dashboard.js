(function (window, document, $) {
    'use strict';

    if (!$) {
        return;
    }

    var storageKey = 'ReportingSuite.PersonalWorkspace.v2';
    var state = { modules: [], reports: [], recent: [] };
    var activeKind = 'all';
    var dynamicPreviewCache = {};

    function loadState() {
        try {
            var saved = JSON.parse(window.localStorage.getItem(storageKey) || '{}');
            state.modules = Array.isArray(saved.modules) ? saved.modules : [];
            state.reports = Array.isArray(saved.reports) ? saved.reports : [];
            state.recent = Array.isArray(saved.recent) ? saved.recent : [];
        } catch (ignore) {
            state = { modules: [], reports: [], recent: [] };
        }
    }

    function saveState() {
        try {
            window.localStorage.setItem(storageKey, JSON.stringify(state));
        } catch (ignore) {
            // Personalisation remains optional when browser storage is restricted.
        }
    }

    function toggleArrayValue(items, value) {
        var index = items.indexOf(value);
        if (index >= 0) {
            items.splice(index, 1);
            return false;
        }
        items.unshift(value);
        return true;
    }

    function announce(message) {
        var toast = $('.js-workspace-toast');
        if (!toast.length) {
            toast = $('<div class="workspace-toast js-workspace-toast" role="status" aria-live="polite"></div>').appendTo(document.body);
        }
        toast.stop(true, true).text(message).addClass('is-visible');
        window.setTimeout(function () { toast.removeClass('is-visible'); }, 2200);
    }

    function setPinState(button, pinned) {
        button.attr('aria-pressed', pinned ? 'true' : 'false')
            .find('i').toggleClass('fa-star-o', !pinned).toggleClass('fa-star', pinned);
    }

    function renderModules() {
        var grid = $('.js-workspace-module-grid');
        var cards = grid.children('[data-module-id]');
        cards.each(function (index) {
            var card = $(this);
            if (card.attr('data-original-order') === undefined) {
                card.attr('data-original-order', index);
            }
        });

        cards.get().sort(function (left, right) {
            var leftCard = $(left);
            var rightCard = $(right);
            var leftPin = state.modules.indexOf(leftCard.attr('data-module-id'));
            var rightPin = state.modules.indexOf(rightCard.attr('data-module-id'));
            if (leftPin >= 0 || rightPin >= 0) {
                if (leftPin < 0) { return 1; }
                if (rightPin < 0) { return -1; }
                return leftPin - rightPin;
            }
            return parseInt(leftCard.attr('data-original-order'), 10) - parseInt(rightCard.attr('data-original-order'), 10);
        }).forEach(function (card) { grid.append(card); });

        cards.each(function () {
            var card = $(this);
            var pinned = state.modules.indexOf(card.attr('data-module-id')) >= 0;
            card.toggleClass('is-pinned', pinned);
            setPinState(card.find('.js-module-pin'), pinned);
        });
    }

    function reportElement(reportId) {
        return $('.workspace-report-item[data-report-id="' + String(reportId).replace(/"/g, '\\"') + '"]').first();
    }

    function currentReportData(item) {
        return {
            id: String(item.attr('data-report-id') || ''),
            title: String(item.attr('data-title') || 'Report'),
            url: String(item.attr('data-url') || '#'),
            section: String(item.attr('data-section') || 'Report catalogue'),
            kind: String(item.attr('data-kind') || 'standard'),
            opened: new Date().toISOString()
        };
    }

    function renderSavedReports() {
        var container = $('.js-saved-reports').empty();
        var availableIds = [];

        state.reports.forEach(function (reportId) {
            var item = reportElement(reportId);
            if (!item.length) {
                return;
            }
            availableIds.push(String(reportId));
            var data = currentReportData(item);
            var isDynamic = data.kind.split(/\s+/).indexOf('dynamic') >= 0;
            var link = $('<a class="workspace-saved-report js-workspace-report-link"></a>').attr({ href: isDynamic ? '#' : data.url, 'data-report-id': data.id });
            if (isDynamic) {
                link.addClass('js-dynamic-report-preview');
            }
            link.append('<i class="fa fa-star" aria-hidden="true"></i>');
            $('<span></span>').append($('<strong></strong>').text(data.title)).append($('<small></small>').text(data.section)).appendTo(link);
            container.append(link);
        });

        if (availableIds.length !== state.reports.length) {
            state.reports = availableIds;
            saveState();
        }

        $('.js-saved-panel').toggleClass('d-none', availableIds.length === 0);
        $('.js-saved-count').text(availableIds.length + ' saved');
        $('.workspace-report-item').each(function () {
            var item = $(this);
            var pinned = state.reports.indexOf(String(item.attr('data-report-id'))) >= 0;
            item.toggleClass('is-pinned', pinned);
            setPinState(item.find('.js-report-pin'), pinned);
        });
    }

    function renderRecentReports() {
        var container = $('.js-recent-reports').empty();
        var valid = [];
        state.recent.forEach(function (saved) {
            var item = reportElement(saved.id);
            if (!item.length || valid.length >= 5) {
                return;
            }
            var data = currentReportData(item);
            data.opened = saved.opened;
            valid.push(data);
            var isDynamic = data.kind.split(/\s+/).indexOf('dynamic') >= 0;
            var link = $('<a class="workspace-recent-report js-workspace-report-link"></a>').attr({ href: isDynamic ? '#' : data.url, 'data-report-id': data.id });
            if (isDynamic) {
                link.addClass('js-dynamic-report-preview');
            }
            link.append('<i class="icon-file-eye" aria-hidden="true"></i>');
            $('<span></span>').append($('<strong></strong>').text(data.title)).append($('<small></small>').text(data.section)).appendTo(link);
            link.append('<i class="icon-arrow-up-right2 text-muted" aria-hidden="true"></i>');
            container.append(link);
        });
        state.recent = valid;
        saveState();

        if (!valid.length) {
            container.html('<div class="workspace-empty-state"><i class="icon-history"></i><strong>No recent reports yet</strong><span>Reports you open from this workspace will appear here.</span></div>');
        }
        $('.js-clear-recent').prop('disabled', valid.length === 0);
    }

    function recordRecent(reportId) {
        var item = reportElement(reportId);
        if (!item.length) {
            return;
        }
        var data = currentReportData(item);
        state.recent = state.recent.filter(function (saved) { return String(saved.id) !== data.id; });
        state.recent.unshift(data);
        state.recent = state.recent.slice(0, 5);
        saveState();
    }

    function filterModules(query) {
        $('.workspace-module-card').each(function () {
            var card = $(this);
            card.toggle(!query || card.text().toLowerCase().indexOf(query) !== -1);
        });
    }

    function ensureVisibleTab() {
        var activeTab = $('.workspace-division-tabs .nav-link.active').closest('.nav-item');
        if (activeTab.length && !activeTab.hasClass('is-filtered-out')) {
            return;
        }
        var first = $('.workspace-division-tabs .nav-item:not(.is-filtered-out) .nav-link').first();
        if (first.length) {
            first.tab('show');
        }
    }

    function filterReports() {
        var query = $.trim($('#dashboard-report-search').val() || '').toLowerCase();
        var visibleTotal = 0;

        $('[data-section-panel]').each(function () {
            var panel = $(this);
            var visibleInSection = 0;
            panel.find('.workspace-report-item').each(function () {
                var item = $(this);
                var matchesText = !query || String(item.attr('data-search') || '').indexOf(query) !== -1;
                var kinds = String(item.attr('data-kind') || '').split(/\s+/);
                var matchesKind = activeKind === 'all' || kinds.indexOf(activeKind) >= 0;
                var visible = matchesText && matchesKind;
                item.toggleClass('is-filtered-out', !visible).toggle(visible);
                if (visible) {
                    visibleInSection += 1;
                    visibleTotal += 1;
                }
            });
            panel.find('.workspace-section-empty').toggleClass('d-none', visibleInSection !== 0);
            panel.find('.workspace-dynamic-group').each(function () {
                $(this).toggle($(this).find('.workspace-report-item:not(.is-filtered-out)').length > 0);
            });
            $('[data-section-tab="' + panel.attr('data-section-panel') + '"]').toggleClass('is-filtered-out', visibleInSection === 0);
        });

        ensureVisibleTab();
        $('.workspace-catalogue-empty').toggleClass('d-none', visibleTotal !== 0);
        $('.js-workspace-result-summary').text(visibleTotal + ' report' + (visibleTotal === 1 ? '' : 's'));
    }

    function activateFilter(kind, shouldScroll) {
        var supportedKinds = ['custom', 'dynamic', 'adhoc'];
        activeKind = supportedKinds.indexOf(kind) >= 0 ? kind : 'all';
        $('.js-catalogue-filter').each(function () {
            var button = $(this);
            var active = button.attr('data-kind') === activeKind;
            button.toggleClass('is-active', active).attr('aria-pressed', active ? 'true' : 'false');
        });
        filterReports();
        if (shouldScroll) {
            document.getElementById('report-catalogue').scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
    }

    function openDivision(sectionId) {
        var tab = $('.workspace-division-tabs a[href="#division-' + sectionId + '"]');
        if (tab.length) {
            activeKind = 'all';
            $('#dashboard-report-search').val('');
            activateFilter('all', false);
            tab.tab('show');
            document.getElementById('report-catalogue').scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
    }

    function setGreeting() {
        var hour = new Date().getHours();
        var greeting = hour < 12 ? 'Good morning' : hour < 18 ? 'Good afternoon' : 'Good evening';
        $('.js-workspace-greeting').text(greeting);
    }

    function previewBadge(label, cssClass) {
        return $('<span class="dynamic-report-preview-badge"></span>').addClass(cssClass || '').text(label);
    }

    function renderDynamicReportPreview(preview) {
        var modal = $('#dynamic-report-preview-modal');
        var columns = Array.isArray(preview.Columns) ? preview.Columns : [];
        var lineItems = Array.isArray(preview.LineItems) ? preview.LineItems : [];
        var displayColumns = columns.length ? columns : [{ ColumnId: 0, Name: 'Report value', DataType: 'Value', Visible: true }];
        var columnList = modal.find('.js-dynamic-preview-columns').empty();
        var tableHead = modal.find('.js-dynamic-preview-table-head').empty();
        var tableBody = modal.find('.js-dynamic-preview-table-body').empty();

        modal.find('.js-dynamic-preview-title').text(preview.Name || 'Dynamic report template');
        modal.find('.js-dynamic-preview-description').text(preview.Description || 'Configured report structure.');
        modal.find('.js-dynamic-preview-summary').empty()
            .append(previewBadge(preview.EntityGroup || 'Other dynamic reports', 'is-group'))
            .append(previewBadge(lineItems.length + ' line item' + (lineItems.length === 1 ? '' : 's'), 'is-count'))
            .append(previewBadge(columns.length + ' column' + (columns.length === 1 ? '' : 's'), 'is-count'));

        displayColumns.forEach(function (column) {
            var chip = $('<div class="dynamic-report-column-chip"></div>');
            $('<i class="icon-columns" aria-hidden="true"></i>').appendTo(chip);
            $('<span></span>').append($('<strong></strong>').text(column.Name || 'Report value'))
                .append($('<small></small>').text(column.DataType || 'Value')).appendTo(chip);
            if (column.Calculated) { chip.append(previewBadge('Calculated', 'is-calculated')); }
            if (column.StaticValue) { chip.append(previewBadge('Static', 'is-static')); }
            if (column.Visible === false) { chip.append(previewBadge('Hidden', 'is-hidden')); }
            columnList.append(chip);
        });

        var headingRow = $('<tr></tr>').append('<th scope="col" class="dynamic-report-line-heading">Report line item</th>');
        displayColumns.forEach(function (column) {
            $('<th scope="col"></th>').append($('<strong></strong>').text(column.Name || 'Report value'))
                .append($('<small></small>').text(column.DataType || 'Value')).appendTo(headingRow);
        });
        tableHead.append(headingRow);

        var currentSection = null;
        lineItems.forEach(function (lineItem) {
            var sectionName = lineItem.SectionName || 'General';
            if (sectionName !== currentSection) {
                currentSection = sectionName;
                $('<tr class="dynamic-report-section-row"></tr>')
                    .append($('<th scope="rowgroup"></th>').attr('colspan', displayColumns.length + 1).text(sectionName))
                    .appendTo(tableBody);
            }

            var row = $('<tr class="dynamic-report-line-row"></tr>')
                .attr('data-line-search', String(lineItem.Name || '').toLowerCase());
            var label = $('<td class="dynamic-report-line-label"></td>')
                .append($('<strong></strong>').text(lineItem.Name || 'Unnamed line item'));
            var flags = $('<span class="dynamic-report-line-flags"></span>');
            if (lineItem.Main) { flags.append(previewBadge('Main', 'is-main')); }
            if (lineItem.Title) { flags.append(previewBadge('Title', 'is-title')); }
            if (lineItem.Calculated) { flags.append(previewBadge('Calculated', 'is-calculated')); }
            if (lineItem.StaticValue) { flags.append(previewBadge('Static', 'is-static')); }
            if (lineItem.Visible === false) { flags.append(previewBadge('Hidden', 'is-hidden')); }
            label.append(flags);
            row.append(label);
            var mappedColumnIds = Array.isArray(lineItem.MappedColumnIds) ? lineItem.MappedColumnIds.map(String) : [];
            displayColumns.forEach(function (column) {
                var mapped = mappedColumnIds.indexOf(String(column.ColumnId || 0)) >= 0;
                var cell = $('<td class="dynamic-report-template-cell"></td>').toggleClass('is-mapped', mapped);
                if (mapped) {
                    cell.append('<span><i class="icon-checkmark3" aria-hidden="true"></i>Mapped</span>');
                } else {
                    cell.append('<span></span>');
                }
                row.append(cell);
            });
            tableBody.append(row);
        });

        modal.find('.js-dynamic-line-search').val('');
        modal.find('.js-dynamic-preview-empty').toggleClass('d-none', lineItems.length !== 0);
        modal.find('.dynamic-report-template-table-wrap').toggleClass('d-none', lineItems.length === 0);
        modal.find('.js-dynamic-preview-loading, .js-dynamic-preview-error').addClass('d-none');
        modal.find('.js-dynamic-preview-content').removeClass('d-none');
    }

    function showDynamicPreviewError() {
        var modal = $('#dynamic-report-preview-modal');
        modal.find('.js-dynamic-preview-loading, .js-dynamic-preview-content').addClass('d-none');
        modal.find('.js-dynamic-preview-error').removeClass('d-none');
    }

    function openDynamicReportPreview(trigger) {
        var reportId = String(trigger.attr('data-report-id') || trigger.closest('[data-report-id]').attr('data-report-id') || '');
        var reportItem = reportElement(reportId);
        var modal = $('#dynamic-report-preview-modal');
        if (!reportId || !reportItem.length) {
            return;
        }

        recordRecent(reportId);
        renderRecentReports();
        modal.find('.js-dynamic-preview-title').text(reportItem.attr('data-title') || 'Dynamic report template');
        modal.find('.js-dynamic-preview-description').text('Loading the configured report structure.');
        modal.find('.js-dynamic-preview-loading').removeClass('d-none');
        modal.find('.js-dynamic-preview-error, .js-dynamic-preview-content').addClass('d-none');
        modal.modal('show');

        if (dynamicPreviewCache[reportId]) {
            renderDynamicReportPreview(dynamicPreviewCache[reportId]);
            return;
        }

        $.ajax({
            url: 'DynamicReportPreviewData.ashx',
            method: 'GET',
            dataType: 'json',
            data: { reportId: parseInt(reportId, 10) }
        }).done(function (response) {
            var preview = response;
            if (!preview) {
                showDynamicPreviewError();
                return;
            }
            dynamicPreviewCache[reportId] = preview;
            renderDynamicReportPreview(preview);
        }).fail(showDynamicPreviewError);
    }

    $(function () {
        storageKey += '.' + String($('.reporting-home').attr('data-workspace-key') || 'anonymous');
        loadState();
        setGreeting();
        renderModules();
        renderSavedReports();
        renderRecentReports();

        $(document).on('click', '.js-module-pin', function () {
            var card = $(this).closest('[data-module-id]');
            var pinned = toggleArrayValue(state.modules, card.attr('data-module-id'));
            saveState();
            renderModules();
            announce(pinned ? 'Module pinned to quick access' : 'Module unpinned');
        }).on('click', '.js-report-pin', function () {
            var item = $(this).closest('[data-report-id]');
            var pinned = toggleArrayValue(state.reports, String(item.attr('data-report-id')));
            saveState();
            renderSavedReports();
            announce(pinned ? 'Report pinned to your workspace' : 'Report removed from pinned reports');
        }).on('click', '.js-workspace-report-link', function () {
            recordRecent($(this).attr('data-report-id') || $(this).closest('[data-report-id]').attr('data-report-id'));
        }).on('click', '.js-dynamic-report-preview', function (event) {
            event.preventDefault();
            openDynamicReportPreview($(this));
        });

        $('.js-dynamic-line-search').on('input', function () {
            var query = $.trim($(this).val() || '').toLowerCase();
            var body = $('.js-dynamic-preview-table-body');
            body.find('.dynamic-report-line-row').each(function () {
                var row = $(this);
                row.toggle(!query || String(row.attr('data-line-search') || '').indexOf(query) >= 0);
            });
            body.find('.dynamic-report-section-row').each(function () {
                var sectionRow = $(this);
                sectionRow.toggle(sectionRow.nextUntil('.dynamic-report-section-row', '.dynamic-report-line-row:visible').length > 0);
            });
        });

        $('.js-reset-workspace').on('click', function () {
            state.modules = [];
            state.reports = [];
            saveState();
            renderModules();
            renderSavedReports();
            announce('Workspace layout reset');
        });

        $('.js-clear-recent').on('click', function () {
            state.recent = [];
            saveState();
            renderRecentReports();
            announce('Recent report activity cleared');
        });

        $('.js-catalogue-filter').on('click', function () {
            activateFilter($(this).attr('data-kind'), false);
        });
        $('.js-workspace-filter').on('click', function () {
            activateFilter($(this).attr('data-kind'), true);
        });
        $('.js-division-shortcut').on('click', function (event) {
            event.preventDefault();
            openDivision($(this).attr('data-section-id'));
        });

        $('#dashboard-report-search').on('input', filterReports);
        $('#workspace-global-search').on('input', function () {
            var value = $.trim($(this).val() || '');
            filterModules(value.toLowerCase());
            $('#dashboard-report-search').val(value);
            filterReports();
        }).on('keydown', function (event) {
            if (event.key !== 'Enter') {
                return;
            }
            var moduleLink = $('.workspace-module-card:visible .workspace-module-link').first();
            var reportLink = $('.workspace-report-item:visible .workspace-report-link').first();
            if (moduleLink.length) {
                window.location.href = moduleLink.attr('href');
            } else if (reportLink.length) {
                reportLink.trigger('click');
            }
        });

        $(document).on('keydown', function (event) {
            var target = $(event.target);
            if (event.key === '/' && !target.is('input, textarea, select, [contenteditable="true"]')) {
                event.preventDefault();
                $('#workspace-global-search').trigger('focus');
            }
        });

        $('.workspace-division-tabs a[data-toggle="tab"]').on('shown.bs.tab', function () {
            if (window.history && window.history.replaceState) {
                window.history.replaceState(null, document.title, $(this).attr('href'));
            }
        });

        var requestedTab = window.location.hash;
        if (requestedTab && /^#division-(?:\d+|dynamic)$/.test(requestedTab)) {
            $('.workspace-division-tabs a[href="' + requestedTab + '"]').tab('show');
        }
        filterReports();
    });
}(window, document, window.jQuery));
