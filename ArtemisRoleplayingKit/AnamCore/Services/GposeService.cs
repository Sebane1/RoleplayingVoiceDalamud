// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis.Services;

using System;
using System.Threading.Tasks;
using Anamnesis.Core.Memory;
using Anamnesis.Memory;


public delegate void GposeEvent(bool newState);


public class GposeService : ServiceBase<GposeService> {
    private bool initialized = false;

    public static event GposeEvent? GposeStateChanged;

    public bool IsGpose { get; private set; }

    //[DependsOn(nameof(IsGpose))]
    public bool IsOverworld => !this.IsGpose;

    public static bool GetIsGPose() {
        return false;
    }

    public override Task Start() {
       // Task.Run(this.CheckThread);
        return base.Start();
    }

    private async Task CheckThread() {
        while (this.IsAlive) {
            bool newGpose = GetIsGPose();

            if (!this.initialized) {
                this.initialized = true;
                this.IsGpose = newGpose;
                continue;
            }

            if (newGpose != this.IsGpose) {
                this.IsGpose = newGpose;
                GposeStateChanged?.Invoke(newGpose);
            }

            // ~30 fps
            await Task.Delay(32);
        }
    }
}
