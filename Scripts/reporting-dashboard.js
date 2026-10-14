(function (window, document, $) {
    'use strict';

    if (!$) {
        return;
    }

    var storageKey = 'ReportingSuite.PersonalWorkspace.v2';
    var state = { modules: [], reports: [], recent: [] };
    var activeKind = 'all';

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
            var link = $('<a class="workspace-saved-report js-workspace-report-link"></a>').attr({ href: data.url, 'data-report-id': data.id });
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
            var link = $('<a class="workspace-recent-report js-workspace-report-link"></a>').attr({ href: data.url, 'data-report-id': data.id });
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
                var matchesKind = activeKind === 'all' || item.attr('data-kind') === activeKind;
                var visible = matchesText && matchesKind;
                item.toggle(visible);
                if (visible) {
                    visibleInSection += 1;
                    visibleTotal += 1;
                }
            });
            panel.find('.workspace-section-empty').toggleClass('d-none', visibleInSection !== 0);
            $('[data-section-tab="' + panel.attr('data-section-panel') + '"]').toggleClass('is-filtered-out', visibleInSection === 0);
        });

        ensureVisibleTab();
        $('.workspace-catalogue-empty').toggleClass('d-none', visibleTotal !== 0);
        $('.js-workspace-result-summary').text(visibleTotal + ' report' + (visibleTotal === 1 ? '' : 's'));
    }

    function activateFilter(kind, shouldScroll) {
        activeKind = kind === 'adhoc' || kind === 'standard' ? kind : 'all';
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
                window.location.href = reportLink.attr('href');
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
        if (requestedTab && /^#division-\d+$/.test(requestedTab)) {
            $('.workspace-division-tabs a[href="' + requestedTab + '"]').tab('show');
        }
        filterReports();
    });
}(window, document, window.jQuery));
