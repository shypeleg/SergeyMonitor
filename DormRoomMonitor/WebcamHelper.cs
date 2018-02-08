using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using System.Collections.Generic;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;

namespace DormRoomMonitor
{
    /// <summary>
    /// Interacts with an attached camera. Allows one to easily access live webcam feed and capture a photo.
    /// </summary>
    public class CapturedPhotos
    {
        public StorageFile arriving;
        public StorageFile departing;
    }
    public class WebcamHelper
    {
        public MediaCapture arrivingMediaCapture;
        public MediaCapture departingMediaCapture;

        private bool initialized = true;

        /// <summary>
        /// Asynchronously initializes webcam feed
        /// </summary>
        public async Task InitializeCameraAsync()
        {
            if (arrivingMediaCapture == null || departingMediaCapture == null)
            {
                // Attempt to get attached webcam
                var cameraDevices = await FindCameraDevices();

                if (cameraDevices == null)
                {
                    // No camera found, report the error and break out of initialization
                    Debug.WriteLine("No camera found!");
                    var log = new Log { message = "No camera found!"};
                    GuildWebApi.updateLog(log);
                    initialized = false;
                    return;
                }
                if (cameraDevices.Count < 2)
                {
                    Debug.WriteLine("We need two cameras to work properly");
                    var log = new Log { message = "We need two cameras to work properly" };
                    GuildWebApi.updateLog(log);
                    //        initialized = false;
                    //        return;
                }

                // Creates MediaCapture initialization settings with foudnd webcam devices
                
                arrivingMediaCapture = new MediaCapture();
     //           departingMediaCapture = new MediaCapture();
                await arrivingMediaCapture.InitializeAsync(new MediaCaptureInitializationSettings { VideoDeviceId = cameraDevices[0].Id });
       //         await departingMediaCapture.InitializeAsync(new MediaCaptureInitializationSettings { VideoDeviceId = cameraDevices[1].Id });


                initialized = true;
            }
        }

        /// <summary>
        /// Asynchronously looks for and returns first camera device found.
        /// If no device is found, return null
        /// </summary>
        private static async Task<DeviceInformationCollection> FindCameraDevices()
        {
            // Get available devices for capturing pictures
            var allVideoDevices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);


            if (allVideoDevices.Count > 0)
            {
                // If there is a device attached, return the first device found
                return allVideoDevices;
            }
            else
            {
                // Else, return null
                return null;
            }
        }

        /// <summary>
        /// Asynchronously begins live webcam feed
        /// </summary>
        public async Task StartCameraPreview()
        {
            try
            {
                await arrivingMediaCapture.StartPreviewAsync();
            }
            catch
            {
                initialized = false;
                Debug.WriteLine("Failed to start camera preview stream");
                var log = new Log { message = "Failed to start camera preview stream" };
                GuildWebApi.updateLog(log);

            }
        }

        /// <summary>
        /// Asynchronously ends live webcam feed
        /// </summary>
        public async Task StopCameraPreview()
        {
            try
            {
                await arrivingMediaCapture.StopPreviewAsync();
            }
            catch
            {
                Debug.WriteLine("Failed to stop camera preview stream");
                var log = new Log { message = "Failed to stop camera preview stream" };
                GuildWebApi.updateLog(log);
            }
        }

        public async Task<CapturedPhotos> InitializeAndCapturePhotos()
        {
            var cameraDevices = await FindCameraDevices();

            if (cameraDevices == null)
            {
                // No camera found, report the error and break out of initialization
                Debug.WriteLine("No camera found!");
                var log = new Log { message = "No camera found!" };
                GuildWebApi.updateLog(log);
                initialized = false;
                return null;
            }
            if (cameraDevices.Count < 2)
            {
                Debug.WriteLine("We need two cameras to work properly");
                var log = new Log { message = "We need two cameras to work properly" };
                GuildWebApi.updateLog(log);
                initialized = false;
                return null;
            }

            var arrivingPhoto = await initAndCapturePhoto(cameraDevices[0].Id);
            var departingPhoto = await initAndCapturePhoto(cameraDevices[1].Id);
            return new CapturedPhotos { arriving = arrivingPhoto, departing = departingPhoto };

        }
        private async Task<StorageFile> initAndCapturePhoto(string cameraId)
        {
            // Creates MediaCapture initialization settings with foudnd webcam devices
            var camera  = new MediaCapture();
            await camera.InitializeAsync(new MediaCaptureInitializationSettings { VideoDeviceId = cameraId });
            var file = await creatFile();
            await camera.CapturePhotoToStorageFileAsync(ImageEncodingProperties.CreateJpeg(), file);
            return file;
        }
        /// <summary>
        /// Asynchronously captures photo from camera feed and stores it in local storage. Returns image file as a StorageFile.
        /// File is stored in a temporary folder and could be deleted by the system at any time.
        /// </summary>
        public async Task<StorageFile[]> CapturePhotos()
        {

            var files =  await Task.WhenAll(creatFile(), creatFile());
            // Captures and stores new Jpeg image file
            await arrivingMediaCapture.CapturePhotoToStorageFileAsync(ImageEncodingProperties.CreateJpeg(), files[0]);
       //     await departingMediaCapture.CapturePhotoToStorageFileAsync(ImageEncodingProperties.CreateJpeg(), files[1]);
            //await Task.WhenAll(
            //    arrivingMediaCapture.CapturePhotoToStorageFileAsync(ImageEncodingProperties.CreateJpeg(), files[0]).AsTask(),
            //    departingMediaCapture.CapturePhotoToStorageFileAsync(ImageEncodingProperties.CreateJpeg(), files[1]).AsTask());

            // Return image file
            return files;
        }
        private async Task<StorageFile> creatFile()
        { // Create storage file in local app storage
            string fileName = GenerateNewFileName() + ".jpg";
            CreationCollisionOption collisionOption = CreationCollisionOption.GenerateUniqueName;
            return await ApplicationData.Current.TemporaryFolder.CreateFileAsync(fileName, collisionOption);
        }

        /// <summary>
        /// Generates unique file name based on current time and date. Returns value as string.
        /// </summary>
        private string GenerateNewFileName()
        {
            return DateTime.UtcNow.ToString("HH-mm-ss") + "_Dorm_Room_Monitor";
        }

        /// <summary>
        /// Returns true if webcam has been successfully initialized. Otherwise, returns false.
        /// </summary>
        public bool IsInitialized()
        {
            return initialized;
        }
    }
}

