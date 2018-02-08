using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.ProjectOxford.Face;
using System.Diagnostics;
using Windows.Storage;
using Microsoft.ProjectOxford.Face.Contract;
using System.Linq;

namespace DormRoomMonitor.FacialRecognition
{
    class FaceApiRecognizer : IFaceRecognizer
    {
        const int RATE_LIMIT = 100; // ms
        #region Private members
        private static readonly Lazy<FaceApiRecognizer> _recognizer = new Lazy<FaceApiRecognizer>(() => new FaceApiRecognizer());

        private FaceApiWhitelist _whitelist = null;
        private IFaceServiceClient _faceApiClient = null;
        private StorageFolder _whitelistFolder = null;
        #endregion

        #region Properties
        /// <summary>
        /// Face API Recognizer instance
        /// </summary>
        public static FaceApiRecognizer Instance
        {
            get
            {
                return _recognizer.Value;
            }
        }

        /// <summary>
        /// Whitelist Id on Cloud Face API
        /// </summary>
        public string WhitelistId
        {
            get;
            private set;
        }
        int numberOfCalls = 0;
        public IFaceServiceClient FaceApiClient
        {
            get
            {
                ++numberOfCalls;
                Debug.WriteLine("number of calls" + numberOfCalls);

                var log = new Log { message = "number of calls" + numberOfCalls };
                GuildWebApi.updateLog(log);
                return _faceApiClient;
            }
            set => _faceApiClient = value;
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Constructor
        /// Initial Face Api client
        /// </summary>
        private FaceApiRecognizer()
        {
            FaceApiClient = new FaceServiceClient(GeneralConstants.OxfordAPIKey, GeneralConstants.DEFAULT_API_ROOT);
        }
        #endregion

        #region Whitelist

        private void UpdateProgress(IProgress<int> progress, double progressCnt)
        {
            if (progress != null)
            {
                progress.Report((int)Math.Round(progressCnt));
            }
        }

        /// <summary>
        /// Train whitelist until training finished
        /// </summary>
        /// <returns></returns>
        private async Task<bool> TrainingWhitelistAsync()
        {
            bool isSuccess = true;

            // Train whitelist after add all person
            Debug.WriteLine("Start training whitelist...");
            var log = new Log { message = "Start training whitelist" };
            GuildWebApi.updateLog(log);
            await FaceApiClient.TrainPersonGroupAsync(WhitelistId);
            await Task.Delay(RATE_LIMIT);
            TrainingStatus status;

            while (true)
            {
                status = await FaceApiClient.GetPersonGroupTrainingStatusAsync(WhitelistId);
                await Task.Delay(RATE_LIMIT);
                // if still running, continue to check status
                if (status.Status == Status.Running)
                {
                    continue;
                }

                // if timeout or failed
                if (status.Status != Status.Succeeded)
                {
                    isSuccess = false;
                }
                break;
            }

            return isSuccess;
        }

        public async Task<bool> CreateWhitelistFromFolderAsync(string whitelistId, StorageFolder whitelistFolder = null, IProgress<int> progress = null)
        {
            bool isSuccess = true;
            double progressCnt = 0;

            WhitelistId = whitelistId;
            _whitelist = new FaceApiWhitelist(WhitelistId);

            try
            {
                // whitelist folder default to picture library
                if (whitelistFolder == null)
                {
                    whitelistFolder = await KnownFolders.PicturesLibrary.GetFolderAsync("WhiteList");
                }

                _whitelistFolder = whitelistFolder;

                // detele person group if already exists
                try
                {
                    // An exception is thrown if the person group doesn't exist
                    await FaceApiClient.GetPersonGroupAsync(whitelistId);
                    await Task.Delay(RATE_LIMIT);
                    UpdateProgress(progress, ++progressCnt);

                    await FaceApiClient.DeletePersonGroupAsync(whitelistId);
                    await Task.Delay(RATE_LIMIT);
                    UpdateProgress(progress, ++progressCnt);

                    Debug.WriteLine("Deleted old group");
                    var log = new Log { message = "Deleted old group" };
                    GuildWebApi.updateLog(log);
                }
                catch (FaceAPIException ce)
                {
                    // Group not found
                    if (ce.ErrorCode == "PersonGroupNotFound")
                    {
                        Debug.WriteLine("The group doesn't exist");
                        var log = new Log { message = "The group doesn't exist" };
                        GuildWebApi.updateLog(log);
                    }
                    else
                    {
                        throw ce;
                    }
                }

                await FaceApiClient.CreatePersonGroupAsync(WhitelistId, "White List");
                await Task.Delay(RATE_LIMIT);
                UpdateProgress(progress, ++progressCnt);

                await BuildWhiteListAsync(progress, progressCnt);
            }
            catch (FaceAPIException ce)
            {
                isSuccess = false;
                Debug.WriteLine("ClientException in CreateWhitelistFromFolderAsync : " + ce.ErrorCode);
                var log = new Log { message = "ClientException in CreateWhitelistFromFolderAsync : " + ce.ErrorCode };
                GuildWebApi.updateLog(log);
            }
            catch (Exception e)
            {
                isSuccess = false;
                Debug.WriteLine("Exception in CreateWhitelistFromFolderAsync : " + e.Message);
                var log = new Log { message = "Exception in CreateWhitelistFromFolderAsync : " + e.Message };
                GuildWebApi.updateLog(log);
            }

            // progress to 100%
            UpdateProgress(progress, 100);

            return isSuccess;
        }

        /// <summary>
        /// Use whitelist folder to build whitelist Database
        /// </summary>
        /// <returns></returns>
        private async Task BuildWhiteListAsync(IProgress<int> progress, double progressCnt)
        {
            Debug.WriteLine("Start building whitelist from " + _whitelistFolder.Path);
            var log = new Log { message = "Start building whitelist from " + _whitelistFolder.Path  };
            GuildWebApi.updateLog(log);


            var personas = await GuildWebApi.getWhiteList();
            var progressStep = (100.0 - progressCnt) / personas.Count;

            foreach (var persona in personas)
            {
                var personName = persona.name;

                // create new person
                var personId = await CreatePerson(personName);
                persona.faceApiId = personId;
                // iterate all images and add to whitelist
                foreach (var imageUrl in persona.imageUrls)
                {
                    Debug.WriteLine("BuildWhiteList: Processing " + imageUrl);
                    GuildWebApi.updateLog(new Log { message = "BuildWhiteList: Processing " + imageUrl });

                    try
                    {

                        var faceId = await DetectFaceFromImage(imageUrl);
                        Debug.WriteLine("Face identified: " + faceId);

                        await AddFace(personId, faceId, imageUrl);

                        Debug.WriteLine("This image added to whitelist successfully!");
                    }
                    catch (FaceRecognitionException fe)
                    {
                        switch (fe.ExceptionType)
                        {
                            case FaceRecognitionExceptionType.InvalidImage:
                                Debug.WriteLine("WARNING: This file is not a valid image!");
                                break;
                            case FaceRecognitionExceptionType.NoFaceDetected:
                                Debug.WriteLine("WARNING: No face detected in this image");
                                GuildWebApi.updateLog(new Log { message = "WARNING: No face detected in this image: " + imageUrl });
                                break;
                            //case FaceRecognitionExceptionType.MultipleFacesDetected:
                            //    Debug.WriteLine("WARNING: Multiple faces detected, ignored this image");
                            //    break;
                        }
                    }

                    // update progress
                    progressCnt += progressStep;
                    UpdateProgress(progress, progressCnt);
                }
            }
            PersonManager.personas = personas;
            await TrainingWhitelistAsync();

            Debug.WriteLine("Whitelist created successfully!");
            GuildWebApi.updateLog(new Log { message = "Whitelist created successfully!" });
        }
        #endregion

        #region Face
        public async Task AddFaceByUrl(Guid personId, string imageUrl)
        {
            await FaceApiClient.AddPersonFaceAsync(WhitelistId, personId, imageUrl);
            await Task.Delay(RATE_LIMIT);
        }
        /// <summary>
        /// Add face to both Cloud Face API and local whitelist
        /// </summary>
        /// <param name="personId"></param>
        /// <param name="faceId"></param>
        /// <param name="imagePath"></param>
        /// <returns></returns>
        private async Task AddFace(Guid personId, Guid faceId, string imageUrl)
        {
            await FaceApiClient.AddPersonFaceAsync(WhitelistId, personId, imageUrl);
            await Task.Delay(RATE_LIMIT);
            /*await Task.Run(async () =>
            {
                using (Stream imageStream = File.OpenRead(imagePath))
                {
                    await FaceApiClient.AddPersonFaceAsync(WhitelistId, personId, imageStream);
                    await Task.Delay(RATE_LIMIT);
                }
            });

            _whitelist.AddFace(personId, faceId, imagePath);*/
        }

        /// <summary>
        /// Remove face from both Cloud Face API and local whitelist
        /// </summary>
        /// <param name="personId"></param>
        /// <param name="faceId"></param>
        /// <returns></returns>
        private async Task RemoveFace(Guid personId, Guid faceId)
        {
            await FaceApiClient.DeletePersonFaceAsync(WhitelistId, personId, faceId);
            await Task.Delay(RATE_LIMIT);
            _whitelist.RemoveFace(personId, faceId);
        }

        /// <summary>
        /// Detect face and return the face id of a image file
        /// </summary>
        /// <param name="imageFile">
        /// image file to detect face
        /// Note: the image must only contains exactly one face
        /// </param>
        /// <returns>face id</returns>
        private async Task<Guid> DetectFaceFromImage(string imageUrl)
        {
            //var stream = await imageFile.OpenStreamForReadAsync();
            var faces = await FaceApiClient.DetectAsync(imageUrl);
            await Task.Delay(RATE_LIMIT);
            if (faces == null || faces.Length < 1)
            {
                throw new FaceRecognitionException(FaceRecognitionExceptionType.NoFaceDetected);
            }
            else if (faces.Length > 1)
            {
                throw new FaceRecognitionException(FaceRecognitionExceptionType.MultipleFacesDetected);
            }

            return faces[0].FaceId;
        }

        /// <summary>
        /// Detect face and return the face id of a image file
        /// </summary>
        /// <param name="imageFile">
        /// image file to detect face
        /// </param>
        /// <returns>face id</returns>
        private async Task<Guid[]> DetectFacesFromImage(StorageFile imageFile)
        {
            var stream = await imageFile.OpenStreamForReadAsync();
            var faces = await FaceApiClient.DetectAsync(stream);
            await Task.Delay(RATE_LIMIT);
            if (faces == null || faces.Length < 1)
            {
                throw new FaceRecognitionException(FaceRecognitionExceptionType.NoFaceDetected);
            }

            return FaceApiUtils.FacesToFaceIds(faces);
        }

  /*      public async Task<bool> AddImageToWhitelistAsync(StorageFile imageFile, string personName = null)
        {
            bool isSuccess = true;

            // imageFile should be valid image file
            if (!FaceApiUtils.ValidateImageFile(imageFile))
            {
                isSuccess = false;
            }
            else
            {
                var filePath = imageFile.Path;

                // If personName is null/empty, use the folder name as person name
                if (string.IsNullOrEmpty(personName))
                {
                    personName = await FaceApiUtils.GetParentFolderNameAsync(imageFile);
                }

                // If person name doesn't exists, add it
                var personId = _whitelist.GetPersonIdByName(personName);
                if (personId == Guid.Empty)
                {
                    var folder = await imageFile.GetParentAsync();
                    personId = await CreatePerson(personName);
                }

                // detect faces
                var faceId = await DetectFaceFromImage(imageFile);
                await AddFace(personId, faceId, imageFile.Path);

                // train whitelist
                isSuccess = await TrainingWhitelistAsync();
            }

            return isSuccess;
        }
*/
        public async Task<bool> RemoveImageFromWhitelistAsync(StorageFile imageFile, string personName = null)
        {
            bool isSuccess = true;
            if (!FaceApiUtils.ValidateImageFile(imageFile))
            {
                isSuccess = false;
            }
            else
            {
                // If personName is null use the folder name as person name
                if (string.IsNullOrEmpty(personName))
                {
                    personName = await FaceApiUtils.GetParentFolderNameAsync(imageFile);
                }

                var personId = _whitelist.GetPersonIdByName(personName);
                var faceId = _whitelist.GetFaceIdByFilePath(imageFile.Path);
                if (personId == Guid.Empty || faceId == Guid.Empty)
                {
                    isSuccess = false;
                }
                else
                {
                    await RemoveFace(personId, faceId);

                    // train whitelist
                    isSuccess = await TrainingWhitelistAsync();
                }
            }
            return isSuccess;
        }
        #endregion

        #region Person
        /// <summary>
        /// Create a person into Face API and whitelist
        /// </summary>
        /// <param name="personName"></param>
        /// <param name="personFolder"></param>
        /// <returns></returns>
        private async Task<Guid> CreatePerson(string personName)
        {
            try
            {
                await Task.Delay(RATE_LIMIT);
                var ret = await FaceApiClient.CreatePersonAsync(WhitelistId, personName);
                
                var personId = ret.PersonId;

                //_whitelist.AddPerson(personId, personName, personFolder.Path);

                return personId;
            }
            catch (FaceAPIException ex)
            {
                Debug.WriteLine("FaceAPIException" + ex.ErrorCode + " " + ex.ErrorMessage);
                var log = new Log { message = "FaceAPIException on creating " + personName + ex.ErrorCode + " " + ex.ErrorMessage };
                GuildWebApi.updateLog(log);
                throw ex;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                throw ex;
            }
        }

        private async Task RemovePerson(Guid personId)
        {
            await FaceApiClient.DeletePersonAsync(WhitelistId, personId);
            await Task.Delay(RATE_LIMIT);
            _whitelist.RemovePerson(personId);
        }

        /*public async Task<bool> AddPersonToWhitelistAsync(StorageFolder faceImagesFolder, string personName = null)
        {
            bool isSuccess = true;

            if (faceImagesFolder == null)
            {
                isSuccess = false;
            }
            else
            {
                // use folder name if do not have personName
                if (string.IsNullOrEmpty(personName))
                {
                    personName = faceImagesFolder.Name;
                }

                var personId = await CreatePerson(personName, faceImagesFolder);
                var files = await faceImagesFolder.GetFilesAsync();

                // iterate all files and add to whitelist
                foreach (var file in files)
                {
                    try
                    {
                        // detect faces
                        var faceId = await DetectFaceFromImage(file);
                        await AddFace(personId, faceId, file.Path);
                    }
                    catch (FaceRecognitionException fe)
                    {
                        switch (fe.ExceptionType)
                        {
                            case FaceRecognitionExceptionType.InvalidImage:
                                Debug.WriteLine("WARNING: This file is not a valid image!");
                                break;
                            case FaceRecognitionExceptionType.NoFaceDetected:
                                Debug.WriteLine("WARNING: No face detected in this image");
                                break;
                            case FaceRecognitionExceptionType.MultipleFacesDetected:
                                Debug.WriteLine("WARNING: Multiple faces detected, ignored this image");
                                break;
                        }
                    }
                }

                // train whitelist
                isSuccess = await TrainingWhitelistAsync();
            }

            return isSuccess;
        }
        */
        public async Task<bool> RemovePersonFromWhitelistAsync(string personName)
        {
            bool isSuccess = true;

            var personId = _whitelist.GetPersonIdByName(personName);
            if (personId == Guid.Empty)
            {
                isSuccess = false;
            }
            else
            {
                // remove all faces belongs to this person
                var faceIds = _whitelist.GetAllFaceIdsByPersonId(personId);
                if (faceIds != null)
                {
                    var faceIdsArr = faceIds.ToArray();
                    for (int i = 0; i < faceIdsArr.Length; i++)
                    {
                        await RemoveFace(personId, faceIdsArr[i]);
                    }
                }

                // remove person
                await RemovePerson(personId);

                // train whitelist
                isSuccess = await TrainingWhitelistAsync();
            }

            return isSuccess;
        }
        #endregion

        #region Face recognition
        public async Task<IdentifyResult[]> FaceRecognizeAsync(StorageFile imageFile)
        {
            var recogResult = new List<string>();

            if (!FaceApiUtils.ValidateImageFile(imageFile))
            {
                throw new FaceRecognitionException(FaceRecognitionExceptionType.InvalidImage);
            }

            // detect all faces in the image
            var faceIds = await DetectFacesFromImage(imageFile);

            ;
            // try to identify all faces to person
            var identificationResults = await FaceApiClient.IdentifyAsync(WhitelistId, faceIds);
            await Task.Delay(RATE_LIMIT);

            // add identified person name to result list
            foreach (var result in identificationResults)
            {
                if (result.Candidates.Length > 0)
                {
                    //var personName = _whitelist.GetPersonNameById(result.Candidates[0].PersonId);
                    Debug.WriteLine("Face ID Confidence: " + Math.Round(result.Candidates[0].Confidence * 100, 1) + "%");
                    var log = new Log { message = "Face ID Confidence: " + Math.Round(result.Candidates[0].Confidence * 100, 1) + "%", image = imageFile };
                    GuildWebApi.updateLog(log);
                    //recogResult.Add(personName);
                }
            }
            
            return (from result in identificationResults where result.Candidates.Length > 0 select result).ToArray();
        }
        #endregion
    }
}

