﻿using NAudio.Wave;
using System;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Media.Devices;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;

namespace GallonHelpers
{
    public static class Utility
    {
        #region error codes
        readonly static int E_BLUETOOTH_ATT_WRITE_NOT_PERMITTED = unchecked((int)0x80650003);
        readonly static int E_BLUETOOTH_ATT_INVALID_PDU = unchecked((int)0x80650004);
        readonly static int E_ACCESSDENIED = unchecked((int)0x80070005);
        #endregion

        /// <summary>
        /// Created by Lilian Gallon, 11/01/2020
        /// 
        /// Simple Log function that adds a prefix according to the log type
        /// </summary>
        /// <param name="msg">The message to log</param>
        /// <param name="logLevel">The type of the message</param>

        public static void Log(String msg, LogType logType)
        {
            String prefix = "";
            switch (logType)
            {
                case LogType.OK:
                    prefix = "+";
                    break;
                case LogType.PENDING:
                    prefix = "~";
                    break;
                case LogType.WARNING:
                    prefix = "!";
                    break;
                case LogType.ERROR:
                    prefix = "-";
                    break;
            }

            Console.WriteLine("[" + prefix + "] " + msg);
        }

        /// <summary>
        /// Created by Lilian Gallon, 11/01/2020
        /// 
        /// It writes the given hexadecimal value to the given gatt charateristic
        /// 
        /// </summary>
        /// <param name="hex">The hex message to write</param>
        /// <param name="characteristic">The characteristic to override</param>
        /// <returns></returns>
        public async static Task<bool> WriteHex(String hex, GattCharacteristic characteristic)
        {
            return await WriteBufferToSelectedCharacteristicAsync(CryptographicBuffer.DecodeFromHexString(hex), characteristic);
        }

        /// <summary>
        /// Created by Lilian Gallon, 11/01/2020
        /// 
        /// It writes the given buffer to the given gatt charateristic
        /// 
        /// </summary>
        /// <param name="buffer">The hex message to write</param>
        /// <param name="characteristic">The characteristic to override</param>
        /// <returns></returns>
        private async static Task<bool> WriteBufferToSelectedCharacteristicAsync(IBuffer buffer, GattCharacteristic characteristic)
        {
            try
            {
                var result = await characteristic.WriteValueWithResultAsync(buffer);

                if (result.Status == GattCommunicationStatus.Success)
                {
                    return true;
                }
                else
                {
                    Log("Write failed: " + result.Status, LogType.WARNING);
                    return false;
                }
            }
            catch (Exception ex) when (ex.HResult == E_BLUETOOTH_ATT_INVALID_PDU)
            {
                Log("Write failed: " + ex.Message, LogType.ERROR);
                return false;
            }
            catch (Exception ex) when (ex.HResult == E_BLUETOOTH_ATT_WRITE_NOT_PERMITTED || ex.HResult == E_ACCESSDENIED)
            {
                // This usually happens when a device reports that it support writing, but it actually doesn't.
                Log("Write failed: " + ex.Message, LogType.ERROR);
                return false;
            }
        }
    }

    public enum LogType
    {
        ERROR, WARNING, OK, PENDING
    }

    /// <summary>
    /// Created by Lilian Gallon, 11/01/2020
    /// 
    /// Used to get the sound level of the operating system.
    /// It gets updated automatically to ALWAYS listen to the default
    /// output device.
    /// </summary>
    public class SoundListener
    {
        private WasapiLoopbackCapture capture;
        private float soundLevel = 0;

        /// <summary>
        /// Created by Lilian Gallon, 11/01/2020
        /// 
        /// It initializes everything. You can call GetSoundLevel() right after
        /// instanciating this class.
        /// </summary>
        public SoundListener()
        {
            InitCapture();

            MediaDevice.DefaultAudioRenderDeviceChanged += (sender, newDevice) =>
            {
                Dispose();
                InitCapture();
            };
        }

        ~SoundListener()
        {
            Dispose();
        }

        /// <summary>
        /// Inits the capture:
        /// - inits the capture to listen to the current default output device
        /// - creates the listener
        /// - starts recording
        /// </summary>
        private void InitCapture()
        {
            // Takes the current default output device
            capture = new WasapiLoopbackCapture();

            capture.DataAvailable += (object sender, WaveInEventArgs args) =>
            {
                // Interprets the sample as 32 bit floating point audio (-> FloatBuffer)
                soundLevel = 0;
                var buffer = new WaveBuffer(args.Buffer);

                for (int index = 0; index < args.BytesRecorded / 4; index++)
                {
                    var sample = buffer.FloatBuffer[index];
                    if (sample < 0) sample = -sample; // abs
                    if (sample > soundLevel) soundLevel = sample;
                }
            };

            capture.StartRecording();
        }

        /// <summary>
        /// Created by Lilian Gallon, 11/01/2020
        /// 
        /// </summary>
        /// <returns>The current system sound level between 0.0f and 1.0f</returns>
        public float GetSoundLevel()
        {
            return soundLevel;
        }

        /// <summary>
        /// Clears the allocated resources
        /// </summary>
        public void Dispose()
        {
            capture?.StopRecording();
            capture?.Dispose();
        }
    }
}
