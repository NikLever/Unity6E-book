using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[Serializable, VolumeComponentMenuForRenderPipeline("Sample Scene/Terminal", typeof(UniversalRenderPipeline))]
public class SphereVolumeComponent : VolumeComponent, IPostProcessComponent
{
    // For example, an intensity parameter that goes from 0 to 1
    public ClampedFloatParameter intensity = new ClampedFloatParameter(value: 0, min: 0, max: 1, overrideState: true);

    // Tells when our effect should be rendered
    public bool IsActive() => intensity.value > 0;
}