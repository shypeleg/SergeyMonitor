using DormRoomMonitor.FacialRecognition;
using Microsoft.ProjectOxford.Face.Contract;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Devices.Gpio;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace DormRoomMonitor
{
    public sealed partial class MainPage : Page
    {
        // Webcam Related Variables:
        private WebcamHelper webcam;

        // Oxford Related Variables:
        private bool initializedOxford = false;

        // Whitelist Related Variables:
        private List<Visitor> whitelistedVisitors = new List<Visitor>();
        private StorageFolder whitelistFolder;
        private bool currentlyUpdatingWhitelist;

        // Intruder Related Variables:
        private List<Visitor> intruderVisitors = new List<Visitor>();
        private StorageFolder intrudersFolder;
        private bool currentlyUpdatingIntruders;

        // Speech Related Variables:
        private SpeechHelper speech;

        // GPIO Related Variables:
        private GpioHelper gpioHelper;
        private bool gpioAvailable;
        private bool motionJustSensed = false;

        // GUI Related Variables:
        private double visitorIDPhotoGridMaxWidth = 0;
        private double intruderIDPhotoGridMaxWidth = 0;

        /// <summary>
        /// Called when the page is first navigated to.
        /// </summary>
        public MainPage()
        {
            InitializeComponent();

            // Causes this page to save its state when navigating to other pages
            NavigationCacheMode = NavigationCacheMode.Enabled;

            webcam = new WebcamHelper();
            // Check to see if Oxford facial recongition has been initialized
            if (initializedOxford == false)
            {
                // If not, attempt to initialize it
                InitializeOxford();
            }

            // Check to see if GPIO is available
            if (gpioAvailable == false)
            {
                // If not, attempt to initialize it
                InitializeGpio();
            }

            // If user has set the DisableLiveCameraFeed within Constants.cs to true, disable the feed.
            if (GeneralConstants.DisableLiveCameraFeed)
            {
                LiveFeedPanel.Visibility = Visibility.Collapsed;
                DisabledFeedGrid.Visibility = Visibility.Visible;
            }
            else
            {
                LiveFeedPanel.Visibility = Visibility.Visible;
                DisabledFeedGrid.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Triggered every time the page is navigated to.
        /// </summary>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (initializedOxford)
            {
                UpdateWhitelistedVisitors();
            }

            UpdateIntruderVisitors();
        }

        /// <summary>
        /// Called once, when the app is first opened. Initializes Oxford facial recognition.
        /// </summary>
        public async void InitializeOxford()
        {
            // initializedOxford bool will be set to true when Oxford has finished initialization successfully
            //await speech.Read(SpeechContants.INITIALIZING_WHITE_LIST);
            initializedOxford = await OxfordFaceAPIHelper.InitializeOxford();
            //await speech.Read(SpeechContants.FINISHED_INITIALIZING_WHITELIST);
            // Populates UI grid with whitelisted visitors
            //UpdateWhitelistedVisitors();

            // Populates UI grid with intruders
            //UpdateIntruderVisitors();
        }

        /// <summary>
        /// Called once, when the app is first opened. Initializes device GPIO.
        /// </summary>
        public void InitializeGpio()
        {
            try
            {
                // Attempts to initialize application GPIO. 
                gpioHelper = new GpioHelper();
                gpioAvailable = gpioHelper.Initialize();
            }
            catch
            {
                // This can fail if application is run on a device, such as a laptop, that does not have a GPIO controller.
                gpioAvailable = false;
                Debug.WriteLine("GPIO controller not available.");
                var log = new Log { message = "GPIO controller not available." };
                GuildWebApi.updateLog(log);

            }

            // If initialization was successful, attach motion sensor event handler
            if (gpioAvailable)
            {
                gpioHelper.GetPirSensor().ValueChanged += PirSensorChanged;
            }
        }

        /// <summary>
        /// Triggered when webcam feed loads both for the first time and every time this page is navigated to.
        /// If no WebcamHelper has been created, it creates one. Otherwise, simply restarts webcam preview feed on page.
        /// </summary>
        private async void WebcamFeed_Loaded(object sender, RoutedEventArgs e)
        {
          /*  if (webcam == null || !webcam.IsInitialized())
            {
                // Initialize Webcam Helper
                webcam = new WebcamHelper();
                /*await webcam.InitializeCameraAsync();

                // Set source of WebcamFeed on MainPage.xaml
                WebcamFeed.Source = webcam.arrivingMediaCapture;

                // Check to make sure MediaCapture isn't null before attempting to start preview. Will be null if no camera is attached.
                if (WebcamFeed.Source != null)
                {
                    // Start the live feed
                    await webcam.StartCameraPreview();
                }
            }
            else if (webcam.IsInitialized())
            {
               /* WebcamFeed.Source = webcam.arrivingMediaCapture;

                // Check to make sure MediaCapture isn't null before attempting to start preview. Will be null if no camera is attached.
                if (WebcamFeed.Source != null)
                {
                    await webcam.StartCameraPreview();
                }
            }*/
        }

        /// <summary>
        /// Triggered when media element used to play synthesized speech messages is loaded.
        /// Initializes SpeechHelper and greets user.
        /// </summary>
        private async void speechMediaElement_Loaded(object sender, RoutedEventArgs e)
        {
            if (speech == null)
            {
                speech = new SpeechHelper(speechMediaElement);
                await speech.Read(SpeechContants.InitialGreetingMessage);
            }
            else
            {
                // Prevents media element from re-greeting visitor
                speechMediaElement.AutoPlay = false;
            }
        }

        /// <summary>
        /// Triggered when the whitelisted users grid is loaded. Sets the size of each photo within the grid.
        /// </summary>
        private void WhitelistedUsersGrid_Loaded(object sender, RoutedEventArgs e)
        {
            visitorIDPhotoGridMaxWidth = (WhitelistedUsersGrid.ActualWidth / 3) - 10;
        }

        /// <summary>
        /// Triggered when the intruders grid is loaded. Sets the size of each photo within the grid.
        /// </summary>
        private void IntrudersGrid_Loaded(object sender, RoutedEventArgs e)
        {
            intruderIDPhotoGridMaxWidth = (IntrudersGrid.ActualWidth / 3) - 10;
        }

        /// <summary>
        /// Triggered when motion sensor changes - someone either enters room or someone exits room
        /// </summary>
        private async void PirSensorChanged(GpioPin sender, GpioPinValueChangedEventArgs args)
        {
            if (!motionJustSensed)
            {
                // Checks to see if event was triggered from an entry (rising edge) or exit (falling edge)
                if (args.Edge == GpioPinEdge.RisingEdge)
                {
                    //motion sensor was just triggered
                    motionJustSensed = true;

                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                    {
                        await SomeoneEnteredOrLeft();
                    });

                }
            }
        }

        /// <summary>
        /// Triggered when user presses virtual motion button on the app bar
        /// </summary>
        private async void MotionButton_Click(object sender, RoutedEventArgs e)
        {
            if (!motionJustSensed)
            {
                //motion sensor was just triggered
                motionJustSensed = true;
                await SomeoneEnteredOrLeft();
            }
        }

        /// <summary>
        /// Called when someone enters room or vitual motion button is pressed.
        /// Captures photo of current webcam view and sends it to Oxford for facial recognition processing.
        /// </summary>
        private async Task SomeoneEnteredOrLeft()
        {
            try
            {
                // Display analysing visitors grid to inform user that motion was sensed
                AnalysingVisitorGrid.Visibility = Visibility.Visible;

                // List to store visitors recognized by Oxford Face API
                // Count will be greater than 0 if there is an authorized visitor at the door
                
                // Confirms that webcam has been properly initialized and oxford is ready to go
                if (webcam.IsInitialized() && initializedOxford)
                {
                    // Announce that an intruder has been detected
                    //await speech.Read(SpeechContants.IntruderDetectedMessage);

                    // get pictures from cameras
                    var pics = await webcam.InitializeAndCapturePhotos();

                    if (pics == null)
                    {
                        await speech.Read(SpeechContants.NO_PICS);
                        motionJustSensed = false;
                        return;
                    }
                    // Oxford determines whether or not the visitor is on the Whitelist and returns recongized visitor if so
                    var recognizedArriving = await OxfordFaceAPIHelper.IsFaceInWhitelist(pics.arriving);
                    var recognizedDeparting = await OxfordFaceAPIHelper.IsFaceInWhitelist(pics.departing);


                    if (recognizedArriving != null && recognizedArriving.Length > 0)
                    {
                        postCandidatesAttendance(recognizedArriving, true);

                        // If everything went well and a visitor was recognized, allow the person entry
                        var recognizedName = PersonManager.getPersonById(recognizedArriving[0].Candidates[0].PersonId).name;
                        await speech.Read(SpeechContants.AllowedEntryMessage(recognizedName));

                        Debug.WriteLine("authorized on front camera!");
                        var log = new Log { message = recognizedName + " authorized on front camera!", image = pics.arriving };
                        GuildWebApi.updateLog(log);

                        // post to tala!
                    }
                    if (recognizedDeparting != null && recognizedDeparting.Length > 0)
                    {
                        postCandidatesAttendance(recognizedDeparting, false);
                        // If everything went well and a visitor was recognized, allow the person entry
                        var recognizedName = PersonManager.getPersonById(recognizedDeparting[0].Candidates[0].PersonId).name;

                        await speech.Read(SpeechContants.ByeMessage(recognizedName));
                        Debug.WriteLine("authorized on back camera!");
                        var log = new Log { message = recognizedName + " authorized on back camera!", image = pics.departing };
                        GuildWebApi.updateLog(log);

                        // post to tala!
                    }
                }
                motionJustSensed = false;
                AnalysingVisitorGrid.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                await speech.Read(SpeechContants.exception(ex.Message, "someone entered function"));
            }
        }
        private async Task postCandidatesAttendance (IdentifyResult[] results, bool entered)
        {
            foreach (var result in results)
            {
                var person = PersonManager.getPersonById(result.Candidates[0].PersonId);
                var attendance = new Attendance { email = person.email, entered = entered, location = SpeechContants.LOCATION };
                Debug.WriteLine("entred? " + attendance.entered);
                await GuildWebApi.updateAttendance(attendance);
            }
        }
        /// <summary>
        /// Allows the person entry into the room
        /// </summary>
        private async Task AllowEntry(string visitorName)
        {
            // Greet visitor
            await speech.Read(SpeechContants.AllowedEntryMessage(visitorName));
        }
        private async Task SayGoodbye(string visitorName)
        {
            // Greet visitor
            await speech.Read(SpeechContants.ByeMessage(visitorName));
        }
        

        /// <summary>
        /// Called when user hits vitual add user button. Navigates to NewUserPage page.
        /// </summary>
        private async void NewUserButton_Click(object sender, RoutedEventArgs e)
        {
            // Stops camera preview on this page, so that it can be started on NewUserPage
            await webcam.StopCameraPreview();

            //Navigates to NewUserPage, passing through initialized WebcamHelper object
            Frame.Navigate(typeof(NewUserPage), webcam);
        }

        /// <summary>
        /// Updates internal list of whitelisted visitors (whitelistedVisitors) and the visible UI grid
        /// </summary>
        private async void UpdateWhitelistedVisitors()
        {
            // If the whitelist isn't already being updated, update the whitelist
            if (!currentlyUpdatingWhitelist)
            {
                currentlyUpdatingWhitelist = true;
                await UpdateWhitelistedVisitorsList();
                UpdateWhitelistedVisitorsGrid();
                currentlyUpdatingWhitelist = false;
            }
        }

        /// <summary>
        /// Updates the list of Visitor objects with all whitelisted visitors stored on disk
        /// </summary>
        private async Task UpdateWhitelistedVisitorsList()
        {
            // Clears whitelist
            whitelistedVisitors.Clear();

            // If the whitelistFolder has not been opened, open it
            if (whitelistFolder == null)
            {
                // Create the whitelistFolder if it doesn't exist; if it already exists, open it.
                whitelistFolder = await KnownFolders.PicturesLibrary.CreateFolderAsync(GeneralConstants.WhiteListFolderName, CreationCollisionOption.OpenIfExists);
            }

            // Populate subFolders list with all sub folders within the whitelist folder.
            // Each of these sub folders represents the Id photos for a single visitor.
            var whitelistSubFolders = await whitelistFolder.GetFoldersAsync();

            // Iterate all subfolders in whitelist
            foreach (StorageFolder folder in whitelistSubFolders)
            {
                // Get each visitor's name from the folder name
                string whitelistVisitorName = folder.Name;

                // Get the files from each folder
                var filesInWhitelistFolder = await folder.GetFilesAsync();

                // Use the first photo in the folder as the visitors image for the whitelist
                var whitelistPhotoStream = await filesInWhitelistFolder[0].OpenAsync(FileAccessMode.Read);
                BitmapImage visitorImage = new BitmapImage();
                await visitorImage.SetSourceAsync(whitelistPhotoStream);

                // Create the Visitor object will all the information about the visitor
                Visitor whitelistedVisitor = new Visitor(whitelistVisitorName, folder, visitorImage, visitorIDPhotoGridMaxWidth);

                // Add the visitor to the white list
                whitelistedVisitors.Add(whitelistedVisitor);
            }
        }

        /// <summary>
        /// Updates UserInterface list of whitelisted users from the list of Visitor objects (whitelistedVisitors)
        /// </summary>
        private void UpdateWhitelistedVisitorsGrid()
        {
            // Reset source to empty list
            WhitelistedUsersGrid.ItemsSource = new List<Visitor>();

            // Set source of WhitelistedUsersGrid to the whitelistedVisitors list
            WhitelistedUsersGrid.ItemsSource = whitelistedVisitors;

            // Hide Oxford loading ring
            OxfordLoadingRing.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Triggered when the user selects a visitor in the WhitelistedUsersGrid 
        /// </summary>
        private void WhitelistedUsersGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            // Navigate to UserProfilePage, passing through the selected Visitor object and the initialized WebcamHelper as a parameter
            Frame.Navigate(typeof(UserProfilePage), new UserProfileObject(e.ClickedItem as Visitor, webcam));
        }

        /// <summary>
        /// Updates maintained list of intruders (intruderVisitors) and the visible UI grid
        /// </summary>
        private async void UpdateIntruderVisitors()
        {
            // If the list of intruders isn't already being updated, update the list
            if (!currentlyUpdatingIntruders)
            {
                currentlyUpdatingIntruders = true;
                //await UpdateIntruderVisitorsList();
                UpdateIntruderVisitorsGrid();
                currentlyUpdatingIntruders = false;
            }
        }

        /// <summary>
        /// Updates the list of Visitor objects with all whitelisted visitors stored on disk
        /// </summary>
        private async Task UpdateIntruderVisitorsList()
        {
            // Clear list of intruders
            intruderVisitors.Clear();

            // If the intrudersFolder has not been opened, open it
            if (intrudersFolder == null)
            {
                // Create the intrudersFolder if it doesn't exist; if it already exists, open it.
                intrudersFolder = await KnownFolders.PicturesLibrary.CreateFolderAsync(GeneralConstants.IntruderFolderName, CreationCollisionOption.OpenIfExists);
            }

            // Populates intruderSubFolders list with all sub folders within the intruder folder.
            // Each of these sub folders represents the Id photos for a single intruder.
            var intruderSubFolders = await intrudersFolder.GetFoldersAsync();

            // Iterate all subfolders in whitelist
            foreach (StorageFolder folder in intruderSubFolders)
            {
                // Get each visitor's name from the folder name
                string intruderName = folder.Name;

                // Get the files from each folder
                var filesInIntruderFolder = await folder.GetFilesAsync();

                // Use the first photo in the folder as the visitors image for the whitelist
                var intruderPhotoStream = await filesInIntruderFolder[0].OpenAsync(FileAccessMode.Read);
                BitmapImage intruderImage = new BitmapImage();
                await intruderImage.SetSourceAsync(intruderPhotoStream);

                // Create the Visitor object will all the information about the visitor
                Visitor intruderVisitor = new Visitor(intruderName, folder, intruderImage, intruderIDPhotoGridMaxWidth);

                // Add the visitor to the white list
                intruderVisitors.Add(intruderVisitor);
            }
        }

        /// <summary>
        /// Updates UserInterface list of intruders from the list of Visitor objects (intruderVisitors)
        /// </summary>
        private void UpdateIntruderVisitorsGrid()
        {
            // Reset source to empty list
            IntrudersGrid.ItemsSource = new List<Visitor>();

            // Set source of WhitelistedUsersGrid to the whitelistedVisitors list
            IntrudersGrid.ItemsSource = intruderVisitors;

            // Hide Oxford loading ring
            IntrudersLoadingRing.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Triggered when the user selects an intruder in the IntrudersGrid 
        /// </summary>
        private void IntrudersGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            // Navigate to IntruderProfilePage, passing through the selected Visitor object and the initialized WebcamHelper as a parameter
            Frame.Navigate(typeof(IntruderProfilePage), new UserProfileObject(e.ClickedItem as Visitor, webcam));
        }

        /// <summary>
        /// Triggered when the user selects the Shutdown button in the app bar. Closes app.
        /// </summary>
        private void ShutdownButton_Click(object sender, RoutedEventArgs e)
        {
            // Exit app
            Application.Current.Exit();
        }
    }
}
