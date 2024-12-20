namespace ImageCommentsExtension_2022
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Windows.Controls;
    using System.Windows.Media;
    using System.Xml;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Shell.Interop;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Editor;
    using Microsoft.VisualStudio.Text.Formatting;
    using Microsoft.VisualStudio.Text.Tagging;
    using Microsoft.VisualStudio.TextManager.Interop;

    /// <summary>
    /// Important class. Handles creation of image adornments on appropriate lines and associated error tags.
    /// </summary>
    public class ImageAdornmentManager : ITagger<ErrorTag>, IDisposable
    {
        private readonly IAdornmentLayer _layer;
        private readonly IWpfTextView _view;
        private readonly VariableExpander _variableExpander;
        private string _contentTypeName;
        private bool _initialised1;
        private bool _initialised2;
        private readonly List<ITagSpan<ErrorTag>> _errorTags;

        public static bool Enabled { get; set; }

        /// <summary>
        /// Dictionary to map line number to image
        /// </summary>
        internal ConcurrentDictionary<int, MyImage> Images { get; set; }

        /// <summary>
        /// Image cache. string - download url (real paths).
        /// </summary>
        //static ConcurrentDictionary<string, MyImage> dict_UrlImages = new ConcurrentDictionary<string, MyImage>();

        /// <summary>
        /// Initializes static members of the <see cref="ImageAdornmentManager"/> class
        /// </summary>
        static ImageAdornmentManager()
        {
            Enabled = true;
        }

        /// <summary>
        /// Enables or disables image comments. TODO: Make enable/disable mechanism better, e.g. specific to each editor instance and persistent
        /// </summary>
        public static void ToggleEnabled()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Enabled = !Enabled;
            string message = string.Format("Image comments {0}. Scroll editor window(s) to update.",
                Enabled ? "enabled" : "disabled");
            UIMessage.Show(message);
        }

        public ImageAdornmentManager(IWpfTextView view)
        {
            _view = view;
            _layer = view.GetAdornmentLayer("ImageCommentLayer");
            Images = new ConcurrentDictionary<int, MyImage>();
            _view.LayoutChanged += LayoutChangedHandler;

            _contentTypeName = view.TextBuffer.ContentType.TypeName;
            _view.TextBuffer.ContentTypeChanged += contentTypeChangedHandler;

            _errorTags = new List<ITagSpan<ErrorTag>>();
            _variableExpander = new VariableExpander(_view);
        }

        private void contentTypeChangedHandler(object sender, ContentTypeChangedEventArgs e)
        {
            _contentTypeName = e.AfterContentType.TypeName;
        }

        /// <summary>
        /// On layout change add the adornment to any reformatted lines
        /// </summary>
        private void LayoutChangedHandler(object sender, TextViewLayoutChangedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!Enabled)
                return;

            _errorTags.Clear();
            if (TagsChanged != null)
            {
                TagsChanged(this, new SnapshotSpanEventArgs(new SnapshotSpan(_view.TextSnapshot, new Span(0, _view.TextSnapshot.Length))));
            }

            OnTagsChanged(new SnapshotSpan(_view.TextSnapshot, new Span(0, _view.TextSnapshot.Length)));
            string file_path = ImageAdornmentManagerHelper.GetPath(_view);
            string document_dir = "";
            if (file_path == null) {
                file_path = "";
            } else {
                document_dir = Path.GetDirectoryName(file_path);
            }

            foreach (ITextViewLine line in _view.TextViewLines) // TODO [?]: implement more sensible handling of removing error tags, then use e.NewOrReformattedLines
            {
                int lineNumber = line.Snapshot.GetLineFromPosition(line.Start.Position).LineNumber;
                //TODO [?]: Limit rate of calls to the below when user is editing a line
                try
                {
                    
                    CreateVisuals(document_dir, line, lineNumber);
                }
                catch (InvalidOperationException ex)
                {
                    ExceptionHandler.Notify(ex, true);
                }
            }

            // Sometimes, on loading a file in an editor view, the line transform gets triggered before the image adornments 
            // have been added, so the lines don't resize to the image height. So here's a workaround:
            // Changing the zoom level triggers the required update.
            // Need to do it twice - once to trigger the event, and again to change it back to the user's expected level.
            if (!_initialised1)
            {
                _view.ZoomLevel++;
                _initialised1 = true;
            }
            if (!_initialised2)
            {
                _view.ZoomLevel--;
                _initialised2 = true;
            }
        }

        //object locker_image_process_update = new object();
        /// <summary>
        /// Scans text line for matching image comment signature, then adds new or updates existing image adornment
        /// </summary>
        private void CreateVisuals(string document_dir, ITextViewLine line, int lineNumber)
        {
            //lock (locker_image_process_update) 
            {
#pragma warning disable 219
                bool imageDetected = false; // useful for tracing
#pragma warning restore 219

                ThreadHelper.ThrowIfNotOnUIThread();
                try {
                    string lineText = line.Extent.GetText();
                    string matchedText;
                    lineText = lineText.Split(new string[] { "\r\n", "\r"}, StringSplitOptions.None)[0];
                    int matchIndex = ImageCommentParser.Match(_contentTypeName, lineText, out matchedText);
                    if (matchIndex >= 0) {
                        // Get coordinates of text
                        int start = line.Extent.Start.Position + matchIndex;
                        int end = line.Start + (line.Extent.Length - 1);
                        var span = new SnapshotSpan(_view.TextSnapshot, Span.FromBounds(start, end));

                        Exception xmlParseException;
                        string imageUrl;
                        double scale;
                        Color bgColor = new Color();
                        ImageCommentParser.TryParse(matchedText, out imageUrl, out scale, ref bgColor, out xmlParseException);

                        if (xmlParseException != null) {
                            if (Images.ContainsKey(lineNumber)) {
                                _layer.RemoveAdornment(Images[lineNumber]);
                                if (Images.TryRemove(lineNumber, out var ignore) == false) {
                                    Console.WriteLine("cannot remove image");
                                }
                            }

                            _errorTags.Add(new TagSpan<ErrorTag>(span, new ErrorTag("XML parse error", GetErrorMessage(xmlParseException))));

                            return;
                        }

                        MyImage image;
                        //Exception imageLoadingException = null;

                        // Check for and update existing image
                        MyImage existingImage = Images.ContainsKey(lineNumber) ? Images[lineNumber] : null;
                        if (existingImage != null) {
                            image = existingImage;
                            if (existingImage.Url != imageUrl || existingImage.BgColor != bgColor) // URL different, so set new source
                            {
                                existingImage.TrySet(imageUrl, scale, bgColor, () => CreateVisuals(document_dir, line, lineNumber));
                            } //else if (existingImage.Url == imageUrl && Math.Abs(existingImage.Scale - scale) > 0.0001) // URL same but scale changed
                              //if (Math.Abs(existingImage.Scale - scale) > 0.0001) // URL same but scale changed
                            if (image.imageLoadingException == null) {
                                double max_scale = 300.0 / (existingImage.Height / (existingImage.Scale == 0 ? 1 : existingImage.Scale));
                                if (scale > max_scale) {
                                    scale = max_scale;
                                }
                                if (existingImage.Scale != scale && Math.Abs(existingImage.Scale - scale) > 0.0001) {
                                    existingImage.Scale = scale;
                                }
                            }
                        } else // No existing image, so create new one
                          {
                            image = new MyImage(_variableExpander);
                            image.TrySet(imageUrl, scale, bgColor, () => CreateVisuals(document_dir, line, lineNumber));
                            Images.TryAdd(lineNumber, image);
                        }

                        // Position image and add as adornment
                        if (image.imageLoadingException == null) {

                            Geometry g = _view.TextViewLines.GetMarkerGeometry(span);
                            if (g == null) // Exceptional case when image dimensions are massive (e.g. specifying very large scale factor)
                            {
                                throw new InvalidOperationException("Couldn't get source code line geometry. Is the loaded image massive?");
                            }
                            double textLeft = g.Bounds.Left;
                            double textBottom = line.TextBottom;
                            Canvas.SetLeft(image, textLeft);
                            Canvas.SetTop(image, textBottom);

                            // Add image to editor view
                            try {
                                _layer.RemoveAdornment(image);
                                _layer.AddAdornment(AdornmentPositioningBehavior.TextRelative, line.Extent, null, image, null);
                            } catch (Exception ex) {
                                // No expected exceptions, so tell user something is wrong.
                                ExceptionHandler.Notify(ex, true);
                            }
                            //_errorTags.Add(new TagSpan<ErrorTag>(span, new ErrorTag("Current url:", GetErrorMessage(image.Url))));
                        } else {
                            if (Images.ContainsKey(lineNumber)) {
                                //Images.Remove(lineNumber);
                            }

                            _errorTags.Add(new TagSpan<ErrorTag>(span, new ErrorTag("Trouble loading image", GetErrorMessage(image.imageLoadingException)+$"\n {image.additionalExceptionInfo}")));
                        }
                        imageDetected = true;
                    } else {
                        if (Images.ContainsKey(lineNumber)) {
                            if (Images.TryRemove(lineNumber, out var ignore) == false) {
                                Console.WriteLine("cannot remove image");
                            }
                        }
                    }
                }catch(ObjectDisposedException _ex) {

                }catch(Exception _ex) {

                }
            }
        }

        private static string GetErrorMessage(Exception exception)
        {
            Trace.WriteLine("Problem parsing comment text or loading image...\n" + exception);

            string message;
            if (exception is XmlException) { 
                message = "Problem with comment format: " + exception.Message;
            }else if (exception is NotSupportedException) { 
                message = exception.Message + "\nThis problem could be caused by a corrupt, invalid or unsupported image file.";
            }else{ 
                message = exception.Message;
            }
            return message;
        }

        private void UnsubscribeFromViewerEvents()
        {
            _view.LayoutChanged -= LayoutChangedHandler;
            _view.TextBuffer.ContentTypeChanged -= contentTypeChangedHandler;
        }

        #region ITagger<ErrorTag> Members

        public IEnumerable<ITagSpan<ErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            return _errorTags;
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
        protected void OnTagsChanged(SnapshotSpan span)
        {
            EventHandler<SnapshotSpanEventArgs> tagsChanged = TagsChanged;
            if (tagsChanged != null)
                tagsChanged(this, new SnapshotSpanEventArgs(span));
        }

        #endregion

        #region IDisposable Members

        /// <summary>
        /// This is called by the TextView when closing. Events are unsubscribed here.
        /// </summary>
        /// <remarks>
        /// It's actually called twice - once by the IPropertyOwner instance, and again by the ITagger instance
        /// </remarks>
        public void Dispose()
        {
            UnsubscribeFromViewerEvents();
        }

        #endregion
    }

    /// <summary>
    /// https://stackoverflow.com/questions/48068134/get-path-of-the-document-from-iwpftextview-for-non-cs-files
    /// </summary>
    public static class ImageAdornmentManagerHelper {
        public static string GetPath(this IWpfTextView textView) {
            textView.TextBuffer.Properties.TryGetProperty(typeof(IVsTextBuffer), out IVsTextBuffer bufferAdapter);
            var persistFileFormat = bufferAdapter as IPersistFileFormat;

            if (persistFileFormat == null) {
                return null;
            }
            persistFileFormat.GetCurFile(out string filePath, out _);
            return filePath;
        }

    }
}
