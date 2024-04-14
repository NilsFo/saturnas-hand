using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConnectionParticleManager : MonoBehaviour
{
    [Header("Hookup")] public ParticleSystem myParticles;

    [Header("Particle Intensity")] [Range(0f, 1f)]
    public float intensity = 0f;

    [Header("Config")] public int emissionMin = 5;
    public int emissionMax = 120;


    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        float f = Mathf.Clamp(intensity, 0f, 1f);

        ParticleSystem.EmissionModule myParticlesEmission = myParticles.emission;
        myParticlesEmission.rate = Mathf.Lerp(emissionMin,emissionMax,f);
    }
}