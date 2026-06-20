using System;
using System.Runtime.InteropServices;

namespace HDRSnap2
{
    internal static class HdrDisplay
    {
        // scRGB linear value at which SDR reference white sits in the HDR framebuffer.
        // (SDRWhiteLevel is reported in 1/1000 nits relative to the 80-nit scRGB unit,
        //  so scale == SDRWhiteLevel / 1000.) Returns 1.0 if it can't be determined.
        public static float GetSdrWhiteScale()
        {
            try
            {
                if (GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out uint pathCount, out uint modeCount) != 0)
                    return 1.0f;

                var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
                var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
                if (QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero) != 0)
                    return 1.0f;

                for (int i = 0; i < pathCount; i++)
                {
                    var req = new DISPLAYCONFIG_SDR_WHITE_LEVEL
                    {
                        header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                        {
                            type = DISPLAYCONFIG_DEVICE_INFO_GET_SDR_WHITE_LEVEL,
                            size = Marshal.SizeOf<DISPLAYCONFIG_SDR_WHITE_LEVEL>(),
                            adapterId = paths[i].targetInfo.adapterId,
                            id = paths[i].targetInfo.id
                        }
                    };
                    if (DisplayConfigGetDeviceInfo(ref req) == 0 && req.SDRWhiteLevel > 0)
                        return req.SDRWhiteLevel / 1000f;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("SDR white query failed: " + ex.Message);
            }
            return 1.0f;
        }

        const uint QDC_ONLY_ACTIVE_PATHS = 0x00000002;
        const int DISPLAYCONFIG_DEVICE_INFO_GET_SDR_WHITE_LEVEL = 11;

        [DllImport("user32.dll")] static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);
        [DllImport("user32.dll")] static extern int QueryDisplayConfig(uint flags, ref uint numPathArrayElements, [Out] DISPLAYCONFIG_PATH_INFO[] pathArray, ref uint numModeInfoArrayElements, [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray, IntPtr currentTopologyId);
        [DllImport("user32.dll")] static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_SDR_WHITE_LEVEL requestPacket);

        [StructLayout(LayoutKind.Sequential)] struct LUID { public uint LowPart; public int HighPart; }
        [StructLayout(LayoutKind.Sequential)] struct DISPLAYCONFIG_RATIONAL { public uint Numerator; public uint Denominator; }
        [StructLayout(LayoutKind.Sequential)] struct DISPLAYCONFIG_PATH_SOURCE_INFO { public LUID adapterId; public uint id; public uint modeInfoIdx; public uint statusFlags; }
        [StructLayout(LayoutKind.Sequential)]
        struct DISPLAYCONFIG_PATH_TARGET_INFO
        {
            public LUID adapterId; public uint id; public uint modeInfoIdx;
            public uint outputTechnology; public uint rotation; public uint scaling;
            public DISPLAYCONFIG_RATIONAL refreshRate; public uint scanLineOrdering;
            public int targetAvailable; public uint statusFlags;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct DISPLAYCONFIG_PATH_INFO
        {
            public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
            public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
            public uint flags;
        }
        [StructLayout(LayoutKind.Sequential, Size = 64)]
        struct DISPLAYCONFIG_MODE_INFO { public uint infoType; public uint id; public LUID adapterId; }
        [StructLayout(LayoutKind.Sequential)]
        struct DISPLAYCONFIG_DEVICE_INFO_HEADER { public int type; public int size; public LUID adapterId; public uint id; }
        [StructLayout(LayoutKind.Sequential)]
        struct DISPLAYCONFIG_SDR_WHITE_LEVEL { public DISPLAYCONFIG_DEVICE_INFO_HEADER header; public uint SDRWhiteLevel; }
    }
}
