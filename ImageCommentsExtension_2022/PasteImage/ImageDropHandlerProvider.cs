using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.DragDrop;
using Microsoft.VisualStudio.Utilities;

namespace ImageCommentsExtension_2022 {

    [Export(typeof(IDropHandlerProvider))]
    [DropFormat("CF_VSSTGPROJECTITEMS")]
    [DropFormat("FileDrop")]
    [Name("IgnoreDropHandler")]
    [
        ContentType("text"),
        //ContentType("CSharp"), ContentType("C/C++"), ContentType("Basic"), ContentType("Python"), ContentType("F#"), ContentType("TypeScript"), 
        ////ContentType("Any"),
        //ContentType("HTMLXProjection"),
        //ContentType("plaintext"),
        //ContentType("JSON"),
        //ContentType("css"),
    ]
    [Order(Before = "DefaultFileDropHandler")]
    internal class ImageDropHandlerProvider : IDropHandlerProvider
    {
        [Import]
        ITextDocumentFactoryService TextDocumentFactoryService { get; set; }

        public IDropHandler GetAssociatedDropHandler(IWpfTextView view)
        {
            ITextDocument document;

            if (TextDocumentFactoryService.TryGetTextDocument(view.TextBuffer, out document))
            {
                return view.Properties.GetOrCreateSingletonProperty(() => new ImageDropHandler(view, document.FilePath));
            }

            return null;
        }
    }
}