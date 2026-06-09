// iphoneyum.exe
// Backs up all media from an iPhone to a Windows folder using the
// Windows Portable Devices (WPD) API directly over MTP/USB.
//
// Fully vibe coded, but it worked!
//
// Usage:
//   ./iphoneyum.exe --backup-root "D:\iphone_backup\" [--structure yearmonth|flat|type]
//   For a year and month based folder structure:
//   ./iphoneyum.exe --backup-root "D:\iphone_backup\" --structure yearmonth
//
// Compile:
//   C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:exe /out:iphoneyum.exe iphoneyum.cs

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

// ============================================================
// CoCreateInstance P/Invoke
// ============================================================

static class Com
{
    [DllImport("ole32.dll")]
    static extern int CoCreateInstance(
        [In] ref Guid rclsid, IntPtr pUnkOuter, uint dwClsContext,
        [In] ref Guid riid, out IntPtr ppv);

    public static T Create<T>(Guid clsid)
    {
        var attr = (GuidAttribute)Attribute.GetCustomAttribute(typeof(T), typeof(GuidAttribute));
        if (attr == null) throw new InvalidOperationException("No Guid on " + typeof(T).Name);
        Guid iid = new Guid(attr.Value);
        IntPtr ppv;
        int hr = CoCreateInstance(ref clsid, IntPtr.Zero, 5, ref iid, out ppv);
        if (hr != 0) Marshal.ThrowExceptionForHR(hr);
        T result = (T)Marshal.GetObjectForIUnknown(ppv);
        Marshal.Release(ppv);
        return result;
    }

    public static readonly Guid CLSID_PortableDeviceManager = new Guid("0AF10CEC-2ECD-4B92-9581-34F6AE0637F3");
    public static readonly Guid CLSID_PortableDevice        = new Guid("728A21C5-3D9E-48D7-9810-864848F0F404");
    public static readonly Guid CLSID_PortableDeviceFTM     = new Guid("F7C0039A-4762-488A-B4B3-760EF9A1BA9B");
    public static readonly Guid CLSID_PortableDeviceValues  = new Guid("0C15D503-D017-47CE-9016-7B3F978721CC");
}

// ============================================================
// WPD COM Interfaces
// ============================================================

[ComImport, Guid("A1567595-4C2F-4574-A6FA-ECEF917B9A40"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IPortableDeviceManager
{
    void GetDevices(IntPtr pPnPDeviceIDs, ref uint pcPnPDeviceIDs);
    void RefreshDeviceList();
    void GetDeviceFriendlyName([In, MarshalAs(UnmanagedType.LPWStr)] string pszPnPDeviceID,
        [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] char[] pDeviceFriendlyName,
        ref uint pcchDeviceFriendlyName);
    void GetDeviceDescription([In, MarshalAs(UnmanagedType.LPWStr)] string pszPnPDeviceID,
        [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] char[] pDeviceDescription,
        ref uint pcchDeviceDescription);
    void GetDeviceManufacturer([In, MarshalAs(UnmanagedType.LPWStr)] string pszPnPDeviceID,
        [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] char[] pDeviceManufacturer,
        ref uint pcchDeviceManufacturer);
}

[ComImport, Guid("625E2DF8-6392-4CF0-9AD1-3CFA5F17775C"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IPortableDevice
{
    void Open([In, MarshalAs(UnmanagedType.LPWStr)] string pszPnPDeviceID, [In] IPortableDeviceValues pClientInfo);
    void SendCommand([In] uint dwFlags, [In] IPortableDeviceValues pParameters, [Out] out IPortableDeviceValues ppResults);
    void Content([Out] out IPortableDeviceContent ppContent);
    void Capabilities([Out] out IPortableDeviceCapabilities ppCapabilities);
    void Cancel();
    void Close();
    void Advise([In] uint dwFlags, [In] object pCallback, [In] IPortableDeviceValues pParameters, [Out, MarshalAs(UnmanagedType.LPWStr)] out string ppszCookie);
    void Unadvise([In, MarshalAs(UnmanagedType.LPWStr)] string pszCookie);
    void GetPnPDeviceID([Out, MarshalAs(UnmanagedType.LPWStr)] out string ppszPnPDeviceID);
}

[ComImport, Guid("6A96ED84-7C73-4480-9938-BF5AF477D426"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IPortableDeviceContent
{
    void EnumObjects([In] uint dwFlags, [In, MarshalAs(UnmanagedType.LPWStr)] string pszParentObjectID,
        [In] IPortableDeviceValues pFilter, [Out] out IEnumPortableDeviceObjectIDs ppEnum);
    void Properties([Out] out IPortableDeviceProperties ppProperties);
    void Transfer([Out] out IPortableDeviceResources ppResources);
    void CreateObjectWithPropertiesOnly([In] IPortableDeviceValues pValues,
        [In, Out, MarshalAs(UnmanagedType.LPWStr)] ref string ppszObjectID);
    void CreateObjectWithPropertiesAndData([In] IPortableDeviceValues pValues,
        [Out] out IStream ppData, [In, Out] ref uint pdwOptimalWriteBufferSize,
        [In, Out, MarshalAs(UnmanagedType.LPWStr)] ref string ppszCookie);
    void Delete([In] uint dwOptions,
        [In] IPortableDevicePropVariantCollection pObjectIDs,
        [In, Out] ref IPortableDevicePropVariantCollection ppResults);
    void GetObjectIDsFromPersistentUniqueIDs([In] IPortableDevicePropVariantCollection pPersistentUniqueIDs,
        [Out] out IPortableDevicePropVariantCollection ppObjectIDs);
    void Cancel();
    void Move([In] IPortableDevicePropVariantCollection pObjectIDs,
        [In, MarshalAs(UnmanagedType.LPWStr)] string pszDestinationFolderObjectID,
        [In, Out] ref IPortableDevicePropVariantCollection ppResults);
}

[ComImport, Guid("10ECE955-CF41-4728-BFA0-41EEDF1BBF19"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IEnumPortableDeviceObjectIDs
{
    void Next([In] uint cObjects,
        [Out, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr, SizeParamIndex = 0)] string[] rgObjectIDs,
        ref uint pcFetched);
    void Skip([In] uint cObjects);
    void Reset();
    void Clone([Out] out IEnumPortableDeviceObjectIDs ppenum);
    void Cancel();
}

[ComImport, Guid("7F6D695C-03DF-4439-A809-59266BEEE3A6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IPortableDeviceProperties
{
    void GetSupportedProperties([In, MarshalAs(UnmanagedType.LPWStr)] string pszObjectID,
        [Out] out IPortableDeviceKeyCollection ppKeys);
    void GetPropertyAttributes([In, MarshalAs(UnmanagedType.LPWStr)] string pszObjectID,
        [In] ref PropertyKey Key, [Out] out IPortableDeviceValues ppAttributes);
    void GetValues([In, MarshalAs(UnmanagedType.LPWStr)] string pszObjectID,
        [In] IPortableDeviceKeyCollection pKeys, [Out] out IPortableDeviceValues ppValues);
    void SetValues([In, MarshalAs(UnmanagedType.LPWStr)] string pszObjectID,
        [In] IPortableDeviceValues pValues, [Out] out IPortableDeviceValues ppResults);
    void Delete([In, MarshalAs(UnmanagedType.LPWStr)] string pszObjectID,
        [In] IPortableDeviceKeyCollection pKeys);
    void Cancel();
}

[ComImport, Guid("FD8878AC-D841-4D17-891C-E6829CDB6934"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IPortableDeviceResources
{
    void GetSupportedResources([In, MarshalAs(UnmanagedType.LPWStr)] string pszObjectID,
        [Out] out IPortableDeviceKeyCollection ppKeys);
    void GetResourceAttributes([In, MarshalAs(UnmanagedType.LPWStr)] string pszObjectID,
        [In] ref PropertyKey Key, [Out] out IPortableDeviceValues ppResourceAttributes);
    void GetStream([In, MarshalAs(UnmanagedType.LPWStr)] string pszObjectID,
        [In] ref PropertyKey Key, [In] uint dwMode, [In, Out] ref uint pdwOptimalBufferSize,
        [Out, MarshalAs(UnmanagedType.Interface)] out IStream ppStream);
    void Delete([In, MarshalAs(UnmanagedType.LPWStr)] string pszObjectID, [In] ref PropertyKey Key);
    void Cancel();
    void CreateResource([In] IPortableDeviceValues pResourceAttributes,
        [Out, MarshalAs(UnmanagedType.Interface)] out IStream ppData,
        [In, Out] ref uint pdwOptimalWriteBufferSize,
        [In, Out, MarshalAs(UnmanagedType.LPWStr)] ref string ppszCookie);
}

[ComImport, Guid("6848F159-0D44-4B7E-9424-7B8A7B07DA74"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IPortableDeviceCapabilities
{
    void GetSupportedCommands([Out] out IPortableDeviceKeyCollection ppCommands);
    void GetCommandOptions([In] ref PropertyKey Command, [Out] out IPortableDeviceValues ppOptions);
    void GetFunctionalCategories([Out] out IPortableDevicePropVariantCollection ppCategories);
    void GetFunctionalObjects([In] ref Guid Category, [Out] out IPortableDevicePropVariantCollection ppObjectIDs);
    void GetSupportedContentTypes([In] ref Guid Category, [Out] out IPortableDevicePropVariantCollection ppContentTypes);
    void GetSupportedFormats([In] ref Guid ContentType, [Out] out IPortableDevicePropVariantCollection ppFormats);
    void GetSupportedFormatProperties([In] ref Guid Format, [Out] out IPortableDeviceKeyCollection ppKeys);
    void GetFixedPropertyAttributes([In] ref Guid Format, [In] ref PropertyKey Key, [Out] out IPortableDeviceValues ppAttributes);
    void Cancel();
    void GetSupportedEvents([Out] out IPortableDevicePropVariantCollection ppEvents);
    void GetEventOptions([In] ref Guid Event, [Out] out IPortableDeviceValues ppOptions);
}

[ComImport, Guid("6848F6F2-3155-4F86-B6F5-263EEEAB3143"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IPortableDeviceValues
{
    void GetCount([In] ref uint pcelt);
    void GetAt([In] uint index, [In, Out] ref PropertyKey pKey, [In, Out] ref PROPVARIANT pValue);
    void SetValue([In] ref PropertyKey key, [In] ref PROPVARIANT pValue);
    void GetValue([In] ref PropertyKey key, [Out] out PROPVARIANT pValue);
    void SetStringValue([In] ref PropertyKey key, [In, MarshalAs(UnmanagedType.LPWStr)] string Value);
    void GetStringValue([In] ref PropertyKey key, [Out, MarshalAs(UnmanagedType.LPWStr)] out string pValue);
    void SetUnsignedIntegerValue([In] ref PropertyKey key, [In] uint Value);
    void GetUnsignedIntegerValue([In] ref PropertyKey key, [Out] out uint pValue);
    void SetSignedIntegerValue([In] ref PropertyKey key, [In] int Value);
    void GetSignedIntegerValue([In] ref PropertyKey key, [Out] out int pValue);
    void SetUnsignedLargeIntegerValue([In] ref PropertyKey key, [In] ulong Value);
    void GetUnsignedLargeIntegerValue([In] ref PropertyKey key, [Out] out ulong pValue);
    void SetSignedLargeIntegerValue([In] ref PropertyKey key, [In] long Value);
    void GetSignedLargeIntegerValue([In] ref PropertyKey key, [Out] out long pValue);
    void SetFloatValue([In] ref PropertyKey key, [In] float Value);
    void GetFloatValue([In] ref PropertyKey key, [Out] out float pValue);
    void SetErrorValue([In] ref PropertyKey key, [In] int Value);
    void GetErrorValue([In] ref PropertyKey key, [Out] out int pValue);
    void SetKeyValue([In] ref PropertyKey key, [In] ref PropertyKey Value);
    void GetKeyValue([In] ref PropertyKey key, [Out] out PropertyKey pValue);
    void SetBoolValue([In] ref PropertyKey key, [In] int Value);
    void GetBoolValue([In] ref PropertyKey key, [Out] out int pValue);
    void SetIUnknownValue([In] ref PropertyKey key, [In] object pValue);
    void GetIUnknownValue([In] ref PropertyKey key, [Out, MarshalAs(UnmanagedType.IUnknown)] out object ppValue);
    void SetGuidValue([In] ref PropertyKey key, [In] ref Guid Value);
    void GetGuidValue([In] ref PropertyKey key, [Out] out Guid pValue);
    void SetBufferValue([In] ref PropertyKey key, [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] pValue, uint cbValue);
    void GetBufferValue([In] ref PropertyKey key, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] out byte[] ppValue, [Out] out uint pcbValue);
    void SetIPortableDeviceValuesValue([In] ref PropertyKey key, [In] IPortableDeviceValues pValue);
    void GetIPortableDeviceValuesValue([In] ref PropertyKey key, [Out] out IPortableDeviceValues ppValue);
    void SetIPortableDeviceKeyCollectionValue([In] ref PropertyKey key, [In] IPortableDeviceKeyCollection pValue);
    void GetIPortableDeviceKeyCollectionValue([In] ref PropertyKey key, [Out] out IPortableDeviceKeyCollection ppValue);
    void SetIPortableDevicePropVariantCollectionValue([In] ref PropertyKey key, [In] IPortableDevicePropVariantCollection pValue);
    void GetIPortableDevicePropVariantCollectionValue([In] ref PropertyKey key, [Out] out IPortableDevicePropVariantCollection ppValue);
    void SetIPortableDeviceValuesCollectionValue([In] ref PropertyKey key, [In] IPortableDeviceValuesCollection pValue);
    void GetIPortableDeviceValuesCollectionValue([In] ref PropertyKey key, [Out] out IPortableDeviceValuesCollection ppValue);
    void RemoveValue([In] ref PropertyKey key);
    void CopyValuesFromPropertyStore([In] object pStore);
    void CopyValuesToPropertyStore([In] object pStore);
    void Clear();
}

[ComImport, Guid("DADA2357-E0AD-492E-98DB-DD61C53BA353"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IPortableDeviceKeyCollection
{
    void GetCount([Out] out uint pcElems);
    void GetAt([In] uint dwIndex, [Out] out PropertyKey pKey);
    void Add([In] ref PropertyKey Key);
    void Clear();
    void RemoveAt([In] uint dwIndex);
}

[ComImport, Guid("89B2E422-4F1B-4316-BCEF-A44AFEA83EB3"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IPortableDevicePropVariantCollection
{
    void GetCount([Out] out uint pcElems);
    void GetAt([In] uint dwIndex, [Out] out PROPVARIANT pValue);
    void Add([In] ref PROPVARIANT pValue);
    void GetType([Out] out ushort pvt);
    void ChangeType([In] ushort vt);
    void Clear();
    void RemoveAt([In] uint dwIndex);
}

[ComImport, Guid("6E3F2D79-4E07-48C4-8208-D8C2E5AF4A99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IPortableDeviceValuesCollection
{
    void GetCount([Out] out uint pcElems);
    void GetAt([In] uint dwIndex, [Out] out IPortableDeviceValues ppValues);
    void Add([In] IPortableDeviceValues pValues);
    void Clear();
    void RemoveAt([In] uint dwIndex);
}

[ComImport, Guid("0000000C-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IStream
{
    void Read([Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] pv, uint cb, out uint pcbRead);
    void Write([In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] pv, uint cb, out uint pcbWritten);
    void Seek([In] long dlibMove, [In] uint dwOrigin, [Out] out long plibNewPosition);
    void SetSize([In] long libNewSize);
    void CopyTo([In] IStream pstm, [In] long cb, [Out] out long pcbRead, [Out] out long pcbWritten);
    void Commit([In] uint grfCommitFlags);
    void Revert();
    void LockRegion([In] long libOffset, [In] long cb, [In] uint dwLockType);
    void UnlockRegion([In] long libOffset, [In] long cb, [In] uint dwLockType);
    void Stat([Out] out STATSTG pstatstg, [In] uint grfStatFlag);
    void Clone([Out] out IStream ppstm);
}

// ============================================================
// Structs
// ============================================================

[StructLayout(LayoutKind.Sequential)]
struct PropertyKey
{
    public Guid fmtid;
    public uint pid;
}

[StructLayout(LayoutKind.Explicit)]
struct PROPVARIANT
{
    [FieldOffset(0)] public ushort vt;
    [FieldOffset(8)] public IntPtr pszVal;
    [FieldOffset(8)] public long hVal;
    [FieldOffset(8)] public ulong uhVal;
}

[StructLayout(LayoutKind.Sequential)]
struct STATSTG
{
    [MarshalAs(UnmanagedType.LPWStr)] public string pwcsName;
    public uint type;
    public ulong cbSize;
    public System.Runtime.InteropServices.ComTypes.FILETIME mtime;
    public System.Runtime.InteropServices.ComTypes.FILETIME ctime;
    public System.Runtime.InteropServices.ComTypes.FILETIME atime;
    public uint grfMode;
    public uint grfLocksSupported;
    public Guid clsid;
    public uint grfStateBits;
    public uint reserved;
}

// ============================================================
// WPD Property Keys
// ============================================================

static class WPD
{
    static Guid OBJ = new Guid("EF6B490D-5CD8-437A-AFFC-DA8B60EE4A3C");

    public static PropertyKey OBJECT_NAME              = new PropertyKey { fmtid = new Guid("EF6B490D-5CD8-437A-AFFC-DA8B60EE4A3C"), pid = 4  };
    public static PropertyKey OBJECT_ORIGINAL_FILE_NAME= new PropertyKey { fmtid = new Guid("EF6B490D-5CD8-437A-AFFC-DA8B60EE4A3C"), pid = 12 };
    public static PropertyKey OBJECT_CONTENT_TYPE      = new PropertyKey { fmtid = new Guid("EF6B490D-5CD8-437A-AFFC-DA8B60EE4A3C"), pid = 7  };
    public static PropertyKey OBJECT_DATE_MODIFIED     = new PropertyKey { fmtid = new Guid("EF6B490D-5CD8-437A-AFFC-DA8B60EE4A3C"), pid = 19 };
    public static PropertyKey OBJECT_SIZE              = new PropertyKey { fmtid = new Guid("EF6B490D-5CD8-437A-AFFC-DA8B60EE4A3C"), pid = 11 };
    public static PropertyKey RESOURCE_DEFAULT         = new PropertyKey { fmtid = new Guid("E81E79BE-34F0-41BF-B53F-F1A06AE87842"), pid = 0  };

    public static Guid CONTENT_TYPE_FOLDER            = new Guid("27E2E392-A111-48E0-AB0C-E17705A05F85");
    public static Guid CONTENT_TYPE_FUNCTIONAL_OBJECT = new Guid("99ED0160-17FF-4C44-9D98-1D7A6F941921");
    public static Guid CONTENT_TYPE_STORAGE           = new Guid("23F05BBC-15DE-4C2A-A55B-A9AF5CE412EF");
}

// ============================================================
// Main Program
// ============================================================

class Program
{
    static int    copied   = 0;
    static int    skipped  = 0;
    static int    errors   = 0;
    static long   totalBytes = 0;
    static DateTime startTime;

    static void Main(string[] args)
    {
        Console.WriteLine();
        Console.WriteLine("===========================================");
        Console.WriteLine("  iphoneyum");
        Console.WriteLine("===========================================");
        Console.WriteLine();

        // --- Parse arguments ---
        string backupRoot = null;
        string structure  = "yearmonth";

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--backup-root" && i + 1 < args.Length)
                backupRoot = args[++i];
            else if (args[i] == "--structure" && i + 1 < args.Length)
                structure = args[++i];
        }

        if (backupRoot == null)
        {
            Console.Write("  Backup root directory: ");
            backupRoot = Console.ReadLine().Trim();
        }

        if (!new[] { "yearmonth", "flat", "type" }.Contains(structure))
        {
            Console.WriteLine("  Invalid --structure. Use: yearmonth, flat, or type");
            Environment.Exit(1);
        }

        Console.WriteLine("  Backup root : " + backupRoot);
        Console.WriteLine("  Structure   : " + structure);
        Console.WriteLine();

        IPortableDevice device = null;

        try
        {
            // --- Find iPhone ---
            Console.Write("  Searching for devices...");
            var manager = Com.Create<IPortableDeviceManager>(Com.CLSID_PortableDeviceManager);
            manager.RefreshDeviceList();

            uint deviceCount = 0;
            manager.GetDevices(IntPtr.Zero, ref deviceCount);

            if (deviceCount == 0)
            {
                Console.WriteLine();
                Console.WriteLine("  ERROR: No portable devices found.");
                Console.WriteLine("         Make sure your iPhone is connected, unlocked,");
                Console.WriteLine("         and you have tapped 'Trust This Computer'.");
                Environment.Exit(1);
            }

            IntPtr[] ptrs = new IntPtr[deviceCount];
            GCHandle handle = GCHandle.Alloc(ptrs, GCHandleType.Pinned);
            string[] deviceIds = new string[deviceCount];
            try
            {
                manager.GetDevices(handle.AddrOfPinnedObject(), ref deviceCount);
                for (uint i = 0; i < deviceCount; i++)
                    deviceIds[i] = Marshal.PtrToStringUni(ptrs[i]);
            }
            finally { handle.Free(); }

            string iPhoneId   = null;
            string iPhoneName = null;

            foreach (string id in deviceIds)
            {
                uint nameLen = 0;
                try { manager.GetDeviceFriendlyName(id, null, ref nameLen); } catch { }
                string name = id;
                if (nameLen > 0)
                {
                    char[] nameChars = new char[nameLen];
                    try
                    {
                        manager.GetDeviceFriendlyName(id, nameChars, ref nameLen);
                        name = new string(nameChars, 0, Math.Max(0, (int)nameLen - 1));
                    }
                    catch { }
                }
                if (name.IndexOf("iPhone", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Apple",  StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    iPhoneId   = id;
                    iPhoneName = name;
                }
            }

            if (iPhoneId == null)
            {
                Console.WriteLine();
                Console.WriteLine("  ERROR: No iPhone found among connected devices.");
                Environment.Exit(1);
            }

            Console.WriteLine(" found " + iPhoneName);
            Console.WriteLine();

            // --- Open device ---
            try   { device = Com.Create<IPortableDevice>(Com.CLSID_PortableDeviceFTM); }
            catch { device = Com.Create<IPortableDevice>(Com.CLSID_PortableDevice);    }

            var clientInfo = Com.Create<IPortableDeviceValues>(Com.CLSID_PortableDeviceValues);
            device.Open(iPhoneId, clientInfo);

            IPortableDeviceContent    content;    device.Content(out content);
            IPortableDeviceProperties properties; content.Properties(out properties);

            if (!Directory.Exists(backupRoot))
                Directory.CreateDirectory(backupRoot);

            startTime = DateTime.Now;
            Console.WriteLine("  Starting backup...");

            WalkAndCopy(content, properties, "DEVICE", backupRoot, structure);

            // --- Summary ---
            TimeSpan elapsed = DateTime.Now - startTime;
            Console.WriteLine();
            Console.WriteLine("===========================================");
            Console.WriteLine("  Backup Complete");
            Console.WriteLine("===========================================");
            Console.WriteLine("  Copied  : " + copied  + " files (" + FormatBytes(totalBytes) + ")");
            Console.WriteLine("  Skipped : " + skipped + " files (already existed)");
            Console.WriteLine("  Errors  : " + errors);
            Console.WriteLine("  Time    : " + elapsed.ToString(@"hh\:mm\:ss"));
            Console.WriteLine("  Saved to: " + backupRoot);
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine("  ERROR: " + ex.Message);
            Environment.Exit(1);
        }
        finally
        {
            if (device != null) try { device.Close(); } catch { }
        }
    }

    static void WalkAndCopy(IPortableDeviceContent content, IPortableDeviceProperties properties,
        string parentId, string backupRoot, string structure)
    {
        IEnumPortableDeviceObjectIDs enumerator;
        try { content.EnumObjects(0, parentId, null, out enumerator); }
        catch { return; }

        uint     fetched       = 0;
        string[] ids           = new string[16];
        bool     printedFolder = false;
        string   folderName    = GetStringProperty(properties, parentId, WPD.OBJECT_NAME) ?? parentId;

        while (true)
        {
            try { enumerator.Next(16, ids, ref fetched); }
            catch { break; }
            if (fetched == 0) break;

            for (uint i = 0; i < fetched; i++)
            {
                string objId = ids[i];
                if (objId == null) continue;

                string fileName = GetStringProperty(properties, objId, WPD.OBJECT_ORIGINAL_FILE_NAME)
                               ?? GetStringProperty(properties, objId, WPD.OBJECT_NAME)
                               ?? objId;

                if (IsFolder(properties, objId))
                {
                    WalkAndCopy(content, properties, objId, backupRoot, structure);
                }
                else
                {
                    if (!printedFolder)
                    {
                        Console.WriteLine();
                        Console.WriteLine("  [" + folderName + "]");
                        printedFolder = true;
                    }
                    CopyFile(content, properties, objId, fileName, backupRoot, structure);
                }
            }
        }

        Marshal.ReleaseComObject(enumerator);
    }

    static void CopyFile(IPortableDeviceContent content, IPortableDeviceProperties properties,
        string objId, string fileName, string backupRoot, string structure)
    {
        try
        {
            string   dateStr = GetStringProperty(properties, objId, WPD.OBJECT_DATE_MODIFIED);
            DateTime date    = DateTime.Now;
            if (dateStr != null) DateTime.TryParse(dateStr, out date);

            ulong fileSize = 0;
            try
            {
                IPortableDeviceValues vals;
                properties.GetValues(objId, null, out vals);
                vals.GetUnsignedLargeIntegerValue(ref WPD.OBJECT_SIZE, out fileSize);
            }
            catch { }

            string destDir  = GetDestinationPath(fileName, date, structure, backupRoot);
            if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
            string destFile = Path.Combine(destDir, fileName);

            if (File.Exists(destFile))
            {
                Console.Write("\r    SKIP  " + TruncateFileName(fileName, 55) + " (already exists)   ");
                skipped++;
                return;
            }

            Console.Write("\r    --> " + TruncateFileName(fileName, 60));

            IPortableDeviceResources resources;
            content.Transfer(out resources);

            uint    bufferSize = 524288; // 512 KB
            IStream stream;
            resources.GetStream(objId, ref WPD.RESOURCE_DEFAULT, 0, ref bufferSize, out stream);

            byte[]   buffer        = new byte[bufferSize];
            long     bytesCopied   = 0;
            DateTime transferStart = DateTime.Now;

            using (FileStream fs = File.Create(destFile))
            {
                while (true)
                {
                    uint bytesRead = 0;
                    stream.Read(buffer, bufferSize, out bytesRead);
                    if (bytesRead == 0) break;
                    fs.Write(buffer, 0, (int)bytesRead);
                    bytesCopied += bytesRead;

                    if (fileSize > 0)
                    {
                        double pct   = (double)bytesCopied / fileSize * 100;
                        double secs  = (DateTime.Now - transferStart).TotalSeconds;
                        string speed = secs > 0 ? FormatBytes((long)(bytesCopied / secs)) + "/s" : "...";
                        Console.Write("\r    --> {0,-55} {1,5:F1}%  {2,10}",
                            TruncateFileName(fileName, 55), pct, speed);
                    }
                }
            }

            Marshal.ReleaseComObject(stream);
            Marshal.ReleaseComObject(resources);

            TimeSpan duration = DateTime.Now - transferStart;
            string avgSpeed   = duration.TotalSeconds > 0
                ? FormatBytes((long)(bytesCopied / duration.TotalSeconds)) + "/s"
                : "instant";

            Console.WriteLine("\r    OK    {0,-55} {1,8}  {2,10}  {3}",
                TruncateFileName(fileName, 55),
                FormatBytes(bytesCopied),
                avgSpeed,
                duration.ToString(@"mm\:ss"));

            copied++;
            totalBytes += bytesCopied;
        }
        catch (Exception ex)
        {
            Console.WriteLine("\r    ERR   " + TruncateFileName(fileName, 55) + " : " + ex.Message);
            errors++;
        }
    }

    static string GetDestinationPath(string fileName, DateTime date, string structure, string backupRoot)
    {
        string ext = Path.GetExtension(fileName).ToLower();
        switch (structure)
        {
            case "flat":
                return backupRoot;
            case "type":
                string[] videoExts = { ".mp4", ".mov", ".m4v", ".avi", ".hevc" };
                return Path.Combine(backupRoot, Array.IndexOf(videoExts, ext) >= 0 ? "Videos" : "Photos");
            default: // yearmonth
                return Path.Combine(backupRoot, date.Year.ToString(), date.Month.ToString("D2"));
        }
    }

    static bool IsFolder(IPortableDeviceProperties properties, string objId)
    {
        try
        {
            IPortableDeviceValues vals;
            properties.GetValues(objId, null, out vals);
            Guid contentType;
            vals.GetGuidValue(ref WPD.OBJECT_CONTENT_TYPE, out contentType);
            return contentType == WPD.CONTENT_TYPE_FOLDER
                || contentType == WPD.CONTENT_TYPE_FUNCTIONAL_OBJECT
                || contentType == WPD.CONTENT_TYPE_STORAGE;
        }
        catch { return false; }
    }

    static string GetStringProperty(IPortableDeviceProperties properties, string objId, PropertyKey key)
    {
        try
        {
            IPortableDeviceValues vals;
            properties.GetValues(objId, null, out vals);
            if (vals == null) return null;
            string value;
            vals.GetStringValue(ref key, out value);
            return value;
        }
        catch { return null; }
    }

    static string FormatBytes(long bytes)
    {
        if (bytes >= 1073741824) return string.Format("{0:F1} GB", bytes / 1073741824.0);
        if (bytes >= 1048576)    return string.Format("{0:F1} MB", bytes / 1048576.0);
        if (bytes >= 1024)       return string.Format("{0:F1} KB", bytes / 1024.0);
        return bytes + " B";
    }

    static string TruncateFileName(string name, int max)
    {
        if (name.Length <= max) return name;
        return "..." + name.Substring(name.Length - (max - 3));
    }
}