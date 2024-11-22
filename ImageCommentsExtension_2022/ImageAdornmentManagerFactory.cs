namespace ImageCommentsExtension_2022
{
    using System.ComponentModel.Composition;
    using Microsoft.VisualStudio.Text.Editor;
    using Microsoft.VisualStudio.Utilities;

    /// <summary>
    /// Establishes an <see cref="IAdornmentLayer"/> to place the adornment on and exports the <see cref="IWpfTextViewCreationListener"/>
    /// that instantiates the adornment on the event of a <see cref="IWpfTextView"/>'s creation
    /// </summary>
    [Export(typeof(IWpfTextViewCreationListener))]
    [
        ContentType("text"),
        //ContentType("CSharp"), ContentType("C/C++"), ContentType("Basic"), ContentType("F#"), ContentType("TypeScript"),
        ////ContentType("Any"), 
        //ContentType("HTMLXProjection"),
        //ContentType("plaintext"),
        //ContentType("JSON"),
        //ContentType("css"),
    ] // ContentType("Code")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class ImageAdornmentManagerFactory : IWpfTextViewCreationListener
    {
        /// <summary>
        /// Defines the adornment layer for the adornment. This layer is ordered 
        /// after the selection layer in the Z-order
        /// </summary>
        [Export(typeof(AdornmentLayerDefinition))]
        [Name("ImageCommentLayer")]
        [Order(After = PredefinedAdornmentLayers.Selection, Before = PredefinedAdornmentLayers.Text)]
        [TextViewRole(PredefinedTextViewRoles.Document)]
        public AdornmentLayerDefinition editorAdornmentLayer = null;

        /// <summary>
        /// Instantiates a ImageAdornment manager when a textView is created.
        /// </summary>
        /// <param name="textView">The <see cref="IWpfTextView"/> upon which the adornment should be placed</param>
        public void TextViewCreated(IWpfTextView textView)
        {
            textView.Properties.GetOrCreateSingletonProperty<ImageAdornmentManager>(() => new ImageAdornmentManager(textView));
        }
    }
}
