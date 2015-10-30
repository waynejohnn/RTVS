﻿using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;

namespace Microsoft.Languages.Editor.Selection {
    /// <summary>
    /// Selection tracker helps preserve caret position and relative screen scroll position 
    /// when text buffer change is applied. This is default implementation, languages can 
    /// choose to implement their own.
    /// </summary>
    public class SelectionTracker : ISelectionTracker {
        private double _offsetFromTop;
        private bool _automaticTracking;

        protected SnapshotPoint PositionBeforeChanges { get; set; }
        protected SnapshotPoint PositionAfterChanges { get; set; }
        protected int VirtualSpaces { get; set; }

        public SelectionTracker(ITextView textView) {
            TextView = textView;
        }

        #region ISelectionTracker

        /// <summary>
        /// Editor text view
        /// </summary>
        public ITextView TextView { get; private set; }

        /// <summary>
        /// Saves current caret position and optionally starts tracking 
        /// of the caret position across text buffer changes using ITrackingSpan
        /// </summary>
        /// <param name="automaticTracking">True if selection tracker should track text buffer changes using tracking span. 
        /// False if End should simply use current caret position as final position rather than attempt to track it
        /// across changes.</param>
        public virtual void StartTracking(bool automaticTracking) {
            // After multiple changes calling EnsureVisible may randomly scroll the view since editor may no 
            // longer have an anchor position it can use and hence will simply make line caret is at last 
            // visible line. Thus we have to restore viewport position manually here.

            _automaticTracking = automaticTracking;

            // Beware: snapshot point may be relative to HTML buffer snapshot
            // and not relative to actual text view [projection] buffer snapshot.
            PositionBeforeChanges = TextView.Caret.Position.BufferPosition;
            PositionAfterChanges = PositionBeforeChanges;

            var viewLine = TextView.TextViewLines.GetTextViewLineContainingBufferPosition(PositionBeforeChanges);
            if (viewLine != null)
                _offsetFromTop = viewLine.Top - TextView.ViewportTop;
        }

        /// <summary>
        /// Stops tracking and saves current caret position
        /// as final position as final or 'after changes' position.
        /// </summary>
        public virtual void EndTracking() {
            if (_automaticTracking) {
                var snapshot = PositionBeforeChanges.Snapshot.TextBuffer.CurrentSnapshot;
                PositionAfterChanges = PositionBeforeChanges.TranslateTo(snapshot, PointTrackingMode.Positive);
            } else {
                PositionAfterChanges = TextView.Caret.Position.BufferPosition;
            }

            MoveToAfterChanges(VirtualSpaces);
        }

        /// <summary>
        /// Moves caret to 'before changes' position.
        /// </summary>
        public void MoveToBeforeChanges() {
            MoveCaretTo(PositionBeforeChanges, VirtualSpaces);
        }

        /// <summary>
        /// Moves caret to 'after changes' position
        /// </summary>
        public void MoveToAfterChanges(int virtualSpaces = 0) {
            MoveCaretTo(PositionAfterChanges, virtualSpaces);
        }
        #endregion

        protected virtual void MoveCaretTo(SnapshotPoint position, int virtualSpaces) {
            var viewPosition = TextView.BufferGraph.MapUpToBuffer(position, PointTrackingMode.Positive, PositionAffinity.Successor, TextView.TextBuffer);

            if (viewPosition.HasValue) {
                TextView.Caret.MoveTo(new VirtualSnapshotPoint(viewPosition.Value, virtualSpaces));

                if (TextView.Caret.ContainingTextViewLine.VisibilityState != VisibilityState.FullyVisible) {
                    TextView.Caret.EnsureVisible();

                    TextView.DisplayTextLineContainingBufferPosition(viewPosition.Value, _offsetFromTop, ViewRelativePosition.Top);
                }
            }
        }
    }
}
