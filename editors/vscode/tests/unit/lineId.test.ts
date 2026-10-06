import assert from 'node:assert/strict';
import { test } from 'node:test';
import { clickColumn, endColumn, enterColumn, hiddenColumn, isLineId, joinLines, selectsOnlyId, stepsIntoId, trailingId } from '../../src/lineId.ts';

const line = 'mira: Locked. #id:k7qp2x';
const contentEnd = 'mira: Locked.'.length;

test('trailingId finds the ID that ends a line', () => {
    assert.deepEqual(trailingId(line), { contentEnd, idStart: contentEnd + 1, idEnd: line.length, value: 'k7qp2x' });
    assert.deepEqual(trailingId(`${line}  `), { contentEnd, idStart: contentEnd + 1, idEnd: line.length, value: 'k7qp2x' });
});

test('trailingId counts whitespace before the separating space as content', () => {
    assert.equal(trailingId('mira: Locked.   #id:k7qp2x')?.contentEnd, contentEnd + 2);
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

test('joinLines keeps the first line\'s ID when a selection takes the next line\'s content', () => {
    const next = 'mira: Again. #id:p3m8tz';
    const nextEnd = 'mira: Again.'.length;

    assert.deepEqual(joinLines(line, contentEnd, next, nextEnd), { text: line, cursor: contentEnd });
});

test('joinLines drops the ID of a line emptied one character at a time', () => {
    assert.deepEqual(joinLines(line, line.length, ' #id:p3m8tz', 0), { text: line, cursor: contentEnd });
});

test('joinLines puts the next line\'s content before the ID', () => {
    const next = 'More. #id:p3m8tz';

    assert.deepEqual(joinLines(line, line.length, next, 0), { text: 'mira: Locked.More. #id:k7qp2x', cursor: contentEnd });
    assert.deepEqual(joinLines(line, line.length, 'More.', 0), { text: 'mira: Locked.More. #id:k7qp2x', cursor: contentEnd });
});

test('joinLines keeps the last line\'s ID when nothing of the first line\'s content is left', () => {
    const next = '    mira: Again. #id:p3m8tz';

    assert.deepEqual(joinLines('    mira: Gone. #id:k7qp2x', 4, next, 4), { text: next, cursor: 4 });
    assert.deepEqual(joinLines(' #id:k7qp2x', 11, next, 0), { text: next, cursor: 0 });
});

test('joinLines leaves lines without IDs, and deletions inside an ID, to VS Code', () => {
    assert.equal(joinLines('mira: Locked.', 13, 'More.', 0), undefined);
    assert.equal(joinLines(line, line.length - 2, 'More.', 0), undefined);
    assert.equal(joinLines('mira: Locked.', 13, line, contentEnd + 3), undefined);
});

test('clickColumn moves a click past the line\'s end before the ID', () => {
    assert.equal(clickColumn(line, line.length), contentEnd);
    assert.equal(clickColumn(line, contentEnd + 3), contentEnd + 3);
    assert.equal(clickColumn('mira: Locked.', contentEnd), contentEnd);
});

test('isLineId accepts only a line ID\'s shape', () => {
    assert.equal(isLineId('k7qp2x'), true);
    assert.equal(isLineId('intro_greeting'), true);
    assert.equal(isLineId('7kqp2x'), false);
    assert.equal(isLineId('K7qp2x'), false);
    assert.equal(isLineId(''), false);
});

test('hiddenColumn keeps a cursor out of a hidden ID', () => {
    assert.equal(hiddenColumn(line, line.length, true), contentEnd);
    assert.equal(hiddenColumn(line, contentEnd + 3, true), contentEnd);
    assert.equal(hiddenColumn(line, 4, true), 4);
    assert.equal(hiddenColumn('mira: Locked.', contentEnd, true), contentEnd);
});

test('selectsOnlyId is true only for a selection that starts after the content', () => {
    assert.equal(selectsOnlyId(line, contentEnd), true);
    assert.equal(selectsOnlyId(line, contentEnd + 5), true);
    assert.equal(selectsOnlyId(line, contentEnd - 1), false);
    assert.equal(selectsOnlyId('mira: Locked.', contentEnd), false);
});

test('hiddenColumn lets a selection take a whole line, but not part of its ID', () => {
    assert.equal(hiddenColumn(line, line.length, false), line.length);
    assert.equal(hiddenColumn(line, contentEnd + 3, false), contentEnd);
});
