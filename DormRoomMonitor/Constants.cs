namespace DormRoomMonitor
{
    /// <summary>
    /// General constant variables
    /// </summary>
    public static class GeneralConstants
    {
        // With no GPU support, the Raspberry Pi cannot display the live camera feed so this variable should be set to true.
        // However, if you are deploying to other harware on which Windows 10 IoT Core does have GPU support, set it to false.
        public const bool DisableLiveCameraFeed = true;

        // Oxford Face API Primary should be entered here
        // You can obtain a subscription key for Face API by following the instructions here: https://www.microsoft.com/cognitive-services/en-us/sign-up
        //public const string OxfordAPIKey = "0032f38669754c46bf2716001f00837b"; //free
        public const string OxfordAPIKey = "886b120ab1ff46788092abd2b4217110";
        // region you define when getting the api key (for example - to query europe servers)
        public const string DEFAULT_API_ROOT = "https://westcentralus.api.cognitive.microsoft.com/face/v1.0";

        // Name of the folder in which all Whitelist data is stored
        public const string WhiteListFolderName = "Sergey_Monitor_Whitelist";

        // Name of the folder in which all the intruder data is stored
        public const string IntruderFolderName = "Dorm Room Monitor Intruders";
    }

    /// <summary>
    /// Constant variables that hold messages to be read via the SpeechHelper class
    /// </summary>
    public static class SpeechContants
    {
        public const string INITIALIZING_WHITE_LIST = "Initializing known people";
        public const string FINISHED_INITIALIZING_WHITELIST = "Finished training known people";
        public const string NO_PICS = "Camera Error";
        public const string LOCATION = "MAIN BUILDING SECOND FLOOR";
        public const string GET_WHITE_LIST_URL = "https://tala734.wixsite.com/where2/_functions/personas/";
        public const string POST_ATTENDANCE_URL = "https://tala734.wixsite.com/where2/_functions/attendance";
        public const string POST_LOG_URL = "https://tala734.wixsite.com/where2/_functions/log";
        public const string InitialGreetingMessage = "Sergey monitor has been activated.";
        public const string IntruderDetectedMessage = "Intruder detected.";
        public const string NotAllowedEntryMessage = "Sorry! I don't recognize you. You are not authorized to be here.";
        public const string NoCameraMessage = "Sorry! It seems like your camera has not been fully initialized.";
        public const bool MockAPI = false;
        public static string AllowedEntryMessage(string visitorName)
        {
            return "Hello " + visitorName;
        }
        public static string ByeMessage(string visitorName)
        {
            return "Goodbye " + visitorName;
        }
        public static string exception(string message, string where)
        {
            return "Exception at " + where + ". " + message;
        }
    }

    /// <summary>
    /// Constant variables that hold values used to interact with device Gpio
    /// </summary>
    public static class GpioConstants
    {
        // The GPIO pin that the PIR motion sensor is attached to
        public const int PirPin = 5;
    }
}

