using System;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace MDRDesk
{
    /// <summary>
    /// Renders a TextBlock in an element's adorner layer.
    /// </summary>
    public class TextBlockAdorner : Adorner
    {
        #region Data

        private ArrayList _logicalChildren;
        private TextBlock _textBlock;

        #endregion // Data

        #region Constructor

        public TextBlockAdorner(UIElement adornedElement, TextBlock textBlock)
            : base(adornedElement)
        {
            _textBlock = textBlock;

            // Register the TextBlock with the element tree so that
            // it will be rendered, and can inherit DP values.
            base.AddLogicalChild(_textBlock);
            base.AddVisualChild(_textBlock);
        }

        #endregion // Constructor

        #region Measure/Arrange

        /// <summary>
        /// Allows the TextBlock to determine how big it wants to be.
        /// </summary>
        /// <param name="constraint">A limiting size for the TextBlock.</param>
        protected override Size MeasureOverride(Size constraint)
        {
            _textBlock.Measure(constraint);
            return _textBlock.DesiredSize;
        }

        /// <summary>
        /// Positions and sizes the TextBlock.
        /// </summary>
        /// <param name="finalSize">The actual size of the TextBlock.</param>		
        protected override Size ArrangeOverride(Size finalSize)
        {
            Point location = new Point(8, 20);
            Rect rect = new Rect(location, finalSize);
            _textBlock.Arrange(rect);
            return finalSize;
        }

        #endregion // Measure/Arrange

        #region Visual Children

        /// <summary>
        /// Required for the TextBlock to be rendered.
        /// </summary>
        protected override int VisualChildrenCount
        {
            get { return 1; }
        }

        /// <summary>
        /// Required for the TextBlock to be rendered.
        /// </summary>
        protected override Visual GetVisualChild(int index)
        {
            if (index != 0)
                throw new ArgumentOutOfRangeException("index");

            return _textBlock;
        }

        #endregion // Visual Children

        #region Logical Children

        /// <summary>
        /// Required for the TextBlock to inherit property values
        /// from the logical tree, such as FontSize.
        /// </summary>
        protected override IEnumerator LogicalChildren
        {
            get
            {
                if (_logicalChildren == null)
                {
                    _logicalChildren = new ArrayList();
                    _logicalChildren.Add(_textBlock);
                }

                return _logicalChildren.GetEnumerator();
            }
        }

        #endregion // Logical Children
    }
}
