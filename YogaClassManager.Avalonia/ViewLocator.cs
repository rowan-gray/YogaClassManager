using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using YogaClassManager.Avalonia.ViewModels;

namespace YogaClassManager.Avalonia;

/// <summary>
/// Given a view model, returns the corresponding view if possible.
/// </summary>
[RequiresUnreferencedCode(
    "Default implementation of ViewLocator involves reflection which may be trimmed away.",
    Url = "https://docs.avaloniaui.net/docs/concepts/view-locator")]
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        var type = ResolveViewType(param.GetType());

        if (type != null)
            return (Control)Activator.CreateInstance(type)!;

        return new TextBlock { Text = "View not found for: " + param.GetType().FullName };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }

    /// <summary>Resolves a ViewModel type to its View type by the FooViewModel -&gt; FooView naming convention.</summary>
    public static Type? ResolveViewType(Type viewModelType)
    {
        var name = viewModelType.FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        return Type.GetType(name);
    }
}
