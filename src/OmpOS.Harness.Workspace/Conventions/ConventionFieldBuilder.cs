using System.Text;

namespace OmpOS.Workspace.Conventions;

/// <summary>The mutable state of one key while the frontmatter parser reads its lines.</summary>
internal sealed class ConventionFieldBuilder
{
    private readonly StringBuilder _text = new();
    private readonly List<string> _items = [];

    /// <summary>Initializes a new instance of the <see cref="ConventionFieldBuilder"/> class.</summary>
    /// <param name="mode">How the following indented lines are read.</param>
    /// <param name="folded">For a block scalar, whether lines are joined with spaces (<c>&gt;</c>) and not newlines (<c>|</c>).</param>
    public ConventionFieldBuilder(ConventionFieldMode mode, bool folded)
    {
        Mode = mode;
        Folded = folded;
    }

    /// <summary>Gets or sets how the following indented lines are read.</summary>
    public ConventionFieldMode Mode { get; set; }

    /// <summary>Gets a value indicating whether a block scalar joins its lines with spaces.</summary>
    public bool Folded { get; }

    /// <summary>Appends the text of a plain or quoted scalar.</summary>
    /// <param name="value">The scalar.</param>
    public void SetScalar(string value)
    {
        _text.Clear();
        _text.Append(value);
    }

    /// <summary>Gets a value indicating whether the key has scalar text yet.</summary>
    public bool HasText => _text.Length > 0;

    /// <summary>Appends a continuation line of a plain scalar, joined with a space.</summary>
    /// <param name="line">The line without its indentation.</param>
    public void AppendContinuation(string line)
    {
        _text.Append(' ').Append(line);
    }

    /// <summary>Appends one line of a block scalar.</summary>
    /// <param name="line">The line without its indentation.</param>
    public void AppendBlockLine(string line)
    {
        if (_text.Length > 0)
        {
            _text.Append(Folded ? ' ' : '\n');
        }

        _text.Append(line);
    }

    /// <summary>Adds one item of a list.</summary>
    /// <param name="item">The item.</param>
    public void AddItem(string item) => _items.Add(item);

    /// <summary>Adds items of a flow list written on the key line.</summary>
    /// <param name="items">The items.</param>
    public void AddItems(IEnumerable<string> items) => _items.AddRange(items);

    /// <summary>Builds the value of the key.</summary>
    /// <returns>The scalar, or the list.</returns>
    public ConventionFieldValue Build()
    {
        if (Mode == ConventionFieldMode.List)
        {
            return new ConventionFieldValue(string.Join(',', _items), _items.ToArray(), IsList: true);
        }

        return new ConventionFieldValue(_text.ToString().Trim(), [], IsList: false);
    }
}
