using Microsoft.Maui.Layouts;
using StackLayoutManager = Microsoft.Maui.Controls.StackLayoutManager;
using System;
using Microsoft.Maui.Graphics;

namespace YogaClassManager.Components;

[ContentProperty(nameof(Children))]
public class OptionsLayout : VerticalStackLayout
{
	public static readonly BindableProperty VisibleOnProperty = BindableProperty.CreateAttached("VisibleOn",
		typeof(object), typeof(OptionsLayout), null, propertyChanged: Invalidate);
	
	public static readonly BindableProperty OptionProperty = BindableProperty.Create(nameof(Option), typeof(object),
		typeof(OptionsLayout), 0d, propertyChanged: Invalidate);
	
	public static void SetVisibleOn(BindableObject bindable, object value)
	{
		bindable.SetValue(VisibleOnProperty, value);
	}
	
	public static object GetVisibleOn(BindableObject bindable)
	{
		return bindable.GetValue(VisibleOnProperty);
	}

	static void Invalidate(BindableObject bindable, object oldValue, object newValue)
	{
		if (bindable is OptionsLayout optionsLayout)
		{
			optionsLayout.InvalidateMeasure();
		}
		else if (bindable is Element { Parent: OptionsLayout optionsLayoutParent })
		{
			optionsLayoutParent.InvalidateMeasure();
		}
	}
	
	public object Option
	{
		get => GetValue(OptionProperty);
		set => SetValue(OptionProperty, value);
	}
	
	
	public object? GetRow(IView view)
	{
		return view switch
		{
			BindableObject bo => bo.GetValue(VisibleOnProperty),
			_ => null,
		};
	}
	
    protected override ILayoutManager CreateLayoutManager() => new OptionsLayoutManager(this);
}

public class OptionsLayoutManager : VerticalStackLayoutManager
{
	public OptionsLayoutManager(OptionsLayout stackLayout) : base(stackLayout)
	{
	}
	
    public override Size Measure(double widthConstraint, double heightConstraint)
    {
	    var optionsLayout = (OptionsLayout)Stack;
	    
    	var padding = Stack.Padding;

    	double measuredHeight = 0;
    	double measuredWidth = 0;
    	double childWidthConstraint = widthConstraint - padding.HorizontalThickness;
    	int spacingCount = 0;

    	for (int n = 0; n < Stack.Count; n++)
    	{
    		var child = Stack[n];
		    
    		if (child.Visibility == Visibility.Collapsed || !optionsLayout.Option.Equals(optionsLayout.GetRow(child)))
    		{
    			continue;
    		}

    		spacingCount += 1;
    		var measure = child.Measure(childWidthConstraint, double.PositiveInfinity);
    		measuredHeight += measure.Height;
    		measuredWidth = Math.Max(measuredWidth, measure.Width);
    	}

    	measuredHeight += MeasureSpacing(Stack.Spacing, spacingCount);
    	measuredHeight += padding.VerticalThickness;
    	measuredWidth += padding.HorizontalThickness;

    	var finalHeight = ResolveConstraints(heightConstraint, Stack.Height, measuredHeight, Stack.MinimumHeight, Stack.MaximumHeight);
    	var finalWidth = ResolveConstraints(widthConstraint, Stack.Width, measuredWidth, Stack.MinimumWidth, Stack.MaximumWidth);

    	return new Size(finalWidth, finalHeight);
    }

    public override Size ArrangeChildren(Rect bounds)
    {
	    var optionsLayout = (OptionsLayout)Stack;
	    
    	var padding = Stack.Padding;

    	double stackHeight = padding.Top + bounds.Y;
    	double left = padding.Left + bounds.X;
    	double width = bounds.Width - padding.HorizontalThickness;

    	for (int n = 0; n < Stack.Count; n++)
    	{
    		var child = Stack[n];

		    if (child.Visibility == Visibility.Collapsed || !optionsLayout.Option.Equals(optionsLayout.GetRow(child)))
		    {
			    // arrange to take up no space so that it is invisible.
			    child.Arrange(new Rect(0, 0, 0, 0));
    			continue;
    		}

    		var destination = new Rect(left, stackHeight, width, child.DesiredSize.Height);
    		child.Arrange(destination);
    		stackHeight += destination.Height + Stack.Spacing;
    	}

    	var actual = new Size(width, stackHeight);

    	return actual.AdjustForFill(bounds, Stack);
    }
}