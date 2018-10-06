using HtmlAgilityPack;
using JeremyTCD.DocFx.Plugins.Utils;
using Microsoft.DocAsCode.Plugins;
using System;
using System.Collections.Immutable;
using System.Composition;
using System.IO;

namespace JeremyTCD.DocFx.Plugins.OutlineGenerator
{
    [Export(nameof(OutlineGenerator), typeof(IPostProcessor))]
    public class OutlineGenerator : IPostProcessor
    {
        private static string[] _headingElementNames = new string[] { "h1", "h2", "h3", "h4", "h5", "h6" };

        public ImmutableDictionary<string, object> PrepareMetadata(ImmutableDictionary<string, object> metadata)
        {
            // Do nothing
            return metadata;
        }

        // If article menu is enabled, generates outline and inserts it
        public Manifest Process(Manifest manifest, string outputFolder)
        {
            if (outputFolder == null)
            {
                throw new ArgumentNullException("Base directory cannot be null");
            }

            foreach (ManifestItem manifestItem in manifest.Files)
            {
                if (manifestItem.DocumentType != "Conceptual")
                {
                    continue;
                }

                manifestItem.Metadata.TryGetValue("mimo_disableArticleMenu", out object disableArticleMenu);
                if (disableArticleMenu as bool? == true)
                {
                    continue;
                }

                // Get document Node
                HtmlNode documentNode = manifestItem.
                    GetHtmlOutputDoc(outputFolder).
                    DocumentNode;

                // Get article node
                HtmlNode articleNode = documentNode.SelectSingleNode("//article[@class='main-article']");

                // Generate outline tree for article
                OutlineNode rootOutlineNode = new OutlineNode
                {
                    Content = articleNode.SelectSingleNode("h1").InnerText,
                    Level = 1,
                    Href = "#"
                };
                GenerateOutlineTree(articleNode.SelectSingleNode("div[@class='content']"), rootOutlineNode); // content div is the direct parent level 2 sections

                // Render outline tree
                var outlineHtmlDoc = new HtmlDocument();
                HtmlNode rootULElement = outlineHtmlDoc.CreateElement("ul");
                rootULElement.SetAttributeValue("class", "level-2");
                GenerateOutlineNodes(rootULElement, rootOutlineNode, outlineHtmlDoc);

                // Insert title
                HtmlNode outlineElement = documentNode.SelectSingleNode("//*[@id='outline']");
                HtmlNode titleSpanElement = outlineHtmlDoc.CreateElement("span");
                titleSpanElement.InnerHtml = rootOutlineNode.Content;
                HtmlNode titleAElement = outlineHtmlDoc.CreateElement("a");
                titleAElement.SetAttributeValue("href", "#");
                titleAElement.SetAttributeValue("class", "level-1");
                titleAElement.AppendChild(titleSpanElement);
                outlineElement.PrependChild(titleAElement);

                // Insert outline tree
                HtmlNode outlineScrollableElement = outlineElement.SelectSingleNode("*[@id='outline-scrollable']");
                outlineScrollableElement.AppendChild(rootULElement);

                // Save
                string relPath = manifestItem.GetHtmlOutputRelPath();
                File.WriteAllText(Path.Combine(outputFolder, relPath), documentNode.OuterHtml);
            }

            return manifest;
        }

        private void GenerateOutlineNodes(HtmlNode ulElement, OutlineNode outlineNode, HtmlDocument outlineHtmlDoc)
        {
            foreach (OutlineNode childOutlineNode in outlineNode.Children)
            {
                HtmlNode liElement = outlineHtmlDoc.CreateElement("li");
                HtmlNode aElement = outlineHtmlDoc.CreateElement("a");
                aElement.SetAttributeValue("href", childOutlineNode.Href);
                HtmlNode spanElement = outlineHtmlDoc.CreateElement("span");
                spanElement.InnerHtml = childOutlineNode.Content;
                aElement.AppendChild(spanElement);
                liElement.AppendChild(aElement);
                ulElement.AppendChild(liElement);

                // Scrollabe track
                if(outlineNode.Level == 1)
                {
                    HtmlNode indicatorTrackElement = outlineHtmlDoc.CreateElement("div");
                    indicatorTrackElement.SetAttributeValue("class", "outline-scrollable-track");
                    liElement.AppendChild(indicatorTrackElement);
                }

                if (childOutlineNode.Children.Count > 0 && childOutlineNode.Level < 3) // Don't include h4s, h5s and h6s
                {
                    HtmlNode childULElement = outlineHtmlDoc.CreateElement("ul");
                    childULElement.SetAttributeValue("class", $"level-{childOutlineNode.Level + 1}");
                    liElement.AppendChild(childULElement);

                    GenerateOutlineNodes(childULElement, childOutlineNode, outlineHtmlDoc);
                }
            }
        }

        private OutlineNode GenerateOutlineTree(HtmlNode htmlNode, OutlineNode outlineNode)
        {
            HtmlNodeCollection children = htmlNode.ChildNodes;

            foreach (HtmlNode childHtmlNode in children)
            {
                // Intentionally skips sub trees since they do not contribute to the document's outline. Sub trees are children of 
                // sectioning content roots, like blockquote. If child is a blockquote, we never iterate through its children, so sub trees are ignored.
                if (childHtmlNode.Name == "section")
                {
                    // We don't know the heading tag, could be h1|h2|h3|h4|h5|h6. Xpath 1 sucks so bad.
                    HtmlNode headingElement = childHtmlNode.SelectSingleNode("header/*[self::h1 or self::h2 or self::h3 or self::h4 or self::h5 or self::h6]");
                    int level = headingElement.Name[1] - 48; // http://www.asciitable.com/
                    OutlineNode childOutlineNode = new OutlineNode
                    {
                        Content = headingElement.InnerText,
                        Level = level,
                        Href = "#" + childHtmlNode.Id
                    };
                    outlineNode.Children.Add(childOutlineNode);

                    GenerateOutlineTree(childHtmlNode, childOutlineNode);
                }
            }


            return null;
        }      
    }
}
