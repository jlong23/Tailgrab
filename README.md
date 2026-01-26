# TailGrab
VRChat Log Parser and Automation tool to help moderators manage trouble makers in VRChat since VRChat will not take moderation seriously.

# Usage
Open a Powershell or Command Line prompt in your windows host, change directory to where ```tailgrab``` has been extracted to and start it with:

```.\tailgrab.exe```

Or if you have moved where the VR Chat ```output_log_*.txt``` are located; then:

```.\tailgrab.exe {full path to VR Chat logs ending with a \}```

# Configuration

## Environment Variables
The TailGrab application will look for the following environment variables to connect to your VRChat API and OLLama AI services.

Tailgrab uses VR Chat's public API to get information about avatars for the BlackListed Database (SQLite Local DB) and to get user profile infoformation for Profile Evaluation with the AI services.
OLLama Cloud AI services are used to evaluate user profiles for potential bad actors based on your custom prompt criteria.  The OLLama API is called only once for a MD5 checksummed profile to reduce API usage and cost.


|Environment Variable | Definition |
|--------|--------|
| VRCHAT_USERNAME | Your VRChat User Name (What you use to sign into the VR Chat Web Page). |
| VRCHAT_PASSWORD | Your VRChat Password (What you use to sign into the VR Chat Web Page). |
| VRCHAT_TWO_FACTOR_SECRET | Your VRChat Two Factor Authentication Secret (If you have 2FA enabled on your account; **RECOMENDED** ). See https://docs.vrchat.com/docs/using-2fa for more information. |
| OLLAMA_API_KEY | Your OLLama API Key to access your AI services. See https://ollama.com/docs/api for more information. |

On Windows 11 you can set these environment variables by searching for "Environment Variables" in the Start Menu and selecting "Edit the User environment variables". Then click on the "Environment Variables" button and add the variables under "User variables" (Suggested) or "System variables".
If launching from a Powershell or Command Line prompt, you will need to close the window for the values to be set for that session

## "Config.json" File

The confiuration for TailGrab uses a JSON formated payload of the base attribute "lineHandlers" that contains a array of LineHandler Objects, Those may have a attribute of "actions" that contain an array of Action Objects.

## LineHandler Definition

The LineHandler defines what type of system action to perform, what regular expression to use to detect that type of log line and user actions to perform when detected.

|Attribute | Definition |
|--------|--------|
| handlerTypeValue | An enumeration value of the internal LineHandler code segments. See ```handlerTypes``` |
| enabled | Boolean ```true``` or ```false``` to direct the application to include or temporarly ignore the configuration. |
| patternTypeValue | An enumeration value of ```default``` or ```override```; Default will use the programmer's defined default for the Regular Expression to match/extract and a Override will allow the user to fine tune or respond to VRChat application log changes with the attribute ```pattern``` |
| pattern | The Regular expression for the Pattern to match/extract, does nothing unless patternTypeValue is set to override |
| logOutput | Boolean ```true``` or ```false``` to direct the application to log the output of the Line Handler. |
| logOutputColor | A value of ```Default``` will use the programmers ANSI codes for the log output, if you use the last digits of the ANSI codes here, they are used.  EG ```"37m"``` |
| actions | A array of Action Configuration elements or do nothing by leaving it as an empty array ```[]``` |

## actionTypeValue Enum Values

|actionTypeValue | Definition |
|--------|--------|
| DelayAction | Delay a defined amount of time before next action. |
| OSCAction | Send OSC Avatar Parameter values to your VRChat Avatar. |
| KeyPressAction | Send Keystrokes to a named open window title on your system. |


## Action: DelayAction Definition

The Delay Action will allow you to pause other actions with millisecond precision.  If you need to pause for 1 second, use 1000 as the delay time.  This action is used when there is a need for a sound trigger to play or you want to send stacked keystrokes to an application that is running.

|Attribute | Definition |
|--------|--------|
| actionTypeValue | An enumeration value of the internal LineHandler code segments. See ```actionTypeValue``` |
| milliseconds | integer value of milliseconds to wait for. |

## Action: OSCAction Definition

The OSC Action will allow you to send values (```Float```/```Int```/```Bool```) to your VRChat avatar that could be used to trigger animations on it during a action set.

|Attribute | Definition |
|--------|--------|
| actionTypeValue | ```OSCAction``` See ```actionTypeValue``` |
| parameterName | The VRChat Avatar Parameter Path to send to; EG. ```/avatar/parameters/Ear/Right_Angle``` |
| oscValueType | OSC Value types associated with that Parameter Path; ```Float``` or ```Int``` or ```Bool``` |
| value | The Value to send to your avatar; Floats expect a decimal place ```0.0```, Int expect no decimal place ```0```, and Bool expects either ```true``` or ```false```

## Action: TTSAction Definition

The TTS Action will allow you to say a phrase when triggered.

|Attribute | Definition |
|--------|--------|
| actionTypeValue | ```TTSAction``` See ```actionTypeValue``` |
| text | The phrase you wish to have spoken |
| volume | Volume 0...100 |
| rate | The speed of the speech -10...10 |

## Action : KeyPressAction Definition

** Still Broken with Beta 3 release; Will be fixed in future release **

The KeyPress action will let you send keystrokes to a targed application by it's HWND Window Title, if the application runs windowless/without a title bar, this may not work for you.

|Attribute | Definition |
|--------|--------|
| actionTypeValue | ```KeyPressAction``` See ```actionTypeValue``` |
| windowTitle | Windows application title; EG. ```VRChat``` |
| keys | An encoded defintion of keys to send to the application; see below |


From https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.sendkeys?view=windowsdesktop-10.0

The plus sign (```+```), caret (```^```), percent sign (```%```), tilde (```~```), and parentheses ```()``` have special meanings to SendKeys. To specify one of these characters, enclose it within braces ```({})```. For example, to specify the plus sign, use "{+}". To specify brace characters, use ```"{{}"``` and ```"{}}"```. Brackets ```([ ])``` have no special meaning to SendKeys, but you must enclose them in braces. In other applications, brackets do have a special meaning that might be significant when dynamic data exchange (DDE) occurs.

To specify characters that aren't displayed when you press a key, such as ```ENTER``` or ```TAB```, and keys that represent actions rather than characters, use the codes in the following table.

### Key	Encoding
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
