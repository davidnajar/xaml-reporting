﻿
#region Using Directives

using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Markup;

#endregion

namespace System.Windows.Documents.Reporting
{
    /// <summary>
    /// Represents a document part, which has only one fixed page.
    /// </summary>
    [ContentProperty(nameof(Page))]
    public class PagePart : DocumentPart
    {
        #region Public Properties

        /// <summary>
        /// Gets or sets the content of the page part.
        /// </summary>
        public FixedPage Page { get; set; }

        #endregion

        #region DocumentPart Implementation

        /// <summary>
        /// Renders the fixed page.
        /// </summary>
        /// <param name="dataContext">The data context, that is to be used during the rendering. The document part can bind to the content of this data context.</param>
        /// <param name="progress">The object which is used to report the progess of the rendering process.</param>
        /// <returns>Returns a list, which only contains the single rendered fixed page.</returns>
        public override Task<IEnumerable<FixedPage>> RenderAsync(object dataContext, IProgress<double> progress)
        {
            // Checks if the fixed page exists, if not then nothing can be rendered, therefore an empty list of fixed pages is returned
            if (this.Page == null)
                return Task.FromResult(new List<FixedPage>() as IEnumerable<FixedPage>);

            // Reports that the page rendering has started
            progress.Report(0);

            // Sets the data context of the fixed page, so that it bind against its contents
            this.Page.DataContext = dataContext;

            // Initially fixed page has an actual width and height of 0, this makes it impossible for its contents to stretch the whole page, without having to size them absolutely, therefore the layout of the fixed page is updated, so that its actual width and height are correct
            this.Page.Measure(new Size(this.Page.Width, this.Page.Height));
            this.Page.Arrange(new Rect(0, 0, this.Page.Width, this.Page.Height));
            this.Page.UpdateLayout();

            // Reports that the page has been rendered
            progress.Report(1);

            // Returns a list, which only contains the one fixed page that was rendered
            return Task.FromResult(new List<FixedPage> { this.Page } as IEnumerable<FixedPage>);
        }

        #endregion
    }
}