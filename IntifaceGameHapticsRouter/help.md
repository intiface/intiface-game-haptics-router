# Intiface Game Haptics Router Help

The following is a brief tutorial on usage of the Game Haptics Router (GHR). This should be enough to get you up and running with the system, or help troubleshoot any issues you may have.

## Compatible Games Lists

The best place to find compatible games is the [The GHR Game Compatibility Thread](https://discuss.buttplug.io/t/game-haptics-router-compatibility-thread/74) thread on the Intiface/Buttplug forums.

## Getting Help If You Have Crashes/Issues

In order to inject rumble reroute functionality into games, the GHR does some very weird things that may make windows angry. This will lead to the GHR program crashing, buttons not working, etc.

If you are having problems with the GHR, the following resources are available for support:

- See the troubleshooting section at the end of this help document.
- [Threads on the Buttplug/Initface Forums](https://discuss.buttplug.io)  (No account required to view), including [The troubleshooting thread on the Buttplug/Intiface Forums](https://discuss.buttplug.io/t/troubleshooting-install-issues-with-the-game-haptics-router/73) and [The GHR Game Compatibility Thread](https://discuss.buttplug.io/t/game-haptics-router-compatibility-thread/74)
- [The GHR Channel on the Buttplug.io discord](https://discord.buttplug.io)

## The GHR Requires Intiface Central

As of v16, the GHR now requires [Intiface Central](https://intiface.com/central) to be up and running in order to access hardware. [Intiface Central](https://intiface.com/central) is the hub program for the Buttplug and Intiface Ecosystems, and contains all of the hardware connection/control functionality needed to make things like the GHR work. It's free, open source, and available on both desktop and mobile platforms.

## Using the GHR

The steps to using the GHR are as follows. If you run into any issues during these steps, see the Troubleshooting section below.

- Start Intiface Central, and hit the "Start Server" button
- Start the GHR, and make sure it's connected to Intiface Central.Intiface Central should show it as the currently connected client.
- From the GHR, hit "Start Scanning" to find your hardware
- Once your hardware is in the device list, click the checkbox next to it to make it active as a
  rumble rerouting target.
- Start the game you would like to reroute rumble from. This step can happen any time during the
  process, so it's not a problem if the game is already started before you start Intiface
  Central/GHR. The step is included at this point here for instruction clarity.
- Alt-tab back to the GHR, go to the "Process List" tab, and hit "Refresh List"
- If the process name of your game shows up in the list with "(XInput)" next to it, it means that it
  may be (but is not guaranteed to be) compatible with the GHR. Click on the process in the list and hit "Attach to Process"
- If all goes well, the "Attach to Process" button should be grayed out and the status message will
  read "Attached to Process".
- Alt-tab back to the game. Rumble should route from the game to the hardware you marked as active
  in the earlier steps.
- To see rumble signals coming from the game, check out the Visualizer panel. All rumble should show
  up on the graphs as events happen. If there is no activity on the graphs, see the troubleshooting section.

## Troubleshooting Common Issues

### If the GHR or game crashes or won't start

[See the troubleshooting thread on the Buttplug/Intiface Forums](https://discuss.buttplug.io/t/troubleshooting-install-issues-with-the-game-haptics-router/73) (No account required to view).

### Hardware related issues

If your toy isn't showing up on the device list or isn't reacting to rumble, it's best to make sure the toy is working with Intiface Central first.

- Close the game and the GHR
- Go to Intiface Central, make sure the server is started. If not, start it.
- Go to the "Devices" Tab of Intiface Central. Hit "Start Scanning", and make sure your device shows
  up. If your device shows up, move the related control sliders on the device panel to make sure that toy is working correctly with Intiface. If your device does not show up, there may be connection issues with Intiface. Contact support via one of the methods mentioned above.

If your hardware is visible and controllable through Intiface, see the next section for debugging issues in games.

### No rumble rerouted from Game

If you know your hardware works but aren't getting rumble events from the game you're trying:

- Make sure the process is attached in the GHR
- Go to the visualizer screen and make sure events are shown in the graph whenever rumble happens
  in the game.

If no signals are showing up on the visualizer, there may be compatibility issues with the game. Either post on the discord or forums listed above to see if there are issues with games.