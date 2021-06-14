# Intiface Game Vibration Router

[![Patreon donate button](https://img.shields.io/badge/patreon-donate-yellow.svg)](https://www.patreon.com/qdot)
[![Discourse Forum](https://img.shields.io/badge/discourse-forum-blue.svg)](https://metafetish.club)
[![Discord](https://img.shields.io/discord/353303527587708932.svg?logo=discord)](https://discord.buttplug.io)
[![Twitter](https://img.shields.io/twitter/follow/buttplugio.svg?style=social&logo=twitter)](https://twitter.com/buttplugio)
[![Youtube](https://img.shields.io/badge/-youtube-red.svg)](https://youtube.buttplug.io)

The Intiface Game Haptics Router allows users to reroute vibration and
other events from video games to control sex hardware supported by the
[Buttplug Library](https://buttplug.io). This currently includes:

- Games using Windows XInput (Xbox Gamepads)
- Games using Unity VR (Any Version)

Toys that support vibration or rotation are supported by the GHR:

- [List of supported vibrating toys](https://iostindex.com/?filter0Features=OutputsVibrators&filter1ButtplugSupport=4)
- [List of supported rotating toys](https://iostindex.com/?filter0Features=OutputsRotators&filter1ButtplugSupport=4)

Releases can be downloaded [on the releases page.](https://github.com/intiface/intiface-game-haptics-router/releases)

## Table Of Contents

- [Support The Project](#support-the-project)
- [FAQ](#faq)
- [License](#license)

## Support The Project

If you find this project helpful, you can [support us via
Patreon](http://patreon.com/qdot)! Every donation helps us afford more
hardware to reverse, document, and write code for!

## How To Use

- [Download Installer](https://github.com/intiface/intiface-game-haptics-router/releases)
- Install It
- Run the GHR
- Hardware will automatically be scanned for. To use the hardware,
  click check box next to it.
- Moddable game processes will show up in the process list. If your
  game process is not in the list, hit Refresh. If it doesn't show up
  after that, we don't support it. If you think we should support it,
  file an issue here or yell at qDot on
  [discord](https://discord.buttplug.io).
- Once you see your game process, click on it and then hit "Attach".
- Note that there is currently no "Detach". The mod will stay attached
  until the game process ends. You can close the GHR at any time, and
  the game shouldn't complain, but who knows. You're hooking your game
  up to buttplugs, so we're firmly in YOLO land here.

Alternatively, see [this youtube demo
video](https://youtu.be/gPlhEoa3Fcg) for a quick tutorial on the
software, at least, until I spend the time to build a better one once
more of this is finished.

## FAQ

**How Finished is the GHR?**

The GHR is currently in *Alpha* phase. This means it barely works. The
visualizer panel isn't hooked up, XInput isn't hooked up, there isn't
even an about panel. 

Expect breakage for a while, but I'm putting it out now so I can get
bug reports on basic usage.

**What Games does the GHR Work With?**

Technically, it should support any game that uses XInput, or that use
Unity VR and making the VR controllers vibrate.

That said, not all games are going to work with it in a way that is
useful or fun.

For a list of games people have tried with the GHR (or the earlier
version, known of the GVR) and ratings on how well they work, [check
out this forum
thread](https://metafetish.club/t/game-haptics-router-compatibility-thread/105/7).

Also, for Unity, there have been some issues with mods not attaching
correctly to older games. This issues is being tracked
[here](https://github.com/intiface/intiface-game-haptics-router/issues/2).

**How does the GHR work?**

We inject code into a running game process, find the rumble functions,
and any time they are called, forward the information out to the GHR,
which then sends it to whatever hardware you've connected.

For XInput, we use [EasyHook](https://easyhook.github.io/) for
attaching from managed C# to unmanaged C.

For Unity, we use a combination of
[Harmony](https://github.com/pardeike/Harmony/) and
[SharpMonoInjector](https://github.com/warbler/SharpMonoInjector) for
attaching to the Unity assemblies.

**What kind of events does the GHR handle?**

Right now, any time a gamepad or VR controller rumbles, we pass that
information on to make hardware connected to the GHR rumble.

In the future, we do plan on supporting game specific mods.

**Does the GHR require putting files in the game directory?**

No. All GHR mods are completely remote, require no file system
writing, and only live for the process lifetime. 

No one will know you put a buttplug in your game.

**Can I use the GHR with games with anti-cheat mechanisms?**

No. Our injection and loading process, while usually not VAC
triggering, will still be caught by games like Overwatch and denied.
We do not recommend using the GHR with any game that has anti-cheat
mechanisms, and we are not resposible for whatever may happen to your
game account if you try it.

**What type of hardware does the GHR work with?**

At the moment, only vibrating toys, as listed on the bottom of the
front page at [https://buttplug.io]. 

Support for thrusting/stroking/rotating toys will be available in a
future release. See [this
issue](https://github.com/intiface/intiface-game-haptics-router/issues/1)
for more information.

**What's the Intiface Panel do?**

The Intiface Panel is how we deal with connecting to supported
hardware. The user can either use the embedded version, or connect to
a copy of [Intiface
Desktop](https://github.com/intiface/intiface-desktop/releases).

We allow connections to Intiface Desktop so that users that requires
different setups for bluetooth can be supported (for instance, having
to use bluetooth through a phone or Raspberry Pi). More on this once
that's actually a case we fully cover.

**Will this work on Win 7/Mac/Linux?**

Possibly.

- Currently, this should run on Windows 7, but Bluetooth will not be
  available. This will be coming in a future release, most likely
  supported via Intiface Desktop (See Intiface Panel FAQ question).
- There's a chance we could support Mac/Linux Unity games via the same
  mod mechanism we currently use for Unity VR on windows, and
  Buttplug/Intiface already supports those platforms. This mostly
  comes down to demand for implementation, for which there is none at
  the moment.

## License

The Intiface Game Haptics Router is BSD 3-Clause licensed. More
information is available in the LICENSE file.

