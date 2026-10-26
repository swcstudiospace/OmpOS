using OmpOS.Presentation;
using OmpOS.Presentation.Settings;

namespace OmpOS.Native.Tests.Settings;

/// <summary>A scripted source of the General page.</summary>
internal sealed class FakeGeneralSource : FakeSettingsSource<GeneralSettingsValues>, IGeneralSettingsSource
{
    /// <summary>Creates a source whose saved motion is Follow system.</summary>
    public FakeGeneralSource()
        : base(new GeneralSettingsValues(MotionPreference.System))
    {
    }

    /// <inheritdoc />
    public Task<GeneralSettingsValues> LoadAsync(CancellationToken cancellationToken) => LoadCoreAsync(cancellationToken);

    /// <inheritdoc />
    public Task SaveAsync(GeneralSettingsValues values, CancellationToken cancellationToken) => SaveCoreAsync(values);
}
