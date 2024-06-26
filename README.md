# Intiface Game Vibration Router

[![Patreon donate button](https://img.shields.io/badge/patreon-donate-yellow.svg)](https://www.patreon.com/qdot)
[![Discourse Forum](https://img.shields.io/badge/discourse-forum-blue.svg)](https://discuss.buttplug.io)
[![Discord](https://img.shields.io/discord/353303527587708932.svg?logo=discord)](https://discord.buttplug.io)
[![Twitter](https://img.shields.io/twitter/follow/buttplugio.svg?style=social&logo=twitter)](https://twitter.com/buttplugio)
[![Youtube](https://img.shields.io/badge/-youtube-red.svg)](https://youtube.buttplug.io)

The Intiface Game Haptics Router allows users to reroute vibration and other events from video games
to control sex hardware supported by [Buttplug and Intiface Central](https://intiface.com/central). This currently
includes:

- Games using Windows XInput or UWP (Xbox Gamepads)

Toys that support vibration or rotation are supported by the GHR:

- [List of supported vibrating toys](https://iostindex.com/?filter0Features=OutputsVibrators&filter1ButtplugSupport=4)
- [List of supported rotating toys](https://iostindex.com/?filter0Features=OutputsRotators&filter1ButtplugSupport=4)

Releases can be downloaded [on the releases page.](https://github.com/intiface/intiface-game-haptics-router/releases)

**[INTIFACE CENTRAL](https://intiface.com/central) is required to be installed for use with GHR.**

## Table Of Contents

- [Support The Project](#support-the-project)
- [FAQ](#faq)
- [License](#license)

## Support The Project

If you find this project helpful, you can [support us via Patreon](http://patreon.com/qdot)! Every
donation helps us afford more hardware to reverse, document, and write code for!

## How To Use

See the Help tab in the application after install.

## FAQ

**How Finished is the GHR?**

The GHR is currently in *Alpha* phase. This means it barely works. The visualizer panel isn't hooked
up, XInput isn't hooked up, there isn't even an about panel. 

Expect breakage for a while, but I'm putting it out now so I can get bug reports on basic usage.

**What Games does the GHR Work With?**

Technically, it should support any game that uses XInput, or that use Unity VR and making the VR
controllers vibrate.

That said, not all games are going to work with it in a way that is useful or fun.

For a list of games people have tried with the GHR (or the earlier version, known of the GVR) and
ratings on how well they work, [check out this forum
thread](https://metafetish.club/t/game-haptics-router-compatibility-thread/105/7).

Also, for Unity, there have been some issues with mods not attaching correctly to older games. This
issues is being tracked [here](https://github.com/intiface/intiface-game-haptics-router/issues/2).

**How does the GHR work?**

We inject code into a running game process, find the rumble functions, and any time they are called,
forward the information out to the GHR, which then sends it to whatever hardware you've connected.

For XInput, we use [EasyHook](https://easyhook.github.io/) for attaching from managed C# to
unmanaged C.

For Unity, we were using a combination of [Harmony](https://github.com/pardeike/Harmony/) and
[SharpMonoInjector](https://github.com/warbler/SharpMonoInjector) for attaching to the Unity
assemblies. This stopped working a while ago and needs to be rebuilt.

**What kind of events does the GHR handle?**

Right now, any time a gamepad rumbles, we pass that information on to make hardware connected to the
GHR rumble.

In the future, we do plan on supporting game specific mods.

**Does the GHR require putting files in the game directory?**

No. All GHR mods are completely remote, require no file system writing, and only live for the
process lifetime. 

No one will know you put a buttplug in your game.

**Can I use the GHR with games with anti-cheat mechanisms?**

No. Our injection and loading process, while usually not VAC triggering, will still be caught by
games like Overwatch, Rocket League, etc... and denied. We do not recommend using the GHR with any
game that has anti-cheat mechanisms, and we are not resposible for whatever may happen to your game
account if you try it.

**What type of hardware does the GHR work with?**

At the moment, only vibrating toys, as listed on the bottom of the front page at
[https://buttplug.io]. 

Support for thrusting/stroking/rotating toys will be available in a future release. See [this
issue](https://github.com/intiface/intiface-game-haptics-router/issues/1) for more information.

**What's the Intiface Panel do?**

The Intiface Panel is how we deal with connecting to supported hardware. Users are required to use [Intiface Central](https://intiface.com/central)

## License

The Intiface Game Haptics Router is BSD 3-Clause licensed. More
information is available in the LICENSE file.

