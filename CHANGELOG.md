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
