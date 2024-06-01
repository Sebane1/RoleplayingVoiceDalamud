using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoleplayingVoiceDalamud.IPC {
    public interface IArtemisRoleplayingKitIPC {
        /// <summary>
        /// Gets api version.
        /// </summary>
        public int APIVersion { get; }

        /// <summary>
        /// Gets or sets a value indicating whether artemis API is initialized.
        /// </summary>
        public bool IsInitialized { get; }

        public string GetCacheFolder();

        /// <summary>
        /// Hook when an animation is triggered.
        /// </summary>
        /// <returns></returns>
        public bool OnAnimationTriggered(EventHandler<KeyValuePair<nint, ushort>> eventHandler);

        /// <summary>
        /// Hook when an animation is stopped.
        /// </summary>
        /// <returns></returns>
        public bool OnAnimationStopped(EventHandler<nint> eventHandler);

        /// <summary>
        /// Hook when the users voice pack is changed.
        /// </summary>
        /// <returns></returns>
        public bool OnVoicePackChanged(EventHandler eventHandler);

        /// <summary>
        /// Starts the specified animation id on the specified object
        /// </summary>
        /// <param name="objectId">The address of the GameObject to affect</param>
        /// <param name="animationId">The animation id to use</param>
        public bool DoAnimation(nint objectAddress, ushort animationId);

        /// <summary>
        /// Stops any animation currently playing on the specified game object.
        /// </summary>
        /// <param name="objectId">The address of the GameObject to affect</param>
        public bool StopAnimation(nint objectAddress);

        /// <summary>
        /// Starts a sound tied to the specified GameObject;
        /// </summary>
        /// <param name="objectAddress">The address of the GameObject to affect</</param>
        /// <param name="soundPath">The path of the desired sound</param>
        public bool PlaySound(nint objectAddress, string soundPath, int soundType);

        /// <summary>
        /// Stops a sound to the specified GameObject
        /// </summary>
        /// <param name="objectAddress">The gameobject sound</param>
        public bool StopSound(nint objectAddress);

    }
}
