(function ($) {
    'use strict';

    function initialiseReportTable() {
        var table = $('#builder-report-table');
        if (!table.length || !$.fn.DataTable || $.fn.DataTable.isDataTable(table[0])) {
            return;
        }

        var instance = table.DataTable({
            pageLength: 25,
            lengthMenu: [[10, 25, 50, 100], [10, 25, 50, 100]],
            order: [[0, 'asc']],
            autoWidth: false,
            responsive: true,
            stateSave: true,
            dom: '<"d-flex flex-wrap align-items-center justify-content-between mb-3"lf>rt<"d-flex flex-wrap align-items-center justify-content-between mt-3"ip>',
            language: {
                search: '',
                searchPlaceholder: 'Search reports, divisions, or types'
            },
            columnDefs: [{ orderable: false, targets: -1 }]
        });

        $('#builder-report-search').on('input', function () {
            instance.search(this.value).draw();
        });
    }

    function readStoredView(key) {
        try {
            return window.sessionStorage.getItem(key);
        } catch (ignore) {
            return null;
        }
    }

    function storeView(key, value) {
        try {
            window.sessionStorage.setItem(key, value);
        } catch (ignore) {
            // Storage can be unavailable in privacy-restricted browser sessions.
        }
    }

    function initialiseWorkspaceNavigation() {
        var nav = $('#builder-workspace-nav');
        var root = $('.report-builder-workspace[data-report-selected="true"]');
        var buttons = nav.find('[data-builder-view-target]');
        var views = root.find('[data-builder-view]');
        if (!nav.length || !root.length || !buttons.length || !views.length) {
            return;
        }

        var storageKey = 'report-builder-view-' + (root.attr('data-report-id') || 'current');

        views.each(function () {
            var view = $(this);
            var name = view.attr('data-builder-view');
            var id = 'builder-view-' + name;
            view.attr({ id: id, role: 'tabpanel' });
            buttons.filter('[data-builder-view-target="' + name + '"]').attr('aria-controls', id);
        });

        function activate(name, moveFocus) {
            var requested = buttons.filter('[data-builder-view-target="' + name + '"]');
            if (!requested.length) {
                requested = buttons.first();
                name = requested.attr('data-builder-view-target');
            }

            buttons.each(function () {
                var button = $(this);
                var selected = button.is(requested);
                button.toggleClass('active', selected)
                    .attr('aria-selected', selected ? 'true' : 'false')
                    .attr('tabindex', selected ? '0' : '-1');
            });

            views.each(function () {
                var view = $(this);
                var selected = view.attr('data-builder-view') === name;
                view.toggleClass('active', selected).prop('hidden', !selected);
            });

            storeView(storageKey, name);
            root.trigger('builder:viewchange', [name]);
            if (moveFocus) {
                requested.trigger('focus');
            }
        }

        buttons.on('click', function () {
            activate($(this).attr('data-builder-view-target'), false);
        }).on('keydown', function (event) {
            if (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight') {
                return;
            }
            event.preventDefault();
            var current = buttons.index(this);
            var direction = event.key === 'ArrowRight' ? 1 : -1;
            var next = (current + direction + buttons.length) % buttons.length;
            activate(buttons.eq(next).attr('data-builder-view-target'), true);
        });

        activate(readStoredView(storageKey) || 'overview', false);
    }

    function initialiseDesignExplorer() {
        var panel = $('.builder-designer-panel');
        var table = panel.find('.js-builder-designer-table');
        var body = table.find('#Report_Items');
        if (!panel.length || !table.length || !body.length) {
            return;
        }

        var search = panel.find('.js-builder-design-search');
        var filter = panel.find('.js-builder-design-filter');
        var pageSize = panel.find('.js-builder-design-page-size');
        var previous = panel.find('.js-builder-page-prev');
        var next = panel.find('.js-builder-page-next');
        var status = panel.find('.js-builder-page-status');
        var count = panel.find('.js-builder-design-count');
        var density = panel.find('.js-builder-density');
        var orderReset = panel.find('.js-builder-order-reset');
        var orderStatus = panel.find('.js-builder-order-status');
        var page = 1;
        var draggedRow = null;
        var dragValues = [];
        var touchDraggedRow = null;
        var touchTarget = null;
        var touchValues = [];
        var initialOrder = body.children('tr[data-line-item-id]').map(function () {
            var row = $(this);
            return {
                node: this,
                id: row.attr('data-line-item-id'),
                value: row.find('[id^="Sort_Order_"]').val()
            };
        }).get();

        function valueFrom(row, prefix, addName) {
            var field = row.find('[id^="' + prefix + '_"], [name^="' + addName + '_"]').first();
            return field.length ? String(field.val()) : '';
        }

        function rowName(row) {
            var nameField = row.find('[id^="Line_itemName_"], [name^="Add_LineItem_Name_"]').first();
            var name = nameField.length ? nameField.val() : row.attr('data-name');
            return String(name || row.text() || '').toLowerCase();
        }

        function matches(row, query, selectedFilter) {
            if (query && rowName(row).indexOf(query) === -1) {
                return false;
            }

            var visible = valueFrom(row, 'Show', 'Add_Show') || (row.attr('data-visible') === 'true' ? '1' : '0');
            var title = valueFrom(row, 'Is_Title', 'Add_Is_Title') || (row.attr('data-title') === 'true' ? '1' : '0');
            var calculated = row.attr('data-calculated') === 'true';

            if (selectedFilter === 'visible') {
                return visible === '1';
            }
            if (selectedFilter === 'hidden') {
                return visible === '0';
            }
            if (selectedFilter === 'title') {
                return title === '1';
            }
            if (selectedFilter === 'calculated') {
                return calculated;
            }
            return true;
        }

        function render() {
            var rows = body.children('tr');
            var query = String(search.val() || '').trim().toLowerCase();
            var selectedFilter = filter.val() || 'all';
            var sizeValue = pageSize.val() || '25';
            var matching = rows.filter(function () {
                return matches($(this), query, selectedFilter);
            });
            var size = sizeValue === 'all' ? Math.max(matching.length, 1) : parseInt(sizeValue, 10) || 25;
            var pages = Math.max(Math.ceil(matching.length / size), 1);
            page = Math.min(Math.max(page, 1), pages);
            var start = (page - 1) * size;
            var end = Math.min(start + size, matching.length);

            rows.hide().attr('aria-hidden', 'true');
            matching.slice(start, end).show().attr('aria-hidden', 'false');

            if (matching.length) {
                count.text((start + 1) + '–' + end + ' of ' + matching.length + ' matching items');
            } else {
                count.text('No line items match the current search');
            }
            status.text('Page ' + page + ' of ' + pages);
            previous.prop('disabled', page <= 1);
            next.prop('disabled', page >= pages);
            panel.toggleClass('has-no-results', matching.length === 0);
        }

        function persistedRows() {
            return body.children('tr[data-line-item-id]');
        }

        function currentOrderValues() {
            return persistedRows().map(function () {
                return $(this).find('[id^="Sort_Order_"]').val();
            }).get();
        }

        function orderHasChanged() {
            var rows = persistedRows();
            if (rows.length !== initialOrder.length) {
                return true;
            }
            for (var index = 0; index < rows.length; index += 1) {
                if ($(rows[index]).attr('data-line-item-id') !== initialOrder[index].id) {
                    return true;
                }
            }
            return false;
        }

        function announceOrder(message, state) {
            orderStatus.removeClass('is-saving is-saved is-error')
                .addClass(state ? 'is-' + state : '')
                .html('<i class="fa ' + (state === 'saving' ? 'fa-spinner fa-spin' : state === 'saved' ? 'fa-check-circle' : 'fa-arrows-v') + '" aria-hidden="true"></i> ' + message);
        }

        function applyOrderValues(values) {
            var changed = 0;
            persistedRows().each(function (index) {
                var field = $(this).find('[id^="Sort_Order_"]').first();
                if (field.length && values[index] !== undefined && String(field.val()) !== String(values[index])) {
                    field.val(values[index]).trigger('change');
                    changed += 1;
                }
            });

            orderReset.prop('disabled', !orderHasChanged());
            if (changed) {
                announceOrder('Saving ' + changed + ' order change' + (changed === 1 ? '' : 's') + '…', 'saving');
                window.setTimeout(function () {
                    announceOrder('Order changes submitted', 'saved');
                }, 650);
            }
            render();
        }

        function moveRow(row, target, after) {
            if (!row || !target || row[0] === target[0]) {
                return;
            }
            var values = currentOrderValues();
            if (after) {
                row.insertAfter(target);
            } else {
                row.insertBefore(target);
            }
            applyOrderValues(values);
            row.addClass('builder-row-just-moved');
            window.setTimeout(function () { row.removeClass('builder-row-just-moved'); }, 700);
            row.find('.js-builder-drag-handle').trigger('focus');
        }

        search.on('input', function () {
            page = 1;
            render();
        });
        filter.add(pageSize).on('change', function () {
            page = 1;
            render();
        });
        previous.on('click', function () {
            page -= 1;
            render();
        });
        next.on('click', function () {
            page += 1;
            render();
        });
        density.on('click', function () {
            var compact = !panel.hasClass('is-compact');
            panel.toggleClass('is-compact', compact);
            $(this).attr('aria-pressed', compact ? 'true' : 'false')
                .html('<i class="fa ' + (compact ? 'fa-expand' : 'fa-compress') + '" aria-hidden="true"></i> ' + (compact ? 'Comfortable rows' : 'Compact rows'));
        });
        orderReset.on('click', function () {
            var firstNewRow = body.children('tr.is-new').first();
            initialOrder.forEach(function (entry) {
                if (firstNewRow.length) {
                    $(entry.node).insertBefore(firstNewRow);
                } else {
                    body.append(entry.node);
                }
            });
            applyOrderValues(initialOrder.map(function (entry) { return entry.value; }));
            orderReset.prop('disabled', true);
            announceOrder('Original order restored', 'saved');
        });

        body.on('mousedown touchstart', '.js-builder-drag-handle:not(.is-disabled)', function () {
            $(this).closest('tr[data-line-item-id]').attr('draggable', 'true');
        });
        body.on('pointerdown', '.js-builder-drag-handle:not(.is-disabled)', function (event) {
            var original = event.originalEvent;
            if (!original || original.pointerType !== 'touch') {
                return;
            }
            event.preventDefault();
            touchDraggedRow = $(this).closest('tr[data-line-item-id]');
            touchTarget = null;
            touchValues = currentOrderValues();
            touchDraggedRow.addClass('is-dragging');
            if (this.setPointerCapture) {
                this.setPointerCapture(original.pointerId);
            }
            announceOrder('Move the row and release it at the new position', 'saving');
        });
        body.on('pointermove', '.js-builder-drag-handle:not(.is-disabled)', function (event) {
            if (!touchDraggedRow) {
                return;
            }
            var original = event.originalEvent;
            var element = document.elementFromPoint(original.clientX, original.clientY);
            var target = $(element).closest('tr[data-line-item-id]');
            body.children().removeClass('is-drag-target-before is-drag-target-after');
            touchTarget = null;
            if (!target.length || target[0] === touchDraggedRow[0]) {
                return;
            }
            var rect = target[0].getBoundingClientRect();
            var after = original.clientY > rect.top + (rect.height / 2);
            target.addClass(after ? 'is-drag-target-after' : 'is-drag-target-before');
            touchTarget = target;
        });
        body.on('pointerup pointercancel', '.js-builder-drag-handle:not(.is-disabled)', function (event) {
            if (!touchDraggedRow) {
                return;
            }
            var shouldMove = event.type === 'pointerup' && touchTarget && touchTarget.length;
            var movedRow = touchDraggedRow;
            if (shouldMove) {
                if (touchTarget.hasClass('is-drag-target-after')) {
                    movedRow.insertAfter(touchTarget);
                } else {
                    movedRow.insertBefore(touchTarget);
                }
                applyOrderValues(touchValues);
                movedRow.addClass('builder-row-just-moved');
                window.setTimeout(function () { movedRow.removeClass('builder-row-just-moved'); }, 700);
            } else {
                announceOrder('Drag rows to reorder');
            }
            body.children().removeClass('is-dragging is-drag-target-before is-drag-target-after').removeAttr('draggable');
            touchDraggedRow = null;
            touchTarget = null;
            touchValues = [];
        });
        body.on('dragstart', 'tr[data-line-item-id]', function (event) {
            if (!$(event.target).attr('draggable')) {
                event.preventDefault();
                return;
            }
            draggedRow = $(this);
            dragValues = currentOrderValues();
            draggedRow.addClass('is-dragging');
            var transfer = event.originalEvent && event.originalEvent.dataTransfer;
            if (transfer) {
                transfer.effectAllowed = 'move';
                transfer.setData('text/plain', draggedRow.attr('data-line-item-id'));
            }
        });
        body.on('dragover', 'tr[data-line-item-id]', function (event) {
            if (!draggedRow || draggedRow[0] === this) {
                return;
            }
            event.preventDefault();
            body.children().removeClass('is-drag-target-before is-drag-target-after');
            var original = event.originalEvent;
            var rect = this.getBoundingClientRect();
            var after = original.clientY > rect.top + (rect.height / 2);
            $(this).addClass(after ? 'is-drag-target-after' : 'is-drag-target-before');
        });
        body.on('drop', 'tr[data-line-item-id]', function (event) {
            if (!draggedRow || draggedRow[0] === this) {
                return;
            }
            event.preventDefault();
            var target = $(this);
            var after = target.hasClass('is-drag-target-after');
            if (after) {
                draggedRow.insertAfter(target);
            } else {
                draggedRow.insertBefore(target);
            }
            applyOrderValues(dragValues);
            var movedRow = draggedRow;
            movedRow.addClass('builder-row-just-moved');
            window.setTimeout(function () {
                movedRow.removeClass('builder-row-just-moved');
            }, 700);
        });
        body.on('dragend', 'tr[data-line-item-id]', function () {
            body.children().removeClass('is-dragging is-drag-target-before is-drag-target-after');
            $(this).removeAttr('draggable');
            draggedRow = null;
            dragValues = [];
        });
        $(document).on('mouseup.builderOrder touchend.builderOrder', function () {
            if (!draggedRow) {
                persistedRows().removeAttr('draggable');
            }
        });
        body.on('keydown', '.js-builder-drag-handle:not(.is-disabled)', function (event) {
            if (event.key !== 'ArrowUp' && event.key !== 'ArrowDown') {
                return;
            }
            event.preventDefault();
            var row = $(this).closest('tr[data-line-item-id]');
            var visibleRows = persistedRows().filter(':visible');
            var current = visibleRows.index(row);
            var targetIndex = event.key === 'ArrowUp' ? current - 1 : current + 1;
            if (targetIndex >= 0 && targetIndex < visibleRows.length) {
                moveRow(row, visibleRows.eq(targetIndex), event.key === 'ArrowDown');
            }
        });
        body.on('input change', 'input, select', function () {
            if ($(this).is('[id^="Line_itemName_"], [name^="Add_LineItem_Name_"], [id^="Show_"], [name^="Add_Show_"], [id^="Is_Title_"], [name^="Add_Is_Title_"]')) {
                render();
            }
        });

        if (window.MutationObserver) {
            new window.MutationObserver(function (mutations) {
                var addedNewRow = mutations.some(function (mutation) {
                    for (var index = 0; index < mutation.addedNodes.length; index += 1) {
                        if ($(mutation.addedNodes[index]).is('tr.is-new')) {
                            return true;
                        }
                    }
                    return false;
                });
                if (addedNewRow) {
                    page = Math.max(Math.ceil(body.children('tr').length / (parseInt(pageSize.val(), 10) || 25)), 1);
                    render();
                }
            }).observe(body[0], { childList: true });
        }

        $('.report-builder-workspace').on('builder:viewchange', function (event, view) {
            if (view === 'designer') {
                render();
            }
        });
        render();
    }

    function initialiseDesignerModalLayer() {
        var designer = $('.builder-designer-panel');
        var modals = designer.find('.modal');
        if (!designer.length || !modals.length) {
            return;
        }

        function enhanceSmallSelect(select) {
            if (select.prop('multiple') || select.hasClass('builder-native-select') || select.hasClass('select2-hidden-accessible')) {
                return;
            }

            var options = select.find('option');
            var choices = options.filter(function () { return String($(this).val()) !== ''; });
            if (choices.length < 2 || choices.length > 4) {
                return;
            }

            var label = select.closest('.form-group').find('label').first().text().trim() || 'Choose an option';
            var group = $('<div class="builder-choice-group" role="radiogroup"></div>').attr('aria-label', label);
            options.each(function () {
                var option = $(this);
                var value = String(option.val());
                var text = option.text().trim().replace(/^--\s*|\s*--$/g, '') || 'Not set';
                $('<button type="button" class="builder-choice" role="radio"></button>')
                    .attr('data-value', value)
                    .text(text)
                    .appendTo(group);
            });

            function syncChoice() {
                var current = String(select.val() === null ? '' : select.val());
                group.find('.builder-choice').each(function () {
                    var active = String($(this).attr('data-value')) === current;
                    $(this).toggleClass('is-selected', active)
                        .attr('aria-checked', active ? 'true' : 'false')
                        .attr('tabindex', active ? '0' : '-1');
                });
            }

            group.on('click', '.builder-choice', function () {
                select.val($(this).attr('data-value')).trigger('change');
            }).on('keydown', '.builder-choice', function (event) {
                if (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight') {
                    return;
                }
                event.preventDefault();
                var buttons = group.find('.builder-choice');
                var current = buttons.index(this);
                var next = event.key === 'ArrowRight' ? current + 1 : current - 1;
                buttons.eq((next + buttons.length) % buttons.length).trigger('click').trigger('focus');
            });

            select.addClass('builder-native-select').after(group).on('change', syncChoice);
            syncChoice();
        }

        modals.each(function (modalIndex) {
            var modal = $(this);
            var header = modal.find('.modal-header').first();
            var title = header.find('.modal-title').first();
            var body = modal.find('.modal-body').first();
            var fields = body.find('.form-group');
            var titleId = modal.attr('id') + '-title-' + modalIndex;

            modal.addClass('builder-designer-modal').attr('aria-labelledby', titleId);
            modal.find('.modal-dialog').addClass('builder-designer-dialog');
            title.attr('id', titleId).wrap('<div class="builder-modal-heading"></div>');
            title.before('<span class="builder-modal-eyebrow">Report designer</span>');
            $('<button type="button" class="builder-modal-expand" aria-label="Toggle full screen" title="Toggle full screen"><i class="fa fa-expand" aria-hidden="true"></i></button>')
                .insertBefore(header.find('.close').first());

            body.find('.x_panel').addClass('builder-modal-section');
            body.find('.x_title .panel_toolbox').attr('aria-hidden', 'true');
            fields.each(function () {
                var field = $(this);
                field.addClass('builder-modal-field');
                if (!field.children('label').length) {
                    field.addClass('builder-modal-field-actions');
                }
            });
            body.find('select').each(function () { enhanceSmallSelect($(this)); });
            body.find('.checkbox label:has(input[type="checkbox"])').addClass('builder-check-card');

            if (fields.length >= 5) {
                var tools = $('<div class="builder-modal-tools"><label><span class="sr-only">Find a setting</span><i class="fa fa-search" aria-hidden="true"></i><input type="search" class="form-control js-builder-modal-filter" placeholder="Find a setting in this dialog" autocomplete="off" /></label><span class="js-builder-modal-filter-status" aria-live="polite"></span></div>');
                body.prepend(tools);
                tools.on('input', '.js-builder-modal-filter', function () {
                    var query = String($(this).val() || '').trim().toLowerCase();
                    var visible = 0;
                    fields.each(function () {
                        var field = $(this);
                        var match = !query || field.text().toLowerCase().indexOf(query) !== -1 || field.find('input, textarea, select').filter(function () {
                            return String($(this).attr('name') || $(this).attr('placeholder') || '').toLowerCase().indexOf(query) !== -1;
                        }).length > 0;
                        field.toggle(match);
                        if (match) {
                            visible += 1;
                        }
                    });
                    tools.find('.js-builder-modal-filter-status').text(query ? visible + ' setting' + (visible === 1 ? '' : 's') + ' shown' : '');
                });
            }

            modal.appendTo(document.body);
        });

        $(document).on('click', '.builder-modal-expand', function () {
            var modal = $(this).closest('.builder-designer-modal');
            var expanded = !modal.hasClass('is-fullscreen');
            modal.toggleClass('is-fullscreen', expanded);
            $(this).attr('aria-pressed', expanded ? 'true' : 'false')
                .find('i').toggleClass('fa-expand', !expanded).toggleClass('fa-compress', expanded);
        }).on('shown.bs.modal', '.builder-designer-modal', function () {
            var modal = $(this);
            $('.modal-backdrop').last().addClass('builder-designer-backdrop');
            modal.find('.js-builder-modal-filter').val('').trigger('input');
            modal.find('input:visible, select:visible, textarea:visible, button:visible').not('.close, .builder-modal-expand').first().trigger('focus');
        }).on('hidden.bs.modal', '.builder-designer-modal', function () {
            $(this).removeClass('is-fullscreen');
            $('.builder-designer-backdrop').removeClass('builder-designer-backdrop');
        });
    }

    function initialiseSecurityForm() {
        var checkbox = $('#report-security-restricted');
        var fields = $('#report-security-fields');
        if (!checkbox.length || !fields.length) {
            return;
        }

        function sync() {
            fields.toggleClass('d-none', !checkbox.prop('checked'));
            fields.find('textarea').prop('required', false);
        }

        checkbox.on('change', sync);
        sync();
    }

    $(function () {
        initialiseReportTable();
        initialiseWorkspaceNavigation();
        initialiseDesignExplorer();
        initialiseDesignerModalLayer();
        initialiseSecurityForm();

        $('[data-builder-focus]').on('click', function () {
            var selector = $(this).attr('data-builder-focus');
            var target = $(selector);
            if (target.length) {
                target.trigger('focus');
            }
        });
    });
})(window.jQuery);
