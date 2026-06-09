# iphoneyum

A Windows command-line tool for backing up all media from an iPhone over USB, directly to a folder on your PC. No iTunes. No iCloud. No third-party software. 100% Vibe coded in an hour and a half of frustration.

Built on the Windows Portable Devices (WPD) API using direct MTP access, which gives reliable streaming transfers with real-time progress per file.

---

## Dependencies

**None to install.** Everything iphoneyum needs ships with Windows 11:

| Dependency | Where it comes from |
|---|---|
| `.NET Framework 4.x` | Built into Windows 11 |
| `PortableDeviceApi.dll` | Built into Windows 11 (`C:\Windows\System32`) |
| `PortableDeviceTypes.dll` | Built into Windows 11 (`C:\Windows\System32`) |
| `ole32.dll` | Built into Windows 11 |
| `csc.exe` (to compile) | Ships with .NET Framework (`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\`) |

---

## Compile

From PowerShell, navigate to the folder containing `iphoneyum.cs` and run:

```powershell
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:exe /out:iphoneyum.exe iphoneyum.cs
```

This produces `iphoneyum.exe` in the same folder. You only need to compile once — the `.exe` can be copied to any Windows 11 machine and run without any additional setup.

---

## Before You Run

1. Connect your iPhone via USB
2. Unlock your iPhone
3. Tap **Trust This Computer** when prompted on the iPhone screen

If you've previously trusted the computer, just unlock the phone before running.

---

## Usage

```
iphoneyum.exe --backup-root <path> [--structure <yearmonth|flat|type>]
```

### Arguments

`--backup-root` *(required)*
The folder to back up into. Will be created if it doesn't exist.

`--structure` *(optional, default: yearmonth)*
How to organize files inside the backup folder:

| Value | Layout |
|---|---|
| `yearmonth` | `backup\2025\06\IMG_1234.HEIC` |
| `flat` | `backup\IMG_1234.HEIC` |
| `type` | `backup\Photos\IMG_1234.HEIC` or `backup\Videos\clip.MOV` |

### Examples

```powershell
# Back up with year/month folders (default)
.\iphoneyum.exe --backup-root "D:\iphone_backup\june_2026"

# Back up organized by media type
.\iphoneyum.exe --backup-root "D:\iphone_backup\june_2026" --structure type

# Everything in one flat folder
.\iphoneyum.exe --backup-root "D:\iphone_backup\june_2026" --structure flat
```

---

## Output

```
===========================================
  iPhone Backup Tool
===========================================

  Backup root : D:\iphone_backup\june_2026
  Structure   : yearmonth

  Searching for devices... found Apple iPhone

  Starting backup...

  [202510_a]
    OK    IMG_1060.HEIC                                   14.2 MB   23.4 MB/s  00:00
    OK    IMG_9963.MOV                                   231.5 MB   31.1 MB/s  00:07
    SKIP  IMG_1059.HEIC (already exists)

  [202506_a]
    --> IMG_2034.MOV                                      67.3%   28.9 MB/s

===========================================
  Backup Complete
===========================================
  Copied  : 18081 files (88.6 GB)
  Skipped : 4203 files (already existed)
  Errors  : 0
  Time    : 00:59:55
  Saved to: D:\iphone_backup\june_2026
```

Files that already exist at the destination are skipped automatically, so it's safe to run multiple times — only new files will be transferred.

---

## Notes

- **HEIC files** are the default iPhone photo format. Windows 11 can preview them natively, but you may need the [HEIF Image Extensions](https://apps.microsoft.com/detail/9pmmsr1cgpwg) from the Microsoft Store to open them in Photos.
- **AAE files** are edit sidecar files created by iOS. They're small XML files that store crop/filter adjustments. They're useless on Windows but are included in the backup for completeness — if you ever restore to an iPhone, they'll re-apply your edits.
- Transfer speed over USB typically runs **25–35 MB/s**, so expect roughly **1 hour per 100 GB**.
- The tool discovers any Apple device connected via MTP — if you have multiple Apple devices connected, it will use the first one it identifies as an iPhone.
