(function (window, document, $) {
    'use strict';

    if (!$) {
        return;
    }

    function htmlEncode(value) {
        return $('<div>').text(value == null ? '' : String(value)).html();
    }

    function readAjaxError(xhr, fallback) {
        if (xhr && xhr.responseJSON && xhr.responseJSON.error) {
            return xhr.responseJSON.error;
        }
        try {
            var parsed = JSON.parse(xhr.responseText);
            return parsed.error || fallback;
        } catch (ignore) {
            return fallback;
        }
    }

    $(function () {
        var pageConfig = $('#adhoc-page-config');
        if (!pageConfig.length) {
            return;
        }

        var endpoint = pageConfig.attr('data-endpoint');
        var requestToken = pageConfig.attr('data-request-token');
        initializeCreateModal(endpoint, requestToken, pageConfig.attr('data-ace-base'));
        initializeEditModal(endpoint, requestToken, pageConfig.attr('data-ace-base'));
        initializeReportViewer(endpoint);
    });

    function initializeCreateModal(endpoint, requestToken, aceBasePath) {
        var modal = $('#new-adhoc-report-modal');
        if (!modal.length) {
            return;
        }

        var editor;
        var fallback = $('#new-adhoc-query-fallback');
        if (window.ace) {
            window.ace.config.set('basePath', aceBasePath);
            editor = window.ace.edit('new-adhoc-query-editor');
            editor.setTheme('ace/theme/textmate');
            editor.session.setMode('ace/mode/sql');
            editor.session.setUseWrapMode(true);
            editor.setOptions({
                fontSize: '13px',
                minLines: 14,
                maxLines: 24,
                showPrintMargin: false,
                tabSize: 4,
                useSoftTabs: true
            });
        } else {
            $('#new-adhoc-query-editor').addClass('d-none');
            fallback.removeClass('d-none');
        }

        modal.on('shown.bs.modal', function () {
            if (editor) {
                editor.resize();
                editor.focus();
            }
        });

        $('#save-adhoc-report').on('click', function () {
            var button = $(this);
            var error = $('#new-adhoc-error');
            var reportName = $.trim($('#new-adhoc-name').val());
            var reportTitle = $.trim($('#new-adhoc-title').val());
            var sectionId = $('#new-adhoc-section').val();
            var query = $.trim(editor ? editor.getValue() : fallback.val());

            error.addClass('d-none').find('span').empty();
            if (!reportName || !reportTitle || !sectionId || !query) {
                error.removeClass('d-none').find('span').text('Report name, report title, division, and SQL are required.');
                return;
            }

            button.prop('disabled', true).html('<i class="icon-spinner4 spinner mr-2"></i>Creating…');
            $.ajax({
                url: endpoint,
                type: 'POST',
                dataType: 'json',
                data: {
                    action: 'create',
                    requestToken: requestToken,
                    reportName: reportName,
                    reportTitle: reportTitle,
                    sectionId: sectionId,
                    adhocQuery: query
                }
            }).done(function (response) {
                window.location.assign(response.url);
            }).fail(function (xhr) {
                error.removeClass('d-none').find('span').text(readAjaxError(xhr, 'The Adhoc report could not be created.'));
                button.prop('disabled', false).html('<i class="icon-floppy-disk mr-2"></i>Create report');
            });
        });
    }

    function initializeEditModal(endpoint, requestToken, aceBasePath) {
        var modal = $('#edit-adhoc-report-modal');
        if (!modal.length) {
            return;
        }

        var editor;
        var fallback = $('#edit-adhoc-query-fallback');
        var initialQuery = fallback.val() || '';
        if (window.ace) {
            window.ace.config.set('basePath', aceBasePath);
            editor = window.ace.edit('edit-adhoc-query-editor');
            editor.setTheme('ace/theme/textmate');
            editor.session.setMode('ace/mode/sql');
            editor.session.setUseWrapMode(true);
            editor.setValue(initialQuery, -1);
            editor.setOptions({
                fontSize: '13px',
                minLines: 14,
                maxLines: 24,
                showPrintMargin: false,
                tabSize: 4,
                useSoftTabs: true
            });
        } else {
            $('#edit-adhoc-query-editor').addClass('d-none');
            fallback.removeClass('d-none');
        }

        modal.on('shown.bs.modal', function () {
            if (editor) {
                editor.resize();
                editor.focus();
            }
        });

        $('#save-edited-adhoc-report').on('click', function () {
            var button = $(this);
            var error = $('#edit-adhoc-error');
            var reportId = $('#edit-adhoc-report-id').val();
            var reportName = $.trim($('#edit-adhoc-name').val());
            var reportTitle = $.trim($('#edit-adhoc-title').val());
            var sectionId = $('#edit-adhoc-section').val();
            var sortOrder = $('#edit-adhoc-sort-order').val();
            var query = $.trim(editor ? editor.getValue() : fallback.val());

            error.addClass('d-none').find('span').empty();
            if (!reportId || !reportName || !reportTitle || !sectionId || !sortOrder || !query) {
                error.removeClass('d-none').find('span').text('Report name, report title, division, sort order, and SQL are required.');
                return;
            }

            button.prop('disabled', true).html('<i class="icon-spinner4 spinner mr-2"></i>Saving…');
            $.ajax({
                url: endpoint,
                type: 'POST',
                dataType: 'json',
                data: {
                    action: 'edit',
                    requestToken: requestToken,
                    reportId: reportId,
                    reportName: reportName,
                    reportTitle: reportTitle,
                    sectionId: sectionId,
                    sortOrder: sortOrder,
                    adhocQuery: query
                }
            }).done(function (response) {
                window.location.assign(response.url);
            }).fail(function (xhr) {
                error.removeClass('d-none').find('span').text(readAjaxError(xhr, 'The Adhoc report could not be updated.'));
                button.prop('disabled', false).html('<i class="icon-floppy-disk mr-2"></i>Save changes');
            });
        });
    }

    function initializeReportViewer(endpoint) {
        var app = $('#adhoc-report-app');
        if (!app.length) {
            return;
        }

        var reportId = parseInt(app.attr('data-report-id'), 10);
        var filterColumn = $('#adhoc-filter-column');
        var filterValue = $('#adhoc-filter-value');
        var clearFilter = $('#adhoc-clear-filter');
        var advancedButton = $('#adhoc-advanced-filter');
        var advancedModal = $('#adhoc-advanced-filter-modal');
        var errorPanel = $('#adhoc-error');
        var loadingPanel = $('#adhoc-loading');
        var tableContainer = $('#adhoc-table-container');
        var status = $('#adhoc-table-status');
        var columnSummary = $('#adhoc-column-summary');
        var dataCard = $('#adhoc-data-card');
        var insightsCard = $('#adhoc-insights-card');
        var groupColumn = $('#adhoc-group-column');
        var secondaryGroupColumn = $('#adhoc-secondary-group-column');
        var measureColumn = $('#adhoc-measure-column');
        var chartLibrary = $('#adhoc-chart-library');
        var chartType = $('#adhoc-chart-type');
        var groupLimit = $('#adhoc-group-limit');
        var insightError = $('#adhoc-insight-error');
        var insightLoading = $('#adhoc-insight-loading');
        var insightContent = $('#adhoc-insight-content');
        var insightEmpty = $('#adhoc-insight-empty');
        var allChartsModal = $('#adhoc-all-charts-modal');
        var metadata;
        var table;
        var advancedApplied = {};
        var currentInsights;
        var primaryChart;
        var modalCharts = [];
        var insightRequestNumber = 0;
        var insightReloadTimer;
        var insightAjax;
        var tableResizeTimer;
        var tableStateKey = 'ReportingSuite.AdhocReport.DataTable.' + reportId;

        function setCardAccessibility(card) {
            var isExpanded = !card.hasClass('card-collapsed');
            var header = card.children('.card-header');
            header.find('[data-action="collapse"], .adhoc-collapse-heading').attr('aria-expanded', String(isExpanded));
        }

        function resizeDataTable() {
            if (!table || dataCard.hasClass('card-collapsed')) {
                return;
            }
            table.columns.adjust();
            if (table.responsive && typeof table.responsive.recalc === 'function') {
                table.responsive.recalc();
            }
        }

        $('.adhoc-collapsible-card').each(function () {
            setCardAccessibility($(this));
        });

        $('.adhoc-collapse-heading').on('click.adhocCards keydown.adhocCards', function (event) {
            if (event.type === 'keydown' && event.key !== 'Enter' && event.key !== ' ') {
                return;
            }
            event.preventDefault();
            $(this).closest('.adhoc-collapsible-card')
                .children('.card-header')
                .find('[data-action="collapse"]')
                .first()
                .trigger('click');
        });

        $('.adhoc-collapsible-card [data-action="collapse"]').on('click.adhocCards', function () {
            var card = $(this).closest('.adhoc-collapsible-card');
            window.setTimeout(function () {
                setCardAccessibility(card);
                if (!card.hasClass('card-collapsed')) {
                    if (card.is(insightsCard)) {
                        if (primaryChart) {
                            primaryChart.resize();
                        } else {
                            renderPrimaryChart();
                        }
                    } else {
                        resizeDataTable();
                    }
                }
            }, 180);
        });

        dataCard.find('[data-action="fullscreen"]').on('click.adhocTable', function () {
            window.setTimeout(function () {
                if (dataCard.hasClass('fixed-top') && dataCard.hasClass('card-collapsed')) {
                    dataCard.removeClass('card-collapsed').children('.card-header').nextAll().stop(true, true).show();
                    dataCard.find('> .card-header [data-action="collapse"]').removeClass('rotate-180');
                    setCardAccessibility(dataCard);
                }
                resizeDataTable();
            }, 180);
        });

        function showError(message) {
            loadingPanel.addClass('d-none');
            errorPanel.removeClass('d-none').find('span').text(message || 'The report could not be loaded.');
            status.text('Report loading failed.');
        }

        function clearError() {
            errorPanel.addClass('d-none').find('span').empty();
        }

        function formatMetric(value, maximumFractionDigits) {
            var number = Number(value || 0);
            if (!isFinite(number)) {
                return '0';
            }
            return number.toLocaleString(undefined, {
                minimumFractionDigits: 0,
                maximumFractionDigits: maximumFractionDigits == null ? 2 : maximumFractionDigits
            });
        }

        function isNumericColumn(column) {
            var numericTypes = ['byte', 'sbyte', 'int16', 'uint16', 'int32', 'uint32', 'int64', 'uint64', 'single', 'double', 'decimal'];
            return $.inArray(String(column.dataType || '').toLowerCase(), numericTypes) !== -1;
        }

        function showInsightError(message) {
            insightLoading.addClass('d-none');
            insightContent.addClass('d-none');
            insightEmpty.addClass('d-none');
            insightError.removeClass('d-none').find('span').text(message || 'Report insights could not be loaded.');
            $('#adhoc-view-all-charts').prop('disabled', true);
        }

        function clearInsightError() {
            insightError.addClass('d-none').find('span').empty();
        }

        function initializeInsightControls(schema) {
            groupColumn.empty();
            secondaryGroupColumn.empty().append($('<option>').val('-1').text('None'));
            measureColumn.empty().append($('<option>').val('-1').text('Row count'));

            $.each(schema.columns || [], function (index, column) {
                groupColumn.append($('<option>').val(index).text(column.title));
                secondaryGroupColumn.append($('<option>').val(index).text(column.title));
                if (isNumericColumn(column)) {
                    measureColumn.append($('<option>').val(index).text('Sum of ' + column.title));
                }
            });

            var preferredGroup = 0;
            $.each(schema.columns || [], function (index, column) {
                if (!isNumericColumn(column)) {
                    preferredGroup = index;
                    return false;
                }
            });
            groupColumn.val(String(preferredGroup));
            secondaryGroupColumn.val('-1');
            measureColumn.val('-1');
            updateSecondaryGroupOptions();
            groupColumn.add(secondaryGroupColumn).add(measureColumn).add(chartLibrary).add(chartType).add(groupLimit).prop('disabled', false);
            $('#adhoc-refresh-insights').prop('disabled', false);
        }

        function updateSecondaryGroupOptions() {
            var primaryValue = groupColumn.val();
            secondaryGroupColumn.find('option').each(function () {
                var option = $(this);
                option.prop('disabled', option.val() !== '-1' && option.val() === primaryValue);
            });
            if (secondaryGroupColumn.val() === primaryValue) {
                secondaryGroupColumn.val('-1');
            }
        }

        function queueInsightReload() {
            window.clearTimeout(insightReloadTimer);
            insightReloadTimer = window.setTimeout(loadInsights, 450);
        }

        function insightRequestData() {
            return {
                action: 'insights',
                reportId: reportId,
                groupColumn: groupColumn.val(),
                secondaryGroupColumn: secondaryGroupColumn.val(),
                measureColumn: measureColumn.val(),
                limit: groupLimit.val(),
                search: table ? (table.search() || '') : '',
                filters: JSON.stringify(getActiveFilters())
            };
        }

        function updateInsightSummary(response) {
            var groups = response.groups || [];
            var leader = groups.length ? groups[0] : null;
            var hasSecondary = !!response.secondaryGroupColumnTitle;
            $('#adhoc-insight-rows').text(formatMetric(response.totalRows, 0));
            $('#adhoc-insight-groups').text(formatMetric(response.groupCount, 0));
            $('#adhoc-insight-secondary-groups').text(hasSecondary ?
                formatMetric(response.secondaryGroupCount, 0) + ' ' + response.secondaryGroupColumnTitle + ' groups' : '');
            $('#adhoc-insight-total-label').text(response.measureTitle || 'Total');
            $('#adhoc-insight-total').text(formatMetric(response.totalValue));
            $('#adhoc-insight-leader').text(leader ? leader.label : '—').attr('title', leader ? leader.label : '');
            $('#adhoc-insight-leader-value').text(leader ? formatMetric(leader.groupValue != null ? leader.groupValue : leader.value) +
                ' ' + (response.measureTitle || '').toLowerCase() : '');

            var caption = libraryDisplayName(chartLibrary.val()) + ' · Showing ' + formatMetric(response.displayedGroupCount, 0) + ' of ' +
                formatMetric(response.groupCount, 0) + ' ' + (response.groupCount === 1 ? 'group' : 'groups') +
                ' by ' + response.groupColumnTitle;
            if (hasSecondary) {
                caption += ' and ' + formatMetric(response.displayedSecondaryGroupCount, 0) + ' of ' +
                    formatMetric(response.secondaryGroupCount, 0) + ' ' + response.secondaryGroupColumnTitle + ' series';
            }
            caption += ', using the current report filters and search.';
            if (response.isTruncated) {
                caption += ' Increase Top groups to display more.';
            }
            $('#adhoc-chart-caption').text(caption);
            var groupingDescription = response.groupColumnTitle + (hasSecondary ? ' and ' + response.secondaryGroupColumnTitle : '');
            $('#adhoc-insights-subtitle').text((response.measureTitle || 'Row count') + ' grouped by ' + groupingDescription + '.');
            $('#adhoc-all-charts-subtitle').text((response.measureTitle || 'Row count') + ' grouped by ' + groupingDescription +
                ' · ' + formatMetric(response.totalRows, 0) + ' matching rows · ' + libraryDisplayName(chartLibrary.val()));
        }

        var chartPalette = ['#2ec7c9', '#b6a2de', '#5ab1ef', '#ffb980', '#d87a80', '#8d98b3', '#e5cf0d', '#97b552', '#95706d', '#dc69aa'];

        function libraryDisplayName(value) {
            return value === 'd3' ? 'D3' : (value === 'c3' ? 'C3' : 'ECharts');
        }

        function chartData(response) {
            var groups = response.groups || [];
            var hasSecondary = !!response.secondaryGroupColumnTitle;
            var labels = [];
            var secondaryLabels = [];
            var primaryLookup = {};
            var combinationLookup = {};
            var flatGroups = [];

            $.each(groups, function (_, group) {
                var primaryLabel = String(group.label == null ? '(Blank)' : group.label);
                var secondaryLabel = String(group.secondaryLabel == null ? '' : group.secondaryLabel);
                if ($.inArray(primaryLabel, labels) === -1) {
                    labels.push(primaryLabel);
                }
                if (hasSecondary && $.inArray(secondaryLabel, secondaryLabels) === -1) {
                    secondaryLabels.push(secondaryLabel);
                }
                primaryLookup[primaryLabel] = {
                    value: Number(group.groupValue != null ? group.groupValue : group.value || 0),
                    rowCount: Number(group.groupRowCount != null ? group.groupRowCount : group.rowCount || 0)
                };
                combinationLookup[primaryLabel + '\u0001' + secondaryLabel] = {
                    value: Number(group.value || 0),
                    rowCount: Number(group.rowCount || 0)
                };
                flatGroups.push({
                    label: hasSecondary ? primaryLabel + ' · ' + secondaryLabel : primaryLabel,
                    primaryLabel: primaryLabel,
                    secondaryLabel: secondaryLabel,
                    value: Number(group.value || 0),
                    rowCount: Number(group.rowCount || 0)
                });
            });

            if (!hasSecondary) {
                secondaryLabels = [response.measureTitle || 'Row count'];
            }

            var series = $.map(secondaryLabels, function (secondaryLabel) {
                return {
                    name: secondaryLabel,
                    values: $.map(labels, function (primaryLabel) {
                        var combination = hasSecondary ? combinationLookup[primaryLabel + '\u0001' + secondaryLabel] : primaryLookup[primaryLabel];
                        return combination ? combination.value : 0;
                    }),
                    rowCounts: $.map(labels, function (primaryLabel) {
                        var combination = hasSecondary ? combinationLookup[primaryLabel + '\u0001' + secondaryLabel] : primaryLookup[primaryLabel];
                        return combination ? combination.rowCount : 0;
                    })
                };
            });

            var values = $.map(labels, function (label) { return primaryLookup[label] ? primaryLookup[label].value : 0; });
            var rowCounts = $.map(labels, function (label) { return primaryLookup[label] ? primaryLookup[label].rowCount : 0; });
            var countMeasure = measureColumn.val() === '-1';
            return {
                groups: groups,
                labels: labels,
                secondaryLabels: secondaryLabels,
                hasSecondary: hasSecondary,
                series: series,
                flatGroups: flatGroups,
                values: values,
                rowCounts: rowCounts,
                lineName: countMeasure ? 'Overall share of rows' : 'Overall row count',
                lineValues: countMeasure ? $.map(rowCounts, function (count) {
                    return response.totalRows ? Number((count * 100 / response.totalRows).toFixed(1)) : 0;
                }) : rowCounts,
                lineIsPercent: countMeasure,
                measureTitle: response.measureTitle || 'Row count'
            };
        }

        function tooltipFormatter(params) {
            var entries = $.isArray(params) ? params : [params];
            if (!entries.length) {
                return '';
            }
            var first = entries[0];
            var heading = first.axisValue || first.name || '';
            var output = '<strong>' + htmlEncode(heading) + '</strong>';
            $.each(entries, function (_, entry) {
                var suffix = String(entry.seriesName || '').toLowerCase().indexOf('share of rows') !== -1 ? '%' : '';
                var rawValue = entry.value && entry.value.value != null ? entry.value.value : entry.value;
                output += '<br>' + (entry.marker || '') + htmlEncode(entry.seriesName || '') + ': ' +
                    htmlEncode(formatMetric(rawValue, suffix ? 1 : 2) + suffix);
            });
            if (first.percent != null) {
                output += '<br>' + htmlEncode(formatMetric(first.percent, 1) + '% of displayed total');
            }
            return output;
        }

        function abbreviatedLabel(value) {
            var textValue = String(value == null ? '' : value);
            return textValue.length > 24 ? textValue.substring(0, 22) + '…' : textValue;
        }

        function disposeRenderedChart(element) {
            if (!element || !element.adhocChartHandle) {
                return;
            }
            element.adhocChartHandle.dispose();
            element.adhocChartHandle = null;
        }

        function renderEChart(element, type, response) {
            if (!window.echarts) {
                return null;
            }

            var data = chartData(response);
            var chart = window.echarts.init(element);
            var fileName = String(response.reportTitle || 'Adhoc-report').replace(/[\\/:*?"<>|]+/g, '-');
            var common = {
                animationDuration: 450,
                color: chartPalette,
                textStyle: { fontFamily: 'Roboto, Arial, Verdana, sans-serif', fontSize: 12 },
                grid: { left: 24, right: 28, top: 45, bottom: 58, containLabel: true },
                tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' }, formatter: tooltipFormatter },
                toolbox: {
                    right: 8,
                    feature: {
                        restore: { title: 'Restore' },
                        saveAsImage: { title: 'Save image', name: fileName + '-' + type }
                    }
                }
            };

            if (type === 'pie' || type === 'donut') {
                common.tooltip = { trigger: 'item', formatter: tooltipFormatter };
                common.legend = { type: 'scroll', orient: 'horizontal', left: 'center', bottom: 0, show: !data.hasSecondary, data: data.labels };
                if (data.hasSecondary) {
                    common.series = [{
                        name: response.groupColumnTitle,
                        type: 'pie',
                        selectedMode: 'single',
                        radius: type === 'donut' ? ['18%', '36%'] : [0, '34%'],
                        center: ['50%', '50%'],
                        label: { normal: { position: 'inner', formatter: '{b}' } },
                        itemStyle: { normal: { borderWidth: 1, borderColor: '#fff' } },
                        data: $.map(data.labels, function (label, index) { return { name: label, value: Math.max(0, data.values[index]) }; })
                    }, {
                        name: response.secondaryGroupColumnTitle,
                        type: 'pie',
                        radius: type === 'donut' ? ['43%', '70%'] : ['38%', '70%'],
                        center: ['50%', '50%'],
                        minAngle: 1,
                        label: { normal: { show: data.flatGroups.length <= 16, formatter: '{b}' } },
                        itemStyle: { normal: { borderWidth: 1, borderColor: '#fff' } },
                        data: $.map(data.flatGroups, function (group) { return { name: group.label, value: Math.max(0, group.value) }; })
                    }];
                } else {
                    common.series = [{
                        name: data.measureTitle,
                        type: 'pie',
                        radius: type === 'donut' ? ['42%', '68%'] : '68%',
                        center: ['50%', '46%'],
                        minAngle: 2,
                        itemStyle: { normal: { borderWidth: 1, borderColor: '#fff' } },
                        label: { normal: { formatter: '{b}: {d}%' } },
                        data: $.map(data.flatGroups, function (group) { return { name: group.label, value: Math.max(0, group.value) }; })
                    }];
                }
            } else if (type === 'funnel') {
                common.tooltip = { trigger: 'item', formatter: tooltipFormatter };
                common.legend = { type: 'scroll', orient: 'vertical', left: 0, top: 'middle', show: data.flatGroups.length <= 16, data: $.map(data.flatGroups, function (group) { return group.label; }) };
                common.series = [{
                    name: data.measureTitle,
                    type: 'funnel',
                    left: '22%',
                    right: '8%',
                    top: 28,
                    bottom: 20,
                    minSize: '8%',
                    maxSize: '92%',
                    sort: 'descending',
                    gap: 2,
                    label: { normal: { show: true, position: 'inside', formatter: '{b}' } },
                    itemStyle: { normal: { borderColor: '#fff', borderWidth: 1 } },
                    data: $.map(data.flatGroups, function (group) { return { name: group.label, value: Math.max(0, group.value) }; })
                }];
            } else if (type === 'bar') {
                common.grid.left = 30;
                common.grid.bottom = 25;
                common.xAxis = { type: 'value', name: data.measureTitle, nameLocation: 'middle', nameGap: 34, splitLine: { lineStyle: { color: '#eeeeee' } } };
                common.yAxis = { type: 'category', inverse: true, data: data.labels, axisLabel: { formatter: abbreviatedLabel, interval: 0 } };
                common.series = $.map(data.series, function (series) {
                    return { name: series.name, type: 'bar', stack: data.hasSecondary ? 'groupTotal' : null, data: series.values, barMaxWidth: 30 };
                });
                if (data.hasSecondary) {
                    common.legend = { type: 'scroll', top: 8, data: data.secondaryLabels };
                    common.grid.top = 65;
                }
            } else {
                common.xAxis = { type: 'category', data: data.labels, axisLabel: { formatter: abbreviatedLabel, interval: 0, rotate: data.labels.length > 6 ? 32 : 0 } };
                common.yAxis = [{ type: 'value', name: data.measureTitle, splitLine: { lineStyle: { color: '#eeeeee' } } }];

                if (type === 'line' || type === 'area') {
                    common.tooltip.axisPointer.type = 'line';
                    common.series = $.map(data.series, function (series) {
                        return {
                            name: series.name,
                            type: 'line',
                            stack: type === 'area' && data.hasSecondary ? 'groupTotal' : null,
                            data: series.values,
                            smooth: true,
                            symbolSize: 7,
                            areaStyle: type === 'area' ? { normal: { opacity: 0.28 } } : null
                        };
                    });
                } else {
                    common.series = $.map(data.series, function (series) {
                        return { name: series.name, type: 'bar', data: series.values, barMaxWidth: 42 };
                    });
                }

                if (data.hasSecondary) {
                    common.legend = { type: 'scroll', top: 8, data: data.secondaryLabels };
                    common.grid.top = 65;
                }

                if (type === 'mixed') {
                    common.yAxis.push({
                        type: 'value',
                        name: data.lineIsPercent ? 'Share' : 'Rows',
                        min: 0,
                        max: data.lineIsPercent ? 100 : null,
                        minInterval: data.lineIsPercent ? null : 1,
                        axisLabel: data.lineIsPercent ? { formatter: '{value}%' } : {},
                        splitLine: { show: false }
                    });
                    common.legend = { type: 'scroll', top: 8, data: data.secondaryLabels.concat([data.lineName]) };
                    common.grid.top = 65;
                    common.series.push({ name: data.lineName, type: 'line', yAxisIndex: 1, data: data.lineValues, smooth: true, symbolSize: 7 });
                }
            }

            chart.setOption(common, true);
            chart.resize();
            return {
                resize: function () { chart.resize(); },
                dispose: function () { chart.dispose(); }
            };
        }

        function renderC3Chart(element, type, response) {
            if (!window.c3 || !window.d3) {
                return null;
            }
            if (type === 'funnel') {
                return renderD3Chart(element, type, response);
            }

            var data = chartData(response);
            var config = {
                bindto: element,
                size: { height: element.clientHeight || 430 },
                color: { pattern: chartPalette },
                transition: { duration: 350 },
                padding: { top: 15, right: 28, bottom: 10, left: 55 },
                legend: { position: (type === 'pie' || type === 'donut') ? 'bottom' : 'right' },
                tooltip: { format: { value: function (value) { return formatMetric(value); } } }
            };

            if (type === 'pie' || type === 'donut') {
                config.data = {
                    type: type,
                    columns: $.map(data.flatGroups, function (group) {
                        return [group.label, Math.max(0, group.value)];
                    })
                };
                if (type === 'donut') {
                    config.donut = { title: data.measureTitle, width: 42, label: { show: true } };
                }
            } else {
                config.data = {
                    columns: $.map(data.series, function (series) { return [series.name].concat(series.values); }),
                    type: type === 'area' ? 'area-spline' : (type === 'column' || type === 'bar' ? 'bar' : 'line')
                };
                if (type === 'area' && data.hasSecondary) {
                    config.data.groups = [data.secondaryLabels];
                }
                config.axis = {
                    rotated: type === 'bar',
                    x: { type: 'category', categories: data.labels, tick: { rotate: data.labels.length > 6 ? 32 : 0, multiline: false, culling: { max: 12 } } },
                    y: { label: { text: data.measureTitle, position: 'outer-middle' } }
                };
                config.point = { show: type === 'line' || type === 'area' || type === 'mixed' };

                if (type === 'mixed') {
                    config.data.columns.push([data.lineName].concat(data.lineValues));
                    config.data.types = {};
                    $.each(data.series, function (_, series) { config.data.types[series.name] = 'bar'; });
                    config.data.types[data.lineName] = 'spline';
                    config.data.axes = {};
                    config.data.axes[data.lineName] = 'y2';
                    config.axis.y2 = { show: true, max: data.lineIsPercent ? 100 : undefined, label: { text: data.lineIsPercent ? 'Share (%)' : 'Rows', position: 'outer-middle' } };
                }
            }

            var chart = window.c3.generate(config);
            return {
                resize: function () { chart.resize({ height: element.clientHeight || 430 }); },
                dispose: function () { chart.destroy(); }
            };
        }

        function renderD3Chart(element, type, response) {
            if (!window.d3) {
                return null;
            }
            var data = chartData(response);
            var d3 = window.d3;

            function draw() {
                d3.select(element).selectAll('*').remove();
                var width = Math.max(360, element.clientWidth || 760);
                var height = Math.max(300, element.clientHeight || 430);
                var margin = { top: data.hasSecondary ? 62 : 28, right: type === 'mixed' ? 62 : 28, bottom: 86, left: type === 'bar' ? 155 : 68 };
                var innerWidth = Math.max(100, width - margin.left - margin.right);
                var innerHeight = Math.max(100, height - margin.top - margin.bottom);
                var svg = d3.select(element).append('svg').attr('class', 'adhoc-d3-chart').attr('width', width).attr('height', height);
                var plot = svg.append('g').attr('transform', 'translate(' + margin.left + ',' + margin.top + ')');
                var colors = d3.scale.ordinal().range(chartPalette);
                var points = $.map(data.labels, function (label, index) {
                    return { label: label, value: data.values[index], lineValue: data.lineValues[index] };
                });
                var flatPoints = $.map(data.flatGroups, function (group) {
                    return { label: group.label, value: group.value, rowCount: group.rowCount };
                });
                var seriesPoints = $.map(data.series, function (series, seriesIndex) {
                    return {
                        name: series.name,
                        index: seriesIndex,
                        points: $.map(data.labels, function (label, pointIndex) {
                            return { label: label, seriesName: series.name, value: series.values[pointIndex] };
                        })
                    };
                });
                var allSeriesValues = [];
                $.each(data.series, function (_, series) { allSeriesValues = allSeriesValues.concat(series.values); });

                if (data.hasSecondary && type !== 'pie' && type !== 'donut' && type !== 'funnel') {
                    var legendColumns = Math.min(5, data.secondaryLabels.length);
                    var legendWidth = Math.max(90, innerWidth / Math.max(1, legendColumns));
                    var legend = svg.append('g').attr('class', 'adhoc-d3-legend').attr('transform', 'translate(' + margin.left + ',14)');
                    var legendItems = legend.selectAll('.adhoc-d3-legend-item').data(data.secondaryLabels).enter().append('g')
                        .attr('class', 'adhoc-d3-legend-item').attr('transform', function (_, index) {
                            return 'translate(' + ((index % legendColumns) * legendWidth) + ',' + (Math.floor(index / legendColumns) * 18) + ')';
                        });
                    legendItems.append('rect').attr('width', 10).attr('height', 10).attr('rx', 1).style('fill', function (_, index) { return colors(index); });
                    legendItems.append('text').attr('x', 15).attr('y', 9).text(function (label) {
                        return String(label).length > 15 ? String(label).substring(0, 14) + '…' : label;
                    });
                    legendItems.append('title').text(function (label) { return label; });
                }

                if (type === 'pie' || type === 'donut') {
                    var radius = Math.max(70, Math.min(innerWidth, innerHeight) / 2);
                    var pie = d3.layout.pie().sort(null).value(function (item) { return Math.max(0, item.value); });
                    var arc = d3.svg.arc().outerRadius(radius).innerRadius(type === 'donut' ? radius * 0.58 : 0);
                    var arcGroup = plot.append('g').attr('transform', 'translate(' + (innerWidth / 2) + ',' + (innerHeight / 2) + ')');
                    var slices = arcGroup.selectAll('.adhoc-d3-slice').data(pie(flatPoints)).enter().append('path')
                        .attr('class', 'adhoc-d3-slice').attr('d', arc).style('fill', function (_, index) { return colors(index); });
                    slices.append('title').text(function (item) { return item.data.label + ': ' + formatMetric(item.data.value); });
                    arcGroup.selectAll('.adhoc-d3-pie-label').data(pie(flatPoints)).enter().append('text')
                        .attr('class', 'adhoc-d3-pie-label').attr('transform', function (item) { return 'translate(' + arc.centroid(item) + ')'; })
                        .attr('text-anchor', 'middle').text(function (item) { return item.endAngle - item.startAngle > 0.22 ? abbreviatedLabel(item.data.label) : ''; });
                    return;
                }

                if (type === 'funnel') {
                    var positivePoints = $.map(flatPoints, function (point) {
                        return { label: point.label, value: Math.max(0, point.value) };
                    });
                    var maximum = d3.max(positivePoints, function (point) { return point.value; }) || 1;
                    var segmentHeight = innerHeight / Math.max(1, positivePoints.length);
                    var widthScale = d3.scale.linear().domain([0, maximum]).range([innerWidth * 0.12, innerWidth]);
                    var segments = plot.selectAll('.adhoc-d3-funnel-segment').data(positivePoints).enter().append('g');
                    segments.append('path').attr('class', 'adhoc-d3-funnel-segment').attr('d', function (point, index) {
                        var topWidth = widthScale(point.value);
                        var nextValue = index + 1 < positivePoints.length ? positivePoints[index + 1].value : point.value * 0.55;
                        var bottomWidth = widthScale(nextValue);
                        var yTop = index * segmentHeight;
                        var yBottom = yTop + segmentHeight - 2;
                        return 'M' + ((innerWidth - topWidth) / 2) + ',' + yTop + 'L' + ((innerWidth + topWidth) / 2) + ',' + yTop +
                            'L' + ((innerWidth + bottomWidth) / 2) + ',' + yBottom + 'L' + ((innerWidth - bottomWidth) / 2) + ',' + yBottom + 'Z';
                    }).style('fill', function (_, index) { return colors(index); });
                    segments.append('text').attr('class', 'adhoc-d3-funnel-label').attr('x', innerWidth / 2)
                        .attr('y', function (_, index) { return index * segmentHeight + segmentHeight / 2; }).attr('dy', '.35em')
                        .attr('text-anchor', 'middle').text(function (point) { return abbreviatedLabel(point.label) + ' · ' + formatMetric(point.value); });
                    segments.append('title').text(function (point) { return point.label + ': ' + formatMetric(point.value); });
                    return;
                }

                if (type === 'bar') {
                    var categoryY = d3.scale.ordinal().domain(data.labels).rangeRoundBands([0, innerHeight], 0.16);
                    var secondaryY = d3.scale.ordinal().domain(data.secondaryLabels).rangeRoundBands([0, categoryY.rangeBand()], 0.08);
                    var minX = Math.min(0, d3.min(allSeriesValues) || 0);
                    var maxX = Math.max(0, d3.max(allSeriesValues) || 0);
                    if (minX === maxX) { maxX = minX + 1; }
                    var valueX = d3.scale.linear().domain([minX, maxX]).nice().range([0, innerWidth]);
                    plot.append('g').attr('class', 'adhoc-d3-axis adhoc-d3-x-axis').attr('transform', 'translate(0,' + innerHeight + ')').call(d3.svg.axis().scale(valueX).orient('bottom').ticks(6));
                    plot.append('g').attr('class', 'adhoc-d3-axis adhoc-d3-y-axis').call(d3.svg.axis().scale(categoryY).orient('left').tickFormat(abbreviatedLabel));
                    $.each(seriesPoints, function (_, series) {
                        var horizontalBars = plot.selectAll('.adhoc-d3-bar-' + series.index).data(series.points).enter().append('rect').attr('class', 'adhoc-d3-bar')
                            .attr('x', function (point) { return valueX(Math.min(0, point.value)); })
                            .attr('y', function (point) { return categoryY(point.label) + secondaryY(series.name); })
                            .attr('width', function (point) { return Math.abs(valueX(point.value) - valueX(0)); })
                            .attr('height', secondaryY.rangeBand()).style('fill', colors(series.index));
                        horizontalBars.append('title').text(function (point) { return point.label + ' · ' + series.name + ': ' + formatMetric(point.value); });
                    });
                    return;
                }

                var categoryX = d3.scale.ordinal().domain(data.labels).rangeRoundBands([0, innerWidth], 0.16);
                var secondaryX = d3.scale.ordinal().domain(data.secondaryLabels).rangeRoundBands([0, categoryX.rangeBand()], 0.08);
                var minY = Math.min(0, d3.min(allSeriesValues) || 0);
                var maxY = Math.max(0, d3.max(allSeriesValues) || 0);
                if (minY === maxY) { maxY = minY + 1; }
                var valueY = d3.scale.linear().domain([minY, maxY]).nice().range([innerHeight, 0]);
                var xAxis = plot.append('g').attr('class', 'adhoc-d3-axis adhoc-d3-x-axis').attr('transform', 'translate(0,' + innerHeight + ')').call(d3.svg.axis().scale(categoryX).orient('bottom').tickFormat(abbreviatedLabel));
                if (data.labels.length > 6) {
                    xAxis.selectAll('text').style('text-anchor', 'end').attr('dx', '-.7em').attr('dy', '.15em').attr('transform', 'rotate(-32)');
                }
                plot.append('g').attr('class', 'adhoc-d3-axis adhoc-d3-y-axis').call(d3.svg.axis().scale(valueY).orient('left').ticks(6));

                if (type === 'column' || type === 'mixed') {
                    $.each(seriesPoints, function (_, series) {
                        var columns = plot.selectAll('.adhoc-d3-column-' + series.index).data(series.points).enter().append('rect').attr('class', 'adhoc-d3-column')
                            .attr('x', function (point) { return categoryX(point.label) + secondaryX(series.name); }).attr('width', secondaryX.rangeBand())
                            .attr('y', function (point) { return valueY(Math.max(0, point.value)); })
                            .attr('height', function (point) { return Math.abs(valueY(point.value) - valueY(0)); }).style('fill', colors(series.index));
                        columns.append('title').text(function (point) { return point.label + ' · ' + series.name + ': ' + formatMetric(point.value); });
                    });
                }

                if (type === 'line' || type === 'area') {
                    var line = d3.svg.line().x(function (point) { return categoryX(point.label) + categoryX.rangeBand() / 2; }).y(function (point) { return valueY(point.value); }).interpolate('monotone');
                    var area = d3.svg.area().x(function (point) { return categoryX(point.label) + categoryX.rangeBand() / 2; })
                        .y0(valueY(0)).y1(function (point) { return valueY(point.value); }).interpolate('monotone');
                    $.each(seriesPoints, function (_, series) {
                        if (type === 'area') {
                            plot.append('path').datum(series.points).attr('class', 'adhoc-d3-area').attr('d', area)
                                .style('fill', colors(series.index)).style('opacity', data.hasSecondary ? 0.18 : 0.24);
                        }
                        plot.append('path').datum(series.points).attr('class', 'adhoc-d3-line').attr('d', line).style('stroke', colors(series.index));
                        var dots = plot.selectAll('.adhoc-d3-dot-' + series.index).data(series.points).enter().append('circle').attr('class', 'adhoc-d3-dot')
                            .attr('cx', function (point) { return categoryX(point.label) + categoryX.rangeBand() / 2; })
                            .attr('cy', function (point) { return valueY(point.value); }).attr('r', 4).style('stroke', colors(series.index));
                        dots.append('title').text(function (point) { return point.label + ' · ' + series.name + ': ' + formatMetric(point.value); });
                    });
                }

                if (type === 'mixed') {
                    var lineMaximum = Math.max(1, d3.max(data.lineValues) || 0);
                    var lineY = d3.scale.linear().domain([0, data.lineIsPercent ? 100 : lineMaximum]).nice().range([innerHeight, 0]);
                    plot.append('g').attr('class', 'adhoc-d3-axis adhoc-d3-y2-axis').attr('transform', 'translate(' + innerWidth + ',0)')
                        .call(d3.svg.axis().scale(lineY).orient('right').ticks(6).tickFormat(function (value) { return formatMetric(value, data.lineIsPercent ? 0 : 0) + (data.lineIsPercent ? '%' : ''); }));
                    var mixedLine = d3.svg.line().x(function (point) { return categoryX(point.label) + categoryX.rangeBand() / 2; }).y(function (point) { return lineY(point.lineValue); }).interpolate('monotone');
                    plot.append('path').datum(points).attr('class', 'adhoc-d3-line adhoc-d3-mixed-line').attr('d', mixedLine);
                }
            }

            draw();
            return {
                resize: draw,
                dispose: function () { window.d3.select(element).selectAll('*').remove(); }
            };
        }

        function renderChart(elementId, type, response) {
            var element = document.getElementById(elementId);
            if (!element) {
                return null;
            }
            disposeRenderedChart(element);
            $(element).empty().removeClass('adhoc-chart-unavailable');

            var library = chartLibrary.val() || 'echarts';
            var handle = library === 'd3' ? renderD3Chart(element, type, response) :
                (library === 'c3' ? renderC3Chart(element, type, response) : renderEChart(element, type, response));
            if (!handle) {
                $(element).addClass('adhoc-chart-unavailable').html('<div><i class="icon-warning22"></i><span>' + htmlEncode(libraryDisplayName(library)) + ' is unavailable.</span></div>');
                handle = { resize: function () {}, dispose: function () { $(element).empty(); } };
            }
            element.adhocChartHandle = handle;
            return handle;
        }

        function renderPrimaryChart() {
            if (!currentInsights || insightsCard.hasClass('card-collapsed')) {
                return;
            }
            primaryChart = renderChart('adhoc-primary-chart', chartType.val(), currentInsights);
        }

        function renderAllCharts() {
            if (!currentInsights) {
                return;
            }
            modalCharts = [
                renderChart('adhoc-modal-line-chart', 'line', currentInsights),
                renderChart('adhoc-modal-area-chart', 'area', currentInsights),
                renderChart('adhoc-modal-bar-chart', 'bar', currentInsights),
                renderChart('adhoc-modal-column-chart', 'column', currentInsights),
                renderChart('adhoc-modal-mixed-chart', 'mixed', currentInsights),
                renderChart('adhoc-modal-pie-chart', 'pie', currentInsights),
                renderChart('adhoc-modal-donut-chart', 'donut', currentInsights),
                renderChart('adhoc-modal-funnel-chart', 'funnel', currentInsights)
            ];
            window.setTimeout(function () {
                $.each(modalCharts, function (_, chart) { if (chart) { chart.resize(); } });
            }, 50);
        }

        function loadInsights() {
            if (!metadata || groupColumn.val() === '') {
                return;
            }

            if (insightAjax) {
                insightAjax.abort();
            }
            var requestNumber = ++insightRequestNumber;
            clearInsightError();
            insightEmpty.addClass('d-none');
            insightContent.addClass('d-none');
            insightLoading.removeClass('d-none');
            $('#adhoc-view-all-charts').prop('disabled', true);

            insightAjax = $.ajax({
                url: endpoint,
                type: 'POST',
                dataType: 'json',
                data: insightRequestData()
            }).done(function (response) {
                if (requestNumber !== insightRequestNumber) {
                    return;
                }
                currentInsights = response;
                insightLoading.addClass('d-none');
                if (!response.groups || !response.groups.length) {
                    insightEmpty.removeClass('d-none');
                    return;
                }
                updateInsightSummary(response);
                insightContent.removeClass('d-none');
                $('#adhoc-view-all-charts').prop('disabled', false);
                renderPrimaryChart();
            }).fail(function (xhr, statusText) {
                if (requestNumber !== insightRequestNumber) {
                    return;
                }
                if (statusText === 'abort') {
                    return;
                }
                currentInsights = null;
                showInsightError(readAjaxError(xhr, 'Report insights could not be loaded.'));
            }).always(function () {
                if (requestNumber === insightRequestNumber) {
                    insightAjax = null;
                }
            });
        }

        function initializeRemoteSelect(select, columnIndex, dropdownParent) {
            if (select.hasClass('select2-hidden-accessible')) {
                select.select2('destroy');
            }
            select.empty().prop('disabled', false).select2({
                width: '100%',
                multiple: true,
                placeholder: 'All values',
                allowClear: true,
                closeOnSelect: false,
                dropdownParent: dropdownParent,
                ajax: {
                    url: endpoint,
                    type: 'POST',
                    dataType: 'json',
                    delay: 300,
                    cache: true,
                    data: function (params) {
                        return {
                            action: 'distinct',
                            reportId: reportId,
                            column: columnIndex,
                            term: params.term || ''
                        };
                    },
                    processResults: function (response) {
                        return {
                            results: $.map(response.values || [], function (item) {
                                return { id: item.value, text: item.label };
                            })
                        };
                    }
                }
            });
        }

        function addFilter(target, columnIndex, values) {
            if (!values || !values.length) {
                return;
            }
            var key = String(columnIndex);
            target[key] = target[key] || [];
            $.each(values, function (_, value) {
                if ($.inArray(value, target[key]) === -1) {
                    target[key].push(value);
                }
            });
        }

        function getActiveFilters() {
            var combined = {};
            var basicColumn = filterColumn.val();
            if (basicColumn !== '') {
                addFilter(combined, parseInt(basicColumn, 10), filterValue.val() || []);
            }
            $.each(advancedApplied, function (columnIndex, values) {
                addFilter(combined, parseInt(columnIndex, 10), values);
            });

            return $.map(combined, function (values, columnIndex) {
                return { columnIndex: parseInt(columnIndex, 10), values: values };
            });
        }

        function updateFilterSummary() {
            var filters = getActiveFilters();
            var summary = $('#adhoc-active-filters').empty();
            if (!filters.length) {
                summary.text('No column filters applied.');
                return;
            }

            summary.append($('<span>').addClass('mr-2').text('Active filters:'));
            $.each(filters, function (_, filter) {
                var title = metadata.columns[filter.columnIndex].title;
                summary.append($('<span>').addClass('badge badge-light border mr-1').text(title + ' (' + filter.values.length + ')'));
            });
        }

        function reloadTable() {
            updateFilterSummary();
            if (table) {
                table.ajax.reload();
            }
            queueInsightReload();
        }

        function buildAdvancedFilters() {
            var container = $('#adhoc-advanced-filter-fields').empty();
            $.each(metadata.columns || [], function (index, column) {
                var select = $('<select multiple="multiple" class="form-control adhoc-advanced-select" data-no-select2="true"></select>')
                    .attr('data-column-index', index)
                    .attr('aria-label', column.title);
                var field = $('<div class="col-md-6 mb-3"></div>')
                    .append($('<label class="font-weight-semibold d-block"></label>').text(column.title))
                    .append(select);
                container.append(field);
                initializeRemoteSelect(select, index, advancedModal);
            });
        }

        function initializeTable(schema) {
            var tableElement = $('#adhoc-report-table');
            var header = $('<thead><tr></tr></thead>');
            var columns = [];
            var buttonRegistry = $.fn.dataTable.ext && $.fn.dataTable.ext.buttons ? $.fn.dataTable.ext.buttons : {};
            var tableButtons = [];

            metadata = schema;
            filterColumn.empty().append($('<option>').val('').text('Select a column'));
            $.each(schema.columns || [], function (index, column) {
                header.find('tr').append($('<th>').text(column.title));
                columns.push({
                    data: index,
                    name: column.name,
                    className: isNumericColumn(column) ? 'adhoc-cell-number' : '',
                    defaultContent: '',
                    render: function (data, type) {
                        if (data == null) {
                            return type === 'display' ? '<span class="text-muted">—</span>' : '';
                        }
                        return type === 'display' ? htmlEncode(data) : data;
                    },
                    createdCell: function (cell, value) {
                        var text = value == null ? '' : String(value);
                        if (text.length > 28) {
                            $(cell).attr('title', text);
                        }
                    }
                });
                filterColumn.append($('<option>').val(index).text(column.title));
            });

            if (buttonRegistry.colvis) {
                tableButtons.push({
                    extend: 'colvis',
                    text: '<i class="icon-columns mr-1"></i>Columns',
                    titleAttr: 'Choose visible columns'
                });
            }
            if (buttonRegistry.copyHtml5) {
                tableButtons.push({
                    extend: 'copyHtml5',
                    text: '<i class="icon-copy3 mr-1"></i>Copy page',
                    titleAttr: 'Copy the currently loaded page',
                    exportOptions: {
                        columns: ':visible',
                        modifier: { page: 'current', search: 'applied', order: 'applied' }
                    }
                });
            }
            if (buttonRegistry.print) {
                tableButtons.push({
                    extend: 'print',
                    text: '<i class="icon-printer2 mr-1"></i>Print page',
                    titleAttr: 'Print the currently loaded page',
                    exportOptions: {
                        columns: ':visible',
                        modifier: { page: 'current', search: 'applied', order: 'applied' }
                    }
                });
            }
            tableButtons.push({
                text: '<i class="icon-reset mr-1"></i>Reset view',
                titleAttr: 'Clear search, sorting, paging, and hidden-column preferences',
                action: function (event, dataTable) {
                    try { window.localStorage.removeItem(tableStateKey); } catch (ignore) { }
                    dataTable.state.clear();
                    dataTable.search('');
                    dataTable.columns().visible(true, false);
                    dataTable.order([[0, 'asc']]);
                    dataTable.page.len(25);
                    dataTable.page('first');
                    dataTable.columns.adjust().draw();
                }
            });

            tableElement.empty().append(header);
            columnSummary.text(schema.columns.length.toLocaleString() + (schema.columns.length === 1 ? ' column' : ' columns'));
            filterColumn.prop('disabled', false);
            advancedButton.prop('disabled', false);
            clearFilter.prop('disabled', false);
            $('#adhoc-export-csv, #adhoc-export-excel').prop('disabled', false);
            loadingPanel.addClass('d-none');
            tableContainer.removeClass('d-none');
            buildAdvancedFilters();

            table = tableElement.DataTable({
                processing: true,
                serverSide: true,
                deferRender: true,
                autoWidth: false,
                scrollX: true,
                scrollY: Math.max(300, Math.min(560, window.innerHeight - 360)) + 'px',
                scrollCollapse: true,
                responsive: !!($.fn.dataTable.Responsive),
                keys: !!($.fn.dataTable.KeyTable),
                fixedHeader: false,
                scroller: false,
                searchDelay: 450,
                pageLength: 25,
                lengthMenu: [[10, 25, 50, 100, 200], [10, 25, 50, 100, 200]],
                pagingType: 'full_numbers',
                orderMulti: true,
                orderClasses: false,
                stateSave: true,
                stateDuration: 0,
                stateSaveCallback: function (settings, state) {
                    try { window.localStorage.setItem(tableStateKey, JSON.stringify(state)); } catch (ignore) { }
                },
                stateLoadCallback: function () {
                    try {
                        var savedState = JSON.parse(window.localStorage.getItem(tableStateKey));
                        if (!savedState || !savedState.columns || savedState.columns.length !== columns.length) {
                            return null;
                        }
                        return savedState;
                    } catch (ignore) {
                        return null;
                    }
                },
                dom: '<"adhoc-datatable-toolbar"<"adhoc-datatable-tools"B><"adhoc-datatable-controls"lf>>rt<"adhoc-datatable-footer"ip>',
                buttons: {
                    buttons: tableButtons,
                    dom: { button: { className: 'dt-button btn btn-light btn-sm' } }
                },
                order: [[0, 'asc']],
                columns: columns,
                ajax: function (data, callback) {
                    clearError();
                    data.action = 'data';
                    data.reportId = reportId;
                    data.filters = JSON.stringify(getActiveFilters());

                    $.ajax({
                        url: endpoint,
                        type: 'POST',
                        dataType: 'json',
                        data: data
                    }).done(function (response) {
                        callback(response);
                    }).fail(function (xhr) {
                        callback({ draw: data.draw, recordsTotal: 0, recordsFiltered: 0, data: [] });
                        showError(readAjaxError(xhr, 'The report data could not be loaded.'));
                    });
                },
                language: {
                    processing: '<i class="icon-spinner4 spinner mr-2"></i>Loading report rows…',
                    emptyTable: 'This query returned no rows.',
                    zeroRecords: 'No rows match the current filters.',
                    search: '<span class="sr-only">Search report:</span> _INPUT_',
                    searchPlaceholder: 'Search every column',
                    lengthMenu: '<span>Rows:</span> _MENU_',
                    info: 'Showing _START_–_END_ of _TOTAL_ matching rows',
                    infoEmpty: 'Showing 0 matching rows',
                    infoFiltered: '(from _MAX_ total)',
                    loadingRecords: 'Loading report rows…',
                    paginate: {
                        first: '<span aria-hidden="true">«</span><span class="sr-only">First</span>',
                        previous: '<span aria-hidden="true">‹</span><span class="sr-only">Previous</span>',
                        next: '<span aria-hidden="true">›</span><span class="sr-only">Next</span>',
                        last: '<span aria-hidden="true">»</span><span class="sr-only">Last</span>'
                    }
                },
                initComplete: function () {
                    var wrapper = tableElement.closest('.dataTables_wrapper');
                    wrapper.find('.dataTables_filter input').addClass('form-control form-control-sm').attr('aria-label', 'Search all report columns');
                    wrapper.find('.dataTables_length select').addClass('form-control form-control-sm').attr('aria-label', 'Rows per page');
                    this.api().columns.adjust();
                },
                drawCallback: function () {
                    var info = this.api().page.info();
                    var visibleColumns = this.api().columns(':visible').count();
                    status.text(info.recordsDisplay.toLocaleString() + ' matching rows | ' + info.recordsTotal.toLocaleString() + ' total rows | Page ' + (info.page + 1).toLocaleString() + ' of ' + Math.max(1, info.pages).toLocaleString());
                    columnSummary.text(visibleColumns.toLocaleString() + ' of ' + columns.length.toLocaleString() + ' columns visible');
                }
            });

            tableElement.on('search.dt', queueInsightReload)
                .on('column-visibility.dt responsive-resize.dt', function () {
                    if (!table) {
                        return;
                    }
                    columnSummary.text(table.columns(':visible').count().toLocaleString() + ' of ' + columns.length.toLocaleString() + ' columns visible');
                });
            initializeInsightControls(schema);
            loadInsights();
        }

        function exportReport(format, button) {
            if (!table || button.prop('disabled')) {
                return;
            }

            var order = table.order();
            var activeOrder = order && order.length ? order[0] : [0, 'asc'];
            var parameters = new URLSearchParams();
            parameters.append('action', 'export');
            parameters.append('reportId', reportId);
            parameters.append('format', format);
            parameters.append('search', table.search() || '');
            parameters.append('orderColumn', activeOrder[0]);
            parameters.append('orderDirection', activeOrder[1]);
            parameters.append('filters', JSON.stringify(getActiveFilters()));

            var originalHtml = button.html();
            button.prop('disabled', true).html('<i class="icon-spinner4 spinner mr-2"></i>Preparing…');
            clearError();

            window.fetch(endpoint, {
                method: 'POST',
                credentials: 'same-origin',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8' },
                body: parameters.toString()
            }).then(function (response) {
                if (!response.ok) {
                    return response.text().then(function (body) {
                        var message = 'The export could not be created.';
                        try { message = JSON.parse(body).error || message; } catch (ignore) { }
                        throw new Error(message);
                    });
                }
                var disposition = response.headers.get('Content-Disposition') || '';
                var match = /filename="?([^";]+)"?/i.exec(disposition);
                return response.blob().then(function (blob) {
                    return { blob: blob, fileName: match ? match[1] : ('AdhocReport.' + format) };
                });
            }).then(function (download) {
                var url = window.URL.createObjectURL(download.blob);
                var link = document.createElement('a');
                link.href = url;
                link.download = download.fileName;
                document.body.appendChild(link);
                link.click();
                document.body.removeChild(link);
                window.setTimeout(function () { window.URL.revokeObjectURL(url); }, 1000);
            }).catch(function (error) {
                showError(error.message);
            }).then(function () {
                button.prop('disabled', false).html(originalHtml);
            });
        }

        filterColumn.on('change', function () {
            var columnIndex = filterColumn.val();
            if (filterValue.hasClass('select2-hidden-accessible')) {
                filterValue.select2('destroy');
            }
            filterValue.empty().prop('disabled', true);
            if (columnIndex !== '') {
                initializeRemoteSelect(filterValue, parseInt(columnIndex, 10), app);
            }
            reloadTable();
        });
        filterValue.on('change', reloadTable);

        $('#adhoc-apply-advanced').on('click', function () {
            var applied = {};
            $('#adhoc-advanced-filter-fields .adhoc-advanced-select').each(function () {
                var select = $(this);
                addFilter(applied, parseInt(select.attr('data-column-index'), 10), select.val() || []);
            });
            advancedApplied = applied;
            advancedModal.modal('hide');
            reloadTable();
        });

        $('#adhoc-clear-advanced').on('click', function () {
            $('#adhoc-advanced-filter-fields .adhoc-advanced-select').val(null).trigger('change.select2');
        });

        clearFilter.on('click', function () {
            filterColumn.val('');
            if (filterValue.hasClass('select2-hidden-accessible')) {
                filterValue.val(null).trigger('change').select2('destroy');
            }
            filterValue.empty().prop('disabled', true);
            advancedApplied = {};
            $('#adhoc-advanced-filter-fields .adhoc-advanced-select').val(null).trigger('change.select2');
            clearError();
            reloadTable();
        });

        $('#adhoc-refresh').on('click', function (event) {
            event.preventDefault();
            clearError();
            if (table) {
                table.ajax.reload(null, false);
            }
            loadInsights();
        });
        groupColumn.on('change', function () {
            updateSecondaryGroupOptions();
            loadInsights();
        });
        secondaryGroupColumn.add(measureColumn).add(groupLimit).on('change', loadInsights);
        chartType.on('change', renderPrimaryChart);
        chartLibrary.on('change', function () {
            if (currentInsights) {
                updateInsightSummary(currentInsights);
                renderPrimaryChart();
                if (allChartsModal.hasClass('show')) {
                    renderAllCharts();
                }
            }
        });
        $('#adhoc-refresh-insights').on('click', loadInsights);
        $('#adhoc-view-all-charts').on('click', function () {
            if (currentInsights) {
                allChartsModal.modal('show');
            }
        });
        allChartsModal.on('shown.bs.modal', renderAllCharts);
        allChartsModal.on('hidden.bs.modal', function () {
            $.each([
                'adhoc-modal-line-chart', 'adhoc-modal-area-chart', 'adhoc-modal-bar-chart', 'adhoc-modal-column-chart',
                'adhoc-modal-mixed-chart', 'adhoc-modal-pie-chart', 'adhoc-modal-donut-chart', 'adhoc-modal-funnel-chart'
            ], function (_, elementId) {
                disposeRenderedChart(document.getElementById(elementId));
            });
            modalCharts = [];
        });
        $(window).on('resize.adhocInsights', function () {
            window.clearTimeout(tableResizeTimer);
            tableResizeTimer = window.setTimeout(resizeDataTable, 120);
            if (primaryChart) {
                primaryChart.resize();
            }
            $.each(modalCharts, function (_, chart) { if (chart) { chart.resize(); } });
        });
        $('#adhoc-export-csv').on('click', function () { exportReport('csv', $(this)); });
        $('#adhoc-export-excel').on('click', function () { exportReport('xlsx', $(this)); });

        $.ajax({
            url: endpoint,
            type: 'POST',
            dataType: 'json',
            data: { action: 'schema', reportId: reportId }
        }).done(function (schema) {
            if (!schema.columns || !schema.columns.length) {
                showError('The saved query does not return any columns.');
                return;
            }
            initializeTable(schema);
        }).fail(function (xhr) {
            showError(readAjaxError(xhr, 'The report columns could not be loaded.'));
        });
    }
}(window, document, window.jQuery));
