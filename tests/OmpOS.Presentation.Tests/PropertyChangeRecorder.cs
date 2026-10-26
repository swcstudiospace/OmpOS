using System.ComponentModel;
using Shouldly;

namespace OmpOS.Presentation.Tests;

/// <summary>Collects the names of the properties a model announces through <see cref="INotifyPropertyChanged"/>, in order.</summary>
internal static class PropertyChangeRecorder
{
    /// <summary>Subscribes to <paramref name="model"/> and returns the list that receives each announced property name.</summary>
    /// <param name="model">The model to observe.</param>
    /// <returns>The announced property names, in the order they were raised.</returns>
    public static List<string> Record(INotifyPropertyChanged model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var names = new List<string>();
        model.PropertyChanged += (_, args) => names.Add(args.PropertyName ?? string.Empty);
        return names;
    }

    /// <summary>Asserts that <paramref name="propertyName"/> was announced at least once.</summary>
    /// <param name="announcedNames">The names collected by <see cref="Record"/>.</param>
    /// <param name="propertyName">The property that must have been announced.</param>
    public static void ShouldAnnounce(this IEnumerable<string> announcedNames, string propertyName)
    {
        announcedNames.Any(name => string.Equals(name, propertyName, StringComparison.Ordinal))
            .ShouldBeTrue($"{propertyName} should have been announced");
    }
}
