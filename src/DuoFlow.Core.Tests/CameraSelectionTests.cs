using DuoFlow.Vision;
using Xunit;

namespace DuoFlow.Core.Tests;

/// <summary>
/// M3.1: which camera wins when there is more than one.
/// The interesting assertions are the determinism ones - Windows enumerates
/// devices in an order that can change across reboots, so "the same machine
/// picks the same camera" has to be a property of the ranking, not of the list.
/// </summary>
public class CameraSelectionTests
{
    private static CameraDeviceInfo Dev(string name, CameraEnclosure enclosure, bool enabled = true, string? id = null)
        => new()
        {
            Id = id ?? $"id::{name}",
            Name = name,
            Enclosure = enclosure,
            IsEnabled = enabled,
        };

    [Fact]
    public void NoDevices_ReturnsNull()
    {
        Assert.Null(CameraSelection.Choose(Array.Empty<CameraDeviceInfo>()));
        Assert.Null(CameraSelection.Choose(null));
    }

    [Fact]
    public void PrefersFrontCamera()
    {
        CameraDeviceInfo[] devices =
        {
            Dev("External Cam", CameraEnclosure.External),
            Dev("Lid Cam", CameraEnclosure.Front),
            Dev("Back Cam", CameraEnclosure.Back),
        };

        Assert.Equal("Lid Cam", CameraSelection.Choose(devices)!.Name);
    }

    [Fact]
    public void ExplicitId_BeatsTheRanking()
    {
        CameraDeviceInfo[] devices =
        {
            Dev("Lid Cam", CameraEnclosure.Front),
            Dev("External Cam", CameraEnclosure.External, id: "usb-cam"),
        };

        Assert.Equal("External Cam", CameraSelection.Choose(devices, "usb-cam")!.Name);
    }

    [Fact]
    public void UnknownPreference_FallsBackToTheRanking()
    {
        CameraDeviceInfo[] devices =
        {
            Dev("Lid Cam", CameraEnclosure.Front),
            Dev("External Cam", CameraEnclosure.External),
        };

        Assert.Equal("Lid Cam", CameraSelection.Choose(devices, "does-not-exist")!.Name);
    }

    [Fact]
    public void DisabledDevices_AreSkipped_WhenAnEnabledOneExists()
    {
        CameraDeviceInfo[] devices =
        {
            Dev("Lid Cam", CameraEnclosure.Front, enabled: false),
            Dev("External Cam", CameraEnclosure.External),
        };

        Assert.Equal("External Cam", CameraSelection.Choose(devices)!.Name);
    }

    [Fact]
    public void AllDisabled_StillReturnsOne_SoTheCallerCanSayWhy()
    {
        CameraDeviceInfo[] devices =
        {
            Dev("Lid Cam", CameraEnclosure.Front, enabled: false),
        };

        CameraDeviceInfo? chosen = CameraSelection.Choose(devices);
        Assert.NotNull(chosen);
        Assert.False(chosen!.IsEnabled);
    }

    [Fact]
    public void Ranking_DoesNotDependOnEnumerationOrder()
    {
        CameraDeviceInfo[] devices =
        {
            Dev("Zeta", CameraEnclosure.Unknown),
            Dev("Alpha", CameraEnclosure.Front),
            Dev("Beta", CameraEnclosure.External),
            Dev("Gamma", CameraEnclosure.Front),
        };

        // All permutations of the same set must pick the same device.
        var permutations = new List<string[]>
        {
            new[] { "Zeta", "Alpha", "Beta", "Gamma" },
            new[] { "Gamma", "Beta", "Alpha", "Zeta" },
            new[] { "Beta", "Zeta", "Gamma", "Alpha" },
        };

        var picks = new HashSet<string>();
        foreach (string[] order in permutations)
        {
            var reordered = order
                .Select(name => devices.First(d => d.Name == name))
                .ToArray();
            picks.Add(CameraSelection.Choose(reordered)!.Name);
        }

        Assert.Single(picks);
        // Both "Alpha" and "Gamma" are front cameras; name ordering decides.
        Assert.Equal("Alpha", picks.First());
    }

    [Fact]
    public void Compare_OrdersByEnclosureThenNameThenId()
    {
        CameraDeviceInfo front = Dev("Z", CameraEnclosure.Front);
        CameraDeviceInfo external = Dev("A", CameraEnclosure.External);
        Assert.True(CameraSelection.Compare(front, external) < 0);

        CameraDeviceInfo alpha = Dev("Alpha", CameraEnclosure.Front);
        CameraDeviceInfo beta = Dev("Beta", CameraEnclosure.Front);
        Assert.True(CameraSelection.Compare(alpha, beta) < 0);

        CameraDeviceInfo sameNameA = Dev("Same", CameraEnclosure.Front, id: "a");
        CameraDeviceInfo sameNameB = Dev("Same", CameraEnclosure.Front, id: "b");
        Assert.True(CameraSelection.Compare(sameNameA, sameNameB) < 0);
    }
}
