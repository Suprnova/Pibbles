import assert from 'node:assert/strict';
import { test } from 'node:test';
import { commentStart } from '../../src/comments.ts';

/** Where the last `//` on a line is, which is the comment's start on every line these tests expect one on. */
const last = (line: string) => line.lastIndexOf('//');

test('commentStart finds the first // on any kind of line', () => {
    for (const line of [
        '// A comment line.',
        '    // An indented one.',
        'mira: Locked. // Said quietly.',
        '@call kitchen.stuck #id:r4hc6v // Back down.',
        '== kitchen.door // The front one.',
        '-> Leave // The polite exit.',
        '- @jump kitchen.door // Straight there.',
    ]) {
        assert.equal(commentStart(line), last(line), line);
    }
});

test('commentStart returns undefined for a line without one', () => {
    assert.equal(commentStart('mira: Locked.'), undefined);
    assert.equal(commentStart(''), undefined);
    assert.equal(commentStart('/ a lone slash'), undefined);
});

test('commentStart skips an escaped slash', () => {
    assert.equal(commentStart('Visit https:\\//example.com'), undefined);
    const line = 'Visit https:\\//example.com // See the site.';
    assert.equal(commentStart(line), last(line));
});

test('commentStart skips a tag\'s value but not its name', () => {
    assert.equal(commentStart('mira: Hi. #link:https://example.com'), undefined);
    const line = 'mira: Hi. #link:https://example.com // Check.';
    assert.equal(commentStart(line), last(line));
    assert.equal(commentStart('mira: Hi. #tag//x'), last('mira: Hi. #tag//x'));
});

test('commentStart skips a string in code, and not a quote in text', () => {
    assert.equal(commentStart('@set $url = "https://example.com"'), undefined);
    const escaped = '@set $url = "a\\"//b" // Escaped quote.';
    assert.equal(commentStart(escaped), last(escaped));
    for (const line of ['mira: 5" nails // Imperial.', 'mira: Say "hi" // Quoted.']) {
        assert.equal(commentStart(line), last(line), line);
    }
});

test('commentStart skips a string inside brackets and braces', () => {
    assert.equal(commentStart('mira: See {link("https://example.com")}.'), undefined);
    const line = 'mira: See [link "a//b"]this[/link]. // End.';
    assert.equal(commentStart(line), last(line));
});

test('commentStart reads an option\'s modifiers as code', () => {
    assert.equal(commentStart('-> Leave @if $door == "a//b"'), undefined);
    const line = '-> Leave @if $door == "a//b" // Locked.';
    assert.equal(commentStart(line), last(line));
});

test('commentStart finds a note and a longer run of slashes at their first slash', () => {
    assert.equal(commentStart('mira: Hi. /// A note.'), 10);
    assert.equal(commentStart('mira: Hi. //// A banner.'), 10);
});
