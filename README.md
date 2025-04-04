# Control3 - Laptop as KVM

<sup>**Note:** This repository is not maintained, it is for inspirational purposes only.
Most issues and discussion here are referring to the Microsoft App store version (https://apps.microsoft.com/detail/9pmw2phrdf46) which is a separate closed development.
My apologies for the confusion.</sup>

Demo: https://youtu.be/ZCbLUDXHe6E

Test with VGA adapter:  https://youtu.be/rz65hHhDxzw

Use your laptop as Screen/Mouse/Keyboard to manage your external device like a RPi, (mini)PC, PlayStation, nVidia Shield, etc.

I do not want that extra keyboard mouse and monitor when I do a quick configuration. There are tools, but I want direct BIOS access and no driver install on the remote device. There are KVM Console Crash Cart solutions, but you need a good budget. There are low budget products which can be used, but there was no tool to use them for this purpose.

With Control3 you can use any MS2130 based video capture cable, they are blazing fast even over USB2.0, and supports Audio as well! 
For remote Mouse/Keyboard the CH9329 USB module in combination with a CH340 USB Serial module will do the job.
Prefab cables of this combination are available (eBay: CH9329/CH340 KVM USB Cable, Ali: ch9329+ch340uart) or solder it yourself.

I'm not a professional dev, but gave it a shot. Built a C# .NET 6 application to support the cables for remote control on my devices.

Used the lastest Windows App SDK (WinUI 3) they support MediaPlayerElement with GUI support based on WinRT API's.

Catch the keyboard/mouse events with the the MouseKeyHook library (https://github.com/gmamaladze/globalmousekeyhook)

Catch the low-level mouse movements with the SharpDX.DirectInput library (https://github.com/sharpdx/SharpDX)

Used the CH9329 Class from SmallCodeNote to send the mouse/keyboard data to the remote device (https://github.com/SmallCodeNote/CH9329-109KeyClass)

CH9329 documentation translated to English. 
https://github.com/user-attachments/files/15741885/CH9329-CN.translated.PDF

Some doc I created for AZERTY/QWERTZ Mapping:
https://github.com/sipper69/Control3/blob/master/keycodes.xlsx

All under MIT license, many thanks to these contributors, of course my contribution is open source as well.


This is only a preview of what is possible, the tool is very usable for me already, but so many enhancements to think of.
I do not deliver support, but like to discuss around it on Reddit (https://www.reddit.com/r/homelab/comments/1b6io6v/laptop_as_kvm/).
This tool only support US keyboards btw. It runs on Windows 10/11 build 1809 and higher.

I added an unsigned x64 binary, as packaged installer (SetupControl3.exe) if you want to play around directly. It will only recognize the product type of the cable I use (CH340), but you can amend the PID/VID in the code to support your cable. 
Don't forget to install a CH340 driver on your host PC if needed (check if the COM port is there under your devices).
https://www.wch-ic.com/downloads/CH9340EVT_ZIP.html

I hope it is of use to you!

<img width="960" alt="Control3" src="https://github.com/sipper69/Control3/assets/115348579/259b56ab-6749-4c0b-807a-88246b2f0f9e">
<img width="960" alt="Cables3" src="https://github.com/sipper69/Control3/assets/115348579/73345112-29eb-483e-a5fb-38a8e8ed7c19">

