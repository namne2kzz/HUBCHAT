import { renderMarkdown } from './markdown.util';

/**
 * Covers the message Markdown renderer.
 *
 * Its output is bound with [innerHTML], so escaping is a security boundary rather than a formatting
 * detail. Angular sanitizes what it renders, but relying on that alone would mean the escaping here
 * could rot silently — and the first sign would be a message body executing. The escaping tests come
 * first for that reason.
 */
describe('renderMarkdown', () => {
  describe('escaping', () => {
    it('escapes a script tag instead of emitting it', () => {
      const html = renderMarkdown('<script>alert(1)</script>');

      expect(html).not.toContain('<script>');
      expect(html).toContain('&lt;script&gt;');
    });

    it('escapes an img onerror payload', () => {
      const html = renderMarkdown('<img src=x onerror="alert(1)">');

      expect(html).not.toContain('<img');
      expect(html).toContain('&lt;img');
    });

    it('escapes ampersands before angle brackets', () => {
      // Order matters: escaping < first and & second would turn "&lt;" into "&amp;lt;".
      expect(renderMarkdown('a & b')).toContain('a &amp; b');
      expect(renderMarkdown('&lt;')).toContain('&amp;lt;');
    });

    it('escapes html inside a fenced code block', () => {
      const html = renderMarkdown('```\n<script>alert(1)</script>\n```');

      expect(html).toContain('<pre class="md-code">');
      expect(html).not.toContain('<script>');
      expect(html).toContain('&lt;script&gt;');
    });

    it('escapes html inside inline code', () => {
      const html = renderMarkdown('use `<b>bold</b>` here');

      expect(html).toContain('<code class="md-inline">');
      expect(html).not.toContain('<b>bold</b>');
    });
  });

  describe('link safety', () => {
    it('keeps an http link', () => {
      expect(renderMarkdown('[site](https://example.com)'))
        .toContain('<a href="https://example.com" target="_blank" rel="noopener noreferrer">site</a>');
    });

    it('keeps a mailto link', () => {
      expect(renderMarkdown('[mail](mailto:a@b.com)')).toContain('href="mailto:a@b.com"');
    });

    it('neutralises a javascript: url', () => {
      // The classic markdown XSS: the link text looks ordinary while the href executes.
      const html = renderMarkdown('[click](javascript:alert(1))');

      expect(html).toContain('href="#"');
      expect(html).not.toContain('javascript:');
    });

    it('neutralises a data: url', () => {
      const html = renderMarkdown('[click](data:text/html,<script>alert(1)</script>)');

      expect(html).not.toContain('href="data:');
    });

    it('opens links in a new tab without leaking the opener', () => {
      // rel="noopener" is what stops the opened page reaching back through window.opener.
      expect(renderMarkdown('[x](https://example.com)')).toContain('rel="noopener noreferrer"');
    });
  });

  describe('inline formatting', () => {
    it('renders bold', () => {
      expect(renderMarkdown('**loud**')).toContain('<strong>loud</strong>');
    });

    it('renders italic', () => {
      expect(renderMarkdown('say *this*')).toContain('<em>this</em>');
    });

    it('renders strikethrough', () => {
      expect(renderMarkdown('~~gone~~')).toContain('<del>gone</del>');
    });

    it('does not treat bold as italic', () => {
      // The italic rule runs after bold and has to leave its markers alone, or **x** renders as
      // <em>*x*</em> with stray asterisks.
      const html = renderMarkdown('**bold**');

      expect(html).toContain('<strong>bold</strong>');
      expect(html).not.toContain('<em>');
    });

    it('leaves emphasis inside inline code alone', () => {
      const html = renderMarkdown('`**not bold**`');

      expect(html).not.toContain('<strong>');
      expect(html).toContain('**not bold**');
    });
  });

  describe('blocks', () => {
    it('renders a blockquote', () => {
      expect(renderMarkdown('> quoted')).toContain('<blockquote class="md-quote">quoted</blockquote>');
    });

    it('renders a bullet list', () => {
      const html = renderMarkdown('- one\n- two');

      expect(html).toContain('<ul class="md-list">');
      expect(html).toContain('<li>one</li>');
      expect(html).toContain('<li>two</li>');
      expect(html).toContain('</ul>');
    });

    it('renders a numbered list', () => {
      const html = renderMarkdown('1. first\n2. second');

      expect(html).toContain('<ol class="md-list">');
      expect(html).toContain('<li>first</li>');
      expect(html).toContain('</ol>');
    });

    it('closes one list before opening another kind', () => {
      const html = renderMarkdown('- bullet\n1. number');

      // Nesting an <ol> inside an unclosed <ul> produces invalid markup the browser then repairs
      // unpredictably.
      expect(html.indexOf('</ul>')).toBeLessThan(html.indexOf('<ol'));
    });

    it('closes a list before a following paragraph', () => {
      const html = renderMarkdown('- item\nplain text');

      expect(html.indexOf('</ul>')).toBeLessThan(html.indexOf('plain text'));
    });

    it('keeps a language hint on a fenced block', () => {
      expect(renderMarkdown('```ts\nconst x = 1;\n```')).toContain('data-lang="ts"');
    });

    it('renders a fenced block without a language', () => {
      const html = renderMarkdown('```\nplain\n```');

      expect(html).toContain('<pre class="md-code"><code>plain</code></pre>');
    });
  });

  describe('edge cases', () => {
    it('returns an empty string for empty input', () => {
      expect(renderMarkdown('')).toBe('');
    });

    it('turns newlines into line breaks', () => {
      expect(renderMarkdown('one\ntwo')).toContain('one<br>two');
    });

    it('does not add a line break between a list item and its closing tag', () => {
      // The cleanup pass strips the <br> that the newline inside the list would otherwise leave
      // between </li> and </ul>, which would render as a blank row in the list.
      const html = renderMarkdown('- one\n- two');

      expect(html).not.toContain('<br>');
      expect(html).toBe('<ul class="md-list"><li>one</li><li>two</li></ul>');
    });

    it('keeps a single break between a list and the paragraph after it', () => {
      // A blank line after the list leaves one <br> behind: the cleanup removes the break belonging
      // to the list's own last newline, not the one the author typed as separation. Recorded as the
      // actual behaviour rather than as an ideal — it renders as one blank line, which is what a
      // blank line in the source should look like.
      expect(renderMarkdown('- item\n\nafter'))
        .toBe('<ul class="md-list"><li>item</li></ul><br>after');
    });

    it('leaves plain text untouched', () => {
      expect(renderMarkdown('just a normal message')).toBe('just a normal message');
    });

    it('handles several code blocks in one message', () => {
      const html = renderMarkdown('```\nfirst\n```\ntext\n```\nsecond\n```');

      expect(html).toContain('first');
      expect(html).toContain('second');
      expect(html).toContain('text');
    });
  });
});
