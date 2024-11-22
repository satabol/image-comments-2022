using System.ComponentModel.Composition;
using System.Windows.Threading;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;

namespace ImageCommentsExtension_2022 {
    [Export(typeof(IVsTextViewCreationListener))]
    [
        ContentType("text"),
        //ContentType("HTMLX")
        //ContentType("CSharp"), ContentType("C/C++"), ContentType("Basic"), ContentType("Python"), ContentType("F#"), ContentType("TypeScript"), 
        ////ContentType("text/html"), 
        ////ContentType("HTMLXProjection"), 
        ////ContentType("htmlxprojection"), 
        ////ContentType("Html"), 
        ////ContentType("htmlx"), 
        //ContentType("HTMLXProjection"), 
        //ContentType("plaintext"), 
        //ContentType("JSON"), 
        //ContentType("css"), 
    ]
    [
        TextViewRole(PredefinedTextViewRoles.Document),
        TextViewRole(PredefinedTextViewRoles.PrimaryDocument),
    ]
    public class CommandRegistration : IVsTextViewCreationListener {
        [Import]
        IVsEditorAdaptersFactoryService EditorAdaptersFactoryService { get; set; }

        [Import]
        ITextDocumentFactoryService TextDocumentFactoryService { get; set; }

        [Import]
        IClassifierAggregatorService ClassifierAggregatorService { get; set; }

        [Import]
        ITextStructureNavigatorSelectorService NavigatorService { get; set; }

        public void VsTextViewCreated(IVsTextView textViewAdapter) {
            ThreadHelper.Generic.BeginInvoke(DispatcherPriority.ApplicationIdle, () => {
                var textView = EditorAdaptersFactoryService.GetWpfTextView(textViewAdapter);
                ITextDocument document = null;

                if (textView == null || !TextDocumentFactoryService.TryGetTextDocument(textView.TextBuffer, out document))
                    return;

                textView.Properties.GetOrCreateSingletonProperty(() => new ImagePasteCommandTarget(textViewAdapter, textView, document.FilePath));
            });
        }
    }
}