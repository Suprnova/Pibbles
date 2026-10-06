import assert from 'node:assert/strict';
import { test } from 'node:test';
import { openSpan } from '../../src/spans.ts';

test('openSpan finds the innermost open span', () => {
    assert.equal(openSpan('mira: [wave]Ominously.'), 'wave');
    assert.equal(openSpan('[b]Bold and [i]both'), 'i');
    assert.equal(openSpan('[wave amplitude=2]Spooky'), 'wave');
});

test('openSpan skips spans that are already closed', () => {
    assert.equal(openSpan('[b]Bold and [i]both[/i] again'), 'b');
    assert.equal(openSpan('[clue]the key[/clue]. Now'), undefined);
});

test('openSpan ignores escaped brackets', () => {
    assert.equal(openSpan('\\[b] is just text'), undefined);
    assert.equal(openSpan('[shout]Open up \\[now'), 'shout');
});
