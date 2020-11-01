﻿using MSFTHelpers;
using GallonHelpers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using System.Threading;
using Microsoft.VisualBasic.CompilerServices;

namespace AudioBleLedsController
{
    class Program
    {
        static BluetoothLEDevice bluetoothLeDevice = null;

        static void Main(string[] args)
        {
            Run(args).Wait();
        }

        static async Task Run(string[] args)
        {
            Console.WriteLine("Connection");
            Console.WriteLine("-");

            #region connection

            String deviceId;

            if (args.Length == 0)
            {
                deviceId = "be:89:d0:01:7b:9c";
                Utility.Log("No arguments given, will use the id " + deviceId, LogType.WARNING);
            } 
            else
            {
                deviceId = args[0];
            }

            // BluetoothLE#BluetoothLE00:15:83:ed:e4:12-be:89:d0:01:7b:9c
            deviceId = "BluetoothLE#BluetoothLE00:15:83:ed:e4:12-" + deviceId;

            Utility.Log("Looking for BLE device of id " + deviceId, LogType.PENDING);
            bluetoothLeDevice = await BluetoothLEDevice.FromIdAsync(deviceId);
            if (bluetoothLeDevice == null)
            {
                Utility.Log("Failed to connect to device", LogType.ERROR);
            }

            #endregion

            Console.WriteLine("");
            Console.WriteLine("Services");
            Console.WriteLine("-");

            #region services

            GattDeviceService targettedService = null;

            if (bluetoothLeDevice != null)
            {
                Utility.Log("Looking for services", LogType.PENDING);
                GattDeviceServicesResult result = await bluetoothLeDevice.GetGattServicesAsync(BluetoothCacheMode.Uncached);

                if (result.Status == GattCommunicationStatus.Success)
                {
                    var services = result.Services;
                    Utility.Log(String.Format("Found {0} services", services.Count), LogType.OK);
                    foreach (var service in services)
                    {
                        Utility.Log(String.Format("Service: {0}", DisplayHelpers.GetServiceName(service)), LogType.OK);
                    }

                    // TODO: select service

                    targettedService = services[1];
                }
                else
                {
                    Utility.Log("Device unreachable", LogType.ERROR);
                }
            }

            #endregion

            Console.WriteLine("");
            Console.WriteLine("Caracteristics");
            Console.WriteLine("-");

            #region caracteristics

            GattCharacteristic characteristic = null;

            if (targettedService != null)
            {
                IReadOnlyList<GattCharacteristic> characteristics = null;

                try
                {
                    // Ensure we have access to the device.
                    var accessStatus = await targettedService.RequestAccessAsync();
                    if (accessStatus == DeviceAccessStatus.Allowed)
                    {
                        // BT_Code: Get all the child characteristics of a service. Use the cache mode to specify uncached characterstics only 
                        // and the new Async functions to get the characteristics of unpaired devices as well. 
                        var result = await targettedService.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                        if (result.Status == GattCommunicationStatus.Success)
                        {
                            characteristics = result.Characteristics;

                            foreach (GattCharacteristic c in characteristics)
                            {
                                Utility.Log(DisplayHelpers.GetCharacteristicName(c), LogType.OK);
                            }

                            // TODO: select

                            characteristic = characteristics[0];
                        }
                        else
                        {
                            Utility.Log("Error accessing service.", LogType.ERROR);
                        }
                    }
                    else
                    {
                        // Not granted access
                        Utility.Log("Error accessing service.", LogType.ERROR);
                    }
                }
                catch (Exception ex)
                {
                    Utility.Log("Restricted service. Can't read characteristics: " + ex.Message, LogType.ERROR);
                }
            }

            #endregion

            Console.WriteLine("");
            Console.WriteLine("Communication");
            Console.WriteLine("-");

            #region communication

            if (characteristic != null)
            {
                GattCharacteristicProperties properties = characteristic.CharacteristicProperties;

                if (
                    properties.HasFlag(GattCharacteristicProperties.Write) || 
                    properties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse)
                    )
                {
                    Utility.Log("Correct properties!", LogType.OK);

                    SoundListener soundListener = new SoundListener();

                    var i = 0;
                    while (i < 1000)
                    {
                        float soundLevel = soundListener.GetSoundLevel(); // Between 0.0f and 1.0f

                        // Format: 7e 00 01 brightness 00 00 00 00 ef
                        // brightness: 0x00-0x64 (0-100)
                        // So we need to convert the soundLevel to hex so that 1.0f is 0x64 and 0.0f is 0x00
                        // First we multiply it by 100, round it, and then convert it to hex
                        String brightness = ((int) (soundLevel * 100f)).ToString("X");
                        String textToWrite = "7e0001" + brightness + "00000000ef";
                        Utility.WriteHex(textToWrite, characteristic); // result ignored yes, we don't want for it to be blocking

                        Thread.Sleep(50);
                        i++;
                    }

                    soundListener.Dispose();
                }
                else
                {
                    Utility.Log("These properties don't have 'Write' or 'WriteWithoutResponse'", LogType.ERROR);
                }
            }

            #endregion

            Console.WriteLine("");
            Console.WriteLine("Cleanup");
            Console.WriteLine("-");

            #region cleanup

            Utility.Log("Exiting properly", LogType.PENDING);
            bluetoothLeDevice?.Dispose();
            Utility.Log("Done. Type a key to exit", LogType.OK);
            Console.ReadKey(true);

            #endregion
        }
    }
}
