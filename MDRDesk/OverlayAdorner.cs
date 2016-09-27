using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace MDRDesk
{
	/// <summary> 
	/// Overlays a control with the specified content 
	/// </summary> 
	/// <typeparam name="TOverlay">The type of content to create the overlay from</typeparam> 
	public class OverlayAdorner<TOverlay> : Adorner, IDisposable where TOverlay : UIElement, new()
	{
		private UIElement _adorningElement; private AdornerLayer _layer;
		
		/// <summary> /// Overlay the specified element /// </summary>
		/// <param name="elementToAdorn">The element to overlay</param> 
		/// <returns></returns> 
		public static IDisposable Overlay(UIElement elementToAdorn) { return Overlay(elementToAdorn, new TOverlay()); } 

		/// <summary> 
		/// Overlays the element with the specified instance of TOverlay 
		/// </summary> 
		/// <param name="elementToAdorn">Element to overlay</param> 
		/// <param name="adorningElement">The content of the overlay</param> 
		/// <returns></returns> 
		public static IDisposable Overlay(UIElement elementToAdorn, TOverlay adorningElement)
		{
			var adorner = new OverlayAdorner<TOverlay>(elementToAdorn, adorningElement);
			adorner._layer = AdornerLayer.GetAdornerLayer(elementToAdorn);
			adorner._layer.Add(adorner);
			return adorner as IDisposable;
		}

		private OverlayAdorner(UIElement elementToAdorn, UIElement adorningElement)
			: base(elementToAdorn)
		{
			this._adorningElement = adorningElement;
			if (adorningElement != null)
			{
				AddVisualChild(adorningElement);
			}
			Focusable = true;
		}

		protected override int VisualChildrenCount
		{
			get { return _adorningElement == null ? 0 : 1; }
		}

		protected override Size ArrangeOverride(Size finalSize)
		{
			if (_adorningElement != null)
			{
				Point adorningPoint = new Point(0, 0);
				_adorningElement.Arrange(new Rect(adorningPoint, this.AdornedElement.DesiredSize));
			}
			return finalSize;
		}

		protected override Visual GetVisualChild(int index)
		{
			if (index == 0 && _adorningElement != null)
			{
				return _adorningElement;
			}
			return base.GetVisualChild(index);
		}
		public void Dispose()
		{
			_layer.Remove(this);
		}
	}

}
