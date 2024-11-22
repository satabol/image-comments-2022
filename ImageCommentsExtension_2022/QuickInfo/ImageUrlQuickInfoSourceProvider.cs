using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

// На основе примера: https://msdn.microsoft.com/en-us/library/ee197646.aspx

namespace ImageCommentsExtension_2022 {

    [Export(typeof(IQuickInfoSourceProvider))]
    [Name("ToolTip QuickInfo Preview URL Source")]
    [Order(Before = "Default Quick Info Preview URL Presenter")]
    [ContentType("text")]
    internal class ImageUrlQuickInfoSourceProvider : IQuickInfoSourceProvider {

        [Import]
        internal ITextStructureNavigatorSelectorService NavigatorService { get; set; }

        [Import]
        internal ITextBufferFactoryService TextBufferFactoryService { get; set; }

        public IQuickInfoSource TryCreateQuickInfoSource( ITextBuffer textBuffer ) {
            return new ImageUrlQuickInfoSource(this, textBuffer);
        }
    }
}