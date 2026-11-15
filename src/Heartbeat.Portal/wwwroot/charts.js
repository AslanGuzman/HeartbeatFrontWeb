const bandColors = {
    normal: 'rgba(46, 158, 91, 0.025)',
    watch: 'rgba(217, 142, 4, 0.06)',
    critical: 'rgba(153, 14, 14, 0.045)'
};

const statusColors = {
    normal: '#1B6B3C',
    watch: '#805200',
    critical: '#990E0E'
};

const statusBands = {
    id: 'statusBands',
    beforeDraw(chart, _args, options) {
        const bands = options?.bands;
        if (!bands?.length || !chart.chartArea) {
            return;
        }

        const { ctx, chartArea, scales } = chart;
        ctx.save();

        for (const band of bands) {
            const first = band.max == null
                ? chartArea.top
                : scales.y.getPixelForValue(band.max);
            const second = band.min == null
                ? chartArea.bottom
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
        pointBorderColor: '#FFFFFF',
        pointBorderWidth: 1.5,
        pointRadius: payload.points.map(point =>
            point.status === 'watch' || point.status === 'critical' ? 4 : payload.points.length === 1 ? 3 : 0),
        pointStyle: payload.points.map(point => point.status === 'critical' ? 'rectRot' : 'circle'),
        pointHoverRadius: 5,
        pointHitRadius: 10,
        borderWidth: 2,
        tension: 0
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
            pointRadius: payload.points.length === 1 ? 3 : 0,
            pointHoverRadius: 5,
            pointHitRadius: 10,
            borderWidth: 1.5,
            borderDash: [4, 3],
            tension: 0
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
            interaction: { mode: 'index', intersect: false },
            layout: { padding: { top: 8, right: 8 } },
            plugins: {
                legend: {
                    display: datasets.length > 1,
                    position: 'bottom',
                    labels: {
                        boxWidth: 16,
                        boxHeight: 2,
                        padding: 16,
                        color: '#6B7280',
                        font: { size: 11 }
                    }
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
                    border: { display: false },
                    grid: { display: false },
                    ticks: {
                        maxTicksLimit: 4,
                        maxRotation: 0,
                        padding: 10,
                        color: '#6B7280',
                        font: { size: 11 },
                        callback: value => new Date(value).toLocaleDateString(
                            'es-ES',
                            { day: '2-digit', month: 'short' })
                    }
                },
                y: {
                    border: { display: false },
                    grace: '8%',
                    ticks: {
                        maxTicksLimit: 5,
                        padding: 10,
                        color: '#6B7280',
                        font: { size: 11 }
                    },
                    grid: { color: 'rgba(107, 114, 128, 0.10)', drawTicks: false }
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
