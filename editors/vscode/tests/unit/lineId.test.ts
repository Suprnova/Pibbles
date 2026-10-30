import assert from 'node:assert/strict';
import { test } from 'node:test';
import { clickColumn, commentColumn, endColumn, enterColumn, hiddenColumn, isLineId, joinLines, selectsOnlyId, stepsIntoId, trailingId } from '../../src/lineId.ts';

const line = 'mira: Locked. #id:k7qp2x';
const contentEnd = 'mira: Locked.'.length;
const commented = 'mira: Locked. #id:k7qp2x   // Said quietly.';
const commentAt = commented.indexOf('//');

test('trailingId finds the ID that ends a line', () => {
    const found = { contentEnd, idStart: contentEnd + 1, idEnd: line.length, value: 'k7qp2x', commentStart: undefined, hiddenStart: contentEnd };

    assert.deepEqual(trailingId(line), { ...found, hiddenEnd: line.length });
    assert.deepEqual(trailingId(`${line}  `), { ...found, hiddenEnd: line.length + 2 });
});

test('trailingId counts whitespace before the separating space as content', () => {
    assert.equal(trailingId('mira: Locked.   #id:k7qp2x')?.contentEnd, contentEnd + 2);
});

test('trailingId ignores an ID that isn\'t last, and a line without one', () => {
    assert.equal(trailingId('mira: Locked. #id:k7qp2x More.'), undefined);
    assert.equal(trailingId('mira: Locked.'), undefined);
    assert.equal(trailingId('mira: Locked.#id:k7qp2x'), undefined);
});

test('trailingId finds an ID that a comment follows, and hides it but not the space before it', () => {
    assert.deepEqual(trailingId(commented), {
        contentEnd,
        idStart: contentEnd + 1,
        idEnd: line.length,
        value: 'k7qp2x',
        commentStart: commentAt,
        hiddenStart: contentEnd + 1,
        hiddenEnd: commentAt,
    });
    assert.equal(trailingId('@call kitchen.stuck #id:r4hc6v // Back down.')?.value, 'r4hc6v');
    assert.equal(trailingId('mira: Visit https:\\//example.com #id:k7qp2x')?.value, 'k7qp2x');
});

test('trailingId ignores an ID inside a comment', () => {
    assert.equal(trailingId('mira: Locked. // See #id:k7qp2x'), undefined);
    assert.equal(trailingId('// See #id:k7qp2x'), undefined);
    assert.equal(trailingId('mira: Locked. #id:k7qp2x // See #id:p3m8tz')?.value, 'k7qp2x');
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

test('joinLines keeps the first line\'s comment after its ID', () => {
    const next = 'More. #id:p3m8tz';

    assert.deepEqual(joinLines(commented, commented.length, next, 0), { text: 'mira: Locked.More. #id:k7qp2x   // Said quietly.', cursor: contentEnd });
    assert.deepEqual(joinLines(commented, commented.length, 'More.', 0), { text: 'mira: Locked.More. #id:k7qp2x   // Said quietly.', cursor: contentEnd });
});

test('joinLines drops the first line\'s comment when the deletion starts before it', () => {
    assert.deepEqual(joinLines(commented, contentEnd, 'More. #id:p3m8tz', 0), { text: 'mira: Locked.More. #id:k7qp2x', cursor: contentEnd });
    assert.deepEqual(joinLines(commented, commentAt, 'More. #id:p3m8tz', 0), { text: 'mira: Locked.More. #id:k7qp2x', cursor: contentEnd });
});

test('joinLines keeps the last line\'s comment after the first line\'s ID', () => {
    assert.deepEqual(joinLines(line, line.length, 'More. #id:p3m8tz // Again.', 0), { text: 'mira: Locked.More. #id:k7qp2x // Again.', cursor: contentEnd });
    assert.deepEqual(joinLines(line, line.length, 'More. // Again.', 0), { text: 'mira: Locked.More. #id:k7qp2x // Again.', cursor: contentEnd });
    assert.deepEqual(joinLines(commented, commented.length, 'More. #id:p3m8tz // Again.', 0), { text: 'mira: Locked.More. #id:k7qp2x   // Said quietly. // Again.', cursor: contentEnd });
});

test('joinLines drops the last line\'s comment when the deletion takes all of it', () => {
    const next = 'More. #id:p3m8tz // Again.';

    assert.deepEqual(joinLines(line, line.length, next, next.length), { text: line, cursor: contentEnd });
});

test('joinLines keeps the last line\'s comment when nothing of the first line\'s content is left', () => {
    const next = '    mira: Again. #id:p3m8tz // Once more.';

    assert.deepEqual(joinLines('    mira: Gone. #id:k7qp2x // Bye.', 4, next, 4), { text: next, cursor: 4 });
});

test('joinLines leaves a deletion that starts or ends inside a comment\'s text to VS Code', () => {
    assert.equal(joinLines(commented, commentAt + 4, 'More. #id:p3m8tz', 0), undefined);
    assert.equal(joinLines(line, line.length, 'More. #id:p3m8tz // Again.', 'More. #id:p3m8tz // A'.length), undefined);
    assert.equal(joinLines('mira: Locked. // Quietly.', 17, 'More. #id:p3m8tz', 0), undefined);
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
    assert.equal(selectsOnlyId(line, contentEnd, line.length), true);
    assert.equal(selectsOnlyId(line, contentEnd + 5, line.length), true);
    assert.equal(selectsOnlyId(line, contentEnd - 1, line.length), false);
    assert.equal(selectsOnlyId('mira: Locked.', contentEnd, contentEnd), false);
});

test('hiddenColumn lets a selection take a whole line, but not part of its ID', () => {
    assert.equal(hiddenColumn(line, line.length, false), line.length);
    assert.equal(hiddenColumn(line, contentEnd + 3, false), contentEnd);
});

test('hiddenColumn keeps a cursor out of the hidden ID and the start of the comment after it', () => {
    assert.equal(hiddenColumn(commented, contentEnd + 1, true), contentEnd);
    assert.equal(hiddenColumn(commented, commentAt, true), contentEnd);
    assert.equal(hiddenColumn(commented, commentAt + 1, true), commentAt + 1);
    assert.equal(hiddenColumn(commented, commented.length, true), commented.length);
    assert.equal(hiddenColumn(commented, 4, true), 4);
});

test('hiddenColumn lets a selection end where the comment starts, but not inside the ID', () => {
    assert.equal(hiddenColumn(commented, commentAt, false), commentAt);
    assert.equal(hiddenColumn(commented, commentAt - 1, false), contentEnd);
    assert.equal(hiddenColumn(commented, commented.length, false), commented.length);
});

test('selectsOnlyId is false for a selection that reaches into the comment', () => {
    assert.equal(selectsOnlyId(commented, contentEnd, commentAt), true);
    assert.equal(selectsOnlyId(commented, commentAt, commented.length), false);
    assert.equal(selectsOnlyId(commented, contentEnd + 1, commentAt + 3), false);
});

test('endColumn leaves End at the comment\'s end', () => {
    assert.equal(endColumn(commented, 3, commented.length), commented.length);
});

test('clickColumn leaves a click past the comment\'s end alone', () => {
    assert.equal(clickColumn(commented, commented.length), commented.length);
});

test('enterColumn breaks after the comment when the cursor is before the ID or the comment', () => {
    assert.equal(enterColumn(commented, contentEnd), commented.length);
    assert.equal(enterColumn(commented, commentAt), commented.length);
    assert.equal(enterColumn(commented, commentAt + 4), commentAt + 4);
    assert.equal(enterColumn(commented, 4), 4);
});

test('stepsIntoId is false at the content\'s end when a comment follows the ID', () => {
    assert.equal(stepsIntoId(commented, contentEnd), false);
});

test('commentColumn goes past the comment\'s // from the content\'s end, and only there', () => {
    assert.equal(commentColumn(commented, contentEnd), commentAt + 2);
    assert.equal(commentColumn(commented, contentEnd - 1), undefined);
    assert.equal(commentColumn(line, contentEnd), undefined);
    assert.equal(commentColumn('mira: Locked.', contentEnd), undefined);
});
