import assert from 'node:assert/strict';
import { test } from 'node:test';
import { clickColumn, endColumn, enterColumn, stepsIntoId, trailingId } from '../../src/lineId.ts';

const line = 'mira: Locked. #id:k7qp2x';
const contentEnd = 'mira: Locked.'.length;

test('trailingId finds the ID that ends a line', () => {
    assert.deepEqual(trailingId(line), { contentEnd, idStart: contentEnd + 1, idEnd: line.length });
    assert.deepEqual(trailingId(`${line}  `), { contentEnd, idStart: contentEnd + 1, idEnd: line.length });
});

test('trailingId ignores an ID that isn\'t last, and a line without one', () => {
    assert.equal(trailingId('@call kitchen.stuck #id:r4hc6v // Back down.'), undefined);
    assert.equal(trailingId('mira: Locked.'), undefined);
    assert.equal(trailingId('mira: Locked.#id:k7qp2x'), undefined);
});

test('endColumn stops before the ID, then goes to the real end', () => {
    assert.equal(endColumn(line, 3, line.length), contentEnd);
    assert.equal(endColumn(line, contentEnd, line.length), line.length);
    assert.equal(endColumn('mira: Locked.', 3, contentEnd), contentEnd);
});

test('endColumn leaves End on a wrapped segment that ends before the ID alone', () => {
    assert.equal(endColumn(line, 1, 6), 6);
});

test('stepsIntoId is true only at the content\'s end', () => {
    assert.equal(stepsIntoId(line, contentEnd), true);
    assert.equal(stepsIntoId(line, contentEnd - 1), false);
    assert.equal(stepsIntoId('mira: Locked.', contentEnd), false);
});

test('enterColumn breaks after the ID when the cursor is just before it', () => {
    assert.equal(enterColumn(line, contentEnd), line.length);
    assert.equal(enterColumn(line, contentEnd + 1), line.length);
    assert.equal(enterColumn(line, 4), 4);
});

test('clickColumn moves a click past the line\'s end before the ID', () => {
    assert.equal(clickColumn(line, line.length), contentEnd);
    assert.equal(clickColumn(line, contentEnd + 3), contentEnd + 3);
    assert.equal(clickColumn('mira: Locked.', contentEnd), contentEnd);
});
