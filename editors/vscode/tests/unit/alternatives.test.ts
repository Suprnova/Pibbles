import assert from 'node:assert/strict';
import { test } from 'node:test';
import { dashTyped, enter, tab } from '../../src/alternatives.ts';

const story = [
    '== kitchen.window',
    '@cycle #id:skmne3',
    '    - mira: Dusty. #id:i6f2vr',
    '    - mira: Very dusty.',
    '        @wait 0.5s',
    '    - ',
    '    -',
    '- Not in a variation.',
    '@if $door_open',
    '    - Also not in one.',
];

test('Enter at an alternative\'s end starts the next one', () => {
    assert.deepEqual(enter(story, 3, story[3].length), { kind: 'continue', newLine: '    - ' });
});

test('Enter before an alternative\'s trailing ID starts the next one', () => {
    assert.deepEqual(enter(story, 2, '    - mira: Dusty.'.length), { kind: 'continue', newLine: '    - ' });
});

test('Enter in the middle of an alternative is VS Code\'s own', () => {
    assert.equal(enter(story, 3, 8), undefined);
});

test('Enter on a line holding only "- " leaves the variation', () => {
    assert.deepEqual(enter(story, 5, story[5].length), { kind: 'leave', line: '' });
});

test('Enter on a lone "-" is VS Code\'s own, since its block comes next', () => {
    assert.equal(enter(story, 6, story[6].length), undefined);
});

test('Enter on a "- " line outside a variation is VS Code\'s own', () => {
    assert.equal(enter(story, 7, story[7].length), undefined);
    assert.equal(enter(story, 9, story[9].length), undefined);
});

test('Tab on a line holding only "- " makes it a continuation of the alternative above', () => {
    assert.equal(tab(story, 5, '    '), '        ');
});

test('Tab anywhere else is VS Code\'s own', () => {
    assert.equal(tab(story, 3, '    '), undefined);
    assert.equal(tab(story, 9, '    '), undefined);
});

test('"- " typed on a continuation moves out to the alternatives', () => {
    const typed = [...story.slice(0, 5), '        - '];
    assert.equal(dashTyped(typed, 5), '    - ');
});

test('"- " typed anywhere else stays where it is', () => {
    assert.equal(dashTyped(story, 5), undefined);
    assert.equal(dashTyped(['@if $door_open', '    - '], 1), undefined);
});
