using System.Collections.Generic;

namespace JeremyTCD.DocFx.Plugins.OutlineGenerator
{
    internal class OutlineNode
    {
        public string Href;
        public string Content;
        public int Level;
        public readonly List<OutlineNode> Children = new List<OutlineNode>();
    }
}
