using RoleplayingMediaCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace RoleplayingVoiceDalamud {
    internal class DummyObject : IMediaGameObject {
        public DummyObject() {
            Name = Guid.NewGuid().ToString();
        }

        public string Name { get; set; }

        public Vector3 Position => new Vector3();

        public Vector3 Rotation => new Vector3();

        public Vector3 Forward => new Vector3();

        public Vector3 Top => new Vector3();

        public string FocusedPlayerObject => "";

        bool IMediaGameObject.Invalid => false;
    }
}
