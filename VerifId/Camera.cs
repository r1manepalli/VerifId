using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Media.Capture;

namespace VerifId
{
    public class Camera
    {
        public MediaCapture MyMediaCapture { get; private set; }

        public Camera()
        {
            MyMediaCapture = new MediaCapture();
        }

        public async Task InitializeCameraAsync()
        {
            var myCamera = (await DeviceInformation.
                FindAllAsync(DeviceClass.VideoCapture)).FirstOrDefault();

            await MyMediaCapture?.InitializeAsync(
                new MediaCaptureInitializationSettings
                {
                    VideoDeviceId = myCamera.Id
                });
        }
    }
}
