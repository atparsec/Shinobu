using System.Collections.Generic;
using System.IO;

namespace Shinobu.Helpers.Content
{
    public abstract record DocumentNode;

    public sealed record TextNode(string Text) : DocumentNode;

    public sealed record ParagraphNode(IReadOnlyList<DocumentNode> Children) : DocumentNode
    {
        public ParagraphNode() : this([]) { }
    }

    public sealed record HeadingNode(string Title, int Level, IReadOnlyList<DocumentNode> Children) : DocumentNode
    {
        public HeadingNode(string title, int level) : this(title, level, []) { }
    }

    public sealed record RubyNode(string BaseText, string RubyText) : DocumentNode;

    public sealed record ImageNode(byte[] Data, string Extension, double Width, double Height) : DocumentNode;

    public sealed record LineBreakNode : DocumentNode;
}


