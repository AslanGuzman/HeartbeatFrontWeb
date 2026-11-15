const bandColors = {
    normal: 'rgba(22, 93, 45, 0.10)',
    watch: 'rgba(180, 83, 9, 0.12)',
    critical: 'rgba(153, 14, 14, 0.10)'
};

const statusColors = {
    normal: '#165D2D',
    watch: '#B45309',
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

    const datasets = [{
        label: payload.unit ? `Valor (${payload.unit})` : 'Valor',
        data: payload.points.map(point => ({
            x: Date.parse(point.t),
            y: point.value
        })),
        borderColor: '#110559',
        backgroundColor: '#110559',
        pointBackgroundColor: payload.points.map(point => statusColors[point.status] ?? statusColors.normal),
        pointBorderColor: payload.points.map(point => statusColors[point.status] ?? statusColors.normal),
        pointRadius: 3,
        borderWidth: 2,
        tension: 0.25
    }];

    if (payload.points.some(point => point.secondaryValue != null)) {
        datasets.push({
            label: 'Secundario',
            data: payload.points.map(point => ({
                x: Date.parse(point.t),
                y: point.secondaryValue
            })),
            borderColor: '#6B7280',
            backgroundColor: '#6B7280',
            pointRadius: 2,
            borderWidth: 1.5,
            borderDash: [4, 3],
            tension: 0.25
        });
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
                    display: datasets.length > 1,
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
                    grid: { color: 'rgba(221, 217, 233, 0.6)' }
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
