[Back](../README.md)
# Application Log line parsing and actions - "./Config.json" File

The confiuration for TailGrab uses a JSON formated payload of the base attribute "lineHandlers" that contains a array of LineHandler Objects, Those may have a attribute of "actions" that contain an array of Action Objects.  This configuration is loaded on application start. Starting with V1.1.6, line handlers expose some log data to the actions and actions can use that data elements in TTS, HTTP/S APIs

## LineHandler Definition

The LineHandler defines what type of system action to perform, what regular expression to use to detect that type of log line and user actions to perform when detected.

|LineHandlerType | Definition |
|--------|--------|
| AvatarChange | Detects when the VRChat user changes avatars; Primary function is to link the Active Users with their currently worn Avatar |
| AvatarUnpack | Detects when the VRChat avatar unpacks; Primary function is to look up publicly available avatar data and report the avatarId in the logs for crashers |
| Emoji | Detects when the VRChat user uses an Emoji; Primary function is to link the Active Users with Emoji Image AI Evaluation for SFW Instance management |
| OnPlayerJoin | Detects when a VRChat user joins or leaves the instance; Primary function is to lookup the user for Active User panel or Past User panel |	
| OnPlayerNetwork | Broken at the moment, was used to detect what user's instance network ID was assigned to them for linkage to Furry Hideout's Pen Usage; Relies on World Debug output |
| PenNetwork | Broken at the moment, was used to detect what user's instance network ID was assigned to them for linkage to Furry Hideout's Pen Usage; Relies on World Debug output |
| Print | Detects when a VRChat user has dropped a print in the instance; Primary function is to link the Active Users with Print Image AI Evaluation for SFW Instance management |
| Sticker | Detects when a VRChat user has dropped a sticker in the instance; Primary function is to link the Active Users with Sticker Image AI Evaluation for SFW Instance management |
| VTK | Detects when a VRChat user has a Vote to Kick (VTK) action; Primary function is to link the Active Users with VTK notice and log it |
| WarnKick | Detects when a Moderator has warned or Kicked a user in the instance; Primary function is to link the Active Users with Warn/Kick notice and log it. | 
| WorldChange | Detects when the VRChat user changes worlds; Primary function is to flush queues and clean up lists for the next world instance. |
| Quit | Detects when the VRChat user quits the application; Primary function is to flush queues and clean up lists for the next world instance. |


The LineHandler configuration is a JSON object with the following attributes:

|Attribute | Definition |
|--------|--------|
| handlerTypeValue | An enumeration value of the internal LineHandler code segments. See ```handlerTypes``` |
| enabled | Boolean ```true``` or ```false``` to direct the application to include or temporarly ignore the configuration. |
| patternTypeValue | An enumeration value of ```default``` or ```override```; Default will use the programmer's defined default for the Regular Expression to match/extract and a Override will allow the user to fine tune or respond to VRChat application log changes with the attribute ```pattern``` |
| pattern | The Regular expression for the Pattern to match/extract, does nothing unless patternTypeValue is set to override |
| logOutput | Boolean ```true``` or ```false``` to direct the application to log the output of the Line Handler. |
| logOutputColor | A value of ```Default``` will use the programmers ANSI codes for the log output, if you use the last digits of the ANSI codes here, they are used.  EG ```"37m"``` |
| actions | A array of Action Configuration elements or do nothing by leaving it as an empty array ```[]``` |


### LineHandler Exposed Variables for Actions

When the line is parsed and matched, each handler may extract information and place them into replacement varibles for usage by the actions attached to the LineHandler.

For example, the WarnKick LineHandler will extract the userId, userName and what action was used on them from the log line and place them into the replacement variables ```{userId}```, ```{userName}``` and ```{action}``` for usage by the actions of the LineHandler, like TextToSpeech that could report "You have {action} user name {userName}" or a HTTP/S API Call to your groups server to record the activity.

#### AvatarChange LineHandler

| Replacement Variable | Contents | Source |
|--------|--------|
| timestamp | The timestamp of the log line | Log line |
| userId | The VRChat userId of the user that changed avatars| VRC API |
| userName | The VRChat userName of the user that changed avatars | Log Line |
| avatarName | The VRChat avatarName of the avatar that was changed into, from the log line| Log line |

#### AvatarUnpack LineHandler

| Replacement Variable | Contents | Source |
|--------|--------|
| timestamp | The timestamp of the log line | Log line |
| userName | The VRChat userName of the user that UPLOADED the avatar | Log Line |
| avatarName | The VRChat avatarName of the avatar that was changed into, from the log line| Log line |

#### AvatarUnpack LineHandler

| Replacement Variable | Contents | Source |
|--------|--------|
| timestamp | The timestamp of the log line | Log line |
| userName | The VRChat userName of the user that UPLOADED the avatar | Log Line |
| avatarName | The VRChat avatarName of the avatar that was changed into, from the log line| Log line |

#### Emoji LineHandler

| Replacement Variable | Contents | Source |
|--------|--------|
| timestamp | The timestamp of the log line | Log line |
| userId | The VRChat userId of the user that used the emoji| Log line |
| userName | The VRChat userName of the user that emitted the emoji | VRC API |
| inventoryId | The VRChat inventoryId of the emoji that was used | Log line |

#### OnPlayerJoin LineHandler

| Replacement Variable | Contents | Source |
|--------|--------|
| timestamp | The timestamp of the log line | Log line |
| userId | The VRChat userId of the user that joined | Log line |
| userName | The VRChat userName of the user that joined | Log line |
| action | The user instance state Join/Left | Log line |

#### OnPlayerNetwork LineHandler

| Replacement Variable | Contents | Source |
|--------|--------|
| timestamp | The timestamp of the log line | Log line |
| userName | The VRChat userName of the user that joined | Log line |
| networkId | The user instance state network id | Log line |

#### PenNetworkId LineHandler

| Replacement Variable | Contents | Source |
|--------|--------|
| timestamp | The timestamp of the log line | Log line |
| objectId | The VRChat World Object NetworkId of the pen that was used | Log line |
| fromUserId | The VRChat user network id that was 'Owner' of the pen before | Log line |
| toUserId | The VRChat user network id that is now the 'Owner' | Log line |

#### Print LineHandler

| Replacement Variable | Contents | Source |
|--------|--------|
| timestamp | The timestamp of the log line | Log line |
| fileUrl | The VRChat Print Data URL | Log line |

#### Quit LineHandler

| Replacement Variable | Contents | Source |
|--------|--------|
| timestamp | The timestamp of the log line | Log line |
| action | Always 'quit' | Hard Coded |
| totalTime | The total time the user spent in VRChat (hh:mm:ss.ssss) | Log line |

#### Sticker LineHandler

| Replacement Variable | Contents | Source |
|--------|--------|
| timestamp | The timestamp of the log line | Log line |
| userId | The VRChat userId of the user that used the sticker| Log line |
| userName | The VRChat userName of the user that emitted the sticker | Log line |
| fileUrl | The VRChat inventoryId of the sticker that was used | Log line |

#### VTK LineHandler

| Replacement Variable | Contents | Source |
|--------|--------|
| timestamp | The timestamp of the log line | Log line |
| userId | The VRChat userId of the user that had the VTK | VRC API |
| userName | The VRChat userName of the user that had the VTK | Log line |

#### Warn/Kick LineHandler

| Replacement Variable | Contents | Source |
|--------|--------|
| timestamp | The timestamp of the log line | Log line |
| userId | The VRChat userId of the user that had the VTK | VRC API |
| userName | The VRChat userName of the user that had the VTK | Log line |
| action | The user instance moderation state Warn/Kick | Log line |

#### WorldChange LineHandler

| Replacement Variable | Contents | Source |
|--------|--------|
| timestamp | The timestamp of the log line | Log line |
| worldId | The VRChat worldId of the world that was changed to | Log line |
| instanceId | The VRChat instanceId of the world that was changed to 93883~group(grp_b6593dd3-6e86-4951-a8d8-e2fa3cb91096)~groupAccessType(public)~region(us) | Log line |




## Action Definitions

### actionTypeValue Enum Values

|actionTypeValue | Definition |
|--------|--------|
| DelayAction | Delay a defined amount of time before next action. |
| OSCAction | Send OSC Avatar Parameter values to your VRChat Avatar. |
| KeyPressAction | Send Keystrokes to a named open window title on your system. |
| PlaySoundAction | Play a local application sound file. |
| TTSAction | Text to Speech phrase to be spoken. |
| HTTPGetAction | Send a HTTP GET request to a defined URL. |
| HTTPPostAction | Send a HTTP POST request to a defined URL. |


### Action: DelayAction Definition

The Delay Action will allow you to pause other actions with millisecond precision.  If you need to pause for 1 second, use 1000 as the delay time.  This action is used when there is a need for a sound trigger to play or you want to send stacked keystrokes to an application that is running.

|Attribute | Definition |
|--------|--------|
| actionTypeValue | An enumeration value of the internal LineHandler code segments. See ```actionTypeValue``` |
| milliseconds | integer value of milliseconds to wait for. |

### Action: OSCAction Definition

The OSC Action will allow you to send values (```Float```/```Int```/```Bool```) to your VRChat avatar that could be used to trigger animations on it during a action set.

|Attribute | Definition |
|--------|--------|
| actionTypeValue | ```OSCAction``` See ```actionTypeValue``` |
| parameterName | The VRChat Avatar Parameter Path to send to; EG. ```/avatar/parameters/Ear/Right_Angle``` |
| oscValueType | OSC Value types associated with that Parameter Path; ```Float``` or ```Int``` or ```Bool``` |
| value | The Value to send to your avatar; Floats expect a decimal place ```0.0```, Int expect no decimal place ```0```, and Bool expects either ```true``` or ```false```

### Action: TTSAction Definition (Not Working ATM)

The TTS Action will allow you to say a phrase when triggered.

|Attribute | Definition |
|--------|--------|
| actionTypeValue | ```TTSAction``` See ```actionTypeValue``` |
| text | The phrase you wish to have spoken |
| volume | Volume 0...100 |
| rate | The speed of the speech -10...10 |

### KeyPressAction Definition in Actions

The KeyPress action will let you send keystrokes to a targed application by it's HWND Window Title, if the application runs windowless/without a title bar, this may not work for you.

|Attribute | Definition |
|--------|--------|
| actionTypeValue | ```KeyPressAction``` See ```actionTypeValue``` |
| windowTitle | Windows application title; EG. ```VRChat``` |
| keys | An encoded defintion of keys to send to the application; see below |

### PlaySoundAction Definition in Actions

The PlaySound action will let you play a local application sound file when triggered. You hear it only unless you have routed your system audio to a VRChat microphone input, then it will be heard by others in the instance.  Use keypress action to trigger some audio globally with a soundboard reacting to the keypress.

|Attribute | Definition |
|--------|--------|
| actionTypeValue | ```PlaySoundAction``` See ```actionTypeValue``` |
| soundFile | The path to the sound file to play; EG. ```alert``` |


From https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.sendkeys?view=windowsdesktop-10.0

The plus sign (```+```), caret (```^```), percent sign (```%```), tilde (```~```), and parentheses ```()``` have special meanings to SendKeys. To specify one of these characters, enclose it within braces ```({})```. For example, to specify the plus sign, use "{+}". To specify brace characters, use ```"{{}"``` and ```"{}}"```. Brackets ```([ ])``` have no special meaning to SendKeys, but you must enclose them in braces. In other applications, brackets do have a special meaning that might be significant when dynamic data exchange (DDE) occurs.

To specify characters that aren't displayed when you press a key, such as ```ENTER``` or ```TAB```, and keys that represent actions rather than characters, use the codes in the following table.

#### Key	Encoding
|Key Desired | Key Encoding |
|--------|--------|
|BACKSPACE | {BACKSPACE}, {BS}, or {BKSP} |
|BREAK | {BREAK} |
|CAPS LOCK | {CAPSLOCK} |
|DEL or DELETE | {DELETE} or {DEL} |
|DOWN ARROW|{DOWN}|
|END | {END}
|ENTER | {ENTER} or ~
|ESC | {ESC}
|HELP | {HELP}
|HOME | {HOME}
|INS or INSERT | {INSERT} or {INS}
|LEFT ARROW | {LEFT}
|NUM LOCK | {NUMLOCK}
|PAGE DOWN | {PGDN}
|PAGE UP | {PGUP}
|PRINT SCREEN | {PRTSC} (reserved for future use)
|RIGHT ARROW | {RIGHT}
|SCROLL LOCK | {SCROLLLOCK}
|TAB | {TAB}
|UP ARROW | {UP}
|F1 | {F1}
|F2 | {F2}
|F3 | {F3}
|F4 | {F4}
|F5 | {F5}
|F6 | {F6}
|F7 | {F7}
|F8 | {F8}
|F9 | {F9}
|F10 | {F10}
|F11 | {F11}
|F12 | {F12}
|F13 | {F13}
|F14 | {F14}
|F15 | {F15}
|F16 | {F16}
|Keypad add | {ADD}
|Keypad subtract | {SUBTRACT}
|Keypad multiply | {MULTIPLY}
|Keypad divide | {DIVIDE}

To specify keys combined with any combination of the SHIFT, CTRL, and ALT keys, precede the key code with one or more of the following codes.

|Key Desired | Key Encoding |
|--------|--------|
|SHIFT | + |
|CTRL | ^ |
|ALT | % |

To specify that any combination of SHIFT, CTRL, and ALT should be held down while several other keys are pressed, enclose the code for those keys in parentheses. For example, to specify to hold down SHIFT while E and C are pressed, use ```"+(EC)"```. To specify to hold down SHIFT while E is pressed, followed by C without SHIFT, use ```"+EC"```.

To specify repeating keys, use the form ```{key number}```. You must put a space between key and number. For example, ```{LEFT 42}``` means press the LEFT ARROW key 42 times; ```{h 10}``` means press H 10 times.


