<p align="center"><img src="images/logo/horizontal.png" alt="Chafu" height="150px"></p>

# Chafu

## Continuous Integration

| Build Server | Platform | Build Status |
|--------------|----------|--------------|
| AppVeyor     | Windows  | [![AppVeyor Build status](https://ci.appveyor.com/api/projects/status/k4nhuf35dnwr42av?svg=true)](https://ci.appveyor.com/project/Cheesebaron/chafu) |
| Bitrise      | macOS    | ![Bitrise](https://www.bitrise.io/app/f0120017f7ea5f31.svg?token=_ib-YfmU9rlOoJ700ET43g) |
| VSTS         | Windows  | [![Visual Studio Team services](https://img.shields.io/vso/build/osteost/c29638c3-fef2-4228-b760-97c3d9496e88/2.svg)](https://osteost.visualstudio.com/_apis/public/build/definitions/c29638c3-fef2-4228-b760-97c3d9496e88/2/badge) |
| MyGet        | Windows  | [![chafu MyGet Build Status](https://www.myget.org/BuildSource/Badge/chafu?identifier=46b6aebc-6608-4fe4-8c90-776f121a61de)](https://www.myget.org/) |

## Packages
[![NuGet](https://img.shields.io/nuget/v/Chafu.svg?maxAge=2592000)](https://www.nuget.org/packages/Chafu/)

## Description
Chafu is a photo browser and camera library for Xamarin.iOS. It is heavily inspired from [Fusuma][1], which is a Swift library written by [ytakzk][2].

It has been tweaked for ease of use in a C# environment, all xibs converted to C# code and unnecessary wrapper views have been removed. The library
has been simplified and loads of unfixed Fusuma bugs and features have been fixed in this library.

## Preview
<img src="https://raw.githubusercontent.com/Cheesebaron/Chafu/master/images/sample.gif" width="340px">

## Features

- [x] UIImagePickerController alternative
- [x] Camera roll (images and video)
- [x] Album view to show images and video from folder
- [x] Camera for capturing both photos and video
- [x] Cropping of photos into squares
- [x] Toggling of flash when capturing photos and video
- [x] Supports front and back cameras
- [x] Face detection
- [x] Customizable

## Installation

Install from NuGet

`Install-Package Chafu`

> Note: 
> iOS 10 requires the developer to provide usage descriptions in the `info.plist`. Otherwise, the application will crash, when requesting permissions to camera, photo library or microphone.

```
<key>NSPhotoLibraryUsageDescription</key>
<string>Describe what photo library is used for</string>
<key>NSCameraUsageDescription</key>
<string>Describe what camera is used for</string>
<key>NSMicrophoneUsageDescription</key>
<string>Describe what microphone is used for</string>
```

## Usage

Add a `using chafu;` in the top of your class.

```
var chafu = new ChafuViewController();
chafu.HasVideo = true; // add video tab
chafu.MediaTypes = MediaType.Image | MediaType.Video // customize what to show
PresentViewController(chafu, true);
```

or if you only want to show gallery:

```
var gallery = new AlbumViewController();
gallery.MediaTypes = MediaType.Image | MediaType.Video // customize what to show
PresentViewController(gallery, true);
```

Both accept custom data source which will be instantiated lazily:

```
var chafu = new AlbumViewController {
    LazyDataSource = (view, size, mediaTypes) => 
        new LocalFilesDataSource(view, size, mediaTypes) {ImagesPath = path},
    LazyDelegate = (view, source) => new LocalFilesDelegate(view, (LocalFilesDataSource) source),
};
```

where `path` is a directory the App has access to.

## Events

```
// When image is selected or captured with camera
chafu.ImageSelected += (sender, image) => imageView.Image = image;

// When video is captured with camera
chafu.VideoSelected += (sender, videoUrl) => urlLabel.Text = videoUrl.AbsoluteString;

// When ViewController is dismissed
chafu.Closed += (sender, e) => { /* do stuff on closed */ };

// When permissions to access camera roll are denied by the user
chafu.CameraRollUnauthorized += (s, e) => { /* do stuff when Camera Roll is unauthorized */ };

// when permissions to access camera are denied by the user
chafu.CameraUnauthorized += (s, e) => { /* do stuff when Camera is unauthorized */ };
```

## Customization

All customization happens through the static `Configuration` class.

```
Configuration.BaseTintColor = UIColor.White;
Configuration.TintColor = UIColor.Red;
Configuration.BackgroundColor = UIColor.Cyan;
Configuration.CropImage = false;
Configuration.TintIcons = true;
// etc...
```

Explore the class for more configuration.

## Thanks to
Many thanks to [ytakzk][2] for his initial [Fusuma][1] implementation, which this library started as.

## What does Chafu mean?
Fusuma means bran in Japanese, Chafu in Japanese means chaff. Chaff is sometimes confused with bran.

## License
Chafu is licensed under the MIT License, see the LICENSE file for more information.

[1]: https://github.com/ytakzk/Fusuma
[2]: https://github.com/ytakzk
