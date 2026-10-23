namespace DuoFlow.Vision;

/// <summary>
/// Picks which camera to use when there is more than one.
///
/// <para>
/// The rule is a PREFERENCE, never an assumption (HARDWARE §1.1 #1): a laptop's
/// lid camera is usually "Front", an external webcam is usually "External",
/// and we prefer the former because it looks at the user and therefore sees
/// the screen edge from a similar side. But the caller can always force a
/// device by id, and the ranking never fails - the worst case is "the first one
/// that exists", which is what a single-camera machine gives anyway.
/// </para>
/// <para>
/// Determinism matters more than it looks: Windows enumerates devices in an
/// order that can change across reboots and driver updates. Ranking by
/// (enclosure, name, id) means the same machine picks the same camera twice in
/// a row, so "the angle jumps when I reboot" cannot be blamed on selection.
/// </para>
/// </summary>
public static class CameraSelection
{
    /// <summary>
    /// Choose a device, or null when the list is empty.
    /// <paramref name="preferredId"/> wins outright if it is present and enabled.
    /// </summary>
    public static CameraDeviceInfo? Choose(
        IReadOnlyList<CameraDeviceInfo>? devices,
        string? preferredId = null)
    {
        if (devices is null || devices.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(preferredId))
        {
            foreach (CameraDeviceInfo device in devices)
            {
                if (string.Equals(device.Id, preferredId, StringComparison.Ordinal) && device.IsEnabled)
                {
                    return device;
                }
            }
        }

        CameraDeviceInfo? best = null;
        foreach (CameraDeviceInfo device in devices)
        {
            if (!device.IsEnabled)
            {
                continue;
            }
            if (best is null || Compare(device, best) < 0)
            {
                best = device;
            }
        }

        // Every device disabled: still report the first one so the caller can
        // surface "your camera is disabled" instead of the vaguer "no camera".
        return best ?? devices[0];
    }

    /// <summary>Negative when <paramref name="a"/> is preferred over <paramref name="b"/>.</summary>
    public static int Compare(CameraDeviceInfo a, CameraDeviceInfo b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        int byEnclosure = Rank(a.Enclosure).CompareTo(Rank(b.Enclosure));
        if (byEnclosure != 0)
        {
            return byEnclosure;
        }

        int byEnabled = (a.IsEnabled ? 0 : 1).CompareTo(b.IsEnabled ? 0 : 1);
        if (byEnabled != 0)
        {
            return byEnabled;
        }

        int byName = string.CompareOrdinal(a.Name, b.Name);
        if (byName != 0)
        {
            return byName;
        }

        return string.CompareOrdinal(a.Id, b.Id);
    }

    private static int Rank(CameraEnclosure enclosure) => enclosure switch
    {
        CameraEnclosure.Front => 0,
        CameraEnclosure.Unknown => 1,
        CameraEnclosure.Back => 2,
        CameraEnclosure.External => 3,
        _ => 1,
    };
}
