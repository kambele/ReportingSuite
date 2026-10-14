(function (window, document, $) {
    'use strict';

    if (!$) {
        return;
    }

    var clockTimer;

    function updateClock() {
        var clock = document.getElementById('reporting-clock');
        if (!clock) {
            return;
        }

        clock.textContent = new Intl.DateTimeFormat(undefined, {
            dateStyle: 'medium',
            timeStyle: 'short'
        }).format(new Date());
    }

    function startClock() {
        window.clearInterval(clockTimer);
        updateClock();
        clockTimer = window.setInterval(updateClock, 30000);
    }

    function markActiveNavigation() {
        var currentPath = window.location.pathname.replace(/\/$/, '').toLowerCase();
        var currentQuery = window.location.search.toLowerCase();
        var exactMatch;
        var pathMatch;

        $('.reporting-sidebar .nav-link[href]').each(function () {
            var href = this.getAttribute('href');
            if (!href || href === '#') {
                return;
            }

            var target;
            try {
                target = new URL(href, window.location.href);
            } catch (error) {
                return;
            }

            var targetPath = target.pathname.replace(/\/$/, '').toLowerCase();
            var targetQuery = target.search.toLowerCase();
            if (targetPath === currentPath && targetQuery && targetQuery === currentQuery) {
                exactMatch = this;
            } else if (!pathMatch && targetPath === currentPath && !targetQuery) {
                pathMatch = this;
            }
        });

        var activeLink = exactMatch || pathMatch;
        if (!activeLink) {
            return;
        }

        $(activeLink).addClass('active')
            .parents('.nav-item-submenu').addClass('nav-item-open')
            .children('.nav-group-sub').css('display', 'block');
    }

    function initializeSidebarScrolling() {
        var pane = $('#reporting-navigation-scroll');
        if (!pane.length) {
            return;
        }

        var storageKey = 'ReportingSuite.Navigation.ScrollTop';
        var restored = false;
        try {
            var savedPosition = parseInt(window.sessionStorage.getItem(storageKey), 10);
            if (!isNaN(savedPosition) && savedPosition >= 0) {
                pane.scrollTop(savedPosition);
                restored = true;
            }
        } catch (ignore) { }

        if (!restored) {
            var activeLink = pane.find('.nav-link.active').first();
            if (activeLink.length) {
                var linkTop = activeLink.position().top;
                if (linkTop < 0 || linkTop + activeLink.outerHeight() > pane.innerHeight()) {
                    pane.scrollTop(Math.max(0, pane.scrollTop() + linkTop - 80));
                }
            }
        }

        var saveTimer;
        pane.off('scroll.reportingNavigation').on('scroll.reportingNavigation', function () {
            window.clearTimeout(saveTimer);
            saveTimer = window.setTimeout(function () {
                try {
                    window.sessionStorage.setItem(storageKey, String(pane.scrollTop()));
                } catch (ignore) { }
            }, 100);
        });
    }

    function checkboxSelectionSignature(select) {
        return (select.val() || []).slice().sort().join('|');
    }

    function renderCheckboxOption(option) {
        if (!option.id) {
            return option.text;
        }

        var content = $('<span class="reporting-checkbox-option"><span class="reporting-checkbox-option-box" aria-hidden="true"></span><span class="reporting-checkbox-option-label"></span></span>');
        content.find('.reporting-checkbox-option-label').text(option.text);
        return content;
    }

    function updateCheckboxFilterStatus(select) {
        var container = select.closest('.report-checkbox-filter');
        var selectedCount = select.find('option:selected').filter(function () {
            return this.value !== '';
        }).length;
        var totalCount = select.find('option').filter(function () {
            return this.value !== '';
        }).length;
        var status = container.find('[data-multiselect-count]').first();
        var label = select.attr('data-selection-label') || 'items';
        var text;

        if (selectedCount === 0) {
            text = select.attr('data-empty-label') || 'None selected';
        } else if (selectedCount === totalCount && totalCount > 0) {
            text = 'All ' + totalCount + ' ' + label + ' selected';
        } else {
            text = selectedCount + ' of ' + totalCount + ' ' + label + ' selected';
        }

        status.text(text);
        container.toggleClass('has-selection', selectedCount > 0);
        container.toggleClass('has-all-selected', totalCount > 0 && selectedCount === totalCount);
    }

    function postBackCheckboxFilter(select) {
        if (!select.is('[data-dependent-refresh="true"]') || select.data('reportingPostbackPending')) {
            return;
        }

        select.data('reportingPostbackPending', true);
        window.setTimeout(function () {
            if (typeof window.__doPostBack === 'function') {
                window.__doPostBack(select.attr('name'), '');
            }
        }, 0);
    }

    function initializeSelects(context) {
        if (!$.fn.select2) {
            return;
        }

        $(context).find('select').addBack('select').each(function () {
            var select = $(this);
            if (select.hasClass('select2-hidden-accessible') || select.is('[data-no-select2]')) {
                return;
            }

            var modal = select.closest('.modal');
            var options = {
                dropdownAutoWidth: true,
                dropdownParent: modal.length ? modal : select.parent(),
                width: '100%'
            };

            if (select.is('[data-checkbox-multiselect="true"]')) {
                if (select.is('[data-dependent-refresh="true"]')) {
                    select.removeAttr('onchange').prop('onchange', null);
                }
                options.closeOnSelect = false;
                options.placeholder = select.attr('data-placeholder') || 'Tick one or more options';
                options.templateResult = renderCheckboxOption;
            }

            select.select2(options);

            if (select.is('[data-checkbox-multiselect="true"]')) {
                select
                    .off('.reportingCheckboxFilter')
                    .on('change.reportingCheckboxFilter', function () {
                        updateCheckboxFilterStatus(select);
                    })
                    .on('select2:open.reportingCheckboxFilter', function () {
                        select.data('reportingOpenSelection', checkboxSelectionSignature(select));
                    })
                    .on('select2:close.reportingCheckboxFilter', function () {
                        var openedWith = select.data('reportingOpenSelection');
                        if (typeof openedWith !== 'undefined' && openedWith !== checkboxSelectionSignature(select)) {
                            postBackCheckboxFilter(select);
                        }
                    });
                updateCheckboxFilterStatus(select);
            }
        });
    }

    function initializeDatePickers(context) {
        if ($.fn.datepicker) {
            $(context).find('.datepicker').addBack('.datepicker').each(function () {
                var picker = $(this);
                if (!picker.data('datepicker')) {
                    picker.datepicker({ autoclose: true, todayHighlight: true });
                }
            });
        }
    }

    function initializeDataTables(context) {
        if (!$.fn.DataTable) {
            return;
        }

        $(context).find('#datatable').addBack('#datatable').each(function () {
            if (!$.fn.dataTable.isDataTable(this)) {
                var buttonRegistry = $.fn.dataTable.ext && $.fn.dataTable.ext.buttons ? $.fn.dataTable.ext.buttons : {};
                var buttons = [];
                if (buttonRegistry.colvis) {
                    buttons.push({ extend: 'colvis', text: '<i class="icon-columns mr-1"></i>Columns' });
                }
                if (buttonRegistry.copyHtml5) {
                    buttons.push({ extend: 'copyHtml5', text: '<i class="icon-copy3 mr-1"></i>Copy' });
                }
                if (buttonRegistry.print) {
                    buttons.push({ extend: 'print', text: '<i class="icon-printer2 mr-1"></i>Print' });
                }

                var options = {
                    autoWidth: false,
                    pageLength: 25,
                    lengthMenu: [[10, 25, 50, 100], [10, 25, 50, 100]],
                    pagingType: 'full_numbers',
                    orderMulti: true,
                    orderClasses: false,
                    stateSave: true,
                    responsive: !!($.fn.dataTable.Responsive),
                    keys: !!($.fn.dataTable.KeyTable),
                    dom: buttons.length ? '<"datatable-header"Bfl>rt<"datatable-footer"ip>' : '<"datatable-header"fl>rt<"datatable-footer"ip>',
                    language: {
                        search: '<span>Search:</span> _INPUT_',
                        searchPlaceholder: 'Search table',
                        lengthMenu: '<span>Show:</span> _MENU_',
                        emptyTable: 'No records are available.',
                        zeroRecords: 'No records match your search.'
                    }
                };
                if (buttons.length) {
                    options.buttons = {
                        buttons: buttons,
                        dom: { button: { className: 'dt-button btn btn-light btn-sm' } }
                    };
                }
                $(this).DataTable(options);
            }
        });
    }

    function makeTablesResponsive(context) {
        $(context).find('table.table').addBack('table.table').each(function () {
            var table = $(this);
            if (table.parent().hasClass('table-responsive') ||
                table.closest('.dataTables_scroll').length ||
                table.hasClass('dataTable') ||
                table.is('[data-datatable-managed="true"]')) {
                return;
            }

            table.wrap('<div class="table-responsive reporting-table-scroll"></div>');
        });
    }

    function initializeValidation(context) {
        if ($.fn.parsley) {
            $(context).find('form[data-parsley-validate]').addBack('form[data-parsley-validate]').each(function () {
                if (!$(this).data('Parsley')) {
                    $(this).parsley();
                }
            });
        }
    }

    function initializeUtilities(context) {
        if ($.fn.tooltip) {
            $(context).find('[data-toggle="tooltip"]').addBack('[data-toggle="tooltip"]').tooltip({ container: 'body' });
        }

        if (window.autosize) {
            window.autosize($(context).find('textarea').addBack('textarea'));
        }
    }

    function enhance(context) {
        var root = context || document;
        initializeSelects(root);
        initializeDatePickers(root);
        initializeDataTables(root);
        makeTablesResponsive(root);
        initializeValidation(root);
        initializeUtilities(root);
    }

    function bindLegacyPanelActions() {
        $(document)
            .off('click.reportingCheckboxFilter', '[data-multiselect-action]')
            .on('click.reportingCheckboxFilter', '[data-multiselect-action]', function () {
                var button = $(this);
                var select = button.closest('.report-checkbox-filter').find('select[data-checkbox-multiselect="true"]').first();
                if (!select.length || select.prop('disabled')) {
                    return;
                }

                var before = checkboxSelectionSignature(select);
                if (button.attr('data-multiselect-action') === 'select-all') {
                    select.val(select.find('option').filter(function () {
                        return this.value !== '';
                    }).map(function () {
                        return this.value;
                    }).get());
                } else {
                    select.val(null);
                }
                select.trigger('change');

                if (before !== checkboxSelectionSignature(select)) {
                    postBackCheckboxFilter(select);
                }
            })
            .off('click.reportingCollapse', '.collapse-link')
            .on('click.reportingCollapse', '.collapse-link', function (event) {
                event.preventDefault();
                var panel = $(this).closest('.x_panel, .panel');
                panel.children('.x_content, .panel-body').first().stop(true, true).slideToggle(160);
                $(this).find('.fa, [class^="icon-"]').toggleClass('fa-chevron-up fa-chevron-down');
            })
            .off('click.reportingClose', '.close-link')
            .on('click.reportingClose', '.close-link', function (event) {
                event.preventDefault();
                $(this).closest('.x_panel, .panel').stop(true, true).slideUp(160);
            })
            .off('shown.bs.modal.reporting', '.modal')
            .on('shown.bs.modal.reporting', '.modal', function () {
                enhance(this);
            });
    }

    function registerAsyncPostbackSupport() {
        if (window.Sys && window.Sys.WebForms && window.Sys.WebForms.PageRequestManager) {
            window.Sys.WebForms.PageRequestManager.getInstance().add_endRequest(function (sender, args) {
                enhance(document);
                if (args.get_error()) {
                    args.set_errorHandled(false);
                }
            });
        }
    }

    $(function () {
        startClock();
        markActiveNavigation();
        initializeSidebarScrolling();
        bindLegacyPanelActions();
        enhance(document);
        registerAsyncPostbackSupport();

        if (window.NProgress) {
            window.NProgress.done();
        }
    });

    $(window).on('beforeunload', function () {
        if (window.NProgress) {
            window.NProgress.start();
        }
    });
}(window, document, window.jQuery));
