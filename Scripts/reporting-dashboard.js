(function (window, document, $) {
    'use strict';

    if (!$) {
        return;
    }

    function filterReports() {
        var term = $.trim($('#dashboard-report-search').val()).toLowerCase();

        $('.tab-pane[id^="division-"]').each(function () {
            var pane = $(this);
            var visible = 0;

            pane.find('.dashboard-report-item').each(function () {
                var item = $(this);
                var matches = !term || String(item.data('search') || '').indexOf(term) !== -1;
                item.toggle(matches);
                if (matches) {
                    visible += 1;
                }
            });

            pane.find('.dashboard-no-results').toggleClass('d-none', visible !== 0);
        });
    }

    $(function () {
        $('#dashboard-report-search').on('input', filterReports);

        $('a[data-toggle="tab"]').on('shown.bs.tab', function () {
            window.setTimeout(function () {
                window.dispatchEvent(new Event('resize'));
            }, 0);
        });

        var requestedTab = window.location.hash;
        if (requestedTab && $(requestedTab).length) {
            $('.dashboard-tab-strip a[href="' + requestedTab.replace(/"/g, '\\"') + '"]').tab('show');
        }
    });
}(window, document, window.jQuery));
