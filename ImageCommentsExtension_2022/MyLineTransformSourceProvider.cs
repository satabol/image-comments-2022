using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Text.Formatting;

namespace ImageCommentsExtension_2022
{
    [Export(typeof(ILineTransformSourceProvider))]
    [
        ContentType("text"),
        //ContentType("CSharp"), ContentType("C/C++"), ContentType("Basic"), ContentType("Python"), ContentType("F#"), ContentType("TypeScript"), 
        //ContentType("Any"),
        //ContentType("HTMLXProjection"),
        //ContentType("plaintext"),
        //ContentType("JSON"),
        //ContentType("css"),
    ] //ContentType("Code")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal class MyLineTransformSourceProvider : ILineTransformSourceProvider
    {
        ILineTransformSource ILineTransformSourceProvider.Create(IWpfTextView view)
        {
            ImageAdornmentManager manager = view.Properties.GetOrCreateSingletonProperty<ImageAdornmentManager>(() => new ImageAdornmentManager(view));
            return new MyLineTransformSource(manager);
        }
    }
}