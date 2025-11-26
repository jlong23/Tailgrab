# TailGrab
VRChat Log Parser and Automation tool to help moderators manage trouble makers in VRChat since VRChat will not take moderation seriously.

# Usage
Open a Powershell or Command Line prompt in your windows host, change directory to where ```tailgrab``` has been extracted to and start it with:

```.\tailgrab.exe```

Or if you have moved where the VR Chat ```output_log_*.txt``` are located; then:

```.\tailgrab.exe {full path to VR Chat logs ending with a \}```


# Capabilities

The core concept of the TailGrab was to create a Windows friendly ```grep``` of VR Chat log events that would allow a group moderation team to review, get insights of bad actors and with the action framework to perform a scripted reaction to a log event.

EG:
A ```Vote To Kick``` is received from a patreon, the action sequence could:
- Send a OSC Avatar Parameter(s) that change the avatar's ear position
- Delay for a quarter of a second
- Send a keystroke to your soundboard application
- Send a keystroke to OBS to start recording


## POC Version
- Parse VRChat log files 
- World ```Furry Hideout``` will record User Pen Interaction
- Record User's avatar usage while in the instance
- Record User's moderation while in the instance (Warn and final Kick)
- Partial work with OSC Triggered events to send to your avatar
- Partial work with Keystroke events sent to a application of your choice