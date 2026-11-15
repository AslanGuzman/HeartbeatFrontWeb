const profileColors = ['#110559', '#165D2D', '#B45309'];

export function renderComparison(canvasId, payload) {
    const canvas = document.getElementById(canvasId);
    if (!canvas || typeof Chart === 'undefined') return;

    Chart.getChart(canvas)?.destroy();
    const datasets = payload.series.map((series, index) => ({
        label: series.label,
        data: series.points,
        borderColor: profileColors[index % profileColors.length],
        backgroundColor: profileColors[index % profileColors.length],
        borderWidth: 2,
        pointRadius: 2,
        tension: 0.25
    }));

    new Chart(canvas, {
        type: 'line',
        data: { datasets },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            animation: false,
            plugins: {
                legend: { position: 'bottom', labels: { boxWidth: 10, font: { size: 10 } } },
                tooltip: {
                    callbacks: {
                        title: items => `Week ${items[0].parsed.x}`,
                        label: item => `${item.dataset.label}: ${item.parsed.y} ${payload.unit}`
                    }
                }
            },
            scales: {
                x: {
                    type: 'linear',
                    title: { display: true, text: 'Mission week' },
                    ticks: { stepSize: 4, font: { size: 10 } },
                    grid: { display: false }
                },
                y: {
                    title: { display: true, text: payload.unit },
                    ticks: { font: { size: 10 } },
                    grid: { color: 'rgba(221, 217, 233, 0.6)' }
                }
            }
        }
    });
}

export function destroyAll() {
    if (typeof Chart === 'undefined') return;
    for (const canvas of document.querySelectorAll('canvas[id^="phantom-chart-"]')) {
        Chart.getChart(canvas)?.destroy();
    }
}
