const bandColors = {
    normal: 'rgba(46, 158, 91, 0.10)',
    watch: 'rgba(217, 142, 4, 0.12)',
    critical: 'rgba(153, 14, 14, 0.10)'
};

const statusColors = {
    normal: '#1B6B3C',
    watch: '#805200',
    critical: '#990E0E'
};

const statusBands = {
    id: 'statusBands',
    beforeDatasetsDraw(chart, _args, options) {
        const bands = options?.bands;
        if (!bands?.length || !chart.chartArea) {
            return;
        }

        const { ctx, chartArea, scales } = chart;
        ctx.save();

        for (const band of bands) {
            const first = band.max == null
                ? chartArea.bottom
                : scales.y.getPixelForValue(band.max);
            const second = band.min == null
                ? chartArea.top
                : scales.y.getPixelForValue(band.min);
            const top = Math.max(chartArea.top, Math.min(first, second));
            const bottom = Math.min(chartArea.bottom, Math.max(first, second));

            if (bottom <= top) {
                continue;
            }

            ctx.fillStyle = bandColors[band.status] ?? bandColors.normal;
            ctx.fillRect(chartArea.left, top, chartArea.right - chartArea.left, bottom - top);
        }

        ctx.restore();
    }
};

export function render(canvasId, payload) {
    const canvas = document.getElementById(canvasId);
    if (!canvas || typeof Chart === 'undefined') {
        return;
    }

    Chart.getChart(canvas)?.destroy();

    const sourceStyles = {
        manual: { label: 'Manual', color: '#110559', dash: [] },
        wearable: { label: 'Reloj', color: '#6D28D9', dash: [6, 3] }
    };
    const sources = [...new Set(payload.points.map(point => point.source ?? 'manual'))];
    const datasets = sources.map(source => {
        const style = sourceStyles[source] ?? sourceStyles.manual;
        const points = payload.points.filter(point => (point.source ?? 'manual') === source);
        return {
            label: payload.unit ? `${style.label} (${payload.unit})` : style.label,
            source,
            data: points.map(point => ({
                x: Date.parse(point.t),
                y: point.value,
                status: point.status
            })),
            borderColor: style.color,
            backgroundColor: style.color,
            pointBackgroundColor: points.map(point => statusColors[point.status] ?? statusColors.normal),
            pointBorderColor: points.map(point => statusColors[point.status] ?? statusColors.normal),
            pointRadius: 3,
            borderWidth: 2,
            borderDash: style.dash,
            tension: 0.25
        };
    });

    if (payload.points.some(point => point.secondaryValue != null)) {
        for (const source of sources) {
            const style = sourceStyles[source] ?? sourceStyles.manual;
            const points = payload.points.filter(
                point => (point.source ?? 'manual') === source && point.secondaryValue != null);
            if (points.length) {
                datasets.push({
                    label: `${style.label} · secundario`,
                    source,
                    data: points.map(point => ({
                        x: Date.parse(point.t),
                        y: point.secondaryValue,
                        status: point.status
                    })),
                    borderColor: style.color,
                    backgroundColor: style.color,
                    pointRadius: 2,
                    borderWidth: 1.5,
                    borderDash: [2, 3],
                    tension: 0.25
                });
            }
        }
    }

    new Chart(canvas, {
        type: 'line',
        data: { datasets },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            animation: false,
            parsing: false,
            plugins: {
                legend: {
                    display: true,
                    labels: { boxWidth: 10, font: { size: 10 } }
                },
                tooltip: {
                    callbacks: {
                        title: items => new Date(items[0].parsed.x).toLocaleString('es-ES'),
                        label: item => {
                            const unit = payload.unit ? ` ${payload.unit}` : '';
                            return `${item.dataset.label}: ${item.parsed.y}${unit}`;
                        }
                    }
                },
                statusBands: { bands: payload.bands }
            },
            scales: {
                x: {
                    type: 'linear',
                    grid: { display: false },
                    ticks: {
                        maxTicksLimit: 6,
                        font: { size: 10 },
                        callback: value => new Date(value).toLocaleDateString(
                            'es-ES',
                            { day: '2-digit', month: 'short' })
                    }
                },
                y: {
                    ticks: { font: { size: 10 } },
                    grid: { color: 'rgba(236, 237, 245, 0.9)' }
                }
            }
        },
        plugins: [statusBands]
    });
}

export function destroyAll() {
    if (typeof Chart === 'undefined') {
        return;
    }

    for (const canvas of document.querySelectorAll('canvas[id^="chart-"]')) {
        Chart.getChart(canvas)?.destroy();
    }
}
