# Tobii Pro Glasses 3 SDK for .net ![example workflow](https://github.com/tobiipro/G3SDK.net/actions/workflows/build.yml/badge.svg)

## License
The source code for this SDK and its examples is licensed under a permissive MIT license, but the 
[Tobii Software Development License Agreement](SDLA/licenseagreement.html) is still in effect when 
it comes to developing software for the Glasses3 API.

## Installation 

The easiest way to get started is to use the NuGet package for G3SDK.net

You can install using NuGet like this:

```cmd
nuget install Tobii.Glasses3.SDK.net
```

Or select it from the NuGet packages UI on Visual Studio.

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
    g3.Recorder.Stop();
}
```
## Limitations

The SDK does not support streaming data via WebRTC or RTSP on its own, you will need exteral libraries for that. 
[RtspClientSharp](https://github.com/BogdanovKirill/RtspClientSharp) is a good start if you want to use video streaming over RTSP.

## Samples
### LSL Connector for Glasses 3
This is a complete sample that will expose Glasses3 data streams to as Lab Streaming Layer data streams. To run the sample, just build and start it. It will automatically locate any available Glasses 3 device and register a stream outlet for the gaze stream from the unit.

### Document extractor for Glasses 3
This example shows how to access metadata for the API including the documentation that is used to build the API browser in the WebUI for Glasses 3. It will extract the documentation and save it as a json-file. There is also a very simple viewer for such json-files that can show two json-files side by side and highlight the changes. 
Start the webserver in examples\G3DocumentExtractor\miniweb\miniweb.exe and point your web browser to http://localhost:8000
included in the repo is the json files for the firmware versions that have been publicly released.