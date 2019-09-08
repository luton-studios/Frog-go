using UnityEngine;

public enum FrogSkinParticles { None = 0, Inferno_FireSparks, Bill_DollarBills = 2 };

public class FrogSkinHandler : MonoBehaviour {
    public GameObject cachedGo;
    public Renderer frogRenderer;
    public GameObject[] skinParticles;

    public void UpdateVisuals(FrogSkinData data) {
        frogRenderer.material = data.skinMaterial;

        // Toggle particle visibility based on skin data.
        int particlesIndex = (int)data.particles - 1;

        for(int i = 0; i < skinParticles.Length; i++) {
            skinParticles[i].SetActive(i == particlesIndex);
        }
    }

    public void SetVisibility(bool on) {
        cachedGo.SetActive(on);
    }
}

[System.Serializable]
public class FrogSkinData {
    public Material skinMaterial;
    public FrogSkinParticles particles = FrogSkinParticles.None;
}