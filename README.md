<p align="center">
  <img src="docs/icon.png" width="96" height="96" alt="">
</p>

<h1 align="center">Clothesline</h1>

<p align="center">
  Screenshots, hung out to dry. For Windows 10 and 11.
  <br>
  A native port of <a href="https://github.com/alejandrobujan/tendedero">Tendedero</a> for macOS.
</p>

<br>

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/snap-dark.png">
  <img src="docs/snap-light.png" alt="Three screenshots in glass frames hang from a thin sagging line at the top of the screen. The middle one shows a Copied badge.">
</picture>

<br>

## Out of sight. Within reach.

Every screenshot you take hangs on a line just above your screen.
Push the pointer against the top edge and it glides down. Move away and it's gone.

Each capture lifts off from where you took it and flies to the line. Cards swing when
they land, sway in an occasional breeze, and fall off the screen when you let them go.

<br>

## A gesture for everything.

| | |
|:--|:--|
| Click | Copy the image. Paste it anywhere, as a picture or as a file. |
| Press and hold | Open it in your image editor (Paint, unless you changed it). The line shows the edited version when you save. |
| Double click | Open it in Photos. |
| Drag into an app | Send a copy. It stays on the line. |
| Drag into a folder | Keep it there. It leaves the line. |
| Drag to the Recycle Bin, or click the cross | Let it go. |
| Right click | Copy, open, edit, show in Explorer, save, discard. |
| Push the pointer against the top edge | Bring the line down on that screen. If your taskbar is at the top, rest the pointer on it. |
| <kbd>Ctrl</kbd>&thinsp;<kbd>Alt</kbd>&thinsp;<kbd>T</kbd> | Show or hide the line. Clicking the tray icon does the same. |

<br>

## Every capture, not just the saved ones.

Clothesline watches `Pictures\Screenshots`, where <kbd>Win</kbd>&thinsp;<kbd>PrtScn</kbd> and the
Snipping Tool save their files. It also catches captures that only reach the clipboard:
<kbd>Win</kbd>&thinsp;<kbd>Shift</kbd>&thinsp;<kbd>S</kbd> with automatic saving turned off,
<kbd>PrtScn</kbd>, <kbd>Alt</kbd>&thinsp;<kbd>PrtScn</kbd>. Those are written to Clothesline's own
folder and hang like any other. Only a pure image is caught: copying a picture out of a web
page or a document, which brings text along, is left alone.

Caught captures are the only files Clothesline ever deletes. Discard one and it goes to the
Recycle Bin. Drag it to a folder, or choose *Save to Desktop*, to keep it. Screenshots from
`Pictures\Screenshots` or anywhere else stay where they are; taking one down only takes it
off the line.

Using ShareX, Greenshot or something else? Add its folder to `watchFolders` in the settings file.

<br>

## Private by design.

No account. No network. No analytics. No changes to your system settings.
Clothesline runs entirely on your PC, and your screenshots never leave it.

<br>

## Tech specs

| | |
|:--|:--|
| **Compatibility** | Windows 10 version 1809 or later, Windows 11. x64. Per‑monitor DPI aware, multi‑monitor, light and dark themes. |
| **Size** | Under 1 MB with the .NET 8 Desktop Runtime installed, or a 63 MB portable build that needs nothing. |
| **Built with** | C#, WPF and Win32. Rendered on the DWM, with real spring physics. |
| **Network access** | None |
| **Price** | Free |
| **License** | MIT |

<br>

## Install

Download `Clothesline.exe` from the latest release and run it. It lives in the notification
area; choose *Start with Windows* from its menu to keep it around. The small build needs the
[.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0); the portable build
does not.

<br>

## Build from source

```powershell
git clone <this repository>
cd clothesline
scripts\build.ps1            # dist\win-x64\Clothesline.exe, needs the .NET 8 Desktop Runtime
scripts\build.ps1 -Portable  # dist\portable\Clothesline.exe, self-contained
```

Requires the .NET 8 SDK. Visual Studio is optional.

<details>
<summary>Inside the app</summary>
<br>

| File | Role |
|:--|:--|
| `AppController.cs` | Tray icon, shortcut, revealing and tucking away the line |
| `UI/LineWindow.cs` | The transparent strip along the top of the screen |
| `UI/LineCanvas.cs` | The line and where each photo hangs, driven by one clock |
| `UI/PeggedControl.cs` | One photo: glass frame, clip, swing, breeze, click, hold, drag |
| `UI/FlightWindow.cs` | A capture flying to the line, and a card falling off it |
| `UI/Motion.cs` | Springs and tweens |
| `UI/Glass.cs` | The glass frame and the aluminium clip |
| `Core/Line.cs` | What is hanging, and what you can do with it |
| `Core/ScreenshotWatcher.cs` | Notices new screenshots in a folder |
| `Core/ClipboardWatcher.cs` | Catches captures that only reach the clipboard |
| `Core/CaptureRect.cs` | Guesses where a capture was taken, so it can fly from there |
| `Core/Sounds.cs` | Three sounds, synthesised at launch |
| `Interop/DragSource.cs` | Shell-style drag and drop with a drag image |
| `Interop/FullScreen.cs` | Knows when to stay hidden |

Settings and the log live in `%LOCALAPPDATA%\Clothesline`. The hot key, extra folders to
watch and the folder for caught captures can be changed in `settings.json`.

The icon and the images in this README are drawn in code: `scripts\make-icon.ps1`,
`scripts\make-samples.ps1`, and `Clothesline.exe --snapshot out.png image1 image2 …`.

</details>

<br>

## Thanks

Clothesline is a port of [Tendedero](https://github.com/alejandrobujan/tendedero) by Alejandro
Buján, whose design, motion and words it follows closely. The name and icon of Tendedero are
his; this project uses its own.
