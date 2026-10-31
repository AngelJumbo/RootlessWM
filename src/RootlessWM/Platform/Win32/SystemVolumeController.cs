using System.Runtime.InteropServices;
using RootlessWM.App;

namespace RootlessWM.Platform.Win32;

internal sealed class SystemVolumeController : ISystemVolume
{
    public SystemVolumeState Sample()
        => WithEndpoint(endpoint =>
        {
            endpoint.GetMasterVolumeLevelScalar(out var scalar);
            endpoint.GetMute(out var muted);
            return new SystemVolumeState(scalar, muted);
        });

    public void Adjust(float delta)
        => WithEndpoint(endpoint =>
        {
            endpoint.GetMasterVolumeLevelScalar(out var scalar);
            endpoint.SetMasterVolumeLevelScalar(AdjustScalar(scalar, delta), Guid.Empty);
            return true;
        });

    public void ToggleMute()
        => WithEndpoint(endpoint =>
        {
            endpoint.GetMute(out var muted);
            endpoint.SetMute(!muted, Guid.Empty);
            return true;
        });

    internal static float AdjustScalar(float scalar, float delta)
        => Math.Clamp(scalar + delta, 0F, 1F);

    // Resolve on each operation so device changes do not leave the bar controlling an old endpoint.
    private static T WithEndpoint<T>(Func<IAudioEndpointVolume, T> action)
    {
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
        IMMDevice? device = null;
        object? endpointObject = null;
        try
        {
            enumerator.GetDefaultAudioEndpoint(0, 1, out device); // eRender, eMultimedia
            var iid = typeof(IAudioEndpointVolume).GUID;
            device.Activate(ref iid, 23, nint.Zero, out endpointObject); // CLSCTX_ALL
            return action((IAudioEndpointVolume)endpointObject);
        }
        finally
        {
            if (endpointObject is not null)
            {
                _ = Marshal.ReleaseComObject(endpointObject);
            }

            if (device is not null)
            {
                _ = Marshal.ReleaseComObject(device);
            }

            _ = Marshal.ReleaseComObject(enumerator);
        }
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumerator;

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        void EnumAudioEndpoints(int dataFlow, uint stateMask, out nint devices);
        void GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        void Activate(ref Guid iid, uint context, nint activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        void RegisterControlChangeNotify(nint notify);
        void UnregisterControlChangeNotify(nint notify);
        void GetChannelCount(out uint count);
        void SetMasterVolumeLevel(float level, [In, MarshalAs(UnmanagedType.LPStruct)] Guid context);
        void SetMasterVolumeLevelScalar(float level, [In, MarshalAs(UnmanagedType.LPStruct)] Guid context);
        void GetMasterVolumeLevel(out float level);
        void GetMasterVolumeLevelScalar(out float level);
        void SetChannelVolumeLevel(uint channel, float level, [In, MarshalAs(UnmanagedType.LPStruct)] Guid context);
        void SetChannelVolumeLevelScalar(uint channel, float level, [In, MarshalAs(UnmanagedType.LPStruct)] Guid context);
        void GetChannelVolumeLevel(uint channel, out float level);
        void GetChannelVolumeLevelScalar(uint channel, out float level);
        void SetMute([MarshalAs(UnmanagedType.Bool)] bool muted, [In, MarshalAs(UnmanagedType.LPStruct)] Guid context);
        void GetMute([MarshalAs(UnmanagedType.Bool)] out bool muted);
    }
}
