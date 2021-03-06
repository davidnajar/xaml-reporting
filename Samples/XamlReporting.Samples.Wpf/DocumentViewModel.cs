﻿
#region Using Directives

using System.Windows.Documents;

#endregion

namespace XamlReporting.Samples.Wpf
{
    /// <summary>
    /// Represents the view model for the document that is to be created.
    /// </summary>
    public class DocumentViewModel
    {
        #region Public Properties

        /// <summary>
        /// Gets or sets the title of the document.
        /// </summary>
        public string Title { get; set; } = "Hello World from WPF";

        /// <summary>
        /// Gets or sets the abstract of the document.
        /// </summary>
        public string Abstract { get; set; } = "This is a simple test of the XAML Reporting engine in a WPF application.";

        /// <summary>
        /// Gets or sets the name of the author.
        /// </summary>
        public string Author { get; set; } = "Jane Doe";

        /// <summary>
        /// Gets an HTML string, which is displayed in the document.
        /// </summary>
        public string Html { get; } = @"
            <!DOCTYPE html>
            <html>
                <head></head>
                <body>
                    <h1>Heading 1</h1>
                    <p>
                        Lorem ipsum dolor sit amet sed diam nonumy eirmod tempor invidunt ut labore et dolore magna
                        <strong>aliquyam</strong> erat, sed diam voluptua. At vero eos et accusam et justo duo dolores
                        et ea rebum. Stet clita kasd gubergren, no sea takimata sanctus est Lorem ipsum dolor sit amet.
                    </p>
                    <ol>
                        <li>List item 1</li>
                        <li>
                            List item 2
                            <ul>
                                <li>List item 3</li>
                                <li>List item 4</li>
                                <li>List item 5</li>
                            </ul>
                        </li>
                        <li>List item 6</li>
                    </ol>
                    <h2>Heading 1.1</h2>
                    <section>
                        Lorem ipsum dolor sit amet sed diam nonumy eirmod tempor invidunt ut labore et dolore magna
                        <q>aliquyam erat, sed diam voluptua.</q> At vero eos et accusam et justo duo dolores et ea
                        rebum. Stet clita kasd gubergren, no sea takimata sanctus est Lorem ipsum dolor sit amet.
                    </section>
                    <h2>Heading 1.2</h2>
                    <p>
                        Lorem ipsum dolor sit <em>amet sed</em> diam nonumy eirmod tempor invidunt ut labore et dolore.
                        <br/><br/>
                        Stet clita kasd gubergren, no sea <u>takimata</u> sanctus est Lorem ipsum dolor sit <small>amet</small>.
                    </p>
                    <blockquote>
                        <em>
                            Lorem ipsum dolor sit amet, consetetur sadipscing elitr, sed diam nonumy eirmod tempor
                            invidunt ut labore et dolore magna aliquyam erat, sed diam voluptua. At vero eos et accusam
                            et justo duo dolores et ea rebum. Stet clita kasd gubergren, no sea takimata sanctus est
                            Lorem ipsum dolor sit amet.
                        </em>
                    </blockquote>
                    <hr/>
                    <p>
                        Lorem ipsum dolor sit amet sed diam nonumy eirmod tempor invidunt ut labore et dolore magna
                        aliquyam erat, sed diam voluptua. At vero eos et accusam et justo duo dolores et ea rebum. Stet
                        clita kasd gubergren, no sea takimata sanctus est Lorem ipsum dolor sit amet.
                    </p>
                    <h1>Heading 2</h1>
                    Lorem ipsum dolor sit amet, <s>consetetur</s> sadipscing elitr, sed diam nonumy eirmod tempor
                    invidunt<sup>1</sup> ut labore et dolore magna aliquyam erat, sed diam voluptua. At vero eos et accusam
                    et justo duo dolores<sub>2</sub> et ea rebum. Stet clita kasd gubergren, no sea takimata sanctus est
                    Lorem ipsum dolor sit amet: <a href='https://www.google.de'>Google</a>
                    <h1>Heading 3</h1>
                    <pre>
                        Lorem ipsum dolor sit amet,            <s>consetetur</s>            sadipscing elitr, sed diam
                        nonumy eirmod tempor
                    </pre
                </body>
            </html>";

        #endregion
    }
}