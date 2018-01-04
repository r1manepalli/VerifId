using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace VerifId
{
    public sealed partial class MainPage : Page
    {
        private string[] PersonNames = new string[5];
        private Guid[] PersonIds = new Guid[5];

        private Camera camera = new Camera();

        private string PersonGroup { get; } = AppConstants.VerifId_PersonGroup;  // Guid.NewGuid().ToString();
        private string Person { get; set; } = "Default Person";  // Guid.NewGuid().ToString();
        private Guid PersonId { get; set; }

        private readonly SolidColorBrush whiteBrush = new SolidColorBrush(Colors.White);
        private readonly SolidColorBrush redBrush = new SolidColorBrush(Colors.Red);
        private readonly SolidColorBrush greenBrush = new SolidColorBrush(Colors.Green);

        private FaceServiceClient faceServiceClient;

        public MainPage()
        {
            this.InitializeComponent();

            TxtTitle.Text += PersonGroup + "'";
            Prepare2InitCam();
            BtnInitCam.IsEnabled = false;
            PopulatePersonsList();
            CbPersons.ItemsSource = PersonNames;
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Into Page_Loaded");

            // Init Face Service Client
            faceServiceClient = new FaceServiceClient(AppConstants.FaceApiSubscriptionKey,
                AppConstants.FaceServicesBaseUrl);

            // Clean-up possible PersonGroup objects (including persons attached to them)
            try
            {
                var pgList = await faceServiceClient.ListPersonGroupsAsync();

                foreach (var pg in pgList)
                {
                    await faceServiceClient.DeletePersonGroupAsync(pg.PersonGroupId);
                }

                // Init PersonGroup
                Debug.WriteLine($"Initializing Person Group: {PersonGroup}");

                await faceServiceClient.CreatePersonGroupAsync(PersonGroup, PersonGroup);

                BtnInitCam.IsEnabled = true;
            }
            catch (FaceAPIException ex)
            {
                Debug.WriteLine($"Error deleting old PersonGroup objects: {ex.ErrorMessage}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in Page_Loaded: {ex.Message}");
            }

            Debug.WriteLine("Out of: Page_Loaded");
        }

        private void PopulatePersonsList()
        {
            for (int i = 0; i < 5; i++)
                PersonNames[i] = String.Format("Person {0}", i + 1);
        }

        private void Prepare2InitCam()
        {
            SpPreview.Visibility = Visibility.Collapsed;
            SpManagePersons.Visibility = Visibility.Collapsed;
            SpAccessControl.Visibility = Visibility.Collapsed;
        }

        private async void BtnInitCam_Click(object sender, RoutedEventArgs e)
        {
            SpPreview.Visibility = Visibility.Visible;
            BtnExitCam.IsEnabled = false;

            BtnInitCam.IsEnabled = false;
            BtnExitCam.IsEnabled = true;
            CbPersons.IsEnabled = false;
            CbPersons.SelectedIndex = -1;
            CbPersons.IsEnabled = true;
            SpPicture1.Visibility = Visibility.Collapsed;
            SpPicture2.Visibility = Visibility.Collapsed;

            // Initialize the camera and start previewing
            camera = new Camera();
            await camera.InitializeCameraAsync();
            PreviewElement.Source = camera.MyMediaCapture;
            await camera.MyMediaCapture.StartPreviewAsync();

            SpManagePersons.Visibility = Visibility.Visible;
        }

        private void BtnExitCam_Click(object sender, RoutedEventArgs e)
        {
            // Close cam
            camera.MyMediaCapture.Dispose();

            Prepare2InitCam();
            BtnInitCam.IsEnabled = true;
        }

        private async void CbPersons_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int iPerson = ((ComboBox)sender).SelectedIndex;

            if (iPerson >= 0)
            {
                CbPersons.IsEnabled = false;

                Person = PersonNames[iPerson];

                if (PersonIds[iPerson] == Guid.Empty)
                {
                    PersonId = (await faceServiceClient.CreatePersonAsync(PersonGroup, Person)).PersonId;
                    PersonIds[iPerson] = PersonId;
                }
                else
                {
                    PersonId = PersonIds[iPerson];
                }

                SpPicture1.Visibility = Visibility.Visible;
                SpPicture2.Visibility = Visibility.Collapsed;
                BtnTakePic1.IsEnabled = true;
            }
        }

        private void UpdateTrainingStatus(string msg)
        {
            txtTrainingStatus.Text = msg;
        }

        private async void BtnTakePic1_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("In BtnTakePic1_Click");
            UpdateTrainingStatus("Saving Image 1 ...");

            BtnTakePic1.IsEnabled = false;
            BtnTakePic2.IsEnabled = false;

            // Take Pic 1 and save it
            bool imageStored = await StorePersonFace(ImgCaptured1);

            if (imageStored)
            {
                Debug.WriteLine("Image 1 stored successfully");
                UpdateTrainingStatus("Image 1 stored");

                SpPicture2.Visibility = Visibility.Visible;
                BtnTakePic2.IsEnabled = true;
            }
            else
            {
                Debug.WriteLine("Image 1 NOT stored");
                UpdateTrainingStatus("Image 1 NOT saved");

                CbPersons.SelectedIndex = -1;
                CbPersons.IsEnabled = true;
                SpPicture1.Visibility = Visibility.Collapsed;
            }
        }

        private async Task<bool> StorePersonFace(Image captureImage)
        {
            Debug.WriteLine("In StorePersonFace");

            bool personStored = false;

            try
            {
                Stream stream = await TakePicture(captureImage);
                if (stream != null)
                {
                    var faceStored = await faceServiceClient.AddPersonFaceAsync(PersonGroup,
                        PersonId, stream);
                    personStored = true;
                }
            }
            catch (FaceAPIException ex)
            {
                if (!ex.ErrorCode.Contains("InvalidImage"))
                {
                    Debug.WriteLine($"Exception in StorePersonFace {ex.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error: {ex.Message}");
            }

            return personStored;
        }

        private async Task<Stream> TakePicture(Image captureImage)
        {
            Debug.WriteLine("In TakePicture");

            IRandomAccessStream photoStream = null;

            try
            {
                photoStream = new InMemoryRandomAccessStream();
                await camera.MyMediaCapture.CapturePhotoToStreamAsync(
                    ImageEncodingProperties.CreateJpeg(), photoStream);
                photoStream.Seek(0L);
                var bitmap = new BitmapImage();
                bitmap.SetSource(photoStream);
                captureImage.Source = bitmap;
                photoStream.Seek(0L);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in TakePicture: {ex.Message}");
            }

            return photoStream?.AsStream();
        }

        private async void BtnTakePic2_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("In BtnTakePic2_Click");
            UpdateTrainingStatus("Saving Image 2 ...");

            BtnTakePic2.IsEnabled = false;

            // Take pic 2 and save it
            bool imageStored = await StorePersonFace(ImgCaptured2);

            if (imageStored)
            {
                Debug.WriteLine("Image 2 stored successfully");
                UpdateTrainingStatus("Image 2 saved");

                // Train Face Services
                bool trainingSucceeded = await TrainModel();

                if (trainingSucceeded)
                {
                    Debug.WriteLine("Images Trained successfully");
                    UpdateTrainingStatus("Images trained");

                    SpAccessControl.Visibility = Visibility.Visible;
                    BtnAccessRequest.IsEnabled = true;
                }
                else
                {
                    UpdateTrainingStatus("Images NOT trained");
                    CbPersons.SelectedIndex = -1;
                }
            }
            else
            {
                Debug.WriteLine("Image 2 NOT stored");
                UpdateTrainingStatus("Image 2 NOT stored");

                CbPersons.SelectedIndex = -1;
            }

            CbPersons.IsEnabled = true;
            SpPicture1.Visibility = Visibility.Collapsed;
            SpPicture2.Visibility = Visibility.Collapsed;
        }

        private async Task<bool> TrainModel()
        {
            bool trainingSucceeded = false;

            TrainingStatus trainingStatus = null;

            try
            {
                await faceServiceClient.TrainPersonGroupAsync(PersonGroup);

                while (true)
                {
                    trainingStatus = await faceServiceClient.GetPersonGroupTrainingStatusAsync(PersonGroup);

                    if (trainingStatus.Status != Status.Running)
                    {
                        break;
                    }

                    await Task.Delay(1000);
                }
            }
            catch (FaceAPIException ex)
            {
                Debug.WriteLine($"Error training the model: {ex.ErrorMessage}");
            }

            if (trainingStatus.Status == Status.Succeeded)
            {
                trainingSucceeded = true;
            }

            return trainingSucceeded;
        }

        private async void BtnAccessRequest_Click(object sender, RoutedEventArgs e)
        {
            BtnAccessRequest.IsEnabled = false;

            // Capture image, analyze it and decide whether to access control
            RectAccessStatus.Fill = whiteBrush;
            int iPerson = await IdentifyPerson(ImgAccessRequestFor);
            RectAccessStatus.Fill = iPerson >= 0 ? greenBrush : redBrush;
            TxtAccessStatus.Text = iPerson >= 0 ? "Thank you! " + PersonNames[iPerson] : "Sorry! User";

            BtnAccessRequest.IsEnabled = true;
        }

        private async Task<int> IdentifyPerson(Image captureImage)
        {
            int iPerson = -1;

            try
            {
                var stream = await TakePicture(captureImage);
                if (stream != null)
                {
                    // Detect and identify a face through the Face API cognitive service
                    var faces = await faceServiceClient.DetectAsync(stream);
                    var faceIds = faces.Select(face => face.FaceId).ToArray();
                    var results = await faceServiceClient.IdentifyAsync(PersonGroup, faceIds);
                    var identifyResult = results.FirstOrDefault();

                    // Check if the result contains the person id of our reference person
                    if (identifyResult.Candidates.Length != 0)
                    {
                        iPerson = SearchPersons(identifyResult.Candidates[0].PersonId);
                    }

                }
            }
            catch (FaceAPIException ex)
            {
                if (!ex.ErrorCode.Contains("InvalidImage"))
                {
                    Debug.WriteLine($"Error identifying a person: {ex.ErrorMessage}");
                }
            }

            return iPerson;
        }

        private int SearchPersons(Guid personId)
        {
            int iPerson = -1;

            for (int i = 0; i < 5; i++)
            {
                if (PersonIds[i] == personId)
                {
                    iPerson = i;
                    break;
                }
            }

            return iPerson;
        }
    }  // end of: class MainPage

}  // end of: namespace
