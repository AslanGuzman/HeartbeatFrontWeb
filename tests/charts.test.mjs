import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

const source = await readFile(new URL('../src/Heartbeat.Portal/wwwroot/charts.js', import.meta.url), 'utf8');
const { render } = await import(`data:text/javascript;base64,${Buffer.from(source).toString('base64')}`);
const canvas = {};
let config;
globalThis.document = { getElementById: () => canvas };
globalThis.Chart = class {
    static getChart() { return null; }
    constructor(_canvas, options) { config = options; }
};

test('keeps readings intact, highlights abnormal readings and allows hover without visible dots', () => {
    render('chart-test', {
        unit: 'bpm',
        bands: [],
        points: ['normal', 'watch', 'critical'].map((status, index) => ({
            t: `2026-11-0${index + 1}T00:00:00Z`,
            value: 70 + index,
            secondaryValue: index === 1 ? null : 60,
            status
        }))
    });
    const [primary, secondary] = config.data.datasets;
    assert.deepEqual(primary.data.map(point => point.y), [70, 71, 72]);
    assert.deepEqual(primary.pointRadius, [0, 4, 4]);
    assert.deepEqual(primary.pointStyle, ['circle', 'circle', 'rectRot']);
    assert.equal(secondary.data[1].y, null);
    assert.equal(config.options.interaction.intersect, false);
});

test('open-ended bands shade only their own ranges and stay inside the plot', () => {
    const rectangles = [];
    const chart = {
        ctx: {
            save() {},
            restore() {},
            fillRect: (...rectangle) => rectangles.push(rectangle)
        },
        chartArea: { left: 10, right: 210, top: 0, bottom: 100 },
        scales: { y: { getPixelForValue: value => 100 - value } }
    };
    config.plugins[0].beforeDraw(chart, {}, {
        bands: [
            { status: 'critical', min: null, max: 20 },
            { status: 'normal', min: 20, max: 80 },
            { status: 'watch', min: 80, max: null },
            { status: 'critical', min: 110, max: 120 }
        ]
    });
    assert.deepEqual(rectangles, [
        [10, 80, 200, 20],
        [10, 20, 200, 60],
        [10, 0, 200, 20]
    ]);
});

test('a single normal reading remains visible', () => {
    render('chart-test', {
        points: [{ t: '2026-11-01T00:00:00Z', value: 70, status: 'normal' }]
    });
    assert.deepEqual(config.data.datasets[0].pointRadius, [3]);
});

test('keeps manual and wearable readings separate with their visual styles', () => {
    render('chart-test', {
        unit: 'bpm',
        points: [
            { t: '2026-11-01T00:00:00Z', value: 70, status: 'normal', source: 'manual' },
            { t: '2026-11-02T00:00:00Z', value: 110, secondaryValue: 80, status: 'critical', source: 'wearable' }
        ]
    });
    const [manual, wearable, secondary] = config.data.datasets;
    assert.equal(manual.source, 'manual');
    assert.deepEqual(manual.data.map(point => point.y), [70]);
    assert.deepEqual(manual.pointRadius, [3]);
    assert.equal(wearable.source, 'wearable');
    assert.deepEqual(wearable.data.map(point => point.y), [110]);
    assert.deepEqual(wearable.borderDash, [6, 3]);
    assert.deepEqual(wearable.pointRadius, [4]);
    assert.deepEqual(wearable.pointStyle, ['rectRot']);
    assert.equal(secondary.source, 'wearable');
    assert.deepEqual(secondary.data.map(point => point.y), [80]);
    assert.equal(config.options.plugins.legend.display, true);
});
