﻿
#region Using Directives

using AngleSharp.Dom;
using AngleSharp.Dom.Html;
using AngleSharp.Parser.Html;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

#endregion

namespace System.Windows.Documents.Reporting
{
    /// <summary>
    /// Represents a simple HTML converter, which parses the HTML and convertes it to a flow document, which can then be used to renders PDFs or XPSs.
    /// </summary>
    public class HtmlConverter
    {
        #region Private Static Fields

        /// <summary>
        /// Contains a dictionary, which maps the name of an HTML element to a method, which converts this HTML element to a flow document element.,
        /// </summary>
        private static Dictionary<string, Func<IHtmlElement, TextElement>> htmlElementConversionMap;

        #endregion

        #region Private Static Methods

        /// <summary>
        /// Retrieves all the runs and line breaks inside an inline collection recursively. This is very useful for white-space character handling.
        /// </summary>
        /// <param name="inlineCollection">The inline collection from which the runs and line breaks are to be retrieved.</param>
        /// <returns>Returns a list of runs and line breaks from the specified inline collection in the order that they appear within the inline collection.</returns>
        private static List<Inline> GetRunsAndLineBreaks(InlineCollection inlineCollection)
        {
            // Creates a new list, which will contain the result
            List<Inline> runsAndLineBreaks = new List<Inline>();

            // Cycles over all the inlines in the inline collection
            foreach (Inline inline in inlineCollection)
            {
                // Checks if the current inline element is a run, if so then it is added to the result set
                Run run = inline as Run;
                if (run != null)
                    runsAndLineBreaks.Add(run);

                // Checks if the current inline element is a line break, if so then it is added to the result set
                LineBreak lineBreak = inline as LineBreak;
                if (lineBreak != null)
                    runsAndLineBreaks.Add(lineBreak);

                // Checks if the current inline element is a span, a span is the only inline element, which can itself contain inline elements, if the current
                // inline element is a span, then the runs and line breaks that it contains are recursively retrieved and added to the result set
                Span span = inline as Span;
                if (span != null)
                    runsAndLineBreaks.AddRange(HtmlConverter.GetRunsAndLineBreaks(span.Inlines));
            }

            // Returns the created list of runs and line breaks
            return runsAndLineBreaks;
        }

        /// <summary>
        /// Applies the proper white-space character handling to the converted contents.
        /// </summary>
        /// <param name="blockElements">The block elements to which white-space character handling is to be applied.</param>
        private static void ApplyWhiteSpaceHandling(IEnumerable<Block> blockElements)
        {
            // Cylces over each paragraph in the list of block elements and applies the rules for white-space character handling to it
            foreach (Paragraph paragraph in blockElements.OfType<Paragraph>().Where(p => p.Tag as string != "PreformattedText").ToList())
            {
                // Recursively retrieves all the runs and line breaks that are contained in the paragraph (runs and line breaks are the only inline elements that
                // are relevant for white-space character handling, because runs are the only inline elements that can directly contain text, and therefore
                // white-space characters, and all white space characters directly before and after line breaks must be ignored)
                List<Inline> runsAndLineBreaks = HtmlConverter.GetRunsAndLineBreaks(paragraph.Inlines);

                // Replaces all white-space characters with white spaces and replaces multiple white spaces with one
                foreach (Run run in runsAndLineBreaks.OfType<Run>())
                    run.Text = Regex.Replace(run.Text, "\\s+", " ");

                // Cycles over the list of retrieved runs and line breaks, till there are no more run left
                while (runsAndLineBreaks.OfType<Run>().Any())
                {
                    // Retrieves all runs till the first line break appears
                    List<Run> runs = runsAndLineBreaks.SkipWhile(inline => inline is LineBreak).TakeWhile(inline => inline is Run).OfType<Run>().ToList();
                    
                    // Applies the first rule of white-space character handling: there must only be one white space between two inline elements
                    for (int i = 0; i < runs.Count() - 1; i++)
                    {
                        if (runs[i].Text.EndsWith(" ") && runs[i + 1].Text.StartsWith(" "))
                            runs[i].Text = runs[i].Text.TrimEnd();
                    }

                    // Applies the second rule of white-space handling: the first non-empty run must not start with white-space characters (this is because it is
                    // the run that has the first textual content within the paragraph, which is a block element, and block elements must never begin with a
                    // white-space or it is the first run that has textual content after a line break and white-spaces after line breaks have to be ignored)
                    foreach (Run run in runs)
                    {
                        run.Text = run.Text.TrimStart();
                        if (!string.IsNullOrWhiteSpace(run.Text))
                            break;
                    }

                    // Applies the third rule of white-space handling: the last non-empty run must not end with white-space characters (this is because it is the
                    // run that has the last textual content within the paragraph, which is a block element, and block elements must never end with a white-space,
                    // or it is the last run before a line break and white-spaces before line-breaks have to be ignored)
                    foreach (Run run in runs.Reverse<Run>())
                    {
                        run.Text = run.Text.TrimEnd();
                        if (!string.IsNullOrWhiteSpace(run.Text))
                            break;
                    }

                    // Removes all the runs from the collection, because their white-spaces have already been handled
                    runsAndLineBreaks.RemoveAll(inline => runs.Contains(inline as Run));
                }
            }

            // Cycles over all sections and lists in the list of block elements and recusively applies white-space character handling to them
            foreach (Section section in blockElements.OfType<Section>().ToList())
                HtmlConverter.ApplyWhiteSpaceHandling(section.Blocks);
            foreach (List list in blockElements.OfType<List>().ToList())
            {
                foreach (ListItem listItem in list.ListItems.ToList())
                    HtmlConverter.ApplyWhiteSpaceHandling(listItem.Blocks);
            }
        }

        /// <summary>
        /// Converts a list of text elements into a list of blocks by wrapping inline elements in block elements.
        /// </summary>
        /// <param name="textElements">The text elements that are to be converted into a list of block elements.</param>
        /// <returns>Returns the converted list of block elements.</returns>
        private static IEnumerable<Block> ConvertTextElementsToBlocks(IEnumerable<TextElement> textElements)
        {
            // Creates a new list for the result
            List<Block> blockElements = new List<Block>();

            // Cycles over all text elements in order to wrap the inline elements in block elements
            Paragraph currentContainerParagraph = null;
            foreach (TextElement textElement in textElements)
            {
                // Checks if the element is a block element or an inline element
                Block block = textElement as Block;
                if (block == null)
                {
                    // Since the element is an inline element, it is added to the paragraph, which wraps the inline elements in a block element
                    currentContainerParagraph = currentContainerParagraph ?? new Paragraph();
                    currentContainerParagraph.Inlines.Add(textElement as Inline);
                }
                else
                {
                    // Since the element is a block element, the previously wrapped inline elements are are added to the result set
                    if (currentContainerParagraph != null)
                    {
                        if (currentContainerParagraph.Inlines.Count > 1 || !currentContainerParagraph.Inlines.OfType<Run>().Any(run => string.IsNullOrWhiteSpace(run.Text)))
                            blockElements.Add(currentContainerParagraph);
                        currentContainerParagraph = null;
                    }

                    // Adds the block element to the result set
                    blockElements.Add(block);
                }
            }

            // Checks if there are any wrapped inline elements left, if so they are also added to the result set
            if (currentContainerParagraph != null)
                blockElements.Add(currentContainerParagraph);

            // Returns the result list of block elements
            return blockElements;
        }

        /// <summary>
        /// Initializes the dictionary, which maps the name of an HTML element to a method, which converts the HTML element to a flow document element.
        /// </summary>
        private static void InitializeHtmlElementConversionMap()
        {
            // Checks if the HTML element conversion map has already been initialized, if so then nothing needs to be done
            if (HtmlConverter.htmlElementConversionMap != null)
                return;

            // Creates a new HTML element conversion map
            HtmlConverter.htmlElementConversionMap = new Dictionary<string, Func<IHtmlElement, TextElement>>();

            // Cycles over all static methods of the method and collects all the methods that are marked by the HTML element converter attribute
            foreach (MethodInfo methodInfo in typeof(HtmlConverter).GetMethods(BindingFlags.Static | BindingFlags.NonPublic))
            {
                // Gets all the HTML element converter attributes of the method, if it has none, then the next method is processed
                IEnumerable<HtmlElementConverterAttribute> htmlConverterAttributes = methodInfo.GetCustomAttributes<HtmlElementConverterAttribute>();
                if (!htmlConverterAttributes.Any())
                    continue;

                // Adds the conversion method to the HTML element conversion map
                foreach (HtmlElementConverterAttribute htmlElementConverterAttribute in htmlConverterAttributes)
                    HtmlConverter.htmlElementConversionMap.Add(htmlElementConverterAttribute.HtmlElementName.ToUpperInvariant(), htmlElement => methodInfo.Invoke(null, new object[] { htmlElement }) as TextElement);
            }
        }

        /// <summary>
        /// Converts the specified HTML node into flow document content element.
        /// </summary>
        /// <param name="htmlNode">The HTML node that is to be converted into flow document content element.</param>
        /// <returns>Returns the converted flow document element.</returns>
        private static TextElement ConvertHtmlNode(INode htmlNode)
        {
            // Initializes the HTML element conversion map if it has not yet been initialized
            HtmlConverter.InitializeHtmlElementConversionMap();

            // Checks if the HTML node is an HTML element, in that case the HTML element is parsed
            IHtmlElement htmlElement = htmlNode as IHtmlElement;
            if (htmlElement != null && HtmlConverter.htmlElementConversionMap.ContainsKey(htmlElement.TagName.ToUpperInvariant()))
                return HtmlConverter.htmlElementConversionMap[htmlElement.TagName.ToUpperInvariant()](htmlElement);

            // Since the HTML node was either not an HTML element or the HTML element is not supported, the textual content of the HTML node is returned as a run element
            return new Run(htmlNode.TextContent);
        }

        #endregion

        #region Private Static Conversion Methods

        /// <summary>
        /// Converts the specified line break HTML element to a line break flow document element.
        /// </summary>
        /// <param name="lineBreakHtmlElement">The line break HTML element that is to be converted.</param>
        /// <returns>Returns a line break flow document element.</returns>
        [HtmlElementConverter("BR")]
        private static TextElement ConvertLineBreakElement(IHtmlElement lineBreakHtmlElement) => new LineBreak();

        /// <summary>
        /// Converts the specified paragraph HTML element to a paragraph flow document element.
        /// </summary>
        /// <param name="paragraphHtmlElement">The paragraph HTML element that is to be converted.</param>
        /// <returns>Returns a paragraph flow document element.</returns>
        [HtmlElementConverter("P")]
        private static TextElement ConvertParagraphElement(IHtmlElement paragraphHtmlElement)
        {
            IEnumerable<Inline> paragraphContent = paragraphHtmlElement.ChildNodes.Select(child => HtmlConverter.ConvertHtmlNode(child)).OfType<Inline>();
            Paragraph paragraph = new Paragraph();
            paragraph.Inlines.AddRange(paragraphContent);
            return paragraph;
        }

        /// <summary>
        /// Converts the specified preformatted text HTML element to a paragraph flow document element, where the white-space character handling is skipped.
        /// </summary>
        /// <param name="paragraphHtmlElement">The preformatted text HTML element that is to be converted.</param>
        /// <returns>Returns a paragraph flow document element, where the white-space character handling is skipped.</returns>
        [HtmlElementConverter("PRE")]
        private static TextElement ConvertPreformattedTextElement(IHtmlElement paragraphHtmlElement)
        {
            IEnumerable<Inline> preformattedTextContent = paragraphHtmlElement.ChildNodes.Select(child => HtmlConverter.ConvertHtmlNode(child)).OfType<Inline>();
            Paragraph preformattedText = new Paragraph();
            preformattedText.Tag = "PreformattedText";
            preformattedText.Inlines.AddRange(preformattedTextContent);
            return preformattedText;
        }

        /// <summary>
        /// Converts the specified section HTML element to a section flow document element.
        /// </summary>
        /// <param name="sectionHtmlElement">The section HTML element that is to be converted.</param>
        /// <returns>Returns a section flow document element.</returns>
        [HtmlElementConverter("SECTION")]
        private static TextElement ConvertSectionElement(IHtmlElement sectionHtmlElement)
        {
            IEnumerable<Block> sectionContent = HtmlConverter.ConvertTextElementsToBlocks(sectionHtmlElement.ChildNodes.Select(child => HtmlConverter.ConvertHtmlNode(child)));
            Section section = new Section();
            section.Blocks.AddRange(sectionContent);
            return section;
        }

        /// <summary>
        /// Converts the specified div HTML element to a section flow document element.
        /// </summary>
        /// <param name="divisionHtmlElement">The div HTML element that is to be converted.</param>
        /// <returns>Returns a section flow document element.</returns>
        [HtmlElementConverter("DIV")]
        private static TextElement ConvertDivisionElement(IHtmlElement divisionHtmlElement)
        {
            IEnumerable<Block> divisionContent = HtmlConverter.ConvertTextElementsToBlocks(divisionHtmlElement.ChildNodes.Select(child => HtmlConverter.ConvertHtmlNode(child)));
            Section division = new Section();
            division.Blocks.AddRange(divisionContent);
            return division;
        }

        /// <summary>
        /// Converts the specified span HTML element to a span flow document element.
        /// </summary>
        /// <param name="spanHtmlElement">The span HTML element that is to be converted.</param>
        /// <returns>Returns a span flow document element.</returns>
        [HtmlElementConverter("SPAN")]
        private static TextElement ConvertSpanElement(IHtmlElement spanHtmlElement)
        {
            IEnumerable<Inline> spanContent = spanHtmlElement.ChildNodes.Select(child => HtmlConverter.ConvertHtmlNode(child)).OfType<Inline>();
            Span span = new Span();
            span.Inlines.AddRange(spanContent);
            return span;
        }

        /// <summary>
        /// Converts the specified emphasis (or italics) HTML element to a span flow document element, which has an italic font style.
        /// </summary>
        /// <param name="emphasisHtmlElement">The emphasis (or italics) HTML element that is to be converted.</param>
        /// <returns>Returns a span flow document element, which has an italic font style.</returns>
        [HtmlElementConverter("I")]
        [HtmlElementConverter("EM")]
        private static TextElement ConvertEmphasisElement(IHtmlElement emphasisHtmlElement)
        {
            IEnumerable<Inline> emphasisContent = emphasisHtmlElement.ChildNodes.Select(child => HtmlConverter.ConvertHtmlNode(child)).OfType<Inline>();
            Span emphasis = new Span();
            emphasis.FontStyle = FontStyles.Italic;
            emphasis.Inlines.AddRange(emphasisContent);
            return emphasis;
        }

        /// <summary>
        /// Converts the specified strong (or bold) HTML element to a span flow document element, which has a bold font style.
        /// </summary>
        /// <param name="strongHtmlElement">The strong (or bold) HTML element that is to be converted.</param>
        /// <returns>Returns a span flow document element, which has a bold font style.</returns>
        [HtmlElementConverter("B")]
        [HtmlElementConverter("STRONG")]
        private static TextElement ConvertStrongElement(IHtmlElement strongHtmlElement)
        {
            IEnumerable<Inline> boldContent = strongHtmlElement.ChildNodes.Select(child => HtmlConverter.ConvertHtmlNode(child)).OfType<Inline>();
            Span bold = new Span();
            bold.FontWeight = FontWeights.Bold;
            bold.Inlines.AddRange(boldContent);
            return bold;
        }

        /// <summary>
        /// Converts the specified small HTML element to a span flow document element, which has a smaller font size.
        /// </summary>
        /// <param name="smallHtmlElement">The small HTML element that is to be converted.</param>
        /// <returns>Returns a span flow document element, which has a smaller font size.</returns>
        [HtmlElementConverter("SMALL")]
        private static TextElement ConvertSmallElement(IHtmlElement smallHtmlElement)
        {
            IEnumerable<Inline> smallContent = smallHtmlElement.ChildNodes.Select(child => HtmlConverter.ConvertHtmlNode(child)).OfType<Inline>();
            Span small = new Span();
            small.FontSize = (double)new FontSizeConverter().ConvertFrom("10pt");
            small.Inlines.AddRange(smallContent);
            return small;
        }

        /// <summary>
        /// Converts the specified horizontal ruler HTML element to a table flow document element, which simulates the horizontal line.
        /// </summary>
        /// <param name="smallHtmlElement">The horizontal ruler HTML element that is to be converted.</param>
        /// <returns>Returns a table flow document element, which simulates the horizontal line.</returns>
        [HtmlElementConverter("HR")]
        private static TextElement ConvertHorizontalRulerElement(IHtmlElement smallHtmlElement)
        {
            Table horizontalRuler = new Table();
            TableRowGroup tableRowGroup = new TableRowGroup();
            horizontalRuler.RowGroups.Add(tableRowGroup);
            TableRow tableRow = new TableRow();
            tableRowGroup.Rows.Add(tableRow);
            TableCell tableCell = new TableCell
            {
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(0.0d, 1.0d, 0.0d, 0.0d),
                FontSize = 0.1
            };
            tableRow.Cells.Add(tableCell);
            return horizontalRuler;
        }

        /// <summary>
        /// Converts the specified quotatiopn HTML element to a span flow document element, which is wrapped in quotation marks.
        /// </summary>
        /// <param name="quotationHtmlElement">The quotation HTML element that is to be converted.</param>
        /// <returns>Returns a span flow document element, which is wrapped in quotation marks.</returns>
        [HtmlElementConverter("Q")]
        private static TextElement ConvertQuotationElement(IHtmlElement quotationHtmlElement)
        {
            IEnumerable<Inline> quotationContent = quotationHtmlElement.ChildNodes.Select(child => HtmlConverter.ConvertHtmlNode(child)).OfType<Inline>();
            Span quotation = new Span();
            quotation.Inlines.Add(new Run("\""));
            quotation.Inlines.AddRange(quotationContent);
            quotation.Inlines.Add(new Run("\""));
            return quotation;
        }

        /// <summary>
        /// Converts the specified strike-through HTML element to a span flow document element, which is striked through.
        /// </summary>
        /// <param name="strikeThroughHtmlElement">The strike-through HTML element that is to be converted.</param>
        /// <returns>Returns a span flow document element, which is striked through.</returns>
        [HtmlElementConverter("S")]
        [HtmlElementConverter("STRIKE")]
        private static TextElement ConvertStrikeThroughElement(IHtmlElement strikeThroughHtmlElement)
        {
            IEnumerable<Inline> strikeThroughContent = strikeThroughHtmlElement.ChildNodes.Select(child => HtmlConverter.ConvertHtmlNode(child)).OfType<Inline>();
            Span strikeThrough = new Span();
            strikeThrough.TextDecorations.Add(TextDecorations.Strikethrough);
            strikeThrough.Inlines.AddRange(strikeThroughContent);
            return strikeThrough;
        }

        /// <summary>
        /// Converts the specified underline HTML element to a span flow document element, which is underlined.
        /// </summary>
        /// <param name="underlineHtmlElement">The underline HTML element that is to be converted.</param>
        /// <returns>Returns a span flow document element, which is underlined.</returns>
        [HtmlElementConverter("U")]
        private static TextElement ConvertUnderlineElement(IHtmlElement underlineHtmlElement)
        {
            IEnumerable<Inline> underlineContent = underlineHtmlElement.ChildNodes.Select(child => HtmlConverter.ConvertHtmlNode(child)).OfType<Inline>();
            Span underline = new Span();
            underline.TextDecorations.Add(TextDecorations.Underline);
            underline.Inlines.AddRange(underlineContent);
            return underline;
        }

        /// <summary>
        /// Converts the specified anchor HTML element to a hyperlink flow document element.
        /// </summary>
        /// <param name="anchorHtmlElement">The anchor HTML element that is to be converted.</param>
        /// <returns>Returns a hyperlink flow document element.</returns>
        [HtmlElementConverter("A")]
        private static TextElement ConvertAnchorElement(IHtmlElement anchorHtmlElement)
        {
            IEnumerable<Inline> hyperlinkContent = anchorHtmlElement.ChildNodes.Select(child => HtmlConverter.ConvertHtmlNode(child)).OfType<Inline>();
            Hyperlink hyperlink = new Hyperlink();
            hyperlink.Inlines.AddRange(hyperlinkContent);
            string hyperReference = anchorHtmlElement.GetAttribute("href");
            Uri navigationUri;
            if (Uri.TryCreate(hyperReference, UriKind.RelativeOrAbsolute, out navigationUri))
                hyperlink.NavigateUri = navigationUri;
            return hyperlink;
        }

        /// <summary>
        /// Converts the specified heading HTML element to a paragraph flow document element, which has the correct font size.
        /// </summary>
        /// <param name="headingHtmlElement">The heading HTML element that is to be converted.</param>
        /// <returns>Returns a paragraph flow document element, which has the correct font size.</returns>
        [HtmlElementConverter("H1")]
        [HtmlElementConverter("H2")]
        [HtmlElementConverter("H3")]
        [HtmlElementConverter("H4")]
        [HtmlElementConverter("H5")]
        [HtmlElementConverter("H6")]
        private static TextElement ConvertHeadingElement(IHtmlElement headingHtmlElement)
        {
            IEnumerable<Inline> headingContent = headingHtmlElement.ChildNodes.Select(child => HtmlConverter.ConvertHtmlNode(child)).OfType<Inline>();
            Paragraph heading = new Paragraph();
            double fontSize = (double)new FontSizeConverter().ConvertFromInvariantString(new string[] { "24pt", "18pt", "13.5pt", "12pt", "10pt", "7.5pt" }[Convert.ToInt32(headingHtmlElement.NodeName.Substring(1)) - 1]);
            heading.FontSize = fontSize;
            heading.Margin = new Thickness(double.NaN, double.NaN, double.NaN, 0.0d);
            heading.Inlines.AddRange(headingContent);
            return heading;
        }

        /// <summary>
        /// Converts the specified block quote HTML element to a paragraph flow document element, which is styled like a block quote.
        /// </summary>
        /// <param name="blockQuoteHtmlElement">The block quote HTML element that is to be converted.</param>
        /// <returns>Returns a paragraph flow document element, which is styled like a block quote.</returns>
        [HtmlElementConverter("BLOCKQUOTE")]
        private static TextElement ConvertBlockQuoteElement(IHtmlElement blockQuoteHtmlElement)
        {
            IEnumerable<Inline> blockQuoteContent = blockQuoteHtmlElement.ChildNodes.Select(child => HtmlConverter.ConvertHtmlNode(child)).OfType<Inline>();
            Paragraph blockQuote = new Paragraph();
            blockQuote.Margin = new Thickness(40.0d, double.NaN, 40.0d, double.NaN);
            blockQuote.Inlines.AddRange(blockQuoteContent);
            return blockQuote;
        }

        /// <summary>
        /// Converts the specified subscript HTML element to a run flow document element, which is in subscript.
        /// </summary>
        /// <param name="subscriptHtmlElement">The subscript HTML element that is to be converted.</param>
        /// <returns>Returns a run flow document element, which is in subscript.</returns>
        [HtmlElementConverter("SUB")]
        private static TextElement ConvertSubscriptElement(IHtmlElement subscriptHtmlElement)
        {
            Run subscript = new Run(subscriptHtmlElement.TextContent);
            subscript.FontSize = (double)new FontSizeConverter().ConvertFrom("8pt");
            subscript.BaselineAlignment = BaselineAlignment.Subscript;
            return subscript;
        }

        /// <summary>
        /// Converts the specified superscript HTML element to a run flow document element, which is in superscript.
        /// </summary>
        /// <param name="superscriptHtmlElement">The superscript HTML element that is to be converted.</param>
        /// <returns>Returns a run flow document element, which is in superscript.</returns>
        [HtmlElementConverter("SUP")]
        private static TextElement ConvertSuperscriptElement(IHtmlElement superscriptHtmlElement)
        {
            Run superscript = new Run(superscriptHtmlElement.TextContent);
            superscript.FontSize = (double)new FontSizeConverter().ConvertFrom("8pt");
            superscript.BaselineAlignment = BaselineAlignment.Superscript;
            return superscript;
        }

        /// <summary>
        /// Converts the specified ordered or unordered list HTML element to a list flow document element.
        /// </summary>
        /// <param name="listHtmlElement">The ordered or unordered list HTML element that is to be converted.</param>
        /// <returns>Returns a list flow document element.</returns>
        [HtmlElementConverter("OL")]
        [HtmlElementConverter("UL")]
        private static TextElement ConvertListElement(IHtmlElement listHtmlElement)
        {
            IEnumerable<ListItem> listItems = listHtmlElement.ChildNodes.Select(child => HtmlConverter.ConvertHtmlNode(child)).OfType<ListItem>();
            List list = new List();
            list.MarkerStyle = listHtmlElement.TagName.ToUpperInvariant() == "UL" ? TextMarkerStyle.Circle : TextMarkerStyle.Decimal;
            list.ListItems.AddRange(listItems);
            return list;
        }

        /// <summary>
        /// Converts the specified list item HTML element to a list item flow document element.
        /// </summary>
        /// <param name="listItemHtmlElement">The list item HTML element that is to be converted.</param>
        /// <returns>Returns a list item flow document element.</returns>
        [HtmlElementConverter("LI")]
        private static TextElement ConvertListItemElement(IHtmlElement listItemHtmlElement)
        {
            // Gets the content of the list item
            List<TextElement> listItemTextElementContent = listItemHtmlElement.ChildNodes.Select(child => HtmlConverter.ConvertHtmlNode(child)).ToList();

            // There is a problem with the white-space character handling, when the last item of the list item content is an empty run, then there is too much
            // space to the bottom, therefore it is removed
            listItemTextElementContent.RemoveAll(textElement => textElement is Run && string.IsNullOrWhiteSpace((textElement as Run).Text));

            // List items may only contain block elements, therefore all inline elements, which are content of the list item HTML element are wrapped in paragraphs
            IEnumerable<Block> listItemContent = HtmlConverter.ConvertTextElementsToBlocks(listItemTextElementContent);

            // Removes margins and paddings from any nested list, because otherwise there would be too much space between the nested list and its parent
            foreach (List list in listItemContent.OfType<List>())
            {
                list.Margin = new Thickness(double.NaN, 5.0d, double.NaN, 5.0d);
                list.Padding = new Thickness(25.0d, double.NaN, double.NaN, double.NaN);
            }

            // Creates the list item, adds the content to it, and returns it
            ListItem listItem = new ListItem();
            listItem.Blocks.AddRange(listItemContent);
            return listItem;
        }

        #endregion

        #region Public Static Methods

        /// <summary>
        /// Converts the HTML in the specified stream into flow document content.
        /// </summary>
        /// <param name="stream">The stream that contains the HTML that is to be converted.</param>
        /// <param name="cancellationToken">The cancellation token, which can be used to cancel the parsing and converting of the HTML.</param>
        /// <exception cref="InvalidOperationException">If the HTML could not be parsed, then an <see cref="InvalidOperationException"/> exception is thrown.</exception>
        /// <returns>Returns the converted flow document content.</returns>
        public static async Task<Section> ConvertFromStreamAsync(Stream stream, CancellationToken cancellationToken)
        {
            // Tries to parse the HTML, if it could not be parsed, then an exception is thrown
            IHtmlDocument htmlDocument;
            try
            {
                HtmlParser htmlParser = new HtmlParser();
                htmlDocument = await new HtmlParser().ParseAsync(stream);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(Resources.Localization.HtmlConverter.HtmlCouldNotBeParsedExceptionMessage, exception);
            }

            // Converts the HTML to block items
            IEnumerable<TextElement> textElements = htmlDocument.Body.ChildNodes.Select(childNode => HtmlConverter.ConvertHtmlNode(childNode)).ToList();

            // Wraps the converted text elements in block elements if needed, because on the top level only block elements are allowed
            IEnumerable<Block> blockElements = HtmlConverter.ConvertTextElementsToBlocks(textElements);

            // Applies the proper white-space handling to the content
            HtmlConverter.ApplyWhiteSpaceHandling(blockElements);

            // Wraps the converted content in a section and returns it, so that it can be added to a flow document
            Section flowDocumentContent = new Section();
            flowDocumentContent.Blocks.AddRange(blockElements);
            return flowDocumentContent;
        }

        /// <summary>
        /// Converts the HTML in the specified stream into flow document content.
        /// </summary>
        /// <param name="stream">The stream that contains the HTML that is to be converted.</param>
        /// <exception cref="InvalidOperationException">If the HTML could not be parsed, then an <see cref="InvalidOperationException"/> exception is thrown.</exception>
        /// <returns>Returns the converted flow document content.</returns>
        public static Task<Section> ConvertFromStreamAsync(Stream stream) => HtmlConverter.ConvertFromStreamAsync(stream, new CancellationTokenSource().Token);

        /// <summary>
        /// Downloads the HTML from the specified URI and converts it into flow document content.
        /// </summary>
        /// <param name="uri">The URI from which the HTML is to be loaded.</param>
        /// <param name="cancellationToken">The cancellation token, which can be used to cancel the parsing and converting of the HTML.</param>
        /// <exception cref="InvalidOperationException">If the HTML could not be downloaded or parsed, then an <see cref="InvalidOperationException"/> exception is thrown.</exception>
        /// <returns>Returns the converted flow document content.</returns>
        public static async Task<Section> ConvertFromUriAsync(Uri uri, CancellationToken cancellationToken)
        {
            // Tries to download the HTML from the specified URI, if it could not be loaded, then an exception is thrown
            try
            {
                using (HttpClient httpClient = new HttpClient())
                {
                    HttpResponseMessage responseMessage = await httpClient.GetAsync(uri, cancellationToken);
                    responseMessage.EnsureSuccessStatusCode();
                    return await HtmlConverter.ConvertFromStreamAsync(await responseMessage.Content.ReadAsStreamAsync(), cancellationToken);
                }
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(Resources.Localization.HtmlConverter.HtmlPageCouldNotBeLoadedExceptionMessage, exception);
            }
        }

        /// <summary>
        /// Downloads the HTML from the specified URI and converts it into flow document content.
        /// </summary>
        /// <param name="uri">The URI from which the HTML is to be loaded.</param>
        /// <exception cref="InvalidOperationException">If the HTML could not be downloaded or parsed, then an <see cref="InvalidOperationException"/> exception is thrown.</exception>
        /// <returns>Returns the converted flow document content.</returns>
        public static Task<Section> ConvertFromUriAsync(Uri uri) => HtmlConverter.ConvertFromUriAsync(uri, new CancellationTokenSource().Token);

        /// <summary>
        /// Converts the specified HTML file into flow document content.
        /// </summary>
        /// <param name="fileName">The name of the HTML file, that is to be loaded and converted into flow document content.</param>
        /// <param name="cancellationToken">The cancellation token, which can be used to cancel the parsing and converting of the HTML.</param>
        /// <exception cref="InvalidOperationException">If the HTML could not be parsed, then an <see cref="InvalidOperationException"/> exception is thrown.</exception>
        /// <exception cref="FileNotFoundException">If the specified file could not be found, then a <see cref="FileNotFoundException"/> exception is thrown.</exception>
        /// <returns>Returns the converted flow document content.</returns>
        public static Task<Section> ConvertFromFileAsync(string fileName, CancellationToken cancellationToken)
        {
            // Checks if the file exists, if not then an exception is thrown
            if (!File.Exists(fileName))
                throw new FileNotFoundException(Resources.Localization.HtmlConverter.HtmlFileCouldNotBeFoundExceptionMessage, fileName);

            // Loads the file, converts it into flow document content, and returns the converted flow document
            using (FileStream fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read))
                return HtmlConverter.ConvertFromStreamAsync(fileStream, cancellationToken);
        }

        /// <summary>
        /// Converts the specified HTML file into flow document content.
        /// </summary>
        /// <param name="fileName">The name of the HTML file, that is to be loaded and converted into flow document content.</param>
        /// <exception cref="InvalidOperationException">If the HTML could not be parsed, then an <see cref="InvalidOperationException"/> exception is thrown.</exception>
        /// <exception cref="FileNotFoundException">If the specified file could not be found, then a <see cref="FileNotFoundException"/> exception is thrown.</exception>
        /// <returns>Returns the converted flow document content.</returns>
        public static Task<Section> ConvertFromFileAsync(string fileName) => HtmlConverter.ConvertFromFileAsync(fileName, new CancellationTokenSource().Token);

        /// <summary>
        /// Converts the specified HTML into flow document content.
        /// </summary>
        /// <param name="html">The HTML string that is to be converted into flow document content.</param>
        /// <param name="cancellationToken">The cancellation token, which can be used to cancel the parsing and converting of the HTML.</param>
        /// <exception cref="InvalidOperationException">If the HTML could not be parsed, then an <see cref="InvalidOperationException"/> exception is thrown.</exception>
        /// <returns>Returns the converted flow document content.</returns>
        public static Task<Section> ConvertFromStringAsync(string html, CancellationToken cancellationToken)
        {
            // Converts the string into a memory stream, converts the HTML into flow document content, and returns it
            using (MemoryStream memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(html)))
                return HtmlConverter.ConvertFromStreamAsync(memoryStream, cancellationToken);
        }

        /// <summary>
        /// Converts the specified HTML into flow document content.
        /// </summary>
        /// <param name="html">The HTML string that is to be converted into flow document content.</param>
        /// <exception cref="InvalidOperationException">If the HTML could not be parsed, then an <see cref="InvalidOperationException"/> exception is thrown.</exception>
        /// <returns>Returns the converted flow document content.</returns>
        public static Task<Section> ConvertFromStringAsync(string html) => HtmlConverter.ConvertFromStringAsync(html, new CancellationTokenSource().Token);

        #endregion

        #region Nested Types

        /// <summary>
        /// Represents an attribute, which marks a method as a converter for an HTML element.
        /// </summary>
        [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
        private class HtmlElementConverterAttribute : Attribute
        {
            #region Constructors

            /// <summary>
            /// Initializes a new <see cref="HtmlElementConverterAttribute"/> instance.
            /// </summary>
            /// <param name="htmlElementName">The name of the HTML element that the method, which was marked with this attribute, is able to convert.</param>
            public HtmlElementConverterAttribute(string htmlElementName)
            {
                this.HtmlElementName = htmlElementName;
            }

            #endregion

            #region Public Properties

            /// <summary>
            /// Gets the name of the HTML element that the method, which was marked with this attribute, is able to convert.
            /// </summary>
            public string HtmlElementName { get; private set; }

            #endregion
        }

        #endregion
    }
}