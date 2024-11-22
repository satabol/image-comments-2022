using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

// На основе примера: https://msdn.microsoft.com/en-us/library/ee197646.aspx

namespace ImageCommentsExtension_2022 {
    internal class ImageUrlQuickInfoController : IIntellisenseController {

        static ImageUrlQuickInfoController() {
            DependencyAssemblyLoader.Main();
        }

        private ITextView m_textView;
        private IList<ITextBuffer> m_subjectBuffers;
        private ImageUrlQuickInfoControllerProvider m_provider;
        private IAsyncQuickInfoSession m_session;

        internal ImageUrlQuickInfoController( ITextView textView, IList<ITextBuffer> subjectBuffers, ImageUrlQuickInfoControllerProvider provider ) {
            m_textView = textView;
            m_subjectBuffers = subjectBuffers;
            m_provider = provider;

            m_textView.MouseHover += this.OnTextViewMouseHover;
        }

        private async void OnTextViewMouseHover( object sender, MouseHoverEventArgs e ) {
            //find the mouse position by mapping down to the subject buffer
            SnapshotPoint? point = m_textView.BufferGraph.MapDownToFirstMatch
                 (new SnapshotPoint(m_textView.TextSnapshot, e.Position),
                PointTrackingMode.Positive,
                snapshot => m_subjectBuffers.Contains(snapshot.TextBuffer),
                PositionAffinity.Predecessor);

            if (point != null) {
                ITrackingPoint triggerPoint = point.Value.Snapshot.CreateTrackingPoint(point.Value.Position,
                PointTrackingMode.Positive);

                if (!m_provider.QuickInfoBroker.IsQuickInfoActive(m_textView)) {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                        m_session = await m_provider.QuickInfoBroker.TriggerQuickInfoAsync(m_textView, triggerPoint);
                    } else {
                        // Иногда сбоит и считает, что работает не в Windows!!!
                        Console.WriteLine("Ошибочно считает, что работает не в Windows!!!");
                    }
                }
            }
        }

        public void Detach( ITextView textView ) {
            if (m_textView == textView) {
                m_textView.MouseHover -= this.OnTextViewMouseHover;
                m_textView = null;
            }
        }

        public void ConnectSubjectBuffer( ITextBuffer subjectBuffer ) {
        }

        public void DisconnectSubjectBuffer( ITextBuffer subjectBuffer ) {
        }
    }
}
