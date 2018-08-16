# IoTPInvoke
Demonstration of using P/Invoke from C# to call the Azure IoT SDK C functions (Windows Only)

This is a C# wrapper for the [Azure IoT C SDK](https://github.com/azure/azure-iot-sdk-c). It uses P/Invoke to call the functions in the C SDK. It is by no means a complete implementation of all the functionality offered by the C SDK but it offers the option to use C# for interaction with the IoT hub where that might otherwise be impossible.

It is recommended that you use the [C# SDK](https://github.com/azure/azure-iot-sdk-csharp) if possible. This should only be used when you are unable to do so and do not wish to switch to an alternative language.

## Why I Created this Wrapper

If you intend to create an application in C# that will communicate with the Azure IoT hub via AMQP over WebSockets or MQTT over WebSockets and wish to run the code on Windows 7 you will find that this will not work. The .NET Desktop assembly that is used to implement web sockets depends upon a binary dll that is not shipped with Windows 7. However, the WebSocket support in the C SDK does not have any dependancies thus one can use that on Windows 7. In order to stay with C# this wrapper was created.

## Building the C SDK

This repository contains two dlls that are created when the C SDK is built. These are aziotsharedutil.dll and iothub_client_dll.dll. I do not intend constantly rebuilding and updating these dlls in the repository. If you need a later version of the C SDK then you will need to build these two dlls for yourself. 

In order to do that you will need to perform the following steps:
1. Clone the C SDK with the command ```git clone --recursive https://github.com/azure/azure-iot-sdk-c.git```
2. Switch to the new directory ```cd azure-iot-sdk-c```
3. Create a cmake directory ```mkdir cmake```
4. Switch to that directory ```cd cmake```
5. Run cmake as follows: ```cmake -Dbuild_as_dynamic=yes ..```
6. Open the new project in Visual Studio ```start ALL_BUILD.vcxproj```
7. Build the project

The required dlls will be found at 
* .\c-utility\Debug\aziotsharedutil.dll
* .\iothub_client\Debug\iothub_client_dll.dll

Replace debug with release if you are building the release versions.
