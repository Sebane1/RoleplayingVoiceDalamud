using FFXIVClientStructs.FFXIV.Client.Game;
using RoleplayingMediaCore;
using System.Numerics;

namespace RoleplayingVoiceDalamud {
    public class MediaCameraObject : IMediaGameObject {
        private unsafe Camera* _camera;

        public unsafe MediaCameraObject(Camera* camera) {
            this._camera = camera;
        }

        public string Name => "Camera";

        unsafe public Vector3 Position {
            get {
                return _camera->CameraBase.SceneCamera.Object.Position;
            }
        }

        unsafe public Vector3 Rotation {
            get {
                return _camera->CameraBase.SceneCamera.Object.Rotation.EulerAngles;
            }
        }

        unsafe public Vector3 Forward {
            get {
                var cameraViewMatrix = _camera->CameraBase.SceneCamera.ViewMatrix;
                return new Vector3(cameraViewMatrix.M13, cameraViewMatrix.M23, cameraViewMatrix.M33);
            }
        }

        unsafe public Vector3 Top {
            get {
                return _camera->CameraBase.SceneCamera.Vector_1; ;
            }
        }

        public string FocusedPlayerObject {
            get {
                return "";
            }
        }

        bool IMediaGameObject.Invalid => false;
    }
}
