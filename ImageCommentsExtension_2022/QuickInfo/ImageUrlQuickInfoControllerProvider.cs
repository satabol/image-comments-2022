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
using System.Reflection;
using System.Globalization;
using System.IO;

// На основе примера: https://msdn.microsoft.com/en-us/library/ee197646.aspx

namespace ImageCommentsExtension_2022 {
    [Export(typeof(IIntellisenseControllerProvider))]
    [Name("ToolTip QuickInfo URL Preview Controller")]
    [
        ContentType("text"),
        //ContentType("CSharp"), ContentType("C/C++"), ContentType("Basic"), ContentType("Python"), ContentType("F#"), ContentType("TypeScript"),
    ] //ContentType("Code")]
    internal class ImageUrlQuickInfoControllerProvider : IIntellisenseControllerProvider {

        [Import]
        internal IAsyncQuickInfoBroker QuickInfoBroker { get; set; }

        public IIntellisenseController TryCreateIntellisenseController( ITextView textView, IList<ITextBuffer> subjectBuffers ) {
            return new ImageUrlQuickInfoController(textView, subjectBuffers, this);
        }

    }
}
