# TailGrab
VRChat Log Parser and Automation tool to help moderators manage trouble makers in VRChat since VRChat Management Team is not taking moderation seriously; ever.

## Configuration
See [Application Config](./docs/Config_Application.md) for details on how to configure the application to connect to API services.

See [Config Line Handlers](./docs/Config_LineHandlers.md) for details on how to configure the application to respond to VRChat local game log events.

# Capabilities

The core concept of the TailGrab was to create a Windows friendly ```grep``` of VR Chat log events that would allow a group moderation team to review, get insights of bad actors and with the action framework to perform a scripted reaction to a VR Chat game log event.

EG:
A ```Vote To Kick``` is received from a patreon, the action sequence could:
- Send a OSC Avatar Parameter(s) that change the avatar's ear position
- Delay for a quarter of a second
- Send a keystroke to your soundboard application
- Send a keystroke to OBS to start recording

# Usage
Click the windows application or open a Powershell or Command Line prompt in your windows host, change directory to where ```tailgrab.exe``` has been extracted to and start it with:

```.\tailgrab.exe```

Or if you have moved where the VR Chat ```output_log_*.txt``` are located; then:

```.\tailgrab.exe -l {full path to VR Chat logs ending with a \}```

If you need to clear all registry settings stored for TailGrab, you can run:

```.\tailgrab.exe -clear```

This will remove all stored configuration and secret values from the Windows Registry for TailGrab, you can then reconfigure the application as needed, save them, restart and get back to watching the instance.

## VRChat Source Log Files

By default TailGrab will look for VRChat log files in the default location of:

```YourUserHome\AppData\LocalLow\VRChat\VRChat\```

This can be overridden by passing the full path to the VRChat log files as the first argument to the application.

```.\\tailgrab.exe D:\MyVRChatLogs\```

## Watching TailGrab Application Logs

The TailGrab application will log it's internal operations to the ```./logs``` folder in the same directory as the application executable.  Each run of the application will create a new log file with a timestamp in the filename.

If you want to watch the application logs in real time, you can use a tool like ```tail``` from Git Bash or ```Get-Content``` from Powershell session with the log filename.

```Get-Content -Path .\logs\tailgrab-2026-01-26.log -wait```


