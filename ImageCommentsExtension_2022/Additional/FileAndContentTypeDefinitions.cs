using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace ImageCommentsExtension_2022.Additional {
    internal static class FileAndContentTypeDefinitions {
        [Export]
        [Name("htmlx")]
        [BaseDefinition("text")]
        internal static ContentTypeDefinition hidingContentTypeDefinition;

        [Export]
        [FileExtension(".html")]
        [ContentType("htmlx")]
        internal static FileExtensionToContentTypeDefinition hiddenFileExtensionDefinition;
    }
}
