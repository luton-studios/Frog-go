using UnityEngine;

public class AnimatedSkinPreview : MonoBehaviour {
    public Camera previewCamera;
    public FrogSkinHandler frogSkinRenderer;
    public int textureResolution = 128;
    public float renderAspect = 1f;

    public RenderTexture previewTexture { get; private set; }

    public void Init(FrogSkinData skinData) {
        frogSkinRenderer.UpdateVisuals(skinData);
        frogSkinRenderer.SetVisibility(false); // Hide until you open shop.

        previewTexture = new RenderTexture(textureResolution, textureResolution, 16, RenderTextureFormat.ARGB32);
        previewTexture.filterMode = FilterMode.Point;
        previewTexture.hideFlags = HideFlags.HideAndDontSave;
        previewTexture.autoGenerateMips = false;

        previewCamera.aspect = renderAspect;
        previewCamera.targetTexture = previewTexture;
        previewCamera.enabled = false;
    }
}