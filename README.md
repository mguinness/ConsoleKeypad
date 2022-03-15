## Introduction

This project is a command line application that acts as a virtual keypad for the [Ademco](https://en.wikipedia.org/wiki/Ademco_Security_Group) Vista alarm panel that is connected to a [Eyez-On](https://www.eyezon.com/) EnvisaLink module.

Under the hood it uses the [Telnet](https://en.wikipedia.org/wiki/Telnet) protocol to communicate with the EnvisaLink module using the [TPI protocol](http://forum.eyez-on.com/FORUM/viewtopic.php?t=301) by Envisacor, specifically the EnvisaLink Vista TPI Programmer's Document.

## Building

The project was developed using [.NET Core](https://en.wikipedia.org/wiki/.NET) and can be built using the [.NET 5.0](https://dotnet.microsoft.com/en-us/download/dotnet/5.0) SDK for Windows, OSX or Linux.

Once you have that installed, download the project files and run the [dotnet build](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-build) in the project folder.  This will generate an executable file that will be located under the `bin` subfolder.

## Usage

The parameters for the application are the password for the user account and optionally the hostname or IP address for the EnvisaLink module.

`Usage: ConsoleKeypad <pwd> [host]`

Once connected, it will display a single line with alphanumeric messages from the alarm panel along with status indicators.

```console
Connected.  Enter a number, *, # or q to quit.
DISARMED BYPASS  |   Ready to Arm                         AC CHIME READY
```

From there you can peform any action, like arming/disarming, that you would from a physical keypad.  Just enter key presses as you would normally and wait for the response.

Effectively it acts as a virtual keypad for the alarm panel, meaning you can perform any actions including [panel programming](https://advancedsecurityllc.com/wp-content/uploads/v15pand20pprogrammingguide.pdf).

![Fixed English Keypad](https://digitalassets.resideo.com/damroot/RDEDesktop/10006/6148-c1-6.jpg)

## Credits

A simple task-based event-driven Telnet client
https://github.com/Spksh/TentacleSoftware.Telnet/

THIS SOFTWARE IS PROVIDED AS IS, NO WARRANTY, NO LIABILITY. NEITHER AUTHOR NOR ANYONE ELSE ARE RESPONSIBLE FOR ANY DAMAGE THAT COULD BE CAUSED BY THIS SOFTWARE. USE AT YOUR OWN RISK.