# Migration information

This repository has been migrated to the new Tobii organization on github: https://github.com/tobii/tobii-glasses3-sdk
This copy will be archived.

# Tobii Pro Glasses 3 SDK for .net [![Build](https://github.com/tobiipro/Tobii.Glasses3.SDK/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/tobiipro/Tobii.Glasses3.SDK/actions/workflows/build.yml)

## License
The source code for this SDK and its examples is licensed under the 
[Tobii Pro Software Development License Agreement](https://github.com/tobiipro/Tobii.Glasses3.SDK/tree/main/SDLA).

## Installation 

The easiest way to get started is to use the NuGet package for the Glasses3 SDK

You can install using the dotnet CLI like this:

```cmd
dotnet add package Tobii.Glasses3.SDK
```

Or select it from the NuGet packages UI in Visual Studio.

On Visual Studio, make sure that you are targeting atleast either .netframework 4.7.2 or .net5, as this package uses some features of newer .NETs.  

Alternatively, you can [download it](https://nuget.org/packages/Tobii.Glasses3.SDK) directly.

## Using the Glasses 3 SDK
Here is an example of how to make a calibration and a 10 second recording with the first available pair of glasses:

```csharp
var browser = new G3Browser();
       
// grab the first available glasses
var g3 = (await browser.ProbeForDevices()).First();

// try to calibrate, if it succeeds, proceed with the recording
if (await g3.Calibrate.Run())
{
    await g3.Recorder.Start();
    await Task.Delay(TimeSpan.FromSeconds(10));
    await g3.Recorder.Stop();
}
```

## Documentation

There is a [Glasses 3 Developers Guide](https://go.tobii.com/tobii-pro-glasses-3-developer-guide) available from the Tobii website. Even though the SDK handles most of the communication with the glasses, it is still recommended to read the developers guide to get a better understanding of how the API works.
For more information on individual methods in the SDK, please refer to the API documentation in the WebUI on your glasses (http://<serialnumber>.local/browse.html).

## Limitations

The SDK does not handle streaming data via WebRTC or RTSP on its own, you will need exteral libraries for that. 
[FFMediaElement](https://github.com/unosquare/ffmediaelement) is a good start if you want to displayt streaming video over RTSP. It also supports the data streams provided by Glasses3. [RtspClientSharp](https://github.com/BogdanovKirill/RtspClientSharp) is another low level alterntive, but this package will only handle the transport layer, not the decoding, and it does not seem to support data channels.

## Samples
### Glasses Demo
Demo that shows 
* How to find glasses
* Show live view (using RTSP via the FFME.Windows package) with synchronized local gaze overlay
* Start/stop recordings
* Calibrate glasses
* Change settings
* List recordings and read recording meta data
* Delete recordings
* Replay recording (using HTTP) with synchronized local gaze overlay
* Take and show snapshots/thumbnails

### LSL Connector for Glasses 3
This is a complete sample that will expose Glasses3 data streams to as Lab Streaming Layer data streams. To run the sample, just build and start it. It will automatically locate any available Glasses 3 device and register a stream outlet for the gaze stream from the unit. You can also download precompiled binaries (for windows) from the [releases section](https://github.com/tobiipro/Tobii.Glasses3.SDK/releases).

### Document extractor for Glasses 3
This example shows how to access metadata for the API including the documentation that is used to build the API browser in the WebUI for Glasses 3. It will extract the documentation and save it as a json-file. There is also a very simple viewer for such json-files that can show two json-files side by side and highlight the changes. 

Start the webserver in examples\G3DocumentExtractor\miniweb\miniweb.exe and point your web browser to http://localhost:8000 included in the repo is the json files for the firmware versions that have been publicly released.

### G3 To Screen Mapper
This is a rather advanced demo that shows how to receive and display RTSP video using OpenCV. it also performs some image processing of the video to find and position a computer screen in the video and map the gaze data to the coordinate system of this screen.

To do this, the screen is decorated with ArUco markers along the edges. The application uses OpenCV to receive live video frames from the camera of the glasses, each frame is searched for existing ArUco markers (again using OpenCV). If enough markers are found, the position of the screen in the video frame is determined, and then gaze data is transformed from frame to the screen. The original video frame is warped to a new image using the same transform so that the screen is mapped to the center of image. Both the warped image and the original video frame is displayed in a window.
