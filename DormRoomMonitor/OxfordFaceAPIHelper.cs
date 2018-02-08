﻿using DormRoomMonitor.FacialRecognition;
using Microsoft.ProjectOxford.Face.Contract;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Storage;

namespace DormRoomMonitor
{
    /// <summary>
    /// Allows easy access to Oxford functions such as adding a visitor to whitelist and checking to see if a visitor is on the whitelist
    /// </summary>
    static class OxfordFaceAPIHelper
    {
        /// <summary>
        /// Initializes Oxford API. Builds existing whitelist or creates one if one does not exist.
        /// </summary>
        public async static Task<bool> InitializeOxford()
        {
            // Attempts to open whitelist folder, or creates one
            StorageFolder whitelistFolder = await KnownFolders.PicturesLibrary.CreateFolderAsync(GeneralConstants.WhiteListFolderName, CreationCollisionOption.OpenIfExists);

            // Creates a new instance of the Oxford API Controller
            FaceApiRecognizer sdkController = FaceApiRecognizer.Instance;

            // Attempts to open whitelist ID file, or creates one
            StorageFile WhiteListIdFile = await whitelistFolder.CreateFileAsync("WhiteListIdNew.txt", CreationCollisionOption.OpenIfExists);

            // Reads whitelist file to get whitelist ID and stores value
            string savedWhitelistId = await FileIO.ReadTextAsync(WhiteListIdFile);

            // If the ID has not been created, creates a whitelist ID
            if (savedWhitelistId == "")
            {
                string id = Guid.NewGuid().ToString();
                await FileIO.WriteTextAsync(WhiteListIdFile, id);
                savedWhitelistId = id;
            }

            // Builds whitelist from exisiting whitelist folder
            await sdkController.CreateWhitelistFromFolderAsync(savedWhitelistId, whitelistFolder, null);

            // Return true to indicate that Oxford was initialized successfully
            return true;
        }

        /// <summary>
        /// Accepts a user name and the folder in which their identifying photos are stored. Adds them to the whitelist.
        /// </summary>
        /*public async static void AddUserToWhitelist(string name, StorageFolder photoFolder)
        {
            try
            {
                // Acquires instance of Oxford SDK controller
                FaceApiRecognizer sdkController = FaceApiRecognizer.Instance;
                // Asynchronously adds user to whitelist
                await sdkController.AddPersonToWhitelistAsync(photoFolder, name);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to add user to whitelist. " + ex.Message);
            }

        }*/

        /// <summary>
        /// Accepts an image file and the name of a visitor. Associates photo with exisiting visitor.
        /// </summary>
        /*public async static void AddImageToWhitelist(StorageFile imageFile, string name)
        {
            try
            {
                // Acquires instance of Oxford SDK controller
                FaceApiRecognizer sdkController = FaceApiRecognizer.Instance;
                // Asynchronously adds image to whitelist
                await sdkController.AddImageToWhitelistAsync(imageFile, name);
            }
            catch
            {
                Debug.WriteLine("Failed to add image.");
            }
        }*/

        /// <summary>
        /// Accepts the name of a visitor. Removes them from whitelist.
        /// </summary>
        public async static void RemoveUserFromWhitelist(string name)
        {
            // Acquires instance of Oxford SDK controller
            FaceApiRecognizer sdkController = FaceApiRecognizer.Instance;
            // Asynchronously remove user from whitelist
            await sdkController.RemovePersonFromWhitelistAsync(name);
        }

        /// <summary>
        /// Checks to see if a whitelisted visitor is in passed through image. Returns list of whitelisted visitors. If no authorized users are detected, returns an empty list.
        /// </summary>
        public async static Task<IdentifyResult[]> IsFaceInWhitelist(StorageFile image)
        {
            try
            {
                return await FaceApiRecognizer.Instance.FaceRecognizeAsync(image);
            }
            catch (FaceRecognitionException fe)
            {
                switch (fe.ExceptionType)
                {
                    // Fails and catches as a FaceRecognitionException if no face is detected in the image
                    case FaceRecognitionExceptionType.NoFaceDetected:
                        Debug.WriteLine("WARNING: No face detected in this image.");
                        var log = new Log { message = "WARNING: No face detected in this image.", image = image };
                        GuildWebApi.updateLog(log);
                        break;
                }
            }
            catch (Exception ex)
            {
                // General error. This can happen if there are no visitors authorized in the whitelist
                Debug.WriteLine("WARNING: Oxford just threw a general expception.");
                GuildWebApi.updateLog(new Log { message = "WARNING: Oxford just threw a general expception.", image = image });
            }
            return null;
        }
    }
}

