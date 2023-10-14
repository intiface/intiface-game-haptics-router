# v18 (2023-10-06)

## Features

- Added UWP detection/injection
- New Icon

# v17 (2023-02-26)

## Bugfixes

- Fix bug where controller indexes were being checked raw instead of as a bitfield comparison,
  meaning no commands were ever sent to Intiface. (#40)

# v16 (2023-02-25)

## Features

- Reroute rumble from all connected controllers, not just first controller
- Remove embedded server, now only connects to Intiface Central
- Remove unity support (Broken, will be readded once fixed)
- Update to Buttplug C# v3
  - Mostly bugfixes for the moment.
- Add help tab

# v15 (2021-12-11)

## Features

- Update to Buttplug C# v2.0.6
  - Lots of bugfixes and hardware updates, see buttplug library CHANGELOG for more info

# v14 (2021-02-13)

## Bugfixes

- Update to Buttplug C# v1.0.14
  - Fixes crash on max power command

# v13 (2021-02-09)

## Bugfixes

- Turn autoupdate checking back on

# v12 (2021-02-06)

## Features

- Move to Buttplug C# v1.0.12
  - Lovense Diamo support
  - Error/bug fixes, fixes for Xinput issues

# v11 (2021-01-19)

## Features

- Move to Buttplug C# v1.0.9
  - Lovense Ferri support
  - Lovense dongle support fixes

# v10 (2021-01-08)

## Features

- Move to Buttplug C# v1.0.3
  - More toy support
  - Lovense Dongle support!
- Add process detaching (XInput only)
- Add ability to connect to Intiface Desktop
- Add property saving/storage
- Add packet gap timing setting to reduce toy lag issues

## Bugfixes

- Process list generation now uses all available processors
- Messages added in cases where restarts are required
- Vibration Multiplier now applies to VR attachments

# v9 (2020-08-30)

## Bugfixes

- Fixed version mismatch issue that caused update dialog to always
  open.

# v8 (2020-08-30)

## Features

- Add simple, single direction rotation support

## Bugfixes

- XInput hook now tries to hook all DLL versions, not just first
  found.
  - Some games bind to multiple DLLs, like xinput1_3 and xinput1_4. We
    hook all found DLLs now, expect that we'll only get one input back
    from a process.

# v7 (2020-07-04)

## Features

- Update Buttplug dependency to 0.5.9

## Bugfixes

- Add VR haptics detection for .Net Standard Unity games

# v6 (2020-03-10)

## Bugfixes

- Reprioritize load order of XInput DLLs

# v5 (2019-12-10)

## Features

- Updated Buttplug dependency to 0.5.6
- Updated other dependencies

# v4 (2019-09-28)

## Features

- Updated Buttplug dependency to 0.5.1
- Updated other dependencies

# v3 (2019-07-25)

## Features

- Updated Buttplug dependency to 0.4.7
- Readded Multiplier/Baseline capabilities
- Added update checking for github releases

## Bugfixes

- Multiplier now shown in visualizer
- Baseline will continue to run even if no vibration
- Fixed release name in CI

# v2 (2019-05-26)

## Features

- Updated Buttplug dependency to 0.4.5 (So we'll have Kiiroo
  2.1/RealTouch support at some point)
- Readded XInput capabilities
- Add OVRInput patching support (Needed for AltSpaceVR)

## Bugfixes

- Increase size of IPC buffer so we don't crash on backtraces from
  remote
- Manually copy EasyHook so that it works on CI.

# v1 (2019-05-23)

## Features

- Add support for Oculus "Clip" haptics, needed for Oculus Store Games

## Bugfixes

- IPC messages from Unity mod should now show up in logs.
- Clean up mod patching functions a bit

# v0 (2019-05-22)

## Features

- First Release of the new GHR (Upgraded from GVR)
- Basic usability for device selection using Intiface Embedded
- Only capable of Unity VR modding at the moment
- Visualizer doesn't work
