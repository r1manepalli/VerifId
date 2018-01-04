namespace VerifId
{
    public class AppConstants
    {
        private static string CognitiveServicesRegion = "your_service_region";
        public static string FaceServicesBaseUrl = $"https://{CognitiveServicesRegion}.api.cognitive.microsoft.com/face/v1.0";
        public static string FaceApiSubscriptionKey = "your_face_api_key";

        // Person Group is generally your company name
        // Allowed characters for PersonGroup
        //   lower case letters, numerals, -, _
        public static string VerifId_PersonGroup = "your_person_group";
    }
}
