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
using System.Windows.Controls;
using System.Text.RegularExpressions;
using System.Diagnostics;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using System.Windows.Media;

// На основе примера: https://msdn.microsoft.com/en-us/library/ee197646.aspx

namespace ImageCommentsExtension_2022 {
    
    internal class ImageUrlQuickInfoSource : IQuickInfoSource {

        private ImageUrlQuickInfoSourceProvider m_provider;
        private ITextBuffer m_subjectBuffer;

        public ImageUrlQuickInfoSource( ImageUrlQuickInfoSourceProvider provider, ITextBuffer subjectBuffer ) {
            m_provider = provider;
            m_subjectBuffer = subjectBuffer;
        }

        public string GetDocumentPath(Microsoft.VisualStudio.Text.ITextSnapshot ts) {
            Microsoft.VisualStudio.Text.ITextDocument textDoc;
            bool rc = ts.TextBuffer.Properties.TryGetProperty(
                typeof(Microsoft.VisualStudio.Text.ITextDocument), out textDoc);
            if (rc && textDoc != null)
                return textDoc.FilePath;
            return null;
        }

        // Регулярное выражение для поиска ссылки в тексте под курсором:
        public static readonly Regex httpUrlRegex = new Regex(@"((http|ftp|https):\/\/[\w\-_]+(\.[\w\-_]+)+([\w\-\.,@?^=%&amp;:/~\+#]*[\w\-\@?^=%&amp;/~\+#])?)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private Regex httpVariablePathRegex = new Regex(@"(\$\((ProjectDir|SolutionDir|ItemDir)\)[^""]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private Regex httpLocalPathRegex = new Regex(@"([a-z]\:\\[^""]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public void AugmentQuickInfoSession( IQuickInfoSession session, IList<object> qiContent, out ITrackingSpan applicableToSpan ) {
            ITrackingSpan _applicableToSpan = null;
            try {
                //DTE2 dte = (DTE2)this.ServiceProvider.GetService(typeof(DTE));
                //string solutionDir = System.IO.Path.GetDirectoryName(dte.Solution.FullName);

                //string solutionDir = null;
                //DTE2 dte = (DTE2)ServiceProvider.GlobalProvider.GetService(typeof(EnvDTE.DTE));
                //if (dte == null) {
                //    solutionDir = System.IO.Path.GetDirectoryName(dte.Solution.FullName);
                //}


                // Map the trigger point down to our buffer.
                SnapshotPoint? subjectTriggerPoint = session.GetTriggerPoint(m_subjectBuffer.CurrentSnapshot);
                if (!subjectTriggerPoint.HasValue) {
                    applicableToSpan = _applicableToSpan;
                    return;
                }

                ITextSnapshot currentSnapshot = subjectTriggerPoint.Value.Snapshot;
                SnapshotSpan querySpan = new SnapshotSpan(subjectTriggerPoint.Value, 0);

                VariableExpander ve = null;
                ITextDocument textDoc;
                bool rc = currentSnapshot.TextBuffer.Properties.TryGetProperty( typeof(Microsoft.VisualStudio.Text.ITextDocument), out textDoc);
                if(rc==true && textDoc != null) {
                    ve = new VariableExpander(textDoc);
                }

                //look for occurrences of our QuickInfo words in the span
                //ITextStructureNavigator navigator = m_provider.NavigatorService.GetTextStructureNavigator(m_subjectBuffer);
                //TextExtent extent = navigator.GetExtentOfWord(subjectTriggerPoint.Value);
                //string searchText = extent.Span.GetText();
                ITextSnapshotLine line = null;

                line = subjectTriggerPoint.Value.GetContainingLine();
                HashSet<string> hUrls = new HashSet<string>();
                // Пройти по всему массиву подходящих подстрок, найденных в строке под курсором:
                foreach (Regex re in new Regex[] { httpUrlRegex, httpVariablePathRegex, httpLocalPathRegex }) { 
                    foreach (Match m in re.Matches(line.GetText())) {
                        if (line.Start.Position + m.Index <= subjectTriggerPoint.Value.Position && subjectTriggerPoint.Value.Position <= line.Start.Position + m.Index + m.Length
                        ) {
                            string path = m.Value;
                            if (ve != null) {
                                path = ve.ProcessText(path);
                            }
                            hUrls.Add(path);
                        }
                    }
                }

                {
                    Exception xmlParseException;
                    string imageUrl;
                    double scale;
                    Color bgColor = new Color();
                    ImageCommentParser.TryParse(line.GetText(), out imageUrl, out scale, ref bgColor, out xmlParseException);

                    if (xmlParseException == null && imageUrl!=null && imageUrl.Length>0) {
                        imageUrl = ve.ProcessText(imageUrl);
                        hUrls.Add(imageUrl);
                    }
                }

                foreach (string str in hUrls) {
                    //HREFPreview iqi = new HREFPreview(str);
                    MyImageControl iqi = new MyImageControl(str);
                    qiContent.Add(iqi);
                    // https://docs.microsoft.com/ru-ru/visualstudio/extensibility/walkthrough-displaying-quickinfo-tooltips?view=vs-2017
                    _applicableToSpan = currentSnapshot.CreateTrackingSpan( querySpan.Start.Position, 1, SpanTrackingMode.EdgeInclusive );
                }
            } catch (Exception ex) {
                StackTrace st = new StackTrace(ex, true);
                string ex_params = "Exception:";
                for (int i = st.FrameCount - 1; i >= 0; i--) {
                    StackFrame st_frame = st.GetFrame(i);
                    ex_params += "[file: " + st_frame.GetFileName() + ", line: " + st_frame.GetFileLineNumber() + ", row: " + st_frame.GetFileColumnNumber() + ")" + "]\n";
                }
                string error = ex_params;
            }


            applicableToSpan = _applicableToSpan;
        }

        private bool m_isDisposed;

        public void Dispose() {
            if (!m_isDisposed) {
                GC.SuppressFinalize(this);
                m_isDisposed = true;
            }
        }
    }
}
