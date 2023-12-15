using FFXIVClientStructs.FFXIV.Common.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vector3 = System.Numerics.Vector3;

namespace RoleplayingVoiceDalamud {
    public class MovingObject {
        Vector3 lastPosition;
        float lastRotation;
        bool isMoving = false;

        public MovingObject(Vector3 lastPosition, float lastRotation, bool isMoving) {
            this.lastPosition = lastPosition;
            this.lastRotation = lastRotation;
            this.isMoving = isMoving;
        }

        public bool IsMoving { get => isMoving; set => isMoving = value; }
        public float LastRotation { get => lastRotation; set => lastRotation = value; }
        public Vector3 LastPosition { get => lastPosition; set => lastPosition = value; }
    }
}
